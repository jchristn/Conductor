# Analytics Workspace Plan

Owner: Product Manager
Technical owner: Principal Architect
Execution owner: Engineering Manager
Created: 2026-06-14
Status: Not Started
Target surface: Dashboard Analytics workspace, backend analytics APIs, SDKs, Postman, documentation, and operations assets

## Progress Key

Use this file as the execution tracker. Mark checkboxes as work lands, and update the status phrase on each line when a task starts, blocks, reviews, or completes.

Status values:

- `Not Started`
- `In Progress`
- `Blocked`
- `In Review`
- `Done`
- `Deferred`

Priority values:

- `P0`: required for the first useful Analytics workspace release
- `P1`: required before calling the workspace production-ready
- `P2`: useful follow-up after the core workflow is stable

Risk values:

- `Low`
- `Medium`
- `High`

## Product Direction

Conductor already has request analytics: trace IDs, request-history analytics events, aggregate overview APIs, dashboard charts, slow-request drill-down, token counts, endpoint summaries, and observability metrics. That foundation is good, but the dashboard still treats analytics as one request-history page. Operators integrating Conductor into a broader IT stack need a workspace, not a chart. They need to answer operational, financial, capacity, compliance, and performance questions without exporting raw rows into a spreadsheet every time.

The Analytics workspace should become the command surface for AI consumption and model-serving operations. It should connect who used which model, through which VMR, on which endpoint, with what token volume, latency, policy result, and estimated cost. Over time it can add deeper hardware context. A slow completion is not just a slow HTTP request. It may be a queueing problem, a cold model, a prompt-evaluation bottleneck, a bad routing policy, a tenant abuse pattern, or a provider outage. The workspace should make those explanations visible.

This is not a replacement for the existing request analytics data path. It is a broader product workspace that enhances the same foundation. The dashboard can rename `Request Analytics` to `Analytics`, keep `/request-analytics` as a compatibility redirect, and grow the current page into a richer workspace instead of building a disconnected second analytics product.

## Clarified First-Release Decisions

These decisions are settled for the first implementation pass and should drive prioritization.

| Area | Decision |
| --- | --- |
| Navigation | Rename `Request Analytics` to `Analytics` in the dashboard nav. Keep the old route as a compatibility redirect or alias. |
| First-release scope | Ship TTFT and token usage analytics first. Include user drill-down and a simple cost estimate based on a user-supplied per-token unit cost. |
| Cost semantics | Cost is estimate-only. Do not present it as provider billing reconciliation, accounting-grade cost, or formal chargeback. |
| Access | System admins, tenant admins, and a new analytics-specific role can view analytics. User-level and credential-level drill-down requires one of those permissions. |
| Tenant scope | System admins see global analytics by default and can filter to one tenant. Tenant admins and other tenant-scoped users are forced into their tenant scope server-side. |
| Retention | Analytics is retained for 30 days. The dashboard and documentation must make this visible anywhere custom time ranges or exports are offered. |
| Time ranges | Use the same presets as the request volume chart: last hour, last day, last week, and last month. Add custom start/end selection and a granularity selector. |
| TTFT definition | Time-to-first-token means Conductor request received to first token received. Stage breakdowns can show the subcomponents that explain that total. |
| Token scope | Track prompt/input, completion/output, total, cached, and multimodal token fields where providers expose them. Missing provider usage remains unknown, not zero. |
| Usage denominator | Token and cost usage charts include successful completions only. Failed, denied, rate-limited, and cancelled requests belong in separate reliability/access reports. |
| Grouping | Usage and latency must support grouping by requested model, effective model, model definition, endpoint, VMR, tenant, user, credential, and provider. |
| Chargeback language | Use "cost estimate", "usage allocation", or "estimated cost by user/tenant/model" rather than formal chargeback/billback language for the first release. |
| Cost centers and departments | Cost centers, departments, applications, hardware-hour allocation, price books, currencies over time, and effective-date pricing are out of scope for the first release. |
| Hardware analytics | GPU, RigMonitor history, Prometheus/OpenTelemetry, capacity planning, and alerting are not first-release scope. Keep them as future analytics work only. |
| Operator quick answers | The first dashboard should answer: average TTFT for this user, tokens used by this user, and estimated cost for this user over the last day. |
| Reports | Saved reports are in scope. Scheduled delivery is out of scope. |
| Export formats | Plan for CSV, JSON, Parquet, PDF, and shareable dashboard links for exported/saved outputs. |
| Comparison mode | Do not build comparison mode in the first release. |
| SDKs | JavaScript, Python, and C# SDKs need first-class analytics helpers. |
| Postman | Postman examples should serve both operators and developers. |
| Primary consumer | Optimize the first API and UX shape for dashboard use. External BI and automation can follow after dashboard workflows are stable. |

## Explicit Outcomes

- [ ] Status: Not Started | Priority: P0 | Owner: Product Manager | Rename `Request Analytics` as `Analytics`, keep the existing route compatible, and document the dashboard nav/TOC change.
- [ ] Status: Not Started | Priority: P0 | Owner: UX Designer | Provide first-release workflows that answer average TTFT for a user, token usage for a user, and estimated user cost over the last day.
- [ ] Status: Not Started | Priority: P0 | Owner: Principal Architect | Define a stable analytics metric catalog with dimensions, null semantics, denominator rules, retention behavior, and backward-compatible API contracts.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Extend backend APIs so analytics are queried by model, endpoint, VMR, tenant, user, credential, provider, request type, model access policy, load-balancing policy, and time range.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add first-release dashboard tabs for overview, latency/TTFT, token usage, user breakdowns, saved reports, and estimate-only cost output.
- [ ] Status: Not Started | Priority: P1 | Owner: Data Engineer | Add rollup and export paths when raw indexed aggregation is not enough for 30-day reporting workflows.
- [ ] Status: Not Started | Priority: P1 | Owner: Documentation Engineer | Update `README.md`, `REST_API.md`, `CHANGELOG.md`, `TESTING.md`, SDK READMEs, and operator runbooks.
- [ ] Status: Not Started | Priority: P1 | Owner: Developer Relations | Add JavaScript SDK, Python SDK, C# SDK, Postman, and API Explorer parity for every new public route.

## Non-Goals

Analytics should not become a data lake inside Conductor. The workspace should answer operational questions from bounded, tenant-scoped, indexed data and export clean aggregates for downstream systems. Raw request bodies, response bodies, prompts, completions, secrets, bearer tokens, provider keys, cookies, and other sensitive material must not be copied into analytics tables or rollups.

The first release should not add one row per streamed token. Capture stream boundaries, counts, durations, and final provider usage where available. Token-level tracing is a different feature with much higher cost and privacy risk.

Prometheus metrics should remain low cardinality. High-cardinality dimensions such as user ID, credential ID, model name, endpoint name, trace ID, and request history ID belong in database-backed analytics APIs, not in metric labels.

The first release should not add GPU history, Prometheus/OpenTelemetry integration, capacity planning, alerting, cost centers, departments, application mapping, hardware-hour cost allocation, price books, scheduled report delivery, or model/endpoint comparison mode. Those are legitimate future analytics areas, but they should not block the TTFT, token usage, and estimate-only cost workflows.

## Existing State

Conductor currently has these useful pieces:

- `GET /v1.0/requesthistory/analytics/overview` returns chart-ready request counts, success/failure counts, latency percentiles, analytics coverage, token totals, time-series buckets, stage breakdowns, endpoint summaries, and slowest requests.
- `GET /v1.0/requesthistory/{id}/analytics` returns normalized analytics events for a single request history entry.
- `RequestHistoryEntry` already stores VMR, endpoint, model definition, model configuration, requested/effective model, requestor user, credential, model access decision, TTFT, response time, trace ID, provider request ID, token counts, token throughput, analytics coverage, and dominant stage.
- `RequestAnalyticsEvent` already stores stage timing, provider, API format, model name, limiter wait, upstream headers wait, first-token wait, generation time, request/response bytes, token counts, tokens per second, and redacted raw provider metrics.
- The dashboard has `RequestAnalytics.jsx`, a nav item, a time-series chart, stage breakdown, slowest-request drill-down, endpoint summary, and filters for range, VMR, endpoint, provider, and effective model.
- RigMonitor telemetry is already available through endpoint health and load-balancing policy surfaces, including CPU, memory, disk, network, GPU, Ollama, telemetry freshness, and health state.
- SDKs and Postman already include the current request analytics routes.

The current implementation is request-centric. The workspace plan below turns it into an operator analytics product with richer dimensions, saved views, estimate-only cost output, exports, and documentation. Hardware correlation, formal chargeback, and external observability integrations remain future work.

## Primary Gaps

The dashboard cannot yet answer several questions without manual correlation:

- Average TTFT by model endpoint and VMR, then drilled into a specific user or credential.
- Total prompt, completion, and total tokens over time by model, endpoint, VMR, tenant, user, or credential.
- Token throughput and generation performance by model and endpoint, separated from queue time and upstream header wait.
- Estimate-only cost by tenant, user, credential, model, endpoint, provider, and VMR from a user-supplied per-token unit cost.
- Which users, credentials, or tenants are driving bursts, failures, or cost.
- Whether a latency spike was caused by Conductor routing, capacity wait, upstream header wait, first-token wait, provider generation time, endpoint behavior, or client payload size.
- Whether load balancing is distributing traffic as intended.
- Whether model access policy enforcement is blocking or would block expected traffic.
- Whether provider usage telemetry is missing often enough to make cost reporting unreliable.
- Which endpoints, users, credentials, tenants, models, and VMRs are driving successful token usage, slow TTFT, failed requests, denied requests, rate limits, or estimated cost.

## Requirements Alignment

| Requirement Area | Source | Applicable Guidance | Plan Impact |
| --- | --- | --- | --- |
| Repository shape | `C:\Code\Agents\requirements\REPOSITORY_REQUIREMENTS.md` | Keep product code under `src/`, dashboard code under `dashboard/`, SDKs under `sdk/`, and maintain README, CHANGELOG, Postman, Docker, and docs assets. | All analytics work must land in existing repo structure and update the root documentation set. |
| Backend architecture | `C:\Code\Agents\requirements\BACKEND_ARCHITECTURE.md` | Use Watson 7 route registrars, typed models, provider-neutral database interfaces, provider-specific SQL, cancellation tokens, and startup migrations. | Add typed analytics filters/results, route module(s), database methods, migrations for SQLite/MySQL/PostgreSQL/SQL Server, and no `JsonElement` fixed contracts. |
| Backend style | `C:\Code\Agents\requirements\CODE_STYLE.md` | No `var`, no tuples, using directives inside namespaces, XML docs for public APIs, explicit cancellation, `ConfigureAwait(false)`, one class or enum per file. | Every new backend model, service, enum, and method must follow Conductor's strict C# bar. |
| Authentication | `C:\Code\Agents\requirements\AUTHENTICATION.md` | Tenant isolation, server-side authorization, stable request context, accounting, and no client-side-only security. | Analytics APIs must enforce tenant scope and admin semantics server-side, especially for user/credential drill-down and exports. |
| Test architecture | `C:\Code\Agents\requirements\BACKEND_TEST_ARCHITECTURE.md` | Shared Touchstone suites should run through xUnit, NUnit, and automated runners. | Analytics model, service, database, controller, retention, and security tests belong in `src/Test.Shared` and run through all runners. |
| Frontend architecture | `C:\Code\Agents\requirements\FRONTEND_ARCHITECTURE.md` | React/Vite dashboard, fetch-based API client, page-level views, hand-rolled SVG charts, dense operator UI, no charting library. | Build a new dashboard workspace using existing component patterns and custom SVG charts. |
| I18N | `C:\Code\Agents\requirements\I18N.md` | User-facing strings and formatting should flow through an i18n layer with explicit locale-aware formatters. | Analytics should either introduce the dashboard i18n foundation or isolate all new strings/formatters behind a catalog-ready boundary and track the remaining debt explicitly. |
| Documentation quality | `C:\Code\Agents\requirements\WRITING_DOCUMENTS.md` | Human-readable docs need concrete, specific, authored prose rather than generic filler. | README, REST_API, TESTING, and operator runbooks must explain real Conductor workflows and decision paths. |
| Example applications | `C:\Code\Agents\requirements\EXAMPLE_APPLICATIONS.md` | Conductor is the local reference for Touchstone tests and mature dashboard surfaces; Hydra is the reference for i18n. | Follow current Conductor patterns while using Hydra as the i18n model if the dashboard foundation is expanded. |

## Persona Accountability

Analytics cuts across product, engineering, operations, security, finance, customer support, and go-to-market. The persona library in `C:\Code\Agents\personas` should shape the work so the plan has clear decision owners.

| Persona | Analytics Lens | Owned Decisions or Artifacts | Plan Implications |
| --- | --- | --- | --- |
| CEO | Company-level growth and strategic fit | Business tradeoff when revenue, quality, risk, or timeline conflict | Escalate if analytics becomes a committed sales promise before release quality is proven. |
| CTO | Technical direction and production risk | Architecture direction, cross-system integration posture | Approve the analytics namespace, rollup strategy, and future hardware telemetry correlation model. |
| CFO | Profitability and financial controls | Cost estimate language and pricing assumptions | Cost analytics must be clearly labeled estimate-only and driven by an operator-supplied per-token unit cost. |
| Chief Product Officer | Product strategy and scope | Product strategy, launch scope, sunset/migration decisions | Treat `Analytics` as the renamed and expanded request analytics workspace. |
| VP Engineering | Delivery predictability | Staffing, capacity, delivery governance | Keep the plan phased so backend rollups, dashboard workspace, and SDK/docs do not collide late. |
| Product Manager | Product requirements and roadmap priority | PRD, acceptance criteria, roadmap tradeoffs | Own the tab inventory, first-release metric list, and report/export scope. |
| Product Marketing Manager | Positioning accuracy | Launch messaging and claims | Claims must distinguish request analytics, estimate-only cost output, and deferred hardware telemetry analytics. |
| Technical Program Manager | Cross-team execution | Milestones, dependency map, risk register | Coordinate backend, dashboard, SDK, Postman, docs, QA, and release sequencing. |
| UX Designer | Workspace experience | Information architecture, interaction design, responsive behavior | Design dense operator tabs, drill-down flows, saved reports, export flows, and empty/loading/error states without first-release comparison mode. |
| UX Researcher | Operator workflow evidence | Research plan and task validation | Validate workflows with SRE/support/operator personas before polishing charts. |
| Principal Architect | Technical design | ADR, API shape, data model, compatibility strategy | Own the `/v1.0/analytics` API design, aggregation semantics, rollup strategy, and compatibility path. |
| Engineering Manager | Implementation execution | Engineering plan and sequencing | Break work into backend, frontend, SDK, docs, and QA slices with clear merge gates. |
| Software Engineer | Product implementation | Backend, dashboard, SDK, Postman changes | Implement typed contracts, services, route modules, dashboard workspace, and testable client methods. |
| DevOps Engineer | Deployment assets | Docker, compose, environment, local runtime | Ensure seeded/local environments can generate and inspect representative analytics data. |
| Site Reliability Engineer | Production operation | SLOs, alerts, runbooks, operational dashboard needs | Define latency, TTFT, saturation, analytics coverage, and error-rate views that map to operations. |
| Security Engineer | Data protection | Security review and threat model | Review user/credential drill-down, exports, trace IDs, retained provider metrics, and tenant boundaries. |
| Data Engineer | Telemetry model | Data dictionary, rollups, data quality checks | Define metric semantics, null-not-zero rules, rollup grain, and export format contracts. |
| Database Engineer / DBA | Storage and query safety | Migration, index, query-plan, retention, rollback plans | Validate query plans for range scans, group-by dimensions, rollups, and deletion/retention behavior. |
| Data Analyst | Reporting validity | Analytics report definitions and dashboard validation | Define denominators, percentile behavior, cohort summaries, and estimate-only cost report validity. |
| AI/ML Engineer | Model-serving signal quality | Model performance and inference cost interpretation | Help interpret token throughput, prompt eval time, model load time, and context length. |
| QA Engineer | Release quality signal | Test plan and evidence | Own backend, dashboard, SDK, Postman, responsive, i18n, security, and regression validation gates. |
| Automation Engineer | Repeatable validation | CI and automated test workflows | Add scripts/checks for API coverage, seeded data, snapshot comparisons, and Postman parse/import checks. |
| Documentation Engineer | User-facing docs | README, REST_API, TESTING, runbooks, examples | Write concrete workflows for TTFT investigation, token usage, estimated cost, failed requests, and denied requests. |
| Customer Success Manager | Adoption and support | Customer rollout and health checklists | Turn analytics into customer-facing operational recipes and onboarding guidance. |
| Technical Support Engineer | Troubleshooting usability | Support runbooks and diagnostic playbooks | Ensure support can answer "why was it slow?", "who used tokens?", and "why did it deny?" quickly. |
| Developer Relations | Integration experience | SDK examples, Postman examples, API Explorer flows | Provide copy/paste SDK and Postman examples for every common analytics query. |
| Growth Marketing Manager | Adoption metrics | Funnel and usage instrumentation around analytics | Avoid marketing claims until workspace usage and customer value are measurable. |
| Account Executive | Revenue commitments | Deal risk and customer commitment escalation | Escalate custom analytics commitments that require non-roadmap fields or SLA promises. |
| Sales Engineer | Technical validation in deals | Demo readiness and proof points | Build demo scenarios for TTFT, token usage, user cost estimates, failed request reporting, and access denials. |
| Legal Counsel | Contract and data risk | Legal review of telemetry claims and exports | Review wording around cost estimates, retained metadata, and exportable user-level usage. |
| Compliance Officer | Audit and retention | Compliance audit package | Validate retention, deletion, access controls, export auditability, and tenant isolation evidence. |

## Core Operator Questions

The workspace should start from questions an operator actually asks. Metrics and charts are implementation details; the product should make the answer discoverable.

| Question | Required Dimensions | Required Metrics | Drill-Down Path |
| --- | --- | --- | --- |
| What is the average, P50, P95, and P99 time-to-first-token for this endpoint, VMR, or user? | Time, tenant, VMR, endpoint, provider, requested model, effective model, model definition, user, credential | `FirstTokenTimeMs` from request received to first token received, stage components, successful request count, analytics coverage | Bucket -> endpoint/model row -> user/credential split -> request history entry -> analytics timeline |
| Which user or credential is driving TTFT outliers? | User, credential, tenant, VMR, endpoint, model, status class | TTFT percentiles, slowest successful requests, request count, token counts, prompt size proxies | User row -> request list -> request detail -> provider metrics |
| What is total token usage over time for a given model, endpoint, VMR, tenant, or user? | Requested model, effective model, model definition, provider, VMR, endpoint, tenant, user, credential | Prompt tokens, completion tokens, total tokens, cached tokens, multimodal token fields where available, tokens per second, successful request count | Time bucket -> model breakdown -> users/credentials -> export |
| What is the estimated cost for this user over the last day? | User, tenant, credential, model, endpoint, VMR, provider | Successful total tokens, user-supplied per-token unit cost, estimated cost, unknown-token count, analytics coverage | User summary -> token details -> request history -> CSV/JSON/Parquet/PDF/exported dashboard link |
| Which tenants, users, credentials, models, endpoints, or VMRs are responsible for usage? | Tenant, user, credential, model, endpoint, VMR, provider | Token totals, estimated cost, successful request count, unreported usage | Usage allocation summary -> source requests -> export |
| Which endpoint should be scaled, drained, quarantined, or retired? Future scope. | Endpoint, host, provider, VMR, model, service state, health state | Request volume, failure rate, p95 latency, TTFT, generation TPS, in-flight, capacity wait, uptime | Endpoint row -> health detail -> routing evidence |
| Did load balancing spread traffic as expected? Future scope. | VMR, load-balancing policy, endpoint, routing outcome, session-affinity state | Endpoint share, routing denials, saturation denials, capacity wait, policy fallback count | Distribution chart -> policy -> explain-routing examples |
| Did a model access policy block or nearly block traffic? | Model access policy, rule, VMR, model, user, credential, tenant | denied count, would-deny count, default allow/deny counts, evaluator errors | Policy row -> matched rule -> request history -> policy simulation |
| Are model costs rising because prompts are growing or completions are growing? | Model, request type, user, credential, VMR, endpoint | prompt tokens, completion tokens, prompt/completion ratio, request bytes, response bytes | Token composition chart -> top users -> sample requests |
| Is a latency spike caused by model serving or by Conductor routing/capacity? | Stage kind, endpoint, VMR, model, health state | routing time, capacity wait, upstream headers wait, first-token wait, generation time, completion time | Stage breakdown -> endpoint health -> per-request timeline |
| Is provider usage telemetry reliable enough for reporting? | Provider, endpoint, model, VMR, time | analytics coverage, provider usage present, provider usage missing, null metric count, malformed usage count | Data quality tab -> request examples -> provider parser evidence |
| Which models are unused, expensive, slow, or unreliable? | Model, model definition, provider, endpoint, VMR | request count, token totals, p95 latency, TTFT, error rate, estimated cost | Model table -> endpoint split -> request history |
| What is the utilization trend for GPU-backed local models? Future scope. | Endpoint, host, GPU index, model, VMR, time | GPU utilization, VRAM used/free, power, temperature, CPU, memory, model residency, token throughput | Hardware chart -> endpoint -> RigMonitor telemetry -> traffic overlay |
| Are capacity limits causing user-facing throttling? | Endpoint, VMR, user, credential, model, time | capacity wait, saturation denials, `429`, in-flight, max parallel requests, queue-like stage time | Saturation panel -> endpoint config -> request history |
| Which integrations are behaving badly? | Credential, user, source IP, VMR, model, endpoint, status class | request bursts, 4xx/5xx rate, denied requests, token spikes, failed auth attribution when available | Credential/user row -> request history -> model access policy |
| What should be included in an IT reporting export? | Tenant, user, credential, VMR, model, endpoint, provider, time | usage, estimated cost, latency SLO, failures, denials, analytics coverage | Saved report -> export job -> audit record |

## Metric Catalog

Metric definitions need to be stable enough for SDK clients, Postman users, support runbooks, and downstream IT reporting. A future `/v1.0/analytics/catalog` endpoint should publish these definitions so the dashboard and SDK examples do not hard-code names in multiple places.

### Latency Metrics

- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Define `ResponseTimeMs` as total Conductor-observed request duration.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Define `FirstTokenTimeMs` as Conductor request received to first response token received, with non-streaming semantics documented separately.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Define `RequestToHeadersMs` as upstream dispatch to upstream response headers.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Define `HeadersToFirstTokenMs` as upstream headers to first token/byte.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Define `FirstTokenToLastTokenMs` as generation or response body streaming time.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Define `EndpointLimiterWaitMs` as capacity wait before a request can enter the selected endpoint.
- [ ] Status: Not Started | Priority: P1 | Owner: Data Engineer | Define `ProviderLoadMs`, `ProviderPromptEvalMs`, and `ProviderGenerationMs` where providers expose them.
- [ ] Status: Not Started | Priority: P1 | Owner: Data Engineer | Define percentile rules for `P50`, `P90`, `P95`, and `P99` consistently across raw and rollup queries.

### Token And Throughput Metrics

- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Keep separate prompt/input, completion/output, total, cached, and multimodal token metrics where providers expose them.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Define null token metrics as "not reported", never zero.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Define average overall tokens per second and generation tokens per second.
- [ ] Status: Not Started | Priority: P1 | Owner: AI/ML Engineer | Add prompt-to-completion ratio and completion-token share for cost and behavior diagnosis.
- [ ] Status: Not Started | Priority: P1 | Owner: AI/ML Engineer | Track request and response byte counts alongside token counts for providers that do not report usage.

### Cost Estimate Metrics

- [ ] Status: Not Started | Priority: P0 | Owner: CFO | Label first-release cost as estimate-only and not provider billing reconciliation, accounting-grade reporting, chargeback, or billback.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Add query input for a user-supplied per-token unit cost and currency/display label.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Calculate estimated cost as successful billable tokens multiplied by the supplied per-token unit cost.
- [ ] Status: Deferred | Priority: P2 | Owner: Product Manager | Defer cost centers, departments, applications, hardware-hour allocation, price books, and effective-date pricing.
- [ ] Status: Not Started | Priority: P1 | Owner: Compliance Officer | Add audit metadata for saved report and export jobs; pricing-rule audit is future work if stored pricing rules are added later.

### Reliability And Access Metrics

- [ ] Status: Not Started | Priority: P0 | Owner: SRE | Track success rate, failure rate, status class, provider errors, endpoint errors, saturation denials, and unrouted requests.
- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Track model access permit, deny, would-deny, default permit, default deny, and evaluator error outcomes.
- [ ] Status: Not Started | Priority: P1 | Owner: SRE | Track endpoint drain/quarantine impact and health-state transition correlation with traffic.
- [ ] Status: Not Started | Priority: P1 | Owner: Technical Support Engineer | Define stable troubleshooting categories for slow, failed, denied, throttled, unrouted, and missing-telemetry requests.

### Hardware And Capacity Metrics

Hardware and capacity analytics are useful but deferred from the first release. Do not block TTFT, token usage, saved reports, exports, or estimate-only cost output on GPU or RigMonitor history work.

- [ ] Status: Deferred | Priority: P2 | Owner: SRE | Capture or aggregate RigMonitor telemetry snapshots over time for endpoint analytics rather than only current health state.
- [ ] Status: Deferred | Priority: P2 | Owner: Data Engineer | Define GPU utilization, VRAM used/free, power draw, temperature, CPU, memory, disk queue, network throughput, and telemetry age as analytics dimensions or measures.
- [ ] Status: Deferred | Priority: P2 | Owner: AI/ML Engineer | Correlate model load/residency signals with TTFT, provider load time, and token throughput.
- [ ] Status: Deferred | Priority: P2 | Owner: SRE | Add capacity trend views for max parallel requests, in-flight requests, capacity wait, and saturation denials.

### Data Quality Metrics

- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Track analytics coverage, missing provider usage, missing stage events, and malformed provider metrics.
- [ ] Status: Deferred | Priority: P2 | Owner: Data Engineer | Track stale RigMonitor telemetry when hardware analytics are implemented.
- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Document null-not-zero semantics in `REST_API.md`, `README.md`, and operator runbooks.
- [ ] Status: Not Started | Priority: P1 | Owner: QA Engineer | Add tests proving missing usage data does not produce false zero-cost or zero-token reports.

## Dimensions And Filters

The workspace should apply the same filter model across tabs. A filter selected in the workspace header should drive every chart, table, saved view, export, and drill-down unless a tab explicitly adds a narrower local filter.

Required dimensions:

- `tenantId`
- `requestorUserId`
- `requestorUserEmail`
- `credentialId`
- `credentialName`
- `virtualModelRunnerId`
- `virtualModelRunnerName`
- `modelRunnerEndpointId`
- `modelRunnerEndpointName`
- `providerName`
- `apiFormat`
- `requestedModel`
- `effectiveModel`
- `modelDefinitionId`
- `modelDefinitionName`
- `modelConfigurationId`
- `requestType`
- `action`
- `statusClass`
- `httpStatus`
- `routingOutcomeCode`
- `denialReasonCode`
- `loadBalancingPolicyId`
- `modelAccessPolicyId`
- `modelAccessRuleId`
- `modelAccessDecision`
- `modelAccessWouldDeny`
- `stageKind`
- `dominantStageKind`
- `endpointServiceState`
- `endpointHealthState`
- `rigMonitorReady`
- `telemetryFreshness`
- `labels`
- `tags`

Required time controls:

- `lastHour`
- `lastDay`
- `lastWeek`
- `lastMonth`
- custom `startUtc` and `endUtc`, capped by 30-day retention
- explicit granularity selector that maps to server-validated `bucketSeconds`
- selected display timezone for dashboard formatting and exports

Tenant scope controls:

- system admin default: global results across tenants
- system admin optional filter: one selected tenant
- tenant admin and tenant-scoped user behavior: forced tenant scope, with server-side override prevention
- analytics-specific role: can view analytics within the scope granted by the caller's tenant/system context

Required grouping controls:

- group by model
- group by endpoint
- group by VMR
- group by user
- group by credential
- group by tenant
- group by provider
- group by status class
- group by stage

Comparison mode is out of scope for the first release. Grouping should still cover endpoint, VMR, model, provider, tenant, user, and credential.

Retention behavior:

- [ ] Status: Not Started | Priority: P0 | Owner: Compliance Officer | Enforce and document 30-day analytics retention for first release.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Reject or clamp custom ranges outside the retained window with a clear API error or dashboard message.
- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Document 30-day retention in README, REST_API, dashboard help text, and export/report descriptions.

## Backend Plan

### API Namespace

The existing request-history analytics endpoints should remain for backward compatibility. New workspace endpoints should live under `/v1.0/analytics` so operators and SDK users can discover analytics as a first-class product area. The API should be optimized for the dashboard first, with stable contracts that SDKs and Postman can reuse.

Proposed routes:

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/v1.0/analytics/catalog` | Metric, dimension, stage, grouping, range, and export format catalog. |
| `POST` | `/v1.0/analytics/query` | General typed analytics query for summary, time-series, grouped tables, saved reports, and exports. |
| `GET` | `/v1.0/analytics/summary` | Convenience summary endpoint for dashboard page load and simple SDK use. |
| `GET` | `/v1.0/analytics/timeseries` | Time-bucketed series for selected metrics and dimensions. |
| `GET` | `/v1.0/analytics/ttft` | TTFT-focused summary and time series by VMR, endpoint, model, user, or credential. |
| `GET` | `/v1.0/analytics/tokens` | Token usage and throughput summary over time. |
| `GET` | `/v1.0/analytics/costs` | Estimate-only cost breakdowns from successful tokens and a supplied per-token unit cost. |
| `GET` | `/v1.0/analytics/models` | Model adoption, latency, token, cost, and error summaries. |
| `GET` | `/v1.0/analytics/endpoints` | Endpoint performance and health summaries; hardware history is future scope. |
| `GET` | `/v1.0/analytics/users` | User and credential usage summaries. |
| `GET` | `/v1.0/analytics/access` | Model access policy permit/deny/would-deny analytics. |
| `GET` | `/v1.0/analytics/data-quality` | Analytics coverage, missing metrics, stale telemetry, and parser health. |
| `POST` | `/v1.0/analytics/exports` | Start an export job for a saved or ad hoc analytics query. |
| `GET` | `/v1.0/analytics/exports/{id}` | Read export job status and metadata. |
| `GET` | `/v1.0/analytics/exports/{id}/download` | Download CSV, JSON, Parquet, or PDF export output, subject to auth and retention. |
| `GET` | `/v1.0/analytics/reports` | List saved report definitions and dashboard-link metadata. |
| `POST` | `/v1.0/analytics/reports` | Create a saved report definition. |
| `PUT` | `/v1.0/analytics/reports/{id}` | Update a saved report definition. |
| `DELETE` | `/v1.0/analytics/reports/{id}` | Delete a saved report definition. |

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: Principal Architect | Create an ADR for analytics namespace, query model, compatibility behavior, and report/export boundaries.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add typed request and response models under `src/Conductor.Core/Models`, one class per file if implementation requires new public models.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add a route registrar such as `AnalyticsRouteModule` following the existing route registry pattern.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add controller/service methods with explicit tenant scoping and cancellation tokens.
- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Authorize analytics with system admin, tenant admin, or analytics-specific rights.
- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Apply global scope only for system admins; force tenant scope for tenant admins and tenant-scoped analytics users.
- [ ] Status: Not Started | Priority: P1 | Owner: Developer Relations | Keep the old `requesthistory/analytics` SDK methods and document the new workspace APIs as preferred.

### Query Contract

The query contract should support simple GET endpoints and a richer POST query body. GET endpoints keep Postman and curl pleasant. POST avoids unreadable URLs for grouped, compared, and exported reports.

Recommended `AnalyticsQueryRequest` fields:

```json
{
  "TenantId": "ten_xxx",
  "Range": "lastDay",
  "StartUtc": null,
  "EndUtc": null,
  "BucketSeconds": 900,
  "Timezone": "America/Los_Angeles",
  "TokenUnitCost": 0.000001,
  "CostCurrency": "USD",
  "Metrics": ["request.count", "latency.ttft.avg", "tokens.total", "cost.estimated"],
  "GroupBy": ["VirtualModelRunnerId", "ModelRunnerEndpointId"],
  "Filters": {
    "VirtualModelRunnerIds": ["vmr_xxx"],
    "ModelRunnerEndpointIds": ["mre_xxx"],
    "ModelNames": ["llama3.1"],
    "RequestorUserIds": ["usr_xxx"],
    "CredentialIds": ["cred_xxx"],
    "ProviderNames": ["Ollama"],
    "StatusClasses": ["2xx"],
    "SuccessfulCompletionsOnly": true,
    "StageKinds": ["first_token_wait"],
    "ModelAccessWouldDeny": null
  },
  "Limit": 10000,
  "ContinuationToken": null
}
```

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Define canonical metric IDs and dimension IDs.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add server-side validation for invalid metric IDs, dimension IDs, ranges, bucket sizes, group counts, and limit values.
- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Ensure user and credential drill-down is allowed only for permitted system admin, tenant admin, or analytics-role callers.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Treat successful completions as the default denominator for token and cost metrics; expose failures, denials, and rate limits through separate reliability/access metrics.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Validate token unit cost inputs and never persist them as billing truth.
- [ ] Status: Not Started | Priority: P1 | Owner: Database Engineer / DBA | Add hard caps for range, bucket count, group cardinality, export row count, and scan row count.

### Data Model

The existing request history and request analytics event tables should remain the source of truth for per-request investigation. The workspace needs additional persisted data only where the current model cannot answer long-range or reporting questions efficiently.

Candidate additions:

| Table or Field | Purpose | Priority |
| --- | --- | --- |
| `analyticsreports` | Saved dashboard report/view definitions, including filters, metrics, granularity, visualization state, and dashboard-link metadata. | P0 |
| `analyticsexportjobs` | Export job metadata, auth owner, status, format, object key, row count, expiry, and retained filter summary. | P0/P1 |
| `analyticspricingrules` | Future stored token and request pricing rules by provider/model/effective date if estimate-only unit-cost input becomes insufficient. | P2 |
| `analyticshardwaretelemetrysamples` | Future time-series samples from cached RigMonitor snapshots, scoped by tenant and endpoint. | P2 |
| `analyticsrollups` | Optional aggregate rollups by tenant/time/dimension/metric after benchmark evidence. | P1/P2 |
| `requesthistory.costestimated` | Deferred. First-release cost is computed at query time from supplied token unit cost. | P2 |
| `requesthistory.costcurrency` | Deferred. First-release currency is display metadata supplied with the query/report. | P2 |
| `requesthistory.costruleid` | Deferred. No stored pricing rule is required for first-release estimate-only cost. | P2 |
| `requesthistory.requesttype` index updates | Improve filtering by action/request type. | P0 |
| `requesthistory.requestoruserguid` index updates | Improve user drill-down. | P0 |
| `requesthistory.credentialguid` index updates | Improve credential drill-down. | P0 |
| `requesthistory.effectivemodel` index updates | Improve model usage and token queries. | P0 |
| `requesthistory.modeldefinitionguid` index updates | Improve model-definition grouping. | P0 |
| `requesthistory.modelaccesspolicyguid` index updates | Improve access analytics. | P1 |

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: Database Engineer / DBA | Produce a migration plan for SQLite, MySQL, PostgreSQL, and SQL Server before implementation.
- [ ] Status: Not Started | Priority: P0 | Owner: Database Engineer / DBA | Add query-plan evidence for TTFT by endpoint/VMR/user and token usage by model over last day/week/month.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add idempotent startup migrations and Docker factory schema changes for any new tables or indexes.
- [ ] Status: Not Started | Priority: P1 | Owner: Software Engineer | Add backup/restore support for saved reports and export metadata when those features are implemented.
- [ ] Status: Deferred | Priority: P2 | Owner: SRE | Decide whether hardware telemetry samples are persisted from the health loop or aggregated from request-time snapshots in a future phase.

### Aggregation Services

The current overview builder operates in memory from bounded raw rows. That can remain for smaller windows, but operator reporting needs predictable performance over larger datasets and groupings.

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add `AnalyticsQueryService` to normalize filters, authorize scope, pick raw vs rollup execution, and return typed results.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add focused builders for summary, time-series, TTFT, tokens, estimate-only costs, users, models, access, and data-quality results.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Preserve empty buckets in time-series responses so charts do not guess at missing intervals.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Keep null provider metrics as null in aggregates unless a count metric explicitly counts missing data.
- [ ] Status: Not Started | Priority: P1 | Owner: Database Engineer / DBA | Add rollup jobs only after raw indexed query benchmarks show a real performance need.
- [ ] Status: Not Started | Priority: P1 | Owner: SRE | Add warnings/logs when queries hit scan limits, bucket caps, group caps, or degraded rollup freshness.

### Hardware Correlation

RigMonitor currently enriches health and routing policy decisions. Historical hardware analytics are deferred from the first release. The first release can still show request-stage timing and endpoint identifiers, but it should not require GPU history, Prometheus/OpenTelemetry, capacity planning, or alerting work.

Tasks:

- [ ] Status: Deferred | Priority: P2 | Owner: Principal Architect | Decide whether future hardware analytics use request-time endpoint telemetry snapshots, periodic endpoint telemetry samples, or both.
- [ ] Status: Deferred | Priority: P2 | Owner: Software Engineer | Persist bounded RigMonitor telemetry samples keyed by tenant, endpoint, host, collected timestamp, and telemetry profile.
- [ ] Status: Deferred | Priority: P2 | Owner: Data Engineer | Normalize GPU telemetry into aggregate fields: GPU count, avg/max utilization, min/max/avg free VRAM, power, temperature, and per-GPU optional details.
- [ ] Status: Deferred | Priority: P2 | Owner: Security Engineer | Review whether hostnames, endpoint URLs, GPU UUIDs, process names, or Ollama model names are sensitive in future exports.
- [ ] Status: Deferred | Priority: P2 | Owner: SRE | Add data-quality indicators for stale, missing, partial, or failed RigMonitor telemetry.

### Cost Estimate Rules

Cost reporting should be explicit about its source. For the first release, Conductor should not silently scrape, assume, or store provider prices. The operator supplies a per-token unit cost in the dashboard query, saved report, SDK call, or Postman request. Conductor multiplies successful reported tokens by that unit cost and labels the result as an estimate.

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: CFO | Approve user-facing estimate-only language.
- [ ] Status: Not Started | Priority: P0 | Owner: Product Manager | Define token-unit-cost UX: numeric unit cost, optional currency/display label, reset behavior, saved report behavior, and export metadata.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add validation for token unit cost in dashboard and API query models.
- [ ] Status: Deferred | Priority: P2 | Owner: Software Engineer | Add stored pricing-rule CRUD only if future requirements need price books, effective dates, or provider/model-specific pricing.
- [ ] Status: Not Started | Priority: P1 | Owner: Compliance Officer | Add audit trail requirements for saved report changes and export generation.
- [ ] Status: Not Started | Priority: P1 | Owner: Documentation Engineer | Document that missing provider usage means cost is unknown, not zero.

## Frontend Plan

### Navigation And Workspace Structure

The dashboard should rename `Request Analytics` to `Analytics` as a top-level nav item. The existing `/request-analytics` route should redirect to `/analytics` or remain as an alias until users have migrated. The README and docs should link to `ANALYTICS.md` while the implementation is in progress, then to the final operator documentation after release.

Proposed routes:

| Route | View | Notes |
| --- | --- | --- |
| `/analytics` | `AnalyticsWorkspace.jsx` | Main workspace with shared filters and tabs. |
| `/analytics/latency` | same view or nested tab state | Latency, TTFT, stage timing, slow requests. |
| `/analytics/tokens` | same view or nested tab state | Token totals, throughput, model/user/endpoint splits, and estimate-only cost from unit token cost. |
| `/analytics/costs` | same view or nested tab state | Optional alias or subtab for estimate-only cost, not formal chargeback. |
| `/analytics/models` | same view or nested tab state | Model adoption, performance, reliability. |
| `/analytics/endpoints` | same view or nested tab state | Endpoint, health, and saturation summaries; hardware correlation is future scope. |
| `/analytics/users` | same view or nested tab state | User and credential usage. |
| `/analytics/access` | same view or nested tab state | Model access decisions and denials. |
| `/analytics/reports` | same view or nested tab state | Saved reports and exports. |
| `/request-analytics` | redirect or legacy route | Backward-compatible navigation. |

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add or evolve `AnalyticsWorkspace.jsx` and route registration from the current request analytics page.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Update `Sidebar.jsx` so `Analytics` is the nav label and request analytics becomes a child tab or redirect.
- [ ] Status: Not Started | Priority: P0 | Owner: UX Designer | Define tab order, workspace header filters, drill-down behavior, and saved-view behavior.
- [ ] Status: Not Started | Priority: P1 | Owner: Documentation Engineer | Update README documentation navigation and any table of contents entries.

### Shared Workspace Header

The workspace header should contain controls that operators expect to use constantly:

- date range segmented control
- custom start/end picker
- granularity selector
- tenant selector for global admins
- VMR selector
- endpoint selector
- model selector
- user/credential selector
- provider selector
- request type/action selector
- token unit cost input for estimate-only cost output
- saved view menu
- export button
- refresh button

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: UX Designer | Produce wireframes for the shared filter header in desktop, tablet, and mobile widths.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Build filters with stable dimensions so labels and controls do not resize charts or tables while loading.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add debounced query updates and cancel stale requests when filters change.
- [ ] Status: Not Started | Priority: P1 | Owner: QA Engineer | Validate keyboard navigation, touch targets, focus order, and no horizontal scrolling at 1280px, 768px, and 390px.

### Tabs And Views

#### Overview

The Overview tab should summarize traffic, latency, token usage, estimated cost, failures, model access denials, and analytics coverage. It is not a landing page. It is the first operational control surface. Hardware telemetry freshness can be added later when hardware analytics are implemented.

- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add KPI strip for successful requests, success rate, p95 latency, average TTFT, tokens, estimated cost, denials, and coverage.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add time-series chart combining request volume, p95 latency, average TTFT, and total tokens.
- [ ] Status: Not Started | Priority: P1 | Owner: SRE | Add "investigate" links from anomalies to the latency, endpoint, or access tab with filters preserved.

#### Latency And TTFT

- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add TTFT by VMR and endpoint table with average, p50, p95, p99, request count, and coverage.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add drill-down to user/credential splits for the selected endpoint/VMR/model.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add stage breakdown chart separating routing, capacity wait, upstream headers, first-token wait, generation, and completion.
- [ ] Status: Not Started | Priority: P1 | Owner: Technical Support Engineer | Add copyable support summary for a selected slow request including trace ID, endpoint, model, TTFT, dominant stage, and provider request ID.

#### Tokens And Throughput

- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add token usage over time for selected requested model, effective model, model definition, endpoint, VMR, tenant, user, or credential.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Split prompt, completion, total, cached, and multimodal token fields where available.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add top models, top users, top credentials, and top endpoints by total tokens.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Show estimated cost from the supplied per-token unit cost, with clear estimate-only labeling and unknown-token handling.
- [ ] Status: Not Started | Priority: P1 | Owner: AI/ML Engineer | Add throughput distribution by model and endpoint using generation TPS and overall TPS.

#### Cost Estimates

- [ ] Status: Not Started | Priority: P0 | Owner: Product Manager | Keep first-release cost estimate output tied to token usage and user-supplied per-token unit cost.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add estimated cost over time by tenant, user, credential, model, endpoint, and VMR.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Show unpriced tokens and missing-usage rows separately from zero-cost rows.
- [ ] Status: Not Started | Priority: P1 | Owner: Software Engineer | Add CSV, JSON, Parquet, PDF, and dashboard-link export for cost estimate reports with report metadata and filter summary.

#### Models

- [ ] Status: Not Started | Priority: P1 | Owner: Software Engineer | Add model adoption table with request count, token count, estimated cost, p95 latency, average TTFT, error rate, and active endpoint count.
- [ ] Status: Not Started | Priority: P1 | Owner: AI/ML Engineer | Add model behavior indicators such as prompt/completion ratio, generation TPS, provider load time, and context-pressure proxies where available.
- [ ] Status: Not Started | Priority: P1 | Owner: Product Manager | Add idle, slow, expensive, and unreliable model flags.

#### Endpoints And Hardware

- [ ] Status: Not Started | Priority: P1 | Owner: Software Engineer | Add endpoint performance table with volume, p95 latency, average TTFT, token throughput, failures, saturation, and health state.
- [ ] Status: Deferred | Priority: P2 | Owner: Software Engineer | Add hardware overlay charts for GPU utilization, VRAM, CPU, memory, power, temperature, disk, and network where RigMonitor is enabled.
- [ ] Status: Deferred | Priority: P2 | Owner: SRE | Add endpoint recommendation hints for drain, quarantine, capacity increase, or model placement review.
- [ ] Status: Deferred | Priority: P2 | Owner: Database Engineer / DBA | Confirm hardware telemetry queries remain bounded and do not turn RigMonitor into unbounded time-series storage.

#### Users And Credentials

- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add user and credential usage tables with requests, tokens, estimated cost, p95 latency, average TTFT, failures, denials, and last seen time.
- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Gate user/credential drill-down by existing admin/tenant-admin semantics.
- [ ] Status: Not Started | Priority: P1 | Owner: Customer Success Manager | Add support-friendly workflow for identifying an integration causing bursts, failures, or spend spikes.

#### Reliability And Access

- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add failures by status class, provider error, endpoint, VMR, model, user, and credential.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add model access permit, deny, would-deny, default permit, default deny, and evaluator error panels.
- [ ] Status: Not Started | Priority: P1 | Owner: Software Engineer | Link access rows to policy simulation and request history detail.
- [ ] Status: Not Started | Priority: P1 | Owner: SRE | Add saturation and unrouted request views.

#### Data Quality

- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Add analytics coverage by provider, endpoint, VMR, and model.
- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Add missing token usage, missing stage events, and malformed provider metrics panels.
- [ ] Status: Deferred | Priority: P2 | Owner: Data Engineer | Add stale hardware telemetry panels when hardware analytics are implemented.
- [ ] Status: Not Started | Priority: P1 | Owner: QA Engineer | Add fixtures for complete, partial, old, missing, malformed, and null provider metrics.

#### Reports And Exports

- [ ] Status: Not Started | Priority: P0 | Owner: Product Manager | Define saved report types for TTFT, token usage, user cost estimate, model usage, endpoint performance, access denials, and data quality.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add saved views and export jobs for CSV, JSON, Parquet, PDF, and shareable dashboard links.
- [ ] Status: Not Started | Priority: P1 | Owner: Compliance Officer | Add export access logging and retention/expiry for generated files.
- [ ] Status: Deferred | Priority: P2 | Owner: Product Manager | Scheduled emailed reports are deferred unless a customer requirement justifies notification infrastructure.

### Frontend Design Requirements

Analytics is an operational workspace. Keep the layout quiet, dense, and built for scanning. Avoid a marketing-style hero, decorative cards, nested cards, gradient decorations, and one-note palettes. Charts must use stable dimensions and custom SVG, following the existing dashboard direction.

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: UX Designer | Define chart and table patterns that fit long endpoint names, model names, user emails, and localized labels.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Use existing dashboard components where possible: `PageHeader`, `DataTable`, `Modal`, `CopyableId`, action buttons, and existing CSS variables.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Build feature-complete loading, empty, partial-data, permission-denied, error, retry, and stale-data states.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Keep chart tooltips keyboard and pointer accessible.
- [ ] Status: Not Started | Priority: P1 | Owner: Software Engineer | Add saved filter state in URL query parameters so links preserve investigation context.
- [ ] Status: Not Started | Priority: P1 | Owner: UX Designer | Validate responsive layouts at desktop, tablet, and mobile widths with production-like data.

### Internationalization And Formatting

Conductor currently has many hard-coded dashboard strings. The Analytics workspace is large enough that adding more raw English strings would deepen that debt. A compliant implementation should either introduce the dashboard i18n foundation or isolate the Analytics workspace behind a resource catalog and shared formatter layer that can plug into the foundation.

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: Principal Architect | Decide whether the Analytics workspace introduces the full dashboard i18n runtime or an interim catalog-ready boundary.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add locale-aware formatters for numbers, percentages, durations, bytes, tokens, currency, date, time, date-time, and relative time.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Do not use implicit `toLocaleString()` in Analytics charts or tables.
- [ ] Status: Not Started | Priority: P1 | Owner: QA Engineer | Add pseudo-locale or expansion checks for Analytics nav, tabs, charts, tables, filters, modals, tooltips, and exports.

## SDK Plan

SDKs should make common operator queries easy without requiring users to hand-build query strings. Keep existing request-history analytics helpers for compatibility and add first-class analytics workspace helpers for JavaScript, Python, and C#. The C# SDK is new scope if one does not already exist in the repo.

### JavaScript SDK

- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `getAnalyticsCatalog`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `queryAnalytics`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `getAnalyticsSummary`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `getAnalyticsTimeSeries`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `getAnalyticsTtft`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `getAnalyticsTokens`.
- [ ] Status: Not Started | Priority: P1 | Owner: Developer Relations | Add `getAnalyticsCosts`, `getAnalyticsModels`, `getAnalyticsEndpoints`, `getAnalyticsUsers`, `getAnalyticsAccess`, and `getAnalyticsDataQuality`.
- [ ] Status: Not Started | Priority: P1 | Owner: Developer Relations | Add export and saved-report helpers if those APIs ship.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add exact URL, method, query, body, and tenant-scope tests.
- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Update `sdk/javascript/README.md` with TTFT, token usage, user cost estimate, and saved-report examples.

### Python SDK

- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `get_analytics_catalog`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `query_analytics`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `get_analytics_summary`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `get_analytics_time_series`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `get_analytics_ttft`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `get_analytics_tokens`.
- [ ] Status: Not Started | Priority: P1 | Owner: Developer Relations | Add cost, model, endpoint, user, access, data-quality, export, and saved-report helpers as APIs ship.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add exact URL, method, query, body, and tenant-scope tests with `PYTHONPATH=src`.
- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Update `sdk/python/README.md` with concrete operator examples.

### C# SDK

- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Decide repo placement for the C# SDK, such as `sdk/csharp`, and align package naming with Conductor conventions.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `GetAnalyticsCatalogAsync`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `QueryAnalyticsAsync`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `GetAnalyticsSummaryAsync`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `GetAnalyticsTimeSeriesAsync`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `GetAnalyticsTtftAsync`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add `GetAnalyticsTokensAsync`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add helpers for estimate-only cost using supplied token unit cost.
- [ ] Status: Not Started | Priority: P1 | Owner: Developer Relations | Add saved-report and export helpers as APIs ship.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add C# SDK tests for exact route, query serialization, cancellation tokens, tenant-scope behavior, and estimate-only cost parameters.
- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Add `sdk/csharp/README.md` or the chosen equivalent with dashboard-aligned examples.

## Postman And API Explorer Plan

Postman should remain useful for operators and developers who need to debug without the dashboard. API Explorer should show the new routes automatically through OpenAPI, but the dashboard can also provide curated request templates for common analytics questions.

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add an `Analytics Workspace` folder to `Conductor.postman_collection.json`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add variables: `analyticsRange`, `analyticsStartUtc`, `analyticsEndUtc`, `analyticsBucketSeconds`, `analyticsTimezone`, `analyticsGroupBy`, `analyticsMetric`, `analyticsModelName`, `analyticsEndpointId`, `analyticsVmrId`, `analyticsTenantId`, `analyticsUserId`, `analyticsCredentialId`, `analyticsTokenUnitCost`, `analyticsCurrency`, `analyticsReportId`, `analyticsExportId`.
- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add requests for catalog, summary, query, TTFT, tokens, endpoints, users, models, access, data quality, and exports.
- [ ] Status: Not Started | Priority: P1 | Owner: Developer Relations | Add example bodies for "TTFT by user", "tokens by model and user", "estimated user cost over the last day", and "failed request types by rate-limited or denied outcome".
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Validate Postman JSON parsing and route coverage in automated checks.
- [ ] Status: Not Started | Priority: P1 | Owner: Software Engineer | Add API Explorer request templates for the most common analytics questions.

## Documentation Plan

The documentation should help an operator use Analytics as a troubleshooting and reporting tool. API reference is necessary, but not enough. The README should make the feature discoverable, REST_API should define contracts and null semantics, TESTING should define release gates, and an operator runbook should teach the diagnostic workflows.

Files to update during implementation:

- `README.md`
- `REST_API.md`
- `CHANGELOG.md`
- `TESTING.md`
- `Conductor.postman_collection.json`
- `sdk/javascript/README.md`
- `sdk/python/README.md`
- `sdk/csharp/README.md` or the chosen C# SDK documentation path
- `docker/factory/schema.sql` and Docker/PostgreSQL seed assets if new tables are added
- `docs/adr/NNNN-analytics-workspace.md`
- optional `docs/analytics-runbook.md` or a dedicated section in `TESTING.md`

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Add README overview of the Analytics workspace, operator questions, and dashboard navigation.
- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Update README documentation list or table of contents to include `ANALYTICS.md` during planning and the final operator guide after implementation.
- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Update `REST_API.md` with new route tables, query fields, request/response examples, metric catalog, cost-rule semantics, export behavior, and error codes.
- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Update `CHANGELOG.md` under Unreleased.
- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Update `TESTING.md` with analytics workspace release gates, manual QA, Postman QA, data-quality checks, and troubleshooting runbooks.
- [ ] Status: Not Started | Priority: P1 | Owner: Documentation Engineer | Add real-world examples: slow TTFT investigation, token spike investigation, estimated user cost over the last day, model access denial reporting, and failed request type reporting.
- [ ] Status: Not Started | Priority: P1 | Owner: Legal Counsel | Review any user-facing claims about cost accuracy, provider billing equivalence, hardware utilization, and compliance export readiness.

## Testing Plan

Analytics can mislead operators if tests only check that a chart renders. The test plan must prove math, authorization, null semantics, filter scoping, migrations, and dashboard behavior with realistic data.

### Backend Tests

- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add model tests for analytics query filters, metric IDs, dimension IDs, report definitions, token-unit-cost fields, and export jobs.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add service tests for TTFT aggregation by endpoint/VMR/user/credential.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add service tests for token usage over time by model, endpoint, VMR, user, and credential.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add service tests proving token and cost usage metrics include successful completions only.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add service tests proving estimate-only cost equals successful reported tokens multiplied by supplied unit cost.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add service tests for null provider usage, missing analytics, and malformed provider metrics.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add controller tests for system-admin global scope, system-admin tenant filter, forced tenant scope for tenant admins, analytics-role permissions, invalid filters, limit caps, and forbidden user/credential drill-down.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add database tests for new migrations and indexes across all provider implementations supported by the test suite.
- [ ] Status: Not Started | Priority: P1 | Owner: QA Engineer | Add export job tests for access control, row caps, CSV/JSON/Parquet/PDF format selection, dashboard-link generation, expiry, and audit metadata.
- [ ] Status: Deferred | Priority: P2 | Owner: QA Engineer | Add hardware telemetry correlation tests using RigMonitor fixture snapshots when hardware analytics are implemented.

### Frontend Tests And QA

- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Run dashboard production build.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Manually validate the workspace with realistic complete, partial, empty, stale, and permission-denied data.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Verify filters update all tabs and preserve drill-down context.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Verify charts do not overlap or clip text at 1280px, 768px, and 390px.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Verify long model names, long endpoint names, long emails, CJK text, and expansion pseudo-locale behavior.
- [ ] Status: Not Started | Priority: P1 | Owner: QA Engineer | Verify keyboard navigation and screen-reader labels for tabs, filters, charts, tables, modals, and export buttons.

### SDK And Postman Tests

- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Run JavaScript SDK tests.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Run Python SDK tests with `PYTHONPATH=src`.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Run C# SDK tests once the C# SDK exists.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Parse Postman JSON and verify required analytics routes exist.
- [ ] Status: Not Started | Priority: P1 | Owner: QA Engineer | Exercise Postman against a local server with seeded analytics data.

### Release Gate Commands

Use the final command list in `TESTING.md`, but the implementation should pass at least:

```powershell
git diff --check
dotnet build src\Conductor.sln --no-restore
dotnet test src\Test.Xunit\Test.Xunit.csproj --no-restore --no-build
dotnet test src\Test.Nunit\Test.Nunit.csproj --no-restore --no-build
dotnet run --project src\Test.Automated\Test.Automated.csproj --no-restore --no-build
npm.cmd run build
```

SDK and Postman checks:

```powershell
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

## Security, Privacy, And Compliance

Analytics contains operational metadata that can become sensitive when grouped by user, credential, tenant, model, endpoint, or source. Treat exports as administrative artifacts and make access explicit.

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Threat-model user/credential drill-down and export access.
- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Confirm all analytics queries enforce tenant scope server-side.
- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Confirm system admins can query globally and optionally filter to a tenant, while tenant admins and tenant-scoped analytics users cannot escape their tenant.
- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Add or wire a dedicated analytics permission/role if existing admin and tenant-admin roles are too broad.
- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Confirm analytics never persists or exports bearer tokens, provider keys, cookies, raw prompts, raw completions, request bodies, or response bodies.
- [ ] Status: Not Started | Priority: P0 | Owner: Compliance Officer | Enforce 30-day retention for analytics events and rollups, and document export/report retention separately.
- [ ] Status: Not Started | Priority: P1 | Owner: Compliance Officer | Add export audit evidence: who exported, tenant scope, filters, time range, row count, format, and expiry.
- [ ] Status: Not Started | Priority: P1 | Owner: Legal Counsel | Review external-facing cost and hardware utilization claims.

## Operations And Performance

Analytics should help operate Conductor without making Conductor harder to operate. Query caps, rollups, and data-quality indicators are product requirements, not later optimizations.

Tasks:

- [ ] Status: Not Started | Priority: P0 | Owner: SRE | Define SLO-oriented metrics: p95 response time, p95 TTFT, error rate, saturation denials, and analytics coverage.
- [ ] Status: Not Started | Priority: P0 | Owner: Database Engineer / DBA | Benchmark raw queries for last hour, last day, last week, last month, and custom ranges within the 30-day retention window with realistic cardinality.
- [ ] Status: Not Started | Priority: P1 | Owner: Database Engineer / DBA | Add rollups only where benchmark evidence shows raw queries are insufficient.
- [ ] Status: Not Started | Priority: P1 | Owner: SRE | Add runbook guidance for slow stage categories: routing, capacity wait, upstream headers, first-token wait, generation, completion, provider load, prompt eval, provider generation, and persistence.
- [ ] Status: Not Started | Priority: P1 | Owner: DevOps Engineer | Add seeded local data or scripts that produce enough analytics records for dashboard QA.

## Implementation Phases

### Phase 0: Discovery And Product Definition

- [ ] Status: Not Started | Priority: P0 | Owner: Product Manager | Convert this plan into a PRD with first-release scope, explicit exclusions, and acceptance criteria.
- [ ] Status: Not Started | Priority: P0 | Owner: UX Researcher | Validate the operator questions with at least support/SRE/customer-success stakeholders.
- [ ] Status: Not Started | Priority: P0 | Owner: Principal Architect | Create analytics workspace ADR.
- [ ] Status: Not Started | Priority: P0 | Owner: Technical Program Manager | Create milestone plan, dependency map, and risk register.

Acceptance criteria:

- PRD names first-release tabs and metrics.
- ADR names route namespace, data model, compatibility behavior, and rollup stance.
- UX research produces task findings for average TTFT by user, token usage by user, and estimated user cost over the last day.

### Phase 1: Metric Catalog And Backend Foundations

- [ ] Status: Not Started | Priority: P0 | Owner: Data Engineer | Define metric/dimension catalog and null semantics.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add typed analytics query/request/response models.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add `/v1.0/analytics/catalog`, `/summary`, `/timeseries`, `/ttft`, and `/tokens`.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add query-time cost estimate support from supplied token unit cost.
- [ ] Status: Not Started | Priority: P0 | Owner: Security Engineer | Add system-admin global scope, tenant-admin forced scope, and analytics-role enforcement.
- [ ] Status: Not Started | Priority: P0 | Owner: Database Engineer / DBA | Add indexes required for P0 queries.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Add shared tests for P0 query behavior.

Acceptance criteria:

- Backend can answer average and percentile TTFT by endpoint/VMR/user.
- Backend can answer token usage over time by model/endpoint/VMR/user.
- Backend can answer estimated cost by user over the last day using supplied token unit cost.
- Old request-history analytics routes still work.

### Phase 2: Dashboard Workspace

- [ ] Status: Not Started | Priority: P0 | Owner: UX Designer | Finalize workspace IA and dense operational layouts.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Rename the nav to `Analytics`, add `/analytics` route/alias behavior, shared filters, Overview tab, Latency tab, Tokens tab, unit-cost input, and granularity selector.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add drill-down from aggregate rows to request analytics detail and request history detail.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add dashboard support for last hour, last day, last week, last month, custom range, and custom granularity within 30-day retention.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Validate dashboard production build and manual responsive behavior.

Acceptance criteria:

- Operator can answer average TTFT for a user, token usage for a user, and estimated cost for a user over the last day directly in the dashboard.
- Links preserve filter context.
- Existing `/request-analytics` users are redirected or otherwise not broken.

### Phase 3: Cost Estimates, Saved Reports, And Exports

- [ ] Status: Not Started | Priority: P0 | Owner: CFO | Approve estimate-only cost language.
- [ ] Status: Not Started | Priority: P0 | Owner: Software Engineer | Add saved reports for TTFT, token usage, and user cost estimates.
- [ ] Status: Not Started | Priority: P1 | Owner: Software Engineer | Add export jobs for CSV, JSON, Parquet, PDF, and dashboard links.
- [ ] Status: Not Started | Priority: P1 | Owner: Compliance Officer | Add export audit and retention behavior.
- [ ] Status: Deferred | Priority: P2 | Owner: Product Manager | Scheduled report delivery remains out of scope.

Acceptance criteria:

- Operators can save TTFT, token usage, and estimated-cost reports with clear filter, granularity, and unit-cost metadata.
- Operators can export saved or ad hoc reports in CSV, JSON, Parquet, PDF, and dashboard-link form.
- Missing usage data is visible and excluded from false cost certainty.

### Phase 4: Reliability Analytics And Deferred Endpoint/Hardware Work

- [ ] Status: Not Started | Priority: P1 | Owner: SRE | Define endpoint signals needed for reliability and saturation diagnosis.
- [ ] Status: Not Started | Priority: P1 | Owner: Software Engineer | Add endpoint reliability APIs and dashboard tab.
- [ ] Status: Not Started | Priority: P1 | Owner: Software Engineer | Add access/reliability dashboard tab.
- [ ] Status: Not Started | Priority: P1 | Owner: QA Engineer | Add reliability fixtures/tests, with hardware fixtures deferred until hardware analytics are implemented.
- [ ] Status: Deferred | Priority: P2 | Owner: SRE | Defer GPU/hardware history, Prometheus/OpenTelemetry integration, capacity planning, and alerting.

Acceptance criteria:

- Operators can inspect failure types, rate limits, denied requests, endpoint health, and saturation signals.
- Operators can inspect model access denials and would-deny trends.

### Phase 5: SDKs, Postman, Documentation, And Release Hardening

- [ ] Status: Not Started | Priority: P0 | Owner: Developer Relations | Add JavaScript, Python, C# SDK, and Postman parity for all shipped endpoints.
- [ ] Status: Not Started | Priority: P0 | Owner: Documentation Engineer | Update README, REST_API, CHANGELOG, TESTING, and SDK READMEs.
- [ ] Status: Not Started | Priority: P0 | Owner: QA Engineer | Run full release gate.
- [ ] Status: Not Started | Priority: P1 | Owner: Security Engineer | Complete security review.
- [ ] Status: Not Started | Priority: P1 | Owner: SRE | Complete performance benchmarks and runbook review.

Acceptance criteria:

- Docs and client tooling cover every new or changed API.
- Tests pass under xUnit, NUnit, automated runner, dashboard build, JavaScript SDK, Python SDK, C# SDK, and Postman JSON parsing.
- Security, performance, and manual dashboard QA signoffs are recorded.

## Open Decisions

- [ ] Status: Not Started | Owner: Principal Architect | Should complex analytics queries use only `POST /v1.0/analytics/query`, or should every tab have a GET convenience route?
- [ ] Status: Not Started | Owner: Database Engineer / DBA | Are raw indexed queries sufficient for last-month reporting, or are rollups required in the first production release?
- [ ] Status: Not Started | Owner: Compliance Officer | How long should generated export files be retained after creation, separate from 30-day analytics data retention?
- [ ] Status: Not Started | Owner: Product Manager | Should PDF export be server-rendered, client-rendered, or deferred behind CSV/JSON/Parquet if implementation cost is high?
- [ ] Status: Not Started | Owner: Principal Architect | Which Parquet implementation should be used, and should Parquet export run synchronously or only through export jobs?
- [ ] Status: Not Started | Owner: Security Engineer | What exact permission name should represent analytics-specific access?
- [ ] Status: Not Started | Owner: Principal Architect | Should Analytics introduce the dashboard i18n foundation or isolate strings behind a catalog-ready boundary first?

## Definition Of Done

- [ ] `ANALYTICS.md` remains current as implementation progresses.
- [ ] Dashboard has a top-level Analytics workspace linked in nav.
- [ ] README documentation list or table of contents points operators to Analytics documentation.
- [ ] REST_API documents every new analytics endpoint, query field, response shape, error code, and null semantic.
- [ ] CHANGELOG includes the new analytics workspace and API additions.
- [ ] TESTING includes backend, frontend, SDK, Postman, security, performance, and manual QA gates.
- [ ] JavaScript, Python, and C# SDKs expose all shipped analytics endpoints with tests.
- [ ] Postman imports cleanly and includes analytics workspace examples.
- [ ] Backend APIs enforce tenant and role boundaries server-side.
- [ ] TTFT by endpoint and VMR, with user/credential drill-down, is available in dashboard and API.
- [ ] Token usage over time by model, endpoint, VMR, user, and credential is available in dashboard and API.
- [ ] Estimate-only cost semantics are implemented using supplied token unit cost and documented as not chargeback, billback, or accounting-grade reporting.
- [ ] Hardware telemetry correlation is deliberately deferred with a documented boundary.
- [ ] Null provider metrics render as unavailable, not zero.
- [ ] Query caps, bucket caps, group caps, and scan limits prevent unbounded analytics queries.
- [ ] Dashboard production build passes.
- [ ] xUnit, NUnit, and automated test runners pass.
- [ ] JavaScript, Python, and C# SDK tests pass.
- [ ] Postman JSON parsing and route coverage checks pass.
- [ ] Security review signs off on exports, user/credential drill-down, retained metrics, and tenant isolation.
- [ ] Responsive visual QA passes at desktop, tablet, and mobile widths.
- [ ] Operators can follow documented workflows for slow TTFT, token spike, estimated user cost, failed/rate-limited/denied request reporting, model access denial, and missing telemetry investigations.

## First Developer Starting Point

Begin with the P0 questions rather than the UI. Add or extend backend query tests that prove Conductor can calculate average/P95 TTFT for a selected endpoint, VMR, and user, using request-received to first-token-received semantics. Add a second test proving successful prompt/completion/total/cached/multimodal tokens over time by model, endpoint, VMR, tenant, and user. Add a third test proving estimated cost for a user over the last day from a supplied per-token unit cost, with missing token usage excluded from false certainty. Once those service tests pass, wire the API and dashboard around them.

The first visible dashboard slice should be small but real: `Analytics` nav item, compatibility behavior for `/request-analytics`, shared time/granularity/tenant/VMR/endpoint/model/user filters, token unit cost input, Latency tab with TTFT breakdown, Tokens tab with token and estimate-only cost time series, and request drill-down back into existing request history detail. Saved reports and exports should build on that spine. Hardware history, formal chargeback, comparison mode, and scheduled delivery are deferred.
