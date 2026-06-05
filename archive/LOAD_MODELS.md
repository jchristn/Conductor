# Load Models API Implementation Plan

## Purpose

Conductor needs a control-plane API that asks a configured model runner endpoint, or a virtual model runner that resolves to one or more endpoints, to load or warm a model before user traffic reaches it. The API should cover local runners such as Ollama, OpenAI-compatible runners such as vLLM, and hosted providers such as OpenAI and Gemini without pretending those providers all support the same host-local loading behavior.

The first production-ready version should expose one consistent API shape, return explicit provider semantics, and surface the action in the dashboard where operators already manage model runner endpoints and virtual model runners.

## Progress Legend

Use these markers while implementing:

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete
- `[!]` Blocked, with note in the Evidence column

## Product Decisions

| Status | Decision | Rationale | Evidence |
| --- | --- | --- | --- |
| [x] | Add `POST /v1.0/modelrunnerendpoints/{id}/load-model` for direct endpoint warmup. | Operators often know the exact host they want to prepare, such as an Ollama endpoint on a GPU box. | Implemented in `ModelRunnerEndpointController` and `ModelRunnerRouteModule`; `dotnet build src/Conductor.sln -v:minimal` passes. |
| [x] | Add `POST /v1.0/virtualmodelrunners/{id}/load-model` for route-aware warmup. | Operators should be able to warm the effective model behind a VMR without manually reconstructing its model definition, configuration, and endpoint mapping. | Implemented in `VirtualModelRunnerController` and `VirtualModelRunnerRouteModule`; `dotnet build src/Conductor.sln -v:minimal` passes. |
| [x] | Treat the operation as "load or warm when possible, verify when explicit loading is not supported." | OpenAI and Gemini do not expose a Conductor-controlled host-local model load primitive. A successful result must say `VerifiedRemote` or `NoExplicitLoadSupported`, not `Loaded`. | `ModelLoadProbeBuilder`, `ModelLoadVerificationService`, and `ModelLoadService` return provider-specific `Loaded`, `Verified`, and `VerifiedRemote` outcomes. |
| [x] | Use synchronous execution for v1, with a bounded timeout and per-endpoint result objects. | Keeps the API simple for Postman, SDKs, and dashboard. Async jobs can be added later if large-model warmup routinely exceeds operator HTTP timeouts. | `ModelLoadRequest.TimeoutMs` clamps the upstream timeout; `ModelLoadResponse.EndpointResults` carries per-endpoint timings and outcomes. |
| [x] | Require tenant-scoped execution permission. | Model loading consumes GPU memory and can trigger billable upstream calls. Until full RBAC exists, add the new request types to tenant-admin authorization or explicitly document any compatibility exception. | Added `LoadModelRunnerEndpointModel` and `LoadVirtualModelRunnerModel` to `RequiresTenantAdminAccess` and `RequestTypeResolver`. |
| [x] | Do not pull or install missing models in this feature. | Loading a model already present on the host is different from fetching model artifacts. Ollama pull and delete already exist as model-management proxy operations. | Provider adapter never calls Ollama `/api/pull`; missing models fail through provider status or metadata verification. |

## Assumptions And Open Questions

| Status | Item | Plan Impact | Evidence |
| --- | --- | --- | --- |
| [x] | Assume a missing `Model` can be resolved from exactly one active attached `ModelDefinition` on a VMR. | If zero or multiple active model definitions exist, the API returns `400 ModelRequired`. | `ModelLoadService.ResolveModelAsync` implements this rule. |
| [x] | Decide whether endpoint-level loading should be allowed when `Active=false`. | Recommended: allow direct endpoint load for inactive endpoints so operators can warm before resume. VMR loading should exclude inactive endpoints unless `IncludeInactive=true`. | Direct endpoint path does not filter `Active`; VMR target selection skips inactive endpoints unless `IncludeInactive=true`. |
| [x] | Decide whether hosted providers should default to metadata-only verification or a tiny paid probe. | Recommended default for `ProbeKind=Auto`: metadata/list-model verification for OpenAI, vLLM, and Gemini where possible; tiny generation only when explicitly requested. | `ModelLoadProbeBuilder` maps OpenAI, vLLM, and Gemini `Auto` to `MetadataOnly`; Ollama maps `Auto` to native generate. |
| [x] | Decide whether dashboard load actions should be visible to non-tenant-admin users. | Recommended: hide or disable based on server authorization once effective-permission support exists; in v1, rely on server-side rejection. | Dashboard exposes actions to authenticated operators and relies on server-side tenant-admin rejection for v1. |

## Requirement Matrix

| Area | Source | Applicable Guidance | Plan Impact |
| --- | --- | --- | --- |
| Authentication and authorization | `C:\code\agents\requirements\AUTHENTICATION.md` | Non-system requests resolve one tenant, permissions map to operation types, client-side checks are UI-only, authorization failures should be auditable. | Add `RequestTypeEnum` values and resolver entries; resolve tenant through existing auth metadata plus `tenantId`; require an execute/admin-level control-plane permission; never rely on dashboard visibility for enforcement. |
| Backend structure | `C:\code\agents\requirements\BACKEND_ARCHITECTURE.md` | Watson routes, typed DTOs, feature route modules, service layer, `CancellationToken`, OpenAPI metadata. | Add typed load request/response DTOs, service classes under `Conductor.Server\Services`, controller methods, route registrations with OpenAPI docs, and cancellation-aware HTTP calls. |
| Backend tests | `C:\code\agents\requirements\BACKEND_TEST_ARCHITECTURE.md` | Shared test descriptors in `Test.Shared`, run through automated, xUnit, and NUnit. | Add shared service/controller/route tests and make the existing runners consume them. |
| Code style | `C:\code\agents\requirements\CODE_STYLE.md` | No `var`, no tuples, XML docs on public types, in-namespace usings, one public class or enum per file, `ConfigureAwait(false)`. | Apply the stricter style to every new C# file. |
| Frontend | `C:\code\agents\requirements\FRONTEND_ARCHITECTURE.md` | Dashboard uses hand-rolled `fetch` API client, views under `dashboard/src/views`, reusable modals/tables/action menus, responsive checks. | Add client methods to `dashboard/src/api/api.js`; add load actions and modals to `ModelRunnerEndpoints.jsx` and `VirtualModelRunners.jsx`; verify desktop/tablet/mobile behavior. |
| Internationalization | `C:\code\agents\requirements\I18N.md` | User-facing strings and formatting should flow through i18n and locale-aware helpers. | Existing dashboard is not i18n-ready. Track an explicit task to either add the feature strings through an i18n foundation or document the temporary product gap before launch. |
| Repository and SDKs | `C:\code\agents\requirements\REPOSITORY_REQUIREMENTS.md` | SDKs live under `sdk/{language}`, each with README and tests. | Update JavaScript and Python SDKs, their tests, and README examples. |
| Documentation | `C:\code\agents\requirements\WRITING_DOCUMENTS.md` | Human-readable documents should be specific, useful, and not generic filler. | Keep docs concrete: endpoint paths, request fields, provider behavior, examples, and operational caveats. |

## API Contract

### Endpoints

| Status | Method | Path | Purpose | Evidence |
| --- | --- | --- | --- | --- |
| [x] | `POST` | `/v1.0/modelrunnerendpoints/{id}/load-model` | Load or verify a model on one concrete endpoint. Optional `tenantId` query for admin/cross-tenant callers. | Implemented with typed route, OpenAPI metadata, tenant-admin request type, dashboard action, SDK helpers, and Postman request. |
| [x] | `POST` | `/v1.0/virtualmodelrunners/{id}/load-model` | Resolve the VMR's effective model and target endpoint set, then load or verify the model. Optional `tenantId` query. | Implemented with typed route, OpenAPI metadata, tenant-admin request type, dashboard action, SDK helpers, and Postman request. |

### Request Shape

```json
{
  "Model": "gemma3:4b",
  "ModelDefinitionId": null,
  "ProbeKind": "Auto",
  "TargetMode": "SelectedEndpoint",
  "EndpointIds": [],
  "InputText": "conductor warmup",
  "KeepAlive": "30m",
  "TimeoutMs": 300000,
  "MaxRetries": 0,
  "VerifyLoaded": true,
  "IncludeInactive": false,
  "DryRun": false
}
```

Field notes:

- `Model`: model name/tag to load. Required for endpoint loading. Optional for VMR loading only when a single active attached model definition exists.
- `ModelDefinitionId`: optional VMR-scoped model definition selector. When present, `Model` must either be empty or match the definition name.
- `ProbeKind`: `Auto`, `MetadataOnly`, `ChatCompletion`, `Completion`, `Embeddings`, or `NativeGenerate`.
- `TargetMode`: VMR only. `SelectedEndpoint`, `AllEligibleEndpoints`, `AllConfiguredEndpoints`, or `SpecificEndpointIds`.
- `EndpointIds`: VMR only, required when `TargetMode=SpecificEndpointIds`.
- `InputText`: non-sensitive tiny prompt/input. Default should be short and documented because it may be sent to a hosted provider.
- `KeepAlive`: provider-specific retention hint. Used by Ollama. Ignored by providers that do not support it, with an ignored-field note in the response.
- `TimeoutMs`: bounded per upstream attempt. Clamp to a sane range, for example 1,000 to 1,800,000 ms.
- `VerifyLoaded`: when true, run the provider-specific post-check where one exists, such as Ollama `/api/ps`.
- `IncludeInactive`: endpoint loading may use inactive endpoints. VMR loading requires this flag to include inactive endpoints under non-selected target modes.
- `DryRun`: returns the planned provider call and target endpoints without sending upstream traffic.

### Response Shape

```json
{
  "Success": true,
  "TenantId": "ten_...",
  "TargetType": "VirtualModelRunner",
  "TargetId": "vmr_...",
  "Model": "gemma3:4b",
  "ProbeKind": "NativeGenerate",
  "StartedUtc": "2026-06-05T17:00:00.000Z",
  "CompletedUtc": "2026-06-05T17:00:04.250Z",
  "DurationMs": 4250,
  "OutcomeCode": "Loaded",
  "Message": "Model load probe completed on 1 endpoint.",
  "RoutingDecision": {
    "SelectedEndpointId": "mre_...",
    "EffectiveModel": "gemma3:4b"
  },
  "EndpointResults": [
    {
      "EndpointId": "mre_...",
      "EndpointName": "gpu-host-01",
      "ApiType": "Ollama",
      "BaseUrl": "http://gpu-host-01:11434",
      "Success": true,
      "OutcomeCode": "Loaded",
      "ProviderStatusCode": 200,
      "Mechanism": "OllamaGenerate",
      "RequestPath": "/api/generate",
      "DurationMs": 4211,
      "StartedUtc": "2026-06-05T17:00:00.039Z",
      "CompletedUtc": "2026-06-05T17:00:04.250Z",
      "VerifiedLoaded": true,
      "IgnoredFields": [],
      "ErrorMessage": null
    }
  ]
}
```

Outcome codes should be stable strings:

- `Loaded`
- `AlreadyAvailable`
- `Verified`
- `VerifiedRemote`
- `NoExplicitLoadSupported`
- `DryRun`
- `Skipped`
- `Failed`
- `TimedOut`
- `UnauthorizedUpstream`
- `ModelRequired`
- `ModelNotAttached`
- `NoEligibleEndpoints`
- `UnsupportedApiType`

## Provider Behavior Matrix

| Status | Provider | Auto Behavior | Verification | Caveat | Evidence |
| --- | --- | --- | --- | --- | --- |
| [x] | Ollama | Send `POST /api/generate` with `{ model, prompt, stream:false, keep_alive, options:{ num_predict:1 } }` for completion models. Use `/api/embed` when `ProbeKind=Embeddings`. | If `VerifyLoaded=true`, call `/api/ps` and mark `AlreadyAvailable` or `Loaded` when the model appears. | Do not call `/api/pull`; missing models should fail clearly. | Implemented in `ModelLoadProbeBuilder` and verified by focused tests. |
| [x] | vLLM | Check `/v1/models` for the model. If caller requests a probe, send OpenAI-compatible completion, chat, or embedding request with minimal output. | `/v1/models` confirms whether the served model is available. | Most vLLM deployments load models at process startup; Conductor can warm tokenization/graph paths but cannot make vLLM load arbitrary new weights through OpenAI APIs. | Implemented as OpenAI-compatible adapter with vLLM mechanism names; focused completion test added. |
| [x] | OpenAI | Prefer metadata/list-model verification for `Auto`. Optional tiny chat/completion/embedding probe only when requested. | Provider response success is verification. | Hosted OpenAI does not expose host-local loading. Mark as `VerifiedRemote` or `NoExplicitLoadSupported`; warn about billable probes. | `Auto` metadata verification implemented; dashboard/docs warn for explicit hosted-provider probes. |
| [x] | Gemini | Prefer model metadata/list verification for `Auto`. Optional `generateContent` or `embedContent` probe only when requested. | Provider response success is verification. | Hosted Gemini does not expose host-local loading. Mark as `VerifiedRemote` or `NoExplicitLoadSupported`; warn about billable probes. | `Auto` metadata verification and explicit Gemini probes implemented; dashboard/docs warn for explicit hosted-provider probes. |
| [x] | Unknown future provider | Return `400 UnsupportedApiType` unless an adapter exists. | None. | Do not fall back to arbitrary generic JSON calls without an explicit adapter. | Probe builder rejects unsupported API types instead of guessing a generic request shape. |

## Backend Work

### DTOs And Enums

Create one class or enum per file, with XML docs, no tuples, no `var`, and cancellation-aware service APIs.

| Status | File | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | `src/Conductor.Core/Enums/ModelLoadProbeKindEnum.cs` | Add `Auto`, `MetadataOnly`, `ChatCompletion`, `Completion`, `Embeddings`, `NativeGenerate`. | JSON string enum serialization works with existing serializer behavior. | File added; server solution builds cleanly. |
| [x] | `src/Conductor.Core/Enums/ModelLoadTargetModeEnum.cs` | Add `SelectedEndpoint`, `AllEligibleEndpoints`, `AllConfiguredEndpoints`, `SpecificEndpointIds`. | VMR route rejects invalid combinations with `400`. | File added; `ModelLoadService.ResolveTargetsAsync` validates supported modes. |
| [x] | `src/Conductor.Core/Enums/ModelLoadOutcomeEnum.cs` | Add stable response outcome values. | Dashboard and SDKs do not parse human text. | File added and used by response contracts and service outcomes. |
| [x] | `src/Conductor.Core/Requests/ModelLoadRequest.cs` | Add typed request contract and validation-friendly defaults. | No fixed contract uses `JsonElement`; timeout is clamped; lists default to empty. | File added with timeout/retry clamps and non-null endpoint ID list. |
| [x] | `src/Conductor.Core/Responses/ModelLoadResponse.cs` | Add top-level response contract. | Includes target, timing, model, outcome, routing decision summary, and endpoint results. | File added and returned by both routes. |
| [x] | `src/Conductor.Core/Responses/ModelLoadEndpointResult.cs` | Add per-endpoint result contract. | Includes endpoint ID/name, API type, mechanism, status code, duration, ignored fields, verification, and error. | File added and populated by `ModelLoadService.ExecuteForEndpointAsync`. |
| [x] | `src/Conductor.Core/Responses/ModelLoadPlan.cs` | Add optional dry-run plan contract if not folded into response. | Dry run can show exact target endpoints and provider mechanism without upstream traffic. | Folded into `ModelLoadResponse.EndpointResults` with `OutcomeCode=DryRun`, `Mechanism`, `RequestPath`, target endpoint IDs, and no upstream transport call. |

### Service Layer

The service should be the only place that knows how to convert a provider type and probe mode into upstream HTTP traffic. Controllers should resolve resources, authorize, and delegate.

| Status | File | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | `src/Conductor.Server/Services/ModelLoadService.cs` | Implement endpoint and VMR load orchestration. | Public async methods accept `CancellationToken`; route/controller methods call this service; endpoint and VMR flows share provider adapters. | Endpoint and VMR orchestration implemented; server solution builds. |
| [~] | `src/Conductor.Server/Services/ModelLoadProbeBuilder.cs` | Build provider-specific request path/body/header plan from endpoint, model, and request. | Unit tests cover Ollama, OpenAI, vLLM, Gemini, dry run, ignored fields, and invalid probe/provider combinations. | Provider plans implemented; focused tests cover representative provider modes, but not every combination. |
| [~] | `src/Conductor.Server/Services/ModelLoadTransport.cs` | Send upstream HTTP requests with endpoint auth, timeout, redaction-safe logging, and cancellation. | Tests can inject a fake transport or handler; no secrets are written to logs. | `IModelLoadTransport` and `DefaultModelLoadTransport` implemented; service tests inject fake transport. Header-level auth tests remain pending. |
| [~] | `src/Conductor.Server/Services/ModelLoadVerificationService.cs` | Implement optional post-load checks, especially Ollama `/api/ps` and OpenAI-compatible `/v1/models`. | Response distinguishes `Loaded`, `AlreadyAvailable`, `Verified`, and `NoExplicitLoadSupported`. | Verification service implemented; service test covers Ollama `AlreadyAvailable`; broader metadata parsing tests remain pending. |
| [x] | `src/Conductor.Server/Services/RoutingDecisionService.cs` | Reuse existing VMR selection for `TargetMode=SelectedEndpoint` with synthetic load request context and `persistSessionPin=false`. | VMR load respects active state, health, service state, load-balancing policy, strict mode, model definitions, and model configuration mappings. | `ModelLoadService.ResolveSelectedEndpointAsync` calls `RoutingDecisionService.EvaluateAsync(..., false, token)`. |
| [x] | `src/Conductor.Server/Services/OperationalMetricsService.cs` | Add counters and duration histograms for model load attempts. | Prometheus text includes low-cardinality metrics with tenant, target type, API family, outcome, and provider mechanism labels. | Added model-load request and per-endpoint counters/histograms to Prometheus rendering. |

Suggested metrics:

- `conductor_model_load_requests_total`
- `conductor_model_load_endpoint_attempts_total`
- `conductor_model_load_duration_ms`
- `conductor_model_load_endpoint_duration_ms`

### Controller And Routing

| Status | File | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | `src/Conductor.Server/Controllers/ModelRunnerEndpointController.cs` | Add `LoadModel(string tenantId, string id, ModelLoadRequest request, CancellationToken token = default)`. | Returns `404` for missing endpoint, `400` for invalid model/probe, and typed load response on success/failure. | Controller method added; server solution builds. |
| [x] | `src/Conductor.Server/Controllers/VirtualModelRunnerController.cs` | Add `LoadModel(string tenantId, string id, ModelLoadRequest request, CancellationToken token = default)`. | Handles model resolution, target mode validation, and returns per-endpoint results. | Controller method added; server solution builds. |
| [x] | `src/Conductor.Server/Routing/ModelRunnerRouteModule.cs` | Register `POST /v1.0/modelrunnerendpoints/{id}/load-model` with OpenAPI metadata. | `/openapi.json` exposes request and response schema; route requires auth. | Route registered with request/response metadata and `auth: true`. |
| [x] | `src/Conductor.Server/Routing/VirtualModelRunnerRouteModule.cs` | Register `POST /v1.0/virtualmodelrunners/{id}/load-model` with OpenAPI metadata. | `/openapi.json` exposes request and response schema; route requires auth. | Route registered with request/response metadata and `auth: true`. |
| [x] | `src/Conductor.Server/Routing/ConductorRouteContext.cs` | Construct and expose `ModelLoadService` to endpoint and VMR controllers. | No controller creates its own transport or unrelated service dependencies. | Route context accepts the shared service and passes it into both controllers. |
| [x] | `src/Conductor.Server/ConductorServer.cs` | Instantiate `ModelLoadService` after routing, health, and metrics services are available. | Service is disposed if it owns disposable resources. | Server constructs and disposes the shared `ModelLoadService`. |

### Authentication And Request Type Mapping

| Status | File | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | `src/Conductor.Core/Enums/RequestTypeEnum.cs` | Add `LoadModelRunnerEndpointModel` and `LoadVirtualModelRunnerModel`. | Enum XML docs describe the operation as control-plane execution. | Enum values added with control-plane XML docs. |
| [x] | `src/Conductor.Server/Services/RequestTypeResolver.cs` | Map the two new route patterns. | Auth route classifies load routes before generic `{id}` patterns. | Resolver maps both `load-model` route patterns. |
| [x] | `src/Conductor.Core/Authorization/AuthorizationConfig.cs` | Add request types to the selected auth set. Recommended: tenant admin or stronger. | Non-admin cross-tenant requests are denied; tenant ID conflicts are rejected by existing auth path. | Both request types require tenant-admin access. |
| [~] | Authorization tests | Prove unauthenticated, wrong-tenant, non-admin, tenant-admin, global-admin, and admin-header behavior. | Denials use expected status codes and do not leak upstream endpoint secrets. | Request type resolver and tenant-admin config tests pass; end-to-end denial matrix remains pending. |

### Request History And Audit

| Status | Area | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | Request history policy | Decide whether load-model management calls appear in request history or only metrics/logs. | Decision is documented in `REST_API.md`; dashboard result is usable without relying on request history. | `REST_API.md` documents that model loading is not written to request history; use typed response, logs, and `conductor_model_load_*` metrics. |
| [~] | Redaction | Ensure upstream request bodies and headers redact API keys and authorization material. | Tests prove `Authorization`, `x-goog-api-key`, query `key`, and endpoint `ApiKey` are never exposed in response/logs/history. | Transport does not include secrets in typed responses and logs only high-level errors; explicit redaction tests for headers/logs remain pending. |
| [~] | Correlation | Include request ID/correlation fields where existing infrastructure supports them. | Operators can connect dashboard result, logs, and metrics. | Typed response includes target IDs, endpoint IDs, timings, and metrics labels; no new correlation ID added. |

## Dashboard Work

The feature should live inside the existing operator workflows. Add actions to the endpoint and VMR tables rather than adding a separate landing page.

| Status | File | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | `dashboard/src/api/api.js` | Add `loadModelRunnerEndpointModel(id, payload, tenantId)` and `loadVirtualModelRunnerModel(id, payload, tenantId)`. | Methods use existing auth headers and error handling. | Methods added with tenant query handling; `npm.cmd run build` passes. |
| [x] | `dashboard/src/views/ModelRunnerEndpoints.jsx` | Add `Load Model` action to `ActionMenu`. | Opens modal prefilled with endpoint API type; model field is required; action disabled while request is in flight. | Endpoint action opens `LoadModelModal`; endpoint model input is required. |
| [x] | `dashboard/src/views/VirtualModelRunners.jsx` | Add `Load Model` action to `ActionMenu`. | Modal suggests attached active model definitions, exposes target mode, and defaults to `SelectedEndpoint`. | VMR action opens `LoadModelModal` with attached definitions/endpoints and target mode controls. |
| [x] | Shared modal component or local modal block | Build a load model form and result view. | Shows model, probe kind, timeout, target mode, per-endpoint outcome, timing, and upstream caveats. | Added `dashboard/src/components/LoadModelModal.jsx`; result table shows per-endpoint outcome, status, mechanism, duration, verification, and errors. |
| [x] | Dashboard safety copy | Warn when the selected mechanism may send a billable hosted-provider request. | Warning is shown before OpenAI/Gemini generation or embedding probes. | Modal shows warning for OpenAI/Gemini when probe kind is not `Auto` or `MetadataOnly`. |
| [~] | Health/RigMonitor refresh | Refresh endpoint health/RigMonitor after a successful Ollama load when the endpoint has RigMonitor enabled. | Loaded model telemetry appears without manual page refresh when available. | Endpoint load completion refreshes health data; full RigMonitor detail modal refresh remains a manual action. |
| [~] | Responsive checks | Verify at desktop, tablet, and mobile widths. | Modal controls do not overlap; long model names and endpoint names wrap or truncate with accessible titles. | Dashboard build passes; browser viewport checks still pending. |
| [x] | i18n readiness | Either add these strings through an i18n foundation or record a release exception for the existing non-i18n dashboard. | The gap is explicit and does not get hidden inside this feature. | Existing dashboard has no i18n foundation; this release keeps the explicit plan gap documented. |

Recommended modal fields:

- Model
- Model definition selector for VMRs
- Probe kind
- Target mode for VMRs
- Endpoint selector when `SpecificEndpointIds`
- Timeout
- Keep alive for Ollama
- Verify loaded
- Dry run

Recommended result display:

- Top-level success badge and outcome code
- Model and mechanism
- Selected endpoint or endpoint count
- Per-endpoint table: endpoint, API type, outcome, HTTP status, duration, verification, error
- Copyable JSON response for support/debugging

## Documentation Work

| Status | File | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | `REST_API.md` | Add a "Model Loading" section with both endpoints, auth requirements, request/response fields, examples, status codes, and provider caveats. | Examples cover endpoint load and VMR load for `gemma3:4b`. | Added model loading endpoint rows, dedicated section, provider matrix, request/response examples, and curl examples. |
| [x] | `README.md` | Add a short operator workflow link or subsection for warming local models before traffic. | Points to `REST_API.md` for details; avoids duplicating the entire API contract. | Added model loading feature note and REST API link. |
| [x] | `CHANGELOG.md` | Add an unreleased entry for the model loading API and dashboard action. | Entry mentions API, dashboard, SDKs, and provider semantics. | Unreleased entry added. |
| [x] | `TESTING.md` | Add test commands and manual checks for the load-model feature. | Includes server, dashboard build, SDK tests, and optional local Ollama smoke test. | Added model loading release gate, dashboard checks, and optional Ollama smoke test. |
| [x] | `archive/LOAD_MODELS.md` | Keep this archived plan updated while work progresses. | Each completed item has evidence such as commit, test, or file reference. | Updated through backend, dashboard, SDK, docs, Postman, and final automated verification evidence. |

Documentation examples should be honest about provider differences. For hosted OpenAI and Gemini, say "verify or warm by probe" instead of "load into memory."

## Postman Collection Work

The existing collection folders include "Model Runner Endpoints" and "Virtual Model Runners". Add either a dedicated "Model Loading" folder or place the requests next to the related resource folders. A dedicated folder is easier for operators to discover.

| Status | File | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | `Conductor.postman_collection.json` | Add `Load Model On Endpoint`. | Uses `{{baseUrl}}/v1.0/modelrunnerendpoints/{{endpointId}}/load-model?tenantId={{tenantId}}`; body includes `gemma3:4b`. | Added to dedicated `Model Loading` folder. |
| [x] | `Conductor.postman_collection.json` | Add `Load Model On Virtual Model Runner`. | Uses `{{baseUrl}}/v1.0/virtualmodelrunners/{{vmrId}}/load-model?tenantId={{tenantId}}`; body includes target mode. | Added to dedicated `Model Loading` folder. |
| [x] | `Conductor.postman_collection.json` | Add example responses for Ollama loaded, vLLM verified, and hosted-provider no explicit load. | Examples avoid secrets and use stable outcome codes. | Added Ollama `Loaded`, vLLM `Verified`, and hosted-provider `VerifiedRemote` examples with no secrets. |
| [x] | `Conductor.postman_collection.json` | Add or document collection variables `endpointId`, `vmrId`, `tenantId`, `modelName`. | Requests work after users fill standard variables. | Added `tenantId`; existing `endpointId`, `vmrId`, and `modelName` are present. `ConvertFrom-Json` validation passes. |

## SDK Work

### JavaScript SDK

| Status | File | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | `sdk/javascript/src/index.js` | Add `loadModelRunnerEndpointModel(id, payload = {}, tenantId = null)`. | Encodes IDs and tenant query consistently with existing methods. | Method added with encoded endpoint ID and tenant query. |
| [x] | `sdk/javascript/src/index.js` | Add `loadVirtualModelRunnerModel(id, payload = {}, tenantId = null)`. | Uses `POST` and returns typed JSON response. | Method added with encoded VMR ID and tenant query. |
| [x] | `sdk/javascript/test/client.test.js` | Add tests for paths, body serialization, tenant query, and error handling. | Existing test command passes. | `npm.cmd test` passes, 7 tests. |
| [x] | `sdk/javascript/README.md` | Add usage examples for endpoint and VMR model loading. | Examples include `gemma3:4b` and warn about hosted-provider probes. | README examples added with hosted-provider caveat. |

### Python SDK

| Status | File | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | `sdk/python/src/conductor_client/client.py` | Add `load_model_runner_endpoint_model(endpoint_id, payload=None, tenant_id=None)`. | Uses existing `_request` helper and tenant query. | Method added with tenant query and default empty payload. |
| [x] | `sdk/python/src/conductor_client/client.py` | Add `load_virtual_model_runner_model(vmr_id, payload=None, tenant_id=None)`. | Uses existing `_request` helper and returns dict. | Method added with tenant query and default empty payload. |
| [x] | `sdk/python/tests/test_client.py` | Add tests for endpoint path, VMR path, body, tenant query, and API errors. | Existing Python test command passes. | `PYTHONPATH=src python -m unittest discover -s tests` passes, 7 tests. README command needs editable install or local `PYTHONPATH` in this environment. |
| [x] | `sdk/python/README.md` | Add usage examples for endpoint and VMR model loading. | Examples align with REST docs. | README examples added with hosted-provider caveat. |

## Test Plan

### Backend Unit And Service Tests

| Status | Test Area | Cases | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [~] | Request validation | Missing model, invalid timeout, invalid target mode, endpoint IDs with wrong mode, model definition mismatch. | Returns `400` with stable outcome or validation error. | DTO clamp behavior and service model resolution covered indirectly; full negative matrix remains pending. |
| [~] | Endpoint resolution | Same-tenant read, admin tenant query, wrong tenant, missing endpoint, inactive endpoint behavior. | Tenant isolation is enforced. | VMR inactive skip behavior covered in `ModelLoadServiceTests`; full controller tenant matrix remains pending. |
| [~] | VMR model resolution | Explicit model, model definition ID, one attached model, zero attached models, multiple attached models, strict mode rejection. | Effective model matches routing/mutation behavior. | Single attached model resolution covered in `ModelLoadServiceTests`; full negative matrix remains pending. |
| [~] | Provider probe builder | Ollama native generate, Ollama embeddings, OpenAI metadata, OpenAI chat, vLLM models, Gemini metadata, Gemini generate. | Request path/body/header plan matches provider matrix. | `ModelLoadProbeBuilderTests` cover Ollama auto, OpenAI auto, vLLM completion, and Gemini embeddings; remaining modes pending. |
| [~] | Redaction | API keys in headers, query string, and endpoint model are redacted. | No test output or response exposes raw secret material. | Response shape avoids auth material; explicit redaction tests remain pending. |
| [~] | Metrics | Success, failure, timeout, no explicit load, and dry run increment expected counters. | Snapshot and Prometheus rendering include expected metric names. | `OperationalMetricsServiceTests` cover model-load metric families; full outcome matrix remains pending. |

### Route And Controller Tests

| Status | File/Area | Work | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [~] | `src/Test.Shared/Server/Controllers/ModelRunnerEndpointControllerTests.cs` | Add load-model controller tests with fake service/transport. | Direct endpoint load returns expected response and status behavior. | Controller-specific tests pending; service tests cover endpoint orchestration. |
| [~] | `src/Test.Shared/Server/Controllers/VirtualModelRunnerControllerTests.cs` | Add VMR load-model tests for selected endpoint and all eligible endpoints. | Routing and target-mode behavior are covered. | Controller-specific tests pending; service tests cover VMR all-configured target behavior. |
| [x] | Auth route tests | Add request type resolver and authorization cases. | New routes are never classified as `Unknown`. | Added `ModelLoadAuthorizationTests`; full `dotnet test src/Conductor.sln -v:minimal` passes. |
| [~] | OpenAPI smoke | Confirm `/openapi.json` includes both load-model routes. | API Explorer and Postman users can discover the routes. | Route modules include OpenAPI metadata and server builds; live `/openapi.json` fetch not performed. |

### Dashboard Verification

| Status | Check | Acceptance Criteria | Evidence |
| --- | --- | --- | --- |
| [x] | `npm run build` in `dashboard/` | Build succeeds. | `npm` PowerShell shim was blocked by execution policy; `npm.cmd run build` passed with the existing Vite large-chunk warning. |
| [x] | Endpoint load modal | Opens from endpoint action menu, validates model, submits, displays results, refreshes health. | Implemented in `ModelRunnerEndpoints.jsx` plus `LoadModelModal`; build passes. |
| [x] | VMR load modal | Opens from VMR action menu, suggests model definitions, target modes work, displays per-endpoint results. | Implemented in `VirtualModelRunners.jsx` plus `LoadModelModal`; build passes. |
| [x] | Hosted-provider warning | OpenAI/Gemini generation or embedding probes show cost/warmup warning. | `LoadModelModal` warns for hosted providers when generation/embedding probes are selected. |
| [~] | Responsive layout | Modal and tables usable at 1280px, 768px, and 390px. | Source uses responsive modal/table wrapping and dashboard build passes; browser viewport QA not performed. |

### SDK And Collection Verification

| Status | Check | Acceptance Criteria | Evidence |
| --- | --- | --- | --- |
| [x] | JavaScript SDK tests | Existing and new tests pass. | `npm.cmd test` passes, 8 tests. |
| [x] | Python SDK tests | Existing and new tests pass. | `PYTHONPATH=src python -m unittest discover -s tests` passes, 8 tests. |
| [~] | Postman import | Collection imports cleanly and new requests run against a local server after variables are set. | JSON parses with `ConvertFrom-Json`; live Postman/Newman run not performed yet. |

### Optional Local Smoke Tests

Run when an Ollama host is available:

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

Expected result: `Success=true`, at least one endpoint result, `OutcomeCode=Loaded` or `AlreadyAvailable`, and `VerifiedLoaded=true` when `/api/ps` confirms the model.

## Ollama Endpoint Model Management Follow-Up

This is intentionally separate from model loading. Loading warms or verifies a model that should already be available to the runner. Ollama model management installs or removes local model artifacts on one configured Ollama endpoint.

| Status | Task | Owner | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | Add tenant-admin backend routes for Ollama endpoint model management. | Backend | `GET /v1.0/modelrunnerendpoints/{id}/ollama/models`, `POST /pull`, and `POST /delete` are registered with OpenAPI metadata and reject non-Ollama endpoints. | Implemented in `ModelRunnerEndpointController`, `ModelRunnerRouteModule`, and `OllamaModelManagementService`; `dotnet build src/Conductor.sln -v:minimal -m:1 /nodeReuse:false` passes. |
| [x] | Add request classification and authorization. | Backend/Security | All three management routes resolve to stable request types and require tenant-admin access. | `RequestTypeResolver` and `AuthorizationConfig` updated; focused auth tests added. |
| [x] | Add dashboard action and modal. | Frontend | Ollama endpoints show `Manage Models`; modal lists models, pulls a new model, and confirms deletion inline. | Implemented in `ModelRunnerEndpoints.jsx` and `OllamaModelManagerModal.jsx`; dashboard build passes. |
| [x] | Add dashboard API client and SDK helpers. | SDK/Frontend | Dashboard, JavaScript SDK, and Python SDK can call list, pull, and delete routes. | Dashboard API client plus JS/Python SDK methods and tests added. |
| [x] | Update docs and changelog. | Docs | REST docs and README describe the Ollama-only management behavior and dashboard entry point. | `REST_API.md`, `README.md`, SDK READMEs, and `CHANGELOG.md` updated. |
| [x] | Update Postman collection examples. | DevRel | Collection includes list, pull, and delete examples and validates as JSON. | Added list, pull, and delete requests under Model Runner Endpoints; `ConvertFrom-Json` validation passes. |
| [!] | Run live Ollama model management smoke test. | QA/SRE | Local model list returns current models; pull/delete work against a disposable model tag. | Not run in this environment; no live Conductor server/Ollama endpoint/token was provided. |

## Implementation Sequence

### Phase 1: Contract And Backend Core

| Status | Task | Owner | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | Finalize request/response DTOs and enum names. | Backend | DTOs compile and serializer emits expected JSON. | Core contracts added; `dotnet build src/Conductor.sln -v:minimal` passes. |
| [x] | Add request type resolver and authorization config. | Backend/Security | New routes require intended auth level. | Request types mapped and added to tenant-admin authorization. |
| [~] | Implement provider probe builder and fake-transport tests. | Backend | Provider matrix tests pass without live upstream services. | `ModelLoadProbeBuilderTests` and `ModelLoadServiceTests` added; full provider matrix coverage still pending. |
| [x] | Implement endpoint load service path. | Backend | Direct endpoint tests pass. | Endpoint load service and route implemented; build passes. |
| [x] | Implement VMR load service path for `SelectedEndpoint`. | Backend | VMR tests prove model resolution and routing selection. | Selected-endpoint routing path implemented with existing routing service; build passes. |

### Phase 2: Multi-Endpoint VMR And Observability

| Status | Task | Owner | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | Add `AllEligibleEndpoints`, `AllConfiguredEndpoints`, and `SpecificEndpointIds`. | Backend | Per-endpoint results return partial success and aggregate status correctly. | Multi-endpoint target modes implemented in `ModelLoadService`; build passes. |
| [~] | Add metrics and redaction tests. | Backend/SRE | Metrics and logs are useful without secrets. | Model-load metric families covered; explicit redaction tests still pending. |
| [x] | Add OpenAPI route metadata. | Backend | API Explorer discovers both routes. | Both route modules include OpenAPI request/response metadata. |

### Phase 3: Dashboard

| Status | Task | Owner | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | Add API client methods. | Frontend | Existing API client style preserved. | Added dashboard API methods for both load-model routes. |
| [x] | Add endpoint action and modal. | Frontend | Operator can warm a concrete host. | Endpoint table action opens shared load-model modal. |
| [x] | Add VMR action and modal. | Frontend | Operator can warm a VMR-selected or multi-endpoint target. | VMR table action opens shared load-model modal with target mode controls. |
| [x] | Add result rendering and health refresh. | Frontend | Result is clear enough for support without opening DevTools. | Result header, summary grid, endpoint table, and JSON details added; endpoint health refreshes after success. |
| [~] | Run responsive and build checks. | Frontend/QA | No overlap or broken modal flows. | Build passes; responsive browser checks still pending. |

### Phase 4: SDKs, Postman, Documentation

| Status | Task | Owner | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | Update REST docs, README, TESTING, CHANGELOG. | Docs/Backend | Docs match implemented contract. | REST docs, README, TESTING, and CHANGELOG updated. |
| [~] | Update Postman collection. | DevRel/Backend | Collection imports and examples execute. | Collection requests/examples added and JSON validates; live execution not performed. |
| [x] | Update JavaScript SDK and tests. | SDK | Tests pass and README has examples. | Methods, tests, and README examples added; JS SDK tests pass. |
| [x] | Update Python SDK and tests. | SDK | Tests pass and README has examples. | Methods, tests, and README examples added; Python SDK tests pass with `PYTHONPATH=src`. |

### Phase 5: Hardening And Release Readiness

| Status | Task | Owner | Acceptance Criteria | Evidence |
| --- | --- | --- | --- | --- |
| [x] | Run full .NET test suite. | QA | `dotnet test src/Conductor.sln` or documented equivalent passes. | `dotnet test src/Conductor.sln -v:minimal -m:1 /nodeReuse:false` passes: xUnit 899, NUnit 899. |
| [x] | Run dashboard build. | QA | `npm run build` passes in `dashboard/`. | `npm.cmd run build` passes; `npm` PowerShell shim is blocked by execution policy. |
| [x] | Run SDK tests. | QA/SDK | JS and Python SDK tests pass. | `npm.cmd test` and `PYTHONPATH=src python -m unittest discover -s tests` pass. |
| [!] | Run live Ollama smoke test if available. | QA/SRE | `gemma3:4b` or configured local model can be warmed and verified. | Not run in this environment; no live Conductor server/Ollama endpoint/token was provided. |
| [~] | Review auth, cost, and redaction behavior. | Security/SRE | Hosted-provider probes are opt-in or clearly warned; secrets are redacted. | Auth route tests pass and hosted-provider warnings/docs are implemented; explicit redaction tests remain pending. |
| [x] | Update this plan with final status and evidence. | TPM/Implementer | Final implementation status and any remaining manual gaps are recorded. | Updated with automated test evidence plus explicit live Ollama, live Postman/Newman, redaction-matrix, and browser viewport QA gaps. |

## Definition Of Done

| Status | Requirement | Evidence |
| --- | --- | --- |
| [x] | Both REST endpoints are implemented and covered by OpenAPI metadata. | Routes registered in endpoint and VMR route modules; server solution builds. |
| [x] | Endpoint loading and VMR loading return typed, stable response objects with per-endpoint results. | `ModelLoadResponse` and `ModelLoadEndpointResult` implemented and covered by service tests. |
| [x] | Ollama load/warm works without pulling missing models. | Probe builder uses `/api/generate`, `/api/chat`, `/api/embed`, and `/api/ps`; no `/api/pull` call exists in load service. |
| [x] | vLLM, OpenAI, and Gemini return honest `VerifiedRemote`, `NoExplicitLoadSupported`, or probe results instead of claiming host-local loading. | Provider adapter defaults hosted/OpenAI-compatible `Auto` to metadata verification and uses `Verified`/`VerifiedRemote` semantics. |
| [~] | Authorization, tenant scoping, and redaction tests pass. | Auth route tests pass; service target tests pass; full tenant/redaction matrix remains pending. |
| [x] | Dashboard exposes load actions for model runner endpoints and VMRs. | Actions added in both management views; dashboard build passes. |
| [~] | Dashboard result modals are usable on desktop, tablet, and mobile. | Responsive source constraints added and build passes; manual viewport QA not performed. |
| [x] | `REST_API.md`, `README.md`, `TESTING.md`, `CHANGELOG.md`, Postman collection, JavaScript SDK, and Python SDK are updated. | Docs, collection, and SDKs updated; Postman JSON validates. |
| [x] | .NET tests, dashboard build, and SDK tests pass or any skipped checks are documented with reason. | .NET solution tests, dashboard build, JS SDK tests, and Python SDK tests pass; live Postman/Ollama and viewport QA not performed. |

## Risks And Mitigations

| Status | Risk | Mitigation | Evidence |
| --- | --- | --- | --- |
| [x] | Hosted providers may incur cost for warmup probes. | Default to metadata verification where possible; show dashboard warning; document `ProbeKind` behavior. | `Auto` defaults to metadata for OpenAI/Gemini; dashboard warning and docs added for explicit probes. |
| [x] | Large local model warmup may exceed HTTP timeouts. | Clamp and expose `TimeoutMs`; document that async job support is a future extension. | `TimeoutMs` is clamped in the request contract and documented. |
| [x] | Loading into all endpoints can saturate a cluster. | Default VMR target mode to `SelectedEndpoint`; require explicit selection for all endpoints; consider small concurrency limit. | VMR `TargetMode` defaults to `SelectedEndpoint`; all-endpoint modes require explicit selection. |
| [x] | The API may be mistaken for model installation. | Docs and response codes must say missing models fail; no pull behavior in this feature. | Docs state missing local models fail; load implementation never calls Ollama `/api/pull`. |
| [x] | Existing dashboard lacks i18n foundation. | Track the gap explicitly; do not consider dashboard launch complete if the product owner requires strict i18n compliance for this release. | Dashboard i18n gap remains explicit in this plan. |
| [~] | Duplicated provider logic can drift from proxy behavior. | Centralize auth/header/URL building in the load service and reuse existing endpoint base URL behavior. Add tests matching `ProxyController` auth conventions. | Provider logic is centralized in load service/probe builder/transport; ProxyController header parity tests remain pending. |

## Future Extensions

| Status | Extension | Trigger |
| --- | --- | --- |
| [ ] | Async load jobs with status polling and cancellation. | Needed if common warmup calls exceed dashboard/operator HTTP timeout windows. |
| [ ] | Persistent last-load status per endpoint/model. | Needed if operators want historical load state independent of request history and metrics. |
| [ ] | Scheduled pre-warming. | Needed for predictable traffic windows or rolling deployments. |
| [ ] | Native provider plugins beyond current API types. | Needed when a runner exposes a real load/unload API outside Ollama/OpenAI/Gemini/vLLM shapes. |
| [ ] | Dashboard permission introspection. | Needed when full RBAC effective-permission endpoints are implemented. |
