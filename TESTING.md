# Conductor Testing Guide

Conductor’s automated test suite now uses **Touchstone** so the same shared test cases can run through multiple hosts without duplicating test logic.

## Projects

| Project | Location | Purpose |
|---|---|---|
| `Test.Shared` | `src/Test.Shared/` | Authoritative source for all test cases and Touchstone descriptors |
| `Test.Automated` | `src/Test.Automated/` | Console runner built on `Touchstone.Cli` |
| `Test.Xunit` | `src/Test.Xunit/` | xUnit host built on `Touchstone.XunitAdapter` |
| `Test.Nunit` | `src/Test.Nunit/` | NUnit host built on `Touchstone.NunitAdapter` |

`Test.Shared` contains the real tests. `Test.Xunit` and `Test.Nunit` each expose the same descriptors through their own framework adapters, so `dotnet test src/Conductor.sln` runs the shared suite twice on purpose.

## Quick Start

Run the framework hosts through the solution:

```bash
dotnet test src/Conductor.sln
```

Run only the xUnit host:

```bash
dotnet test src/Test.Xunit/Test.Xunit.csproj
```

Run only the NUnit host:

```bash
dotnet test src/Test.Nunit/Test.Nunit.csproj
```

Run the console host:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj
```

`Test.Shared` is not a standalone test host. Running `dotnet test src/Test.Shared/Test.Shared.csproj` only compiles the shared descriptors; it does not execute them. Use `Test.Automated`, `Test.Xunit`, or `Test.Nunit` when you need real execution.

Write console results to JSON:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --results TestResults/touchstone-results.json
```

Collect coverage from a framework host:

```bash
dotnet test src/Test.Xunit/Test.Xunit.csproj --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

## How It Works

`Test.Shared` uses a reflection-based Touchstone registry:

- Any non-abstract public class in `src/Test.Shared/` whose name ends with `Tests` becomes a Touchstone suite.
- Any public parameterless `void`, `Task`, or `ValueTask` method on that class becomes a Touchstone test case.
- `Initialize`, `InitializeAsync`, `Dispose`, and `DisposeAsync` are treated as per-test lifecycle hooks and are not exposed as test cases.
- Descriptor display names are emitted as `ClassName.MethodName`.

This preserves the original xUnit-style “new class instance per test” behavior while making the test definitions runner-agnostic.

## Writing Tests

Add new tests under `src/Test.Shared/` and keep the existing structure by domain:

- `Core/Enums`
- `Core/Helpers`
- `Core/Models`
- `Core/Settings`
- `Server/Controllers`
- `Server/Integration`
- `Server/Services`

Guidelines:

- Name files `*Tests.cs`.
- Add tests as public parameterless methods returning `void`, `Task`, or `ValueTask`.
- Use optional `InitializeAsync`/`Dispose` hooks when a test class needs setup or cleanup.
- Keep using `FluentAssertions`.
- Prefer explicit positive and negative coverage for any behavior you touch.

## Feature Expectations

Changes in the following areas should ship with targeted shared-suite coverage:

- model access policy validation, evaluation, routing enforcement, list-model filtering, and request-history recording
- routing explanation, session affinity, and policy evidence
- load-balancing mode compatibility, eligibility screening, route-scoped state, and policy evidence
- validation routes and effective-configuration preview
- request-history search, summary, redaction, and retention behavior
- observability metrics aggregation and export
- endpoint drain, resume, quarantine, and health-payload visibility
- shared health-check coalescing when multiple endpoints point at the same effective health URL
- compatibility behavior where new fields must default cleanly when older payloads omit them

When a feature spans the dashboard or SDKs, verify those surfaces too:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj
dotnet build src/Conductor.sln
cd dashboard && npm run build
cd sdk/javascript && npm test
cd sdk/python && set PYTHONPATH=src && python -m unittest discover -s tests
```

## Load Balancing Release Gate

Load-balancing changes must be verified across backend routing behavior, enum serialization, dashboard create/edit affordances, docs, Postman examples, and SDK compatibility checks.

Run the focused product gate before marking load-balancing work complete:

```powershell
dotnet restore src\Conductor.sln --disable-parallel
dotnet list src\Conductor.sln package --outdated
dotnet list src\Conductor.sln package --vulnerable --include-transitive
dotnet build src\Conductor.sln --no-restore
dotnet test src\Test.Xunit\Test.Xunit.csproj --no-restore --no-build --logger "console;verbosity=minimal"
dotnet test src\Test.Nunit\Test.Nunit.csproj --no-restore --no-build --logger "console;verbosity=minimal"
dotnet run --project src\Test.Automated\Test.Automated.csproj --no-restore --no-build -- --results "$env:TEMP\conductor-test-automated-results.json"
Push-Location dashboard; npm.cmd run build; Pop-Location
Push-Location sdk\javascript; npm.cmd test; Pop-Location
Push-Location sdk\python; $env:PYTHONPATH='src'; python -m pytest; Pop-Location
Push-Location sdk\csharp; dotnet test --no-restore --no-build; Pop-Location
Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null
```

Focused shared tests for `LeastRecentlyUsed` live in:

- `src/Test.Shared/Core/Enums/EnumTests.cs`
- `src/Test.Shared/Server/Services/RoutingDecisionServiceTests.cs`

Manual dashboard checks:

- Create a VMR and confirm `Least Recently Used` appears in the same load-balancing mode control as the existing values.
- Edit an existing VMR, switch to `Least Recently Used`, save, reopen, and confirm the value persists without changing unrelated fields.
- Cancel an edit after changing the load-balancing mode and confirm the original value is retained.
- Verify the create/edit modal at 1280px, 768px, and 390px so the selector, labels, validation messages, and action buttons do not overlap or wrap awkwardly.

## Model Access Release Gate

Model access policy changes must be verified across backend policy evaluation, proxy authentication, list-model response shaping, request history, SDK helpers, docs, and the Postman collection.

Run the focused product gate before marking model access complete:

```powershell
dotnet build src/Conductor.sln --no-restore
dotnet test src/Test.Xunit/Test.Xunit.csproj --logger "console;verbosity=minimal"
dotnet test src/Test.Nunit/Test.Nunit.csproj --logger "console;verbosity=minimal"
Push-Location sdk/javascript; npm.cmd test; Pop-Location
Push-Location sdk/python; $env:PYTHONPATH='src'; python -m unittest discover -s tests; Pop-Location
Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null
```

Focused shared tests for this feature live in:

- `src/Test.Shared/Server/Services/ModelAccessControlServiceTests.cs`
- `src/Test.Shared/Server/Services/ModelAccessListModelsResponseFilterTests.cs`
- `src/Test.Shared/Server/Services/RoutingDecisionServiceTests.cs`
- `src/Test.Shared/Server/Controllers/ModelAccessPolicyControllerTests.cs`
- `src/Test.Shared/Server/Services/ModelLoadAuthorizationTests.cs`

Manual validation:

- Create a default-deny policy, add a credential allow rule, attach it to a VMR, and confirm allowed completions route.
- Confirm a credential without an allow rule receives `403` and no provider request is sent.
- Enable monitor mode and confirm a would-deny request still routes while request history records `ModelAccessWouldDeny=true`.
- Enable `RequireCredentialForProxy` and confirm missing or invalid proxy credentials return `401`.
- Confirm OpenAI, Gemini, and Ollama list-model responses hide denied models when `ListModelsBehavior=Filter`.
- Switch `ListModelsBehavior=Synthesize` and confirm returned model lists come from allowed active VMR model definitions.
- Confirm denial logs do not include bearer tokens, Gemini `key` values, request bodies, provider URLs, or endpoint API keys.
- Confirm policy deletion returns conflict while attached, then succeeds with `forceDetach=true`.

## Model Loading Release Gate

Model loading changes must be verified across backend routing/auth, provider probe construction, dashboard UX, SDK helpers, docs, and the Postman collection.

Run the focused product gate before marking model loading complete:

```powershell
dotnet build src/Conductor.sln
dotnet test src/Conductor.sln
Push-Location dashboard; npm.cmd run build; Pop-Location
Push-Location sdk/javascript; npm.cmd test; Pop-Location
Push-Location sdk/python; $env:PYTHONPATH='src'; python -m unittest discover -s tests; Pop-Location
Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null
```

Manual dashboard checks:

- Open a model runner endpoint action menu, select `Load Model`, submit `gemma3:4b`, and confirm the result table shows endpoint outcome, HTTP status, mechanism, duration, and verification.
- On an Ollama-type model runner endpoint, select `Manage Models`, refresh the list, verify the pull form accepts a model name, and confirm delete uses the inline confirmation state before sending the request.
- Open a VMR action menu, select `Load Model`, verify attached model definitions are selectable, switch `TargetMode` through `SelectedEndpoint`, `AllEligibleEndpoints`, `AllConfiguredEndpoints`, and `SpecificEndpointIds`, and confirm target endpoints render correctly.
- For OpenAI and Gemini, select `ChatCompletion` or `Embeddings` and confirm the hosted-provider billing warning appears before submit.
- Verify at 1280px, 768px, and 390px that the modal controls, result table, long endpoint names, and long model names do not overlap.

Optional local Ollama smoke test:

```powershell
$body = @{
  Model = "gemma3:4b"
  ProbeKind = "Auto"
  KeepAlive = "30m"
  VerifyLoaded = $true
  TimeoutMs = 300000
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:9000/v1.0/modelrunnerendpoints/$env:CONDUCTOR_ENDPOINT_ID/load-model?tenantId=$env:CONDUCTOR_TENANT_ID" `
  -Headers @{ Authorization = "Bearer $env:CONDUCTOR_TOKEN" } `
  -ContentType "application/json" `
  -Body $body
```

Expected result: `Success=true`, `OutcomeCode=Loaded` or `AlreadyAvailable`, and `VerifiedLoaded=true` when Ollama `/api/ps` confirms residency. Missing local models should fail clearly; this feature must not call Ollama `/api/pull`.

Example:

```csharp
public class MyComponentTests
{
    public void Value_DefaultsCorrectly()
    {
        MyComponent component = new MyComponent();
        component.Value.Should().Be(42);
    }

    public async Task Create_WithInvalidInput_Throws()
    {
        Func<Task> act = async () => await _Controller.Create(null);
        await act.Should().ThrowAsync<Exception>();
    }
}
```

## Request Analytics Release Gate

Request analytics changes must be verified across backend capture, database retention, aggregate query behavior, dashboard build output, SDK helpers, Postman examples, and operator-facing documentation.

Run the full product gate before marking request analytics complete:

```powershell
dotnet build src/Conductor.sln
dotnet test src/Test.Xunit/Test.Xunit.csproj
dotnet test src/Test.Nunit/Test.Nunit.csproj
dotnet run --project src/Test.Automated/Test.Automated.csproj
Push-Location dashboard; npm run build; Pop-Location
Push-Location sdk/javascript; npm test; Pop-Location
Push-Location sdk/python; $env:PYTHONPATH='src'; python -m unittest discover -s tests; Pop-Location
Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null
```

The focused shared tests for this feature live in:

- `src/Test.Shared/Server/Services/RequestAnalyticsServiceTests.cs`
- `src/Test.Shared/Server/Services/RequestHistoryCleanupServiceTests.cs`
- `src/Test.Shared/Core/Database/RequestHistorySchemaTests.cs`
- `src/Test.Shared/Server/Integration/DatabaseMigrationTests.cs`

These tests should prove that:

- Provider metrics are parsed when OpenAI-compatible usage data is available.
- Malformed or missing provider usage remains `null`, not zero.
- Analytics overview aggregation returns request counts, success counts, percentiles, telemetry coverage, stage breakdowns, endpoint summaries, slowest requests, and chart buckets.
- Explicit over-large analytics ranges are capped to a bounded range and bucket count.
- Metadata retention cleanup deletes analytics events with expired request history rows.
- Fresh and upgraded databases create the request analytics columns, table, and indexes.

## Request Analytics Manual QA

Dashboard visual QA requires realistic request-history rows with full analytics, partial analytics, missing analytics, failures, long names, and null provider metrics. Use a local server and the dashboard dev server:

```powershell
dotnet run --project src/Conductor.Server/Conductor.Server.csproj
Push-Location dashboard; npm run dev; Pop-Location
```

Validate at 1280px, 768px, and 390px:

- Request Analytics loads without layout overlap, clipped controls, or chart text collisions.
- Range controls switch between `lastHour`, `lastDay`, `lastWeek`, and `lastMonth`.
- Volume/outcome, latency, stage composition, provider timing, endpoint/model mix, token throughput, slowest requests, errors/statuses, and telemetry coverage are visible without fetching raw history rows in React.
- Empty states distinguish no traffic from traffic with no analytics captured.
- Null provider metrics render as unavailable, not as zero.
- Long VMR, endpoint, provider, and model names wrap or truncate predictably.
- Chart tooltips stay inside the viewport and do not cover the control that opened them.
- Keyboard users can reach filters, charts, slowest-request links, detail modals, copy controls, and close actions.
- Focus returns to the invoking row or action after closing a modal.
- Request History detail shows trace ID, timing bars, stage table, provider metrics, token profile, and old-row/missing-analytics state.

API Explorer replay is deferred in the current implementation. Until it lands, verify that the dashboard does not offer exact replay for scrubbed, redacted, truncated, or missing bodies.

## Request Analytics Postman QA

The Postman collection must import without JSON errors and expose analytics requests without manual URL surgery. Required variables:

- `baseUrl`
- `bearerToken`
- `tenantGuid`
- `requestHistoryId`
- `traceId`
- `vmrGuid`
- `endpointGuid`
- `providerName`
- `modelName`
- `analyticsRange`
- `analyticsStartUtc`
- `analyticsEndUtc`
- `analyticsBucketSeconds`
- `analyticsStage`
- `analyticsLimit`

If Newman is available, run:

```powershell
newman run Conductor.postman_collection.json --env-var baseUrl=http://localhost:9000 --env-var bearerToken=$env:CONDUCTOR_BEARER_TOKEN
```

If Newman is not available, import `Conductor.postman_collection.json` into Postman, set the variables above, and exercise:

- Request analytics overview with `analyticsRange=lastHour`.
- Request analytics overview with explicit `analyticsStartUtc`, `analyticsEndUtc`, and `analyticsBucketSeconds`.
- Per-request analytics detail by `requestHistoryId`.
- Tenant-scoped calls with a tenant user.
- Cross-tenant or missing-auth calls, which must be rejected by the server.

Record the server version, collection import result, request names run, status codes, and any response-shape mismatches before marking Postman QA complete.

## Request Analytics Troubleshooting Runbook

Use Request Analytics to diagnose by dominant slow stage before changing routing or endpoint settings.

High `capacity_wait` means the request waited for an endpoint capacity slot. Check endpoint concurrency limits, drain/quarantine state, health status, and whether one VMR is saturating shared capacity.

High `upstream_headers` or request-to-headers time means Conductor dispatched upstream but the provider did not return headers quickly. Check provider cold start, network path, DNS/TLS behavior, provider queueing, and upstream service saturation.

High `first_token_wait` means headers arrived but the first token or byte was delayed. Check prompt size, provider prompt-eval metrics, model load duration, tool or schema overhead, and whether streaming final-usage chunks are missing.

High `generation` or low generation TPS means the model is spending time producing output. Check completion token count, max token settings, model size, endpoint hardware, and whether the endpoint is sharing GPU/CPU with other workloads.

High provider load duration, when reported, points to cold model load or eviction. Confirm model residency, keep-warm policy, endpoint memory pressure, and recent health-check or deployment activity.

Denied routing with analytics captured should show routing-only stages. Inspect the routing explanation, denial reason, policy fallback, session affinity, VMR settings, endpoint health, and auth context.

Missing analytics on new rows usually means request history capture is disabled, VMR history capture is disabled, analytics capture failed, or the request predates the analytics schema. Missing analytics on old rows can also be expected after retention cleanup.

Null token or provider timing metrics mean the provider did not report those fields or the response was malformed. Do not interpret null metrics as zero-cost or zero-duration work.

Redacted, scrubbed, truncated, or metadata-only bodies cannot be replayed exactly. Use the visible retained metadata and redaction state to reconstruct a safe test request manually.

## Request Analytics Security And Performance Checks

Before release, confirm:

- Persisted analytics never include authorization headers, cookies, bearer tokens, API keys, or secret-like provider headers.
- Raw provider metrics are bounded and redacted before persistence.
- Tenant scoping is enforced on analytics overview, detail, delete, retention, SDK, and Postman flows.
- Request history metadata deletion and retention cleanup remove child analytics events.
- Prometheus metrics remain low-cardinality and do not include request IDs, trace IDs, endpoint URLs, model names, or provider request IDs as labels.
- Analytics overview queries are bounded by tenant and date range, with at most 31 days and 720 returned buckets in the initial implementation.
- Large or over-limit analytics scans produce warning logs and stay within the server-side row limit.

## Analytics Workspace Release Gate

Analytics workspace changes must be verified across backend aggregation, dashboard UX, SDK helpers, Postman examples, tenant scoping, documentation, and release notes.

Run the focused gate before marking `/v1.0/analytics` changes complete:

```powershell
git diff --check
dotnet build src/Conductor.sln --no-restore
dotnet test src/Test.Xunit/Test.Xunit.csproj --no-restore
Push-Location dashboard; npm.cmd run build; Pop-Location
Push-Location sdk/javascript; npm.cmd test; Pop-Location
Push-Location sdk/python; $env:PYTHONPATH='src'; python -m pytest; Pop-Location
dotnet build sdk/csharp/Conductor.Sdk/Conductor.Sdk.csproj
Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null
```

Required backend checks:

- `/v1.0/analytics/catalog` returns the shipped metric, dimension, range, granularity, retention, and unavailable export-format catalog.
- `/v1.0/analytics/query` and GET convenience routes return the same result shape and honor `range`, `startUtc`, `endUtc`, `bucketSeconds`, `groupBy`, `limit`, and filter parameters.
- System administrators can query global analytics and optionally filter by `tenantId`.
- Tenant administrators are forced into their authenticated tenant scope.
- Tenant users with the `analytics.read` user label or tag can read tenant-scoped Analytics without full tenant-admin rights.
- TTFT is measured from request received by Conductor to first token or byte received from the provider.
- Token and cost metrics include successful completions only.
- Missing provider usage increments `UnknownTokenUsageCount` and does not become zero usage.
- Estimate-only cost equals successful reported token usage multiplied by caller-supplied `tokenUnitCost`.
- Failed, denied, and rate-limited request counts are still visible even though usage metrics use successful completions.
- Custom ranges are clamped to the documented 30-day retained window and bucket count remains bounded.
- Saved reports persist query filters, grouping, token unit cost, display state, owner, tenant/global scope, labels, and tags.
- Tenant-scoped saved reports cannot be read, updated, or deleted outside the authenticated tenant scope.

Required dashboard checks:

- The sidebar exposes `Analytics` and `/request-analytics` redirects to `/analytics`.
- The first screen shows range, granularity, VMR, endpoint, provider, model, user, credential, token-unit-cost, and refresh controls without layout overlap at 1280px, 768px, and 390px.
- Metric cards show request count, success rate, P95 TTFT, unknown token usage, token totals, and estimate-only cost.
- User and credential breakdown tables show requests, success rate, failures, denials, rate limits, TTFT, tokens, unknown usage, estimated cost, coverage, and last seen time.
- Charts and tables make clear that cost is an estimate and that the retained window is 30 days.
- Saved report controls can create, update, delete, load, and copy a dashboard link without changing unrelated filters.
- Empty states and null token/TTFT fields render as unavailable, not as zero.

Required SDK and Postman checks:

- JavaScript, Python, and C# clients expose catalog, query, summary, time-series, TTFT, tokens, costs, users, and access helpers.
- SDK tests or build output prove URL, method, query/body serialization, tenant scope, and token unit cost behavior for shipped helpers.
- The Postman collection imports without JSON errors and contains Analytics Workspace requests for catalog, summary, query, saved-report CRUD, TTFT by user, tokens by model, user cost estimate, and access/reliability counts.

Manual operator workflows to exercise:

- Average TTFT for a specific endpoint and VMR, then drill into a user.
- Total token usage over the last day, week, and custom range for a model and VMR.
- Estimate-only cost for a user over the last day using a supplied per-token unit cost.
- Create and reload a saved report for daily user cost, then copy its dashboard link.
- Failed request type review for denied and rate-limited requests.
- Tenant-admin login verifies that cross-tenant filters cannot escape the authenticated tenant.

## VMR Reservations Release Gate

Reservation changes must prove exclusive admission without weakening normal ACL/model-access behavior.

Required backend checks:

- Reservation CRUD validates tenant, VMR, UTC window, active subject existence, duplicate subjects, and overlapping active windows.
- Active reservation participant users, participant credentials, and credentials owned by participant users are admitted through the reservation gate.
- Anonymous callers, unrelated users, unrelated credentials, and unlisted admins are denied with `ReservationAuthenticationRequired`, `ReservationDenied`, or `ReservationDrainDenied` before endpoint inventory or provider calls.
- List-model, show-model, embeddings, chat/completions, routing simulation, explain, and VMR model-load paths evaluate reservation state where they exercise VMR access.
- Reservation allow decisions still run normal ACL, request-type, model access, endpoint health, load balancing, and provider checks.
- Request history search/summary, analytics overview, and Analytics workspace queries filter by `reservationGuid`, `reservationDecision`, and `reservationReasonCode`.
- Backup/restore exports, validates, restores, skips, overwrites, and fails reservation records and nested subjects according to conflict mode.

Required dashboard checks:

- Every table page has an icon refresh control; the VMR Reservations table refresh reloads tenant-scoped reservation data.
- `/reservations?vmrId=...` filters to that VMR, and create defaults to the selected VMR and tenant.
- VMR rows show reservation state badges for open, upcoming, drain, active/reserved, and conflict states.
- Request History shows reservation columns, filters, and detail fields for reservation id/name, decision, reason, and UTC window.
- Analytics shows reservation-denial cards, per-reservation denial counts, and reservation filters that persist in saved reports and dashboard links.

Required docs and tooling checks:

- `REST_API.md`, `README.md`, `MANAGING_RESERVATIONS.md`, SDK examples, and Postman examples match actual route names and request/response shapes.
- The Postman collection imports without JSON errors and includes positive create/list/effective-access flows plus negative outsider/overlap examples.
- Logs, request history, analytics, and dashboard views expose reservation denial reason codes without bearer tokens, credential secrets, provider keys, or raw secret-bearing request bodies.

## Packages

| Project | Key Packages |
|---|---|
| `Test.Shared` | `Touchstone.Core`, `FluentAssertions` |
| `Test.Automated` | `Touchstone.Cli` |
| `Test.Xunit` | `Touchstone.XunitAdapter`, `xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` |
| `Test.Nunit` | `Touchstone.NunitAdapter`, `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` |

## Notes

- The console runner is the fastest way to inspect raw descriptor output locally.
- The xUnit and NUnit hosts should remain thin; do not move real test logic out of `Test.Shared`.
- If a shared test starts failing in one framework host and not the other, treat that as a runner integration issue first, not as a reason to fork the test logic.
