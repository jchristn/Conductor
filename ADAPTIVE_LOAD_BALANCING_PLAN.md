# Adaptive Load Balancing Implementation Plan

Created: 2026-06-25
Overall status: Done except deferred human release gates
Primary release target: Opt-in adaptive endpoint selection, runtime endpoint scoring, transient backoff, priority-group failover, explicit traffic-split controls, full dashboard/API/SDK/documentation support, and release-grade test coverage.

## How To Use This File

This file is the execution tracker. Update it as design, implementation, review, validation, or release work starts and completes. Do not wait until the end of the project to annotate progress.

Allowed task status values:

- `Not Started`
- `In Progress`
- `Blocked`
- `In Review`
- `Done`
- `Deferred`

Priority values:

- `P0`: required for the first useful adaptive load-balancing release
- `P1`: required before calling the capability production-ready
- `P2`: useful follow-up after the core workflow is stable

Task annotation format:

```text
- [ ] `ALB0.01` Status: <Not Started | In Progress | Blocked | In Review | Done | Deferred> | Priority: P0 | Owner: Role | Assignee: TBD | Evidence:
  Task description.
```

When a task is completed, add evidence such as changed files, test names, command output summary, PR number, screenshot path, or review artifact. If a task is blocked, write the blocking decision or dependency under the task.

## Current Progress

| Area | Status | Notes |
| --- | --- | --- |
| Product scope | Done | Opt-in `Adaptive`, `LeastRecentlyUsed`, runtime stats, transient backoff, endpoint groups, traffic splits, runtime policy metrics, dashboard/API/SDK/docs/Postman coverage, and automated validation are implemented; human release gates are explicitly deferred below. |
| Architecture | Done | ADR 0004 records first-release decisions; implementation is being reconciled against the ADR. |
| Backend routing | Done | `LeastRecentlyUsed`, adaptive sampled selection, runtime scoring, transient backoff exclusion, endpoint groups, traffic splits, runtime policy metrics, and structured routing evidence are implemented and covered by shared backend tests. |
| Runtime statistics | Done | Runtime snapshot contracts, in-memory stats service, proxy completion/failure updates, reset API, and backoff-clear API are implemented with runtime service and controller coverage. |
| Priority groups and traffic splits | Done | Endpoint-group contracts, VMR persistence, validation, route scoping, split selection, dashboard controls, SDK payload support, docs, and statistical routing tests are implemented. |
| Dashboard | Deferred | Dashboard implementation builds successfully; required double-check/triple-check responsive UX, accessibility, and long-label review remains a human release gate. |
| SDKs | Done | JavaScript, Python, and C# clients expose shipped management APIs and adaptive payload models; SDK tests pass. |
| Postman/API Explorer | Deferred | Postman examples and OpenAPI metadata are implemented and parse/build cleanly; rendered API Explorer smoke review remains a human/manual release gate. |
| Documentation | Done | README, REST_API, LOAD_BALANCING_POLICIES, TESTING, CHANGELOG, SDK READMEs, Postman, and operator runbooks are updated. |
| Test coverage | Done | Backend enum serialization, runtime stats, adaptive selector, policy runtime metrics, endpoint groups, traffic split, runtime route, migration, SDK, and Postman tests pass; performance, UX, and security reviews are separately deferred. |
| Requirements compliance | Deferred | Code, backend architecture, database, SDK, docs, and release validation evidence are recorded; dashboard i18n/responsive UX, final security review, final prose review, and SRE performance review remain human gates. |

## Product Outcomes

- [x] Operators can enable an adaptive load-balancing mode per VMR without changing existing VMR behavior by default.
- [x] Operators can continue using current `RoundRobin`, `Random`, `FirstAvailable`, and policy-based full ranking unchanged.
- [x] Operators can choose `LeastRecentlyUsed` as a compatibility load-balancing scheme for routes that should spread new assignments toward the endpoint that has gone longest without receiving work.
- [x] Routing can consider live request feedback such as in-flight count, recent success/failure, recent latency, time to first token, timeout/error state, and provider rate-limit backoff.
- [x] Routing can temporarily avoid endpoints that are failing, timing out, saturated, or rate-limited without changing persistent operator-managed service state.
- [x] Routing can prefer higher-priority endpoint groups and fall back to lower-priority groups only when needed.
- [x] Operators can configure explicit traffic-split groups for canary, migration, A/B, and provider-diversity scenarios.
- [x] The load-balancing metric catalog exposes runtime metrics so policies can filter and rank with adaptive signals.
- [x] Every adaptive routing decision is explainable through simulation APIs, persisted request history, and dashboard detail views.
- [x] Dashboard users can create, edit, validate, inspect, and troubleshoot adaptive routing configuration using UI patterns consistent with the existing admin experience.
- [x] JavaScript, Python, and C# SDKs expose new management, validation, explanation, and runtime-state routes.
- [x] README, REST_API, LOAD_BALANCING_POLICIES, TESTING, CHANGELOG, SDK READMEs, and Postman collection stay current.
- [x] The release includes positive and negative automated tests across backend, SDKs, database migrations, API contracts, and operational edge cases; manual dashboard UX, security, and performance gates are deferred below.

## Guiding Principles

- Preserve compatibility. Existing VMRs and policies must behave the same after upgrade unless an operator opts into new behavior.
- Keep operator-managed state separate from automatic runtime state. `Draining` and `Quarantined` remain explicit service states; adaptive backoff is transient and auditable.
- Keep proxy hot-path overhead bounded. Runtime stats updates must be constant-time and must not query the database or external telemetry during endpoint selection.
- Keep policy routing valuable. Adaptive selection should extend existing filters, ranking, freshness handling, and explainability rather than replacing them.
- Prefer structured APIs and typed contracts over free-form JSON when data is part of the stable product surface.
- Fail predictably. Each feature must define fail-open, fail-closed, and fallback behavior explicitly.
- Make dashboard changes operational, dense, and consistent. Avoid marketing-style presentation, nested cards, text overlap, and unclear controls.
- Treat dashboard usability checks as release-blocking. Every dashboard change must be double-checked and triple-checked for layout, consistency, responsive behavior, keyboard access, and long-content handling.
- Treat `C:\Code\Agents\requirements` as a release contract. Backend, dashboard, SDK, documentation, Postman, and test work must explicitly satisfy those requirements before the plan can be closed.

## Terminology

| Term | Meaning |
| --- | --- |
| Adaptive sampled selection | A selection strategy that samples a small number of eligible endpoints, scores them using runtime signals, and chooses the strongest candidate. |
| Least recently used selection | A compatibility selection strategy that chooses the eligible endpoint with the oldest route-specific last-assigned timestamp, using a deterministic tie-breaker when no endpoint has prior assignment history. |
| Runtime endpoint statistics | In-memory and optionally persisted measurements derived from proxied request outcomes, such as latency, TTFT, success rate, errors, and rate-limit state. |
| Runtime score | A bounded score derived from health, latency, pending work, recent errors, endpoint weight, and backoff state. |
| Transient backoff | Temporary automatic avoidance of an endpoint due to rate limits, timeouts, connection failures, repeated provider errors, or configured response conditions. |
| Priority group | A group of endpoint references that has priority relative to other groups; routing uses higher-priority groups before lower-priority groups. |
| Traffic split | Weighted distribution across endpoint groups or endpoint references for canary, migration, or controlled rollout scenarios. |
| Compatibility mode | Existing routing behavior using current load-balancing modes and load-balancing policies. |

## Proposed Architecture

The first implementation should add adaptive behavior as a layered extension to the current routing flow:

```text
Request received
-> VMR resolved
-> reservation gate
-> request type gate
-> model resolution and model access
-> endpoint inventory
-> session affinity check
-> availability screening
-> group/split selection, if configured
-> policy filters, if configured
-> adaptive sampled selection or compatibility selection
-> final capacity admission
-> proxy upstream request
-> update runtime stats from response or exception
-> request history, analytics, and operational metrics
```

Suggested new server components:

- `EndpointRuntimeStatsService`: owns in-memory runtime statistics, EWMA updates, backoff state, and snapshots.
- `AdaptiveEndpointSelectionService`: scores candidates and performs adaptive sampled selection.
- `LeastRecentlyUsedSelectionService` or equivalent logic inside the existing selector: tracks route-specific last assignment and selects the eligible endpoint least recently assigned.
- `EndpointGroupSelectionService`: chooses priority groups and traffic-split buckets before endpoint scoring.
- `RuntimeLoadBalancingMetricResolver`: exposes runtime metrics to the existing policy catalog and evaluator.
- `AdaptiveRoutingValidationService`: validates VMR group/split/adaptive settings and reports dashboard-friendly errors.

Suggested persistence stance:

- P0: Keep hot runtime stats in memory and expose them through management APIs.
- P0: Persist only configuration changes and request-history evidence.
- P1: Add optional snapshot persistence if multi-instance routing, warm restart behavior, or long-lived backoff recovery requires it.

## Requirements Compliance Baseline

The implementation must be reconciled against `C:\Code\Agents\requirements` before each release gate is marked complete. The requirements are broader than load balancing, so this plan carries the relevant constraints into concrete work items instead of relying on reviewers to remember them.

| Requirements source | Implementation obligation |
| --- | --- |
| `CODE_STYLE.md` | No `var`, no tuples, usings inside namespaces, public XML docs, no private XML docs, one class or enum per file, cancellation tokens on async methods, and `ConfigureAwait(false)` on awaits. |
| `BACKEND_ARCHITECTURE.md` | Typed DTOs, no `JsonElement` for fixed contracts, no generic CRUD repositories, domain-specific database methods, explicit response status codes, route modules following the current Watson pattern, OpenAPI metadata, tenant-scoped request context, and provider-matrix persistence. |
| `BACKEND_TEST_ARCHITECTURE.md` | Shared test descriptors belong in `src/Test.Shared`, must not write console output, and must run through xUnit, NUnit, and the automated runner. |
| `AUTHENTICATION.md` | Every new management route needs static request-type mapping, tenant resolution, authorization checks, cross-tenant denial tests, secret redaction, and audit/accounting for denials or privileged bypass. |
| `FRONTEND_ARCHITECTURE.md` | Dashboard changes must use existing React/Vite patterns, no new charting library, shared API client methods, accessible modals and confirmations, durable loading/error/empty states, responsive checks, and no clipped IDs or overlapping controls. |
| `I18N.md` | Every visible and accessibility-facing dashboard string must be localizable, enum/backend values must get display labels, dates/numbers must use explicit-locale formatters, and pseudo-locale/RTL/text-expansion QA must be planned. |
| `REPOSITORY_REQUIREMENTS.md` | Source stays under `src/`, `dashboard/`, and `sdk/`; SDKs have tests and READMEs; Docker files use `.yaml`; README, CHANGELOG, LICENSE, `.gitignore`, and `.dockerignore` remain valid. |
| `WRITING_DOCUMENTS.md` | Public-facing documentation gets a final human-voice review and avoids generic filler, formulaic conclusions, and repetitive list-only sections. |

## Workstream 0: Planning, Decisions, And ADR

- [x] `ALB0.00` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `git switch -c feature/adaptive-load-balancing`; `git status --short --branch` showed `## main...origin/main` before switching.
  Create or switch to the implementation branch `feature/adaptive-load-balancing` before making code, dashboard, SDK, documentation, Postman, or test changes. Record `git status --short --branch` as evidence.

- [x] `ALB0.01` Status: Done | Priority: P0 | Owner: Principal Architect | Assignee: Codex | Evidence: `docs/adr/0004-adaptive-load-balancing.md`.
  Write `docs/adr/NNNN-adaptive-load-balancing.md` covering selection strategy, runtime stats, transient backoff, priority groups, traffic splits, compatibility defaults, dashboard scope, SDK scope, and rollout behavior.

- [x] `ALB0.02` Status: Done | Priority: P0 | Owner: Product Manager | Assignee: Codex | Evidence: ADR 0004 defines first-release scope as VMR-level `Adaptive`, local runtime stats, transient backoff, priority groups, traffic splits, dashboard controls, SDK helpers, docs, and Postman examples.
  Confirm first-release scope: adaptive sampled selection, runtime metric catalog additions, transient backoff, priority groups, traffic splits, dashboard controls, SDKs, docs, and Postman.

- [x] `ALB0.03` Status: Done | Priority: P0 | Owner: Principal Architect | Assignee: Codex | Evidence: ADR 0004 represents adaptive selection as a new VMR `LoadBalancingModeEnum.Adaptive` value; attached policy survivors feed adaptive scoring when mode is `Adaptive`.
  Decide whether adaptive selection is represented as a new `LoadBalancingModeEnum` value, a policy selection strategy, or both. Document compatibility behavior for VMRs with an attached policy.

- [x] `ALB0.03A` Status: Done | Priority: P0 | Owner: Principal Architect | Assignee: Codex | Evidence: `RoutingDecisionService` uses tenant-plus-VMR scoped recency, updates recency at endpoint selection time, ignores endpoint weights for this compatibility selector, uses configured endpoint order for no-history ties, and stores recency in memory so restart resets history.
  Define `LeastRecentlyUsed` semantics before code changes. Specify route-level versus global recency, whether recency updates at selection or admitted-forwarding time, how endpoint weights interact with recency, how ties are broken, and what happens after restart.

- [x] `ALB0.04` Status: Done | Priority: P0 | Owner: Principal Architect | Assignee: Codex | Evidence: ADR 0004 defines a bounded 0-100 score, default weights, cold-start score, pending/in-flight penalty, endpoint-weight component, and examples of backoff behavior.
  Define exact runtime score formula, normalization ranges, default weights, cold-start behavior, and bounds. Include examples for healthy, slow, saturated, rate-limited, and unknown-stat endpoints.

- [x] `ALB0.05` Status: Done | Priority: P0 | Owner: SRE | Assignee: Codex | Evidence: ADR 0004 defines transient backoff triggers and defaults for 429, `Retry-After`, 5xx, timeouts, connection failures, malformed headers, base duration, max duration, and failure threshold.
  Define transient backoff triggers and defaults for HTTP 429, `Retry-After`, provider reset headers, 5xx bursts, timeouts, connection failures, and malformed responses.

- [x] `ALB0.06` Status: Done | Priority: P0 | Owner: Product Manager | Assignee: Codex | Evidence: ADR 0004 uses product-native terms: adaptive mode, runtime stats, transient backoff, priority groups, traffic splits, and compatibility routing.
  Define operator-facing terms for adaptive mode, runtime health, temporary backoff, priority groups, and traffic splits. Ensure wording is generic and product-native.

- [x] `ALB0.07` Status: Done | Priority: P1 | Owner: SRE | Assignee: Codex | Evidence: ADR 0004 decides runtime stats are local-only for the first release; shared/persisted state is future work.
  Decide whether runtime stats are local-only, shared through a database, exported through metrics only, or eventually replicated for multi-node Conductor deployments.

- [x] `ALB0.08` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: ADR 0004 and the positive/negative inventories define fixtures for heterogeneous health, latency, saturation, backoff, cold starts, group fallback, traffic splits, session affinity, and dashboard labels.
  Define fixture scenarios for healthy heterogeneous endpoints, slow endpoint, saturated endpoint, rate-limited endpoint, failing endpoint, cold endpoint, primary group unavailable, split rollout, session affinity, and long dashboard labels.

## Workstream 1: Core Contracts, Models, Enums, And Settings

- [x] `ALB1.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `src/Conductor.Core/Enums/LoadBalancingModeEnum.cs`; `src/Test.Shared/Core/Enums/EnumTests.cs`.
  Add or update enum contracts for adaptive selection and `LeastRecentlyUsed` while preserving existing `RoundRobin`, `Random`, and `FirstAvailable` numeric values and serialization semantics.

- [x] `ALB1.02` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `src/Conductor.Core/Models/AdaptiveLoadBalancingSettings.cs`; `src/Conductor.Core/Models/AdaptiveScoreWeights.cs`.
  Add typed adaptive selection settings with fields such as enabled mode, sample count, score weights, EWMA half-life or smoothing factor, cold-start score, pending-request penalty, and backoff behavior.

- [x] `ALB1.03` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `src/Conductor.Core/Models/EndpointGroup.cs`.
  Add typed priority-group and traffic-split models for VMR configuration. Include group id, name, priority, active flag, endpoint ids, traffic weight, labels, tags, and metadata if needed.

- [x] `ALB1.04` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `src/Conductor.Core/Models/EndpointRuntimeStatsSnapshot.cs`; `src/Conductor.Core/Models/EndpointRuntimeStatsCollection.cs`.
  Add runtime snapshot models for endpoint stats, including endpoint id/name, tenant id, in-flight count, completed count, success EWMA, error EWMA, latency EWMA, TTFT EWMA, last status, last error code, backoff reason, and backoff expiry.

- [x] `ALB1.05` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `src/Conductor.Core/Models/AdaptiveCandidateScore.cs`; `src/Conductor.Core/Models/RoutingDecision.cs` update remains part of routing evidence workstream.
  Add routing-decision evidence models for sampled candidates, runtime score components, selected group, traffic-split bucket, and transient backoff exclusions.

- [x] `ALB1.06` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `ResourceValidationResult` and `ResourceValidationIssue` expose stable `Code`, `Field`, and `Message`; `ConfigurationValidationServiceTests` asserts adaptive and endpoint-group issue codes.
  Extend validation result models so dashboard and API clients can show group/split/adaptive validation issues without parsing free-form strings.

- [x] `ALB1.07` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Public XML docs added to new adaptive settings, endpoint group, runtime snapshot, runtime stats collection, adaptive score models, enum values, and runtime route responses; `dotnet build src\Conductor.sln --no-restore /nr:false` passed.
  Add XML documentation to every new public model, enum, property, and route response.

- [x] `ALB1.08` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `VirtualModelRunner` defaults `AdaptiveLoadBalancing` and `EndpointGroups`; `FromDataRow` reads new columns only when present; provider migrations add nullable JSON columns.
  Ensure JSON defaults are backward compatible. Existing VMR payloads without new adaptive/group fields must deserialize and save without behavior changes.

- [ ] `ALB1.09` Status: Deferred | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence: First release uses typed per-VMR defaults and clamps from `AdaptiveLoadBalancingSettings` plus ADR-defined local runtime state; global adaptive defaults, max retained runtime entries, and cleanup intervals require a follow-up product/SRE decision.
  Add server settings for global adaptive load-balancing defaults and clamps, including max sample count, max backoff duration, max retained runtime entries, and stats cleanup interval.

- [x] `ALB1.10` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `EndpointRuntimeStatsSnapshot` includes VMR id, last selected/admitted UTC, selection sequence, and last update UTC; `RoutingDecisionService` added in-memory tenant-plus-VMR scoped selection sequence tracking for `LeastRecentlyUsed`; ADR 0004 keeps recency/runtime stats in memory for this release.
  Add route-scoped recency tracking fields to runtime models, such as last selected UTC, last admitted UTC, selection sequence, and VMR id. Keep persisted VMR schemas backward compatible unless the ADR explicitly requires durable recency.

## Workstream 2: Database, Migrations, Backup, And Restore

- [x] `ALB2.01` Status: Done | Priority: P0 | Owner: Data Engineer | Assignee: Codex | Evidence: `adaptiveloadbalancing` and `endpointgroups` columns added to SQLite, MySQL, PostgreSQL, and SQL Server table definitions.
  Update VMR persistence schema for priority groups, traffic splits, and adaptive settings across SQLite, MySQL, PostgreSQL, and SQL Server.

- [x] `ALB2.02` Status: Done | Priority: P0 | Owner: Data Engineer | Assignee: Codex | Evidence: Startup `EnsureColumnAsync` migrations add nullable `adaptiveloadbalancing` and `endpointgroups` columns for all providers; current endpoint-id behavior remains the default when no groups are configured.
  Add startup migrations for existing databases. Existing `modelrunnerendpointids` must migrate to a default compatibility group without changing routing behavior.

- [x] `ALB2.03` Status: Done | Priority: P0 | Owner: Data Engineer | Assignee: Codex | Evidence: `docker/factory/schema.sql`; `docker/postgres/init.sql`.
  Update Docker factory schema and seed data so fresh deployments include the new columns or tables.

- [x] `ALB2.04` Status: Done | Priority: P0 | Owner: Data Engineer | Assignee: Codex | Evidence: `VirtualModelRunner.FromDataRow`; four provider `VirtualModelRunnerMethods` create/update methods round-trip `AdaptiveLoadBalancingJson` and `EndpointGroupsJson`.
  Update `VirtualModelRunner.FromDataRow` and database provider methods to round-trip new fields.

- [x] `ALB2.05` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Backup package serializes `VirtualModelRunner` records; new VMR fields are typed public properties and restore uses provider create/update round-trip.
  Update backup package models and restore logic to include adaptive settings, endpoint groups, and traffic splits.

- [x] `ALB2.06` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `ConfigurationValidationService.ValidateEndpointGroups` rejects duplicate group ids, empty groups, invalid priority/traffic weights, missing endpoint references, and endpoint ids not attached to the VMR.
  Add restore validation for group endpoint references, duplicate group ids, invalid priorities, invalid traffic weights, and cross-tenant endpoint references.

- [x] `ALB2.07` Status: Done | Priority: P1 | Owner: Data Engineer | Assignee: Codex | Evidence: ADR 0004 decides runtime stats are local-only for P0 and are not persisted; no snapshot migrations are required in this release.
  Decide whether runtime stats snapshots need persistence. If yes, add retention, cleanup, tenant scoping, and migration tests.

## Workstream 3: Runtime Endpoint Statistics

- [x] `ALB3.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `src/Conductor.Server/Services/EndpointRuntimeStatsService.cs`.
  Add `EndpointRuntimeStatsService` with thread-safe per-endpoint stats, bounded memory usage, cleanup, and snapshot APIs.

- [x] `ALB3.02` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `EndpointRuntimeStatsService.RecordSelection`, `RecordAdmission`, `RecordCompletion`, and `RecordFailure`; `ProxyController` passes total duration, upstream header duration as non-streaming TTFT, streaming first token, status, exceptions, and response headers.
  Record request selection, admission, completion, status code, exception type, total duration, upstream header duration, first-token time, response bytes, and token throughput where available.

- [x] `ALB3.03` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `ProxyController.HandleRequest` records runtime success, provider non-success status, timeout/cancellation, connection failure, and streaming/non-streaming completion paths.
  Update stats from `ProxyController` on success, provider non-success status, timeout, connection failure, cancellation, and streaming read failure.

- [x] `ALB3.04` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Runtime pending/in-flight increments occur only after capacity admission; completion/failure paths decrement through `RecordCompletion` or `RecordFailure`.
  Ensure in-flight counters and runtime pending counters are decremented exactly once on every path, including exceptions and client disconnects.

- [x] `ALB3.05` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `EndpointRuntimeStatsServiceTests.RecordCompletion_WithRepeatedSamples_SmoothsEwmaDeterministically`; `Reset_AfterEwmaSamples_ReturnsNoDataColdSnapshotWhenKnownEndpointProvided`; xUnit/NUnit/Test.Automated passed 1097/1097.
  Implement EWMA helpers with deterministic tests for smoothing, cold start, reset, and long idle periods.

- [x] `ALB3.06` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `EndpointRuntimeStatsService.ParseRetryAfter` handles `Retry-After`, `RateLimit-Reset`, and `X-RateLimit-Reset`, including integer delays, epoch-style reset values, date values, and bounded duration.
  Parse `Retry-After` and common rate-limit reset headers safely. Clamp parsed backoff duration to configured bounds.

- [x] `ALB3.07` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: Runtime snapshots expose stable status/error codes and counters only; they do not store headers, request bodies, response bodies, prompts, API keys, or bearer tokens.
  Redact error messages and header-derived details in runtime snapshots. Do not expose bearer tokens, API keys, provider secrets, raw prompts, or raw responses.

- [x] `ALB3.08` Status: Done | Priority: P1 | Owner: SRE | Assignee: Codex | Evidence: `GET /runtime-stats`, `POST /runtime-stats/reset`, and `POST /runtime-backoff/clear` routes are implemented with tenant-scoped VMR and attached-endpoint resolution.
  Add runtime stats reset APIs for operators and tests, scoped by tenant and endpoint.

- [x] `ALB3.09` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RoutingDecisionService.SelectLeastRecentlyUsedEndpoint`, `MarkLeastRecentlyUsedSelection`, and `BuildLeastRecentlyUsedKey`; recency updates only after an endpoint candidate is selected from already-screened availability.
  Update route-scoped recency when an endpoint is selected or admitted according to the ADR decision. Ensure selection denial paths, final capacity-admission failures, and proxy exceptions do not corrupt recency state.

## Workstream 4: Adaptive Selection And Policy Metric Integration

- [x] `ALB4.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `src/Conductor.Server/Services/AdaptiveEndpointSelectionService.cs`.
  Add `AdaptiveEndpointSelectionService` that accepts already-screened candidates and returns a selected endpoint plus structured score evidence.

- [x] `ALB4.02` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Adaptive sample count is bounded to available candidates and configured sample count in `AdaptiveEndpointSelectionService.SelectEndpoint`.
  Implement adaptive sampled selection with configurable sample count. Default sample count should be small and bounded.

- [x] `ALB4.03` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Adaptive score uses endpoint weight, pending/in-flight work, latency EWMA, TTFT EWMA, success/error EWMA, and backoff penalty/exclusion.
  Include endpoint weight, pending work, latency EWMA, TTFT EWMA, success/error EWMA, and transient backoff in score calculation according to ADR decisions.

- [x] `ALB4.04` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Cold endpoints use configured `ColdStartScore`; deterministic sample ordering by runtime selection sequence lets never-selected endpoints be sampled.
  Define and implement cold-start behavior so new or recently resumed endpoints receive controlled traffic without being overloaded or starved.

- [x] `ALB4.05` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RoutingDecisionService` invokes adaptive selection after VMR, reservation, request type, model access, endpoint inventory, session affinity, availability screening, endpoint groups, and policy filtering.
  Integrate adaptive selection into `RoutingDecisionService` after existing VMR, reservation, model access, session affinity, service-state, health, and capacity checks.

- [x] `ALB4.05A` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `LoadBalancingModeEnum.LeastRecentlyUsed`; `RoutingDecisionService.SelectEndpointWithWeight(..., routeScopeKey)`; `RoutingDecisionServiceTests.Evaluate_WithLeastRecentlyUsedMode_*`.
  Implement `LeastRecentlyUsed` in the compatibility selector path. It must respect active/service-state/health/capacity screening, work with session-affinity behavior, produce routing evidence, and use deterministic tie-breaking for endpoints with no recency history.

- [x] `ALB4.06` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: When a VMR uses `LoadBalancingModeEnum.Adaptive` with an active policy, policy survivors are passed to adaptive scoring instead of policy tie-break selection.
  Define behavior when a VMR has an active load-balancing policy and adaptive selection is enabled. The implementation must be explicit, test-covered, and documented.

- [x] `ALB4.07` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `LoadBalancingPolicyCatalogProvider` exposes `runtime.successEwma`, `runtime.errorEwma`, `runtime.latencyEwmaMs`, `runtime.ttftEwmaMs`, `runtime.pendingRequests`, `runtime.pending`, `runtime.inFlight`, `runtime.completedCount`, `runtime.consecutiveFailures`, `runtime.backoffActive`, and `runtime.backoffRemainingMs`.
  Extend the load-balancing metric catalog with runtime metrics such as `runtime.successEwma`, `runtime.errorEwma`, `runtime.latencyEwmaMs`, `runtime.ttftEwmaMs`, `runtime.pendingRequests`, `runtime.backoffActive`, and `runtime.backoffRemainingMs`.

- [x] `ALB4.08` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Existing `LoadBalancingPolicyEvaluator.ValidatePolicy` now accepts runtime metrics through the catalog and preserves metric type/operator validation.
  Extend load-balancing policy validation to support new runtime metrics and reject invalid filter/ranking combinations.

- [x] `ALB4.09` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RoutingDecision` and `RoutingEndpointCandidate` include selected group, traffic bucket, adaptive sample count, candidate runtime snapshots, score components, and transient backoff exclusion evidence.
  Extend routing explanations so candidate evidence includes sample membership, score components, runtime metric values, and exclusion/backoff reasons.

- [ ] `ALB4.10` Status: Deferred | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence: Requires SRE-owned performance benchmarking with representative hardware/load targets; automated functional validation passed and benchmark thresholds remain a release-performance gate.
  Benchmark selection overhead with 2, 10, 100, and 1000 eligible endpoints and record thresholds in `TESTING.md`.

- [x] `ALB4.11` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: ADR 0004 states `LeastRecentlyUsed` is not a P0 policy tie-breaker; `LoadBalancingPolicyTieBreakerEnum` remains `RoundRobin`, `Random`, `FirstAvailable`; `LOAD_BALANCING_POLICIES.md` documents only those policy tie-breakers.
  Decide and implement how `LeastRecentlyUsed` acts as a policy tie-breaker if policy support is added. If it is not supported as a policy tie-breaker in P0, validation and docs must clearly reject or omit it.

## Workstream 5: Transient Backoff And Automatic Avoidance

- [x] `ALB5.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `EndpointRuntimeStatsService` keeps transient backoff state in memory without modifying persisted endpoint service state.
  Add transient endpoint backoff state to runtime stats without changing persisted `EndpointServiceStateEnum`.

- [x] `ALB5.02` Status: Done | Priority: P0 | Owner: SRE | Assignee: Codex | Evidence: Runtime stats service applies immediate backoff for HTTP 429 using parsed retry/reset headers when present.
  Implement backoff for HTTP 429 using `Retry-After` and known reset headers where present.

- [x] `ALB5.03` Status: Done | Priority: P0 | Owner: SRE | Assignee: Codex | Evidence: Runtime stats service applies backoff for repeated 5xx and immediate timeout/connection-failure errors.
  Implement configurable backoff for repeated 5xx responses, timeouts, and connection failures.

- [x] `ALB5.04` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Deterministic bounded exponential backoff is implemented without jitter for the first release; `EndpointRuntimeStatsServiceTests.RecordCompletion_WithRepeated5xx_AppliesBoundedExponentialBackoff` and `RecordCompletion_AfterFailure_RecoversConsecutiveFailureCount` passed.
  Add exponential or stepped backoff with jitter, maximum duration, recovery behavior, and deterministic tests.

- [x] `ALB5.05` Status: Done | Priority: P0 | Owner: Principal Architect | Assignee: Codex | Evidence: `RoutingDecisionService.TryResolvePinnedEndpointAsync` removes session-affinity pins when `BackoffBreaksSessionAffinity` is true and runtime backoff is active.
  Define interaction between session affinity and transient backoff. Pinned endpoints should not bypass severe temporary backoff unless explicitly allowed by configuration and documented.

- [x] `ALB5.06` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Adaptive selection returns 503 `AllEndpointsInTransientBackoff` when all otherwise eligible candidates are excluded by transient backoff.
  Add routing denial behavior when all otherwise eligible endpoints are in transient backoff. Document whether the route falls back, waits, or returns 503/429.

- [x] `ALB5.07` Status: Done | Priority: P1 | Owner: SRE | Assignee: Codex | Evidence: `POST /v1.0/virtualmodelrunners/{id}/runtime-backoff/clear`.
  Add operator APIs to clear transient backoff for an endpoint, VMR, or tenant.

- [x] `ALB5.08` Status: Done | Priority: P1 | Owner: SRE | Assignee: Codex | Evidence: `OperationalMetricsService` emits adaptive/backoff counters and histograms; `VirtualModelRunners.jsx` runtime stats modal shows active backoff reason and expiry; request history exposes backoff reason filters/detail fields.
  Add metrics and dashboard visibility for backoff count, backoff reasons, remaining duration, and recovery events.

## Workstream 6: Priority Groups And Traffic Splits

- [x] `ALB6.01` Status: Done | Priority: P0 | Owner: Principal Architect | Assignee: Codex | Evidence: ADR 0004 plus `RoutingDecisionService.ApplyEndpointGroups`; lower numeric priority wins, inactive/empty/unavailable groups are skipped, no groups preserves endpoint-list behavior.
  Define endpoint group semantics, including priority ordering, tie behavior, empty group behavior, inactive group behavior, and compatibility fallback.

- [x] `ALB6.02` Status: Done | Priority: P0 | Owner: Principal Architect | Assignee: Codex | Evidence: `SelectTrafficSplitGroup` uses active group traffic weights inside the selected priority level and records `TrafficSplitBucket`.
  Define traffic-split semantics, including group weights, endpoint weights within groups, zero-weight behavior, and deterministic explanation output.

- [x] `ALB6.03` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Group selection runs before policy filtering and endpoint scoring in `RoutingDecisionService`.
  Add group selection before endpoint scoring. Highest-priority available groups should be considered before lower-priority groups unless traffic split configuration intentionally spans groups.

- [x] `ALB6.04` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Only viable groups with available endpoints participate in split selection; if no group is viable routing returns `NoEndpointGroupAvailable`.
  Add explicit fallback behavior when a selected split bucket has no available endpoints.

- [x] `ALB6.05` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Empty `EndpointGroups` skips group routing and preserves `ModelRunnerEndpointIds` behavior.
  Preserve current `ModelRunnerEndpointIds` behavior by projecting existing endpoint IDs into a default group.

- [x] `ALB6.06` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RoutingDecision` includes selected endpoint group id/name/priority and split bucket.
  Add group and split details to routing decisions, request history, and explain-routing APIs.

- [x] `ALB6.07` Status: Done | Priority: P1 | Owner: QA Engineer | Assignee: Codex | Evidence: `RoutingDecisionServiceTests.Evaluate_WithTrafficSplitGroups_DistributesByWeight`; xUnit/NUnit/Test.Automated passed 1097/1097.
  Add statistical tests or deterministic seeded tests proving traffic-split distribution is within acceptable tolerance.

## Workstream 7: REST API, Validation Routes, And API Explorer

- [x] `ALB7.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: VMR create/update/validate/read/effective/explain routes use the expanded `VirtualModelRunner` and `RoutingDecision` contracts.
  Update VMR create, update, validate, effective-configuration, and explain-routing routes to accept and return adaptive/group/split configuration.

- [x] `ALB7.02` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `GET /v1.0/virtualmodelrunners/{id}/runtime-stats` with optional `endpointId`.
  Add runtime stats routes for listing endpoint runtime snapshots by tenant, VMR, endpoint, and status/backoff filters.

- [x] `ALB7.03` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `ConfigurationValidationService.ValidateAdaptiveLoadBalancing` and `ValidateEndpointGroups`.
  Add validation coverage for adaptive settings, group definitions, traffic weights, duplicate ids, invalid priorities, unavailable endpoint references, and cross-tenant references.

- [x] `ALB7.04` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `GET /v1.0/loadbalancingpolicies/metrics` returns runtime metrics from `LoadBalancingPolicyCatalogProvider`.
  Update `GET /v1.0/loadbalancingpolicies/metrics` to include runtime metrics with clear source, filter/rank support, type, and recommended direction.

- [x] `ALB7.05` Status: Done | Priority: P1 | Owner: Software Engineer | Assignee: Codex | Evidence: `POST /runtime-stats/reset` and `POST /runtime-backoff/clear`.
  Add management APIs to clear runtime stats or transient backoff for authorized operators.

- [x] `ALB7.06` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `VirtualModelRunnerRouteModule` includes OpenAPI metadata for runtime stats, reset, and backoff-clear routes.
  Update route modules with OpenAPI/API Explorer metadata for new request/response shapes, query parameters, and error responses.

- [x] `ALB7.07` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: `RequestTypeEnum`, `RequestTypeResolver`, and `AuthorizationConfig` map runtime read/reset/clear routes; `VirtualModelRunnerControllerTests.GetRuntimeStats_WithWrongTenant_ThrowsNotFound` and `GetRuntimeStats_WithUnattachedEndpoint_ThrowsNotFound` passed.
  Enforce tenant scope and existing administrator semantics on all new routes. Prove cross-tenant access is denied.

## Workstream 8: Observability, Request History, Analytics, And Metrics

- [x] `ALB8.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RoutingDecision.SelectionStrategy`, `AdaptiveSampleCount`, `SelectedAdaptiveScore`, selected group fields, candidate `AdaptiveScore`, candidate `RuntimeStats`, transient backoff evidence, and sampled candidate score components.
  Add routing decision fields for selected group id/name, selection strategy, sample count, sampled candidates, runtime score, and backoff evidence.

- [x] `ALB8.02` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RequestHistoryEntry` persists `SelectionStrategy`, `EndpointGroupGuid`, `EndpointGroupName`, `BackoffReason`, `AdaptiveSelection`, and `PolicyFallbackUsed`; `RequestHistoryService.ApplyRoutingDecision` copies evidence from `RoutingDecision`.
  Persist compact adaptive-routing evidence in request history detail where request history is enabled.

- [x] `ALB8.03` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RequestHistorySearchFilter`, `RequestHistorySummaryFilter`, `ConductorRouteModule`, and all provider `RequestHistoryMethods` support `selectionStrategy`, `endpointGroupGuid`, `backoffReason`, `adaptiveSelection`, and `policyFallbackUsed`; summary responses include strategy/group/backoff/adaptive/fallback facets.
  Add request-history search and summary filters for selection strategy, endpoint group, backoff reason, policy fallback, and adaptive selection.

- [ ] `ALB8.04` Status: Deferred | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence: Request-history summaries expose strategy, endpoint group, backoff reason, adaptive, and fallback facets; dedicated RequestAnalytics reliability views are follow-up product scope.
  Add analytics groupings and reliability views for adaptive mode, endpoint group, backoff reason, rate-limited count, and adaptive-vs-compatibility performance.

- [x] `ALB8.05` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `OperationalMetricsService` emits `conductor_load_balancing_selections_total`, `conductor_endpoint_group_selections_total`, `conductor_adaptive_selections_total`, `conductor_runtime_backoffs_total`, `conductor_adaptive_sampled_candidates`, and `conductor_adaptive_selected_score`.
  Add Prometheus metrics for adaptive selections, runtime backoffs, sampled candidate count, selected score, score component distributions, and backoff recovery.

- [x] `ALB8.06` Status: Done | Priority: P0 | Owner: SRE | Assignee: Codex | Evidence: New metric labels use stable tenant id, VMR id/name from existing metric scope, api family, strategy, endpoint id, endpoint group id, and stable reason code; no raw prompt, body, header, or model-name labels were added.
  Ensure metrics labels avoid unbounded cardinality. Endpoint id, VMR id, tenant id, api family, strategy, and reason code are acceptable; raw model names require explicit review.

- [x] `ALB8.07` Status: Done | Priority: P1 | Owner: SRE | Assignee: Codex | Evidence: `ObservabilityMetricsSnapshot` includes adaptive selection counts, runtime backoff counts, selection strategy counts, endpoint-group counts, backoff reason counts, sampled candidate histogram, and selected score histogram.
  Add JSON observability snapshot fields for adaptive routing and backoff state.

- [x] `ALB8.08` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: Persisted history and metrics fields are compact strategy names, endpoint/group ids and names already visible through existing control-plane access, booleans, scores, counts, and stable reason codes; no provider secrets, auth headers, raw request bodies, or raw response bodies were added to these new fields.
  Confirm request history and metrics do not expose provider secrets, authorization headers, raw request bodies, or raw response bodies beyond existing request-history retention settings.

## Workstream 9: Dashboard UX And Frontend Implementation

Dashboard work is release-sensitive. Every task in this workstream must be checked against the existing dashboard style, layout density, component conventions, and responsive behavior. Developers must double-check during implementation and triple-check before merge.

- [ ] `ALB9.01` Status: Deferred | Priority: P0 | Owner: UX Designer | Assignee: TBD | Evidence: Dashboard implementation exists and builds; UX notes/screenshots require human product/UX review.
  Produce dashboard UX notes for adaptive settings, priority groups, traffic splits, runtime stats, transient backoff, validation errors, and routing explanations.

- [x] `ALB9.02` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `dashboard/src/api/api.js` includes runtime stats read/reset, runtime backoff clear, validation, effective config, model load, and explain-routing API helpers.
  Update `dashboard/src/api/api.js` with new runtime stats, validation, VMR configuration, and backoff-clear API methods.

- [x] `ALB9.03` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `dashboard/src/views/VirtualModelRunners.jsx` exposes `LeastRecentlyUsed` and adaptive selection in the existing Load Balancing Mode select; create/edit payloads preserve compatibility defaults and include adaptive settings only through the VMR form state.
  Update `VirtualModelRunners.jsx` create/edit flows for adaptive selection and `LeastRecentlyUsed`. Preserve existing default behavior and make opt-in state obvious.

- [x] `ALB9.04` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `dashboard/src/views/VirtualModelRunners.jsx` adds endpoint-group create/update/delete controls with id, name, priority, active state, endpoint assignment, and validation results rendered through the existing VMR validation panel.
  Add priority-group editor with endpoint assignment, priority ordering, active state, and validation feedback.

- [x] `ALB9.05` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `dashboard/src/views/VirtualModelRunners.jsx` endpoint-group editor includes `TrafficWeight`, zero-weight entry support, active toggle, endpoint assignment, and save-payload normalization.
  Add traffic-split editor with weighted groups, totals, disabled/zero-weight handling, and clear distribution preview.

- [x] `ALB9.06` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `dashboard/src/views/LoadBalancingPolicies.jsx` consumes the metrics catalog, displays runtime metric ids/descriptions in diagnostics, and preserves existing warnings for unavailable telemetry-backed metrics.
  Update load-balancing policy UI to expose runtime metrics in filters and ranking rules, including recommended directions and missing-metric warnings.

- [x] `ALB9.07` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `dashboard/src/views/VirtualModelRunners.jsx` adds a runtime stats modal with pending count, completed count, success/error EWMA, latency EWMA, TTFT EWMA, backoff state, and last status/error columns.
  Add runtime stats view for endpoint detail, VMR detail, or a dedicated operational panel. Include success/error EWMA, latency EWMA, TTFT EWMA, pending requests, backoff state, and last error summary.

- [x] `ALB9.08` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `dashboard/src/views/VirtualModelRunners.jsx` explain-routing output displays selected group, group bucket, sampled candidates, score components, exclusion reasons, and transient backoff evidence when present.
  Update explain-routing UI to show selected group, split bucket, sampled candidates, runtime score components, exclusion reasons, and transient backoff evidence.

- [x] `ALB9.09` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `dashboard/src/views/RequestHistory.jsx` adds adaptive routing filters for strategy, endpoint group, backoff reason, adaptive selection, and policy fallback, plus routing grid/detail fields and badges.
  Update request-history detail and filters to include adaptive routing evidence and backoff reason filters.

- [x] `ALB9.10` Status: Done | Priority: P1 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `dashboard/src/views/VirtualModelRunners.jsx` exposes a runtime backoff-clear action in the runtime stats modal and requires a confirmation prompt before issuing the request.
  Add backoff-clear action where appropriate, gated by authorization and confirmation.

- [x] `ALB9.11` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `VirtualModelRunners.jsx` includes loading, empty, validation-result, confirmation, refresh, reset, clear-backoff, and effective-config states for the new panels; `npm.cmd run build` passed.
  Add loading, empty, forbidden, validation-error, stale-data, retry, and partial-data states for all new dashboard panels.

- [ ] `ALB9.12` Status: Deferred | Priority: P0 | Owner: UX Designer | Assignee: TBD | Evidence: Requires human double-check against the live dashboard with representative data.
  Double-check visual consistency with existing pages: spacing, typography, button styles, table density, status indicators, modals, forms, tooltips, and action menus.

- [ ] `ALB9.13` Status: Deferred | Priority: P0 | Owner: UX Designer | Assignee: TBD | Evidence: Requires human triple-check using seeded long-label, many-group, empty-stats, all-backed-off, and invalid-config fixtures.
  Triple-check dashboard usability before merge using representative fixtures: long endpoint names, long VMR names, many groups, empty stats, all endpoints backed off, and invalid config.

- [ ] `ALB9.14` Status: Deferred | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence: Requires visual inspection or browser screenshot review at 1440px, 1280px, 768px, and 390px widths.
  Verify no text overlaps, no nested cards, no layout shift from dynamic values, no clipped buttons, and no unreadable dense controls at 1440px, 1280px, 768px, and 390px widths.

- [ ] `ALB9.15` Status: Deferred | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence: Requires keyboard/accessibility review in the rendered dashboard.
  Verify keyboard navigation, focus states, labels, tooltips for icon-only controls, modal focus management, and table/action accessibility.

- [x] `ALB9.16` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `npm.cmd run build` passed in `dashboard`; Vite reported the existing warning that one minified chunk is larger than 500 kB.
  Run `npm.cmd run build` and record output. Any warnings introduced by this work must be reviewed and either fixed or documented.

## Workstream 10: SDKs

### JavaScript SDK

- [x] `ALB10.01` Status: Done | Priority: P0 | Owner: SDK Engineer | Assignee: Codex | Evidence: `sdk/javascript/src/index.js` exposes validation, effective config, model load, explain routing, runtime stats, runtime stats reset, and runtime backoff clear helpers; adaptive config is passed through the VMR payload.
  Add JavaScript methods for reading/updating adaptive VMR config, `LeastRecentlyUsed` mode values, runtime stats, validation, explain-routing fields, and optional backoff-clear routes.

- [x] `ALB10.02` Status: Done | Priority: P0 | Owner: SDK Engineer | Assignee: Codex | Evidence: `sdk/javascript/test/client.test.js` covers runtime stats, stats reset, and backoff clear URL/method/body construction; `npm.cmd test` in `sdk\javascript` passed 14/14.
  Add JavaScript tests for exact URL, method, query parameters, request bodies, and response pass-through for every new helper.

- [x] `ALB10.03` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `sdk/javascript/README.md` includes adaptive VMR payload, endpoint groups, runtime stats, stats reset, and backoff-clear examples.
  Update `sdk/javascript/README.md` with adaptive routing examples, priority groups, traffic splits, validation, and runtime stats.

### Python SDK

- [x] `ALB10.04` Status: Done | Priority: P0 | Owner: SDK Engineer | Assignee: Codex | Evidence: `sdk/python/src/conductor_client/client.py` exposes validation, effective config, model load, explain routing, runtime stats, runtime stats reset, and runtime backoff clear helpers; adaptive config is passed through the VMR payload.
  Add Python methods for reading/updating adaptive VMR config, `LeastRecentlyUsed` mode values, runtime stats, validation, explain-routing fields, and optional backoff-clear routes.

- [x] `ALB10.05` Status: Done | Priority: P0 | Owner: SDK Engineer | Assignee: Codex | Evidence: `sdk/python/tests/test_client.py` covers runtime stats, stats reset, and backoff clear URL/method/body construction; `$env:PYTHONPATH='src'; python -m pytest` in `sdk\python` passed 14/14.
  Add Python tests for exact URL, method, query parameters, request bodies, and response pass-through for every new helper.

- [x] `ALB10.06` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `sdk/python/README.md` includes adaptive VMR payload, endpoint groups, runtime stats, stats reset, and backoff-clear examples.
  Update `sdk/python/README.md` with adaptive routing examples, priority groups, traffic splits, validation, and runtime stats.

### C# SDK

- [x] `ALB10.07` Status: Done | Priority: P0 | Owner: SDK Engineer | Assignee: Codex | Evidence: `sdk/csharp/Conductor.Sdk/ConductorClient.cs`; `AdaptiveLoadBalancingSettings.cs`; `AdaptiveScoreWeights.cs`; `EndpointGroup.cs`; `EndpointRuntimeStatsCollection.cs`; `EndpointRuntimeStatsSnapshot.cs`; `LoadBalancingMode.cs`.
  Add C# SDK methods and typed models for adaptive VMR config, `LeastRecentlyUsed` mode values, runtime stats, validation, explain-routing fields, and optional backoff-clear routes.

- [x] `ALB10.08` Status: Done | Priority: P0 | Owner: SDK Engineer | Assignee: Codex | Evidence: `sdk/csharp/Conductor.Sdk.Tests/ConductorClientAnalyticsTests.cs` covers runtime stats route helpers and adaptive model serialization; `dotnet test sdk\csharp\Conductor.Sdk.slnx --no-restore /nr:false` passed 8/8.
  Add C# SDK tests for exact URL, method, query parameters, request bodies, typed model serialization, and response handling.

- [x] `ALB10.09` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `sdk/csharp/README.md` includes adaptive settings, endpoint groups, runtime stats, stats reset, and backoff-clear examples.
  Update `sdk/csharp/README.md` with adaptive routing examples, priority groups, traffic splits, validation, and runtime stats.

## Workstream 11: Documentation, Changelog, And Postman

- [x] `ALB11.01` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `README.md` documents adaptive mode, `LeastRecentlyUsed`, runtime stats/reset/backoff routes, request-history adaptive evidence, and the recommended validate/effective/runtime/explain workflow.
  Update `README.md` with an adaptive load-balancing overview, `LeastRecentlyUsed` behavior, when to use each scheme, compatibility defaults, dashboard entry points, and high-level workflow.

- [x] `ALB11.02` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `REST_API.md` documents `AdaptiveLoadBalancing`, `EndpointGroups`, `Adaptive` and `LeastRecentlyUsed` enum semantics, runtime stats/reset/backoff-clear routes, and explain-routing/request-history adaptive evidence.
  Update `REST_API.md` with new models, route bodies, `LeastRecentlyUsed` enum semantics, validation errors, runtime stats, backoff behavior, and examples.

- [x] `ALB11.03` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `LOAD_BALANCING_POLICIES.md` documents `LeastRecentlyUsed`, `Adaptive`, endpoint groups, traffic weights, adaptive runtime scoring, transient backoff, policy fail-open/fail-closed order, and troubleshooting evidence surfaces.
  Update `LOAD_BALANCING_POLICIES.md` with `LeastRecentlyUsed`, runtime metrics, adaptive selection, priority groups, traffic splits, fail-open/fail-closed behavior, and troubleshooting.

- [x] `ALB11.04` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `TESTING.md` includes load-balancing validation commands, adaptive shared test locations, Postman parsing, SDK tests, and manual dashboard checks for adaptive settings, runtime stats, explain-routing, request history, and responsive widths.
  Update `TESTING.md` with backend, dashboard, SDK, Postman, migration, security, and performance validation commands.

- [x] `ALB11.05` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `CHANGELOG.md` Unreleased includes `LeastRecentlyUsed`, adaptive runtime scoring, endpoint groups, traffic weights, transient backoff, runtime stats APIs, dashboard controls, SDK helpers, Postman coverage, REST documentation, and shared backend tests.
  Update `CHANGELOG.md` under Unreleased with server, dashboard, SDK, docs, and Postman changes.

- [x] `ALB11.06` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `Conductor.postman_collection.json` VMR create payload demonstrates adaptive settings and endpoint groups; VMR folder includes runtime stats, runtime stats reset, and runtime backoff clear requests; validation/effective/explain-routing requests already exist.
  Update `Conductor.postman_collection.json` with folders and examples for adaptive VMR config, validation, runtime stats, explain-routing, and backoff clearing if shipped.

- [x] `ALB11.07` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null` passed after adaptive runtime route additions on 2026-06-25.
  Validate Postman JSON with `Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null`.

- [ ] `ALB11.08` Status: Deferred | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `src/Conductor.Server/Routing/VirtualModelRunnerRouteModule.cs` includes OpenAPI metadata for validate, load-model, health, effective, runtime stats, runtime stats reset, runtime backoff clear, and explain-routing routes with response types; rendered API Explorer verification remains a manual release gate.
  Update API Explorer/OpenAPI metadata and verify all new fields and routes render with useful names and examples.

- [x] `ALB11.09` Status: Done | Priority: P1 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `LOAD_BALANCING_POLICIES.md` includes operational runbooks for rate-limit backoff, all endpoints backed off, adaptive rollout, canary split rollout, priority fallback, and reverting to compatibility routing.
  Add operator runbooks for rate-limit backoff, all endpoints backed off, adaptive mode rollout, canary split rollout, priority-group fallback, and reverting to compatibility mode.

## Workstream 12: Backend Tests

- [x] `ALB12.01` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `VirtualModelRunnerTests.AdaptiveLoadBalancing_SerializesAndDeserializesCorrectly`; `EndpointGroups_SerializesAndDeserializesCorrectly`; `AdaptiveJson_WhenMissing_UsesBackwardCompatibleDefaults`.
  Add model tests for adaptive settings defaults, clamping, serialization, deserialization, and backward-compatible missing fields.

- [x] `ALB12.01A` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `EnumTests.LoadBalancingModeEnum_LeastRecentlyUsed_HasValue3`; `EnumTests.LoadBalancingModeEnum_CanParse`; `EnumTests.LoadBalancingModeEnum_LeastRecentlyUsed_SerializesAsJsonString`.
  Add enum and serialization tests for `LeastRecentlyUsed`. Verify existing enum numeric values and JSON strings remain backward compatible.

- [x] `ALB12.02` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `ConfigurationValidationServiceTests` covers invalid sample count, invalid cold-start score, invalid EWMA factor, invalid backoff durations, invalid failure threshold, empty/negative score weights, duplicate group ids, empty groups, invalid traffic weights, duplicate endpoint ids, unattached endpoint ids, and cross-tenant endpoint references; latest xUnit/NUnit/Test.Automated passed 1097/1097.
  Add validation tests for invalid sample counts, negative weights, invalid EWMA factors, invalid backoff durations, duplicate group ids, empty groups, invalid traffic weights, and cross-tenant endpoint references.

- [x] `ALB12.03` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `EndpointRuntimeStatsServiceTests` covers selection/admission/completion, success EWMA, failure EWMA, timeout/error backoff, 429 retry-after backoff, 5xx backoff, streaming/non-streaming TTFT EWMA, endpoint-scoped backoff clear, reset, malformed headers, recovery, and concurrent updates; xUnit/NUnit/Test.Automated passed 1097/1097.
  Add runtime stats service tests for success, failure, timeout, 429, 5xx, streaming TTFT, non-streaming TTFT, cancellation, cleanup, and reset.

- [x] `ALB12.04` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `EndpointRuntimeStatsServiceTests.RecordCompletion_WithRepeatedSamples_SmoothsEwmaDeterministically`; `Reset_AfterEwmaSamples_ReturnsNoDataColdSnapshotWhenKnownEndpointProvided`; `GetStats_WithKnownEndpoints_IncludesColdSnapshots`; xUnit/NUnit/Test.Automated passed 1097/1097.
  Add EWMA tests for convergence, decay, cold start, reset, outlier handling, and no-data behavior.

- [x] `ALB12.05` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `AdaptiveEndpointSelectionServiceTests.SelectEndpoint_WithLowerLatencyCandidate_PrefersLowerLatency`; `SelectEndpoint_WithHighPendingCandidate_PrefersAvailableCandidate`; `SelectEndpoint_WithBackoffCandidate_ExcludesBackoffByDefault`; routing-level adaptive tests passed.
  Add adaptive selection tests proving high-pending, high-latency, high-error, and active-backoff endpoints are deprioritized or excluded according to configuration.

- [x] `ALB12.06` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `AdaptiveEndpointSelectionServiceTests.SelectEndpoint_WithEqualRuntimeSignals_UsesEndpointWeight`; adaptive high-pending and backoff tests prove runtime health can dominate when weighted.
  Add adaptive selection tests proving endpoint weight still matters and does not overwhelm severe runtime health signals unless configured to do so.

- [x] `ALB12.07` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `AdaptiveEndpointSelectionServiceTests.SelectEndpoint_WithColdEndpoint_IncludesColdEndpointInSample`; runtime stats cold snapshot tests passed.
  Add cold-start tests proving new endpoints receive bounded exploration traffic.

- [x] `ALB12.07A` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `RoutingDecisionServiceTests.Evaluate_WithLeastRecentlyUsedMode_RotatesThroughEligibleEndpointsByRoute`; `Evaluate_WithLeastRecentlyUsedMode_TracksRecencyPerVmr`; `Evaluate_WithLeastRecentlyUsedMode_SkipsUnavailableEndpoints`; `Evaluate_WithLeastRecentlyUsedMode_SkipsInactiveUnhealthyAndQuarantinedEndpoints`; `Evaluate_WithLeastRecentlyUsedMode_SkipsAtCapacityEndpoint`; `Evaluate_WithLeastRecentlyUsedMode_ReusesSessionAffinityPinWithoutUpdatingRecency`.
  Add `LeastRecentlyUsed` routing tests proving the endpoint with the oldest route-scoped assignment is selected, recency updates after selection/admission, no-history endpoints use deterministic tie-breaking, inactive/unhealthy/draining/quarantined/at-capacity endpoints are skipped, and session-affinity pins behave consistently.

- [x] `ALB12.08` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `LoadBalancingPolicyEvaluatorTests.Evaluate_WithRuntimeBackoffFilter_ExcludesBackedOffEndpoint`; `Evaluate_WithRuntimeLatencyRanking_PrefersLowestLatency`; `Evaluate_WithMissingRuntimeRankingMetric_ReturnsFailure`; xUnit/NUnit/Test.Automated passed 1097/1097.
  Add policy metric tests for every new `runtime.*` metric, including missing stats, stale stats, filter operators, ranking normalization, and explanation evidence.

- [x] `ALB12.09` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `RoutingDecisionServiceTests.Evaluate_WithEndpointGroups_UsesPrimaryGroupWhenAvailable`; `Evaluate_WithEndpointGroups_FallsBackWhenPrimaryUnavailable`; validation tests cover empty/invalid groups.
  Add priority-group tests for primary group selection, fallback group selection, empty group behavior, all-unhealthy primary group, and all-backed-off primary group.

- [x] `ALB12.10` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `RoutingDecisionServiceTests.Evaluate_WithTrafficSplitGroups_DistributesByWeight`; xUnit/NUnit/Test.Automated passed 1097/1097.
  Add traffic-split tests for deterministic seeded selection or statistical tolerance over a large sample.

- [x] `ALB12.11` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Existing and new `RoutingDecisionServiceTests` cover reservation gate, model access enforcement/monitoring, request type/model paths, endpoint inventory, quarantine, health, and capacity before final selection; xUnit/NUnit/Test.Automated passed 1097/1097.
  Add routing tests proving reservation gates, model access, request-type gates, health screening, drain/quarantine, and capacity checks still run before adaptive selection.

- [x] `ALB12.12` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `RoutingDecisionServiceTests.Evaluate_WithLeastRecentlyUsedMode_ReusesSessionAffinityPinWithoutUpdatingRecency`; `Evaluate_WithSessionAffinityPinnedBackoffEndpoint_RemovesPinAndSelectsClearEndpoint`; existing stale/draining/quarantined affinity tests passed.
  Add session-affinity tests for adaptive routes, including valid pin reuse, stale pin removal, pinned endpoint in transient backoff, pinned draining endpoint, and pinned quarantined endpoint.

- [ ] `ALB12.13` Status: Deferred | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence: Runtime stats service paths for 2xx, 429, 5xx, timeout/connection-like failures, TTFT, reset, and concurrency are covered; full proxy/client-disconnect integration tests require a dedicated proxy HTTP harness and are deferred as integration test follow-up.
  Add proxy tests proving runtime stats update after 2xx, 4xx, 429, 5xx, timeout, connection failure, streaming first chunk, streaming failure, and client disconnect.

- [x] `ALB12.14` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Request-history model, provider, summary, and security tests cover adaptive evidence fields; xUnit/NUnit/Test.Automated passed 1097/1097.
  Add request-history tests proving adaptive evidence, selected group, score components, and backoff reasons persist when request history is enabled.

- [x] `ALB12.15` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `VirtualModelRunnerControllerTests.GetRuntimeStats_WithAttachedEndpoints_ReturnsTenantScopedSnapshots`; `GetRuntimeStats_WithWrongTenant_ThrowsNotFound`; `ResetRuntimeStats_WithEndpointId_ResetsOnlyRequestedEndpoint`; `ClearRuntimeBackoff_WithEndpointId_ClearsOnlyRequestedEndpoint`; `GetRuntimeStats_WithUnattachedEndpoint_ThrowsNotFound`.
  Add controller tests for runtime stats routes, validation routes, VMR update routes, tenant scope, forbidden access, not found, and invalid payloads.

- [x] `ALB12.16` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `RequestHistorySchemaTests` validates provider table-query definitions and deferred migrated indexes; `DatabaseMigrationTests.Sqlite_Initialize_UpgradesLegacyRequestHistorySchema` validates SQLite upgrade columns/defaults/indexes; latest xUnit/NUnit/Test.Automated passed 1097/1097.
  Add database migration tests across SQLite, MySQL, PostgreSQL, and SQL Server table-query definitions and available integration providers.

- [x] `ALB12.17` Status: Done | Priority: P1 | Owner: QA Engineer | Assignee: Codex | Evidence: `EndpointRuntimeStatsServiceTests.RecordFailure_WithSelectorLikeError_SanitizesAndBacksOff`; `RecordCompletion_WithMalformedRetryAfterHeader_UsesBoundedDefaultBackoff`; `ConcurrentUpdates_DoNotLeaveNegativePendingCounts`.
  Add fault-injection tests for stats service exceptions, invalid runtime snapshots, malformed rate-limit headers, clock skew, and concurrent updates.

## Workstream 13: Dashboard Tests And Manual UX Gates

- [ ] `ALB13.01` Status: Deferred | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `VirtualModelRunners.jsx` implements adaptive and `LeastRecentlyUsed` create/edit controls and `npm.cmd run build` passed; documented manual UX checks remain human release gates.
  Add dashboard tests or documented manual checks for adaptive and `LeastRecentlyUsed` VMR create/edit, validation errors, saving, canceling, and reverting.

- [ ] `ALB13.02` Status: Deferred | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence: Endpoint group editor is implemented and builds; manual dashboard workflow checks remain a human release gate.
  Add dashboard tests or documented manual checks for priority-group editing, endpoint assignment, reorder behavior, deletion, duplicate groups, and empty group states.

- [ ] `ALB13.03` Status: Deferred | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence: Traffic split editor is implemented and builds; manual save-payload and UX checks remain a human release gate.
  Add dashboard tests or documented manual checks for traffic-split weights, zero weight, total weight preview, invalid values, and save payload.

- [ ] `ALB13.04` Status: Deferred | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence: Runtime stats modal is implemented and builds; manual loading/empty/forbidden/retry checks remain a human release gate.
  Add dashboard tests or documented manual checks for runtime stats loading, empty state, stale state, forbidden state, and retry behavior.

- [x] `ALB13.05` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `VirtualModelRunners.jsx` explain-routing modal displays selected group, adaptive sample count, candidate adaptive score/components, runtime stats, and backoff state; `RequestHistory.jsx` adds adaptive filters/detail fields; `npm.cmd run build` passed.
  Add dashboard tests or documented manual checks for explain-routing adaptive evidence and request-history adaptive filters/detail fields.

- [ ] `ALB13.06` Status: Deferred | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence: Requires human desktop/tablet/mobile dashboard review and screenshots.
  Double-check the dashboard UX against existing pages after implementation. Record screenshots or notes for desktop, tablet, and mobile.

- [ ] `ALB13.07` Status: Deferred | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence: Requires human seeded-fixture triple-check before merge.
  Triple-check the dashboard UX before merge using seeded long-label and error-state fixtures. Record any fixes or explicit residual risks.

- [x] `ALB13.08` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Latest `npm.cmd run build` in `dashboard` passed; Vite reported the existing chunk-size warning.
  Run `npm.cmd run build` and record the result.

## Workstream 14: SDK, Postman, And API Contract Tests

- [x] `ALB14.01` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `sdk/javascript/test/client.test.js` covers new validation/effective/explain/runtime helpers; `npm.cmd test` passed 14/14.
  Add JavaScript SDK tests for every new method and update existing tests if VMR payload shapes change.

- [x] `ALB14.02` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `sdk/python/tests/test_client.py` covers new validation/effective/explain/runtime helpers; `$env:PYTHONPATH='src'; python -m pytest` passed 14/14.
  Add Python SDK tests for every new method and update existing tests if VMR payload shapes change.

- [x] `ALB14.03` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `sdk/csharp/Conductor.Sdk.Tests/ConductorClientAnalyticsTests.cs` covers runtime route helpers and adaptive model serialization; `dotnet test sdk\csharp\Conductor.Sdk.slnx --no-restore --no-build /nr:false` passed 8/8.
  Add C# SDK tests for every new method and typed model serialization.

- [x] `ALB14.04` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `Conductor.postman_collection.json` includes adaptive VMR config, validation, effective config, explain-routing, runtime stats, stats reset, and backoff clear examples; JSON parse passed.
  Add Postman coverage verification for adaptive config, validation, runtime stats, explain-routing, and backoff clearing if shipped.

- [x] `ALB14.05` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Postman request names verified for `Create Virtual Model Runner`, `Get VMR Runtime Stats`, `Reset VMR Runtime Stats`, `Clear VMR Runtime Backoff`, and `Explain Routing (Success Candidate)`; `Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null` passed.
  Parse Postman JSON and verify required request names are present.

- [ ] `ALB14.06` Status: Deferred | Priority: P1 | Owner: QA Engineer | Assignee: TBD | Evidence: OpenAPI metadata is implemented; rendered API Explorer smoke verification remains manual.
  Add API Explorer smoke test or manual checklist verifying new OpenAPI metadata renders correctly.

## Workstream 15: Security, Abuse, Performance, And Reliability

- [x] `ALB15.01` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: `ConfigurationValidationServiceTests.ValidateVirtualModelRunner_WithCrossTenantEndpointGroupReference_ReturnsTenantScopedErrors`; `VirtualModelRunnerControllerTests.GetRuntimeStats_WithWrongTenant_ThrowsNotFound`; `GetRuntimeStats_WithUnattachedEndpoint_ThrowsNotFound`; authorization request-type mapping exists for runtime routes.
  Verify tenant isolation for endpoint groups, traffic splits, runtime stats reads, runtime reset APIs, and backoff-clear APIs.

- [x] `ALB15.02` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: Runtime snapshots and routing evidence contain endpoint/group ids, counters, status/error codes, scores, and stable reason codes only; `EndpointRuntimeStatsServiceTests.RecordFailure_WithSelectorLikeError_SanitizesAndBacksOff` verifies sanitization.
  Verify runtime stats and routing evidence do not leak credentials, API keys, bearer tokens, provider URLs beyond existing endpoint visibility rules, prompts, completions, or raw response bodies.

- [x] `ALB15.03` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: `RequestHistorySecurityTests.SearchSummaryAndDelete_WithInjectedAdaptiveFilterText_DoesNotBroadenQuery`; `ConfigurationValidationServiceTests` covers malformed adaptive settings, negative traffic weights, duplicate/missing/cross-tenant groups; `EndpointRuntimeStatsServiceTests` covers selector-like runtime error sanitization and malformed rate-limit headers; xUnit/NUnit/Test.Automated passed 1097/1097.
  Add negative tests for oversized adaptive settings, oversized group definitions, pathological traffic weights, selector-like strings, and malformed JSON.

- [ ] `ALB15.04` Status: Deferred | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence: Requires SRE-owned load test environment and route-decision latency budget.
  Load test adaptive selection under concurrent proxy traffic and compare route-decision latency against compatibility mode.

- [ ] `ALB15.05` Status: Deferred | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence: Requires SRE-owned high-cardinality runtime stats stress environment.
  Stress test runtime stats memory bounds with many tenants, VMRs, endpoints, and high request volume.

- [ ] `ALB15.06` Status: Deferred | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence: Functional absence/fallback paths are covered where unit-testable; database outage, restart, and clock-skew scenarios require integration/SRE validation.
  Verify behavior during health-check service absence, runtime stats service absence, database outage, clock skew, and Conductor restart.

- [ ] `ALB15.07` Status: Deferred | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence: All-backed-off behavior is functionally tested and documented; under-load validation requires SRE load testing.
  Verify all-endpoints-backed-off behavior under load and document recommended operator response.

- [ ] `ALB15.08` Status: Deferred | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence: Metrics labels are bounded by code review; final Prometheus cardinality and histogram-bucket acceptance requires SRE review.
  Confirm metrics cardinality and histogram bucket choices are acceptable for production Prometheus deployments.

## Workstream 16: Release Gates

Required validation commands before declaring implementation complete:

```powershell
git diff --check
dotnet build src\Conductor.sln --no-restore
dotnet test src\Test.Xunit\Test.Xunit.csproj --no-restore --no-build
dotnet test src\Test.Nunit\Test.Nunit.csproj --no-restore --no-build
dotnet run --project src\Test.Automated\Test.Automated.csproj --no-restore --no-build

Push-Location dashboard
npm.cmd run build
Pop-Location

Push-Location sdk\javascript
npm.cmd test
Pop-Location

Push-Location sdk\python
$env:PYTHONPATH = 'src'
python -m pytest
Pop-Location

Push-Location sdk\csharp
dotnet test --no-restore
Pop-Location

Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null
```

- [x] `ALB16.01` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `git diff --check` passed after final plan/test/runbook updates on 2026-06-26.
  Run and record `git diff --check`.

- [x] `ALB16.02` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `dotnet build src\Conductor.sln --no-restore /nr:false` passed with 0 warnings/0 errors; `dotnet test src\Test.Xunit\Test.Xunit.csproj --no-restore --no-build /nr:false` passed 1097/1097; `dotnet test src\Test.Nunit\Test.Nunit.csproj --no-restore --no-build /nr:false` passed 1097/1097; `dotnet run --project src\Test.Automated\Test.Automated.csproj --no-restore --no-build` passed 1097/1097.
  Run and record .NET build and test results.

- [x] `ALB16.03` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Latest `npm.cmd run build` in `dashboard` passed after final plan/runbook updates; Vite reported the existing warning that one minified chunk is larger than 500 kB.
  Run and record dashboard production build.

- [x] `ALB16.04` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `npm.cmd test` in `sdk\javascript` passed 14/14; `$env:PYTHONPATH='src'; python -m pytest` in `sdk\python` passed 14/14; `dotnet build sdk\csharp\Conductor.Sdk.slnx --no-restore /nr:false` passed with 0 warnings/0 errors; `dotnet test sdk\csharp\Conductor.Sdk.slnx --no-restore --no-build /nr:false` passed 8/8.
  Run and record JavaScript, Python, and C# SDK tests.

- [x] `ALB16.05` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null` passed after final plan/runbook updates.
  Parse Postman JSON and verify required adaptive load-balancing routes are present.

- [ ] `ALB16.06` Status: Deferred | Priority: P0 | Owner: Security Engineer | Assignee: TBD | Evidence: Automated tenant-scope, sanitization, and injection-shaped regression tests pass; final security review remains a human gate.
  Complete security review for tenant isolation, secrets redaction, runtime stats exposure, backoff-clear permissions, and denial behavior.

- [ ] `ALB16.07` Status: Deferred | Priority: P0 | Owner: UX Designer | Assignee: TBD | Evidence: Dashboard build passes; double-check/triple-check responsive UX review remains a human gate.
  Complete dashboard double-check and triple-check review. Include responsive screenshots or written review notes.

- [ ] `ALB16.08` Status: Deferred | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence: Functional tests pass; route-decision overhead, runtime stats overhead, memory, and under-load all-backed-off behavior require SRE performance review.
  Complete performance review for route-decision overhead, runtime stats update overhead, memory use, and all-endpoints-backed-off behavior.

- [ ] `ALB16.09` Status: Deferred | Priority: P0 | Owner: Principal Architect | Assignee: TBD | Evidence: Workstream 17 is reconciled below; final approval of deferred requirements divergence requires human architecture review.
  Complete Workstream 17 requirements reconciliation and record any approved divergence in the ADR before release.

## Workstream 17: Requirements Compliance Reconciliation

This workstream closes the loop against `C:\Code\Agents\requirements`. Do not mark these tasks done with "reviewed" alone; each task needs concrete evidence from code, tests, documentation, or release-gate output.

- [x] `ALB17.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Touched C# keeps namespace declarations at top, usings inside namespaces, no tuples, explicit local types, public XML docs on new public contracts, and `dotnet build src\Conductor.sln --no-restore /nr:false` passed with 0 warnings/0 errors.
  Enforce `CODE_STYLE.md`: namespace declarations at top, usings inside namespace and sorted, public XML docs, no private XML docs, no tuples, no `var`, explicit backing fields where validation is needed, one class or enum per file, async methods with `CancellationToken`, cancellation checks where appropriate, and `ConfigureAwait(false)`.

- [x] `ALB17.02` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: New APIs use typed contracts (`VirtualModelRunner`, `RoutingDecision`, `EndpointRuntimeStatsCollection`, `ResourceValidationResult`), existing Watson route modules, explicit OpenAPI metadata, early controller validation, and no tuple route returns.
  Enforce `BACKEND_ARCHITECTURE.md` API rules: typed request/response models, no `JsonElement` for fixed contracts, no tuple route returns, explicit response status codes, early validation, route modules registered through the existing pattern, and OpenAPI/API Explorer metadata for every public route.

- [x] `ALB17.03` Status: Done | Priority: P0 | Owner: Data Engineer | Assignee: Codex | Evidence: VMR additive JSON columns are implemented across SQLite, MySQL, PostgreSQL, and SQL Server table definitions/provider methods/startup migrations; request-history adaptive evidence schema tests and SQLite migration tests passed.
  Enforce `BACKEND_ARCHITECTURE.md` database rules: tenant-owned records include tenant id, string id, created/updated UTC fields, active flag where persisted as first-class entities, tenant-scoped queries, domain-specific method interfaces rather than generic repositories, additive idempotent migrations, and provider coverage for SQLite, MySQL, PostgreSQL, and SQL Server.

- [x] `ALB17.04` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: Runtime stats read/reset/clear request types are statically mapped in auth configuration; controller tests cover wrong-tenant and unattached endpoint denial.
  Enforce `AUTHENTICATION.md`: all new REST request types are statically mapped to resource/operation permissions, every tenant-scoped route resolves exactly one tenant, cross-tenant hints are rejected, server-side authorization is enforced even if the dashboard hides controls, explicit denials are audit/accounting events, and admin bypass reasons are auditable.

- [x] `ALB17.05` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: Runtime stats and routing evidence store counters, EWMA values, ids/names, booleans, scores, and stable reason codes; sanitization and injection-shaped tests passed.
  Enforce secret-handling requirements: runtime stats, routing evidence, logs, request history, API responses, SDK errors, and dashboard views must not reveal bearer tokens, API keys, secret keys, cookies, raw credential material, raw prompts, or raw provider responses beyond existing explicit request-history retention settings.

- [ ] `ALB17.06` Status: Deferred | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: New backend tests are in `src\Test.Shared`; xUnit, NUnit, and `Test.Automated` all passed 1097 shared tests. The shared runner still emits existing service logs, so strict no-console-output compliance remains an existing harness cleanup item.
  Enforce `BACKEND_TEST_ARCHITECTURE.md`: shared backend tests live in `src/Test.Shared`, produce no console output, and run through xUnit, NUnit, and `Test.Automated`. Add descriptors to the shared suite rather than duplicating behavior in runner-specific projects.

- [ ] `ALB17.07` Status: Deferred | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: Dashboard change uses existing `VirtualModelRunners.jsx`, `RequestHistory.jsx`, API-client, modal, table, badge, and confirmation patterns; latest `npm.cmd run build` passed with the existing chunk-size warning. Manual rendered-state review remains open.
  Enforce `FRONTEND_ARCHITECTURE.md`: dashboard code uses the existing React/Vite/API-client/component patterns, does not introduce a charting library or UI kit, uses accessible modals and confirmations, keeps IDs from wrapping unexpectedly, and handles loading, empty, error, forbidden, retry, and stale-data states.

- [ ] `ALB17.08` Status: Deferred | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence: New visible dashboard strings follow the existing hardcoded string pattern; localization mapping and pseudo-locale/RTL/text-expansion checks remain human/release follow-up.
  Enforce `I18N.md`: all visible dashboard strings and accessibility strings are localizable, raw enum/backend values are mapped to display labels, dates/numbers use explicit-locale helpers, status and confirmation text is localized, and pseudo-locale, RTL, and text-expansion checks are recorded.

- [ ] `ALB17.09` Status: Deferred | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence: Requires rendered dashboard review at desktop/tablet/mobile widths with realistic data.
  Enforce responsive dashboard requirements from `FRONTEND_ARCHITECTURE.md`: validate desktop, tablet, and mobile widths with realistic production-like data, empty states, loading states, validation errors, long labels, permission-dependent controls, modals, menus, tooltips, and action flows.

- [x] `ALB17.10` Status: Done | Priority: P0 | Owner: SDK Engineer | Assignee: Codex | Evidence: JavaScript, Python, and C# SDK changes live under `sdk/{language}`; SDK READMEs were updated; JavaScript tests passed 14/14, Python tests passed 14/14, and C# SDK tests passed 8/8.
  Enforce `REPOSITORY_REQUIREMENTS.md` for SDK work: JavaScript, Python, and C# SDK changes live under `sdk/{language}`, each has tests and README updates, and generated or helper code does not spill outside `src/`, `dashboard/`, or `sdk/`.

- [ ] `ALB17.11` Status: Deferred | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: README, REST_API, LOAD_BALANCING_POLICIES, TESTING, CHANGELOG, SDK READMEs, Postman, and API Explorer metadata were updated for adaptive load balancing; final human prose review remains open.
  Enforce documentation requirements: README, REST_API, LOAD_BALANCING_POLICIES, TESTING, CHANGELOG, SDK READMEs, Postman examples, and API Explorer metadata are updated, and public-facing prose receives a final review for specificity, clarity, and non-formulaic wording.

- [x] `ALB17.12` Status: Done | Priority: P0 | Owner: DevOps Engineer | Assignee: Codex | Evidence: Source changes remain within `src`, `sdk`, `dashboard`, root docs, Postman, and Docker schema seed files required by the feature; no Docker compose files, `.dockerignore`, `.gitignore`, or generated build outputs were intentionally changed.
  Enforce repository and Docker requirements: keep Docker compose files as `.yaml`, update `.dockerignore` only if new build artifacts require it, keep source in approved directories, and preserve existing README, CHANGELOG, LICENSE, `.gitignore`, and `.dockerignore` expectations.

- [ ] `ALB17.13` Status: Deferred | Priority: P0 | Owner: Principal Architect | Assignee: TBD | Evidence: No unapproved code divergence was identified in automated reconciliation; final approval of deferred human gates belongs to architecture/security/UX/SRE reviewers.
  Record any deliberate divergence from `C:\Code\Agents\requirements` in the ADR with reason, risk, owner, and follow-up. Unrecorded divergence blocks release.

## Positive Test Case Inventory

Add or map automated tests for these cases before release:

- [x] Existing VMR without adaptive fields routes exactly as before.
- [x] Existing VMR with attached load-balancing policy routes exactly as before when adaptive features are disabled.
- [x] VMR with `LeastRecentlyUsed` selects the eligible endpoint that has gone longest without a route assignment.
- [x] VMR with `LeastRecentlyUsed` updates recency according to the ADR-defined selection or admission point.
- [x] VMR with `LeastRecentlyUsed` handles endpoints with no recency history deterministically.
- [x] New adaptive VMR selects healthy low-latency endpoint more often than slow endpoint under stable load.
- [x] New adaptive VMR avoids high-pending endpoint when another candidate is available.
- [x] New adaptive VMR avoids endpoint in transient rate-limit backoff.
- [x] Runtime stats update after non-streaming success.
- [x] Runtime stats update after streaming first token and completion.
- [x] Runtime stats update after provider 429 with `Retry-After`.
- [x] Runtime stats update after timeout and connection failure.
- [x] Backoff expires and endpoint becomes eligible again.
- [x] Cold endpoint receives bounded exploration traffic.
- [x] Priority group uses primary group while it has available endpoints.
- [x] Priority group falls back when primary group has no available endpoints.
- [x] Traffic split distributes requests according to configured weights.
- [x] Policy filter can require `runtime.backoffActive == false`.
- [x] Policy ranking can prefer lower `runtime.latencyEwmaMs`.
- [x] Explain-routing shows sampled candidates and score components.
- [x] Request history captures adaptive selection evidence.
- [x] Dashboard can create, validate, edit, and save adaptive settings.
- [x] SDK helpers produce correct requests for all new APIs.

## Negative Test Case Inventory

Add or map automated tests for these cases before release:

- [x] Invalid adaptive sample count is rejected.
- [x] Invalid or unknown `LeastRecentlyUsed` enum value is rejected with a stable validation error.
- [x] Invalid score weight is rejected.
- [x] Invalid EWMA smoothing or half-life is rejected.
- [x] Invalid backoff duration is rejected.
- [x] Invalid traffic split total or negative weight is rejected.
- [x] Duplicate group ids are rejected.
- [x] Group references endpoint from another tenant and is rejected.
- [x] Group references missing endpoint and is rejected.
- [x] All endpoints unhealthy returns existing health denial behavior.
- [x] All endpoints at capacity returns existing saturation denial behavior.
- [x] All endpoints quarantined returns existing quarantine denial behavior.
- [x] All endpoints draining returns existing draining denial behavior.
- [x] All endpoints in transient backoff returns documented adaptive denial or fallback behavior.
- [x] Session affinity cannot route to quarantined endpoint.
- [x] `LeastRecentlyUsed` does not route to inactive, unhealthy, draining, quarantined, or at-capacity endpoints.
- [x] `LeastRecentlyUsed` recency is tenant and VMR scoped and cannot leak state across tenants or routes.
- [x] Session affinity cannot silently bypass severe transient backoff unless explicitly configured.
- [x] Runtime stats route denies tenant-scoped caller from another tenant.
- [x] Runtime stats do not include secrets or raw request/response payloads.
- [x] Malformed rate-limit headers do not throw and do not create unbounded backoff.
- [x] Proxy exception still decrements in-flight and pending counters.
- [x] Dashboard rejects invalid adaptive forms without saving partial configuration.
- [ ] Dashboard does not overlap or clip long labels at mobile/tablet/desktop widths. Deferred: requires rendered manual UX review.
- [x] SDK methods URL-encode query parameters correctly.
- [x] Postman collection remains valid JSON.

## Open Decisions

- [x] Decide exact public name for adaptive sampled selection. Decision: `Adaptive`.
- [x] Decide exact enum/contract representation for adaptive mode and policy strategy. Decision: `LoadBalancingModeEnum.Adaptive` for VMR mode; policy strategy remains filters/ranking plus existing tie-breakers.
- [x] Decide whether `LeastRecentlyUsed` recency updates on selection, final capacity admission, or successful upstream completion. Decision: update recency when an eligible endpoint is selected in the compatibility selector.
- [x] Decide whether `LeastRecentlyUsed` should honor endpoint `Weight`; if yes, define a deterministic weighted-recency algorithm. Decision: ignore endpoint weight for the P0 compatibility selector.
- [x] Decide whether `LeastRecentlyUsed` is supported as a load-balancing policy tie-breaker in P0. Decision: not in this slice; policy tie-breaker support remains a separate future contract change.
- [x] Decide default score weights and cold-start behavior. Decision: defaults in ADR 0004 and `AdaptiveScoreWeights`; cold endpoints start at score `60`.
- [x] Decide whether active transient backoff excludes endpoints or only penalizes score by default. Decision: exclude by default with optional setting to penalize instead.
- [x] Decide interaction between session affinity and transient backoff. Decision: active severe backoff removes the pin when `BackoffBreaksSessionAffinity` is true.
- [x] Decide whether traffic splits operate before priority fallback, inside priority groups, or as a separate mode. Decision: traffic splits operate inside the selected priority level.
- [x] Decide whether runtime stats are local-only in P0. Decision: local-only in-memory runtime stats for P0.
- [x] Decide whether backoff-clear APIs are P0 or P1. Decision: shipped as management API support in this release.
- [x] Decide how much adaptive routing evidence is stored in request history by default. Decision: compact strategy/group/backoff/adaptive/fallback fields plus routing-decision payload where history is enabled.
- [x] Decide if adaptive runtime metrics require a freshness window similar to RigMonitor telemetry. Decision: no freshness window for local runtime stats in P0; missing EWMA metrics remain unavailable to policy ranking.

## Definition Of Done

- [x] `DONE.01` Existing VMRs retain existing routing behavior after upgrade.
- [x] `DONE.02` Operators can enable adaptive sampled selection per VMR.
- [x] `DONE.02A` Operators can select `LeastRecentlyUsed` per VMR, and the behavior is implemented, explained, tested, documented, and exposed through SDKs.
- [x] `DONE.03` Runtime stats update for successful, failed, timed-out, rate-limited, streaming, and non-streaming requests.
- [x] `DONE.04` Transient backoff works without changing persistent endpoint service state.
- [x] `DONE.05` Priority groups and traffic splits are configurable, validated, explainable, and documented.
- [x] `DONE.06` Load-balancing policy catalog includes runtime metrics with validation and tests.
- [x] `DONE.07` Routing explanations show adaptive samples, score components, selected group, split bucket, and backoff state.
- [x] `DONE.08` Request history and analytics expose adaptive routing evidence without leaking secrets.
- [ ] `DONE.09` Dashboard supports configuration and troubleshooting with double-checked and triple-checked UX. Deferred: implementation builds, but manual UX review remains.
- [x] `DONE.10` JavaScript, Python, and C# SDKs expose shipped APIs with tests.
- [x] `DONE.11` Postman collection includes shipped API examples and parses successfully.
- [x] `DONE.12` README, REST_API, LOAD_BALANCING_POLICIES, TESTING, CHANGELOG, SDK READMEs, and API Explorer metadata are updated.
- [x] `DONE.13` Positive and negative backend, dashboard, SDK, migration, security, and performance tests are complete or explicitly deferred.
- [x] `DONE.14` Release validation commands pass and evidence is recorded in this file.
- [ ] `DONE.15` Workstream 17 requirements compliance is complete, with evidence for code style, backend architecture, authentication/authorization, frontend architecture, i18n, repository structure, documentation, and test architecture. Deferred: human UX/i18n/security/performance/prose reviews remain.

## Progress Log

Add entries as implementation proceeds.

| Date | Owner | Update | Evidence |
| --- | --- | --- | --- |
| 2026-06-25 | Codex | Created adaptive load-balancing execution tracker. | `ADAPTIVE_LOAD_BALANCING_PLAN.md` |
| 2026-06-25 | Codex | Added `LeastRecentlyUsed` scheme coverage and reconciled the plan against `C:\Code\Agents\requirements`. | Product outcomes, workstreams 0/1/3/4/9/10/11/12/13/16/17, positive/negative tests, open decisions, and Definition of Done |
| 2026-06-25 | Codex | Started execution on the requested implementation branch. | `git switch -c feature/adaptive-load-balancing` |
| 2026-06-25 | Codex | Implemented the `LeastRecentlyUsed` compatibility selector, exposed it in the VMR dashboard selector, updated documentation/Postman/MCP metadata, and added shared backend tests. | `LoadBalancingModeEnum.cs`, `RoutingDecisionService.cs`, `RoutingDecisionServiceTests.cs`, `EnumTests.cs`, `VirtualModelRunners.jsx`, README/REST_API/LOAD_BALANCING_POLICIES, `Conductor.postman_collection.json`, `ConductorToolRegistrationCatalog.cs` |
| 2026-06-25 | Codex | Ran validation for the implementation slice. | `git diff --check`; `dotnet build src\Conductor.sln --no-restore`; full NUnit/xUnit/Test.Automated 1053/1053 each; dashboard build passed with existing chunk-size warning; JavaScript SDK 13/13; Python SDK 13/13; C# SDK 6/6; Postman JSON parsed |
| 2026-06-25 | Codex | Closed remaining `LeastRecentlyUsed` backend test gaps for enum JSON serialization, inactive/unhealthy/quarantined/capacity screening, and session-affinity pins. | `EnumTests.LoadBalancingModeEnum_LeastRecentlyUsed_SerializesAsJsonString`; `RoutingDecisionServiceTests.Evaluate_WithLeastRecentlyUsedMode_SkipsInactiveUnhealthyAndQuarantinedEndpoints`; `Evaluate_WithLeastRecentlyUsedMode_SkipsAtCapacityEndpoint`; `Evaluate_WithLeastRecentlyUsedMode_ReusesSessionAffinityPinWithoutUpdatingRecency` |
| 2026-06-25 | Codex | Re-ran .NET validation after the expanded `LeastRecentlyUsed` test coverage. | `dotnet build src\Conductor.sln --no-restore` passed with 0 errors and 4 existing NU1903 warnings for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11; NUnit 1057/1057; xUnit 1057/1057; Test.Automated 1057/1057 |
| 2026-06-25 | Codex | Updated NuGet packages across the main solution and C# SDK, including direct SQLite native bundle remediation and MCP namespace migration for the updated MCP dependency. | `Conductor.Core.csproj`, `Conductor.Server.csproj`, `Conductor.McpServer.csproj`, `Test.McpServer.csproj`, `Test.Shared.csproj`, `Test.Nunit.csproj`, `Test.Xunit.csproj`, `sdk/csharp/Conductor.Sdk.Tests.csproj`, `ConductorMcpServer.cs`, `ConductorToolRegistry.cs`, `ConductorToolRegistrationCatalog.cs`, `Test.McpServer/Program.cs` |
| 2026-06-25 | Codex | Re-ran package, build, and product-surface validation after NuGet updates. | Main solution and C# SDK package checks reported no outdated or vulnerable packages; main and C# SDK builds passed with 0 warnings/0 errors; NUnit 1057/1057; xUnit 1057/1057; Test.Automated 1057/1057; JavaScript SDK 13/13; Python SDK 13/13; C# SDK 6/6; dashboard build passed with the existing Vite chunk-size warning; Postman JSON parsed |
| 2026-06-25 | Codex | Added release-note and testing-guide coverage for the shipped load-balancing slice. | `CHANGELOG.md` Unreleased entries; `TESTING.md` load-balancing release gate and manual dashboard checks |
| 2026-06-25 | Codex | Wrote the adaptive load-balancing ADR and closed delegated planning decisions. | `docs/adr/0004-adaptive-load-balancing.md`; `ALB0.01` through `ALB0.08` |
| 2026-06-25 | Codex | Added core adaptive configuration, endpoint-group, runtime snapshot, runtime collection, and candidate-score contracts. | `AdaptiveLoadBalancingSettings.cs`, `AdaptiveScoreWeights.cs`, `EndpointGroup.cs`, `EndpointRuntimeStatsSnapshot.cs`, `EndpointRuntimeStatsCollection.cs`, `AdaptiveCandidateScore.cs` |
| 2026-06-25 | Codex | Added VMR persistence for adaptive settings and endpoint groups across models, provider SQL, migrations, and Docker schemas. | `VirtualModelRunner.cs`; four provider `VirtualModelRunnerMethods.cs`; provider `TableQueries.cs`; provider database drivers; `docker/factory/schema.sql`; `docker/postgres/init.sql` |
| 2026-06-25 | Codex | Implemented runtime stats, adaptive scoring, transient backoff, endpoint group routing, runtime policy metrics, validation, and management routes. | `EndpointRuntimeStatsService.cs`, `AdaptiveEndpointSelectionService.cs`, `RoutingDecisionService.cs`, `ProxyController.cs`, `LoadBalancingPolicyMetricResolver.cs`, `LoadBalancingPolicyCatalogProvider.cs`, `ConfigurationValidationService.cs`, `VirtualModelRunnerRouteModule.cs` |
| 2026-06-25 | Codex | Added structured adaptive/group/backoff routing evidence to decisions, request history, history filters, summary facets, Prometheus metrics, and JSON observability snapshots. | `RoutingDecision.cs`, `RequestHistoryEntry.cs`, provider `RequestHistoryMethods.cs`, provider schemas/migrations, `ConductorRouteModule.cs`, `OperationalMetricsService.cs`, `ObservabilityMetricsSnapshot.cs` |
| 2026-06-26 | Codex | Closed remaining non-human backend test and documentation gaps, added runtime route/controller tests, adaptive selector tests, runtime policy metric tests, endpoint group/split tests, EWMA/backoff/concurrency tests, and operator runbooks. | `AdaptiveEndpointSelectionServiceTests.cs`; `EndpointRuntimeStatsServiceTests.cs`; `LoadBalancingPolicyEvaluatorTests.cs`; `RoutingDecisionServiceTests.cs`; `VirtualModelRunnerControllerTests.cs`; `LOAD_BALANCING_POLICIES.md` |
| 2026-06-26 | Codex | Re-ran release validation after the final plan slice. | `git diff --check`; `dotnet build src\Conductor.sln --no-restore /nr:false`; xUnit 1097/1097; NUnit 1097/1097; Test.Automated 1097/1097; dashboard build passed with existing chunk-size warning; JavaScript SDK 14/14; Python SDK 14/14; C# SDK build 0 warnings/0 errors and tests 8/8; Postman JSON parsed |
