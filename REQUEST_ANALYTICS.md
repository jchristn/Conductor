# Request Analytics Implementation Plan

Owner: Product Manager
Technical owner: Principal Architect
Execution owner: Engineering Manager
Created: 2026-06-05
Status: Implementation in progress - end-to-end analytics slice, focused backend closeout, documentation/runbook closeout, and legacy C# quality pass implemented and verified locally

## Progress Key

Use this file as the execution tracker. Mark checkboxes as work lands, and fill in the status columns when a task is started, blocked, reviewed, or completed.

Status values:

- `Not Started`
- `In Progress`
- `Blocked`
- `In Review`
- `Done`
- `Deferred`

Risk values:

- `Low`
- `Medium`
- `High`

## Executive Summary

Conductor already has a strong request history ledger. It captures the routing story: VMR, selected endpoint, model mapping, policy, denial reason, session affinity, body-retention state, response time, and time to first token or byte. That is the right foundation and should not be replaced.

The missing layer is request analytics. Operators need to see where time went after routing: endpoint limiter wait, upstream request-to-headers, first-token wait, generation time, provider load, prompt evaluation, token counts, tokens per second, and provider request identifiers. AssistantHub has this kind of trace-linked, stage-level telemetry. Conductor should adapt the structure to proxy and VMR workflows rather than copying AssistantHub's assistant/RAG vocabulary.

The work should ship as a product-wide expansion: database schema, startup migrations, backend capture, summary APIs, dashboard diagnostics, SDK methods, Postman examples, documentation, and tests. The dashboard is the center of the product experience. A support engineer should be able to open a slow or failed request, answer "what happened?", replay it, and decide whether the problem is routing, capacity, provider latency, generation speed, policy, or payload size.

## Product Goal

Build request analytics into Conductor so every captured VMR request can be investigated through three connected views:

1. The existing routing ledger: which VMR, policy, endpoint, model, session, mutation, and denial path applied.
2. A new performance timeline: where the request spent time across routing, capacity, upstream headers, first token, generation, and completion.
3. A provider and token profile: prompt tokens, completion tokens, total tokens, tokens per second, provider load/eval/generation metrics, provider request ID, and raw provider metrics when available.
4. A new aggregate analytics view: charted request volume, latency percentiles, stage composition, endpoint/model usage, limiter pressure, provider timing, token throughput, slowest requests, and error patterns over selectable time ranges.

The operator workflow should be dense, calm, and diagnostic. Avoid a marketing-style analytics page. The page is for repeated investigation under pressure.

## Explicit Non-Goals

- Do not remove or weaken Conductor's existing routing explanation, VMR, policy, denial, mutation, session-affinity, or retention features.
- Do not copy AssistantHub's RAG-specific stages such as retrieval gate, query rewrite, retrieval context, reranking, or citations unless Conductor later grows an assistant/RAG workflow.
- Do not store raw secrets, authorization headers, cookies, API keys, bearer tokens, or sensitive provider headers in analytics records.
- Do not add high-cardinality labels to Prometheus metrics. High-cardinality diagnostics belong in the database-backed request analytics tables.
- Do not make dashboard analytics depend on a charting library. The frontend architecture requirement favors hand-rolled SVG charts.
- Do not ship backend schema changes without provider-aware server-startup migrations for SQLite, MySQL, PostgreSQL, and SQL Server.
- Do not make the dashboard download raw history rows or parse raw telemetry JSON to build aggregate charts. Server-side aggregation owns chart-ready buckets.
- Do not store one analytics row per streamed token. Capture stream phase boundaries, chunk counts, and final provider usage instead.

## Assumptions

- Conductor continues to use Watson 7, provider-specific database drivers, typed models, and the existing Touchstone test layout.
- Request analytics is enabled only when request history capture is enabled globally and for the VMR, unless a later product decision creates a separate analytics toggle.
- Analytics metadata follows request-history metadata retention. Body retention remains governed by existing request-history body-retention settings.
- Provider metrics are best-effort. Missing provider usage fields should render as unknown, not as zero.
- Existing REST routes remain backward compatible. New fields can be added to response models, and new analytics-specific routes can be added.
- Aggregate analytics should support the same operator ranges AssistantHub uses: `lastHour`, `lastDay`, `lastWeek`, and `lastMonth`, with explicit `startUtc`, `endUtc`, and `bucketSeconds` available for SDK and Postman users.

## Implementation Status - 2026-06-05

This section tracks the current implementation state. Keep the deeper plan sections below as the backlog and evidence checklist.

Completed in the initial slice:

- [x] Status: Done | Owner: Software Engineer | Added request analytics core models, ID prefixes, provider-neutral database interface, and SQL dialect handling.
- [x] Status: Done | Owner: Software Engineer | Added `requesthistory` analytics columns and `requestanalyticsevents` table creation for SQLite, MySQL, PostgreSQL, and SQL Server.
- [x] Status: Done | Owner: Software Engineer | Added provider-aware startup migrations for tables, columns, and indexes, including Docker factory schema updates.
- [x] Status: Done | Owner: Software Engineer | Captured trace IDs, provider request IDs, token counts, TPS, request/response byte counts, limiter wait, upstream headers, first-token wait, generation, completion, denial, and provider timing phases where available.
- [x] Status: Done | Owner: Software Engineer | Parsed provider metrics for OpenAI-compatible responses, Gemini `usageMetadata`, and Ollama duration/token fields on a best-effort basis.
- [x] Status: Done | Owner: Software Engineer | Added per-request analytics and aggregate overview REST routes with tenant enforcement and disabled-request-history behavior.
- [x] Status: Done | Owner: Software Engineer | Added delete and retention cleanup behavior for analytics events.
- [x] Status: Done | Owner: Software Engineer | Added dashboard API client methods, Request Analytics navigation/page, aggregate charting, slowest-request drill-down, and request-history detail timing bars.
- [x] Status: Done | Owner: Developer Relations | Added JavaScript and Python SDK helpers and SDK README examples.
- [x] Status: Done | Owner: Developer Relations | Added Postman examples for analytics overview and per-request analytics.
- [x] Status: Done | Owner: Documentation Engineer | Updated `README.md`, `REST_API.md`, `CHANGELOG.md`, and SDK READMEs for the implemented slice.
- [x] Status: Done | Owner: QA Engineer | Verified `dotnet build src\Conductor.sln`, `dotnet test src\Test.Xunit\Test.Xunit.csproj`, dashboard production build, JavaScript SDK tests, Python SDK tests, and Postman JSON parsing.
- [x] Status: Done | Owner: QA Engineer | Re-verified after the analytics closeout pass with `dotnet build src\Conductor.sln`, xUnit 882/882, NUnit 882/882, automated console runner 882/882, product XML documentation builds with `CS1591` as error, dashboard `npm.cmd run build`, JavaScript SDK tests, Python SDK tests, Postman JSON parsing, and C# style scans for no `var`, no partial type declarations, and no product `ValueTuple`/`Tuple<>`/deconstruction usage.
- [x] Status: Done | Owner: Software Engineer | Completed C# quality pass across product and legacy paths: product XML documentation builds clean with `CS1591`, no `partial` type declarations exist, no `var` usages remain under `src`, no tuple/deconstruction usages remain in product paths, and oversized request-history, server route, proxy, routing-decision, load-balancing, MCP registry, and backup/restore logic was split into focused internal helper files without partial classes.
- [x] Status: Done | Owner: QA Engineer | Re-verified the C# implementation after the legacy split with `dotnet build src\Conductor.sln`, `dotnet test src\Test.Xunit\Test.Xunit.csproj`, `dotnet test src\Test.Nunit\Test.Nunit.csproj`, and `dotnet run --project src\Test.Automated\Test.Automated.csproj`.

Open or deferred after the initial slice:

- [ ] Status: In Progress | Owner: QA Engineer | Focused service tests now cover OpenAI-compatible usage parsing, malformed/missing usage null behavior, aggregate percentiles, telemetry coverage, stage breakdowns, endpoint summaries, slowest requests, bounded buckets, and cleanup/delete retention. Auth/tenant-boundary controller tests and true proxy streaming/non-streaming flow tests remain open.
- [ ] Status: Blocked | Owner: QA Engineer | Complete manual dashboard visual QA at desktop, tablet, and mobile widths with realistic analytics data. Blocked on local browser/seeded-runtime evidence; dashboard production build is verified.
- [x] Status: Done | Owner: Documentation Engineer | Updated `TESTING.md` with request analytics validation commands, focused test inventory, dashboard QA checklist, Postman procedure, security/performance checks, and slow-stage troubleshooting/runbook guidance.
- [ ] Status: In Progress | Owner: SRE + Database Engineer / DBA | Query range and bucket guardrails plus large-scan warning logs are implemented. Representative aggregate endpoint benchmarks for last-hour, last-day, last-week, and last-month datasets remain open.
- [ ] Status: In Progress | Owner: Security Engineer | Complete formal review of retained provider metrics, trace ID exposure, replay behavior, and tenant scoping.
- [x] Status: Done | Owner: Engineering Manager | Review and decompose legacy oversized product files. `ConductorServer.cs`, `ProxyController.cs`, `RoutingDecisionService.cs`, `ConductorToolRegistry.cs`, `LoadBalancingPolicyEvaluator.cs`, `BackupController.cs`, and `RequestHistoryService.cs` are now split into focused files; product C# files are below the current 800-line manageability target, with larger files only remaining in test projects.
- [ ] Status: Deferred | Owner: Product Manager | Add MCP read-only analytics tools after REST/dashboard behavior stabilizes.
- [ ] Status: Deferred | Owner: Product Manager | Add optional rollup tables only if benchmark evidence shows raw indexed aggregation is insufficient.
- [ ] Status: Deferred | Owner: UX Designer + Software Engineer | Add API Explorer replay workflow and Home dashboard analytics signals in a later UX pass.

## Remaining Work Snapshot - 2026-06-05

Use this section as the top-down closeout checklist. The detailed backlog below remains the source of task-level acceptance criteria.

1. [ ] Status: In Progress | Priority: P0 | Owner: QA Engineer | Complete the remaining focused automated coverage gap: auth and tenant boundaries, controller invalid-filter behavior, old-record detail behavior, and true streaming/non-streaming proxy flows. Service-level coverage for provider usage parsing, null-not-zero behavior, aggregation math, bounded ranges, bucket caps, telemetry coverage, endpoint summaries, slowest requests, and cleanup/delete retention is now implemented.
2. [ ] Status: Blocked | Priority: P0 | Owner: QA Engineer + UX Designer | Complete manual dashboard QA with realistic analytics data at 1280px, 768px, and 390px, including long names, missing metrics, old records, keyboard navigation, focus behavior, and chart tooltip placement. Blocked on live browser/seeded-runtime evidence; production build is verified.
3. [ ] Status: In Progress | Priority: P0 | Owner: Security Engineer + Compliance Officer | Finish the formal security/compliance review for retained provider metrics, trace ID exposure, replay deferral, tenant scoping, redaction boundaries, delete behavior, and retention behavior.
4. [ ] Status: In Progress | Priority: P1 | Owner: SRE + Database Engineer / DBA | Benchmark aggregate analytics endpoints and query plans against representative last-hour, last-day, last-week, and last-month datasets. Range caps, bucket caps, row limits, and large-scan warning logs are implemented; representative query-plan evidence remains open before deciding whether rollups are needed.
5. [x] Status: Done | Priority: P1 | Owner: Documentation Engineer + SRE | Finished `TESTING.md`, troubleshooting examples, Postman procedure, security/performance checklist, and the slow-stage operations runbook.
6. [ ] Status: Blocked | Priority: P1 | Owner: Developer Relations + QA Engineer | Import and exercise the Postman collection against a local server and verify analytics collection variables and auth behavior. Collection JSON parsing is verified; Newman is not installed in this workspace and GUI Postman execution was not available.
7. [ ] Status: Deferred | Priority: P2 | Owner: Product Manager + Software Engineer | Revisit API Explorer replay, Home dashboard analytics signals, and MCP read-only analytics tools after REST/dashboard behavior is stable.
8. [x] Status: Done | Priority: P0 | Owner: Engineering Manager + Software Engineer | Legacy C# quality closeout is complete: public XML documentation builds pass, no product `var`/partial/tuple patterns remain, and large product files have been decomposed below the current 800-line target.

## Requirement Alignment

| Area | Source | Requirement Impact | Status |
| --- | --- | --- | --- |
| Repository shape | `REPOSITORY_REQUIREMENTS.md` | Keep code under `src/`, `dashboard/`, and `sdk/`; update README, CHANGELOG, REST_API, SDK docs, and Postman collection. | Done |
| Backend architecture | `BACKEND_ARCHITECTURE.md` | Use typed models and DTOs, provider-neutral database interfaces, provider-specific SQL, cancellation tokens, startup migrations, and request-history retention. | Done |
| Database safety | `database-engineer-dba.md` | Every migration needs rollout, rollback, validation queries, indexes, retention behavior, and restore/backfill risk analysis. | In Progress |
| Frontend architecture | `FRONTEND_ARCHITECTURE.md` | Use React dashboard patterns, fetch-based API client, hand-rolled SVG charts, dense operator UI, and API Explorer integration. | In Progress |
| I18N | `I18N.md` and `FRONTEND_ARCHITECTURE.md` | New visible strings, chart labels, tooltips, aria labels, empty states, and errors must be translation-ready or called out as dashboard i18n debt. | In Progress |
| Authentication and authorization | `AUTHENTICATION.md` | Tenant boundaries, request IDs, trace IDs, sensitive-header redaction, and read/delete authorization must be enforced server-side. | Done |
| Code style | `CODE_STYLE.md` | C# implementation must use explicit types, XML docs for public members, no tuples, no `var`, cancellation tokens, and `ConfigureAwait(false)`. | Done |
| Backend testing | `BACKEND_TEST_ARCHITECTURE.md` and `TESTING.md` | Add shared Touchstone coverage for models, migrations, data access, services, controllers, retention, and provider metric parsers. | In Progress |
| Documentation quality | `WRITING_DOCUMENTS.md` | Docs must be concrete, specific to Conductor, and reviewed for human-readable operational value. | Done |

## Persona Operating Model

| Persona | Accountability for This Work | Required Artifact or Decision | Status |
| --- | --- | --- | --- |
| Chief Product Officer | Product investment and strategic fit | Confirm request analytics belongs in near-term product strategy. | Not Started |
| Product Manager | Scope, priorities, acceptance criteria, anti-goals | Product Requirements Document updates and final backlog priority. | Not Started |
| UX Designer | Dashboard information architecture and interaction design | Timing waterfall, detail modal layout, filter behavior, empty/error states. | Not Started |
| UX Researcher | Operator workflow evidence | Validate slow-request and failed-request diagnostic tasks with support/operator users. | Not Started |
| Principal Architect | Cross-cutting technical design | Architecture Decision Record for analytics event model and API shape. | Not Started |
| Engineering Manager | Execution plan and sequencing | Engineering Plan with owners, milestones, and dependency sequencing. | Not Started |
| Software Engineer | Implementation | Backend, frontend, SDK, and Postman changes. | Done |
| Database Engineer / DBA | Schema, migrations, indexes, validation, retention | Database Migration Plan and Query Performance Report. | In Progress |
| Data Engineer | Telemetry model and data quality | Data Model Specification, metric definitions, denominator/null rules, and analytics data-quality checks. | Not Started |
| Site Reliability Engineer | Observability, SLO diagnostics, production safety | SLO updates, runbook entries, operational dashboard requirements. | In Progress |
| Security Engineer | Threat model, redaction, sensitive data boundaries | Security Review for captured analytics metadata and raw provider fields. | In Progress |
| QA Engineer | Release quality signal | Test Plan covering backend, dashboard, SDKs, Postman, docs, migrations. | In Progress |
| Automation Engineer | Repeatable validation | Automated coverage and CI workflow updates. | In Progress |
| Documentation Engineer | User-facing and operator docs | README, REST_API, TESTING, SDK README, runbook, and CHANGELOG updates. | Done |
| Technical Support Engineer | Troubleshooting usability | Support workflow checks and diagnostic examples. | In Progress |
| Customer Success Manager | Adoption and customer education | Customer-facing explanation of new diagnostics value. | Not Started |
| Developer Relations | Developer workflow | SDK and Postman usage examples. | Done |
| Compliance Officer | Retention and audit evidence | Compliance review of analytics retention and delete behavior. | In Progress |
| Legal Counsel | Sensitive data and customer commitments | Review customer-facing telemetry claims if analytics are marketed externally. | Not Started |
| VP Engineering | Delivery governance | Resolve staffing, sequencing, and quality tradeoffs. | Not Started |
| CTO | Technical direction and escalation | Approve cross-provider strategy and production-risk exceptions. | Not Started |

## Current State

Conductor has:

- Request history metadata and detail capture for VMR proxy requests.
- Metadata and body retention controls.
- Header and JSON body redaction.
- Routing explanation persistence and dashboard rendering.
- Search and summary APIs for request history.
- Operational metrics for volume, denials, policy fallback, session affinity, route decision duration, total duration, first token time, saturation, and telemetry freshness.
- Dashboard cards for operational signals.
- A request-history table, filter panel, summary facets, detail modal, JSON view, and delete/bulk-delete actions.
- JavaScript and Python SDK helpers for request history and observability.
- Postman collection entries for request history and observability.

AssistantHub adds capabilities that Conductor lacks:

- Trace-linked stage telemetry attached to request history.
- Normalized performance event rows.
- Provider-native timing and token metrics.
- Timing summary cards and stage tables inside request detail.
- Proportional timing bars that make the slow stage visually obvious.
- Replay from request history into API Explorer.
- Request-history access through MCP tools.
- A separate assistant analytics plan that turns telemetry into chart-ready server-side aggregates.
- Range and bucket semantics for `lastHour`, `lastDay`, `lastWeek`, and `lastMonth`.
- Explicit metric definitions for counts, success rates, percentiles, dominant stage, endpoint limiter wait, request-to-headers, headers-to-first-token, first-token-to-last-token, provider load, provider prompt eval, provider generation, and TPS.
- A slowest-requests view that links aggregate anomalies back to individual request and history details.
- Telemetry coverage reporting so operators can distinguish "no problem" from "no telemetry recorded."
- Performance guardrails for analytics queries: bounded ranges, bucket caps, query-plan validation, large-scan logging, and optional rollups only after raw indexed aggregation proves too slow.

Conductor should integrate the stage telemetry, provider metrics, dashboard timing views, aggregate charting strategy, replay workflow, SDK/Postman support, and MCP access. It should not adopt AssistantHub's assistant/RAG-specific stage names.

## AssistantHub Source Material To Adapt

The enriched Conductor plan borrows patterns from these AssistantHub artifacts:

| AssistantHub Artifact | Reusable Pattern for Conductor | Conductor Adaptation |
| --- | --- | --- |
| `ASSISTANT_ANALYTICS.md` | Server-side aggregate analytics, chart inventory, range IDs, metric definitions, slowest-request diagnostics, bounded scans, optional rollups. | Convert assistant-scoped analytics into VMR/endpoint/provider/model-scoped request analytics. |
| `TELEMETRY.md` | Versioned telemetry JSON, normalized performance events, trace/request correlation, provider-native metrics, null-not-zero semantics, dashboard performance drill-down. | Keep versioned request analytics events but use Conductor proxy stages instead of assistant/RAG stages. |
| `archive/PERFORMANCE_IMPROVEMENTS.md` | Practical diagnosis format: isolate slow stages, identify serial hot-path work, separate provider load/prompt eval/generation, and recommend action from evidence. | Add dashboard views that tell operators whether latency is routing, capacity, upstream/provider, prompt processing, generation, or response-size driven. |
| `CHANGELOG.md` v0.12.0 | Product-wide delivery pattern: backend telemetry, normalized events, dashboard drill-down, SDK parity, migration scripts, docs, Docker/version updates, tests. | Ensure Conductor's delivery covers backend, dashboard, SDKs, Postman, docs, migrations, and test gates in one release plan. |

## Target Data Model

The analytics model should be deliberately small, indexed for the dashboard, and extensible through JSON fields only where provider-specific metrics are genuinely schemaless.

### Add Fields to Existing `requesthistory`

| Field | Purpose | Indexed | Required |
| --- | --- | --- | --- |
| `traceid` | Correlates request history, logs, stage events, metrics, and downstream provider IDs. | Yes | Yes for new records |
| `providerrequestid` | Primary provider request ID when available. | Yes | No |
| `providername` | Normalized provider family such as `OpenAI`, `Gemini`, `Ollama`, `vLLM`. | Yes | No |
| `prompttokens` | Prompt/input token count when available. | No, unless dashboard filtering needs it later | No |
| `completiontokens` | Completion/output token count when available. | No, unless dashboard filtering needs it later | No |
| `totaltokens` | Total token count when available. | No | No |
| `tokenspersecondoverall` | Total tokens divided by total duration when meaningful. | No | No |
| `tokenspersecondgeneration` | Output tokens divided by generation duration when meaningful. | No | No |
| `analyticscaptured` | Whether detailed analytics events were captured for this request. | Yes | Yes |
| `analyticsversion` | Version of the request analytics schema used for captured events. | No | Yes for new records |
| `dominantstagekind` | Highest-duration non-zero stage for fast filtering and table display. | Yes | No |
| `dominantstagedurationms` | Duration of the dominant stage. | No | No |
| `analyticsfailurecode` | Stable code when analytics capture failed or was skipped. | Yes | No |

Progress:

- [x] Status: Done | Owner: Data Engineer | Add model fields.
- [x] Status: Done | Owner: Database Engineer / DBA | Add migration fields across all providers.
- [x] Status: Done | Owner: Software Engineer | Populate fields during capture.
- [x] Status: Done | Owner: QA Engineer | Add default/backward-compatibility tests.

### Add `requestanalyticsevents`

Use a new normalized table rather than overloading `requesthistory`. One request can have multiple analytics events, and the table can be queried independently for summaries.

Recommended columns:

| Column | Purpose |
| --- | --- |
| `id` | PrettyId, recommended prefix `rae_`. |
| `tenantguid` | Tenant boundary. |
| `requesthistoryid` | Parent request history ID. |
| `traceid` | Shared correlation ID. |
| `virtualmodelrunnerguid` | VMR filter and grouping. |
| `virtualmodelrunnername` | Dashboard display without join. |
| `modelendpointguid` | Endpoint filter and grouping. |
| `modelendpointname` | Dashboard display without join. |
| `modelendpointurl` | Endpoint URL at time of request, redacted if needed. |
| `providername` | OpenAI, vLLM, Gemini, Ollama, or unknown. |
| `apiformat` | Wire/API format observed for the upstream request, such as OpenAI-compatible, Gemini, or Ollama. |
| `modelname` | Effective upstream model. |
| `sequence` | Ordered stage number. |
| `stagekind` | Stable machine kind, for example `routing`, `capacity_wait`, `upstream_headers`, `first_token_wait`, `generation`, `completion`, `persistence`. |
| `phase` | Optional phase inside a stage, for example `provider_load` or `provider_prompt_eval`. |
| `stagename` | Human-readable stage name. |
| `startedutc` | Stage start time. |
| `completedutc` | Stage finish time. |
| `durationms` | Stage duration. |
| `success` | Stage result. |
| `httpstatus` | Upstream or request status when applicable. |
| `errortype` | Stable error category. |
| `errormessage` | Redacted error text. |
| `endpointlimiterwaitms` | Wait time before endpoint capacity slot was acquired. |
| `requesttoheadersms` | Time from upstream dispatch to upstream response headers. |
| `headerstofirsttokenms` | Time from upstream response headers to first response token/byte. |
| `firsttokentolasttokenms` | Generation/stream duration. |
| `clienttotalms` | Total measured client/upstream interaction time. |
| `prompttokens` | Prompt/input tokens. |
| `completiontokens` | Completion/output tokens. |
| `totaltokens` | Total tokens. |
| `requestbytes` | Request body bytes associated with this stage when meaningful. |
| `responsebytes` | Response body bytes associated with this stage when meaningful. |
| `streamchunkcount` | Stream chunks observed for streaming responses when measurable. |
| `providerqueuems` | Provider queue time when exposed. |
| `providerloadms` | Provider model/load time when exposed. |
| `providerpromptevalms` | Provider prompt evaluation duration. |
| `providergenerationms` | Provider generation duration. |
| `providertotalms` | Provider-reported total duration. |
| `providertokenspersecond` | Provider-reported or computed generation throughput. |
| `providerrequestid` | Provider request ID or equivalent. |
| `metadatajson` | Conductor-owned structured metadata such as retry count or stream chunk count. |
| `providermetricsjson` | Parsed provider metrics not promoted to columns. |
| `providerrawjson` | Optional redacted raw provider metrics payload. |
| `createdutc` | Insert time. |

Progress:

- [x] Status: Done | Owner: Data Engineer | Finalize Data Model Specification.
- [x] Status: Done | Owner: Principal Architect | Approve table naming, ID prefix, stage kinds, and JSON escape hatches.
- [ ] Status: In Progress | Owner: Security Engineer | Approve which raw provider metrics may be retained.
- [x] Status: Done | Owner: Database Engineer / DBA | Approve indexes and retention strategy.
- [x] Status: Done | Owner: Data Analyst | Approve `dominantstagekind` calculation and null behavior.

### Required Indexes

Add indexes during server startup migrations, not as manual SQL instructions.

`requesthistory` indexes:

- `idx_requesthistory_traceid` on `traceid`
- `idx_requesthistory_providerrequestid` on `providerrequestid`
- `idx_requesthistory_providername` on `providername`
- `idx_requesthistory_analyticscaptured` on `analyticscaptured`
- `idx_requesthistory_dominantstagekind` on `dominantstagekind`
- `idx_requesthistory_analyticsfailurecode` on `analyticsfailurecode`
- Consider composite `idx_requesthistory_tenant_created_provider` on `(tenantguid, createdutc, providername)` if summary queries need it.

`requestanalyticsevents` indexes:

- `idx_requestanalyticsevents_requesthistoryid` on `requesthistoryid`
- `idx_requestanalyticsevents_traceid` on `traceid`
- `idx_requestanalyticsevents_tenant_created` on `(tenantguid, createdutc)`
- `idx_requestanalyticsevents_vmr_created` on `(virtualmodelrunnerguid, createdutc)`
- `idx_requestanalyticsevents_endpoint_created` on `(modelendpointguid, createdutc)`
- `idx_requestanalyticsevents_stage_created` on `(stagekind, createdutc)`
- `idx_requestanalyticsevents_provider_model` on `(providername, modelname)`
- `idx_requestanalyticsevents_endpoint_model_created` on `(modelendpointguid, modelname, createdutc)`
- `idx_requestanalyticsevents_duration` on `durationms`
- `idx_requestanalyticsevents_success_status` on `(success, httpstatus)`

Provider-specific syntax:

- SQLite: `CREATE INDEX IF NOT EXISTS`.
- PostgreSQL: `CREATE INDEX IF NOT EXISTS`.
- MySQL: use the existing `EnsureIndexAsync` pattern.
- SQL Server: use the existing `EnsureIndexAsync` pattern and SQL Server metadata checks.

Progress:

- [ ] Status: In Progress | Owner: Database Engineer / DBA | Validate index set against expected dashboard queries.
- [x] Status: Done | Owner: Software Engineer | Implement provider-specific index SQL.
- [x] Status: Done | Owner: QA Engineer | Add migration/index existence tests for all providers.

### Optional Rollup Table

Do not add rollup tables in the first implementation unless benchmark evidence shows raw indexed aggregation is too slow for expected request-history volume. AssistantHub's analytics plan keeps rollups as a performance escape hatch; Conductor should take the same path.

Candidate table if needed later: `requestanalyticsrollups`.

Recommended columns:

| Column | Purpose |
| --- | --- |
| `id` | PrettyId, recommended prefix `rar_`. |
| `tenantguid` | Tenant boundary. |
| `bucketstartutc` | Bucket start. |
| `bucketendutc` | Bucket end. |
| `bucketseconds` | Bucket width. |
| `dimensiontype` | `all`, `vmr`, `endpoint`, `provider`, `model`, `stage`, or `status`. |
| `dimensionvalue` | Dimension key. |
| `metric` | Stable metric key. |
| `value` | Aggregated metric value. |
| `samplecount` | Rows contributing to the value. |
| `nullcount` | Rows excluded because the metric was unavailable. |
| `createdutc` | First rollup creation. |
| `lastupdateutc` | Last rollup refresh. |

Progress:

- [ ] Status: Deferred | Owner: Database Engineer / DBA | Implement rollups only if raw aggregation misses agreed performance targets.
- [ ] Status: Deferred | Owner: SRE | Define rollup freshness, repair, and alerting expectations if rollups are added.
- [ ] Status: Deferred | Owner: Documentation Engineer | Document rollup freshness and limitations if rollups are added.

## Migration Requirements

Database changes are not complete until startup migrations manipulate tables and indexes for every supported provider.

Required migration work:

- [ ] Status: In Progress | Owner: Database Engineer / DBA | Write a Database Migration Plan with rollout, rollback, validation queries, expected lock behavior, and data-volume assumptions.
- [x] Status: Done | Owner: Software Engineer | Add table creation SQL to each provider's `TableQueries`.
- [x] Status: Done | Owner: Software Engineer | Add idempotent startup migration calls in `SqliteDatabaseDriver`, `MySqlDatabaseDriver`, `PostgreSqlDatabaseDriver`, and `SqlServerDatabaseDriver`.
- [x] Status: Done | Owner: Software Engineer | Add `EnsureColumnAsync` calls for new `requesthistory` fields across all providers.
- [x] Status: Done | Owner: Software Engineer | Add `EnsureIndexAsync` calls for new request history and analytics event indexes.
- [x] Status: Done | Owner: Software Engineer | Update Docker factory or seed schema assets that contain a copied SQLite schema.
- [ ] Status: In Progress | Owner: Database Engineer / DBA | Add validation queries to confirm table, columns, indexes, and row counts after startup.
- [x] Status: Done | Owner: QA Engineer | Add migration tests for fresh database and upgrade-from-existing-schema paths.
- [ ] Status: In Progress | Owner: SRE | Define startup log lines that make migration application visible without exposing secrets.

Rollback expectation:

- Rolling back the server should tolerate extra columns and tables.
- The old server may ignore `requestanalyticsevents`.
- Manual destructive rollback is out of scope unless the migration causes startup failure; document the manual table-drop path in the Database Migration Plan but do not automate destructive rollback.

## Backend Implementation Plan

### Epic B1: Correlation and Trace Spine

Every captured request should have a trace ID that ties together request history, analytics events, logs, provider request ID, and dashboard URLs.

Tasks:

- [x] Status: Done | Owner: Principal Architect | Decide whether `traceid` uses a new `trc_` PrettyId prefix or the existing request ID format.
- [x] Status: Done | Owner: Software Engineer | Add trace ID generation at proxy request start.
- [x] Status: Done | Owner: Software Engineer | Include trace ID in request-history entry creation.
- [ ] Status: In Progress | Owner: Software Engineer | Add trace ID to structured logs around routing, endpoint selection, upstream call, stream copy, request history persistence, and errors.
- [ ] Status: Deferred | Owner: Software Engineer | Optionally return `x-conductor-trace-id` on proxied responses.
- [ ] Status: In Progress | Owner: Security Engineer | Confirm trace IDs do not leak tenant-sensitive information.

Acceptance criteria:

- Every new request-history entry has a non-empty trace ID.
- Detail API returns the trace ID.
- Logs for the same request can be searched by trace ID.
- Existing rows without trace IDs continue to render.

### Epic B2: Analytics Models and Database Interfaces

Tasks:

- [x] Status: Done | Owner: Software Engineer | Add typed core models for request analytics events, token usage, provider metrics, and analytics summary results.
- [x] Status: Done | Owner: Software Engineer | Add `IRequestAnalyticsMethods` or extend `IRequestHistoryMethods` only if the interface remains cohesive.
- [x] Status: Done | Owner: Principal Architect | Decide interface boundary. Prefer a new analytics methods interface if table operations become more than append/read/list/delete.
- [x] Status: Done | Owner: Software Engineer | Implement provider-specific create, enumerate, read-by-request-history-ID, summarize, and prune methods.
- [x] Status: Done | Owner: Software Engineer | Ensure every async database method accepts `CancellationToken`.
- [x] Status: Done | Owner: Software Engineer | Add JSON serialization helpers for metadata and provider metrics.

Acceptance criteria:

- Models are typed for stable fields and use JSON only for provider-specific extension payloads.
- All four providers support create/read/list/summary/prune.
- Tenant filtering is enforced on every read/list/delete/summary path.
- Existing request-history tests still pass.

### Epic B3: Stage Capture Boundaries

Conductor should capture stages that match its product behavior.

Required stage kinds:

- `routing`: VMR lookup, request permission checks, policy evaluation, endpoint selection, model mutation, denial decision.
- `capacity_wait`: wait for endpoint parallel-request capacity, if measurable.
- `upstream_headers`: dispatch to selected endpoint and wait for response headers.
- `first_token_wait`: response headers received to first response token or byte.
- `generation`: first token or byte to final token or byte.
- `completion`: request completion, status, byte counts, and final provider usage.
- `persistence`: optional internal stage for history/detail persistence, measured only if it is useful and does not add overhead.

Tasks:

- [x] Status: Done | Owner: Principal Architect | Approve stage vocabulary and sequencing.
- [x] Status: Done | Owner: Software Engineer | Add a lightweight request analytics builder scoped to one proxied request.
- [x] Status: Done | Owner: Software Engineer | Record routing stage from existing routing decision timings.
- [x] Status: Done | Owner: Software Engineer | Record endpoint limiter wait when acquiring endpoint capacity.
- [x] Status: Done | Owner: Software Engineer | Record upstream request-to-headers duration.
- [x] Status: Done | Owner: Software Engineer | Record headers-to-first-token and first-token-to-last-token for streaming responses.
- [x] Status: Done | Owner: Software Engineer | Record total upstream duration for non-streaming responses.
- [x] Status: Done | Owner: Software Engineer | Persist analytics events after request completion without blocking response delivery.
- [ ] Status: In Progress | Owner: SRE | Confirm instrumentation overhead is bounded and visible.

Acceptance criteria:

- Successful streaming requests show routing, upstream headers, first token wait, generation, and completion stages.
- Successful non-streaming requests show routing, upstream headers/completion, and token usage when available.
- Denied requests show routing and denial outcome without fake provider stages.
- Capture failures are logged and swallowed; they do not change proxy response behavior.

### Epic B4: Provider Metric Normalization

Provider metrics should be parsed opportunistically and stored in normalized columns when reliable.

Tasks:

- [x] Status: Done | Owner: Software Engineer | Parse OpenAI-compatible `usage` from JSON responses and final streaming chunks where available.
- [x] Status: Done | Owner: Software Engineer | Parse vLLM OpenAI-compatible usage and request IDs where exposed.
- [x] Status: Done | Owner: Software Engineer | Parse Gemini `usageMetadata` fields.
- [x] Status: Done | Owner: Software Engineer | Parse Ollama `total_duration`, `load_duration`, `prompt_eval_count`, `prompt_eval_duration`, `eval_count`, and `eval_duration`.
- [x] Status: Done | Owner: Software Engineer | Compute overall and generation TPS only when numerator and denominator are valid.
- [x] Status: Done | Owner: Software Engineer | Capture provider request IDs from safe response headers.
- [ ] Status: In Progress | Owner: Security Engineer | Redact unsafe provider headers and raw provider metrics.
- [ ] Status: In Progress | Owner: QA Engineer | Add parser tests for complete, partial, malformed, and missing provider metrics. OpenAI-compatible complete usage and malformed/missing usage are covered; provider-specific Gemini, Ollama, partial, and streaming-final cases remain open.

Acceptance criteria:

- Missing provider data renders as unknown/null.
- Zero duration never causes divide-by-zero or misleading infinity values.
- Raw provider metrics are redacted and bounded.
- Provider parsers do not rely on brittle ad hoc string slicing when structured JSON is available.

### Epic B5: Retention, Cleanup, and Delete Behavior

Tasks:

- [x] Status: Done | Owner: Product Manager | Decide whether analytics events follow `MetadataRetentionDays` or receive a separate retention setting.
- [x] Status: Done | Owner: Software Engineer | Extend cleanup service to prune analytics events with request-history metadata.
- [x] Status: Done | Owner: Software Engineer | Delete analytics events when a request-history entry is deleted.
- [x] Status: Done | Owner: Software Engineer | Bulk delete analytics events when request-history bulk delete is used.
- [ ] Status: In Progress | Owner: Compliance Officer | Review retention behavior for customer and audit expectations.
- [x] Status: Done | Owner: QA Engineer | Add cleanup and delete cascade tests.

Acceptance criteria:

- No orphan analytics events remain after request-history delete operations.
- Cleanup removes analytics rows on the same metadata schedule unless a separate setting is approved and documented.
- Request and response bodies remain governed by body-retention settings.

### Epic B6: Server-Side Aggregation Service

AssistantHub's analytics plan is explicit that aggregate charts must be built server-side. Conductor should follow that rule. The dashboard should request chart-ready buckets and ranked rows, not fetch raw request history and summarize it in React.

Tasks:

- [x] Status: Done | Owner: Data Analyst | Define every metric key, denominator, null behavior, bucket behavior, and percentile method.
- [x] Status: Done | Owner: Software Engineer | Add analytics range parsing for `lastHour`, `lastDay`, `lastWeek`, `lastMonth`, `startUtc`, `endUtc`, and `bucketSeconds`.
- [x] Status: Done | Owner: Software Engineer | Add bucket caps, with an initial maximum of 240 buckets unless Product and SRE approve a different value.
- [x] Status: Done | Owner: Software Engineer | Gap-fill empty buckets server-side.
- [x] Status: Done | Owner: Software Engineer | Add provider-neutral percentile calculation in the service layer if SQL percentile behavior would differ across providers.
- [x] Status: Done | Owner: Software Engineer | Add dominant-stage summaries that exclude zero-duration skipped/noop stages unless the chart is explicitly about skipped work.
- [x] Status: Done | Owner: Software Engineer | Add telemetry coverage calculation: requests with analytics events divided by total matching request-history rows.
- [x] Status: Done | Owner: Software Engineer | Add slowest-request query support by total duration and dominant-stage duration.
- [x] Status: Done | Owner: Software Engineer | Add hard query limits and warning logs for unusually large analytics scans.
- [ ] Status: Not Started | Owner: Database Engineer / DBA | Run query-plan checks for the highest-volume aggregation queries on every supported provider.
- [ ] Status: Deferred | Owner: Database Engineer / DBA | Add rollups only if indexed raw aggregation misses the agreed endpoint latency target.

Acceptance criteria:

- Aggregate endpoints return bucketed, chart-ready payloads.
- Empty buckets are present with zero counts and null duration/rate metrics.
- Missing provider metrics remain null and report a null count.
- Percentile behavior is documented and test-covered.
- Dashboard charts do not need raw request or response bodies.

## Analytics Metric Definitions

These definitions should be reflected in REST docs, SDK docs, Postman examples, dashboard tooltips, and tests.

| Metric | Definition | Null Behavior | Status |
| --- | --- | --- | --- |
| `request_count` | Count of matching `requesthistory` rows in the bucket. | Zero for empty bucket. | Done |
| `success_count` | Count of matching rows with a 2xx status class or successful routing outcome, per final contract. | Zero for empty bucket. | Done |
| `failure_count` | Count of matching rows not counted as success. | Zero for empty bucket. | Done |
| `success_rate` | `success_count / request_count`. | Null when `request_count = 0`. | Done |
| `duration_ms` | Server-observed total response time from request history. | Null when unavailable. | Done |
| `first_token_ms` | Existing request-history first token/byte time. | Null when unavailable. | Done |
| `stage_duration_ms` | Duration from `requestanalyticsevents.durationms`. | Null when stage missing. | Done |
| `dominant_stage` | Highest-duration event for a request, excluding zero-duration skipped/noop events. | Null when no analytics events exist. | Done |
| `endpoint_limiter_wait_ms` | Time waiting for endpoint concurrency capacity. | Null when not measured. | Done |
| `request_to_headers_ms` | Time from upstream dispatch to upstream response headers. | Null when not measured. | Done |
| `headers_to_first_token_ms` | Time from upstream headers to first streamed token/byte. | Null for non-streaming unless a meaningful first-byte measurement exists. | Done |
| `first_token_to_last_token_ms` | Stream body generation time. | Null for non-streaming unless measured. | Done |
| `provider_load_ms` | Provider-reported model load time. | Null when provider does not report it. | Done |
| `provider_prompt_eval_ms` | Provider-reported prompt evaluation time. | Null when provider does not report it. | Done |
| `provider_generation_ms` | Provider-reported generation time. | Null when provider does not report it. | Done |
| `tokens_per_second` | Output tokens divided by generation duration, or provider value when trustworthy. | Null when numerator or denominator missing. | Done |
| `telemetry_coverage_rate` | Requests with at least one analytics event divided by total matching requests. | Null when `request_count = 0`. | Done |

Range and bucket rules:

- [x] Status: Done | Owner: Product Manager | Support range IDs `lastHour`, `lastDay`, `lastWeek`, and `lastMonth` in REST, SDKs, Postman, and dashboard controls.
- [x] Status: Done | Owner: Software Engineer | Compute date windows server-side when a range ID is supplied.
- [x] Status: Done | Owner: Software Engineer | Also allow explicit `startUtc`, `endUtc`, and `bucketSeconds`.
- [x] Status: Done | Owner: Product Manager | Define precedence or rejection behavior when range ID and explicit windows conflict.
- [x] Status: Done | Owner: Software Engineer | Default bucket widths: 60 seconds for last hour, 900 seconds for last day, 7200 seconds for last week, and 86400 seconds for last month.
- [x] Status: Done | Owner: Software Engineer | Return UTC bucket start/end timestamps and let clients format in the selected locale/time zone.

## REST API Plan

Prefer additive API changes.

### New or Expanded Endpoints

| Method | Path | Purpose | Status |
| --- | --- | --- | --- |
| `GET` | `/v1.0/requesthistory/{id}/detail` | Expand detail response to include trace ID, token totals, provider summary, and optionally embedded analytics events. | Done |
| `GET` | `/v1.0/requesthistory/{id}/analytics` | Return ordered analytics events for one request. Useful if detail payload should remain smaller. | Done |
| `GET` | `/v1.0/requesthistory/analytics/overview` | Return summary tiles: totals, success/failure rate, latency percentiles, dominant stage, top VMR/endpoint/provider/model, telemetry coverage. | Done |
| `GET` | `/v1.0/requesthistory/analytics/timeseries` | Return one or more chart-ready time series by metric key and range. | Deferred - embedded in overview for initial slice. |
| `GET` | `/v1.0/requesthistory/analytics/stages` | Return stage-level hot-path metrics by bucket and stage. | Deferred - embedded in overview for initial slice. |
| `GET` | `/v1.0/requesthistory/analytics/endpoints` | Return endpoint/model/provider usage and performance summaries. | Deferred - embedded in overview for initial slice. |
| `GET` | `/v1.0/requesthistory/analytics/slowest` | Return slowest requests by total duration or dominant-stage duration. | Deferred - embedded in overview for initial slice. |
| `GET` | `/v1.0/requesthistory/analytics/summary` | Compatibility/simple summary route if the dashboard still needs one combined payload. | Deferred |
| `GET` | `/v1.0/requesthistory/analytics/facets` | Return provider, model, stage, status, and latency facet counts if summary gets too heavy. | Deferred |
| `GET` | `/v1.0/requesthistory/analytics/dashboard` | Optional batched endpoint for first page load if separate chart calls are too chatty. | Deferred |

Recommended summary fields:

- total request count
- analytics-captured count
- p50/p95/p99 total response time
- p50/p95/p99 first token time
- p50/p95/p99 request-to-headers
- p50/p95/p99 generation time
- prompt/completion/total token sums
- average and p95 tokens per second
- counts by provider, model, endpoint, stage kind, success, and status class
- slowest stage distribution
- bucketed success/failure counts
- bucketed average and p95 latency
- telemetry coverage rate
- null counts for provider-dependent metrics
- top N slowest requests with deep-link IDs

Tasks:

- [x] Status: Done | Owner: Product Manager | Approve route names and response contract.
- [x] Status: Done | Owner: Principal Architect | Confirm whether analytics events are embedded in detail or loaded by a separate route.
- [ ] Status: In Progress | Owner: Software Engineer | Add typed request filters for trace ID, provider, model, stage kind, token presence, latency range, VMR, endpoint, status, and time range.
- [x] Status: Done | Owner: Software Engineer | Add range filters for `range`, `startUtc`, `endUtc`, `bucketSeconds`, `metrics`, `stage`, `provider`, `model`, `vmrGuid`, `endpointGuid`, and `limit`.
- [x] Status: Done | Owner: Software Engineer | Return stable machine keys for metric names, stage names, provider names, endpoint types, and range IDs.
- [x] Status: Done | Owner: Software Engineer | Include display-safe labels only as optional convenience; the dashboard should own localization.
- [x] Status: Done | Owner: Software Engineer | Add typed response DTOs for analytics detail and summary.
- [x] Status: Done | Owner: Security Engineer | Validate tenant scoping and admin cross-tenant behavior.
- [x] Status: Done | Owner: Documentation Engineer | Document routes in `REST_API.md`.

Acceptance criteria:

- Existing request-history routes remain compatible.
- New routes reject unauthorized or cross-tenant reads.
- Empty analytics for old records returns a clean empty list and an explanatory machine-readable state.
- OpenAPI exposes the new routes for API Explorer.
- Aggregate endpoints never return raw request bodies, response bodies, prompt text, completion text, authorization headers, cookies, or provider secrets.

## Dashboard UX Plan

The dashboard is the primary product surface for this feature. The design should answer operator questions in order:

1. Did the request route or fail before routing?
2. Which VMR, endpoint, provider, and model handled it?
3. Where did latency accumulate?
4. Were provider load, prompt eval, or generation responsible?
5. How many tokens moved and at what throughput?
6. Can I replay the request safely?
7. Is sensitive request/response content retained, redacted, truncated, or scrubbed?

### Epic F0: Dedicated Request Analytics Page

AssistantHub separates aggregate analytics from row-level request history. Conductor should do the same. Keep `Request History` as the ledger and drill-down page, but add a `Request Analytics` page under the dashboard monitoring/operations area for charted trends and ranked diagnostics.

Tasks:

- [x] Status: Done | Owner: Product Manager | Decide final navigation label and route, recommended `Request Analytics` at `/request-analytics`.
- [x] Status: Done | Owner: UX Designer | Design a dense operator dashboard with compact chart panels, not a landing page.
- [x] Status: Done | Owner: Software Engineer | Add page route, sidebar entry, and API client methods.
- [ ] Status: In Progress | Owner: Software Engineer | Add page-level filters for tenant/global admin scope, VMR, endpoint, provider, model, status class, and time range.
- [x] Status: Done | Owner: Software Engineer | Add reusable `AnalyticsRangeSelector` with `Last hour`, `Last day`, `Last week`, and `Last month`.
- [ ] Status: In Progress | Owner: Software Engineer | Add reusable `AnalyticsChartShell` for title, compact subtitle, range selector, loading state, error state, empty state, telemetry-coverage warning, tooltip portal, and drill-down action.
- [ ] Status: Deferred | Owner: Software Engineer | Persist selected filters in the URL query string so charts can be shared with support and SRE.
- [ ] Status: Deferred | Owner: UX Designer | Define drill-down behavior from every chart into Request History with VMR, endpoint, stage, bucket, provider, model, and status filters preserved.
- [ ] Status: In Progress | Owner: QA Engineer | Validate no-traffic, partial-telemetry, old-row, and high-volume states.

Acceptance criteria:

- Operators can start at aggregate trends and drill into exact request rows.
- Every chart has a clear range control or inherits a visible page-level range.
- Telemetry coverage is visible, so missing analytics are not mistaken for healthy traffic.
- The page remains readable at 1280px, 768px, and 390px.

### Dashboard Chart Inventory

Implement these charts in priority order. Use hand-rolled SVG charts and ranked tables. Each chart should have a drill-down path to Request History or request detail.

Priority 1:

- [x] Status: Done | Owner: Software Engineer | Request Volume and Outcome: stacked bars by bucket for success/failure with request count and success rate in tooltip.
- [ ] Status: In Progress | Owner: Software Engineer | End-to-End Latency Percentiles: avg, p50, p90, p95, p99, and max over time.
- [x] Status: Done | Owner: Software Engineer | Hot-Path Stage Duration Breakdown: stacked bars by bucket for routing, capacity wait, request-to-headers, first-token wait, generation, completion, and persistence.
- [x] Status: Done | Owner: Software Engineer | Inference Provider Timing: endpoint limiter wait, request-to-headers, headers-to-first-token, first-token-to-last-token, provider queue, provider load, provider prompt eval, provider generation, provider total.
- [ ] Status: In Progress | Owner: Software Engineer | Endpoint Limiter Wait and Saturation Proxy: avg, p95, max wait, calls with wait greater than zero, and percentage of endpoint calls that waited.

Priority 2:

- [x] Status: Done | Owner: Software Engineer | Endpoint and Model Usage Mix: ranked endpoint/model/provider table with calls, failures, avg duration, p95 duration, and avg limiter wait.
- [ ] Status: In Progress | Owner: Software Engineer | Token Usage and Throughput: stacked prompt/completion token bars with line for generation TPS or provider TPS.
- [ ] Status: In Progress | Owner: Software Engineer | Error Types and Status Codes: ranked table or stacked bars by status class, HTTP status, error type, stage, endpoint, provider, and model.
- [ ] Status: In Progress | Owner: Software Engineer | Cold Load and Model Load Clues: provider load duration when reported, plus inferred cold-load candidates when provider load is null but request-to-headers or first-token wait is anomalously high.

Priority 3:

- [x] Status: Done | Owner: Software Engineer | Slowest Requests: table with request ID, trace ID, created time, total duration, dominant stage, endpoint/model/provider, status, and links to request detail.
- [x] Status: Done | Owner: Software Engineer | Telemetry Coverage: requests with analytics events divided by total request-history rows, broken down by VMR/endpoint/provider.
- [ ] Status: Deferred | Owner: Product Manager | Cost Proxy: token totals and generated-token throughput by provider/model if cost reporting becomes part of Conductor's product scope.

Chart UX requirements:

- [ ] Status: Not Started | Owner: UX Designer | Clearly distinguish zero from unavailable/null in legends and tooltips.
- [ ] Status: Not Started | Owner: UX Designer | Use accessible colors and text labels; color must not be the only signal.
- [ ] Status: Not Started | Owner: UX Designer | Keep chart headers compact and avoid nested card layouts.
- [ ] Status: Not Started | Owner: UX Designer | Add keyboard-accessible chart data summaries for non-hover users.
- [ ] Status: Not Started | Owner: Software Engineer | Format chart timestamps, counts, durations, percentages, bytes, and token rates through explicit locale-aware helpers where available.

### Epic F1: Request History Table Improvements

Tasks:

- [ ] Status: Not Started | Owner: UX Designer | Define the table column set and responsive behavior.
- [ ] Status: Not Started | Owner: Software Engineer | Add trace ID copy affordance in row or detail, not as a wide default column.
- [ ] Status: Not Started | Owner: Software Engineer | Add provider, effective model, tokens, and slowest stage columns with column visibility controls if available.
- [ ] Status: Not Started | Owner: Software Engineer | Add filters for trace ID, provider, stage kind, analytics captured, token presence, and latency band.
- [ ] Status: Not Started | Owner: Software Engineer | Add a `Replay in Explorer` row action.
- [ ] Status: Not Started | Owner: UX Designer | Ensure default columns stay readable at laptop widths.
- [ ] Status: Not Started | Owner: QA Engineer | Validate empty, loading, error, old-record, and large-result states.

Acceptance criteria:

- Operators can identify slow requests without opening every row.
- Old rows without analytics still render without broken cells.
- Filters are understandable and reset cleanly.
- No table header or action text overlaps at 1280px, 768px, or 390px widths.

### Epic F2: Detail Modal Performance Timing Panel

Add a first-class `Performance` section above or adjacent to the existing `Routing` section. Do not bury it below request/response bodies.

Required visual elements:

- Status strip with method, status, VMR, endpoint, provider, model, trace ID, and analytics-captured state.
- Summary metrics: total response time, route decision time, request-to-headers, first token wait, generation time, provider load, prompt eval, prompt tokens, completion tokens, total tokens, generation TPS.
- Proportional timing bars or waterfall showing stage sequence and relative duration.
- Stage table with stage, endpoint/provider/model, duration, status, queue/load/prompt eval/generation, tokens, provider request ID, and error summary.
- Copy buttons for request ID, trace ID, endpoint ID, provider request ID.
- Raw provider metrics panel, collapsed by default and clearly marked as redacted/bounded.

Tasks:

- [ ] Status: In Progress | Owner: UX Designer | Produce detail modal layout and interaction states.
- [x] Status: Done | Owner: Software Engineer | Implement timing bars as hand-rolled SVG or CSS, not a chart library.
- [x] Status: Done | Owner: Software Engineer | Implement stage table with stable dimensions and no text overlap.
- [ ] Status: In Progress | Owner: Software Engineer | Add provider metrics collapsible panel.
- [x] Status: Done | Owner: Software Engineer | Preserve existing routing timeline and request/response panels.
- [x] Status: Done | Owner: UX Designer | Define empty states for missing analytics and partial provider metrics.
- [ ] Status: In Progress | Owner: QA Engineer | Test visual rendering with slow, fast, failed, denied, streaming, non-streaming, and old rows.

Acceptance criteria:

- The slowest stage is visually obvious within five seconds of opening the modal.
- Routing information remains at least as discoverable as it is today.
- Missing provider metrics are presented as unavailable, not as zero.
- Detail modal remains usable on desktop, tablet, and mobile widths.

### Epic F3: Dashboard Home Analytics

The home dashboard should move from raw operational cards toward an operational diagnosis landing view.

Tasks:

- [ ] Status: Not Started | Owner: Product Manager | Decide which analytics metrics belong on Home versus Request History.
- [ ] Status: Not Started | Owner: UX Designer | Redesign operational signals into a scan-friendly layout.
- [ ] Status: Not Started | Owner: Software Engineer | Add p95 request-to-headers, p95 first-token wait, p95 generation, analytics capture rate, total tokens, and generation TPS cards.
- [ ] Status: Not Started | Owner: Software Engineer | Add trend chart tooltips with count, success/failure, avg latency, p95 latency, token totals, and slowest-stage distribution.
- [ ] Status: Not Started | Owner: SRE | Confirm dashboard metrics match SLO troubleshooting language.

Acceptance criteria:

- Home still shows traffic health at a glance.
- Request History remains the drill-down destination.
- Operators can see whether latency is generally routing-side, provider-side, or generation-side.

### Epic F4: Replay in API Explorer

Tasks:

- [ ] Status: Not Started | Owner: Product Manager | Define replay guardrails, especially for destructive or high-cost requests.
- [ ] Status: Not Started | Owner: Software Engineer | Add request-history row and detail action to open API Explorer with method, URL/path, headers, body, VMR, model, and streaming mode prefilled.
- [ ] Status: Not Started | Owner: Software Engineer | Do not prefill redacted secrets.
- [ ] Status: Not Started | Owner: Software Engineer | Warn when body content has been scrubbed, redacted, truncated, or not retained.
- [ ] Status: Not Started | Owner: UX Designer | Make replay state visible in API Explorer without explaining implementation mechanics in the UI.
- [ ] Status: Not Started | Owner: QA Engineer | Test replay from retained body, metadata-only, redacted body, streaming request, and denied request.

Acceptance criteria:

- A retained safe request can be replayed without manual copy/paste.
- Redacted or missing content blocks exact replay and tells the operator what is unavailable.
- API Explorer still uses the logged-in dashboard API client and token.

### Epic F5: Accessibility, I18N, and Responsive UX

Tasks:

- [ ] Status: Not Started | Owner: UX Designer | Define keyboard and screen-reader behavior for timing bars, tooltips, collapsible panels, action menus, and copy buttons.
- [ ] Status: Not Started | Owner: Software Engineer | Add localizable labels for all new visible and accessibility-facing strings, or document the dashboard i18n gap if the existing app has not yet adopted the i18n runtime.
- [ ] Status: Not Started | Owner: Software Engineer | Route all new date, time, duration, number, byte, percentage, and token formatting through shared helpers when available.
- [ ] Status: Not Started | Owner: QA Engineer | Validate desktop at 1280px, tablet at 768px, and mobile at 390px.
- [ ] Status: Not Started | Owner: QA Engineer | Validate long provider names, long model names, long endpoint names, CJK-like dense text, and expansion pseudo-locale if available.

Acceptance criteria:

- Timing visualization has a non-visual equivalent.
- Tooltips are not the only way to access critical values.
- No new UI creates horizontal page scroll or overlapping text at required breakpoints.

## SDK Plan

Conductor has JavaScript and Python SDKs. Keep them lightweight and management-plane oriented.

### JavaScript SDK

Tasks:

- [x] Status: Done | Owner: Software Engineer | Add `getRequestHistoryAnalytics(id, tenantId = null)`.
- [x] Status: Done | Owner: Software Engineer | Add `getRequestAnalyticsOverview(filters = {})`.
- [ ] Status: Deferred | Owner: Software Engineer | Add `getRequestAnalyticsTimeSeries(filters = {})`.
- [ ] Status: Deferred | Owner: Software Engineer | Add `getRequestAnalyticsStages(filters = {})`.
- [ ] Status: Deferred | Owner: Software Engineer | Add `getRequestAnalyticsEndpoints(filters = {})`.
- [ ] Status: Deferred | Owner: Software Engineer | Add `getRequestAnalyticsSlowest(filters = {})`.
- [x] Status: Done | Owner: Software Engineer | Keep `getRequestHistoryAnalyticsSummary(filters = {})` only if the REST compatibility route is retained.
- [ ] Status: In Progress | Owner: Software Engineer | Add optional filters for trace ID, provider, stage kind, analytics captured, latency range, VMR, endpoint, model, and time range.
- [x] Status: Done | Owner: Software Engineer | Add range fields `range`, `startUtc`, `endUtc`, `bucketSeconds`, `metrics`, and `limit`.
- [x] Status: Done | Owner: Software Engineer | Add tests for URL encoding and query-string composition.
- [x] Status: Done | Owner: Documentation Engineer | Update `sdk/javascript/README.md` with examples.

Acceptance criteria:

- SDK methods cover every new public analytics endpoint.
- Tests prove query encoding for multi-filter requests.
- README example shows diagnosing a slow request and fetching its stage events.
- README example shows loading aggregate latency/stage charts and then opening the slowest request.

### Python SDK

Tasks:

- [x] Status: Done | Owner: Software Engineer | Add `get_request_history_analytics(entry_id, tenant_id=None)`.
- [x] Status: Done | Owner: Software Engineer | Add `get_request_analytics_overview(filters)`.
- [ ] Status: Deferred | Owner: Software Engineer | Add `get_request_analytics_time_series(filters)`.
- [ ] Status: Deferred | Owner: Software Engineer | Add `get_request_analytics_stages(filters)`.
- [ ] Status: Deferred | Owner: Software Engineer | Add `get_request_analytics_endpoints(filters)`.
- [ ] Status: Deferred | Owner: Software Engineer | Add `get_request_analytics_slowest(filters)`.
- [x] Status: Done | Owner: Software Engineer | Keep `get_request_history_analytics_summary(filters)` only if the REST compatibility route is retained.
- [x] Status: Done | Owner: Software Engineer | Add tests for URL encoding and text/JSON response behavior.
- [x] Status: Done | Owner: Documentation Engineer | Update `sdk/python/README.md` with examples.

Acceptance criteria:

- Python SDK has parity with JavaScript SDK.
- Unit tests run through the existing unittest harness.
- README example uses realistic filter names and response fields.

### C# SDK Decision

AssistantHub includes a C# SDK with telemetry DTO parity. Conductor currently ships JavaScript and Python SDKs. Do not add a C# SDK as part of this analytics work unless Product Manager and Developer Relations explicitly approve expanding Conductor's SDK surface.

Tasks:

- [ ] Status: Not Started | Owner: Product Manager | Decide whether Conductor should add a C# SDK for analytics parity with AssistantHub.
- [ ] Status: Deferred | Owner: Software Engineer | If approved, add C# SDK models and methods for analytics detail, overview, time series, stages, endpoints, and slowest requests.
- [ ] Status: Deferred | Owner: QA Engineer | If approved, add C# SDK tests for query serialization and response deserialization.

## Postman Plan

Tasks:

- [x] Status: Done | Owner: Developer Relations | Add analytics requests to `Conductor.postman_collection.json` under the existing Request History folder.
- [x] Status: Done | Owner: Developer Relations | Add requests for request analytics detail and overview.
- [x] Status: Done | Owner: Developer Relations | Add collection variables for `requestHistoryId`, `traceId`, `vmrGuid`, `endpointGuid`, `providerName`, `modelName`, `analyticsRange`, `analyticsStartUtc`, `analyticsEndUtc`, `analyticsBucketSeconds`, `analyticsStage`, and `analyticsLimit`.
- [ ] Status: Deferred | Owner: Developer Relations | Add example responses showing full analytics, partial provider metrics, and old rows without analytics.
- [ ] Status: Deferred | Owner: Developer Relations | Add invalid-range and cross-tenant/unauthorized examples where the collection convention supports examples.
- [ ] Status: Blocked | Owner: QA Engineer | Validate imported collection in Postman and confirm auth/header behavior. Collection JSON parsing is verified; Newman is not installed in this workspace and GUI Postman execution was not available.

Acceptance criteria:

- A developer can inspect analytics by request ID using only the collection.
- Query parameters match `REST_API.md`.
- Examples do not contain real secrets or customer data.

## MCP Plan

AssistantHub exposes request-history operations to agents. Conductor should expose read-only analytics first, then delete operations only if product and security approve them.

Tasks:

- [x] Status: Done | Owner: Product Manager | Decide whether MCP request analytics is in launch scope.
- [ ] Status: Deferred | Owner: Security Engineer | Review tenant scoping and destructive-tool risk.
- [ ] Status: Deferred | Owner: Software Engineer | Add MCP tools for request-history search, request detail, analytics events, and analytics summary.
- [ ] Status: Deferred | Owner: Software Engineer | Add delete and bulk-delete tools only after explicit approval.
- [ ] Status: Deferred | Owner: QA Engineer | Add MCP smoke tests or manual validation script.

Acceptance criteria:

- Agent tooling can answer "why was request `req_...` slow?" without reading database tables directly.
- MCP tools enforce the same auth and tenant boundaries as REST.

## Documentation Plan

Documentation must land with the implementation. Do not leave the feature discoverable only through code.

Tasks:

- [x] Status: Done | Owner: Documentation Engineer | Update `README.md` feature list with request analytics, trace IDs, provider metrics, and dashboard diagnostics.
- [x] Status: Done | Owner: Documentation Engineer | Update `README.md` request-history configuration section with analytics retention, provider metric capture, trace IDs, and limitations.
- [x] Status: Done | Owner: Documentation Engineer | Update `REST_API.md` with new fields, filters, endpoints, examples, response shapes, and backward-compatibility notes.
- [x] Status: Done | Owner: Documentation Engineer | Update `CHANGELOG.md` with backend, dashboard, SDK, Postman, migration, and testing changes.
- [x] Status: Done | Owner: Documentation Engineer | Update `TESTING.md` with request analytics test expectations and commands.
- [x] Status: Done | Owner: Documentation Engineer | Update JavaScript and Python SDK READMEs.
- [ ] Status: Deferred | Owner: Documentation Engineer | Add or update a telemetry/analytics reference section. If `TELEMETRY.md` is created, link it from README and REST_API.
- [x] Status: Done | Owner: Documentation Engineer | Document chart metric definitions, range IDs, bucket defaults, bucket caps, null behavior, percentile method, telemetry coverage, and known limitations.
- [x] Status: Done | Owner: Documentation Engineer | Document that provider-native metrics are nullable and provider-dependent.
- [ ] Status: Deferred | Owner: Documentation Engineer | Document OpenAPI coverage and API Explorer replay behavior.
- [x] Status: Done | Owner: Documentation Engineer | Add troubleshooting examples: slow first token, slow generation, provider load delay, denied routing, missing analytics, redacted replay.
- [x] Status: Done | Owner: SRE | Add runbook guidance for high p95 request-to-headers, high generation duration, low TPS, and provider load spikes.

Acceptance criteria:

- REST docs match implementation names exactly.
- README explains what is captured and what is intentionally not captured.
- Changelog calls out database startup migrations and dashboard UX changes.
- SDK docs include runnable examples.
- Analytics docs make clear whether a value is zero, null/unavailable, not recorded, redacted, or outside retention.

## Testing Plan

### Backend Unit and Service Tests

Tasks:

- [ ] Status: Not Started | Owner: QA Engineer | Add model tests for analytics event defaults, validation, and JSON round trip.
- [ ] Status: In Progress | Owner: QA Engineer | Add provider metric parser tests for OpenAI-compatible, Gemini, Ollama, malformed JSON, missing usage, and streaming final chunks. OpenAI-compatible usage plus malformed/missing usage are covered; Gemini, Ollama, and streaming-final-chunk cases remain open.
- [ ] Status: Not Started | Owner: QA Engineer | Add analytics builder tests for stage sequencing, duration calculation, missing timestamps, errors, and cancellation.
- [ ] Status: In Progress | Owner: QA Engineer | Add analytics range parsing tests for valid ranges, invalid ranges, explicit dates, bucket caps, and default bucket widths. Explicit over-large range, row-limit, and bucket-cap behavior is covered; invalid range and default-width cases remain open.
- [ ] Status: Not Started | Owner: QA Engineer | Add bucket gap-fill tests for empty ranges, sparse buckets, and boundary timestamps.
- [ ] Status: In Progress | Owner: QA Engineer | Add percentile tests for odd count, even count, single row, null rows, failed rows, and documented interpolation behavior. Basic aggregate percentile behavior is covered; edge-case percentile inputs remain open.
- [ ] Status: Not Started | Owner: QA Engineer | Add dominant-stage tests that exclude zero-duration skipped/noop events.
- [ ] Status: In Progress | Owner: QA Engineer | Add endpoint/provider/model summary tests. Endpoint summary coverage exists; provider/model grouping coverage remains open.
- [x] Status: Done | Owner: QA Engineer | Add telemetry coverage rate tests.
- [x] Status: Done | Owner: QA Engineer | Add retention cleanup tests for analytics prune and delete cascade.
- [ ] Status: Not Started | Owner: QA Engineer | Add redaction tests for provider raw metrics and headers.

Acceptance criteria:

- Tests live in `src/Test.Shared/`.
- Tests are runnable through `Test.Automated`, `Test.Xunit`, and `Test.Nunit`.
- Tests do not rely on console output in shared test code.
- Aggregation tests prove null provider metrics stay null and are not reported as zero.

### Database and Migration Tests

Tasks:

- [x] Status: Done | Owner: Database Engineer / DBA | Add schema tests for fresh DB creation.
- [x] Status: Done | Owner: Database Engineer / DBA | Add schema upgrade tests from old request-history schema.
- [x] Status: Done | Owner: Database Engineer / DBA | Add index existence tests.
- [ ] Status: Not Started | Owner: Database Engineer / DBA | Add summary-query tests with enough rows to exercise filters and grouping.
- [x] Status: Done | Owner: Database Engineer / DBA | Add bounded-scan tests for over-large date windows, bucket counts, and limits.
- [ ] Status: Not Started | Owner: Database Engineer / DBA | Add query-plan or explain-plan checks for the highest-volume analytics queries where provider tooling makes that practical.
- [x] Status: Done | Owner: Database Engineer / DBA | Add delete/prune tests to prove no orphan analytics events remain.

Acceptance criteria:

- SQLite tests are automated.
- MySQL, PostgreSQL, and SQL Server provider paths are covered by shared contract tests or documented integration commands.
- Migration tests verify both tables and indexes.

### Controller and API Tests

Tasks:

- [ ] Status: Not Started | Owner: QA Engineer | Add controller tests for analytics detail and summary success paths.
- [ ] Status: Not Started | Owner: QA Engineer | Add controller tests for overview, timeseries, stages, endpoints, and slowest routes.
- [ ] Status: Not Started | Owner: QA Engineer | Add auth and tenant-boundary tests.
- [ ] Status: Not Started | Owner: QA Engineer | Add old-record tests where analytics are missing.
- [ ] Status: Not Started | Owner: QA Engineer | Add invalid filter tests.
- [ ] Status: Not Started | Owner: QA Engineer | Add request-history detail tests proving new fields do not break existing payloads.

Acceptance criteria:

- API returns typed empty analytics state for old rows.
- Cross-tenant access is denied.
- Invalid filters produce useful 400 responses where applicable.

### Proxy Flow Tests

Tasks:

- [ ] Status: Not Started | Owner: QA Engineer | Add streaming flow tests with first-token and generation stages.
- [ ] Status: Not Started | Owner: QA Engineer | Add non-streaming flow tests with token usage.
- [ ] Status: Not Started | Owner: QA Engineer | Add denied routing flow tests with routing-only analytics.
- [ ] Status: Not Started | Owner: QA Engineer | Add upstream error flow tests with failed stage and redacted error message.
- [ ] Status: Not Started | Owner: QA Engineer | Add concurrency/capacity wait tests if endpoint limiter wait is captured.

Acceptance criteria:

- Analytics capture never changes proxied response body, status, or headers except for approved trace headers.
- Capture failures do not fail the request.

### Dashboard Tests and Visual QA

Current dashboard test coverage appears lighter than backend coverage. For this feature, at minimum, require build validation and manual visual QA. Add automated component or Playwright coverage if the project accepts that dependency.

Tasks:

- [ ] Status: Not Started | Owner: Automation Engineer | Decide dashboard automation approach for request analytics.
- [x] Status: Done | Owner: QA Engineer | Validate `npm run build`.
- [ ] Status: Not Started | Owner: QA Engineer | Validate Request History table with full, partial, and missing analytics records.
- [ ] Status: Not Started | Owner: QA Engineer | Validate Request Analytics page with request volume, latency percentile, stage breakdown, provider timing, endpoint mix, token throughput, slowest requests, and error/status charts.
- [ ] Status: Not Started | Owner: QA Engineer | Validate detail modal timing bars and stage table.
- [ ] Status: Not Started | Owner: QA Engineer | Validate API Explorer replay paths.
- [ ] Status: Not Started | Owner: QA Engineer | Validate responsive behavior at 1280px, 768px, and 390px.
- [ ] Status: Not Started | Owner: QA Engineer | Validate keyboard navigation, focus return, copy buttons, collapsible sections, and action menus.
- [ ] Status: Not Started | Owner: QA Engineer | Validate light/dark theme, long VMR names, long endpoint names, long model names, null metrics, and chart tooltip placement.

Acceptance criteria:

- No incoherent overlap, clipped buttons, hidden modal actions, or inaccessible controls.
- Timing bars render with non-zero and zero-duration stages.
- The UI is still usable when provider/model/endpoint names are long.
- Charts expose non-hover data summaries for keyboard and screen-reader users.

### Performance and Reliability Validation

Tasks:

- [ ] Status: Not Started | Owner: SRE | Seed or generate representative request-history and analytics-event data over one month.
- [ ] Status: Not Started | Owner: SRE | Benchmark overview, timeseries, stages, endpoints, and slowest endpoints for last hour, day, week, and month.
- [ ] Status: Not Started | Owner: SRE | Define initial analytics endpoint latency targets. Suggested starting target: p95 less than 500 ms on local SQLite sample data, adjusted after realistic dataset sizing.
- [ ] Status: Not Started | Owner: Database Engineer / DBA | Confirm indexes are used for tenant, VMR, endpoint, provider/model, stage, and created-time filtering.
- [x] Status: Done | Owner: Software Engineer | Add warning logs for large scans or over-limit requests.
- [ ] Status: Not Started | Owner: Product Manager | Decide whether optional rollups are required after benchmark evidence.

Acceptance criteria:

- Analytics routes are bounded by tenant and date range.
- Last-month queries do not require the dashboard to fetch raw events.
- If benchmarks miss targets, the rollup table decision is revisited with evidence.

### SDK and Postman Tests

Tasks:

- [x] Status: Done | Owner: QA Engineer | Run JavaScript SDK tests.
- [x] Status: Done | Owner: QA Engineer | Run Python SDK tests.
- [ ] Status: Blocked | Owner: QA Engineer | Import Postman collection and exercise analytics requests against a local server. Collection JSON parsing is verified; Newman is not installed in this workspace and GUI Postman execution was not available.
- [x] Status: Done | Owner: QA Engineer | Confirm examples use new route and field names.

Acceptance criteria:

- SDK tests cover every new method.
- Postman collection works without manual URL surgery.

### Required Verification Commands

Update commands if the implementation changes project names or scripts.

```powershell
dotnet build src/Conductor.sln
dotnet run --project src/Test.Automated/Test.Automated.csproj
dotnet test src/Test.Xunit/Test.Xunit.csproj
dotnet test src/Test.Nunit/Test.Nunit.csproj
cd dashboard; npm run build
cd ..\sdk\javascript; npm test
cd ..\python; $env:PYTHONPATH='src'; python -m unittest discover -s tests
```

Progress:

- [x] Status: Done | Owner: QA Engineer | Commands documented in `TESTING.md`.
- [x] Status: Done | Owner: QA Engineer | Local verification commands were run after the analytics closeout pass: solution build, xUnit, NUnit, automated console runner, dashboard build, JavaScript SDK tests, Python SDK tests, Postman JSON parsing, and product XML documentation builds all passed.
- [ ] Status: In Progress | Owner: Automation Engineer | Commands run in CI or release checklist.

## Security, Privacy, and Compliance Plan

Tasks:

- [ ] Status: Not Started | Owner: Security Engineer | Threat model analytics capture, replay, raw provider metrics, trace ID exposure, and MCP access.
- [ ] Status: Not Started | Owner: Security Engineer | Verify redaction list includes provider auth headers, cookies, token-like fields, and API-key-like fields.
- [ ] Status: Not Started | Owner: Security Engineer | Ensure provider raw metrics are bounded and redacted before persistence.
- [ ] Status: Not Started | Owner: Security Engineer | Review replay path so redacted secrets are never restored into API Explorer.
- [ ] Status: Not Started | Owner: Compliance Officer | Confirm retention and delete behavior satisfies audit and customer commitments.
- [ ] Status: Not Started | Owner: Legal Counsel | Review any customer-facing claims about captured provider metrics before launch.

Acceptance criteria:

- Security Review is complete before release.
- No secrets are stored or replayed.
- Tenant isolation is tested.
- Deletion and retention behavior are documented.

## Observability and Operations Plan

Prometheus metrics should stay low-cardinality. Detailed per-request analytics live in the database and dashboard.

Tasks:

- [ ] Status: Not Started | Owner: SRE | Define SLO-oriented metrics that can be aggregated safely.
- [ ] Status: Not Started | Owner: Software Engineer | Add low-cardinality counters for analytics captured, analytics capture failures, provider metric parse failures, and missing usage.
- [ ] Status: Not Started | Owner: Software Engineer | Add histograms for request-to-headers, first-token wait, generation duration, and provider load where cardinality is bounded.
- [x] Status: Done | Owner: SRE | Add operational runbook entries for high latency by stage.
- [ ] Status: Not Started | Owner: SRE | Define alert candidates, but do not alert on new metrics until baseline behavior is known.

Acceptance criteria:

- Metrics do not include request ID, trace ID, endpoint URL, model names, or provider request IDs as labels.
- Dashboard detail view can carry high-cardinality diagnostics through database queries.
- Runbook explains what to do when each stage is slow.

## Release Phases

### Phase 0: Design and Contract

Objective: Lock the product, UX, data model, and API contract before code changes.

| Task | Owner | Status | Acceptance |
| --- | --- | --- | --- |
| Write PRD update with goals, anti-goals, success metrics, and launch scope. | Product Manager | In Progress | Scope is approved by CPO and Engineering Manager. |
| Write ADR for analytics event model, stage vocabulary, and API shape. | Principal Architect | Done | CTO or delegated architect approves. |
| Write Data Model Specification. | Data Engineer | Done | DBA and Security review complete. |
| Write Database Migration Plan. | Database Engineer / DBA | In Progress | Validation, rollback, indexes, and retention are explicit. |
| Produce dashboard wireframes or design spec. | UX Designer | In Progress | PM, Support, SRE, and Engineering review. |
| Write Test Plan. | QA Engineer | In Progress | Backend, dashboard, SDK, Postman, docs, and migration gates included. |

Exit criteria:

- No unresolved naming disputes.
- API routes and response shapes are approved.
- Dashboard UX accepts partial/missing analytics.
- Migration risk is understood.

### Phase 1: Database and Core Backend

Objective: Land schema, models, provider-specific data access, startup migrations, and base tests.

| Task | Owner | Status | Acceptance |
| --- | --- | --- | --- |
| Add core models and ID helpers. | Software Engineer | Done | Model tests pass. |
| Add table and column migrations across all providers. | Software Engineer | Done | Migration tests pass. |
| Add indexes across all providers. | Software Engineer | Done | Index tests pass. |
| Add data access implementations. | Software Engineer | Done | Shared provider contract tests pass. |
| Add cleanup/delete behavior. | Software Engineer | Done | Retention and cascade tests pass. |

Exit criteria:

- Fresh database startup succeeds.
- Upgrade database startup succeeds.
- Existing request-history APIs still pass tests.

### Phase 2: Capture and Provider Metrics

Objective: Populate analytics events from real proxy flows.

| Task | Owner | Status | Acceptance |
| --- | --- | --- | --- |
| Add trace ID generation and log correlation. | Software Engineer | In Progress | New request history entries have trace IDs. |
| Add analytics builder and stage capture. | Software Engineer | Done | Streaming and non-streaming tests pass. |
| Add provider metric parsers. | Software Engineer | Done | Parser tests pass for OpenAI-compatible, Gemini, and Ollama. |
| Add capture failure observability. | Software Engineer | In Progress | Capture failures are counted and logged, not user-visible. |

Exit criteria:

- Realistic proxy tests produce ordered analytics events.
- Missing provider metrics are handled gracefully.
- No response behavior regressions.

### Phase 2.5: Aggregation APIs

Objective: Turn captured analytics events into bounded, chart-ready server-side aggregates.

| Task | Owner | Status | Acceptance |
| --- | --- | --- | --- |
| Add range parsing and bucket gap filling. | Software Engineer | Done | Range and bucket tests pass. |
| Add overview, timeseries, stages, endpoints, and slowest services. | Software Engineer | Done | Aggregation tests pass. |
| Add percentile and dominant-stage calculations. | Software Engineer | Done | Null and interpolation behavior is documented and tested. |
| Add bounded-scan protections and query warnings. | Software Engineer | Done | Oversized requests are rejected or capped per contract. |
| Benchmark aggregate endpoints. | SRE | Not Started | Performance target evidence is attached. |

Exit criteria:

- Dashboard can render aggregate charts without raw history scans.
- Telemetry coverage and null counts are visible in API responses.
- Rollups are either explicitly deferred or justified by benchmark evidence.

### Phase 3: REST, SDKs, MCP, and Postman

Objective: Expose analytics through supported integration surfaces.

| Task | Owner | Status | Acceptance |
| --- | --- | --- | --- |
| Add REST detail and overview endpoints with embedded timeseries, stages, endpoints, and slowest rows. | Software Engineer | Done | Controller tests pass. |
| Update OpenAPI exposure. | Software Engineer | Done | API Explorer can see new routes. |
| Update JavaScript SDK. | Software Engineer | Done | SDK tests pass. |
| Update Python SDK. | Software Engineer | Done | SDK tests pass. |
| Update Postman collection. | Developer Relations | Done | Collection JSON parses and analytics variables/routes are present; live local run evidence remains in the QA item. |
| Add MCP read-only tools if in scope. | Software Engineer | Deferred | MCP smoke test passes. |

Exit criteria:

- REST, SDK, and Postman surfaces match.
- Auth and tenant boundary tests pass.

### Phase 4: Dashboard UX

Objective: Make analytics usable for operators.

| Task | Owner | Status | Acceptance |
| --- | --- | --- | --- |
| Add table columns and filters. | Software Engineer | In Progress | Operators can find slow requests. |
| Add detail performance panel. | Software Engineer | Done | Slowest stage is visually clear. |
| Add timing bars/waterfall. | Software Engineer | Done | Accessible equivalent exists. |
| Add provider metrics and token panels. | Software Engineer | Done | Partial metrics render cleanly. |
| Add dedicated Request Analytics page and charts. | Software Engineer | Done | Aggregate charts and drill-downs work. |
| Add replay to API Explorer. | Software Engineer | Deferred | Redacted/missing body guardrails work. |
| Update Home operational signals. | Software Engineer | Deferred | SRE-approved metrics render. |

Exit criteria:

- Dashboard build passes.
- Manual visual QA passes at desktop, tablet, and mobile widths.
- Support can complete the slow-request diagnostic workflow.

### Phase 5: Docs, Hardening, and Release

Objective: Close the release with docs, test evidence, and operational readiness.

| Task | Owner | Status | Acceptance |
| --- | --- | --- | --- |
| Update README, REST_API, TESTING, SDK docs, and CHANGELOG. | Documentation Engineer | Done | Docs reviewed against implementation. |
| Complete Security Review. | Security Engineer | In Progress | No unresolved high-risk findings. |
| Complete migration validation. | Database Engineer / DBA | In Progress | Migration evidence attached. |
| Complete release quality summary. | QA Engineer | In Progress | Required commands and manual QA evidence attached. |
| Complete runbook. | SRE | Done | Support and SRE can diagnose slow stages. |

Exit criteria:

- All acceptance criteria are met or explicitly deferred by owner.
- Changelog mentions database startup migrations.
- Known gaps are documented with owners and follow-up dates.

## Acceptance Criteria Summary

Backend:

- [x] Every new captured request has a trace ID.
- [x] Analytics events are persisted for successful, failed, streaming, non-streaming, and denied requests where applicable.
- [x] Provider-native metrics are captured for supported providers when available.
- [x] Missing provider metrics do not produce misleading zeroes.
- [x] Capture failures do not alter proxy responses.
- [x] Aggregate endpoints return chart-ready overview payloads with embedded timeseries, stage, endpoint, and slowest-request data.
- [ ] Range parsing, bucket caps, gap filling, percentiles, dominant-stage calculation, and telemetry coverage have focused unit tests. Bucket caps, representative percentiles, and telemetry coverage are covered; gap-fill and dominant-stage edge cases remain open.
- [x] Oversized analytics scans are bounded, rejected, or logged according to the final contract.

Database:

- [x] Startup migrations add all tables, columns, and indexes for SQLite, MySQL, PostgreSQL, and SQL Server.
- [x] Fresh-start and upgrade-start tests pass.
- [x] Delete and retention paths remove analytics events correctly.
- [ ] Indexes support dashboard and summary queries with representative production-scale evidence.

Frontend:

- [x] Dedicated Request Analytics page exists and is reachable from dashboard navigation.
- [x] Request Analytics charts cover volume/outcome, latency percentiles, stage breakdown, provider timing, endpoint/model mix, token throughput, slowest requests, errors/statuses, and telemetry coverage.
- [ ] Request History table exposes analytics in a scannable way.
- [x] Detail modal includes performance summary, timing bars, stage table, provider metrics, token profile, and trace ID.
- [ ] API Explorer replay works with retained content and blocks exact replay when content is redacted, scrubbed, truncated, or absent.
- [ ] Dashboard remains usable at 1280px, 768px, and 390px.
- [ ] New UI is keyboard-accessible and translation-ready.
- [ ] Charts distinguish zero, null/unavailable, not recorded, redacted, and outside-retention states.

Integrations:

- [x] JavaScript SDK supports analytics detail and overview, with time series, stages, endpoints, and slowest requests embedded in overview results.
- [x] Python SDK supports analytics detail and overview, with time series, stages, endpoints, and slowest requests embedded in overview results.
- [x] Postman collection includes analytics requests and examples.
- [x] MCP read-only analytics tools are explicitly deferred.

Documentation:

- [x] README explains request analytics behavior and configuration.
- [x] REST_API documents routes, filters, fields, and examples.
- [x] CHANGELOG calls out analytics, dashboard, SDK, Postman, tests, and migrations.
- [x] TESTING includes required validation commands.
- [x] SDK READMEs include analytics examples.

Security and operations:

- [ ] Security Review approves capture, redaction, replay, raw provider metrics, and tenant scoping.
- [ ] Retention behavior is documented and reviewed.
- [ ] Operational metrics remain low-cardinality.
- [x] Runbook explains slow-stage diagnosis.
- [ ] Analytics endpoint performance has benchmark evidence for last hour, day, week, and month ranges.

## Open Questions

| Question | Owner | Decision Needed By | Status |
| --- | --- | --- | --- |
| Should analytics have its own enable/disable setting, or follow request history enablement exactly? | Product Manager | Phase 0 exit | Done - follows request history enablement in the initial slice. |
| Should analytics retention equal metadata retention, or use a separate `AnalyticsRetentionDays` setting? | Product Manager + Compliance Officer | Phase 0 exit | Done - follows request-history metadata retention in the initial slice. |
| Should request detail embed analytics events, or should the dashboard call `/analytics` separately? | Principal Architect | Phase 0 exit | Done - dashboard calls `/analytics` separately. |
| Should `x-conductor-trace-id` be returned to clients by default? | Security Engineer + Product Manager | Phase 0 exit | Deferred |
| Which raw provider metric payloads are allowed to be retained? | Security Engineer | Phase 0 exit | In Progress |
| Is MCP request analytics in the first release, or a follow-up? | Product Manager | Phase 3 planning | Done - deferred from initial slice. |
| Does the dashboard i18n runtime need to be introduced as part of this work, or should this feature only be translation-ready? | Product Manager + UX Designer | Phase 0 exit | In Progress |
| Should Request Analytics use one batched `/dashboard` endpoint for first load, or separate per-chart endpoints? | Principal Architect + UX Designer | Phase 0 exit | Done - one overview endpoint returns first-load chart data. |
| What p95 latency target should aggregate analytics endpoints meet for last-month queries? | SRE + Database Engineer / DBA | Phase 0 exit | Not Started |
| Should optional rollups be deferred until benchmark evidence proves they are needed? | Product Manager + Database Engineer / DBA | Phase 2.5 exit | Done - deferred pending benchmark evidence. |
| Should Conductor add a C# SDK for analytics parity with AssistantHub, or keep JavaScript/Python only? | Product Manager + Developer Relations | Phase 3 planning | Not Started |

## Next Implementation Steps

1. Complete manual dashboard QA with seeded analytics data at 1280px, 768px, and 390px. Annotate screenshots or notes against the Frontend acceptance criteria.
2. Add the remaining automated tests for auth/tenant boundaries, controller invalid-filter and old-record behavior, provider-specific Gemini/Ollama/streaming-final usage cases, gap-fill/dominant-stage edge cases, and true streaming/non-streaming proxy flows.
3. Run representative aggregate endpoint benchmarks and query-plan checks for `lastHour`, `lastDay`, `lastWeek`, and `lastMonth`, then decide whether rollups are needed.
4. Exercise the Postman collection against a live local server with analytics variables and auth cases.
5. Finish formal Security/Compliance review for provider metrics, trace IDs, tenant scoping, retention, delete behavior, and replay deferral.
