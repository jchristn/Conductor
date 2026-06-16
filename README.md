<img src="./assets/icon-dark.png" alt="Conductor" width="128" height="128">

# Conductor

Conductor is a platform for managing models, model runners, model configurations, and virtualizing combinations into virtual model runners exposed to the network through OpenAI, vLLM, Gemini, and Ollama APIs.

## Features

- **Multi-tenant Architecture**: Full tenant isolation with tenant-scoped data access
- **Model Runner Endpoints**: Define and manage first-class endpoint types for OpenAI, vLLM, Gemini, and Ollama model runners
- **Model Definitions**: Catalog your models with metadata like family, parameter size, and quantization
- **Model Configurations**: Create reusable configurations with pinned properties for embeddings and completions
- **Virtual Model Runners**: Combine endpoints and configurations into virtual endpoints with load balancing
- **VMR Reservations**: Schedule exclusive access windows for selected users or credentials while preserving normal on-demand access outside the window
- **Configuration Pinning**: Automatically inject model parameters into requests (like OllamaFlow)
- **Session Affinity**: Pin clients to specific backend endpoints based on IP address, API key, or custom headers to minimize context drops and model swapping
- **Load Balancing**: Round-robin, random, or first-available endpoint selection with weighted distribution and optional session affinity
- **Health Checking**: Automatic background health monitoring of endpoints with configurable thresholds and shared probes for duplicate health URLs
- **RigMonitor Telemetry**: Optionally enrich endpoint health with cached host CPU, memory, disk, network, GPU, and Ollama telemetry from RigMonitor sidecars
- **Policy-Based Routing**: Create first-class load-balancing policies that filter or rank endpoints using health, capacity, and RigMonitor metrics
- **Model Access Policies**: Attach tenant-scoped ACL policies to virtual model runners to allow, deny, monitor, and explain model usage by credential, user, label, model, action, and VMR
- **Explainable Routing**: Simulate representative requests, inspect candidate elimination, review policy evidence, and persist routing explanations into request history
- **Model Load Or Verification Controls**: Warm local models or verify hosted-provider availability from endpoint and virtual model runner workflows
- **Preflight Validation**: Validate endpoints, model definitions, model configurations, load-balancing policies, and VMRs before saving them
- **Effective Configuration Preview**: Resolve the endpoint set, request permissions, policy attachment, model pinning, and session-affinity settings that a VMR will actually use
- **Operational Metrics**: Export Prometheus-friendly latency, denial, fallback, session-affinity, saturation, and telemetry-freshness signals
- **Drain And Quarantine Controls**: Keep endpoints visible for health diagnostics while intentionally excluding them from new routing
- **Rate Limiting**: Per-endpoint maximum parallel request limits with automatic capacity management
- **Request History and Analytics**: Optional per-VMR request/response capture with trace IDs, stage timings, provider request IDs, token counts, throughput, dashboard drill-down, configurable retention, redaction, metadata-only retention modes, and a tenant-scoped Analytics workspace for TTFT, token usage, user/credential breakdowns, estimated cost, and failed-request reporting
- **React Dashboard**: Full-featured UI for managing all entities including real-time health status

## Quick Start

### Using Docker Compose

```bash
cd docker
docker compose up -d
```

The server will be available at `http://localhost:9000` and the dashboard at `http://localhost:9100`.
The Compose file builds the server and dashboard from the local repository Dockerfiles, starts PostgreSQL with a persisted `conductor-postgres-data` volume, and runs a one-shot `conductor-db-init` container to create the database schema and factory default records.

The dashboard container receives `CONDUCTOR_SERVER_URL=http://localhost:9000` from `docker/compose.yaml`, so the login page points browsers at the host-exposed Conductor API by default.

### Building from Source

#### Prerequisites

- .NET 10 SDK
- Node.js 20+

#### Build and Run Server

```bash
cd src/Conductor.Server
dotnet run
```

#### Build and Run Dashboard

```bash
cd dashboard
npm install
npm run dev
```

To allow the Vite dashboard dev server to accept requests from outside `localhost`, bind it to all interfaces:

```bash
npm run dev -- --host 0.0.0.0
```

The dashboard listens on port `9100`, so other machines on the network can open `http://<your-machine-ip>:9100`. On Windows, use `ipconfig` to find the machine IP. If the page is unreachable from another machine, allow inbound TCP traffic for port `9100` in Windows Firewall.

For a built preview that also accepts external requests:

```bash
npm run build
npm run preview -- --host 0.0.0.0
```

## Documentation

- [REST_API.md](./REST_API.md): management API routes, resource shapes, proxy behavior, request history, analytics, and observability.
- [ANALYTICS.md](./ANALYTICS.md): product plan for the Analytics workspace.
- [ANALYTICS_PLAN.md](./ANALYTICS_PLAN.md): implementation tracker for the Analytics workspace.
- [ADR 0002](./docs/adr/0002-analytics-workspace.md): Analytics workspace API, retention, authorization, saved-report, and export decisions.
- [ADR 0003](./docs/adr/0003-virtual-model-runner-reservations.md): VMR reservation admission, observability, API, and backup/restore decisions.
- [MANAGING_RESERVATIONS.md](./MANAGING_RESERVATIONS.md): VMR reservation operations, enforcement order, troubleshooting, and internals.
- [ACCESS_POLICIES.md](./ACCESS_POLICIES.md): practical model access policy authoring guide with real-world examples.
- [TESTING.md](./TESTING.md): test architecture and commands.
- [Conductor.postman_collection.json](./Conductor.postman_collection.json): Postman collection covering management, validation, model access, routing, analytics, and observability routes.
- [CHANGELOG.md](./CHANGELOG.md): unreleased and historical change notes.

## Testing

Conductor's automated tests use **Touchstone** so the same shared test cases can run through multiple hosts.

- `src/Test.Shared/` contains the authoritative test definitions.
- `src/Test.Xunit/` exposes the shared suite through xUnit.
- `src/Test.Nunit/` exposes the same suite through NUnit.
- `src/Test.Automated/` runs the suite through the Touchstone console runner.

Common commands:

```bash
# Run framework-hosted tests
dotnet test src/Conductor.sln

# Run the console host
dotnet run --project src/Test.Automated/Test.Automated.csproj
```

See [TESTING.md](./TESTING.md) for the full testing guide.

## SDKs

Conductor ships lightweight SDKs for common management-plane workflows:

- `sdk/javascript/` for Node.js and browser-adjacent tooling
- `sdk/python/` for Python automation and ops scripts
- `sdk/csharp/` for .NET automation and ops tooling

The SDKs include helpers for:

- validation routes
- VMR effective configuration preview
- explain-routing simulations
- endpoint drain, resume, and quarantine actions
- endpoint and virtual model runner model load or verification requests
- model access policy CRUD, validation, evaluation, and effective-access simulation
- VMR reservation CRUD, validation, scoped listing, and effective-access simulation
- request-history search, summary, detail, analytics, and bulk deletion
- analytics catalog, query, summary, TTFT, token usage, estimate-only cost, user, and access/reliability routes
- observability metrics summary and text export

## API Overview

### Supported Provider Types

Conductor currently supports four model runner provider types in both the backend proxy and the dashboard:

| Provider Type | Runner Type in UI | Proxied API Shape | Notes |
|---------------|-------------------|-------------------|-------|
| OpenAI | `OpenAI` | OpenAI REST API | Supports OpenAI-style chat, embeddings, and model listing |
| vLLM | `vLLM` | OpenAI-compatible REST API | First-class runner type in the UI; uses the OpenAI-compatible API surface |
| Gemini | `Gemini` | Gemini REST API | Supports Gemini-style `models/{model}:generateContent`, streaming, embeddings, and model listing |
| Ollama | `Ollama` | Ollama REST API | Supports Ollama-style `/api/generate`, `/api/chat`, and embeddings flows |

### Authentication

Conductor supports two authentication methods:

1. **Header-based**: Include `x-tenant-id`, `x-email`, and `x-password` headers
2. **Bearer Token**: Include `Authorization: Bearer {token}` header

### User Permission Model

Users have three permission levels:

| Permission | Description |
|------------|-------------|
| **Global Admin** (`IsAdmin=true`) | Full cross-tenant access to all resources |
| **Tenant Admin** (`IsTenantAdmin=true`) | Can manage users and credentials within their own tenant |
| **Standard User** | Can only access model configurations, endpoints, runners, and virtual runners in their tenant |

- **Global Admins** can operate on any tenant by specifying `TenantId` in their requests
- **Tenant Admins** have elevated privileges within their assigned tenant
- **Standard Users** have read/write access to non-administrative resources

### Endpoints

| Entity | Prefix | API Endpoint |
|--------|--------|--------------|
| Administrator | `admin_` | `/v1.0/administrators` |
| Tenant | `ten_` | `/v1.0/tenants` |
| User | `usr_` | `/v1.0/users` |
| Credential | `cred_` | `/v1.0/credentials` |
| Model Runner Endpoint | `mre_` | `/v1.0/modelrunnerendpoints` |
| Model Definition | `md_` | `/v1.0/modeldefinitions` |
| Model Configuration | `mc_` | `/v1.0/modelconfigurations` |
| Load Balancing Policy | `lbp_` | `/v1.0/loadbalancingpolicies` |
| Model Access Policy | `map_` | `/v1.0/modelaccesspolicies` |
| Virtual Model Runner | `vmr_` | `/v1.0/virtualmodelrunners` |
| VMR Reservation | `vmrr_` | `/v1.0/vmrreservations` |
| VMR Reservation Subject | `vmrrs_` | nested in VMR reservation responses |
| Request History | `req_` | `/v1.0/requesthistory` |
| Request History Summary | - | `/v1.0/requesthistory/summary` |
| Request Analytics | `rae_` | `/v1.0/requesthistory/analytics/overview` |
| Analytics Workspace | - | `/v1.0/analytics` |
| Observability Metrics | - | `/v1.0/observability/metrics` |

### Model Loading

Tenant admins can ask Conductor to load or verify a model before user traffic reaches it:

- `POST /v1.0/modelrunnerendpoints/{id}/load-model`
- `POST /v1.0/virtualmodelrunners/{id}/load-model`

For Ollama, `ProbeKind=Auto` uses a minimal native generation probe and can keep the model resident with `KeepAlive`. For vLLM, OpenAI, and Gemini, `Auto` defaults to metadata verification because those providers generally do not expose a host-local load primitive through their public APIs. Explicit generation or embedding probes for hosted providers may be billable.

See [REST_API.md](./REST_API.md#model-loading) for request fields, outcome codes, dashboard behavior, SDK examples, and provider caveats.

### VMR Reservations

VMR reservations schedule exclusive access windows for a Virtual Model Runner. During the active window, and during any configured admission drain lead time, Conductor admits only the users and credentials listed on the reservation. Outside the window, normal on-demand VMR access continues and existing ACL/model-access checks still apply after a reservation participant is allowed.

Operators can manage reservations from the dashboard **Reservations** workspace, from VMR row actions, or through `/v1.0/vmrreservations`. The dashboard supports listing, refreshing, VMR-scoped creation, validating, row-click editing, deactivating, row-level JSON inspection, VMR reservation badges, request-history reservation filters/detail fields, analytics reservation-denial cards, and evaluating effective reservation access for a candidate user or credential. Reservation denials are recorded in logs, request history, and request analytics with machine-readable reasons such as `ReservationDenied`, `ReservationDrainDenied`, `ReservationAuthenticationRequired`, and `ReservationConflict`. Backup/restore includes reservation records and subjects.

See [MANAGING_RESERVATIONS.md](./MANAGING_RESERVATIONS.md) for operational guidance, API examples, enforcement order, troubleshooting, and implementation details.

### Ollama Model Management

Ollama-type model runner endpoints also expose tenant-admin model management:

- `GET /v1.0/modelrunnerendpoints/{id}/ollama/models`
- `POST /v1.0/modelrunnerendpoints/{id}/ollama/models/pull`
- `POST /v1.0/modelrunnerendpoints/{id}/ollama/models/delete`

In the dashboard, use `Manage Models` from an Ollama endpoint action menu to list local models, pull a new model, or delete an installed model.

## RigMonitor And Policy Routing

Model runner endpoints can optionally declare a `RigMonitor` sidecar configuration. Conductor collects that data during the normal endpoint health-check loop, caches it in memory, and exposes it through endpoint health and telemetry routes. The proxy path never performs live RigMonitor calls while handling client traffic.

### Endpoint RigMonitor Configuration

Each `ModelRunnerEndpoint` can include a `RigMonitor` object with fields such as:

- `Enabled`
- `HostnameOverride`
- `Port`
- `UseSsl`
- `TimeoutMs`
- `CollectDuringHealthCheck`
- `RequireReadyz`
- `HealthAffectedByRigMonitor`
- `MaxTelemetryAgeMs`
- `CapabilitiesRefreshIntervalMs`
- `TelemetryProfile`
- `TelemetrySelectors`

Useful routes:

- `GET /v1.0/modelrunnerendpoints/health`
- `GET /v1.0/modelrunnerendpoints/{id}/health`
- `GET /v1.0/modelrunnerendpoints/{id}/rigmonitor`

### First-Class Load Balancing Policies

Load-balancing policies are tenant-scoped resources attached to a VMR by `LoadBalancingPolicyId`.

- Policy CRUD: `/v1.0/loadbalancingpolicies`
- Metrics catalog: `GET /v1.0/loadbalancingpolicies/metrics`
- VMR attachment: set `LoadBalancingPolicyId` on `/v1.0/virtualmodelrunners`

Policies combine:

- `Filters`: hard eligibility checks such as `health.isHealthy == true` or `rig.gpu.available == true`
- `Ranking`: weighted numeric comparisons such as lowest CPU, lowest GPU utilization, or fewest in-flight requests
- `FallbackMode`: use the VMR's legacy load-balancing mode or fail closed
- `TieBreaker`: round-robin, random, or first available when scores are equal

Example policy payload:

```json
{
  "Name": "Lowest GPU Utilization",
  "MaxTelemetryAgeMs": 30000,
  "Filters": [
    { "Metric": "health.isHealthy", "Operator": "Equal", "ValueType": "Boolean", "Value": "true" },
    { "Metric": "health.hasCapacity", "Operator": "Equal", "ValueType": "Boolean", "Value": "true" },
    { "Metric": "rig.gpu.available", "Operator": "Equal", "ValueType": "Boolean", "Value": "true" }
  ],
  "Ranking": [
    { "Metric": "rig.gpu.avgUtilizationPercent", "Direction": "Ascending", "Weight": 1.0 }
  ],
  "FallbackMode": "UseLegacyLoadBalancingMode",
  "TieBreaker": "RoundRobin",
  "Active": true
}
```

Example VMR attachment:

```json
{
  "Name": "GPU Chat VMR",
  "BasePath": "/v1.0/api/gpu-chat/",
  "LoadBalancingMode": "RoundRobin",
  "LoadBalancingPolicyId": "lbp_example",
  "ModelRunnerEndpointIds": ["mre_a", "mre_b"],
  "Active": true
}
```

### Explain, Validate, And Preview

The management plane now exposes first-class safety and explainability routes:

- `POST /v1.0/modelrunnerendpoints/validate`
- `POST /v1.0/modeldefinitions/validate`
- `POST /v1.0/modelconfigurations/validate`
- `POST /v1.0/loadbalancingpolicies/validate`
- `POST /v1.0/virtualmodelrunners/validate`
- `GET /v1.0/virtualmodelrunners/{id}/effective`
- `POST /v1.0/virtualmodelrunners/{id}/explain-routing`

Recommended operator flow:

1. Validate drafts before saving.
2. Inspect the effective VMR preview to confirm endpoint coverage, request permissions, policy attachment, and model pinning.
3. Use explain-routing with a representative request body when you need to understand why a request would route, mutate, reuse a session pin, or be denied.

Request-history detail responses also expose the structured routing decision when history is enabled for the VMR.

### Model Access Policies

Model access policies are tenant-scoped ACL resources attached to virtual model runners through `ModelAccessPolicyId`. They decide whether a credential, user, tenant, or labeled subject can use a model definition, model name, model label, VMR, or any resource for a specific action such as completions, embeddings, list-models, and model management. See [ACCESS_POLICIES.md](./ACCESS_POLICIES.md) for authoring guidance and real-world policy examples.

Management routes require tenant-admin access:

- `GET /v1.0/modelaccesspolicies`
- `POST /v1.0/modelaccesspolicies`
- `GET /v1.0/modelaccesspolicies/{id}`
- `PUT /v1.0/modelaccesspolicies/{id}`
- `DELETE /v1.0/modelaccesspolicies/{id}?forceDetach=true`
- `POST /v1.0/modelaccesspolicies/validate`
- `POST /v1.0/modelaccesspolicies/{id}/evaluate`
- `GET /v1.0/modelaccesspolicies/effective`

Runtime behavior:

- `Disabled` mode allows traffic and records disabled state.
- `Monitor` mode allows traffic but records would-deny decisions in routing evidence, request history, analytics, and audit logs.
- `Enforce` mode blocks denied proxy requests with `403 Forbidden` before endpoint selection or provider calls.
- Missing or invalid proxy credentials return `401 Unauthorized` when `RequireCredentialForProxy` is enabled.
- Successful authentication followed by an ACL denial returns `403 Forbidden`.
- List-models responses can be filtered, synthesized from allowed VMR model definitions, or passed through raw according to `ListModelsBehavior`.

Production rollout path:

1. Leave enforcement disabled or in monitor mode after upgrade.
2. Create and validate policies with explicit allow and deny rules.
3. Attach policies to a small number of VMRs.
4. Inspect would-deny request history, analytics, and routing explanations.
5. Enable enforce mode after expected traffic is clean.

See [ADR 0001](./docs/adr/0001-model-access-policy-semantics.md) for the accepted semantics and compatibility defaults.

### Operator Notes

- Keep unauthenticated RigMonitor sidecars on trusted networks only.
- `TelemetryProfile` now defaults to `Full`; narrow it only if you need to reduce health-check telemetry cost.
- Prefer `FallbackMode = UseLegacyLoadBalancingMode` first, then move selected VMRs to `FailClosed` once telemetry freshness and sidecar reliability are proven.
- Stale or missing telemetry can make a telemetry-dependent endpoint ineligible for policy evaluation.

### Virtual Model Runner Proxy

Virtual model runners expose an API at their configured base path. For example, a VMR with base path `/v1.0/api/my-vmr/` would expose:

- **OpenAI API**: `/v1.0/api/my-vmr/v1/chat/completions`, `/v1.0/api/my-vmr/v1/embeddings`
- **vLLM API**: `/v1.0/api/my-vmr/v1/chat/completions`, `/v1.0/api/my-vmr/v1/embeddings`
- **Gemini API**: `/v1.0/api/my-vmr/v1beta/models/gemini-2.5-flash:generateContent`, `/v1.0/api/my-vmr/v1beta/models/text-embedding-004:embedContent`
- **Ollama API**: `/v1.0/api/my-vmr/api/generate`, `/v1.0/api/my-vmr/api/chat`

## Configuration

### conductor.json

```json
{
  "Webserver": {
    "Hostname": "localhost",
    "Port": 9000,
    "Ssl": false,
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": ["http://localhost:9100"],
      "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      "AllowedHeaders": ["Content-Type", "Authorization", "x-tenant-id", "x-email", "x-password", "x-admin-apikey", "x-admin-email", "x-admin-password"],
      "ExposedHeaders": [],
      "AllowCredentials": false,
      "MaxAgeSeconds": 86400
    }
  },
  "Database": {
    "Type": "PostgreSql",
    "Hostname": "localhost",
    "Port": 5432,
    "DatabaseName": "conductor",
    "Username": "conductor",
    "Password": "conductor",
    "RequireEncryption": false
  },
  "Logging": {
    "Servers": [],
    "LogDirectory": "./logs/",
    "LogFilename": "conductor.log",
    "ConsoleLogging": true,
    "MinimumSeverity": 0
  },
  "RequestHistory": {
    "Enabled": true,
    "Directory": "./request-history/",
    "RetentionDays": 7,
    "MetadataRetentionDays": 30,
    "BodyRetentionDays": 7,
    "CleanupIntervalMinutes": 60,
    "CaptureRequestBody": true,
    "CaptureResponseBody": true,
    "RedactedHeaders": ["authorization", "x-password", "x-admin-password", "x-goog-api-key"],
    "RedactedJsonFields": ["authorization", "api_key", "apikey", "password", "token", "bearertoken"],
    "MaxRequestBodyBytes": 65536,
    "MaxResponseBodyBytes": 65536
  },
  "ModelAccessControl": {
    "Enabled": false,
    "Mode": "Disabled",
    "DefaultDecision": "Permit",
    "RequireCredentialForProxy": false,
    "UnknownModelBehavior": "Deny",
    "ListModelsBehavior": "Filter",
    "CacheTtlMs": 30000,
    "AllowAdministratorBypass": false,
    "AllowGlobalAdministratorBypass": false
  }
}
```

### Supported Databases

PostgreSQL is the default Docker database and is configured with:

```json
{
  "Type": "PostgreSql",
  "Hostname": "localhost",
  "Port": 5432,
  "DatabaseName": "conductor",
  "Username": "conductor",
  "Password": "conductor",
  "RequireEncryption": false
}
```

SQLite remains available for local development by setting:

```json
{
  "Type": "Sqlite",
  "Filename": "./conductor.db"
}
```

### CORS Configuration

Cross-Origin Resource Sharing (CORS) can be enabled to allow browser-based applications to access the Conductor API.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable or disable CORS support |
| `AllowedOrigins` | string[] | `[]` | List of allowed origins. Use `["*"]` for all origins |
| `AllowedMethods` | string[] | `["GET", "POST", "PUT", "DELETE", "OPTIONS"]` | Allowed HTTP methods |
| `AllowedHeaders` | string[] | `["Content-Type", "Authorization", ...]` | Allowed request headers |
| `ExposedHeaders` | string[] | `[]` | Headers exposed to the browser |
| `AllowCredentials` | bool | `false` | Allow credentials (cookies, auth headers). Cannot be used with `AllowedOrigins: ["*"]` |
| `MaxAgeSeconds` | int | `86400` | Preflight cache duration (0-86400 seconds) |

**Example: Allow all origins (development)**
```json
{
  "Webserver": {
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": ["*"]
    }
  }
}
```

**Example: Restrict to specific origins (production)**
```json
{
  "Webserver": {
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": ["https://app.example.com", "https://admin.example.com"],
      "AllowCredentials": true
    }
  }
}
```

### Request History Configuration

Request history captures request/response data for Virtual Model Runners with `RequestHistoryEnabled` set to `true`. New VMRs default this property to `true`, so Request History and Analytics populate unless an operator explicitly disables capture for that VMR. This is useful for debugging, auditing, troubleshooting, and latency analysis. Each completed entry records total response time and time to first token/byte (`FirstTokenTimeMs`). For non-streaming responses, `FirstTokenTimeMs` is set to the same value as `ResponseTimeMs`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable or disable request history globally |
| `Directory` | string | `"./request-history/"` | Directory for storing request detail JSON files |
| `RetentionDays` | int | `30` | Legacy retention knob used as a fallback when the newer retention settings are omitted |
| `MetadataRetentionDays` | int | `30` | Number of days to retain searchable ledger metadata before cleanup (1-365) |
| `BodyRetentionDays` | int | `30` | Number of days to retain request and response bodies inside detail files before they are scrubbed (1-365) |
| `CleanupIntervalMinutes` | int | `60` | Interval between cleanup runs in minutes (1-1440) |
| `CaptureRequestBody` | bool | `true` | Persist request bodies when request history is enabled for the VMR |
| `CaptureResponseBody` | bool | `true` | Persist response bodies when request history is enabled for the VMR |
| `RedactedHeaders` | string[] | built-in sensitive headers | Header names redacted before persistence |
| `RedactedJsonFields` | string[] | built-in sensitive JSON fields | JSON field names redacted recursively before persistence |
| `MaxRequestBodyBytes` | int | `65536` | Maximum request body bytes to capture (1-10485760) |
| `MaxResponseBodyBytes` | int | `65536` | Maximum response body bytes to capture (1-10485760) |

**Note:** Request history must be enabled both globally (in `conductor.json`) and per-VMR (via the `RequestHistoryEnabled` property). The per-VMR default is enabled for newly-created VMRs.

Captured request history entries include the VMR, routed model runner endpoint, matched model definition, matched model configuration, policy attachment, reservation gate dimensions when applicable, requested/effective model names, routing outcome, denial reason, mutation summary, HTTP status, body lengths, transfer type, total response time (`ResponseTimeMs`), time to first token/byte (`FirstTokenTimeMs`), trace ID, provider request ID, token counts, token throughput, analytics coverage, and dominant latency stage. Reservation denials persist `ReservationGuid`, `ReservationName`, `ReservationDecision`, `ReservationReasonCode`, and the UTC reservation window, and request-history search/summary plus request-analytics overview can filter by reservation id and reason.

When `BodyRetentionDays` is shorter than `MetadataRetentionDays`, Conductor scrubs request and response bodies from detail files while preserving the searchable routing and latency ledger.

### Request History Summary API

The summary endpoint returns aggregated request counts grouped by time buckets, useful for charting request volume and success/failure rates over time.

```
GET /v1.0/requesthistory/summary?startUtc={ISO8601}&endUtc={ISO8601}&interval={hour|day}&vmrGuid={guid}
```

Request analytics extends the history ledger with trace-linked performance events and aggregate dashboard data:

```text
GET /v1.0/requesthistory/analytics/overview?range=lastDay&vmrGuid={guid}&endpointGuid={guid}
GET /v1.0/requesthistory/{id}/analytics
```

The overview response includes request counts, success/failure counts, reservation-denial counts, latency percentiles, analytics coverage, token totals, token throughput, time-series buckets, stage breakdowns, endpoint summaries, and slowest-request links.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `startUtc` | string | No | Start of time range (UTC, ISO 8601). Default: 1 hour ago |
| `endUtc` | string | No | End of time range (UTC, ISO 8601). Default: now |
| `interval` | string | No | Bucket interval: `minute`, `15minute`, `hour`, `6hour`, or `day`. Default: `hour` |
| `vmrGuid` | string | No | Filter by Virtual Model Runner GUID |
| `endpointGuid` | string | No | Filter by routed model runner endpoint GUID |
| `requestorUserGuid` | string | No | Filter by authenticated user GUID |
| `credentialGuid` | string | No | Filter by credential GUID |
| `loadBalancingPolicyGuid` | string | No | Filter by attached load-balancing policy GUID |
| `modelName` | string | No | Filter by requested or effective model |
| `mutationSummary` | string | No | Filter by mutation-summary substring |
| `denialReasonCode` | string | No | Filter by denial reason |
| `reservationGuid` | string | No | Filter by VMR reservation id |
| `reservationDecision` | string | No | Filter by reservation gate decision such as `Allowed` or `Denied` |
| `reservationReasonCode` | string | No | Filter by reservation reason such as `ReservationDenied` |
| `sessionAffinityOutcome` | string | No | Filter by session-affinity outcome |
| `statusClass` | string | No | Filter by status class such as `2xx`, `4xx`, or `5xx` |
| `sourceIp` | string | No | Filter by requestor source IP |
| `httpStatus` | integer | No | Filter by exact HTTP status code |

**Response:**
```json
{
  "Data": [
    {
      "TimestampUtc": "2026-03-20T10:00:00Z",
      "SuccessCount": 42,
      "FailureCount": 3,
      "TotalCount": 45
    }
  ],
  "StartUtc": "2026-03-20T10:00:00Z",
  "EndUtc": "2026-03-20T11:00:00Z",
  "Interval": "hour",
  "TotalSuccess": 42,
  "TotalFailure": 3,
  "StatusClassCounts": {
    "2xx": 42,
    "5xx": 3
  },
  "DenialReasonCounts": {
    "AllEndpointsAtCapacity": 2,
    "PolicyRejected": 1
  },
  "SessionAffinityOutcomeCounts": {
    "Hit": 20,
    "Miss": 25
  },
  "TotalRequests": 45
}
```

Success is defined as HTTP status 100-399; failure is HTTP status 400-599 or null (incomplete requests).

### Analytics Workspace API

The dashboard navigation includes an **Analytics** workspace at `/analytics`. This workspace builds on request-history data and answers first-release operator questions without requiring raw request-body access. See [ADR 0002](./docs/adr/0002-analytics-workspace.md) for API, retention, authorization, saved-report, and export decisions.

- What was the average, P50, P95, and P99 time-to-first-token for a model endpoint, VMR, model, tenant, user, credential, or provider?
- How many prompt, completion, and total tokens were used over time for a model, endpoint, VMR, provider, tenant, or user?
- What is the estimate-only cost for a user, model, VMR, endpoint, or tenant when the operator supplies a per-token unit cost?
- How many requests succeeded, failed, were denied, or were rate-limited over the selected window?
- Which users, tenants, models, VMRs, and endpoints are driving usage and cost estimates?
- Which saved report definitions should operators reuse for daily user cost, model usage, TTFT, or reliability review?

```text
GET  /v1.0/analytics/catalog
GET  /v1.0/analytics/summary?range=lastDay&bucketSeconds=3600&tokenUnitCost=0.00001
GET  /v1.0/analytics/ttft?range=lastDay&groupBy=RequestorUserId
GET  /v1.0/analytics/tokens?range=lastWeek&modelName={model}
GET  /v1.0/analytics/costs?range=lastDay&requestorUserGuid={user}&tokenUnitCost=0.00001
GET  /v1.0/analytics/reports
POST /v1.0/analytics/reports
POST /v1.0/analytics/query
```

Analytics data is retained for 30 days in the first release. Named ranges are `lastHour`, `lastDay`, `lastWeek`, `lastMonth`, or `custom` with `startUtc` and `endUtc`; custom ranges are clamped to the retained window. Operators can choose bucket granularity with `bucketSeconds`.

Analytics is populated from persisted request-history rows. Request history must be enabled globally in `conductor.json` and enabled on the Virtual Model Runner before the requests are sent; enabling it later only affects new requests. The Docker dashboard create form defaults new VMRs to Request History and Analytics enabled, and existing VMRs can be edited from the Virtual Model Runners page.

Cost output is an estimate only. Conductor multiplies successful reported token usage by the caller-supplied `tokenUnitCost`; it does not model provider billing rules, cached-token discounts, multimodal pricing, currency conversion, taxes, credits, or account-specific contracts. Missing provider token usage is reported as unknown and is not treated as zero.

System administrators can run global analytics and optionally filter to a tenant with `tenantId`. Tenant administrators are forced into their authenticated tenant scope. A tenant user can be granted read-only Analytics access without tenant-admin rights by adding the `analytics.read` label, or by setting a user tag such as `analytics.read=true` or `permissions=analytics.read`.

Saved reports persist the Analytics query, grouping, token-unit-cost, reservation filters, and dashboard display state. They do not schedule execution or snapshot results. The dashboard can load, update, delete, and copy a link to a saved report.

## Configuration Pinning

Model configurations can define pinned properties that are automatically merged into incoming requests:

```json
{
  "Name": "Low Temperature Config",
  "PinnedCompletionsProperties": {
    "temperature": 0.3,
    "top_p": 0.9,
    "max_tokens": 2048
  },
  "PinnedEmbeddingsProperties": {
    "model": "text-embedding-ada-002"
  }
}
```

When a request comes through a virtual model runner, the pinned properties are merged with the request body, allowing you to enforce specific model parameters.

## Health Checking & Rate Limiting

### Endpoint Health Configuration

Model Runner Endpoints support comprehensive health checking with the following properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HealthCheckUrl` | string | `/` | URL path appended to endpoint base URL for health checks |
| `HealthCheckMethod` | enum | `GET` | HTTP method (`GET` or `HEAD`) |
| `HealthCheckIntervalMs` | int | `5000` | Milliseconds between health checks |
| `HealthCheckTimeoutMs` | int | `5000` | Timeout for health check requests |
| `HealthCheckExpectedStatusCode` | int | `200` | Expected HTTP status code for healthy |
| `UnhealthyThreshold` | int | `2` | Consecutive failures before marking unhealthy |
| `HealthyThreshold` | int | `2` | Consecutive successes before marking healthy |
| `HealthCheckUseAuth` | bool | `false` | Include API key (Bearer token) in health check requests |
| `MaxParallelRequests` | int | `4` | Maximum concurrent requests (0 = unlimited) |
| `Weight` | int | `1` | Relative weight for load balancing (1-1000) |
| `ServiceState` | enum | `Normal` | Operator-controlled traffic state: `Normal`, `Draining`, or `Quarantined` |

**Note for OpenAI and vLLM APIs**: When using `api.openai.com` or another OpenAI-compatible backend that requires authentication for model listing, set `HealthCheckUseAuth` to `true` and `HealthCheckUrl` to `/v1/models`.

**Note for Gemini API**: When using `generativelanguage.googleapis.com`, set `HealthCheckUseAuth` to `true` and `HealthCheckUrl` to `/v1beta/models`. Gemini uses the `x-goog-api-key` header rather than bearer token authentication.

### Health Check Behavior

- Endpoints start in an **unhealthy** state and transition to healthy after meeting the `HealthyThreshold`
- Background tasks continuously monitor each active endpoint at the configured interval
- Endpoints with the same effective health-check request share one upstream HTTP probe, then apply their own expected status codes and thresholds to that shared result
- The proxy automatically excludes unhealthy endpoints from request routing
- Draining endpoints continue to be probed and remain available for already-pinned session-affinity traffic, but they do not receive new assignments
- Quarantined endpoints continue to be probed for diagnostics, but they are excluded from all routing, including pinned-session reuse
- When all endpoints are unhealthy, requests return `502 Bad Gateway`
- When all endpoints are at capacity, requests return `429 Too Many Requests`
- When all configured endpoints are quarantined or draining, requests are denied with an explicit service-state-specific error

### Rate Limiting

- Each endpoint tracks in-flight requests in real-time
- The `MaxParallelRequests` property enforces a per-endpoint concurrency limit
- Set to `0` for unlimited concurrent requests
- Requests are counted from start until the response completes (including streaming)

### Weighted Load Balancing

- The `Weight` property influences endpoint selection in round-robin and random modes
- Higher weight = more traffic directed to that endpoint
- Example: Endpoint A (weight=3) receives 3x more traffic than Endpoint B (weight=1)

### Health Status API

Monitor endpoint health via the REST API:

```bash
# Health of all endpoints in tenant
GET /v1.0/modelrunnerendpoints/health

# Put an endpoint into maintenance drain mode
POST /v1.0/modelrunnerendpoints/{id}/drain

# Resume normal traffic
POST /v1.0/modelrunnerendpoints/{id}/resume

# Exclude an endpoint from all routing while keeping health visibility
POST /v1.0/modelrunnerendpoints/{id}/quarantine

# Health of endpoints for a specific VMR
GET /v1.0/virtualmodelrunners/{id}/health
```

Response includes:
- Current health state (healthy/unhealthy)
- Operator-managed service state (`Normal`, `Draining`, `Quarantined`)
- In-flight request count
- Total uptime/downtime
- Uptime percentage
- Last check timestamp
- Last error message (if any)

## Docker

The included Docker Compose setup uses local build contexts:

- **Server**: `src/Conductor.Server/Dockerfile`
- **Dashboard**: `dashboard/Dockerfile`

### Building Docker Images

```bash
# Build server
./build-server.sh  # or build-server.bat on Windows

# Build dashboard
./build-dashboard.sh  # or build-dashboard.bat on Windows
```

## License

MIT License - see [LICENSE.md](LICENSE.md) for details.

## Attributions

<a href="https://www.flaticon.com/free-icons/music" title="music icons">Music icons created by Freepik - Flaticon</a>
