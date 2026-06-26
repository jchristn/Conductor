# Adaptive Load Balancing Implementation Plan

Created: 2026-06-25
Overall status: In Progress
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
| Product scope | In Progress | `LeastRecentlyUsed` compatibility mode shipped as the first concrete slice; broader adaptive selection scope remains open. |
| Architecture | Not Started | Requires ADR and final API/data contract decisions before implementation. |
| Backend routing | In Progress | `LeastRecentlyUsed` compatibility mode is implemented in the routing selector; adaptive sampled selection, runtime scoring, and backoff remain open. |
| Runtime statistics | Not Started | New in-band stats service must be designed and integrated into proxy completion paths. |
| Priority groups and traffic splits | Not Started | Requires VMR contract changes, migrations, dashboard changes, SDK updates, docs, and compatibility handling. |
| Dashboard | In Progress | VMR create/edit mode selector includes `LeastRecentlyUsed`; required double-check and triple-check UX gates remain open. |
| SDKs | Not Started | JavaScript, Python, and C# clients must expose all new public API surfaces. |
| Postman/API Explorer | In Progress | Postman VMR create example includes `LeastRecentlyUsed`; API Explorer metadata and broader adaptive examples remain open. |
| Documentation | In Progress | README, REST_API, LOAD_BALANCING_POLICIES, TESTING, and CHANGELOG describe the `LeastRecentlyUsed` slice; SDK READMEs and API Explorer remain open. |
| Test coverage | In Progress | Backend enum serialization and routing tests cover the `LeastRecentlyUsed` selector path, including route scoping, unavailable endpoints, capacity, and session affinity; broader adaptive, dashboard, SDK, migration, security, and performance tests remain open. |
| Requirements compliance | In Progress | Touched C# follows no-`var` and public XML-doc enum requirements; NuGet package health checks are clean; dashboard i18n and UX validation remain open. |

## Product Outcomes

- [ ] Operators can enable an adaptive load-balancing mode per VMR without changing existing VMR behavior by default.
- [ ] Operators can continue using current `RoundRobin`, `Random`, `FirstAvailable`, and policy-based full ranking unchanged.
- [x] Operators can choose `LeastRecentlyUsed` as a compatibility load-balancing scheme for routes that should spread new assignments toward the endpoint that has gone longest without receiving work.
- [ ] Routing can consider live request feedback such as in-flight count, recent success/failure, recent latency, time to first token, timeout/error state, and provider rate-limit backoff.
- [ ] Routing can temporarily avoid endpoints that are failing, timing out, saturated, or rate-limited without changing persistent operator-managed service state.
- [ ] Routing can prefer higher-priority endpoint groups and fall back to lower-priority groups only when needed.
- [ ] Operators can configure explicit traffic-split groups for canary, migration, A/B, and provider-diversity scenarios.
- [ ] The load-balancing metric catalog exposes runtime metrics so policies can filter and rank with adaptive signals.
- [ ] Every adaptive routing decision is explainable through simulation APIs, persisted request history, and dashboard detail views.
- [ ] Dashboard users can create, edit, validate, inspect, and troubleshoot adaptive routing configuration using UI patterns consistent with the existing admin experience.
- [ ] JavaScript, Python, and C# SDKs expose new management, validation, explanation, and runtime-state routes.
- [ ] README, REST_API, LOAD_BALANCING_POLICIES, TESTING, CHANGELOG, SDK READMEs, and Postman collection stay current.
- [ ] The release includes positive and negative automated tests across backend, dashboard, SDKs, database migrations, API contracts, and operational edge cases.

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

- [ ] `ALB0.01` Status: Not Started | Priority: P0 | Owner: Principal Architect | Assignee: TBD | Evidence:
  Write `docs/adr/NNNN-adaptive-load-balancing.md` covering selection strategy, runtime stats, transient backoff, priority groups, traffic splits, compatibility defaults, dashboard scope, SDK scope, and rollout behavior.

- [ ] `ALB0.02` Status: Not Started | Priority: P0 | Owner: Product Manager | Assignee: TBD | Evidence:
  Confirm first-release scope: adaptive sampled selection, runtime metric catalog additions, transient backoff, priority groups, traffic splits, dashboard controls, SDKs, docs, and Postman.

- [ ] `ALB0.03` Status: Not Started | Priority: P0 | Owner: Principal Architect | Assignee: TBD | Evidence:
  Decide whether adaptive selection is represented as a new `LoadBalancingModeEnum` value, a policy selection strategy, or both. Document compatibility behavior for VMRs with an attached policy.

- [x] `ALB0.03A` Status: Done | Priority: P0 | Owner: Principal Architect | Assignee: Codex | Evidence: `RoutingDecisionService` uses tenant-plus-VMR scoped recency, updates recency at endpoint selection time, ignores endpoint weights for this compatibility selector, uses configured endpoint order for no-history ties, and stores recency in memory so restart resets history.
  Define `LeastRecentlyUsed` semantics before code changes. Specify route-level versus global recency, whether recency updates at selection or admitted-forwarding time, how endpoint weights interact with recency, how ties are broken, and what happens after restart.

- [ ] `ALB0.04` Status: Not Started | Priority: P0 | Owner: Principal Architect | Assignee: TBD | Evidence:
  Define exact runtime score formula, normalization ranges, default weights, cold-start behavior, and bounds. Include examples for healthy, slow, saturated, rate-limited, and unknown-stat endpoints.

- [ ] `ALB0.05` Status: Not Started | Priority: P0 | Owner: SRE | Assignee: TBD | Evidence:
  Define transient backoff triggers and defaults for HTTP 429, `Retry-After`, provider reset headers, 5xx bursts, timeouts, connection failures, and malformed responses.

- [ ] `ALB0.06` Status: Not Started | Priority: P0 | Owner: Product Manager | Assignee: TBD | Evidence:
  Define operator-facing terms for adaptive mode, runtime health, temporary backoff, priority groups, and traffic splits. Ensure wording is generic and product-native.

- [ ] `ALB0.07` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Decide whether runtime stats are local-only, shared through a database, exported through metrics only, or eventually replicated for multi-node Conductor deployments.

- [ ] `ALB0.08` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Define fixture scenarios for healthy heterogeneous endpoints, slow endpoint, saturated endpoint, rate-limited endpoint, failing endpoint, cold endpoint, primary group unavailable, split rollout, session affinity, and long dashboard labels.

## Workstream 1: Core Contracts, Models, Enums, And Settings

- [x] `ALB1.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `src/Conductor.Core/Enums/LoadBalancingModeEnum.cs`; `src/Test.Shared/Core/Enums/EnumTests.cs`.
  Add or update enum contracts for adaptive selection and `LeastRecentlyUsed` while preserving existing `RoundRobin`, `Random`, and `FirstAvailable` numeric values and serialization semantics.

- [ ] `ALB1.02` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add typed adaptive selection settings with fields such as enabled mode, sample count, score weights, EWMA half-life or smoothing factor, cold-start score, pending-request penalty, and backoff behavior.

- [ ] `ALB1.03` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add typed priority-group and traffic-split models for VMR configuration. Include group id, name, priority, active flag, endpoint ids, traffic weight, labels, tags, and metadata if needed.

- [ ] `ALB1.04` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add runtime snapshot models for endpoint stats, including endpoint id/name, tenant id, in-flight count, completed count, success EWMA, error EWMA, latency EWMA, TTFT EWMA, last status, last error code, backoff reason, and backoff expiry.

- [ ] `ALB1.05` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add routing-decision evidence models for sampled candidates, runtime score components, selected group, traffic-split bucket, and transient backoff exclusions.

- [ ] `ALB1.06` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Extend validation result models so dashboard and API clients can show group/split/adaptive validation issues without parsing free-form strings.

- [ ] `ALB1.07` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add XML documentation to every new public model, enum, property, and route response.

- [ ] `ALB1.08` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Ensure JSON defaults are backward compatible. Existing VMR payloads without new adaptive/group fields must deserialize and save without behavior changes.

- [ ] `ALB1.09` Status: Not Started | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add server settings for global adaptive load-balancing defaults and clamps, including max sample count, max backoff duration, max retained runtime entries, and stats cleanup interval.

- [ ] `ALB1.10` Status: In Progress | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RoutingDecisionService` added in-memory tenant-plus-VMR scoped selection sequence tracking for `LeastRecentlyUsed`; durable runtime snapshot models remain open for the broader adaptive release.
  Add route-scoped recency tracking fields to runtime models, such as last selected UTC, last admitted UTC, selection sequence, and VMR id. Keep persisted VMR schemas backward compatible unless the ADR explicitly requires durable recency.

## Workstream 2: Database, Migrations, Backup, And Restore

- [ ] `ALB2.01` Status: Not Started | Priority: P0 | Owner: Data Engineer | Assignee: TBD | Evidence:
  Update VMR persistence schema for priority groups, traffic splits, and adaptive settings across SQLite, MySQL, PostgreSQL, and SQL Server.

- [ ] `ALB2.02` Status: Not Started | Priority: P0 | Owner: Data Engineer | Assignee: TBD | Evidence:
  Add startup migrations for existing databases. Existing `modelrunnerendpointids` must migrate to a default compatibility group without changing routing behavior.

- [ ] `ALB2.03` Status: Not Started | Priority: P0 | Owner: Data Engineer | Assignee: TBD | Evidence:
  Update Docker factory schema and seed data so fresh deployments include the new columns or tables.

- [ ] `ALB2.04` Status: Not Started | Priority: P0 | Owner: Data Engineer | Assignee: TBD | Evidence:
  Update `VirtualModelRunner.FromDataRow` and database provider methods to round-trip new fields.

- [ ] `ALB2.05` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Update backup package models and restore logic to include adaptive settings, endpoint groups, and traffic splits.

- [ ] `ALB2.06` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add restore validation for group endpoint references, duplicate group ids, invalid priorities, invalid traffic weights, and cross-tenant endpoint references.

- [ ] `ALB2.07` Status: Not Started | Priority: P1 | Owner: Data Engineer | Assignee: TBD | Evidence:
  Decide whether runtime stats snapshots need persistence. If yes, add retention, cleanup, tenant scoping, and migration tests.

## Workstream 3: Runtime Endpoint Statistics

- [ ] `ALB3.01` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add `EndpointRuntimeStatsService` with thread-safe per-endpoint stats, bounded memory usage, cleanup, and snapshot APIs.

- [ ] `ALB3.02` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Record request selection, admission, completion, status code, exception type, total duration, upstream header duration, first-token time, response bytes, and token throughput where available.

- [ ] `ALB3.03` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Update stats from `ProxyController` on success, provider non-success status, timeout, connection failure, cancellation, and streaming read failure.

- [ ] `ALB3.04` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Ensure in-flight counters and runtime pending counters are decremented exactly once on every path, including exceptions and client disconnects.

- [ ] `ALB3.05` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Implement EWMA helpers with deterministic tests for smoothing, cold start, reset, and long idle periods.

- [ ] `ALB3.06` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Parse `Retry-After` and common rate-limit reset headers safely. Clamp parsed backoff duration to configured bounds.

- [ ] `ALB3.07` Status: Not Started | Priority: P0 | Owner: Security Engineer | Assignee: TBD | Evidence:
  Redact error messages and header-derived details in runtime snapshots. Do not expose bearer tokens, API keys, provider secrets, raw prompts, or raw responses.

- [ ] `ALB3.08` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Add runtime stats reset APIs for operators and tests, scoped by tenant and endpoint.

- [x] `ALB3.09` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RoutingDecisionService.SelectLeastRecentlyUsedEndpoint`, `MarkLeastRecentlyUsedSelection`, and `BuildLeastRecentlyUsedKey`; recency updates only after an endpoint candidate is selected from already-screened availability.
  Update route-scoped recency when an endpoint is selected or admitted according to the ADR decision. Ensure selection denial paths, final capacity-admission failures, and proxy exceptions do not corrupt recency state.

## Workstream 4: Adaptive Selection And Policy Metric Integration

- [ ] `ALB4.01` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add `AdaptiveEndpointSelectionService` that accepts already-screened candidates and returns a selected endpoint plus structured score evidence.

- [ ] `ALB4.02` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Implement adaptive sampled selection with configurable sample count. Default sample count should be small and bounded.

- [ ] `ALB4.03` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Include endpoint weight, pending work, latency EWMA, TTFT EWMA, success/error EWMA, and transient backoff in score calculation according to ADR decisions.

- [ ] `ALB4.04` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Define and implement cold-start behavior so new or recently resumed endpoints receive controlled traffic without being overloaded or starved.

- [ ] `ALB4.05` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Integrate adaptive selection into `RoutingDecisionService` after existing VMR, reservation, model access, session affinity, service-state, health, and capacity checks.

- [x] `ALB4.05A` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `LoadBalancingModeEnum.LeastRecentlyUsed`; `RoutingDecisionService.SelectEndpointWithWeight(..., routeScopeKey)`; `RoutingDecisionServiceTests.Evaluate_WithLeastRecentlyUsedMode_*`.
  Implement `LeastRecentlyUsed` in the compatibility selector path. It must respect active/service-state/health/capacity screening, work with session-affinity behavior, produce routing evidence, and use deterministic tie-breaking for endpoints with no recency history.

- [ ] `ALB4.06` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Define behavior when a VMR has an active load-balancing policy and adaptive selection is enabled. The implementation must be explicit, test-covered, and documented.

- [ ] `ALB4.07` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Extend the load-balancing metric catalog with runtime metrics such as `runtime.successEwma`, `runtime.errorEwma`, `runtime.latencyEwmaMs`, `runtime.ttftEwmaMs`, `runtime.pendingRequests`, `runtime.backoffActive`, and `runtime.backoffRemainingMs`.

- [ ] `ALB4.08` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Extend load-balancing policy validation to support new runtime metrics and reject invalid filter/ranking combinations.

- [ ] `ALB4.09` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Extend routing explanations so candidate evidence includes sample membership, score components, runtime metric values, and exclusion/backoff reasons.

- [ ] `ALB4.10` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Benchmark selection overhead with 2, 10, 100, and 1000 eligible endpoints and record thresholds in `TESTING.md`.

- [ ] `ALB4.11` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Decide and implement how `LeastRecentlyUsed` acts as a policy tie-breaker if policy support is added. If it is not supported as a policy tie-breaker in P0, validation and docs must clearly reject or omit it.

## Workstream 5: Transient Backoff And Automatic Avoidance

- [ ] `ALB5.01` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add transient endpoint backoff state to runtime stats without changing persisted `EndpointServiceStateEnum`.

- [ ] `ALB5.02` Status: Not Started | Priority: P0 | Owner: SRE | Assignee: TBD | Evidence:
  Implement backoff for HTTP 429 using `Retry-After` and known reset headers where present.

- [ ] `ALB5.03` Status: Not Started | Priority: P0 | Owner: SRE | Assignee: TBD | Evidence:
  Implement configurable backoff for repeated 5xx responses, timeouts, and connection failures.

- [ ] `ALB5.04` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add exponential or stepped backoff with jitter, maximum duration, recovery behavior, and deterministic tests.

- [ ] `ALB5.05` Status: Not Started | Priority: P0 | Owner: Principal Architect | Assignee: TBD | Evidence:
  Define interaction between session affinity and transient backoff. Pinned endpoints should not bypass severe temporary backoff unless explicitly allowed by configuration and documented.

- [ ] `ALB5.06` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add routing denial behavior when all otherwise eligible endpoints are in transient backoff. Document whether the route falls back, waits, or returns 503/429.

- [ ] `ALB5.07` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Add operator APIs to clear transient backoff for an endpoint, VMR, or tenant.

- [ ] `ALB5.08` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Add metrics and dashboard visibility for backoff count, backoff reasons, remaining duration, and recovery events.

## Workstream 6: Priority Groups And Traffic Splits

- [ ] `ALB6.01` Status: Not Started | Priority: P0 | Owner: Principal Architect | Assignee: TBD | Evidence:
  Define endpoint group semantics, including priority ordering, tie behavior, empty group behavior, inactive group behavior, and compatibility fallback.

- [ ] `ALB6.02` Status: Not Started | Priority: P0 | Owner: Principal Architect | Assignee: TBD | Evidence:
  Define traffic-split semantics, including group weights, endpoint weights within groups, zero-weight behavior, and deterministic explanation output.

- [ ] `ALB6.03` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add group selection before endpoint scoring. Highest-priority available groups should be considered before lower-priority groups unless traffic split configuration intentionally spans groups.

- [ ] `ALB6.04` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add explicit fallback behavior when a selected split bucket has no available endpoints.

- [ ] `ALB6.05` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Preserve current `ModelRunnerEndpointIds` behavior by projecting existing endpoint IDs into a default group.

- [ ] `ALB6.06` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add group and split details to routing decisions, request history, and explain-routing APIs.

- [ ] `ALB6.07` Status: Not Started | Priority: P1 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add statistical tests or deterministic seeded tests proving traffic-split distribution is within acceptable tolerance.

## Workstream 7: REST API, Validation Routes, And API Explorer

- [ ] `ALB7.01` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Update VMR create, update, validate, effective-configuration, and explain-routing routes to accept and return adaptive/group/split configuration.

- [ ] `ALB7.02` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add runtime stats routes for listing endpoint runtime snapshots by tenant, VMR, endpoint, and status/backoff filters.

- [ ] `ALB7.03` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add validation coverage for adaptive settings, group definitions, traffic weights, duplicate ids, invalid priorities, unavailable endpoint references, and cross-tenant references.

- [ ] `ALB7.04` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Update `GET /v1.0/loadbalancingpolicies/metrics` to include runtime metrics with clear source, filter/rank support, type, and recommended direction.

- [ ] `ALB7.05` Status: Not Started | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add management APIs to clear runtime stats or transient backoff for authorized operators.

- [ ] `ALB7.06` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Update route modules with OpenAPI/API Explorer metadata for new request/response shapes, query parameters, and error responses.

- [ ] `ALB7.07` Status: Not Started | Priority: P0 | Owner: Security Engineer | Assignee: TBD | Evidence:
  Enforce tenant scope and existing administrator semantics on all new routes. Prove cross-tenant access is denied.

## Workstream 8: Observability, Request History, Analytics, And Metrics

- [ ] `ALB8.01` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add routing decision fields for selected group id/name, selection strategy, sample count, sampled candidates, runtime score, and backoff evidence.

- [ ] `ALB8.02` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Persist compact adaptive-routing evidence in request history detail where request history is enabled.

- [ ] `ALB8.03` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add request-history search and summary filters for selection strategy, endpoint group, backoff reason, policy fallback, and adaptive selection.

- [ ] `ALB8.04` Status: Not Started | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add analytics groupings and reliability views for adaptive mode, endpoint group, backoff reason, rate-limited count, and adaptive-vs-compatibility performance.

- [ ] `ALB8.05` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Add Prometheus metrics for adaptive selections, runtime backoffs, sampled candidate count, selected score, score component distributions, and backoff recovery.

- [ ] `ALB8.06` Status: Not Started | Priority: P0 | Owner: SRE | Assignee: TBD | Evidence:
  Ensure metrics labels avoid unbounded cardinality. Endpoint id, VMR id, tenant id, api family, strategy, and reason code are acceptable; raw model names require explicit review.

- [ ] `ALB8.07` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Add JSON observability snapshot fields for adaptive routing and backoff state.

- [ ] `ALB8.08` Status: Not Started | Priority: P0 | Owner: Security Engineer | Assignee: TBD | Evidence:
  Confirm request history and metrics do not expose provider secrets, authorization headers, raw request bodies, or raw response bodies beyond existing request-history retention settings.

## Workstream 9: Dashboard UX And Frontend Implementation

Dashboard work is release-sensitive. Every task in this workstream must be checked against the existing dashboard style, layout density, component conventions, and responsive behavior. Developers must double-check during implementation and triple-check before merge.

- [ ] `ALB9.01` Status: Not Started | Priority: P0 | Owner: UX Designer | Assignee: TBD | Evidence:
  Produce dashboard UX notes for adaptive settings, priority groups, traffic splits, runtime stats, transient backoff, validation errors, and routing explanations.

- [ ] `ALB9.02` Status: Not Started | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Update `dashboard/src/api/api.js` with new runtime stats, validation, VMR configuration, and backoff-clear API methods.

- [ ] `ALB9.03` Status: In Progress | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `dashboard/src/views/VirtualModelRunners.jsx` exposes `LeastRecentlyUsed` in the existing Load Balancing Mode select; adaptive settings UI remains open.
  Update `VirtualModelRunners.jsx` create/edit flows for adaptive selection and `LeastRecentlyUsed`. Preserve existing default behavior and make opt-in state obvious.

- [ ] `ALB9.04` Status: Not Started | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Add priority-group editor with endpoint assignment, priority ordering, active state, and validation feedback.

- [ ] `ALB9.05` Status: Not Started | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Add traffic-split editor with weighted groups, totals, disabled/zero-weight handling, and clear distribution preview.

- [ ] `ALB9.06` Status: Not Started | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Update load-balancing policy UI to expose runtime metrics in filters and ranking rules, including recommended directions and missing-metric warnings.

- [ ] `ALB9.07` Status: Not Started | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Add runtime stats view for endpoint detail, VMR detail, or a dedicated operational panel. Include success/error EWMA, latency EWMA, TTFT EWMA, pending requests, backoff state, and last error summary.

- [ ] `ALB9.08` Status: Not Started | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Update explain-routing UI to show selected group, split bucket, sampled candidates, runtime score components, exclusion reasons, and transient backoff evidence.

- [ ] `ALB9.09` Status: Not Started | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Update request-history detail and filters to include adaptive routing evidence and backoff reason filters.

- [ ] `ALB9.10` Status: Not Started | Priority: P1 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Add backoff-clear action where appropriate, gated by authorization and confirmation.

- [ ] `ALB9.11` Status: Not Started | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Add loading, empty, forbidden, validation-error, stale-data, retry, and partial-data states for all new dashboard panels.

- [ ] `ALB9.12` Status: Not Started | Priority: P0 | Owner: UX Designer | Assignee: TBD | Evidence:
  Double-check visual consistency with existing pages: spacing, typography, button styles, table density, status indicators, modals, forms, tooltips, and action menus.

- [ ] `ALB9.13` Status: Not Started | Priority: P0 | Owner: UX Designer | Assignee: TBD | Evidence:
  Triple-check dashboard usability before merge using representative fixtures: long endpoint names, long VMR names, many groups, empty stats, all endpoints backed off, and invalid config.

- [ ] `ALB9.14` Status: Not Started | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Verify no text overlaps, no nested cards, no layout shift from dynamic values, no clipped buttons, and no unreadable dense controls at 1440px, 1280px, 768px, and 390px widths.

- [ ] `ALB9.15` Status: Not Started | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence:
  Verify keyboard navigation, focus states, labels, tooltips for icon-only controls, modal focus management, and table/action accessibility.

- [x] `ALB9.16` Status: Done | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: `npm.cmd run build` passed in `dashboard`; Vite reported the existing warning that one minified chunk is larger than 500 kB.
  Run `npm.cmd run build` and record output. Any warnings introduced by this work must be reviewed and either fixed or documented.

## Workstream 10: SDKs

### JavaScript SDK

- [ ] `ALB10.01` Status: Not Started | Priority: P0 | Owner: SDK Engineer | Assignee: TBD | Evidence:
  Add JavaScript methods for reading/updating adaptive VMR config, `LeastRecentlyUsed` mode values, runtime stats, validation, explain-routing fields, and optional backoff-clear routes.

- [ ] `ALB10.02` Status: Not Started | Priority: P0 | Owner: SDK Engineer | Assignee: TBD | Evidence:
  Add JavaScript tests for exact URL, method, query parameters, request bodies, and response pass-through for every new helper.

- [ ] `ALB10.03` Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Assignee: TBD | Evidence:
  Update `sdk/javascript/README.md` with adaptive routing examples, priority groups, traffic splits, validation, and runtime stats.

### Python SDK

- [ ] `ALB10.04` Status: Not Started | Priority: P0 | Owner: SDK Engineer | Assignee: TBD | Evidence:
  Add Python methods for reading/updating adaptive VMR config, `LeastRecentlyUsed` mode values, runtime stats, validation, explain-routing fields, and optional backoff-clear routes.

- [ ] `ALB10.05` Status: Not Started | Priority: P0 | Owner: SDK Engineer | Assignee: TBD | Evidence:
  Add Python tests for exact URL, method, query parameters, request bodies, and response pass-through for every new helper.

- [ ] `ALB10.06` Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Assignee: TBD | Evidence:
  Update `sdk/python/README.md` with adaptive routing examples, priority groups, traffic splits, validation, and runtime stats.

### C# SDK

- [ ] `ALB10.07` Status: Not Started | Priority: P0 | Owner: SDK Engineer | Assignee: TBD | Evidence:
  Add C# SDK methods and typed models for adaptive VMR config, `LeastRecentlyUsed` mode values, runtime stats, validation, explain-routing fields, and optional backoff-clear routes.

- [ ] `ALB10.08` Status: Not Started | Priority: P0 | Owner: SDK Engineer | Assignee: TBD | Evidence:
  Add C# SDK tests for exact URL, method, query parameters, request bodies, typed model serialization, and response handling.

- [ ] `ALB10.09` Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Assignee: TBD | Evidence:
  Update `sdk/csharp/README.md` with adaptive routing examples, priority groups, traffic splits, validation, and runtime stats.

## Workstream 11: Documentation, Changelog, And Postman

- [ ] `ALB11.01` Status: In Progress | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `README.md` documents `LeastRecentlyUsed` behavior; full adaptive overview and workflow remain open.
  Update `README.md` with an adaptive load-balancing overview, `LeastRecentlyUsed` behavior, when to use each scheme, compatibility defaults, dashboard entry points, and high-level workflow.

- [ ] `ALB11.02` Status: In Progress | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `REST_API.md` lists `LeastRecentlyUsed` as a supported VMR `LoadBalancingMode`; new adaptive models/routes remain open.
  Update `REST_API.md` with new models, route bodies, `LeastRecentlyUsed` enum semantics, validation errors, runtime stats, backoff behavior, and examples.

- [ ] `ALB11.03` Status: In Progress | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `LOAD_BALANCING_POLICIES.md` now separates VMR `LoadBalancingMode` values from policy `TieBreaker` values and documents `LeastRecentlyUsed`; runtime metrics and adaptive policy content remain open.
  Update `LOAD_BALANCING_POLICIES.md` with `LeastRecentlyUsed`, runtime metrics, adaptive selection, priority groups, traffic splits, fail-open/fail-closed behavior, and troubleshooting.

- [ ] `ALB11.04` Status: In Progress | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `TESTING.md` now includes load-balancing feature expectations, focused validation commands, shared test locations, and manual VMR create/edit UX checks for `LeastRecentlyUsed`; migration, security, and performance validation remain open for the broader adaptive release.
  Update `TESTING.md` with backend, dashboard, SDK, Postman, migration, security, and performance validation commands.

- [ ] `ALB11.05` Status: In Progress | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `CHANGELOG.md` Unreleased now includes `LeastRecentlyUsed` behavior and .NET package-update notes; broader adaptive runtime/API/SDK entries remain open until those capabilities ship.
  Update `CHANGELOG.md` under Unreleased with server, dashboard, SDK, docs, and Postman changes.

- [ ] `ALB11.06` Status: In Progress | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `Conductor.postman_collection.json` VMR create payload demonstrates `LeastRecentlyUsed`; broader adaptive examples remain open.
  Update `Conductor.postman_collection.json` with folders and examples for adaptive VMR config, validation, runtime stats, explain-routing, and backoff clearing if shipped.

- [x] `ALB11.07` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null` passed on 2026-06-25.
  Validate Postman JSON with `Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null`.

- [ ] `ALB11.08` Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Assignee: TBD | Evidence:
  Update API Explorer/OpenAPI metadata and verify all new fields and routes render with useful names and examples.

- [ ] `ALB11.09` Status: Not Started | Priority: P1 | Owner: Documentation Engineer | Assignee: TBD | Evidence:
  Add operator runbooks for rate-limit backoff, all endpoints backed off, adaptive mode rollout, canary split rollout, priority-group fallback, and reverting to compatibility mode.

## Workstream 12: Backend Tests

- [ ] `ALB12.01` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add model tests for adaptive settings defaults, clamping, serialization, deserialization, and backward-compatible missing fields.

- [x] `ALB12.01A` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `EnumTests.LoadBalancingModeEnum_LeastRecentlyUsed_HasValue3`; `EnumTests.LoadBalancingModeEnum_CanParse`; `EnumTests.LoadBalancingModeEnum_LeastRecentlyUsed_SerializesAsJsonString`.
  Add enum and serialization tests for `LeastRecentlyUsed`. Verify existing enum numeric values and JSON strings remain backward compatible.

- [ ] `ALB12.02` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add validation tests for invalid sample counts, negative weights, invalid EWMA factors, invalid backoff durations, duplicate group ids, empty groups, invalid traffic weights, and cross-tenant endpoint references.

- [ ] `ALB12.03` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add runtime stats service tests for success, failure, timeout, 429, 5xx, streaming TTFT, non-streaming TTFT, cancellation, cleanup, and reset.

- [ ] `ALB12.04` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add EWMA tests for convergence, decay, cold start, reset, outlier handling, and no-data behavior.

- [ ] `ALB12.05` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add adaptive selection tests proving high-pending, high-latency, high-error, and active-backoff endpoints are deprioritized or excluded according to configuration.

- [ ] `ALB12.06` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add adaptive selection tests proving endpoint weight still matters and does not overwhelm severe runtime health signals unless configured to do so.

- [ ] `ALB12.07` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add cold-start tests proving new endpoints receive bounded exploration traffic.

- [x] `ALB12.07A` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `RoutingDecisionServiceTests.Evaluate_WithLeastRecentlyUsedMode_RotatesThroughEligibleEndpointsByRoute`; `Evaluate_WithLeastRecentlyUsedMode_TracksRecencyPerVmr`; `Evaluate_WithLeastRecentlyUsedMode_SkipsUnavailableEndpoints`; `Evaluate_WithLeastRecentlyUsedMode_SkipsInactiveUnhealthyAndQuarantinedEndpoints`; `Evaluate_WithLeastRecentlyUsedMode_SkipsAtCapacityEndpoint`; `Evaluate_WithLeastRecentlyUsedMode_ReusesSessionAffinityPinWithoutUpdatingRecency`.
  Add `LeastRecentlyUsed` routing tests proving the endpoint with the oldest route-scoped assignment is selected, recency updates after selection/admission, no-history endpoints use deterministic tie-breaking, inactive/unhealthy/draining/quarantined/at-capacity endpoints are skipped, and session-affinity pins behave consistently.

- [ ] `ALB12.08` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add policy metric tests for every new `runtime.*` metric, including missing stats, stale stats, filter operators, ranking normalization, and explanation evidence.

- [ ] `ALB12.09` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add priority-group tests for primary group selection, fallback group selection, empty group behavior, all-unhealthy primary group, and all-backed-off primary group.

- [ ] `ALB12.10` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add traffic-split tests for deterministic seeded selection or statistical tolerance over a large sample.

- [ ] `ALB12.11` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add routing tests proving reservation gates, model access, request-type gates, health screening, drain/quarantine, and capacity checks still run before adaptive selection.

- [ ] `ALB12.12` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add session-affinity tests for adaptive routes, including valid pin reuse, stale pin removal, pinned endpoint in transient backoff, pinned draining endpoint, and pinned quarantined endpoint.

- [ ] `ALB12.13` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add proxy tests proving runtime stats update after 2xx, 4xx, 429, 5xx, timeout, connection failure, streaming first chunk, streaming failure, and client disconnect.

- [ ] `ALB12.14` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add request-history tests proving adaptive evidence, selected group, score components, and backoff reasons persist when request history is enabled.

- [ ] `ALB12.15` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add controller tests for runtime stats routes, validation routes, VMR update routes, tenant scope, forbidden access, not found, and invalid payloads.

- [ ] `ALB12.16` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add database migration tests across SQLite, MySQL, PostgreSQL, and SQL Server table-query definitions and available integration providers.

- [ ] `ALB12.17` Status: Not Started | Priority: P1 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add fault-injection tests for stats service exceptions, invalid runtime snapshots, malformed rate-limit headers, clock skew, and concurrent updates.

## Workstream 13: Dashboard Tests And Manual UX Gates

- [ ] `ALB13.01` Status: In Progress | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `dashboard/src/views/VirtualModelRunners.jsx` exposes the value in the create/edit select; dashboard build and manual UX checks remain open.
  Add dashboard tests or documented manual checks for adaptive and `LeastRecentlyUsed` VMR create/edit, validation errors, saving, canceling, and reverting.

- [ ] `ALB13.02` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add dashboard tests or documented manual checks for priority-group editing, endpoint assignment, reorder behavior, deletion, duplicate groups, and empty group states.

- [ ] `ALB13.03` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add dashboard tests or documented manual checks for traffic-split weights, zero weight, total weight preview, invalid values, and save payload.

- [ ] `ALB13.04` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add dashboard tests or documented manual checks for runtime stats loading, empty state, stale state, forbidden state, and retry behavior.

- [ ] `ALB13.05` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add dashboard tests or documented manual checks for explain-routing adaptive evidence and request-history adaptive filters/detail fields.

- [ ] `ALB13.06` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Double-check the dashboard UX against existing pages after implementation. Record screenshots or notes for desktop, tablet, and mobile.

- [ ] `ALB13.07` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Triple-check the dashboard UX before merge using seeded long-label and error-state fixtures. Record any fixes or explicit residual risks.

- [ ] `ALB13.08` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Run `npm.cmd run build` and record the result.

## Workstream 14: SDK, Postman, And API Contract Tests

- [ ] `ALB14.01` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add JavaScript SDK tests for every new method and update existing tests if VMR payload shapes change.

- [ ] `ALB14.02` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add Python SDK tests for every new method and update existing tests if VMR payload shapes change.

- [ ] `ALB14.03` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add C# SDK tests for every new method and typed model serialization.

- [ ] `ALB14.04` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add Postman coverage verification for adaptive config, validation, runtime stats, explain-routing, and backoff clearing if shipped.

- [ ] `ALB14.05` Status: In Progress | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null` passed; required adaptive route-name verification remains open until those routes are implemented.
  Parse Postman JSON and verify required request names are present.

- [ ] `ALB14.06` Status: Not Started | Priority: P1 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Add API Explorer smoke test or manual checklist verifying new OpenAPI metadata renders correctly.

## Workstream 15: Security, Abuse, Performance, And Reliability

- [ ] `ALB15.01` Status: Not Started | Priority: P0 | Owner: Security Engineer | Assignee: TBD | Evidence:
  Verify tenant isolation for endpoint groups, traffic splits, runtime stats reads, runtime reset APIs, and backoff-clear APIs.

- [ ] `ALB15.02` Status: Not Started | Priority: P0 | Owner: Security Engineer | Assignee: TBD | Evidence:
  Verify runtime stats and routing evidence do not leak credentials, API keys, bearer tokens, provider URLs beyond existing endpoint visibility rules, prompts, completions, or raw response bodies.

- [ ] `ALB15.03` Status: Not Started | Priority: P0 | Owner: Security Engineer | Assignee: TBD | Evidence:
  Add negative tests for oversized adaptive settings, oversized group definitions, pathological traffic weights, selector-like strings, and malformed JSON.

- [ ] `ALB15.04` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Load test adaptive selection under concurrent proxy traffic and compare route-decision latency against compatibility mode.

- [ ] `ALB15.05` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Stress test runtime stats memory bounds with many tenants, VMRs, endpoints, and high request volume.

- [ ] `ALB15.06` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Verify behavior during health-check service absence, runtime stats service absence, database outage, clock skew, and Conductor restart.

- [ ] `ALB15.07` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Verify all-endpoints-backed-off behavior under load and document recommended operator response.

- [ ] `ALB15.08` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
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

- [x] `ALB16.01` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `git diff --check` passed on 2026-06-25.
  Run and record `git diff --check`.

- [x] `ALB16.02` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `dotnet restore src\Conductor.sln --disable-parallel` and `dotnet restore sdk\csharp\Conductor.Sdk.slnx` passed; `dotnet list ... package --outdated` and `--vulnerable --include-transitive` reported no updates and no vulnerabilities for both .NET surfaces; `dotnet build src\Conductor.sln --no-restore /nr:false` passed with 0 warnings/0 errors; `dotnet build sdk\csharp\Conductor.Sdk.slnx --no-restore /nr:false` passed with 0 warnings/0 errors; NUnit 1057/1057; xUnit 1057/1057; Test.Automated 1057/1057.
  Run and record .NET build and test results.

- [x] `ALB16.03` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `npm.cmd run build` in `dashboard` passed; Vite reported the existing warning that one minified chunk is larger than 500 kB.
  Run and record dashboard production build.

- [x] `ALB16.04` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `npm.cmd test` in `sdk\javascript` passed 13/13; `python -m pytest` in `sdk\python` passed 13/13; `dotnet test --no-restore` in `sdk\csharp` passed 6/6.
  Run and record JavaScript, Python, and C# SDK tests.

- [ ] `ALB16.05` Status: In Progress | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json` passed and reported `Conductor API` with 20 top-level items; VMR create payload demonstrates `LeastRecentlyUsed`; broader adaptive route examples remain open because those APIs are not implemented yet.
  Parse Postman JSON and verify required adaptive load-balancing routes are present.

- [ ] `ALB16.06` Status: Not Started | Priority: P0 | Owner: Security Engineer | Assignee: TBD | Evidence:
  Complete security review for tenant isolation, secrets redaction, runtime stats exposure, backoff-clear permissions, and denial behavior.

- [ ] `ALB16.07` Status: Not Started | Priority: P0 | Owner: UX Designer | Assignee: TBD | Evidence:
  Complete dashboard double-check and triple-check review. Include responsive screenshots or written review notes.

- [ ] `ALB16.08` Status: Not Started | Priority: P1 | Owner: SRE | Assignee: TBD | Evidence:
  Complete performance review for route-decision overhead, runtime stats update overhead, memory use, and all-endpoints-backed-off behavior.

- [ ] `ALB16.09` Status: Not Started | Priority: P0 | Owner: Principal Architect | Assignee: TBD | Evidence:
  Complete Workstream 17 requirements reconciliation and record any approved divergence in the ADR before release.

## Workstream 17: Requirements Compliance Reconciliation

This workstream closes the loop against `C:\Code\Agents\requirements`. Do not mark these tasks done with "reviewed" alone; each task needs concrete evidence from code, tests, documentation, or release-gate output.

- [ ] `ALB17.01` Status: In Progress | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Touched C# keeps namespace declarations at top, usings inside namespaces, no tuples, no `var` matches in touched C# files, and public XML docs for `LoadBalancingModeEnum.LeastRecentlyUsed`; updated MCP package namespace migration uses explicit `Voltaic.Core` and `Voltaic.Mcp` usings; full release compliance remains open.
  Enforce `CODE_STYLE.md`: namespace declarations at top, usings inside namespace and sorted, public XML docs, no private XML docs, no tuples, no `var`, explicit backing fields where validation is needed, one class or enum per file, async methods with `CancellationToken`, cancellation checks where appropriate, and `ConfigureAwait(false)`.

- [ ] `ALB17.02` Status: Not Started | Priority: P0 | Owner: Software Engineer | Assignee: TBD | Evidence:
  Enforce `BACKEND_ARCHITECTURE.md` API rules: typed request/response models, no `JsonElement` for fixed contracts, no tuple route returns, explicit response status codes, early validation, route modules registered through the existing pattern, and OpenAPI/API Explorer metadata for every public route.

- [ ] `ALB17.03` Status: Not Started | Priority: P0 | Owner: Data Engineer | Assignee: TBD | Evidence:
  Enforce `BACKEND_ARCHITECTURE.md` database rules: tenant-owned records include tenant id, string id, created/updated UTC fields, active flag where persisted as first-class entities, tenant-scoped queries, domain-specific method interfaces rather than generic repositories, additive idempotent migrations, and provider coverage for SQLite, MySQL, PostgreSQL, and SQL Server.

- [ ] `ALB17.04` Status: Not Started | Priority: P0 | Owner: Security Engineer | Assignee: TBD | Evidence:
  Enforce `AUTHENTICATION.md`: all new REST request types are statically mapped to resource/operation permissions, every tenant-scoped route resolves exactly one tenant, cross-tenant hints are rejected, server-side authorization is enforced even if the dashboard hides controls, explicit denials are audit/accounting events, and admin bypass reasons are auditable.

- [ ] `ALB17.05` Status: Not Started | Priority: P0 | Owner: Security Engineer | Assignee: TBD | Evidence:
  Enforce secret-handling requirements: runtime stats, routing evidence, logs, request history, API responses, SDK errors, and dashboard views must not reveal bearer tokens, API keys, secret keys, cookies, raw credential material, raw prompts, or raw provider responses beyond existing explicit request-history retention settings.

- [ ] `ALB17.06` Status: In Progress | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: New backend tests are in `src\Test.Shared`; xUnit, NUnit, and `Test.Automated` all passed 1057 shared tests. The shared runner still emits existing service logs, so the no-console-output requirement remains open for full compliance.
  Enforce `BACKEND_TEST_ARCHITECTURE.md`: shared backend tests live in `src/Test.Shared`, produce no console output, and run through xUnit, NUnit, and `Test.Automated`. Add descriptors to the shared suite rather than duplicating behavior in runner-specific projects.

- [ ] `ALB17.07` Status: In Progress | Priority: P0 | Owner: Frontend Engineer | Assignee: Codex | Evidence: Dashboard change uses the existing `VirtualModelRunners.jsx` select pattern and `npm.cmd run build` passed; broader loading/error/empty-state and new-panel requirements remain open.
  Enforce `FRONTEND_ARCHITECTURE.md`: dashboard code uses the existing React/Vite/API-client/component patterns, does not introduce a charting library or UI kit, uses accessible modals and confirmations, keeps IDs from wrapping unexpectedly, and handles loading, empty, error, forbidden, retry, and stale-data states.

- [ ] `ALB17.08` Status: In Progress | Priority: P0 | Owner: Frontend Engineer | Assignee: TBD | Evidence: New visible label `Least Recently Used` follows the existing hardcoded enum-option pattern; localization mapping and pseudo-locale/RTL/text-expansion checks remain open before release.
  Enforce `I18N.md`: all visible dashboard strings and accessibility strings are localizable, raw enum/backend values are mapped to display labels, dates/numbers use explicit-locale helpers, status and confirmation text is localized, and pseudo-locale, RTL, and text-expansion checks are recorded.

- [ ] `ALB17.09` Status: Not Started | Priority: P0 | Owner: QA Engineer | Assignee: TBD | Evidence:
  Enforce responsive dashboard requirements from `FRONTEND_ARCHITECTURE.md`: validate desktop, tablet, and mobile widths with realistic production-like data, empty states, loading states, validation errors, long labels, permission-dependent controls, modals, menus, tooltips, and action flows.

- [ ] `ALB17.10` Status: In Progress | Priority: P0 | Owner: SDK Engineer | Assignee: Codex | Evidence: C# SDK test packages were updated and `dotnet test --no-restore --no-build` passed 6/6; JavaScript and Python SDK tests passed 13/13 each; no SDK contract code changed for the LRU slice. SDK API expansion remains open for future adaptive/runtime routes.
  Enforce `REPOSITORY_REQUIREMENTS.md` for SDK work: JavaScript, Python, and C# SDK changes live under `sdk/{language}`, each has tests and README updates, and generated or helper code does not spill outside `src/`, `dashboard/`, or `sdk/`.

- [ ] `ALB17.11` Status: In Progress | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: README, REST_API, LOAD_BALANCING_POLICIES, TESTING, CHANGELOG, Postman, and MCP metadata were updated for `LeastRecentlyUsed`; SDK READMEs, API Explorer metadata, and final prose review remain open.
  Enforce documentation requirements: README, REST_API, LOAD_BALANCING_POLICIES, TESTING, CHANGELOG, SDK READMEs, Postman examples, and API Explorer metadata are updated, and public-facing prose receives a final review for specificity, clarity, and non-formulaic wording.

- [ ] `ALB17.12` Status: In Progress | Priority: P0 | Owner: DevOps Engineer | Assignee: Codex | Evidence: NuGet package updates and source changes remain within `src`, `sdk`, `dashboard`, root docs, and Postman; no Docker compose files, `.dockerignore`, `.gitignore`, or generated build outputs were intentionally changed. Full release packaging review remains open.
  Enforce repository and Docker requirements: keep Docker compose files as `.yaml`, update `.dockerignore` only if new build artifacts require it, keep source in approved directories, and preserve existing README, CHANGELOG, LICENSE, `.gitignore`, and `.dockerignore` expectations.

- [ ] `ALB17.13` Status: Not Started | Priority: P0 | Owner: Principal Architect | Assignee: TBD | Evidence:
  Record any deliberate divergence from `C:\Code\Agents\requirements` in the ADR with reason, risk, owner, and follow-up. Unrecorded divergence blocks release.

## Positive Test Case Inventory

Add or map automated tests for these cases before release:

- [x] Existing VMR without adaptive fields routes exactly as before.
- [x] Existing VMR with attached load-balancing policy routes exactly as before when adaptive features are disabled.
- [x] VMR with `LeastRecentlyUsed` selects the eligible endpoint that has gone longest without a route assignment.
- [x] VMR with `LeastRecentlyUsed` updates recency according to the ADR-defined selection or admission point.
- [x] VMR with `LeastRecentlyUsed` handles endpoints with no recency history deterministically.
- [ ] New adaptive VMR selects healthy low-latency endpoint more often than slow endpoint under stable load.
- [ ] New adaptive VMR avoids high-pending endpoint when another candidate is available.
- [ ] New adaptive VMR avoids endpoint in transient rate-limit backoff.
- [ ] Runtime stats update after non-streaming success.
- [ ] Runtime stats update after streaming first token and completion.
- [ ] Runtime stats update after provider 429 with `Retry-After`.
- [ ] Runtime stats update after timeout and connection failure.
- [ ] Backoff expires and endpoint becomes eligible again.
- [ ] Cold endpoint receives bounded exploration traffic.
- [ ] Priority group uses primary group while it has available endpoints.
- [ ] Priority group falls back when primary group has no available endpoints.
- [ ] Traffic split distributes requests according to configured weights.
- [ ] Policy filter can require `runtime.backoffActive == false`.
- [ ] Policy ranking can prefer lower `runtime.latencyEwmaMs`.
- [ ] Explain-routing shows sampled candidates and score components.
- [ ] Request history captures adaptive selection evidence.
- [ ] Dashboard can create, validate, edit, and save adaptive settings.
- [ ] SDK helpers produce correct requests for all new APIs.

## Negative Test Case Inventory

Add or map automated tests for these cases before release:

- [ ] Invalid adaptive sample count is rejected.
- [ ] Invalid or unknown `LeastRecentlyUsed` enum value is rejected with a stable validation error.
- [ ] Invalid score weight is rejected.
- [ ] Invalid EWMA smoothing or half-life is rejected.
- [ ] Invalid backoff duration is rejected.
- [ ] Invalid traffic split total or negative weight is rejected.
- [ ] Duplicate group ids are rejected.
- [ ] Group references endpoint from another tenant and is rejected.
- [ ] Group references missing endpoint and is rejected.
- [ ] All endpoints unhealthy returns existing health denial behavior.
- [ ] All endpoints at capacity returns existing saturation denial behavior.
- [ ] All endpoints quarantined returns existing quarantine denial behavior.
- [ ] All endpoints draining returns existing draining denial behavior.
- [ ] All endpoints in transient backoff returns documented adaptive denial or fallback behavior.
- [ ] Session affinity cannot route to quarantined endpoint.
- [x] `LeastRecentlyUsed` does not route to inactive, unhealthy, draining, quarantined, or at-capacity endpoints.
- [x] `LeastRecentlyUsed` recency is tenant and VMR scoped and cannot leak state across tenants or routes.
- [ ] Session affinity cannot silently bypass severe transient backoff unless explicitly configured.
- [ ] Runtime stats route denies tenant-scoped caller from another tenant.
- [ ] Runtime stats do not include secrets or raw request/response payloads.
- [ ] Malformed rate-limit headers do not throw and do not create unbounded backoff.
- [ ] Proxy exception still decrements in-flight and pending counters.
- [ ] Dashboard rejects invalid adaptive forms without saving partial configuration.
- [ ] Dashboard does not overlap or clip long labels at mobile/tablet/desktop widths.
- [ ] SDK methods URL-encode query parameters correctly.
- [x] Postman collection remains valid JSON.

## Open Decisions

- [ ] Decide exact public name for adaptive sampled selection.
- [ ] Decide exact enum/contract representation for adaptive mode and policy strategy.
- [x] Decide whether `LeastRecentlyUsed` recency updates on selection, final capacity admission, or successful upstream completion. Decision: update recency when an eligible endpoint is selected in the compatibility selector.
- [x] Decide whether `LeastRecentlyUsed` should honor endpoint `Weight`; if yes, define a deterministic weighted-recency algorithm. Decision: ignore endpoint weight for the P0 compatibility selector.
- [x] Decide whether `LeastRecentlyUsed` is supported as a load-balancing policy tie-breaker in P0. Decision: not in this slice; policy tie-breaker support remains a separate future contract change.
- [ ] Decide default score weights and cold-start behavior.
- [ ] Decide whether active transient backoff excludes endpoints or only penalizes score by default.
- [ ] Decide interaction between session affinity and transient backoff.
- [ ] Decide whether traffic splits operate before priority fallback, inside priority groups, or as a separate mode.
- [ ] Decide whether runtime stats are local-only in P0.
- [ ] Decide whether backoff-clear APIs are P0 or P1.
- [ ] Decide how much adaptive routing evidence is stored in request history by default.
- [ ] Decide if adaptive runtime metrics require a freshness window similar to RigMonitor telemetry.

## Definition Of Done

- [ ] `DONE.01` Existing VMRs retain existing routing behavior after upgrade.
- [ ] `DONE.02` Operators can enable adaptive sampled selection per VMR.
- [ ] `DONE.02A` Operators can select `LeastRecentlyUsed` per VMR, and the behavior is implemented, explained, tested, documented, and exposed through SDKs.
- [ ] `DONE.03` Runtime stats update for successful, failed, timed-out, rate-limited, streaming, and non-streaming requests.
- [ ] `DONE.04` Transient backoff works without changing persistent endpoint service state.
- [ ] `DONE.05` Priority groups and traffic splits are configurable, validated, explainable, and documented.
- [ ] `DONE.06` Load-balancing policy catalog includes runtime metrics with validation and tests.
- [ ] `DONE.07` Routing explanations show adaptive samples, score components, selected group, split bucket, and backoff state.
- [ ] `DONE.08` Request history and analytics expose adaptive routing evidence without leaking secrets.
- [ ] `DONE.09` Dashboard supports configuration and troubleshooting with double-checked and triple-checked UX.
- [ ] `DONE.10` JavaScript, Python, and C# SDKs expose shipped APIs with tests.
- [ ] `DONE.11` Postman collection includes shipped API examples and parses successfully.
- [ ] `DONE.12` README, REST_API, LOAD_BALANCING_POLICIES, TESTING, CHANGELOG, SDK READMEs, and API Explorer metadata are updated.
- [ ] `DONE.13` Positive and negative backend, dashboard, SDK, migration, security, and performance tests are complete or explicitly deferred.
- [ ] `DONE.14` Release validation commands pass and evidence is recorded in this file.
- [ ] `DONE.15` Workstream 17 requirements compliance is complete, with evidence for code style, backend architecture, authentication/authorization, frontend architecture, i18n, repository structure, documentation, and test architecture.

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
