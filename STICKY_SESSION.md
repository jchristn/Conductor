# Sticky Session / Endpoint Pinning Implementation Plan

This document describes the plan for implementing sticky session support (endpoint pinning) on the **Virtual Model Runner** to minimize context drops and model swapping when a requestor is proxied to backend model endpoints.

---

## Problem Statement

Today, every request to a Virtual Model Runner independently selects a backend endpoint using the configured load balancing mode (RoundRobin, Random, or FirstAvailable). There is no affinity between a requestor and a previously-selected endpoint. This means consecutive requests from the same client can be routed to different backend endpoints, causing:

- **Context drops**: Models that maintain conversational context in-memory lose that context when the next request is routed elsewhere.
- **Model swapping**: If different endpoints host different model versions or configurations, the requestor may experience inconsistent behavior across requests.

## Solution Overview

Add a **session affinity** capability to the Virtual Model Runner that, when enabled, pins a requestor to a specific backend endpoint for a configurable duration. The client identity is derived from a configurable key (IP address, API key, or a custom request header), and the mapping is maintained in an in-memory, thread-safe cache with TTL-based expiration.

When the pinned endpoint becomes unhealthy or is at capacity, the system gracefully falls back to standard load balancing and establishes a new pin.

---

## 1. Backend Changes

### 1.1 New Enum: `SessionAffinityModeEnum`

**File**: `src/Conductor.Core/Enums/SessionAffinityModeEnum.cs` (new file)

```
None = 0          No session affinity (current behavior, default)
SourceIP = 1      Pin by client IP address (from X-Forwarded-For or direct connection)
ApiKey = 2        Pin by the Authorization/Bearer token or API key presented
Header = 3        Pin by a custom request header value (specified by SessionAffinityHeader)
```

This enum controls how the client identity key is derived for the session affinity lookup.

---

### 1.2 Model Changes: `VirtualModelRunner`

**File**: `src/Conductor.Core/Models/VirtualModelRunner.cs`

Add four new properties with backing fields and XML documentation:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SessionAffinityMode` | `SessionAffinityModeEnum` | `None` | How session affinity is determined. `None` disables sticky sessions entirely. |
| `SessionAffinityHeader` | `string` | `null` | When `SessionAffinityMode` is `Header`, this specifies the request header name to use as the client identity key (e.g., `X-Session-Id`). Ignored for other modes. |
| `SessionTimeoutMs` | `int` | `600000` | Duration in milliseconds that a session pin remains active since last use. Minimum: 60000 (1 minute). Maximum: 86400000 (24 hours). Refreshed on each request. |
| `SessionMaxEntries` | `int` | `10000` | Maximum number of concurrent session entries per VMR before oldest entries are evicted. Minimum: 100. Maximum: 1000000. Protects memory. |

**Backing fields**:
- `_SessionTimeoutMs` with clamping: `value < 60000 ? 60000 : (value > 86400000 ? 86400000 : value)`
- `_SessionMaxEntries` with clamping: `value < 100 ? 10000 : (value > 1000000 ? 1000000 : value)`

**`FromDataRow`**: Add parsing for the four new columns:
- `SessionAffinityMode` via `DataTableHelper.GetEnumValue<SessionAffinityModeEnum>(row, "sessionaffinitymode", SessionAffinityModeEnum.None)`
- `SessionAffinityHeader` via `DataTableHelper.GetStringValue(row, "sessionaffinityheader")`
- `SessionTimeoutMs` via `DataTableHelper.GetIntValue(row, "sessiontimeoutms")` with a `if (obj.SessionTimeoutMs == 0) obj.SessionTimeoutMs = 600000;` fixup
- `SessionMaxEntries` via `DataTableHelper.GetIntValue(row, "sessionmaxentries")` with a `if (obj.SessionMaxEntries == 0) obj.SessionMaxEntries = 10000;` fixup

---

### 1.3 Model Changes: `RequestContext`

**File**: `src/Conductor.Core/Models/RequestContext.cs`

The existing `ClientIdentifier` property (line 100) and `ClientIpAddress` property (line 95) are already declared but never populated. The implementation will:

- Populate `ClientIpAddress` from `ctx.Request.Source.IpAddress` (with `X-Forwarded-For` chain awareness).
- Populate `ClientIdentifier` in the `ProxyController` based on the VMR's `SessionAffinityMode`:
  - `SourceIP`: Use the leftmost IP from `X-Forwarded-For`, or the direct client IP.
  - `ApiKey`: Extract from the `Authorization` header (the bearer token value).
  - `Header`: Extract the value of the header named in `SessionAffinityHeader`.
  - `None`: Leave `null` (no affinity lookup is performed).

No new properties are needed on this class.

---

### 1.4 New Service: `SessionAffinityService`

**File**: `src/Conductor.Server/Services/SessionAffinityService.cs` (new file)

This is a thread-safe, in-memory service that manages client-to-endpoint pinning. It is instantiated once and shared across all requests (similar to `HealthCheckService`).

#### Internal Data Structure

```
ConcurrentDictionary<string, ConcurrentDictionary<string, SessionEntry>> _Sessions
    Key: VMR ID
    Value: Dictionary mapping client identity key -> SessionEntry
```

#### `SessionEntry` (nested class or separate model)

| Field | Type | Description |
|-------|------|-------------|
| `EndpointId` | `string` | The pinned endpoint ID |
| `LastAccessUtc` | `DateTime` | Refreshed on each use; entries expire after `SessionTimeoutMs` of inactivity |
| `CreatedUtc` | `DateTime` | When the pin was first established |

#### Public Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `TryGetPinnedEndpoint` | `bool TryGetPinnedEndpoint(string vmrId, string clientKey, out string endpointId)` | Looks up the pinned endpoint for a client. Returns `false` if no pin exists or if the entry has expired. Refreshes `LastAccessUtc` on hit. |
| `SetPinnedEndpoint` | `void SetPinnedEndpoint(string vmrId, string clientKey, string endpointId, int timeoutMs, int maxEntries)` | Creates or updates a session pin. If `maxEntries` is exceeded, evicts the oldest entry by `LastAccessUtc`. |
| `RemovePinnedEndpoint` | `void RemovePinnedEndpoint(string vmrId, string clientKey)` | Explicitly removes a pin (e.g., when the pinned endpoint becomes unhealthy). |
| `RemoveAllForVmr` | `void RemoveAllForVmr(string vmrId)` | Clears all session entries for a VMR (useful when VMR configuration changes). |
| `RemoveAllForEndpoint` | `void RemoveAllForEndpoint(string endpointId)` | Clears all entries pinned to a specific endpoint across all VMRs (useful when an endpoint is deactivated or deleted). |
| `GetSessionCount` | `int GetSessionCount(string vmrId)` | Returns the number of active (non-expired) sessions for a VMR. For monitoring/health display. |
| `Cleanup` | `void Cleanup()` | Removes all expired entries across all VMRs. Called periodically by a background timer (e.g., every 60 seconds). |

#### Thread Safety

- All reads/writes to the `ConcurrentDictionary` are inherently thread-safe for lookup and insertion.
- Individual `SessionEntry` mutation (refreshing `LastAccessUtc`) should use `Interlocked` or lock-free assignment since `DateTime` assignment is atomic on 64-bit systems, but to be safe and explicit, use a lock on the entry or `volatile` semantics.
- The `Cleanup` timer uses `System.Threading.Timer` started in the constructor, disposed via `IDisposable`.

#### Eviction Strategy

When `SetPinnedEndpoint` detects the session count for a VMR exceeds `maxEntries`:
1. Sort entries by `LastAccessUtc` ascending.
2. Remove the oldest 10% (or at least 1 entry) to create headroom.
3. This avoids per-request eviction overhead by batching.

---

### 1.5 Proxy Controller Changes

**File**: `src/Conductor.Server/Controllers/ProxyController.cs`

#### Constructor

Add `SessionAffinityService` as a constructor parameter (nullable, like `HealthCheckService`):
```csharp
private readonly SessionAffinityService _SessionAffinityService;
```

#### `HandleRequest` Method Changes

After the VMR is retrieved and validated (around line 76), before endpoint filtering (line 97):

**Step 1: Derive client identity key**

```
string clientKey = null;
if (vmr.SessionAffinityMode != SessionAffinityModeEnum.None)
{
    clientKey = DeriveClientKey(ctx, vmr.SessionAffinityMode, vmr.SessionAffinityHeader);
}
```

**Step 2: Attempt session affinity lookup (before filtering)**

If `clientKey` is not null and `_SessionAffinityService` is not null:
1. Call `_SessionAffinityService.TryGetPinnedEndpoint(vmr.Id, clientKey, out string pinnedEndpointId)`.
2. If a pinned endpoint is found:
   a. Read the endpoint from the database.
   b. Verify it is active.
   c. Check health via `_HealthCheckService.GetHealthState()`.
   d. Check capacity (if `MaxParallelRequests > 0`, verify `InFlightRequests < MaxParallelRequests`).
   e. If the pinned endpoint passes all checks, use it directly (skip `SelectEndpointWithWeight`).
   f. If the pinned endpoint fails any check, call `_SessionAffinityService.RemovePinnedEndpoint()` and fall through to normal load balancing.

**Step 3: After endpoint selection via load balancing (line 156)**

If `clientKey` is not null and `_SessionAffinityService` is not null, and the endpoint was selected via normal load balancing (not from pin):
1. Call `_SessionAffinityService.SetPinnedEndpoint(vmr.Id, clientKey, endpoint.Id, vmr.SessionTimeoutMs, vmr.SessionMaxEntries)`.

**Step 4: Populate RequestContext**

Set `requestContext.ClientIdentifier = clientKey` and `requestContext.ClientIpAddress = ctx.Request.Source.IpAddress` for logging/telemetry.

#### New Private Method: `DeriveClientKey`

```csharp
private string DeriveClientKey(HttpContextBase ctx, SessionAffinityModeEnum mode, string headerName)
```

| Mode | Derivation |
|------|-----------|
| `SourceIP` | Use `X-Forwarded-For` first entry if present, otherwise `ctx.Request.Source.IpAddress`. |
| `ApiKey` | Extract value from `Authorization` header after `Bearer ` prefix. If no `Authorization` header, return `null`. |
| `Header` | Extract value from the header named by `headerName`. If header is missing or empty, return `null`. |
| `None` | Return `null`. |

Return `null` if the derived key is null or empty (falls back to non-affinity behavior).

---

### 1.6 Response Header

**File**: `src/Conductor.Core/Constants.cs`

Add a new constant:
```csharp
public static string HeaderSessionPinned = "X-Conductor-Session-Pinned";
```

In `SendProxyResponse` (ProxyController), add a response header indicating whether session affinity was applied:
- `X-Conductor-Session-Pinned: true` when the request used a pinned endpoint.
- `X-Conductor-Session-Pinned: false` when the request fell back to load balancing (or affinity is disabled).

This helps clients and operators understand routing decisions.

---

### 1.7 ConductorServer Wiring

**File**: `src/Conductor.Server/ConductorServer.cs`

- Instantiate `SessionAffinityService` alongside `HealthCheckService` during server startup.
- Pass it to the `ProxyController` constructor.
- Dispose it during server shutdown.

---

### 1.8 Database Schema Changes

Add four new columns to the `virtualmodelrunners` table across all four database providers.

#### SQLite (`src/Conductor.Core/Database/Sqlite/Queries/TableQueries.cs`)
```sql
sessionaffinitymode INTEGER NOT NULL DEFAULT 0,
sessionaffinityheader TEXT,
sessiontimeoutms INTEGER NOT NULL DEFAULT 600000,
sessionmaxentries INTEGER NOT NULL DEFAULT 10000
```

#### PostgreSQL (`src/Conductor.Core/Database/PostgreSql/Queries/TableQueries.cs`)
```sql
sessionaffinitymode INTEGER NOT NULL DEFAULT 0,
sessionaffinityheader VARCHAR(255),
sessiontimeoutms INTEGER NOT NULL DEFAULT 600000,
sessionmaxentries INTEGER NOT NULL DEFAULT 10000
```

#### SQL Server (`src/Conductor.Core/Database/SqlServer/Queries/TableQueries.cs`)
```sql
sessionaffinitymode INT NOT NULL DEFAULT 0,
sessionaffinityheader NVARCHAR(255),
sessiontimeoutms INT NOT NULL DEFAULT 600000,
sessionmaxentries INT NOT NULL DEFAULT 10000
```

#### MySQL (`src/Conductor.Core/Database/MySql/Queries/TableQueries.cs`)
```sql
sessionaffinitymode INT NOT NULL DEFAULT 0,
sessionaffinityheader VARCHAR(255),
sessiontimeoutms INT NOT NULL DEFAULT 600000,
sessionmaxentries INT NOT NULL DEFAULT 10000
```

**Note**: Also update all INSERT and UPDATE queries for virtual model runners in each database provider to include the four new columns. Update the SELECT queries if they use explicit column lists rather than `SELECT *`.

---

## 2. Dashboard Changes

### 2.1 VMR Form: Session Affinity Configuration

**File**: `dashboard/src/views/VirtualModelRunners.jsx`

#### Form State

Add to the `formData` initial state (line 29-48) and reset state in `handleCreate` (line 78-97):
```javascript
SessionAffinityMode: 'None',
SessionAffinityHeader: '',
SessionTimeoutMs: 600000,
SessionMaxEntries: 10000
```

Add to `handleEdit` (line 104-124) to populate from existing VMR:
```javascript
SessionAffinityMode: vmr.SessionAffinityMode || 'None',
SessionAffinityHeader: vmr.SessionAffinityHeader || '',
SessionTimeoutMs: vmr.SessionTimeoutMs || 600000,
SessionMaxEntries: vmr.SessionMaxEntries || 10000
```

Add to `handleSubmit` data object (line 219-238):
```javascript
SessionAffinityMode: formData.SessionAffinityMode,
SessionAffinityHeader: formData.SessionAffinityHeader || null,
SessionTimeoutMs: parseInt(formData.SessionTimeoutMs),
SessionMaxEntries: parseInt(formData.SessionMaxEntries)
```

#### Form UI

Insert a new section after the Load Balancing Mode / Timeout row (after line 489) and before the Model Runner Endpoints section (line 492). This groups session affinity with other routing configuration:

```
Session Affinity section (form-row):
  - Dropdown: "Session Affinity Mode"
      Options: None, Source IP, API Key, Header
      Tooltip: "Pin subsequent requests from the same client to the same backend
                endpoint to minimize context drops"
  - Text input: "Session Affinity Header"
      Only visible/enabled when mode is "Header"
      Placeholder: "X-Session-Id"
      Tooltip: "Request header name whose value identifies the client session"
  - Number input: "Session Timeout (ms)"
      Only visible when mode is not "None"
      Min: 60000, Step: 60000
      Tooltip: "How long a session pin remains active since the client's last request
                (refreshed on each request)"
  - Number input: "Max Session Entries"
      Only visible when mode is not "None"
      Min: 100, Step: 100
      Tooltip: "Maximum number of concurrent session-to-endpoint pins for this VMR"
```

#### Data Table Column

Update the "Load Balancing" column (line 333-338) tooltip to mention session affinity:
```
"How requests are distributed across endpoints. Session affinity, if enabled, pins
clients to specific endpoints."
```

Optionally add a visual indicator in the Load Balancing column cell when session affinity is enabled (e.g., append a pin icon or "(Pinned)" suffix).

### 2.2 Health Modal: Active Sessions Count

**File**: `dashboard/src/views/VirtualModelRunners.jsx`

In the health status modal (lines 691-776), add a row showing active session count when session affinity is enabled. This data will come from the health endpoint response (see section 2.3).

Add after the health summary header (line 694-712):
```
{healthData && selectedVmr.SessionAffinityMode !== 'None' && (
  <div className="session-info">
    Active Sessions: {healthData.ActiveSessionCount ?? 0}
    / {selectedVmr.SessionMaxEntries}
  </div>
)}
```

### 2.3 API Response Extension

The existing `getVirtualModelRunnerHealth` endpoint response should be extended on the backend to include `ActiveSessionCount` when session affinity is enabled. The dashboard already fetches this via `api.getVirtualModelRunnerHealth()` — no API client changes are needed, only consuming the new field.

---

## 3. Test Changes

### 3.1 Core Model Tests

**File**: `src/Conductor.Core.Tests/Models/VirtualModelRunnerTests.cs`

Add test cases following the existing naming convention (`{Property}_{Scenario}_{ExpectedResult}`):

| Test | Description |
|------|-------------|
| `SessionAffinityMode_DefaultsToNone` | Verify new VMR defaults to `SessionAffinityModeEnum.None` |
| `SessionAffinityMode_WhenSet_ReturnsExpectedValue` | Verify round-trip for each enum value |
| `SessionAffinityHeader_DefaultsToNull` | Verify default is null |
| `SessionAffinityHeader_WhenSet_ReturnsExpectedValue` | Verify round-trip |
| `SessionTimeoutMs_DefaultsTo600000` | Verify default |
| `SessionTimeoutMs_WhenBelowMinimum_ClampsToDefault` | Values < 60000 clamp to 600000 |
| `SessionTimeoutMs_WhenAboveMaximum_ClampsToMaximum` | Values > 86400000 clamp to 86400000 |
| `SessionTimeoutMs_WhenValid_ReturnsExpectedValue` | Values in range pass through |
| `SessionMaxEntries_DefaultsTo10000` | Verify default |
| `SessionMaxEntries_WhenBelowMinimum_ClampsToDefault` | Values < 100 clamp to 10000 |
| `SessionMaxEntries_WhenAboveMaximum_ClampsToMaximum` | Values > 1000000 clamp to 1000000 |
| `SessionMaxEntries_WhenValid_ReturnsExpectedValue` | Values in range pass through |

### 3.2 Enum Tests

**File**: `src/Conductor.Core.Tests/Enums/EnumTests.cs`

Add tests for the new `SessionAffinityModeEnum`:

| Test | Description |
|------|-------------|
| `SessionAffinityModeEnum_None_HasValue0` | Verify `None = 0` |
| `SessionAffinityModeEnum_SourceIP_HasValue1` | Verify `SourceIP = 1` |
| `SessionAffinityModeEnum_ApiKey_HasValue2` | Verify `ApiKey = 2` |
| `SessionAffinityModeEnum_Header_HasValue3` | Verify `Header = 3` |
| `SessionAffinityModeEnum_HasExpectedMemberCount` | Verify enum has exactly 4 members |

### 3.3 SessionAffinityService Tests

**File**: `src/Conductor.Server.Tests/Services/SessionAffinityServiceTests.cs` (new file)

| Test | Description |
|------|-------------|
| `TryGetPinnedEndpoint_WhenNoPinExists_ReturnsFalse` | No entry, returns false |
| `TryGetPinnedEndpoint_WhenPinExists_ReturnsTrueAndEndpointId` | Entry exists, returns correct endpoint |
| `TryGetPinnedEndpoint_WhenPinExpired_ReturnsFalse` | Entry past TTL, returns false |
| `TryGetPinnedEndpoint_RefreshesLastAccessUtc` | Verify the TTL is refreshed on access |
| `SetPinnedEndpoint_CreatesNewEntry` | New entry is created |
| `SetPinnedEndpoint_UpdatesExistingEntry` | Endpoint changes for existing key |
| `SetPinnedEndpoint_EvictsOldestWhenMaxEntriesExceeded` | Verify eviction when limit hit |
| `RemovePinnedEndpoint_RemovesEntry` | Entry is removed |
| `RemovePinnedEndpoint_WhenNotExists_DoesNotThrow` | No-op for missing entry |
| `RemoveAllForVmr_RemovesAllEntriesForVmr` | All entries for a VMR are cleared |
| `RemoveAllForVmr_DoesNotAffectOtherVmrs` | Other VMR entries remain |
| `RemoveAllForEndpoint_RemovesAcrossVmrs` | Entries for a specific endpoint are cleared across all VMRs |
| `GetSessionCount_ReturnsCorrectCount` | Count matches active entries |
| `GetSessionCount_ExcludesExpiredEntries` | Expired entries not counted |
| `Cleanup_RemovesExpiredEntries` | Expired entries are purged |
| `Cleanup_PreservesActiveEntries` | Non-expired entries survive cleanup |
| `ConcurrentAccess_NoExceptionsThrown` | Multi-threaded reads/writes complete without errors |
| `NullVmrId_ReturnsFalse` | Null/empty VMR ID is handled gracefully |
| `NullClientKey_ReturnsFalse` | Null/empty client key is handled gracefully |

### 3.4 Integration Tests

**File**: `src/Conductor.Server.Tests/Integration/DatabaseIntegrationTests.cs`

Add test cases for the new VMR columns:

| Test | Description |
|------|-------------|
| `VirtualModelRunner_SessionAffinityFields_RoundTrip` | Create VMR with session affinity fields, read back, verify all four fields persist correctly |
| `VirtualModelRunner_SessionAffinityDefaults_RoundTrip` | Create VMR without setting session fields, verify defaults are returned |

### 3.5 Controller Tests

**File**: `src/Conductor.Server.Tests/Controllers/VirtualModelRunnerControllerTests.cs`

Add test cases for CRUD operations with the new fields:

| Test | Description |
|------|-------------|
| `Create_WithSessionAffinity_ReturnsCreatedVmr` | Create VMR with session affinity mode set, verify response |
| `Update_SessionAffinityMode_UpdatesSuccessfully` | Update session affinity mode, verify persistence |

---

## 4. Documentation Changes

### 4.1 README.md

**File**: `README.md`

Update the Features section to include session affinity. Add a bullet under the existing features list:

```
- **Session Affinity**: Pin clients to specific backend endpoints based on IP address,
  API key, or custom headers to minimize context drops and model swapping
```

Update the "Load Balancing" bullet to cross-reference session affinity:

```
- **Load Balancing**: Round-robin, random, or first-available endpoint selection with
  weighted distribution and optional session affinity
```

### 4.2 TESTING.md

**File**: `TESTING.md`

Update the test count table to reflect the new tests. Add entries for the new test files:

```
Under Conductor.Core.Tests Structure:
  Models/VirtualModelRunnerTests.cs  - Add note: "session affinity defaults and clamping"
  Enums/EnumTests.cs                 - Add note: "SessionAffinityModeEnum values"

Under Conductor.Server.Tests Structure:
  Services/SessionAffinityServiceTests.cs - "Session pin lifecycle, TTL, eviction, thread safety"
```

### 4.3 CLAUDE.md

No changes required. The existing code style guidelines apply to the new code.

---

## 5. Implementation Order

The implementation should proceed in this order to ensure each layer builds on a stable foundation:

| Step | Component | Description |
|------|-----------|-------------|
| 1 | `SessionAffinityModeEnum` | Create the new enum file |
| 2 | `VirtualModelRunner` model | Add four new properties, backing fields, `FromDataRow` changes |
| 3 | Database schemas (all 4 providers) | Add columns to CREATE TABLE and update INSERT/UPDATE/SELECT queries |
| 4 | `Constants.cs` | Add `HeaderSessionPinned` constant |
| 5 | `SessionAffinityService` | Implement the full service with tests |
| 6 | `ProxyController` | Integrate affinity logic into `HandleRequest`, add `DeriveClientKey` |
| 7 | `ConductorServer` | Wire up `SessionAffinityService` |
| 8 | Core model tests | `VirtualModelRunnerTests`, `EnumTests` |
| 9 | Service tests | `SessionAffinityServiceTests` |
| 10 | Integration tests | Database round-trip tests |
| 11 | Controller tests | CRUD with new fields |
| 12 | Dashboard | Form fields, health modal, data table indicator |
| 13 | Documentation | README.md, TESTING.md updates |
| 14 | Build and verify | `dotnet build`, `dotnet test`, `npm run dev` |

---

## 6. Files Changed or Created

### New Files
| File | Description |
|------|-------------|
| `src/Conductor.Core/Enums/SessionAffinityModeEnum.cs` | Session affinity mode enumeration |
| `src/Conductor.Server/Services/SessionAffinityService.cs` | In-memory session pin management service |
| `src/Conductor.Server.Tests/Services/SessionAffinityServiceTests.cs` | Tests for session affinity service |

### Modified Files
| File | Description |
|------|-------------|
| `src/Conductor.Core/Models/VirtualModelRunner.cs` | Add 4 new properties, backing fields, FromDataRow |
| `src/Conductor.Core/Models/RequestContext.cs` | No property changes (fields already exist), just ensure population |
| `src/Conductor.Core/Constants.cs` | Add `HeaderSessionPinned` |
| `src/Conductor.Server/Controllers/ProxyController.cs` | Session affinity integration, `DeriveClientKey` method |
| `src/Conductor.Server/ConductorServer.cs` | Instantiate and wire `SessionAffinityService` |
| `src/Conductor.Core/Database/Sqlite/Queries/TableQueries.cs` | Add 4 columns |
| `src/Conductor.Core/Database/PostgreSql/Queries/TableQueries.cs` | Add 4 columns |
| `src/Conductor.Core/Database/SqlServer/Queries/TableQueries.cs` | Add 4 columns |
| `src/Conductor.Core/Database/MySql/Queries/TableQueries.cs` | Add 4 columns |
| `dashboard/src/views/VirtualModelRunners.jsx` | Session affinity form controls, health modal, table column |
| `src/Conductor.Core.Tests/Models/VirtualModelRunnerTests.cs` | New test cases for session affinity properties |
| `src/Conductor.Core.Tests/Enums/EnumTests.cs` | New test cases for SessionAffinityModeEnum |
| `src/Conductor.Server.Tests/Integration/DatabaseIntegrationTests.cs` | Round-trip tests for new columns |
| `src/Conductor.Server.Tests/Controllers/VirtualModelRunnerControllerTests.cs` | CRUD tests with new fields |
| `README.md` | Feature description update |
| `TESTING.md` | Test count and structure updates |

---

## 7. Behavioral Summary

### When Session Affinity is `None` (Default)

No change to current behavior. Requests are routed via the configured load balancing mode. The `SessionAffinityService` is not consulted. The `X-Conductor-Session-Pinned` header is not emitted.

### When Session Affinity is Enabled (`SourceIP`, `ApiKey`, or `Header`)

```
Request arrives at VMR
  |
  +--> Derive client key from request (IP, API key, or header)
  |
  +--> Client key is null/empty? --> Fall through to normal load balancing
  |
  +--> Lookup pinned endpoint in SessionAffinityService
  |       |
  |       +--> Pin found and endpoint is healthy + has capacity?
  |       |       |
  |       |       +--> YES: Use pinned endpoint (skip load balancing)
  |       |       |         Set X-Conductor-Session-Pinned: true
  |       |       |
  |       |       +--> NO:  Remove stale pin, fall through
  |       |
  |       +--> No pin found? --> Fall through
  |
  +--> Normal load balancing (SelectEndpointWithWeight)
  |
  +--> Pin the selected endpoint for this client key
  |    Set X-Conductor-Session-Pinned: false
  |
  +--> Forward request to selected endpoint
```

### Edge Cases

| Scenario | Behavior |
|----------|----------|
| Pinned endpoint goes unhealthy | Pin is removed. Next request uses load balancing and creates new pin. |
| Pinned endpoint is at capacity | Pin is removed. Next request uses load balancing and creates new pin. |
| Pinned endpoint is deactivated | Pin is removed (endpoint fails active check). New pin created via load balancing. |
| Pinned endpoint is removed from VMR's endpoint list | Pin is removed (endpoint not in filtered list). New pin created. |
| Client key cannot be derived (header missing, no auth) | Request proceeds with normal load balancing. No pin is created. |
| Session TTL expires | Entry is lazily removed on next lookup. Background cleanup also removes it periodically. |
| Max entries exceeded | Oldest entries (by `LastAccessUtc`) are evicted in batches. |
| VMR configuration changes (session affinity mode changes) | Existing pins remain and expire naturally. Could optionally call `RemoveAllForVmr` on update. |
| Multiple VMRs share the same endpoint | Each VMR maintains its own session map. A client pinned to endpoint X through VMR-A has no relationship to VMR-B. |
