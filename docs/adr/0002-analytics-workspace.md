# ADR 0002: Analytics Workspace

Date: 2026-06-14

Status: Accepted for the first Analytics workspace slice

## Context

Conductor already captured request history and request analytics events, and it exposed request-history analytics endpoints. Operators need a broader dashboard workspace that answers operational questions about AI consumption and model-serving behavior: time to first token, token usage, estimate-only cost, user and tenant drill-down, reliability/access outcomes, and saved reporting views.

The first release is dashboard-first. It must use the existing request-history capture path, avoid storing raw prompts or completions in analytics records, enforce tenant scope server-side, and keep the API stable enough for SDKs, Postman, and documentation.

## Decision

Add a new Analytics workspace under `/v1.0/analytics`.

The existing `/v1.0/requesthistory/analytics/*` routes remain backward-compatible request-history analytics endpoints. Dashboard navigation should use `Analytics`, and `/request-analytics` should remain a dashboard redirect or alias for compatibility.

## API Shape

The canonical query endpoint is:

- `POST /v1.0/analytics/query`

Convenience dashboard endpoints are also provided for common views:

- `GET /v1.0/analytics/catalog`
- `GET /v1.0/analytics/summary`
- `GET /v1.0/analytics/timeseries`
- `GET /v1.0/analytics/ttft`
- `GET /v1.0/analytics/tokens`
- `GET /v1.0/analytics/costs`
- `GET /v1.0/analytics/users`
- `GET /v1.0/analytics/access`

The query shape supports:

- named ranges: `lastHour`, `lastDay`, `lastWeek`, `lastMonth`, and `custom`
- custom `StartUtc` and `EndUtc` within the 30-day retained window
- bounded bucket sizes and bucket counts
- grouping by tenant, requested model, effective model, model definition, endpoint, VMR, user, credential, or provider
- filters for tenant, VMR, endpoint, model, user, credential, provider, status class, stage kind, and model-access would-deny state
- optional per-token unit cost and currency/display label for estimate-only cost output

Unsupported metrics, dimensions, status classes, stage kinds, invalid ranges, invalid bucket sizes, invalid limits, negative token unit costs, and unsupported grouping counts are rejected server-side.

## Metric Semantics

Time to first token is defined as:

> Conductor request received to first token received.

Token and estimate-only cost metrics use successful completions only. A successful completion is a request with a recorded HTTP status in the `100` through `399` range.

Failed, denied, rate-limited, and cancelled or incomplete requests are exposed as separate reliability/access counts instead of being mixed into usage and cost totals.

Missing provider token usage is represented as unknown coverage, not zero. Cached and multimodal token fields are nullable until provider parsers persist them.

Cost is estimate-only:

- `EstimatedCost = successful reported total tokens * TokenUnitCost`
- Conductor does not treat this as provider billing reconciliation, formal chargeback, billback, or accounting-grade cost
- first release does not store price books, cost centers, departments, applications, or effective-dated provider rates

## Retention

Analytics queries are limited to the last 30 days. Server-side query normalization clamps retained ranges to that window and caps bucket counts.

Generated export file retention is intentionally separate from analytics data retention. Export jobs and export-file retention are deferred until export APIs are implemented.

## Authorization And Scope

Analytics routes resolve to `RequestTypeEnum.ReadAnalytics`.

For the first implementation, `ReadAnalytics` is authorized for system admins, tenant admins, and tenant users granted the dedicated analytics permission name `analytics.read`. System admins may query global scope or filter to a specific tenant. Tenant-scoped users, including dedicated analytics readers, are forced into their authenticated tenant scope server-side.

Because the user model already persists labels and tags and does not yet have a separate role table, first-release dedicated Analytics access is granted by adding the `analytics.read` user label, or by adding a user tag such as `analytics.read=true` or `permissions=analytics.read`. A future permission/role store may replace this convention without changing the `RequestTypeEnum.ReadAnalytics` route classification.

User and credential drill-downs must remain gated by the same analytics authorization path. Saved reports inherit the same global or tenant scope behavior.

## Saved Reports

Saved reports are in scope for the first release. Scheduled delivery is not.

Saved reports persist:

- owner
- global or tenant scope
- query definition
- display state
- labels
- tags
- created and updated timestamps

Saved reports must not persist raw prompts, completions, request bodies, response bodies, bearer tokens, provider keys, cookies, or secrets. Query models do not include these fields, and free-form saved-report metadata rejects obvious payload or secret field names and secret-bearing values.

## Exports

CSV, JSON, Parquet, PDF, and dashboard-link exports are deferred from the first shipped API slice.

The analytics catalog may advertise export formats as unavailable so clients can render disabled controls or omit export actions. Export APIs must define separate authorization, audit metadata, row caps, format-specific behavior, expiry, and file retention before being enabled.

## Data Access And Rollups

The first release queries existing request-history metadata and analytics summary fields. Provider-neutral database aggregation methods and rollup tables are not required until benchmark evidence shows raw indexed queries are insufficient for last hour, last day, last week, last month, or custom 30-day dashboard workflows.

Provider-specific SQL and rollups remain valid future optimizations, but the API contract should not expose their storage shape.

## Deferred Hardware Scope

GPU and RigMonitor history correlation, capacity planning, Prometheus/OpenTelemetry integration, alerting, and hardware utilization analytics are deferred.

The Analytics workspace is designed so those metrics can later be added to the catalog and grouped with model, endpoint, VMR, tenant, user, and provider dimensions without changing the first-release request analytics semantics.

## Consequences

Operators can answer first-release questions through the dashboard, API, SDKs, and Postman:

- average TTFT by user, endpoint, VMR, model, tenant, credential, or provider
- token usage over time by the same dimensions
- estimate-only user cost over a selected period
- which users, tenants, models, endpoints, VMRs, credentials, or providers drive usage
- which failed, denied, or rate-limited request classes need separate investigation

The design intentionally leaves these items open:

- export APIs and export audit records
- provider-specific analytics SQL and rollups if benchmarks require them
- manual dashboard accessibility and responsive review
- hardware/GPU correlation and external telemetry integration
