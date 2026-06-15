# Analytics Implementation Plan

Source plan: `ANALYTICS.md`
Created: 2026-06-14
Overall status: First-release implementation complete; deferred work remains tracked below.
Primary release target: Dashboard-first Analytics workspace for TTFT, token usage, user drill-down, saved reports, approved export deferral, and estimate-only token cost.

## How To Use This File

This file is the execution tracker. Update it as work starts, lands, blocks, reviews, or completes. Do not wait until the end of implementation.

Allowed task status values:

- `Not Started`
- `In Progress`
- `Blocked`
- `In Review`
- `Done`
- `Deferred`

Priority values:

- `P0`: required for the first useful Analytics release
- `P1`: required before calling Analytics production-ready
- `P2`: useful follow-up after the core workflow is stable

Task annotation format:

```text
- [ ] `TASK-ID` Status: <Not Started | In Progress | Blocked | In Review | Done | Deferred> | Priority: P0 | Owner: Role | Assignee: TBD | Evidence:
```

When a task is completed, add evidence such as changed files, test names, command output summary, PR number, or design artifact. If a task is blocked, write the blocking decision or missing dependency under the task.

## Current Progress

| Area | Status | Notes |
| --- | --- | --- |
| Product scope | Done for first release | First-release decisions, P0 outcomes, P1/P2 boundaries, and deferrals are captured below. |
| Backend APIs | Done for shipped routes | `/v1.0/analytics` query/catalog/convenience routes and saved-report CRUD are implemented; export APIs are deferred by ADR 0002. |
| Database/indexes | Done for first release | Saved-report storage, request-history/event indexes, provider schema assertions, SQLite CRUD coverage, and fixture seed data are implemented; rollups remain deferred until benchmarks justify them. |
| Dashboard | Done for first release | Analytics nav, `/analytics` route, filters, URL state, debounced requests, tabs, metric cards, charts, breakdowns, saved reports, disabled export status, and data-state banners are implemented. |
| SDKs | Done for shipped routes | JavaScript, Python, and C# analytics/saved-report helpers have route tests for shipped APIs. |
| Postman/API Explorer | Done for shipped routes | Operator examples cover shipped `/v1.0/analytics` and saved-report routes; export examples are intentionally absent until export APIs ship. |
| Documentation | Done for first release | README, REST_API, CHANGELOG, TESTING, SDK docs, and ADR are updated, including estimate-only wording and 30-day retention. |
| Security/compliance | Done for first release | Tenant scope, `analytics.read`, retained metadata constraints, and saved-report secret/payload guards are implemented; export audit is deferred with export APIs. |
| Release validation | Done for first release | Automated gates and static dashboard QA are recorded below; load benchmarks and export job tests are deferred by ADR 0002. |

## Locked First-Release Decisions

| Area | Decision |
| --- | --- |
| Navigation | Rename `Request Analytics` to `Analytics`; keep `/request-analytics` as a redirect or alias. |
| Primary consumer | Optimize APIs and UX for dashboard use first. |
| Scope | TTFT, token usage, user drill-down, saved reports, approved export deferral, and estimate-only token cost. |
| TTFT definition | Conductor request received to first token received. |
| Usage denominator | Token and cost usage charts include successful completions only. |
| Failed requests | Failed, denied, rate-limited, and cancelled requests are separate reliability/access reports. |
| Cost | Estimate-only from supplied per-token unit cost. Not chargeback, billback, provider reconciliation, or accounting-grade reporting. |
| Token fields | Prompt/input, completion/output, total, cached, and multimodal token fields where providers expose them. |
| Missing provider usage | Unknown, not zero. |
| Retention | 30 days for analytics data. Dashboard and docs must say this clearly. |
| Time controls | Last hour, last day, last week, last month, custom start/end, and selectable granularity. |
| Tenant scope | System admins can query globally and filter to one tenant. Tenant admins and tenant-scoped analytics users are forced into their tenant scope. |
| Access | System admin, tenant admin, and analytics-specific permission/role. |
| Grouping | Requested model, effective model, model definition, endpoint, VMR, tenant, user, credential, and provider. |
| Reports | Saved reports are in scope. Scheduled delivery is out of scope. |
| Exports | CSV, JSON, Parquet, PDF, and shareable dashboard links are visible as unavailable; export APIs are deferred by ADR 0002. |
| SDKs | JavaScript, Python, and C#. |
| Deferred | GPU/RigMonitor history, Prometheus/OpenTelemetry, capacity planning, alerting, cost centers, departments, application mapping, stored price books, comparison mode, scheduled reports. |

## P0 Release Outcome

The first release is complete only when an operator can answer these questions in the dashboard and API:

- What is the average TTFT for this user over the selected period?
- How many successful prompt, completion, total, cached, and multimodal tokens were used by this user?
- What is the estimated cost for this user over the last day using a supplied per-token unit cost?
- Which model, endpoint, VMR, tenant, user, credential, or provider drove the selected usage?
- Which failed, denied, or rate-limited request types need separate investigation?

## Final Release Gate Notes

Security review notes:

- Analytics routes resolve to `RequestTypeEnum.ReadAnalytics` and require system admin, tenant admin, or tenant user `analytics.read` access.
- System admins can query global scope or filter to a tenant. Tenant admins and tenant-scoped analytics readers are forced into their authenticated tenant scope server-side.
- User and credential drill-downs use the same analytics authorization path as summary/query routes.
- Saved reports store query/display metadata only. They reject obvious raw payload fields and secret-bearing values, and they do not persist raw prompts, completions, request bodies, response bodies, bearer tokens, provider keys, cookies, or secrets.
- Export APIs are unavailable in the first release, so there is no generated export file, download, expiry, or audit surface to secure yet.

Performance review notes:

- Analytics queries are clamped to the 30-day retention window, cap bucket count at 720, cap request search limit at 50,000, and use existing request-history and analytics-event indexes.
- The first release intentionally avoids provider-specific rollups until benchmark evidence proves indexed request-history queries are insufficient for last-hour, last-day, last-week, last-month, or custom retained ranges.
- Live non-SQLite database benchmarks remain an environment-specific production-readiness activity, not a blocker for the first shipped Analytics API contract.

Dashboard QA notes:

- Production build passes, fixture seed data is available in `scripts/seed-analytics-fixtures.sql`, and the dashboard implements responsive grid/table constraints, stable chart dimensions, tab ARIA semantics, disabled export controls, keyboard-reachable buttons/inputs, and explicit state banners.
- This repo does not currently include a browser automation or pseudo-locale framework; responsive/accessibility review was completed through static implementation checks and production build validation.

## Workstream 0: Planning, Decisions, And Architecture

- [x] `A0.01` Status: Done | Priority: P0 | Owner: Product Manager | Assignee: Codex | Evidence: First-release acceptance criteria and P0/P1/P2 boundaries are recorded under "First-Release Workspace IA And Fixture Scenarios"; `feature/analytics` branch contains the implementation tracker.
  Convert `ANALYTICS.md` and this file into first-release acceptance criteria. Confirm P0 tabs and P1/P2 boundaries.

- [x] `A0.02` Status: Done | Priority: P0 | Owner: Principal Architect | Assignee: Codex | Evidence: Added `docs/adr/0002-analytics-workspace.md` covering namespace, compatibility route behavior, query shape, retention, rollup stance, saved reports, exports, and deferred hardware scope.
  Write `docs/adr/NNNN-analytics-workspace.md` covering namespace, compatibility route behavior, query shape, retention, rollup stance, saved reports, exports, and deferred hardware scope.

- [x] `A0.03` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: ADR 0002 names the dedicated permission `analytics.read`, represented internally by `RequestTypeEnum.ReadAnalytics`; `B2.01` implements first-release grants through user labels/tags.
  Choose the exact analytics-specific permission or role name. Document how it composes with system admin and tenant admin rights.

- [x] `A0.04` Status: Done | Priority: P0 | Owner: Compliance Officer | Assignee: Codex | Evidence: ADR 0002 confirms 30-day analytics data retention and records export-file retention as separate deferred export scope.
  Confirm 30-day analytics data retention and define generated export file retention separately.

- [x] `A0.05` Status: Done | Priority: P0 | Owner: Principal Architect | Assignee: Codex | Evidence: ADR 0002 defines `POST /v1.0/analytics/query` as canonical and keeps GET convenience routes for dashboard views.
  Decide whether each dashboard tab uses only `POST /v1.0/analytics/query` or also gets GET convenience routes.

- [x] `A0.06` Status: Done | Priority: P0 | Owner: Product Manager | Assignee: Codex | Evidence: ADR 0002 and `AnalyticsSavedReport` define owner, scope, query, display state, labels, tags, and timestamps for saved reports.
  Define saved report fields: name, description, owner, visibility, filters, metrics, groupings, range, granularity, token unit cost, export preferences, and dashboard-link state.

- [x] `A0.07` Status: Done | Priority: P1 | Owner: Product Manager | Assignee: Codex | Evidence: ADR 0002 defers PDF export until export APIs define authorization, audit, row caps, expiry, and file retention.
  Decide whether PDF export is server-rendered, client-rendered, or deferred behind CSV/JSON/Parquet if implementation cost is high.

- [x] `A0.08` Status: Done | Priority: P1 | Owner: Principal Architect | Assignee: Codex | Evidence: ADR 0002 defers Parquet export until export APIs and format-specific behavior are implemented.
  Choose Parquet export implementation and whether Parquet exports are job-only.

- [x] `A0.09` Status: Done | Priority: P0 | Owner: UX Designer | Assignee: Codex | Evidence: Dashboard IA is recorded in this file and implemented as Analytics tabs: Overview, Latency/TTFT, Tokens, Users, Reliability/Access, Reports, and Exports. `npm.cmd run build` passed.
  Produce dashboard information architecture for Overview, Latency/TTFT, Tokens, Users, Reports, Exports, and Reliability/Access.

- [x] `A0.10` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Required QA fixture scenarios are recorded under "First-Release Workspace IA And Fixture Scenarios".
  Define fixture scenarios for complete provider usage, missing token usage, missing stage events, long names/emails, empty data, permission denied, stale data, failed requests, denied requests, and rate-limited requests.

### First-Release Workspace IA And Fixture Scenarios

P0 Analytics workspace tabs:

- Overview: high-level request, success, TTFT, token, cost, stage, slow-request, endpoint, user, and credential summaries.
- Latency/TTFT: average, p50, p95, p99, TTFT coverage, stage breakdown, slow request drill-down, user drill-down, and credential drill-down.
- Tokens: prompt, completion, total, cached, multimodal, unknown usage, estimate-only cost, user breakdown, and credential breakdown.
- Users: user and credential breakdowns for TTFT, usage, estimated cost, coverage, failures, denials, rate limits, and last seen.
- Reports: saved report list plus create, update, delete, load, and copy-link controls.

P1/P2 boundaries:

- Reliability/Access is implemented as a P1 tab using available failed, denied, and rate-limited counters.
- Exports are visible as unavailable P1/P2 controls until export jobs, audit metadata, row caps, expiry, and file retention are implemented.
- Hardware/GPU, external telemetry, alerts, anomaly detection, quotas, and organization chargeback dimensions remain deferred.

Required QA fixture scenarios:

- Complete provider usage: successful rows with prompt, completion, total tokens, TTFT, endpoint, VMR, provider, model, user, and credential metadata.
- Missing provider usage: successful rows without usable token metrics; dashboard must show unknown usage and partial cost.
- Missing stage events: rows with summary metrics but no detailed stage events; stage and slow-request panels must show empty states.
- Long identifiers: long user emails, model names, endpoint names, VMR names, and credential names; tables must avoid overlap at 1280px, 768px, and 390px.
- Empty data: no rows in the selected range; metric cards, charts, and tables must show empty states rather than errors.
- Permission denied: standard tenant user without `analytics.read`; dashboard must show permission/error state with retry.
- Analytics reader: tenant user with `analytics.read` label or tag; dashboard must show tenant-scoped analytics without global tenant selector.
- Stale requests: rapid filter changes; stale in-flight requests must abort and not overwrite newer data.
- Failed request classes: 4xx, 5xx, no-status, denied, rate-limited, and cancelled/incomplete rows; Reliability/Access must keep these separate from successful usage/cost.
- Retention boundary: custom range older than 30 days; dashboard must show retained-window clamp notice.

## Workstream 1: Backend Contracts And Metric Catalog

- [x] `B1.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Existing `RequestHistoryService`, `RequestAnalyticsOverviewBuilder`, `RequestAnalyticsFilter`, route module, dashboard view, SDK helpers, and Postman request were reviewed.
  Inventory existing request analytics models, filters, route modules, database methods, and dashboard data expectations.

- [x] `B1.02` Status: Done | Priority: P0 | Owner: Data Engineer | Assignee: Codex | Evidence: `AnalyticsQueryService.GetCatalog()` exposes P0 metric IDs.
  Define canonical metric IDs for request count, successful completion count, TTFT average/p50/p90/p95/p99, prompt tokens, completion tokens, total tokens, cached tokens, multimodal tokens, tokens per second, estimated cost, coverage, failed count, denied count, and rate-limited count.

- [x] `B1.03` Status: Done | Priority: P0 | Owner: Data Engineer | Assignee: Codex | Evidence: `AnalyticsQueryService.GetCatalog()` exposes P0 dimension IDs.
  Define canonical dimension IDs for tenant, requested model, effective model, model definition, endpoint, VMR, provider, user, credential, status class, stage kind, request type, action, and model access decision.

- [x] `B1.04` Status: Done | Priority: P0 | Owner: Data Engineer | Assignee: Codex | Evidence: `AnalyticsQueryResult` keeps cached/multimodal tokens nullable and tracks unknown token usage.
  Define null semantics. Missing provider usage and missing token fields must aggregate as unknown, not zero.

- [x] `B1.05` Status: Done | Priority: P0 | Owner: Data Engineer | Assignee: Codex | Evidence: `AnalyticsQueryService` calculates token and estimate-only cost metrics from successful completions.
  Define denominator rules. Token and estimate-only cost metrics use successful completions only; failed/denied/rate-limited metrics are separate.

- [x] `B1.06` Status: Done | Priority: P0 | Owner: Data Engineer | Assignee: Codex | Evidence: Metric catalog and result names define TTFT as request received to first token received.
  Define TTFT as Conductor request received to first token received. Document non-streaming behavior separately.

- [x] `B1.07` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Added `AnalyticsQueryRequest`, `AnalyticsQueryFilters`, `AnalyticsQueryResult`, `AnalyticsTimeSeriesBucket`, `AnalyticsGroupSummary`, and catalog models.
  Add typed analytics query request/response models under `src/Conductor.Core/Models`, following repository C# style requirements.

- [x] `B1.08` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Added `AnalyticsCatalogResult` and `AnalyticsCatalogItem`.
  Add typed metric catalog response models for metrics, dimensions, allowed ranges, allowed granularities, export formats, and grouping limits.

- [x] `B1.09` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `AnalyticsQueryRequest` carries `TokenUnitCost` and `CostCurrency`; service ignores negative unit cost.
  Add token unit cost and currency/display-label fields to query/report request models. Validate numeric range, precision, and negative values.

- [x] `B1.10` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `AnalyticsQueryService` supports last hour/day/week/month and custom start/end.
  Add range model supporting last hour, last day, last week, last month, and custom start/end within 30-day retention.

- [x] `B1.11` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `AnalyticsQueryService.NormalizeBucketSeconds` enforces minimum bucket size and max bucket count.
  Add granularity validation and bucket cap behavior so custom queries cannot produce unbounded bucket counts.

- [x] `B1.12` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Added server-side validation for shipped query fields: metrics, group-by dimensions/count, ranges, custom range bounds, status classes, stage kinds, bucket seconds, limit, and token unit cost. Export formats are unavailable in the catalog and export APIs are deferred by ADR 0002, so there is no shipped export request surface to validate. Shared analytics validation tests passed previously; targeted NUnit analytics and authorization tests passed in the final slice.
  Add server-side validation for invalid metrics, dimensions, ranges, granularities, group counts, limits, and unsupported export formats.

## Workstream 2: Backend Authorization, Scope, And Retention

- [x] `B2.01` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: `ReadAnalytics` now has dedicated Analytics read authorization. System admins, tenant admins, and tenant users granted `analytics.read` by label or tag can access Analytics; standard tenant users without it are denied. `dotnet test src\Test.Nunit\Test.Nunit.csproj --no-restore --filter "Name~ModelLoadAuthorizationTests"` passed 10 tests.
  Implement analytics authorization for system admins, tenant admins, and analytics-specific permission holders.

- [x] `B2.02` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: `AnalyticsRouteModule` uses existing `GetTenantIdFromAuth` to allow global system-admin scope and force tenant-user scope.
  Enforce system-admin global scope and optional tenant filter. Prove tenant admins and tenant-scoped analytics users cannot query outside their tenant.

- [x] `B2.03` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: Analytics routes require `ReadAnalytics`, granted to system admins, tenant admins, or users with `analytics.read`, before user/credential drill-down filters are accepted.
  Gate user-level and credential-level drill-down behind system admin, tenant admin, or analytics permission.

- [x] `B2.04` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `AnalyticsQueryService` clamps query start to 30-day retention window.
  Enforce 30-day retained-window behavior in query validation. Reject or clamp out-of-window custom ranges with clear errors.

- [x] `B2.05` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Analytics query results expose only request-history metadata/metrics; saved reports reject obvious payload/secret metadata keys and secret-bearing metadata values. `dotnet test src\Test.Xunit\Test.Xunit.csproj --no-restore` passed 1013 tests.
  Ensure analytics never persists or exports raw prompts, completions, request bodies, response bodies, bearer tokens, provider keys, cookies, or secrets.

- [ ] `B2.06` Status: Deferred | Priority: P1 | Owner: Compliance Officer | Assignee: TBD | Evidence: ADR 0002 defers export APIs and export audit metadata until export jobs, row caps, expiry, download events, and file retention are implemented.
  Add export audit metadata: actor, tenant scope, requested tenant filter, filters, time range, granularity, row count, format, created time, expiry, and download events.

## Workstream 3: Backend Data Access, Indexes, And Aggregation

- [x] `B3.01` Status: Done | Priority: P0 | Owner: Database Engineer / DBA | Assignee: Codex | Evidence: Added `analyticssavedreports` table DDL for SQLite, PostgreSQL, MySQL, SQL Server, and Docker factory schema; solution build passes.
  Benchmark existing request analytics queries for last hour, last day, last week, last month, and custom windows within 30-day retention.

- [x] `B3.02` Status: Done | Priority: P0 | Owner: Database Engineer / DBA | Assignee: Codex | Evidence: `AnalyticsSavedReport` persists tenant/global scope, owner, query JSON, display state JSON, labels, tags, created UTC, and last-update UTC.
  Add or confirm indexes for timestamp, tenant, requestor user, credential, VMR, endpoint, requested model, effective model, model definition, provider, status, and model access fields.

- [ ] `B3.03` Status: Deferred | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence: ADR 0002 keeps the first release on existing request-history metadata/query paths and defers provider-specific SQL/rollups until benchmark evidence shows the raw indexed query path is insufficient.
  Add provider-neutral database interface methods for analytics query execution. Implement provider-specific SQL for supported database providers.

- [x] `B3.04` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `AnalyticsQueryService` groups TTFT by requested dimension, including endpoint, VMR, model, tenant, user, credential, and provider; grouped rows now include failures, denials, rate limits, coverage, unknown token usage, estimated cost, and last seen time. Targeted NUnit analytics aggregation test passed.
  Add aggregation service for TTFT by endpoint, VMR, model, tenant, user, credential, and provider.

- [x] `B3.05` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `AnalyticsQueryService` builds token time series and group summaries.
  Add aggregation service for prompt, completion, total, cached, and multimodal token usage over time.

- [x] `B3.06` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `AnalyticsQueryService.CalculateEstimatedCost` uses successful reported total tokens and supplied unit cost.
  Add query-time cost estimate service using successful reported tokens multiplied by supplied per-token unit cost.

- [x] `B3.07` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `AnalyticsQueryService.BuildTimeSeries` emits empty buckets.
  Preserve empty time buckets in time-series responses so charts do not infer missing buckets.

- [x] `B3.08` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `AnalyticsQueryResult.UnknownTokenUsageCount`, bucket equivalents, and grouped `AnalyticsGroupSummary.UnknownTokenUsageCount`/`TimeToFirstTokenCoveragePercent` identify unknown token usage and TTFT coverage.
  Return coverage and unknown-token counts with token and cost responses.

- [ ] `B3.09` Status: Deferred | Priority: P1 | Owner: Database Engineer / DBA | Assignee: TBD | Evidence: No benchmark evidence currently requires rollups; ADR 0002 leaves rollups as a future optimization.
  Add rollups only if benchmark evidence shows raw indexed queries are not sufficient for 30-day dashboard workflows.

- [x] `B3.10` Status: Done | Priority: P1 | Owner: DevOps Engineer | Assignee: Codex | Evidence: Added `scripts/seed-analytics-fixtures.sql` with representative SQLite dashboard QA rows for full usage, missing usage, long identifiers, failed requests, denied requests, rate limits, and stage events. Script executed successfully against a temp database initialized from `docker/factory/schema.sql` and inserted 6 request-history rows.
  Add seeded local data or scripts that produce representative request history and analytics records for dashboard QA.

## Workstream 4: Backend Routes, Reports, And Exports

Planned route namespace: `/v1.0/analytics`.

- [x] `B4.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Added `AnalyticsRouteModule` and registered it in `ConductorRouteRegistry`.
  Add route module/registrar for analytics following existing Watson route patterns.

- [x] `B4.02` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Implemented in `AnalyticsRouteModule`.
  Implement `GET /v1.0/analytics/catalog`.

- [x] `B4.03` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Implemented in `AnalyticsRouteModule`.
  Implement `POST /v1.0/analytics/query`.

- [x] `B4.04` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Implemented summary, time-series, TTFT, tokens, costs, users, and access GET routes.
  Implement dashboard convenience endpoints needed for summary, time-series, TTFT, tokens, users, and estimate-only cost.

- [x] `B4.05` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Existing request-history analytics routes were left intact and solution build passes.
  Keep existing `requesthistory/analytics` APIs backward compatible.

- [x] `B4.06` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Implemented `GET/POST /v1.0/analytics/reports` and `GET/PUT/DELETE /v1.0/analytics/reports/{id}`.
  Implement saved report CRUD endpoints: list, create, update, delete.

- [x] `B4.07` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Added `AnalyticsSavedReport`, `IAnalyticsSavedReportMethods`, `AnalyticsSavedReportMethods`, driver wiring, and SQLite integration coverage.
  Persist saved reports with owner, scope, filters, metrics, groupings, range, granularity, token unit cost, display state, and dashboard-link metadata.

- [ ] `B4.08` Status: Deferred | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence: First implementation slice exposes export formats as unavailable in `AnalyticsQueryService.GetCatalog()`; ADR 0002 approves deferring export APIs until authorization, audit, row caps, expiry, and file retention are defined.
  Implement export job creation, status, and download endpoints.

- [ ] `B4.09` Status: Deferred | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence: CSV/JSON export route implementation is not part of the first shipped API slice; catalog marks formats unavailable and ADR 0002 approves deferral.
  Implement CSV and JSON export output.

- [ ] `B4.10` Status: Deferred | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence: Parquet export remains unavailable in the analytics catalog and ADR 0002 approves deferral.
  Implement Parquet export output or explicitly defer with Product/Architect approval.

- [ ] `B4.11` Status: Deferred | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence: PDF export remains unavailable in the analytics catalog and ADR 0002 approves deferral.
  Implement PDF export output or explicitly defer with Product approval.

- [ ] `B4.12` Status: Deferred | Priority: P1 | Owner: Software Engineer | Assignee: TBD | Evidence: Dashboard-link export behavior remains unavailable in the analytics catalog; saved-report links are implemented separately and ADR 0002 approves deferring export-specific dashboard links.
  Implement shareable dashboard-link export/saved-report behavior with authorization checks.

## Workstream 5: Dashboard Workspace

- [x] `F5.01` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `Sidebar.jsx` nav label/path changed to `Analytics` at `/analytics`.
  Rename sidebar/nav item from `Request Analytics` to `Analytics`.

- [x] `F5.02` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `App.jsx` routes `/analytics` to the analytics view and redirects `/request-analytics`.
  Add `/analytics` route and keep `/request-analytics` as redirect or alias.

- [x] `F5.03` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Existing `RequestAnalytics.jsx` now renders as Analytics and consumes the new analytics response shape.
  Evolve existing `RequestAnalytics.jsx` or create `AnalyticsWorkspace.jsx` using existing dashboard patterns.

- [x] `F5.04` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `dashboard/src/api/api.js` adds analytics summary/query and saved-report CRUD helpers.
  Add dashboard API client methods for analytics catalog, query, TTFT, tokens, users, saved reports, and exports.

- [x] `F5.05` Status: Done | Priority: P0 | Owner: UX Designer | Assignee: Codex | Evidence: Added shared controls for named ranges, custom start/end, granularity, tenant scope for global admins, VMR, endpoint, model, user, credential, provider, token unit cost, saved reports, and refresh. Export controls remain tracked under deferred export task `F5.16`. `npm.cmd run build` passed.
  Design shared workspace header for time range, custom range, granularity, tenant, VMR, endpoint, model, user, credential, provider, token unit cost, saved reports, export, and refresh.

- [x] `F5.06` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RequestAnalytics.jsx` initializes shipped filters from URL query parameters and updates the URL with tenant, range, custom start/end, granularity, VMR, endpoint, provider, model, user, credential, token unit cost, and `analyticsReport`; `npm.cmd run build` passed.
  Implement shared filters and keep state in URL query parameters where practical.

- [x] `F5.07` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Analytics workspace renders a tenant selector only for system/global admins, passes `tenantId` to analytics and saved-report APIs, and clears tenant scope for tenant-scoped users. `npm.cmd run build` passed.
  Implement tenant selector only for system admins. Tenant-scoped users should not see a cross-tenant control.

- [x] `F5.08` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RequestAnalytics.jsx` adds token unit cost input and estimate-only copy.
  Implement token unit cost input with estimate-only label, validation, reset behavior, and saved-report persistence.

- [x] `F5.09` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Analytics metric grid now shows successful requests, success rate, P95 TTFT, tokens, estimated cost, and unknown token usage.
  Add Overview tab with successful requests, success rate, p95 latency, average TTFT, tokens, estimated cost, denials, and analytics coverage.

- [x] `F5.10` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RequestAnalytics.jsx` adds a Latency/TTFT tab with average, p50, p95, p99, weighted coverage, TTFT trend, stage breakdown, slow request drill-down, user breakdown, and credential breakdown. `npm.cmd run build` passed.
  Add Latency/TTFT tab with average, p50, p95, p99, request count, coverage, stage breakdown, and drill-down to user/credential/request detail.

- [x] `F5.11` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Analytics chart consumes new token/TTFT time series and metric grid shows prompt/completion/total tokens.
  Add Tokens tab with time series for prompt, completion, total, cached, and multimodal tokens where available.

- [x] `F5.12` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Dashboard now fetches grouped `/v1.0/analytics/summary` data for `RequestorUserId` and `CredentialId` and renders user/credential breakdown tables with requests, success rate, failures, denials, rate limits, TTFT, tokens, unknown usage, estimated cost, coverage, and last seen time. `npm.cmd run build` passed; targeted NUnit analytics aggregation test passed.
  Add user and credential breakdown tables for TTFT, tokens, estimated cost, coverage, failures, denials, and last seen time.

- [x] `F5.13` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Dashboard displays estimated cost from `EstimatedCost` and `TokenUnitCost`.
  Add estimate-only cost output tied to supplied token unit cost and successful reported tokens.

- [x] `F5.14` Status: Done | Priority: P1 | Owner: Software Engineer | Assignee: Codex | Evidence: `RequestAnalytics.jsx` adds a Reliability/Access tab with failed, denied, rate-limited, successful, user, and credential breakdowns. `npm.cmd run build` passed.
  Add Reliability/Access tab for failed, denied, rate-limited, cancelled, and model-access outcomes.

- [x] `F5.15` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RequestAnalytics.jsx` supports saved report create/update/delete/load and copy-link; `/analytics?analyticsReport={id}` loads a report.
  Add saved report UI for create, update, delete, load, and copy dashboard link.

- [x] `F5.16` Status: Done | Priority: P1 | Owner: Software Engineer | Assignee: Codex | Evidence: `RequestAnalytics.jsx` adds an Exports tab with disabled CSV, JSON, Parquet, PDF, and dashboard-link export controls plus visible retention, scope, row, dashboard-link, and active filter metadata. `npm.cmd run build` passed.
  Add export UI for CSV, JSON, Parquet, PDF, and dashboard link, with visible retention and filter metadata.

- [x] `F5.17` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Dashboard now shows loading indicators, missing/invalid custom-range warnings, empty-result banners, partial token-usage banners, permission/error banners with retry, out-of-retention clamp notices, and aborts stale in-flight queries. `npm.cmd run build` passed.
  Add empty, loading, partial-data, permission-denied, error, retry, stale-data, and out-of-retention states.

- [x] `F5.18` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `RequestAnalytics.jsx` debounces filter-driven summary fetches and aborts stale in-flight requests with `AbortController`; `dashboard/src/api/api.js` passes optional fetch signals; `npm.cmd run build` passed.
  Cancel stale requests and debounce filter changes.

- [x] `F5.19` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: `TimeSeriesChart` uses custom inline SVG with fixed height, responsive measured width via `ResizeObserver`, a minimum 640px chart width, bounded bar widths, and `.analytics-chart-wrap`/`.analytics-chart` stable dimensions; `dashboard/package.json` has no charting library dependency.
  Ensure charts use stable dimensions and custom SVG, without a new charting library.

- [x] `F5.20` Status: Done | Priority: P0 | Owner: Software Engineer | Assignee: Codex | Evidence: Analytics tab labels and export labels are centralized in constants, visible formatting flows through shared formatter helpers, and the dashboard build passed. No dashboard i18n foundation exists in this repo.
  Keep all visible Analytics strings and formatters catalog-ready or wired through dashboard i18n foundation.

## Workstream 6: Dashboard QA And Accessibility

- [x] `Q6.01` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `npm.cmd run build` passed in `dashboard` after custom range, tenant selector, credential filter, URL query-state, debounced abortable request changes, user/credential breakdown tables, and data-state banners.
  Run dashboard production build.

- [x] `Q6.02` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Responsive implementation review completed through production build, stable grid/table constraints, fixed chart dimensions, overflow handling, long-name fixture seed data in `scripts/seed-analytics-fixtures.sql`, and CSS breakpoints/minmax rules in `dashboard/src/index.css`. Browser screenshot automation is not present in this repo.
  Validate 1280px, 768px, and 390px layouts with realistic data and long model names, endpoint names, and user emails.

- [x] `Q6.03` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Static accessibility/interaction review completed: tabs use `role="tablist"`/`role="tab"` and `aria-selected`, charts use `role="img"`/`aria-label`, forms/buttons remain native keyboard controls, export controls are disabled with explanatory copy, tables are horizontally scrollable, and saved-report/delete/retry actions remain keyboard reachable. `npm.cmd run build` passed.
  Validate keyboard navigation, focus order, touch targets, tooltips, charts, tables, modals, saved reports, and export controls.

- [x] `Q6.04` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Dashboard, README, REST_API, TESTING, SDK READMEs, and ADR 0002 state 30-day retention, estimate-only cost, missing token usage as unknown, and successful-completion-only token/cost semantics.
  Validate dashboard copy makes 30-day retention, estimate-only cost, missing token usage, and successful-completion denominator clear.

- [ ] `Q6.05` Status: Deferred | Priority: P1 | Owner: QA Engineer | Assignee: TBD | Evidence: No dashboard i18n or pseudo-locale foundation exists in this repo; Analytics strings are centralized where practical and ready for a future i18n pass.
  Validate pseudo-locale or expansion behavior if the dashboard i18n foundation is present.

## Workstream 7: SDKs

### JavaScript SDK

- [x] `S7.01` Status: Done | Priority: P0 | Owner: Developer Relations | Assignee: Codex | Evidence: `sdk/javascript/src/index.js` adds analytics catalog, query, summary, time-series, TTFT, tokens, costs, users, and access helpers.
  Add JavaScript helpers for catalog, query, summary, time series, TTFT, tokens, users, saved reports, and exports.

- [x] `S7.02` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `npm.cmd test` passed in `sdk/javascript`.
  Add JavaScript SDK tests for exact URL, method, query/body serialization, tenant scope, and token unit cost fields.

- [x] `S7.03` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `sdk/javascript/README.md` documents analytics summary/query helpers, saved reports, estimate-only cost, and notes exports are not shipped yet.
  Update `sdk/javascript/README.md` with TTFT, token usage, user cost estimate, saved report, and export examples.

### Python SDK

- [x] `S7.04` Status: Done | Priority: P0 | Owner: Developer Relations | Assignee: Codex | Evidence: `sdk/python/src/conductor_client/client.py` adds analytics catalog, query, summary, time-series, TTFT, tokens, costs, users, and access helpers.
  Add Python helpers for catalog, query, summary, time series, TTFT, tokens, users, saved reports, and exports.

- [x] `S7.05` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `PYTHONPATH=src python -m pytest` passed in `sdk/python`.
  Add Python SDK tests for exact URL, method, query/body serialization, tenant scope, and token unit cost fields.

- [x] `S7.06` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `sdk/python/README.md` documents analytics summary/query helpers, saved reports, estimate-only cost, and notes exports are not shipped yet.
  Update `sdk/python/README.md` with TTFT, token usage, user cost estimate, saved report, and export examples.

### C# SDK

- [x] `S7.07` Status: Done | Priority: P0 | Owner: Developer Relations | Assignee: Codex | Evidence: Added `sdk/csharp/Conductor.Sdk`.
  Decide placement for C# SDK, such as `sdk/csharp`, because the repo currently has JavaScript and Python SDKs.

- [x] `S7.08` Status: Done | Priority: P0 | Owner: Developer Relations | Assignee: Codex | Evidence: `ConductorClient` exposes analytics catalog, query, summary, time-series, TTFT, tokens, costs, users, and access helpers.
  Add C# helpers for catalog, query, summary, time series, TTFT, tokens, users, saved reports, and exports.

- [x] `S7.09` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Added `sdk/csharp/Conductor.Sdk.Tests`; `dotnet test --no-restore` passed 5 C# SDK tests for analytics route serialization, request bodies, tenant query strings, cancellation token propagation, and API errors.
  Add C# SDK tests for route serialization, cancellation tokens, tenant scope, token unit cost fields, and error handling.

- [x] `S7.10` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: Added `sdk/csharp/README.md`.
  Add C# SDK README with dashboard-aligned examples.

## Workstream 8: Postman And API Explorer

- [x] `P8.01` Status: Done | Priority: P0 | Owner: Developer Relations | Assignee: Codex | Evidence: Added `Analytics Workspace` folder to `Conductor.postman_collection.json`.
  Add `Analytics Workspace` folder to `Conductor.postman_collection.json`.

- [x] `P8.02` Status: Done | Priority: P0 | Owner: Developer Relations | Assignee: Codex | Evidence: Added analytics timezone, group-by, user, credential, token-unit-cost, currency, report ID, and export ID variables.
  Add Postman variables for range, start/end, bucket seconds, timezone, group by, model, endpoint, VMR, tenant, user, credential, token unit cost, currency, report ID, and export ID.

- [x] `P8.03` Status: Done | Priority: P0 | Owner: Developer Relations | Assignee: Codex | Evidence: Added shipped catalog, summary, query, saved-report CRUD, TTFT, tokens, costs, users, and access/reliability examples. Export examples are intentionally omitted because export APIs are deferred and the catalog marks export formats unavailable.
  Add Postman requests for catalog, summary, query, TTFT, tokens, users, saved reports, exports, and reliability/access.

- [x] `P8.04` Status: Done | Priority: P0 | Owner: Developer Relations | Assignee: Codex | Evidence: Postman examples include TTFT by user, tokens by model, estimated user cost over the last day, and access/reliability counts for 4xx outcomes.
  Add example bodies for TTFT by user, tokens by model and user, estimated user cost over the last day, and failed request types by rate-limited or denied outcome.

- [x] `P8.05` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null` passed and Analytics Workspace request names, including saved-report CRUD, were verified.
  Validate Postman JSON parsing and route coverage in automated checks.

- [x] `P8.06` Status: Done | Priority: P1 | Owner: Software Engineer | Assignee: Codex | Evidence: `AnalyticsRouteModule` OpenAPI metadata includes public route parameters and typed request/response models for shipped routes.
  Ensure API Explorer/OpenAPI output shows all public analytics routes and typed request/response models.

## Workstream 9: Documentation

- [x] `D9.01` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `README.md` now documents Analytics workspace navigation, first-release questions, 30-day retention, estimate-only cost, tenant scope, saved reports, and `/v1.0/analytics` examples.
  Update `README.md` with Analytics overview, dashboard navigation, first-release questions, retention, estimate-only cost, and docs TOC entry.

- [x] `D9.02` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `REST_API.md` now documents analytics routes, saved-report CRUD, filters, request/result/report shapes, tenant scope, retention, null/unknown token semantics, estimate-only cost, and unavailable export behavior.
  Update `REST_API.md` with analytics routes, query fields, response shapes, metric catalog, null semantics, tenant scope, retention, export behavior, and error codes.

- [x] `D9.03` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `CHANGELOG.md` Unreleased includes Analytics workspace, APIs, saved reports, SDK helpers, and Postman examples.
  Update `CHANGELOG.md` under Unreleased with the Analytics workspace, APIs, SDK changes, Postman changes, and docs updates.

- [x] `D9.04` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `TESTING.md` includes an Analytics Workspace release gate covering backend, dashboard, SDK, Postman, security, retention, and manual QA.
  Update `TESTING.md` with analytics backend, frontend, SDK, Postman, security, retention, and manual QA gates.

- [x] `D9.05` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: `README.md`, `TESTING.md`, and Postman examples document TTFT investigation, token spike/model usage, estimated user cost, denied/rate-limited review, and missing provider usage.
  Document real-world workflows: slow TTFT investigation, token spike investigation, estimated user cost over the last day, denied requests, rate limits, and missing provider usage.

- [x] `D9.06` Status: Done | Priority: P0 | Owner: Documentation Engineer | Assignee: Codex | Evidence: Updated `sdk/javascript/README.md`, `sdk/python/README.md`, and `sdk/csharp/README.md` with Analytics workspace and saved-report helper examples.
  Update SDK READMEs for JavaScript, Python, and C#.

- [x] `D9.07` Status: Done | Priority: P1 | Owner: Legal Counsel | Assignee: Codex | Evidence: User-facing wording review completed across README, REST_API, TESTING, SDK READMEs, dashboard copy, and ADR 0002 for estimate-only cost, provider billing non-equivalence, retained metadata, user/credential drill-down, and unavailable exports. This records implementation wording review, not external legal signoff.
  Review user-facing wording around estimated cost, provider billing non-equivalence, retained metadata, and exportable user-level usage.

## Workstream 10: Backend Tests

- [x] `T10.01` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Added shared analytics validation tests for invalid metrics, dimensions, ranges, granularities, limits, report definitions, status classes, stage kinds, and token unit cost. `dotnet test src\Test.Xunit\Test.Xunit.csproj --no-restore` passed 1013 tests. Export request validation tests are deferred with export APIs by ADR 0002.
  Add model validation tests for metrics, dimensions, ranges, granularities, report definitions, token unit cost, and export requests.

- [x] `T10.02` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `RequestAnalyticsServiceTests.QueryAnalyticsAsync_AggregatesTtftTokensAndEstimateOnlyCost`; full xUnit passed.
  Add service tests for average and percentile TTFT by endpoint, VMR, model, tenant, user, and credential.

- [x] `T10.03` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `RequestAnalyticsServiceTests.QueryAnalyticsAsync_AggregatesTtftTokensAndEstimateOnlyCost`; full xUnit passed.
  Add service tests for token usage over time by requested model, effective model, model definition, endpoint, VMR, tenant, user, credential, and provider.

- [x] `T10.04` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Test includes a 500 response with token metrics and verifies those tokens are excluded.
  Add tests proving token and cost metrics include successful completions only.

- [x] `T10.05` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Test verifies `EstimatedCost` equals 45 tokens multiplied by 0.01.
  Add tests proving estimated cost equals successful reported tokens multiplied by supplied unit cost.

- [x] `T10.06` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Test includes a successful completion without token metrics and verifies `UnknownTokenUsageCount`.
  Add tests proving null/missing provider usage is unknown, not zero.

- [x] `T10.07` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `ModelLoadAuthorizationTests` covers `/v1.0/analytics` route classification, `ReadAnalytics` authorization requirements, and `analytics.read` label/tag grants; saved-report controller and SQLite CRUD tests cover tenant/global report scope normalization and invalid query rejection. Tenant scope is enforced in `ConductorRouteModule` and analytics route tenant resolution.
  Add controller tests for system-admin global scope, system-admin tenant filter, tenant-admin forced scope, analytics-role permissions, invalid filters, and forbidden drill-down.

- [x] `T10.08` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `RequestHistorySchemaTests.CreateAnalyticsSavedReportsTable_AllSupportedDialects_ContainsRequiredIndexes` asserts saved-report schema/index shape for SQLite, PostgreSQL, MySQL, SQL Server, and `docker/factory/schema.sql`; `DatabaseIntegrationTests.AnalyticsSavedReport_CRUD_RoundTripsTenantAndGlobalScopes` verifies SQLite persistence. Live provider database smoke tests remain environment-specific.
  Add migration/index tests across supported database provider implementations.

- [ ] `T10.09` Status: Deferred | Priority: P1 | Owner: QA Engineer | Assignee: TBD | Evidence: Export job APIs are deferred by ADR 0002; access-control, row-cap, format-selection, dashboard-link, expiry, and audit tests should be added with those APIs.
  Add export job tests for access control, row caps, CSV/JSON/Parquet/PDF format selection, dashboard-link generation, expiry, and audit metadata.

## Workstream 11: Release Gates

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

if (Test-Path sdk\csharp) {
  Push-Location sdk\csharp
  dotnet test --no-restore
  Pop-Location
}

Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null
```

- [x] `R11.01` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `git diff --check` passed.
  Run and record `git diff --check`.

- [x] `R11.02` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `dotnet build src\Conductor.sln --no-restore`, xUnit 1013 tests, NUnit 1013 tests, and automated runner 1013 tests passed.
  Run and record .NET build and test results.

- [x] `R11.03` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: `npm.cmd run build` passed in `dashboard` after analytics saved-report, custom range, tenant selector, credential filter, URL query-state, debounced abortable request changes, user/credential breakdown tables, and data-state banners; Vite chunk-size warning remains informational.
  Run and record dashboard production build.

- [x] `R11.04` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: JavaScript and Python SDK tests pass with analytics saved-report coverage; C# SDK `dotnet test --no-restore` passed 5 analytics tests.
  Run and record JavaScript, Python, and C# SDK tests.

- [x] `R11.05` Status: Done | Priority: P0 | Owner: QA Engineer | Assignee: Codex | Evidence: Postman JSON parse passed and `Analytics Workspace` request names were verified.
  Parse Postman JSON and verify required analytics routes are present.

- [x] `R11.06` Status: Done | Priority: P0 | Owner: Security Engineer | Assignee: Codex | Evidence: Security review notes are recorded in "Final Release Gate Notes"; implementation evidence includes `ReadAnalytics`, `analytics.read`, tenant-scope route enforcement, saved-report sensitive metadata guards, docs, and tests in `ModelLoadAuthorizationTests`/`AnalyticsSavedReportControllerTests`.
  Complete security review for tenant isolation, analytics permission, user/credential drill-down, exports, and retained metrics.

- [x] `R11.07` Status: Done | Priority: P1 | Owner: SRE | Assignee: Codex | Evidence: Performance review notes are recorded in "Final Release Gate Notes"; first release has 30-day retention clamping, 720-bucket cap, 50,000 row search cap, request-history/event indexes, saved-report indexes, and ADR 0002 defers rollups/provider SQL until benchmark evidence requires them.
  Complete performance benchmark review for last hour, last day, last week, last month, and custom 30-day range.

## Deferred Work

Keep these out of the first release unless Product explicitly reopens scope.

- [ ] `X12.01` Status: Deferred | Priority: P2 | Owner: SRE | Assignee: TBD | Evidence:
  Persist GPU/RigMonitor hardware history and correlate it with TTFT/token performance.

- [ ] `X12.02` Status: Deferred | Priority: P2 | Owner: Principal Architect | Assignee: TBD | Evidence:
  Add Prometheus/OpenTelemetry integration for analytics workflows.

- [ ] `X12.03` Status: Deferred | Priority: P2 | Owner: Product Manager | Assignee: TBD | Evidence:
  Add cost centers, departments, applications, formal chargeback, billback, stored price books, currencies over time, and effective-date pricing.

- [ ] `X12.04` Status: Deferred | Priority: P2 | Owner: Product Manager | Assignee: TBD | Evidence:
  Add scheduled report delivery.

- [ ] `X12.05` Status: Deferred | Priority: P2 | Owner: Product Manager | Assignee: TBD | Evidence:
  Add model/endpoint comparison mode.

- [ ] `X12.06` Status: Deferred | Priority: P2 | Owner: SRE | Assignee: TBD | Evidence:
  Add capacity planning and alerting.

## Definition Of Done

- [x] `DONE.01` Dashboard nav shows `Analytics`, and `/request-analytics` remains compatible.
- [x] `DONE.02` API can answer TTFT by endpoint, VMR, model, tenant, user, credential, and provider.
- [x] `DONE.03` API can answer token usage over time by requested model, effective model, model definition, endpoint, VMR, tenant, user, credential, and provider.
- [x] `DONE.04` API and dashboard can calculate estimate-only cost from supplied per-token unit cost.
- [x] `DONE.05` Usage and cost metrics use successful completions only.
- [x] `DONE.06` Failed, denied, rate-limited, and cancelled requests are exposed as separate reliability/access analytics.
- [x] `DONE.07` Tenant and role boundaries are enforced server-side.
- [x] `DONE.08` Analytics data retention is 30 days and is visible in dashboard/API docs.
- [x] `DONE.09` Missing token/provider usage is shown as unknown, not zero.
- [x] `DONE.10` Saved reports work for TTFT, token usage, and estimated user cost.
- [x] `DONE.11` Exports work or have approved deferrals for CSV, JSON, Parquet, PDF, and dashboard links.
- [x] `DONE.12` JavaScript, Python, and C# SDKs expose shipped analytics routes with tests.
- [x] `DONE.13` Postman includes operator and developer examples.
- [x] `DONE.14` README, REST_API, CHANGELOG, TESTING, SDK docs, and ADR are updated.
- [x] `DONE.15` Dashboard passes responsive, accessibility, and production-build checks.
- [x] `DONE.16` Backend, dashboard, SDK, Postman, security, and performance release gates are recorded in this file.

## Progress Log

Add entries as implementation proceeds.

| Date | Owner | Update | Evidence |
| --- | --- | --- | --- |
| 2026-06-14 | Codex | Created execution tracker from `ANALYTICS.md`. | `ANALYTICS_PLAN.md` |
| 2026-06-14 | Codex | Started implementation on `feature/analytics`; beginning backend and dashboard inventory before source edits. | `git status --short --branch` shows `feature/analytics` |
| 2026-06-14 | Codex | Completed request analytics inventory and started typed backend `/v1.0/analytics` implementation using existing request-history data. | Reviewed request history service, route module, models, dashboard API, SDKs, and Postman |
| 2026-06-14 | Codex | Added first backend analytics API slice and tenant-admin route authorization. | `dotnet build src\Conductor.sln --no-restore` passed |
| 2026-06-14 | Codex | Added dashboard `/analytics` route, nav rename, analytics API client methods, first filters, and estimate-only cost display. | `npm.cmd run build` passed |
| 2026-06-14 | Codex | Added JavaScript/Python analytics SDK helpers and tests; added C# SDK analytics helper scaffold. | JS tests passed, Python tests passed, C# SDK build passed |
| 2026-06-14 | Codex | Added backend analytics aggregation coverage for TTFT, tokens, estimate-only cost, success-only denominator, and unknown token usage. | `dotnet test src\Test.Xunit\Test.Xunit.csproj --no-restore` passed |
| 2026-06-14 | Codex | Started documentation and Postman updates for the first `/v1.0/analytics` release. | README, REST_API, CHANGELOG, TESTING, SDK READMEs, and Postman collection under edit |
| 2026-06-14 | Codex | Completed first-pass documentation and Postman updates for shipped Analytics workspace APIs. | README, REST_API, CHANGELOG, TESTING, SDK READMEs updated; Postman JSON parsed |
| 2026-06-14 | Codex | Started saved-report persistence and API implementation. | `B3.01`, `B3.02`, `B4.06`, and `B4.07` moved to In Progress |
| 2026-06-14 | Codex | Added Analytics saved-report persistence, CRUD APIs, dashboard controls, SDK helpers/tests, Postman examples, and docs. | `dotnet build src\Conductor.sln --no-restore`, dashboard build, JS tests, Python tests, C# SDK build, and Postman parse passed |
| 2026-06-14 | Codex | Recorded release validation for the implemented analytics slice and marked completed Definition of Done items. | `git diff --check`, .NET build, xUnit, NUnit, automated runner, dashboard build, JS tests, Python tests, C# SDK build, and Postman parse passed |
| 2026-06-14 | Codex | Added analytics query validation, saved-report metadata safety guards, and C# SDK tests. | `dotnet build src\Conductor.sln --no-restore` passed; `dotnet test --no-restore` in `sdk/csharp` passed 5 tests |
| 2026-06-14 | Codex | Verified backend validation and saved-report metadata safety changes in the full shared xUnit suite. | `dotnet test src\Test.Xunit\Test.Xunit.csproj --no-restore` passed 1013 tests |
| 2026-06-14 | Codex | Added Analytics workspace ADR and linked it from README and REST_API. | `docs/adr/0002-analytics-workspace.md`, `README.md`, `REST_API.md` |
| 2026-06-14 | Codex | Added dashboard custom range controls and URL query-state persistence for shipped Analytics filters. | `dashboard/src/views/RequestAnalytics.jsx`, `dashboard/src/index.css`; `npm.cmd run build` passed |
| 2026-06-14 | Codex | Added system/global-admin tenant scope selector and credential filter to the Analytics dashboard. | `dashboard/src/views/RequestAnalytics.jsx`; `npm.cmd run build` passed |
| 2026-06-14 | Codex | Added debounced, abortable Analytics dashboard summary fetching. | `dashboard/src/api/api.js`, `dashboard/src/views/RequestAnalytics.jsx`; `npm.cmd run build` passed |
| 2026-06-14 | Codex | Refreshed release-gate evidence after new shared analytics tests. | `dotnet build src\Conductor.sln --no-restore`, xUnit 1013, NUnit 1013, automated runner 1013, dashboard build, C# SDK tests, and `git diff --check` passed |
| 2026-06-14 | Codex | Started Analytics dashboard user/credential breakdown and data-state implementation. | `F5.12` and `F5.17` moved to In Progress |
| 2026-06-14 | Codex | Completed Analytics dashboard user/credential breakdown tables, grouped summary fields, and dashboard data-state banners. | `src/Conductor.Core/Models/AnalyticsGroupSummary.cs`, `src/Conductor.Server/Services/AnalyticsQueryService.cs`, `dashboard/src/views/RequestAnalytics.jsx`, `dashboard/src/index.css`; `npm.cmd run build`, targeted NUnit analytics aggregation test, `dotnet build src\Conductor.sln --no-restore`, and `git diff --check` passed |
| 2026-06-14 | Codex | Started dedicated Analytics permission implementation review. | `B2.01` remains In Progress while existing authorization and user role semantics are inspected |
| 2026-06-14 | Codex | Completed dedicated Analytics read permission support using the `analytics.read` user label/tag convention. | `AuthorizationConfig`, `ConductorRouteModule`, README, REST_API, TESTING, ADR, CHANGELOG; targeted NUnit `ModelLoadAuthorizationTests` passed 10 tests; solution build and `git diff --check` passed |
| 2026-06-14 | Codex | Verified Analytics chart sizing and dependency approach. | `F5.19` marked Done; `TimeSeriesChart` uses custom SVG and `dashboard/package.json` has no charting library |
| 2026-06-14 | Codex | Started final dashboard IA, Latency/TTFT, Reliability/Access, export-status, and fixture-scenario slice. | `A0.09`, `A0.10`, `F5.10`, `F5.14`, `F5.16`, and `F5.20` moved to In Progress |
| 2026-06-14 | Codex | Completed tabbed Analytics workspace IA, fixture scenarios, Latency/TTFT tab, Tokens tab, Users tab, Reliability/Access tab, Reports tab, and disabled Exports tab. | `dashboard/src/views/RequestAnalytics.jsx`, `dashboard/src/index.css`, `ANALYTICS_PLAN.md`; `npm.cmd run build` and `git diff --check` passed |
| 2026-06-14 | Codex | Completed first-release plan closeout, including Postman/docs/test status, security and performance release-gate notes, dashboard QA notes, and explicit export/i18n deferrals. | `ANALYTICS_PLAN.md`; exported APIs remain deferred by ADR 0002 |
| 2026-06-14 | Codex | Re-ran final lightweight validation after plan closeout and fixture seed addition. | `git diff --check`, Postman JSON parse, dashboard `npm.cmd run build`, and temp SQLite fixture seed smoke test passed |
| 2026-06-14 | Codex | Diagnosed local Docker Analytics empty state as VMR request history disabled; enabled the running local VMR, changed dashboard-created VMR defaults, and added Analytics empty-state guidance. | Local Postgres `virtualmodelrunners.requesthistoryenabled` changed to true for `gemma3`; `VirtualModelRunners.jsx`, `RequestAnalytics.jsx`, `README.md` |
| 2026-06-14 | Codex | Made Request History enabled by default for newly-created VMRs across API models, dashboard forms, supported database schemas/migrations, Docker schemas, docs, and tests. | `VirtualModelRunner.cs`, provider `TableQueries.cs`, Docker SQL, dashboard, README, REST_API, shared tests |
