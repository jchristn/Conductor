# ADR 0004: Adaptive Load Balancing

Date: 2026-06-25

Status: Accepted for the first adaptive load-balancing slice

## Context

Conductor already supports compatibility routing through VMR load-balancing modes, endpoint health screening, session affinity, and optional load-balancing policies. Operators need a richer opt-in path that can react to live request outcomes without changing the persistent service state of a model runner endpoint.

The feature must preserve existing VMR behavior by default, expose enough evidence for support and troubleshooting, stay tenant-scoped, avoid hot-path database reads during endpoint selection, and keep dashboard, SDK, Postman, and documentation surfaces aligned.

## Decision

Add adaptive load balancing as an explicit VMR-level mode named `Adaptive`.

Existing VMRs continue using `RoundRobin`, `Random`, `FirstAvailable`, `LeastRecentlyUsed`, or an attached policy exactly as before unless the operator opts into `Adaptive` or configures endpoint groups. `LeastRecentlyUsed` remains a compatibility selector and is not a policy tie-breaker in this release.

Adaptive mode uses these layers:

- endpoint inventory from the existing VMR endpoint list
- optional endpoint groups with priority and traffic weight
- existing active, service-state, health, and capacity screening
- existing session affinity, with severe transient backoff allowed to invalidate a stale pin by default
- attached load-balancing policy filters and ranking, when configured
- adaptive sampled selection over the remaining candidates
- in-memory runtime statistics updated from proxy completion, proxy failure, timeout, first-token timing, and rate-limit responses

Runtime statistics are local to the current Conductor server process for the first release. They are exposed through management APIs and metrics, but they are not persisted or shared across nodes. Multi-node sharing and warm restart snapshots are future work.

## Public Contracts

`LoadBalancingModeEnum` adds:

- `Adaptive`: sample eligible endpoints and score candidates using runtime signals.
- `LeastRecentlyUsed`: select the eligible endpoint with the oldest route-scoped assignment.

`VirtualModelRunner` adds:

- `AdaptiveLoadBalancingSettings`
- `EndpointGroups`

These are persisted as JSON columns so the schema remains additive and compatible across database providers. Existing `ModelRunnerEndpointIds` remains the source of compatibility routing. When no endpoint groups are configured, routing builds a default group from `ModelRunnerEndpointIds`.

Runtime APIs are:

- `GET /v1.0/virtualmodelrunners/{id}/runtime-stats`
- `POST /v1.0/virtualmodelrunners/{id}/runtime-stats/reset`
- `POST /v1.0/virtualmodelrunners/{id}/runtime-backoff/clear`

The existing `explain-routing` route includes adaptive evidence when adaptive mode is active.

## Score Formula

Adaptive candidate score is bounded from `0` to `100`.

Default score components:

- success EWMA: `35`
- latency EWMA: `25`
- time-to-first-token EWMA: `15`
- pending/in-flight penalty: `15`
- endpoint configured weight: `10`

Cold endpoints start at `60` so they can receive bounded exploration traffic. Lower latency and TTFT are better. Endpoints with no latency data use the cold-start score for latency components. In-flight and pending work reduce the score. Active transient backoff excludes endpoints by default; if all otherwise-eligible endpoints are backed off, routing fails with a documented adaptive denial rather than silently using a backed-off endpoint.

The default adaptive sample count is `2`. It is clamped between `1` and the number of eligible candidates, with a global maximum of `8`.

## Transient Backoff

Transient backoff is automatic runtime state, separate from operator-managed endpoint service state.

Backoff triggers:

- HTTP `429`, honoring `Retry-After` when present within configured bounds
- upstream `5xx`
- request timeout
- connection failure
- repeated failures that cross the configured consecutive failure threshold

Backoff defaults:

- base duration: `30000` milliseconds
- maximum duration: `300000` milliseconds
- failure threshold: `3`
- `Retry-After` is clamped to the maximum duration
- malformed rate-limit headers are ignored and fall back to base duration

Backoff expiry automatically returns the endpoint to eligibility. Manual clear APIs are tenant-scoped and authorization-protected.

## Priority Groups And Traffic Splits

Endpoint groups are evaluated by ascending priority. Only groups at the best available priority are considered. Within that priority, group traffic weights choose the routing bucket. A group with no active endpoints is ignored. If all endpoints in the selected traffic bucket are later filtered out, routing falls back to another available group at the same priority before failing.

If no groups are configured, Conductor creates an implicit compatibility group containing `ModelRunnerEndpointIds` with priority `0` and traffic weight `100`.

Traffic splits operate inside a priority level. They do not skip a higher-priority group in favor of a lower-priority group while the higher-priority level has any available endpoints.

## Session Affinity

Session affinity is evaluated before adaptive scoring for compatibility. A pinned endpoint is reused only if it remains active, normal, healthy, under capacity, and not in severe active transient backoff. If the pin is invalid, the pin is removed and adaptive routing proceeds normally.

## Policy Interaction

When a VMR has an attached load-balancing policy, the policy runs after group and availability screening. If the policy returns candidates, adaptive scoring operates on the policy survivors only when the VMR mode is `Adaptive`. If the policy cannot produce a candidate and is configured fail-open, routing falls back to the VMR mode.

Runtime metric identifiers are added for policy filters and ranking:

- `runtime.backoffActive`
- `runtime.inFlight`
- `runtime.pending`
- `runtime.successEwma`
- `runtime.errorEwma`
- `runtime.latencyEwmaMs`
- `runtime.ttftEwmaMs`
- `runtime.completedCount`
- `runtime.consecutiveFailures`

## Evidence And History

Routing decisions include:

- selected priority group
- selected split bucket
- sampled candidates
- score components
- runtime statistics used for each candidate
- transient backoff exclusions

Request history stores this evidence through the existing routing-decision payload. Runtime stats APIs and logs must not expose bearer tokens, API keys, cookies, raw prompts, raw request bodies, or raw provider responses.

## Validation

VMR validation rejects:

- invalid sample count
- invalid score weights
- invalid EWMA smoothing
- invalid backoff durations
- duplicate endpoint group ids
- endpoint groups that reference missing or cross-tenant endpoints
- active adaptive configuration with no routable endpoint source
- negative traffic weights

Validation returns structured field-level errors through the existing validation result model.

## Dashboard And SDK Scope

Dashboard users can configure adaptive settings, endpoint groups, and traffic split weights in the VMR create/edit flow, inspect runtime stats, clear backoff, and use explain-routing evidence. UX review for layout, consistency, keyboard access, and responsive behavior is release-blocking.

JavaScript, Python, and C# SDKs expose VMR runtime stats, reset stats, clear backoff, validation, and explain-routing helpers.

## Consequences

Operators get adaptive routing that can avoid overloaded, failing, or rate-limited endpoints without changing endpoint service state. Existing deployments retain compatibility behavior. In-memory runtime state is simple and fast, but multi-node deployments will see node-local adaptive behavior until a future shared state design is implemented.

