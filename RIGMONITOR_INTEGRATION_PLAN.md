# RigMonitor Integration Plan

## Scope

Integrate optional RigMonitor host telemetry into Conductor endpoint health monitoring and use cached RigMonitor metrics as inputs to first-class Conductor load-balancing policies that can be attached to virtual model runners by policy ID.

## Grounding

This plan is grounded in:

- `C:\Code\RigMonitor\REST_API.md`
- `C:\Code\RigMonitor\README.md`
- `src/Conductor.Server/Services/HealthCheckService.cs`
- `src/Conductor.Server/Controllers/ProxyController.cs`
- `src/Conductor.Server/ConductorServer.cs`
- `src/Conductor.Core/Models/ModelRunnerEndpoint.cs`
- `src/Conductor.Core/Models/VirtualModelRunner.cs`
- `Conductor.postman_collection.json`
- `README.md`
- `dashboard/src/views/ModelRunnerEndpoints.jsx`
- `dashboard/src/views/VirtualModelRunners.jsx`
- `dashboard/src/utils/codeGenerators.js`

## Key Observations

- RigMonitor is a sidecar daemon, not the model runner itself.
- RigMonitor exposes its own REST API on its own port, with the important routes:
  - `GET /readyz`
  - `GET /v1/capabilities`
  - `GET /v1/telemetry`
- `GET /v1/telemetry` supports selective collection using these query keys:
  - `system`
  - `cpu`
  - `memory`
  - `network`
  - `disk`
  - `gpu`
  - `ollama`
- Conductor currently treats endpoint health as pass or fail HTTP health checks only.
- Conductor currently supports only these load-balancing modes:
  - `RoundRobin`
  - `Random`
  - `FirstAvailable`
- Conductor does not currently have a first-class load-balancing policy resource.
- The current endpoint selection path is request-path critical and should not perform live RigMonitor calls.
- The dashboard already has endpoint health and VMR health views, so there is a natural place to expose cached RigMonitor state.
- Conductor already has multiple externally visible contract surfaces that must stay aligned:
  - REST API routes and OpenAPI metadata in `src/Conductor.Server/ConductorServer.cs`
  - `Conductor.postman_collection.json`
  - repo documentation in `README.md`
  - dashboard code generation helpers in `dashboard/src/utils/codeGenerators.js`
  - any official or generated SDKs derived from OpenAPI, whether they live in this repo or not

## Design Principles

- Keep the existing model-runner health check as the primary health signal by default.
- Treat RigMonitor as optional enrichment unless an administrator explicitly opts into health-affecting behavior.
- Collect RigMonitor data during the existing endpoint health loop and cache it in memory.
- Never call RigMonitor synchronously from the request forwarding path.
- Normalize RigMonitor metrics into Conductor-owned metric identifiers before using them in policies.
- Make load-balancing policies first-class resources in both the API and the UI.
- Attach policies to VMRs by stable policy ID.
- Keep legacy load-balancing modes fully backward compatible.
- Make policy-driven routing opt-in per VMR.

## Progress Snapshot

- Completed:
  - endpoint-side RigMonitor persistence and runtime health integration
  - first-class load-balancing policy models, persistence, controllers, routes, and OpenAPI wiring
  - policy-aware proxy selection with cached telemetry only
  - dashboard support for endpoint RigMonitor configuration, policy CRUD, policy cloning, policy diagnostics, and VMR policy attachment
  - README and Postman updates for the new API surface
  - expanded automated coverage for selector resolution, policy evaluation, policy CRUD, endpoint RigMonitor persistence, and VMR attachment validation
- Remaining:
  - richer policy explainability and diagnostics endpoints
  - dedicated pre-save RigMonitor probe endpoint instead of relying on cached detail
  - explicit array aggregation controls and more complete missing-data policy semantics
  - SDK regeneration or external SDK release coordination if SDKs are produced outside this repo

## Delivery Plan

- [x] Phase 0 - Confirm semantics
  - Decide whether RigMonitor failures are advisory-only by default or can mark an endpoint unavailable.
  - Decide who can view raw host telemetry.
  - Decide missing-data behavior for policy evaluation.
  - Recommended default:
    - RigMonitor advisory-only
    - raw telemetry visible only to users who can manage the endpoint or VMR
    - policy fallback to legacy routing unless explicitly configured otherwise

- [x] Phase 1 - Extend endpoint configuration
  - Add optional RigMonitor settings to `ModelRunnerEndpoint`.
  - Prefer a single serialized settings object rather than many scalar properties.
  - Suggested shape:
    - `Enabled`
    - `HostnameOverride`
    - `Port`
    - `UseSsl`
    - `TimeoutMs`
    - `CollectDuringHealthCheck`
    - `RequireReadyz`
    - `HealthAffectedByRigMonitor`
    - `TelemetryProfile`
    - `TelemetrySelectors`
    - `CapabilitiesRefreshIntervalMs`
    - `MaxTelemetryAgeMs`

- [x] Phase 2 - Introduce a first-class load-balancing policy entity
  - Add a new tenant-scoped `LoadBalancingPolicy` resource.
  - Give policies their own IDs, CRUD lifecycle, labels, tags, metadata, and active flag.
  - Suggested core fields:
    - `Id`
    - `TenantId`
    - `Name`
    - `Description`
    - `Enabled`
    - `MaxTelemetryAgeMs`
    - `Filters`
    - `Ranking`
    - `FallbackMode`
    - `TieBreaker`
    - `Active`
    - `Labels`
    - `Tags`
    - `Metadata`
  - Version the policy schema so rules can evolve without breaking saved policies.

- [x] Phase 3 - Attach policies to VMRs by ID
  - Keep `LoadBalancingMode` unchanged for legacy behavior.
  - Add nullable `LoadBalancingPolicyId` to `VirtualModelRunner`.
  - Make policy-driven routing opt-in by presence of a valid attached policy ID.
  - Ensure legacy mode and policy attachment can coexist during migration.
  - Decide whether a VMR attaches exactly one policy in v1 or whether policy composition is a real near-term requirement.

- [x] Phase 4 - Persistence and migration
  - Add endpoint, VMR, and policy persistence across SQLite, MySQL, PostgreSQL, and SQL Server.
  - Update create-table queries.
  - Add additive migrations for existing databases.
  - Add CRUD methods for the new policy resource.
  - Update CRUD methods for endpoint and VMR persistence.
  - Ensure backup and restore include new fields.
  - Bump backup schema metadata once finalized.

- [x] Phase 5 - Add RigMonitor client module
  - Implement a dedicated client for:
    - `GET /readyz`
    - `GET /v1/capabilities`
    - `GET /v1/telemetry`
  - Parse camelCase JSON into Conductor DTOs.
  - Support query selector generation exactly as documented in `REST_API.md`.
  - Isolate all RigMonitor HTTP handling from controllers and proxy logic.

- [x] Phase 6 - Add runtime RigMonitor state
  - Extend endpoint runtime health state with RigMonitor-specific cached data.
  - Suggested fields:
    - `RigMonitorStatus`
    - `LastReadyzUtc`
    - `LastCapabilitiesUtc`
    - `LastTelemetryUtc`
    - `LastRigMonitorError`
    - `RigMonitorCapabilities`
    - `RigMonitorTelemetrySummary`
    - optional raw cached snapshot for diagnostics
  - Keep this data in memory beside the current `EndpointHealthState`.

- [x] Phase 7 - Integrate with health loop
  - Keep existing model-runner health check behavior intact.
  - After the existing probe, if RigMonitor is enabled:
    - optionally call `readyz`
    - refresh `capabilities` on a slower cadence
    - collect `telemetry` on the configured cadence
  - Do not let RigMonitor replace the core model-runner health probe.
  - Only let RigMonitor affect health if explicitly enabled.

- [x] Phase 8 - Define telemetry collection profiles
  - Avoid full telemetry collection for every endpoint by default.
  - Offer predefined profiles:
    - `Basic`: `cpu`, `memory`
    - `GpuPlacement`: `cpu`, `memory`, `gpu`
    - `OllamaPlacement`: `cpu`, `memory`, `ollama`
    - `Full`
  - Add custom-selector mode for administrators.

- [x] Phase 9 - Normalize RigMonitor metrics
  - Do not expose raw external JSON paths as the saved policy contract.
  - Map RigMonitor data into stable Conductor metric IDs.
  - Suggested metrics:
    - `health.isHealthy`
    - `health.hasCapacity`
    - `health.inFlightRequests`
    - `endpoint.weight`
    - `endpoint.maxParallelRequests`
    - `rig.cpu.utilizationPercent`
    - `rig.memory.availableBytes`
    - `rig.memory.utilizationPercent`
    - `rig.network.totalReceiveBytesPerSecond`
    - `rig.network.totalTransmitBytesPerSecond`
    - `rig.disk.readOperationsPerSecond`
    - `rig.disk.writeOperationsPerSecond`
    - `rig.disk.maxVolumeUtilizationPercent`
    - `rig.gpu.available`
    - `rig.gpu.avgUtilizationPercent`
    - `rig.gpu.maxUtilizationPercent`
    - `rig.gpu.minFreeMemoryMegabytes`
    - `rig.gpu.maxTemperatureCelsius`
    - `rig.ollama.available`
    - `rig.ollama.loadedModelCount`
    - `rig.ollama.availableModelCount`

- [ ] Phase 10 - Define aggregation and missing-data rules
  - Progress:
    - stale telemetry rejection is implemented
    - unavailable metrics currently make an endpoint ineligible for telemetry-dependent filters or rankings
    - explicit `min`/`max`/`avg`/`sum`/`count` operator controls are not exposed yet
  - For arrays like `gpu.devices`, `network.interfaces`, and `disk.volumes`, support explicit aggregations:
    - `min`
    - `max`
    - `avg`
    - `sum`
    - `count`
  - Distinguish:
    - not collected
    - unavailable on host
    - stale
    - collection error
  - Make policy behavior configurable for each case.

- [ ] Phase 11 - Add API surfaces
  - Progress:
    - cached RigMonitor detail endpoint is implemented
    - policy CRUD and metrics catalog routes are implemented
    - a dedicated pre-save RigMonitor probe endpoint and policy diagnostics routes are still open
  - Keep existing health endpoints backward compatible.
  - Add cached RigMonitor detail endpoint for a model runner endpoint.
  - Add optional probe or test endpoint for validating RigMonitor configuration before save.
  - Add first-class policy endpoints:
    - create
    - read
    - update
    - delete
    - list
  - Add explicit VMR attachment semantics using `LoadBalancingPolicyId`.
  - Add diagnostics endpoints as needed for explaining policy evaluation outcomes.
  - Do not expose live pass-through access to the RigMonitor daemon.

- [x] Phase 12 - Harden authorization
  - Add explicit request types and authorization rules for any new telemetry routes.
  - Add explicit request types and authorization rules for policy CRUD routes.
  - Audit current endpoint-health and VMR-health route classification.
  - Ensure richer host telemetry does not accidentally inherit ambiguous authorization behavior.

- [x] Phase 13 - Extend endpoint dashboard UX
  - Add a `RigMonitor` configuration section in the endpoint form.
  - Include:
    - enable checkbox
    - host reuse by default
    - port default `9000`
    - SSL toggle
    - timeout
    - telemetry profile
    - selector settings
    - health-affecting toggle
    - telemetry freshness help text
    - `Test RigMonitor` action
  - `Test RigMonitor` should show:
    - `readyz` result
    - `capabilities` result
    - sample telemetry retrieval result

- [x] Phase 14 - Extend endpoint health UX
  - Show RigMonitor state alongside current health data, not mixed into a single opaque status.
  - Surface:
    - ready or warming
    - telemetry age
    - last telemetry error
    - capability flags like `telemetryWarm`, `nvidiaAvailable`, `ollamaAvailable`
    - compact CPU, memory, GPU, disk, and Ollama summaries where collected
  - Hide raw JSON behind an explicit details expansion.

- [x] Phase 15 - Add policy engine
  - Evaluate attached policies from cached endpoint state only.
  - Recommended pipeline:
    - determine eligible endpoints
    - apply hard filters
    - score or rank remaining endpoints
    - apply fallback if no valid result
    - apply tie-breaker
  - Existing hard gates should remain first:
    - endpoint active
    - endpoint healthy
    - endpoint has capacity

- [x] Phase 16 - Integrate policy engine into proxy path
  - Preserve current session-affinity short-circuit behavior.
  - If a session pin is valid, continue using it.
  - If no session pin is valid, call a new selection path such as `EvaluatePolicyOrLegacyMode`.
  - If a pinned endpoint no longer satisfies hard gates or configured policy constraints, remove the pin and reselect.

- [ ] Phase 17 - Add first-class policy management UX
  - Progress:
    - dedicated policy CRUD view is implemented
    - VMR policy picker is implemented
    - template seeding is implemented
    - clone flows and operator diagnostics are implemented
    - request-path selection explainability is still open
  - Add a dedicated policy management surface in the dashboard rather than hiding policy logic inside the VMR form.
  - Support first-class policy create, edit, clone, delete, list, and inspect flows.
  - In the VMR form, attach a policy by ID using a policy picker.
  - Keep current load-balancing dropdown available for legacy mode and migration.
  - Provide guided templates:
    - Lowest CPU utilization
    - Lowest memory utilization
    - Lowest GPU utilization
    - Most free VRAM
    - Least in-flight requests
    - Balanced CPU + GPU + in-flight score
  - Show structured rule editing plus JSON preview for power users.
  - Add policy explainability in the UI so operators can see why a specific endpoint was selected or filtered out.

- [ ] Phase 18 - Update OpenAPI, Postman, and SDK surfaces
  - Progress:
    - OpenAPI tags and route metadata are implemented
    - `Conductor.postman_collection.json` now includes policy CRUD, VMR attachment, and RigMonitor inspection examples
    - no checked-in SDK artifacts were present in this repo to regenerate
    - `dashboard/src/utils/codeGenerators.js` was audited and did not require changes because the API Explorer still targets VMR inference routes rather than management APIs
  - Update OpenAPI tags, schemas, route metadata, request bodies, and examples for:
    - endpoint RigMonitor configuration
    - cached telemetry and diagnostics routes
    - load-balancing policy CRUD
    - VMR policy attachment
  - Update `Conductor.postman_collection.json` with:
    - policy CRUD requests
    - VMR attach or update-by-policy-ID examples
    - RigMonitor probe and telemetry inspection requests
    - revised endpoint create and update examples including RigMonitor settings
  - Audit the dashboard code-generation helpers in `dashboard/src/utils/codeGenerators.js` so exposed examples remain accurate after policy and telemetry route additions.
  - If Conductor maintains or generates official SDKs from OpenAPI, regenerate and verify them as release-blocking artifacts.
  - If SDKs are maintained outside this repo, add a coordinated release checklist so the API change does not ship without corresponding SDK support.

- [x] Phase 19 - Update documentation and operator guidance
  - Update `README.md` to describe:
    - RigMonitor endpoint integration
    - telemetry-backed health enrichment
    - first-class load-balancing policies
    - VMR policy attachment by ID
  - Add operator guidance for:
    - securing unauthenticated RigMonitor sidecars on trusted networks only
    - tuning telemetry selector profiles
    - choosing fail-open versus fail-closed behavior
    - understanding stale or missing telemetry
  - Add admin-facing examples for common policies and endpoint layouts.
  - Add API usage examples that match OpenAPI and Postman examples.

- [ ] Phase 20 - Defer model-aware placement unless needed
  - If future policy goals include "prefer host where requested Ollama model is already loaded," treat that as a second milestone.
  - Current proxy flow selects an endpoint before request-body-derived model resolution.
  - Reordering that logic is possible but should not block first delivery.

- [ ] Phase 21 - Testing
  - Progress:
    - added coverage for telemetry selector generation
    - added coverage for stale telemetry rejection and policy ranking
    - added coverage for policy CRUD detach behavior and VMR policy attachment validation
    - added coverage for policy update persistence, active filtering, descending ranking, mixed-weight scoring, and RigMonitor base-url/selector presets
    - broader integration cases like malformed telemetry payloads, backup/restore round-trips, and generated-client contract tests remain open
  - Add unit and integration coverage for:
    - `readyz` success and warming
    - telemetry selector generation
    - timeout handling
    - malformed or partial telemetry payloads
    - omitted optional sections
    - missing GPU capability
    - stale telemetry rejection
    - policy ranking and tie-breaking
    - policy CRUD and VMR attachment by policy ID
    - fail-open and fail-closed behavior
    - session-affinity repinning
    - backup and restore persistence
    - Postman sample validity
    - OpenAPI schema correctness
    - SDK or generated-client contract coverage
    - dashboard serialization and edit flows

- [ ] Phase 22 - Rollout
  - Ship first in telemetry-only mode with no policy enforcement.
  - Then enable policy evaluation with fallback to legacy load balancing.
  - Only later allow fail-closed behavior based on telemetry once field behavior is validated.

## Suggested Policy Shape

```json
{
  "version": 1,
  "enabled": true,
  "maxTelemetryAgeMs": 30000,
  "filters": [
    { "metric": "health.isHealthy", "op": "eq", "value": true },
    { "metric": "health.hasCapacity", "op": "eq", "value": true },
    { "metric": "rig.gpu.available", "op": "eq", "value": true }
  ],
  "ranking": [
    { "metric": "rig.gpu.avgUtilizationPercent", "direction": "asc", "weight": 0.6 },
    { "metric": "rig.cpu.utilizationPercent", "direction": "asc", "weight": 0.25 },
    { "metric": "health.inFlightRequests", "direction": "asc", "weight": 0.15 }
  ],
  "fallbackMode": "UseLegacyLoadBalancingMode",
  "tieBreaker": "RoundRobin"
}
```

## Recommended First Milestone

- [x] Add endpoint-side RigMonitor configuration.
- [x] Add RigMonitor client and cached runtime state.
- [x] Collect cached telemetry in the health loop.
- [x] Expose cached RigMonitor state in endpoint and VMR health views.
- [x] Define the first-class policy resource and persistence shape, but do not route traffic by policy yet.
- [ ] Keep all routing behavior unchanged.

This milestone gives the team visibility and operational confidence before changing endpoint selection behavior.

## Recommended Second Milestone

- [x] Add first-class load-balancing policy CRUD and VMR policy attachment by ID.
- [x] Implement metric normalization and evaluation engine.
- [x] Add policy management UI with a few high-value templates.
- [x] Update OpenAPI, Postman, docs, and SDKs as part of the same release train.
- [x] Use cached metrics during endpoint selection with safe fallback to legacy mode.

## Open Questions For The Team

- [ ] Should RigMonitor be allowed to mark an endpoint unhealthy, or only influence routing scores?
- [ ] Should tenant users see full host telemetry, or only derived policy-relevant summaries?
- [ ] Should stale telemetry remove an endpoint from selection, or only reduce its rank?
- [ ] Is policy evaluation allowed to depend on optional GPU or Ollama metrics, or must all production policies degrade gracefully without them?
- [ ] Should a VMR attach exactly one policy by ID, or is policy chaining or composition a real requirement?
- [ ] Is "prefer endpoint where model is already loaded" a phase-one requirement, or can it wait?
- [ ] Are SDK updates generated from OpenAPI, maintained by hand, or maintained in a separate repository?
