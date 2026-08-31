# QoS & Queueing for Virtual Model Runners — Implementation Plan

**Status:** Implemented on `feature/qos` · **Target version:** `0.4.0` → **`0.5.0`** (minor)
**Library:** [QoSKit](https://github.com/jchristn/qoskit) `0.2.0` (`QoSKit` NuGet) — alpha, multi-targets `netstandard2.0;net8.0;net10.0`, drops directly into a net10.0 project.

> **Implementation status.** All steps below are implemented and committed on `feature/qos`. The full
> solution builds with 0 errors; the backend test suite (engine, four-dialect persistence, seeding,
> tenant purge, VMR-requires-profile, telemetry) passes; the three SDKs and the dashboard build and
> their tests pass. Remaining work is human-in-the-loop only: running the containerized stack end to
> end under induced saturation, dashboard visual QA (themes/viewports, the React Flow diagram, the nuke
> flow), and runtime verification of the PostgreSQL/SQL Server/MySQL dialects (compile-verified here,
> SQLite runtime-verified). One framework limitation is documented in code: WatsonWebserver 7.1 exposes
> no per-request client-abort token, so admission wait is bounded by the profile deadline rather than
> client disconnect.

---

## How to use this plan

This is a working checklist. Every actionable item is a status box you annotate as you go:

- `- [ ]` not started · `- [~]` in progress · `- [x]` done · `- [!]` blocked (add a note)

Steps are ordered. Do them top to bottom; each leaves the solution compiling. **Step 1 (branch + version bump) comes before any code.** Sub-steps under a phase can be checked independently, but a phase is not "done" until all its boxes are checked and the build is warning-free.

---

## What this adds, in one paragraph

Every virtual model runner (VMR) is linked to a **QoS profile** — a tenant-scoped, database-stored definition of how that VMR's traffic is classified, queued, and admitted. A profile says which class a request belongs to (driven by whatever the operator chooses: a custom header, a specific credential, the model name, a body attribute, and so on), which queue discipline holds each class (FIFO, priority, weighted-fair, low-latency, weighted round robin, or a hierarchy of them), and what happens under contention. When a VMR's endpoints are all busy, requests wait in the profile's queues and are released in scheduled order as capacity frees, instead of bouncing immediately with `429`. Linking a profile is **required** when you create a VMR; a **default FIFO profile** is seeded per tenant at startup and is the sensible default. All QoS configuration lives in the database across a small set of normalized tables created by on-startup migrations — nothing about QoS lives in `conductor.json`. QoSKit's per-class metrics and hop-by-hop traces flow out through Conductor's existing OpenTelemetry pipeline and are fully Prometheus-scrapable.

The work mirrors the existing `LoadBalancingPolicy` resource end to end — model, tenant-scoped CRUD, soft id-reference from the VMR, dashboard view, VMR-editor selector — which is what keeps it a *minor* release rather than a rewrite.

---

## Design summary

### Decisions already settled

- **Admission gates against existing endpoint capacity — nothing new competes with it.** Conductor already enforces per-endpoint concurrency via `HealthCheckService.TryIncrementInFlight(endpointId, MaxParallelRequests)`. The QoS queue sits in front of that gate, holding requests only when a VMR's endpoints are collectively saturated and releasing them — in the discipline's order — as slots free. A profile introduces **no** second concurrency limit; a VMR's "service rate" is the sum of its endpoints' `MaxParallelRequests`.
- **A full queue or an expired wait returns `429` with `Retry-After`.** Matches the status Conductor already emits for capacity, so existing client/SDK retry logic keeps working. Overflow policy and wait deadline are per-profile (stored in the DB).
- **Queues are in-memory; all *configuration* is in the database.** An in-flight HTTP request can't survive a restart, so persisting the backlog buys nothing — **no dependency on `QoSKit.Persistence.Sqlite`**. Profiles and every QoS configuration item live in normalized DB tables.
- **Every VMR routes through a real QoSKit queue.** Because a profile is required and the default is a real `FifoQoSQueue`, there are no "unprofiled" VMRs and no special pass-through code path. Every VMR therefore emits uniform QoS telemetry — which is what makes the Prometheus/Grafana view complete rather than partial. (This resolves the earlier open question about telemetry uniformity: the answer is uniform, by construction.)
- **The hierarchy diagram is an interactive React Flow editor.** The dashboard ships zero diagram libraries today; `@xyflow/react` is a deliberate new frontend dependency (React 19 compatibility verified during the frontend phase).

### The mental model

A profile answers three questions and nothing else. **Which class is this request?** — classification, driven by an operator-chosen input (header, credential, model, body attribute, tenant, user, client IP, API family) resolved to a named class such as `interactive` or `batch`; it never blocks and never touches the network. **Which queue holds that class, and how is it scheduled against the others?** — the topology, one or more queue nodes wired into a hierarchy ending in a single tail. **When may a waiting request proceed, and what if it can't?** — admission: a per-VMR scheduler drains the tail in the discipline's order and releases one request each time an endpoint slot frees; a full queue is rejected immediately, a request past the deadline is rejected with `Retry-After`.

Scheduling is invisible until requests actually wait. A VMR with spare capacity releases everything instantly and a priority queue looks just like FIFO; QoS earns its keep exactly when a VMR is saturated and the operator has to decide who waits and who goes first.

### How a stored profile becomes live queues

QoSKit classifies with **delegates** (`Func<T,int>`, `Func<T,string>`, `Func<T,bool>`), not config. A `QosProfileCompiler` in `Conductor.Server` reads a profile aggregate from the database and produces a `QosRuntime`: a classifier delegate, one queue instance per node (each named `"{profileId}:{node}"` so metrics attribute per profile/node), a `QoSPipeline<QosAdmissionTicket>` wiring the nodes (`Merge`/`ChainTo`/`AsPipeline`, plus a `QoSRouter<T>` when ingress is class-routed), and the tail the scheduler drains. The item type is a lightweight ticket — classification result plus a `TaskCompletionSource` release signal and the client-abort token — never the request body, so a deep backlog costs kilobytes and nothing about the payload reaches a metric tag. Runtimes are cached per VMR and rebuilt atomically on profile edit.

`QosAdmissionService.AdmitAsync(vmr, ctx, requestAborted)` classifies, enforces total depth, enqueues, and awaits release with the profile's deadline and the abort token linked. The per-VMR scheduler loop dequeues the next ticket in discipline order, waits for a free VMR endpoint slot (reserving it so a burst of frees can't over-release), and completes the ticket. Client disconnect tombstones the ticket — it's skipped when it reaches the head, freeing no slot — since QoSKit can't pluck an item from the middle of a queue.

### Where admission happens

In `ProxyController.HandleRequest` (`src/Conductor.Server/Controllers/ProxyController.cs`), the routing decision completes around line 169 and the capacity gate `TryIncrementInFlight` runs around line 201. The QoS `await AdmitAsync(...)` goes **between them**, inside an `inference.qos.admit` span opened on `ConductorTelemetry.InferenceSource` so it nests under `inference.proxy`. Because the scheduler only releases when a slot is genuinely free, the subsequent `TryIncrementInFlight` almost always succeeds; the rare lost race falls through to the existing `429` path, now a backstop rather than the primary behavior.

---

## Data & storage model (normalized, database-only)

All QoS configuration lives in the database. Nothing goes in `conductor.json`. The rule-bearing structure is normalized into separate tables rather than JSON blobs; only genuinely free-form metadata (`labels`/`tags`/`metadata`) uses JSON columns, consistent with every other entity. Every table below is created by an on-startup migration in all four dialects.

**`qos_profiles`** — one row per profile.
`id` (PK, `qos_` prefix) · `tenantid` (FK `tenants(id)`) · `name` · `description` · `isdefault` (bool; the non-deletable per-tenant default FIFO profile) · `active` · `defaultclass` (class when no rule matches) · `ingressmode` (`Single`|`Router`) · `ingressdefaultnode` · `tailnode` · `maxtotaldepth` (0=unbounded) · `maxqueuewaitms` · `rejectionstatuscode` (default 429) · `includeretryafter` · `retryafterseconds` · `createdutc` · `lastupdateutc` · `labels`/`tags`/`metadata` (JSON). Indexes: `tenantid`, `active`, `name`, `(tenantid, isdefault)`.

**`qos_classifier_rules`** — ordered classification rules; first match wins.
`id` (PK) · `profileid` (FK `qos_profiles(id)`) · `ordinal` · `source` (`Header`|`BodyJsonPath`|`QueryParam`|`Model`|`ApiFamily`|`RequestType`|`Tenant`|`Credential`|`User`|`ClientIp`|`Vmr`) · `matchkey` (e.g. the header name, JSON path, or blank when the source is itself the value) · `operator` (`Equals`|`Contains`|`Regex`|`Exists`|`GreaterThan`|`LessThan`) · `matchvalue` · `classname`. Index: `(profileid, ordinal)`.

**`qos_queue_nodes`** — one row per queue in the topology.
`id` (PK) · `profileid` (FK) · `name` (unique per profile) · `discipline` (`Fifo`|`Lifo`|`Priority`|`Wfq`|`Cbwfq`|`Llq`|`Wrr`) · `maxdepth` (0=unbounded) · `overflowpolicy` (`Reject`|`DropNewest`|`DropOldest`|`Block`) · `agingthresholdms` (priority) · `flowsource`/`flowkey` (WFQ flow key input, reusing the classifier-source vocabulary) · `unknownkeypolicy` · `defaultkey` · `defaultweight` · `wrrmode` (`Balancer`|`Classifier`) · `enableperclassmetrics` · `enabletracing`. Index: `(profileid, name)`.

**`qos_queue_classes`** — the per-node class/flow/band/sub-queue rows, unified.
`id` (PK) · `nodeid` (FK `qos_queue_nodes(id)`) · `ordinal` · `kind` (`Band`|`Flow`|`Class`|`PriorityClass`|`FairClass`|`SubQueue`) · `classname` · `weight` · `band` · `rateperssecond` · `burst` (LLQ token-bucket). Index: `(nodeid, ordinal)`.

**`qos_queue_links`** — the edges of the hierarchy.
`id` (PK) · `profileid` (FK) · `fromnode` · `tonode`. Index: `profileid`.

**`qos_ingress_routes`** — class → ingress node, for `Router` ingress mode.
`id` (PK) · `profileid` (FK) · `ordinal` · `classname` · `node`. Index: `profileid`.

**`qos_traffic_classes`** — the tenant-scoped catalog of named traffic classes (the reusable class vocabulary; profiles' classifier rules and queue-class rows reference these names). Seeded per tenant (see Step 7).
`id` (PK, `qtc_` prefix) · `tenantid` (FK `tenants(id)`) · `name` (unique per tenant, e.g. `human-interactive`) · `description` · `tier` (`QosClassTierEnum`: `Realtime`|`Interactive`|`AgentInteractive`|`BatchTimebound`|`BatchBackground`|`Default` — a suggested scheduling tier a profile can adopt) · `issystem` (bool; the seeded standard classes) · `createdutc` · `lastupdateutc`. Index: `(tenantid, name)`.

**`virtualmodelrunners.qosprofileid`** — new `TEXT` column, added by migration. Nullable at the SQL level (so the `ALTER` and the backfill can run), but **required by application logic** on VMR create/update. Existing rows are backfilled to their tenant's default profile by the startup pass (there is no in-migration `UPDATE` precedent in this codebase; backfill happens in app code).

Deletion cascades are handled in application code (delete child rows, then the profile) rather than relying on SQL `ON DELETE CASCADE`, matching `TenantController.DeleteAssociatedDataAsync`. On profile update, child rows are replaced (delete-and-reinsert) inside the same operation.

A concrete profile — the user's "LLQ + WFQ → priority" example — is represented as: two ingress nodes (`realtime` = `Llq` with a rate-limited `interactive` priority class; `fair` = `Wfq` keyed on tenant), an `egress` = `Priority` tail with `interactive`→band 0 and `standard`→band 1, `qos_ingress_routes` sending `interactive`→`realtime` with `ingressdefaultnode=fair`, and `qos_queue_links` `realtime→egress`, `fair→egress`. The scheduler drains `egress`.

---

## Step 1 — Branch and version bump (do this first)

- [ ] `git checkout -b feature/qos` off `main`.
- [ ] Bump `<Version>` `0.4.0 → 0.5.0` in `src/Conductor.Core/Conductor.Core.csproj`, `src/Conductor.Server/Conductor.Server.csproj`, `src/Conductor.McpServer/Conductor.McpServer.csproj`.
- [ ] Bump `<Version>` `0.4.0 → 0.5.0` in the aligned test projects: `Test.Shared`, `Test.Xunit`, `Test.Nunit`, `Test.McpServer`, `Test.Automated`.
- [ ] Update `docker/compose.yaml` image tags `jchristn77/conductor-server` and `jchristn77/conductor-dashboard` `v0.2.0 → v0.5.0` (aligning the lagging tags to the product version).
- [ ] Add a `## [0.5.0]` section to `CHANGELOG.md` (Keep-a-Changelog headings; fill the Added items as the work lands).
- [ ] Update any version string shown in `README.md`, `DOCKERHUB_README.md`, and the dashboard server-info surface to `0.5.0`.
- [ ] Commit: `chore: branch feature/qos and bump to 0.5.0`.

---

## Step 2 — Core models, enums, and id generation (`src/Conductor.Core`)

- [ ] Add `IdGenerator.QosProfilePrefix = "qos_"` and `NewQosProfileId()` in `src/Conductor.Core/Helpers/IdGenerator.cs`.
- [ ] Add enums under `Enums/`: `QosDisciplineEnum`, `QosClassifierSourceEnum`, `QosClassifierOperatorEnum`, `QosOverflowPolicyEnum`, `QosUnknownKeyPolicyEnum`, `QosIngressModeEnum`, `QosQueueClassKindEnum`.
- [ ] Add the profile aggregate model `Models/QosProfile.cs` holding the assembled sub-collections (`Rules`, `Nodes` with their `Classes`, `Links`, `IngressRoutes`) plus the scalar/limit fields, with `FromDataRow`/`FromDataTable` and the standard `labels/tags/metadata` `…Json` twins.
- [ ] Add the sub-models: `QosClassifierRule`, `QosQueueNode`, `QosQueueClass`, `QosQueueLink`, `QosIngressRoute` (each with `FromDataRow`), following one-type-per-file and `CLAUDE.md` style.
- [ ] Add `Models/QosTrafficClass.cs` (with `FromDataRow`/`FromDataTable`), the `QosClassTierEnum`, and `IdGenerator.QosTrafficClassPrefix = "qtc_"` / `NewQosTrafficClassId()`.
- [ ] Confirm the solution builds warning-free.

---

## Step 3 — Normalized tables and startup migrations (all four dialects)

For **each** of `Database/{Sqlite,PostgreSql,SqlServer,MySql}/Queries/TableQueries.cs`:

- [ ] Add `CreateQosProfilesTable`, `CreateQosClassifierRulesTable`, `CreateQosQueueNodesTable`, `CreateQosQueueClassesTable`, `CreateQosQueueLinksTable`, `CreateQosIngressRoutesTable`, `CreateQosTrafficClassesTable` DDL constants (columns per the storage model above; model them on `CreateLoadBalancingPoliciesTable`).
- [ ] Add `AddQosProfileIdColumn` = `ALTER TABLE virtualmodelrunners ADD COLUMN qosprofileid TEXT;` mirroring `AddLoadBalancingPolicyIdColumn`.

For **each** driver (`Database/{dialect}/{Dialect}DatabaseDriver.cs`):

- [ ] Register the seven `CreateQos*Table` constants in the `InitializeAsync` create-list (e.g. `SqliteDatabaseDriver.cs` lines ~56–75).
- [ ] Wire `AddQosProfileIdColumn` through `EnsureColumnAsync("virtualmodelrunners", "qosprofileid", TableQueries.AddQosProfileIdColumn, token)` in `RunMigrationsAsync` (idempotent; the existing migration mechanism runs on every startup).
- [ ] Add indexes via `EnsureIndexAsync` for the QoS tables.
- [ ] Verify on a copy of an existing SQLite DB that startup adds the tables and the `qosprofileid` column without touching existing data.

---

## Step 4 — Database methods, driver wiring, and the VMR column

- [ ] Add `Database/Interfaces/IQosProfileMethods.cs`: `CreateAsync`, `ReadAsync`, `ReadByIdAsync`, `UpdateAsync`, `DeleteAsync`, `ExistsAsync`, `EnumerateAsync`, plus `ReadDefaultAsync(tenantId)` and `EnsureDefaultAsync(tenantId)` — all taking a `CancellationToken`.
- [ ] Implement it in all four `Database/{dialect}/Implementations/QosProfileMethods.cs`. Reads assemble the aggregate across the six tables; writes insert the profile then its child rows; updates delete-and-reinsert child rows; deletes remove child rows then the profile (application-level cascade). Use the `Sanitize`/`FormatNullableString`/`FormatBoolean`/`FormatDateTime` idioms from `LoadBalancingPolicyMethods`.
- [ ] Add `Database/Interfaces/IQosTrafficClassMethods.cs` and its four implementations (`Create`/`Read`/`Update`/`Delete`/`Exists`/`Enumerate` by tenant); expose as `Database.QosTrafficClass`.
- [ ] Add the `QosProfile` and `QosTrafficClass` properties to `DatabaseDriverBase` and instantiate them in each driver constructor.
- [ ] Add `string QosProfileId` to `VirtualModelRunner`: read it in `FromDataRow` guarded by `row.Table.Columns.Contains("qosprofileid")`, and add it to every INSERT and UPDATE column list in all four `VirtualModelRunnerMethods` (all four change together).
- [ ] Build warning-free.

---

## Step 5 — Compiler, runtime, admission service, scheduler (`src/Conductor.Server`)

- [ ] Add the `QoSKit` `0.2.0` package reference to `Conductor.Server.csproj` (not `Core` — keep Core dependency-light).
- [ ] Add `Services/QosAdmissionTicket.cs` (Band, ClassKey, FlowKey, Cost, EnqueuedTicks, RequestAborted token, `TaskCompletionSource<bool> Release`).
- [ ] Add `Services/QosProfileCompiler.cs`: compile classifier rules → `Func<RequestContext, QosAdmissionTicket>` (compile regexes/JSON paths once); build each node's QoSKit queue from `qos_queue_nodes` + `qos_queue_classes`; wire the `QoSPipeline`/`QoSRouter` from links + ingress; validate acyclic (catch `PipelineCycleException`); return a `QosRuntime`.
- [ ] Add `Services/QosRuntime.cs` (owns the queues, pipeline, tail, and classifier; disposable).
- [ ] Add `Services/QosAdmissionService.cs`: per-VMR runtime cache, `AdmitAsync`, the scheduler loop, the capacity gate (free slots = Σ over endpoints of `max(0, MaxParallelRequests − inFlight)`; `0` = unbounded → immediate release), and the reservation counter. Subscribe to the health-check completion signal to wake the gate. Dispose cancels parked waiters.
- [ ] Add `Services/QosAdmissionResult.cs` (`Admitted` | `Rejected(status, retryAfter, reason)` | `Aborted`).
- [ ] Reject `Block` overflow on any ingress-reachable node at compile time (a blocking enqueue would stall the proxy thread).
- [ ] Unit-test the scheduler against a mock capacity gate **before** touching the proxy (see Step 13).

---

## Step 6 — Proxy integration

- [ ] In `ProxyController.HandleRequest`, insert `await _QosAdmissionService.AdmitAsync(vmr, requestContext, ctx.Request.Aborted)` between the routing decision (~line 169) and `TryIncrementInFlight` (~line 201), inside an `inference.qos.admit` span on `ConductorTelemetry.InferenceSource`.
- [ ] On a non-admitted result, add `SendQosRejection(ctx, result)` (emits the QoS denial metric, sets `429`/`Retry-After`, returns) — superseding the synchronous `EndpointAtCapacity` path for the common case.
- [ ] Re-validate the selected endpoint after release (a long wait can stale the routing pick); on a stale pick, fall through to the existing `TryIncrementInFlight` fallback. Verify against `RoutingDecisionService`.
- [ ] Thread `QosAdmissionService` into the controller via `ConductorRouteContext`.

---

## Step 7 — Startup seeding, tenant lifecycle (create, cascade, nuke), backfill, delete guards

- [ ] Add a `QosProfileFactory` with:
  - `BuildDefaultFifo(tenantId)` — the required, non-deletable VMR-default profile: one `Fifo` node (`name="default"`, `maxdepth=0`, overflow `Reject`), no classifier rules, `defaultclass="default"`, `ingressmode=Single`, `tailnode="default"`, `maxqueuewaitms=30000`, `rejectionstatuscode=429`, `includeretryafter=true`, `retryafterseconds=5`, `isdefault=true`, `name="Default (FIFO)"`.
  - `StandardTrafficClasses()` — the seeded standard class set (`realtime`, `human-interactive`, `agent-interactive`, `batch-time-bound`, `batch-background`, `default`; see the [standard classes reference](#standard-traffic-classes-seeded-per-tenant)).
  - `BuildStandardWorkloads(tenantId)` — an opinionated multi-class profile referencing those classes: a single `Llq` node with `realtime` and `human-interactive` as rate-limited strict-priority classes and `agent-interactive` (weight 8), `batch-time-bound` (weight 3, node aging on), `default` (weight 2), `batch-background` (weight 1, overflow `DropOldest`) as weighted-fair classes; classification by the `X-Conductor-Class` header (value → class) with `defaultclass="default"`; `isdefault=false`. This is the worked example of the class model and a ready starting point to clone.
- [ ] Add `EnsureTenantQosDefaultsAsync(tenantId)`: (1) ensure the non-deletable `Default (FIFO)` profile exists; (2) **once**, seed the standard traffic classes into `qos_traffic_classes` and create the `Standard Workloads` profile — guarded by a `tenant.Metadata["qosStandardSeeded"]="true"` marker so an operator's later deletion is not resurrected on the next startup; (3) backfill any VMR with a null `QosProfileId` to the tenant's default profile id.
- [ ] Call `EnsureTenantQosDefaultsAsync` from an idempotent startup pass in `Program` (ConductorServer.cs) after `InitializeAsync`, **independent of** the fresh-DB `InitializeFirstRunAsync` guard, iterating every tenant so existing deployments are seeded on upgrade and the default tenant is covered on first run.
- [ ] Call `EnsureTenantQosDefaultsAsync(tenant.Id)` from `TenantController.Create` after `Database.Tenant.CreateAsync(...)`, so every **new tenant on creation** gets its default FIFO profile, the standard traffic classes, and the Standard Workloads profile.
- [ ] Delete guard: `QosProfileController.Delete` throws `WebserverException(ApiResultEnum.BadRequest, "Cannot delete the default QoS profile")` when `isdefault` is set (mirrors `AdministratorController.Delete` self-delete guard).
- [ ] Reference handling on delete of a non-default profile: reassign referencing VMRs to the tenant's default profile (VMRs require a profile, so nulling is not an option), then delete the profile and its children.

### Tenant cascade and the "nuke tenant" API

The current `TenantController.DeleteAssociatedDataAsync` (lines ~113–160) cascades VMRs, model configs/definitions, endpoints, credentials, and users — but **not** load-balancing policies or QoS. Centralize the cascade so both the normal delete and the new nuke path clean up everything, QoS included.

- [ ] Add `Services/TenantPurgeService.cs` with `Task<TenantPurgeReport> PurgeAsync(string tenantId, IProgress<TenantPurgeProgress> progress, CancellationToken token)`. Delete in leaf-first dependency order, reporting one progress event per category with a running deleted-count: request history/analytics, sessions, reservations, VMRs, endpoint groups, model-runner endpoints, model configurations, model definitions, credentials, users, load-balancing policies, model-access policies, **QoS profiles and their five child tables** (`qos_classifier_rules`, `qos_queue_nodes`, `qos_queue_classes`, `qos_queue_links`, `qos_ingress_routes`), **the tenant's `qos_traffic_classes`**, and finally the tenant row. Continue-on-error per category and record failures in the report.
- [ ] Add `Models/TenantPurgeReport.cs` and `Models/TenantPurgeProgress.cs` (per-category `{ category, deletedCount, status, error }`).
- [ ] Route the existing `TenantController.Delete` / `DeleteAssociatedDataAsync` through `TenantPurgeService.PurgeAsync` so deleting a tenant now also cleans up QoS config (and LB/model-access policies).
- [ ] Add the nuke endpoint `POST /v1.0/tenants/{id}/purge` in `TenantRouteModule`. **System-admin only:** register with `auth: true` and require `req.Http.Metadata is Services.AdminAuthenticationResult` (a system administrator); reject a tenant-scoped `AuthenticationResult` with `403 Forbidden` (this is the gate `AdministratorRouteModule` uses — the tenant CRUD routes today are not admin-gated, so the nuke route must add the check explicitly).
- [ ] Require confirmation in the body: `{ "confirmTenantId": "<id>" }` must equal the path `{id}`, else `400 BadRequest` — a server-side backstop for the dashboard's type-the-ID gate.
- [ ] Return `TenantPurgeReport` (200). Stream per-category progress as chunked newline-delimited JSON events for a live progress window (WatsonWebserver supports chunked responses); if streaming is deferred, return the full itemized report and have the dashboard render it as a completed checklist.
- [ ] Guard the default tenant: refuse to purge the reserved `"default"` tenant (or require an extra explicit flag), mirroring the spirit of the self-delete guard.

---

## Step 8 — Controller, route module, registry, and VMR-requires-profile

- [ ] Add `Controllers/QosProfileController.cs` (`Create`, `Read`, `Update`, `Delete`, `Enumerate`, `Validate`, `GetClassifierCatalog`). `Validate` dry-run-compiles and returns structured errors (unknown class references, cyclic topology, `maxqueuewaitms` above the VMR timeout, `Block` on an ingress-reachable node, a topology mentioning a class the classifier can't produce). `GetClassifierCatalog` returns the available `source` values, operators, **and the tenant's traffic classes** for the UI.
- [ ] Add `Routing/QosProfileRouteModule.cs`: `GET /v1.0/qosprofiles/classifier-catalog`, `POST /v1.0/qosprofiles` (201), `POST /v1.0/qosprofiles/validate`, `GET /v1.0/qosprofiles/{id}`, `PUT /{id}`, `DELETE /{id}` (204), `GET /v1.0/qosprofiles` (list).
- [ ] Add `Controllers/QosTrafficClassController.cs` and `Routing/QosTrafficClassRouteModule.cs`: `POST /v1.0/qostrafficclasses` (201), `GET /v1.0/qostrafficclasses/{id}`, `PUT /{id}`, `DELETE /{id}` (204), `GET /v1.0/qostrafficclasses` (list) — tenant-scoped CRUD over the class catalog. Deleting an `issystem` standard class is allowed but warns if any profile references it.
- [ ] Register both controllers in `ConductorRouteContext` and `ConductorRouteModule`, and both modules in `ConductorRouteRegistry` (before `VirtualModelRunnerRouteModule`).
- [ ] **Require the profile on VMR write:** in `VirtualModelRunnerController.Create` and `Update`, reject a missing/empty `QosProfileId` with `BadRequest`, and validate it exists in the tenant via a new `ValidateQosProfileAsync(tenantId, vmr.QosProfileId)` (mirrors `ValidateLoadBalancingPolicyAsync`).
- [ ] Confirm `Create` without a `QosProfileId` returns `400`, and with an unknown id returns `400`.

---

## Step 9 — Telemetry and Prometheus exposure

- [ ] Add `"QoSKit"` to `ConductorTelemetry.MeterNames` and `ConductorTelemetry.ActivitySourceNames` (the host loops these into `AddMeter`/`AddSource`, so QoSKit's per-class metrics and `queue.enqueue`/`queue.dequeue`/`link.move` spans export through the existing pipeline — no new exporter).
- [ ] Register an explicit-bucket view for `qoskit.queue.wait.duration` (milliseconds) in `ConductorTelemetry.HistogramBuckets` with ms-appropriate boundaries (QoSKit uses ms; Conductor's own histograms use seconds).
- [ ] Add Conductor-side QoS instruments to `ConductorTelemetry`, tagged the way inference metrics are (`vmr`, `qos_class`, closed-set): `conductor.qos.admissions` (counter; `outcome` = admitted/rejected/timed_out/aborted), `conductor.qos.queue.wait.duration` (histogram, seconds), `conductor.qos.queue.depth` (observable gauge), `conductor.qos.rejections` (counter; `reason` = queue_full/total_depth/wait_timeout). Add their meter/source names and buckets to the subscription lists.
- [ ] Set `enableperclassmetrics=false` on any WFQ node whose flow source is high-cardinality (per-tenant/user), and surface a warning in the UI next to such a node.
- [ ] **Prometheus (hard requirement):** confirm — no config change needed for the bundled stack. QoS metrics push over OTLP to the collector, which re-exposes them on its Prometheus exporter at `:8889` (`docker/otel/otel-collector-config.yaml`); `docker/prometheus/prometheus.yaml` already scrapes `otel-collector:8889`. Verify `qoskit_*` and `conductor_qos_*` series appear in Prometheus with the stack up.
- [ ] Document and uncomment the `conductor-direct` scrape job in `prometheus.yaml` as the supported collector-free path (in-process `AddPrometheusHttpListener`, `:9464/metrics`).

---

## Step 10 — MCP tool surface

- [ ] Add QoS-profile MCP tools to `Conductor.McpServer`: `create_qos_profile`, `get_qos_profile`, `list_qos_profiles`, `update_qos_profile`, `delete_qos_profile`, `validate_qos_profile`.
- [ ] Update `MCP_API.md` with each tool's input schema and examples (`REPOSITORY_REQUIREMENTS.md` §14).

---

## Step 11 — Dashboard

- [ ] Add API-client methods in `dashboard/src/api/api.js` (mirror the load-balancing block ~lines 490–521, routed through `request`/`dedupedRequest`): `listQosProfiles`, `getQosProfile`, `createQosProfile`, `validateQosProfile`, `updateQosProfile`, `deleteQosProfile`, `getQosClassifierCatalog`, and the traffic-class set `listQosTrafficClasses`, `createQosTrafficClass`, `updateQosTrafficClass`, `deleteQosTrafficClass`.
- [ ] Add a **Traffic Classes** management surface (a `QosTrafficClasses.jsx` view, or a tab within the QoS Profiles page) for the tenant class catalog — list/create/edit/delete, with the seeded standard classes shown and their `tier`. The profile editor's classifier and topology sections pick class names from this catalog rather than free text.
- [ ] Add `@xyflow/react` to `dashboard/package.json` and verify it renders under React 19.
- [ ] Add `dashboard/src/components/QueueHierarchyDiagram.jsx` — an interactive node/edge editor over `formData.Topology`; nodes are disciplines (name, discipline, depth), edges are links, ingress/router and tail are visually marked; rejects a drawn cycle with the backend's message.
- [ ] Add `dashboard/src/views/QosProfiles.jsx` (copy `LoadBalancingPolicies.jsx`): list via `DataTable`; wide `Modal` create/edit with three sections — **Classification** (structured rule editor: `source` `<select>` from the catalog, `matchkey` input, `operator` `<select>`, `matchvalue`, `classname`, plus default class), **Topology** (the diagram, same state), **Limits** (numeric inputs with UI clamping + the wait-vs-timeout warning). Reuse `ActionMenu`, `DeleteConfirmModal`, `StatusIndicator`, `CopyableId`, `LabelsTagsEditor`, `ViewMetadataModal`. Row actions View/Edit/Duplicate/View JSON/Delete; loading/empty/error states.
- [ ] Add a `QOS_TEMPLATES` starter set (FIFO, Two-tier priority, Weighted-fair by tenant, LLQ+fair→priority) analogous to `POLICY_TEMPLATES`.
- [ ] Add the route in `dashboard/src/App.jsx` (`/qos-profiles`) and a `navItems` entry in `dashboard/src/components/Sidebar.jsx` (after Load Balancing Policies), role-gated to tenant admins.
- [ ] **Make the VMR profile link required in the UI:** in `dashboard/src/views/VirtualModelRunners.jsx`, add a **QoS Profile** `<select>` beside the existing policy selectors — options from `listQosProfiles` filtered to the tenant, **required (no "none")**, pre-selecting the tenant's default FIFO profile — wired into formData, edit/clone mapping, payload, and a resolved-name list column.
- [ ] Add a direct link to the "Conductor — QoS & Queueing" Grafana dashboard in the existing "Observability & Tooling" section of `dashboard/src/views/Dashboard.jsx` (the `observabilityServices` cards already exist at the bottom with default credentials; QoS adds one deep link, it does not re-implement the section).
- [ ] **Nuke Tenant UI (system-admin only).** Add a "Nuke Tenant" action, gated to global admins (`isAdmin`, the same gate `App.jsx` uses for admin-only routes) and hidden from tenant admins. Add `api.purgeTenant(tenantId, { confirmTenantId })` to `api.js`.
- [ ] A dedicated confirmation modal (not the standard `DeleteConfirmModal`) that requires the operator to type the exact tenant ID before the destructive button enables (GitHub-style "type the name to confirm").
- [ ] A progress window: on confirm, call the purge and render each category as `deleting → deleted (N)`, live if the endpoint streams chunked progress, otherwise as a completed checklist from the returned `TenantPurgeReport`; show per-category failures distinctly and a final summary. Disable the modal's close while running.
- [ ] i18n posture: match the dashboard's existing English-only posture (it ships no i18n framework); record "dashboard has no i18n infrastructure" as a pre-existing gap in the handoff rather than introducing `i18next` for one feature.
- [ ] Visual QA: desktop/tablet/mobile × light/dark for the list, create/edit modal, diagram editor, VMR selector, and the nuke-tenant confirm + progress window. Rebuild the dashboard Docker image after changes.

---

## Step 12 — Grafana: native, organized integration of all QoSKit metrics and traces

The goal is that **every** QoSKit signal is a first-class, organized part of the bundled Grafana, not just a couple of hand-picked panels — so an operator can see per-class behavior end to end without building anything.

- [ ] Create a dedicated Grafana folder **"Conductor — QoS & Queueing"** under `docker/grafana/dashboards/qos/` (the provisioning tree already builds folders from the file structure).
- [ ] Metrics dashboard `conductor-qos-metrics.json` covering **every** QoSKit instrument, grouped into rows: **Admission** (`conductor_qos_admissions_total` by outcome, `conductor_qos_rejections_total` by reason, `conductor_qos_queue_depth`, `conductor_qos_queue_wait_duration_seconds` quantiles) · **Throughput** (`qoskit_queue_enqueued_total`/`qoskit_queue_dequeued_total` and their `_bytes`, per `queue_class`) · **Backpressure & drops** (`qoskit_queue_dropped_total` by `drop_reason`, `qoskit_queue_rejected_total`, `qoskit_queue_depth`, `qoskit_queue_capacity`, fill ratio, `qoskit_queue_peak_depth`, `qoskit_queue_resident_bytes`) · **Latency** (`qoskit_queue_wait_duration_milliseconds` p50/p95/p99 **per class**) · **Policer** (`qoskit_policer_conformed_total` vs `qoskit_policer_exceeded_total` per priority class). Templated variables for `vmr`, `queue_name`, and `queue_class`.
- [ ] Traces: ensure the Tempo datasource surfaces the QoSKit spans (`queue.enqueue`, `queue.dequeue`, `link.move`) and the Conductor `inference.qos.admit` span, with a dashboard panel/Explore link that filters Tempo to QoS spans and a service-graph/trace-to-logs correlation consistent with the existing datasource provisioning (exemplars from the wait histogram link to a representative trace).
- [ ] Confirm the collector already carries all of it (QoSKit rides the same OTLP pipeline; no collector edit) and that all PromQL resolves against the stock Prometheus fed by `otel-collector:8889`.
- [ ] Add the QoS folder's dashboards to any seeded/demo Grafana profile so both stacks render identically, and add the direct home-page link (Step 11) to the metrics dashboard.
- [ ] Add a "QoS & queueing" subsystem section to `TELEMETRY.md` documenting every subscribed `QoSKit` instrument and span, the `conductor.qos.*` instruments, the Grafana folder and its panels, and both Prometheus paths.

---

## Step 13 — Tests (Touchstone, per `BACKEND_TEST_ARCHITECTURE.md`)

Descriptors live once in `Test.Shared`, run through `Test.Automated`/`Test.Xunit`/`Test.Nunit`; no console output; self-contained; `127.0.0.1` never `localhost`.

**Coverage bar:** every new QoS capability is covered, and **every test has a positive and a negative variant** — the success path *and* the rejection/failure/edge path. The suite must move overall coverage toward 100%; run a coverage pass (the `coverlet.collector` already referenced by the test projects) and close gaps on new QoS code paths until every branch of the compiler, admission service, scheduler, controllers, seeding, and purge is exercised. Each bullet below is a positive/negative pair.

- [ ] **Classification — per source and operator.** Positive: each `source` (Header, Credential, User, Model, BodyJsonPath, QueryParam, ApiFamily, RequestType, Tenant, Vmr, ClientIp) and each operator (Equals/Contains/Regex/Exists/GreaterThan/LessThan) resolves to the expected class — explicitly including custom-header and specific-credential mapping. Negative: a non-matching value falls through to the default class; a malformed regex / bad JSON path is rejected at compile time, not at request time.
- [ ] **Compilation — per discipline.** Positive: every discipline (Fifo/Lifo/Priority/Wfq/Cbwfq/Llq/Wrr) compiles to the right QoSKit type with the right params; a valid hierarchy (Merge/ChainTo, Router ingress) builds. Negative: cyclic topology throws; unknown class reference rejected; `Block` on an ingress-reachable node rejected; a topology referencing a class the classifier can't produce rejected; a `maxqueuewaitms` above the VMR timeout warns/rejects per policy.
- [ ] **Admission & scheduling** (mock capacity gate). Positive: immediate admit when slots free; queue builds when saturated and release order honors each discipline (priority band, WFQ weight ratios, LLQ priority-over-fair, WRR proportions, aging promotion). Negative: no release while saturated; a higher-priority late arrival does not preempt an in-hand ticket; unbounded-capacity endpoints never queue.
- [ ] **Overflow & timeout.** Positive: within depth, all admit; `DropOldest`/`DropNewest` shed the right item and count the drop with the right `drop.reason`. Negative: `Reject` at `maxdepth` and at `maxtotaldepth` → `429 + Retry-After`; wait past the deadline → `429 + Retry-After`; client-abort tombstone frees no slot and is skipped at the head.
- [ ] **Reservation accounting.** Positive: releases match freed slots one-for-one. Negative: a burst of frees never over-releases beyond available capacity; a failed post-release `TryIncrementInFlight` returns the reservation.
- [ ] **Persistence & aggregate mapping.** Positive: a profile round-trips across all tables; update replaces child rows; enumerate/read-by-id return the assembled aggregate; the migration adds tables/column idempotently on an existing DB. Negative: reading a missing profile returns null/404; a malformed aggregate (duplicate node name, missing tail) is rejected on write.
- [ ] **Traffic-class catalog.** Positive: CRUD over `qos_traffic_classes`; seeded standard set present with correct tiers. Negative: duplicate class name per tenant rejected; deleting a class referenced by a profile warns; unknown id → 404.
- [ ] **Seeding & lifecycle.** Positive: startup and tenant-create seed the default FIFO profile, the standard classes, and the Standard Workloads profile; existing VMRs backfill to the default; the `qosStandardSeeded` marker is set. Negative: re-running startup does not duplicate or resurrect deleted standard classes/profiles; the default FIFO profile cannot be deleted (`400`); deleting a non-default profile reassigns referencing VMRs to the default rather than orphaning them.
- [ ] **VMR-requires-profile.** Positive: create/update with a valid tenant profile succeeds and the link resolves. Negative: create/update with a missing profile → `400`; with an unknown id → `400`; with a profile from another tenant → `400`.
- [ ] **Tenant purge / nuke.** Positive: `PurgeAsync` deletes every category including QoS across all seven tables, leaves no orphans, and the report counts match; a normal tenant delete also removes QoS config; a system admin invoking the nuke endpoint succeeds. Negative: a tenant-admin caller → `403`; a `confirmTenantId` mismatch → `400`; the reserved `"default"` tenant is protected; a mid-purge failure in one category is recorded and the purge continues.
- [ ] **Telemetry.** Positive: with in-process `MeterListener`/`ActivityListener` on `QoSKit` + Conductor sources, admission emits the expected instruments with the right tags and `inference.qos.admit` nests under `inference.proxy`; per-class tags present. Negative: `enableperclassmetrics=false` drops the class tag; a queue with metrics disabled emits nothing.
- [ ] Run a coverage report; confirm new QoS code paths are covered and note residual gaps. All three runners green.

---

## Step 14 — Documentation revision (all docs) and SDKs

This is the required, comprehensive documentation sweep for the release — **every** doc that touches the changed surface is revised to describe the new capabilities, what changed, and how to use them, with examples. CHANGELOG stays a summary; README carries the detail; a new `QOS_OVERVIEW.md` is the end-to-end guide.

- [ ] **Create `QOS_OVERVIEW.md`** — the end-to-end QoS guide, structured as: what QoS in Conductor does and why (the problem it solves) · core concepts (profiles, traffic classes, disciplines, hierarchy, admission-vs-capacity) · the standard traffic classes and the `Standard Workloads` example walked through · **how to configure** (create/edit a profile in the dashboard and via REST; classify by custom header and by specific credential with concrete request examples; link a profile to a VMR; the required-link and default-FIFO behavior; overflow/`Retry-After`) · **how to monitor** (the Grafana "Conductor — QoS & Queueing" folder and its panels, the `qoskit_*`/`conductor_qos_*` Prometheus series with example PromQL, the `inference.qos.admit` trace segment in Tempo) · operations (the system-admin nuke/purge flow) · the disciplines catalog (FIFO/LIFO/priority/WFQ/CBWFQ/LLQ/WRR) with when to use each. Link it from README and the dashboard.
- [ ] **`README.md` (detailed)** — a full QoS section: what it is, the profile/class/discipline model, classification sources (custom header, specific credential, model, body attribute, …) with a worked example, linking to a VMR, the seeded default classes and profiles, the required-link behavior, and a monitoring pointer; link to `QOS_OVERVIEW.md`. Re-read the whole README for accuracy per `CLAUDE.md`.
- [ ] **`CHANGELOG.md` `[0.5.0]` (summary)** — concise Added/Changed entries: per-VMR QoS profiles and queueing, seeded default traffic classes and profiles, required profile link on VMR create, QoSKit telemetry integrated into Grafana/Prometheus, tenant nuke/purge API, dashboard management. One-line-per-capability, with a pointer to `QOS_OVERVIEW.md` for detail.
- [ ] **`REST_API.md`** — the seven `/v1.0/qosprofiles` endpoints, the five `/v1.0/qostrafficclasses` endpoints, and `POST /v1.0/tenants/{id}/purge` (methods, params, bodies, status codes, the system-admin `403` and `confirmTenantId` `400`, examples).
- [ ] **`MCP_API.md`** — confirmed in sync with Step 10 tools.
- [ ] **`TELEMETRY.md`** — confirmed in sync with Step 12 (all QoSKit + `conductor.qos.*` signals and the Grafana folder).
- [ ] **`DOCKERHUB_README.md`** — reflect the QoS capability in the feature summary with explicit asset URLs.
- [ ] **Postman collection** (`assets/postman/…`) — documented "QoS Profiles", "Traffic Classes", and "Tenant Purge" folders using the existing base-URL/token variables.
- [ ] **SDKs** — add QoS-profile and traffic-class CRUD to `sdk/python`, `sdk/javascript`, `sdk/csharp` with harness and README updates, loopback on `127.0.0.1`. *(Largest optional-scope lever: if the release must be smaller, SDK coverage is the natural `0.5.x` fast-follow — note the tradeoff in the PR.)*
- [ ] Sweep every other doc that references the VMR lifecycle, tenants, or telemetry for accuracy against the shipped behavior.

---

## Step 15 — Final verification

- [ ] Full solution builds warning-free.
- [ ] `Test.Automated`, `Test.Xunit`, `Test.Nunit` all green.
- [ ] `docker compose up` smoke test: create a profiled VMR, induce saturation (low `MaxParallelRequests`), watch requests queue and release in order; confirm a full queue returns `429 + Retry-After`.
- [ ] Grafana "Conductor — QoS & Queueing" dashboard renders from Prometheus; a slow request in Tempo shows the `inference.qos.admit` segment.
- [ ] Confirm every VMR (including default-FIFO ones) appears in the QoS metrics, and that the Grafana QoS folder shows every QoSKit instrument organized into its rows.
- [ ] Confirm each tenant is seeded with the standard traffic classes and the Standard Workloads profile, and a fresh tenant created via the API gets them too.
- [ ] Dashboard visual QA complete across themes and viewports (profiles list, profile editor + diagram, traffic-classes surface, VMR selector, nuke confirm + progress window).
- [ ] Test coverage report reviewed; every new QoS capability has positive and negative tests; residual gaps noted.
- [ ] **All documentation revised** (the last step): `QOS_OVERVIEW.md` created; `README.md` details + examples; `CHANGELOG.md` `[0.5.0]` summary; `REST_API.md`, `MCP_API.md`, `TELEMETRY.md`, `DOCKERHUB_README.md`, and the Postman collection match the shipped surface.
- [ ] Version reads `0.5.0` everywhere it is declared (csproj, compose tags, docs).

---

## Classifier reference — what the operator can key on

The whole point of classification is that the operator decides *what dictates the mapping*. A rule names a **source**, a **key** into that source, an **operator**, a **value**, and the **class** to assign. First match wins; unmatched requests take the profile's default class. Every source below is already populated on `RequestContext` at proxy time or trivially derived from the parsed body.

| Source | `matchkey` means | Example rule |
| --- | --- | --- |
| `Header` | the custom header name | `Header · X-Team · Equals · payments · → gold` |
| `Credential` | (blank; value is the credential id/name) | `Credential · · Equals · cred_abc123 · → priority` |
| `User` | (blank; value is the user id/email) | `User · · Equals · ops@acme.com · → interactive` |
| `Model` | (blank; value is the requested model) | `Model · · Contains · embed · → bulk` |
| `BodyJsonPath` | a JSON path into the request body | `BodyJsonPath · $.stream · Equals · true · → interactive` |
| `QueryParam` | the query-string parameter name | `QueryParam · tier · Equals · free · → best_effort` |
| `ApiFamily` / `RequestType` | (blank; value is the family/type) | `ApiFamily · · Equals · Ollama · → standard` |
| `Tenant` / `Vmr` / `ClientIp` | (blank; value is the id/address) | `ClientIp · · Equals · 10.0.0.5 · → internal` |

Operators: `Equals`, `Contains`, `Regex`, `Exists`, `GreaterThan`, `LessThan` (the numeric two for body attributes such as `max_tokens`). This is a first-class, database-stored, UI-editable list — a custom header and a specific credential are just two of the sources, exactly as requested.

### Standard traffic classes (seeded per tenant)

Each tenant is seeded — on tenant creation and at initial server startup — with a catalog of standard traffic classes in `qos_traffic_classes`. Classification rules resolve a request to one of these names; a profile's topology schedules them. The set is opinionated but editable, and the `Standard Workloads` profile wires them into a working LLQ scheme out of the box.

| Class | `tier` | What it is | Default scheduling intent |
| --- | --- | --- | --- |
| `realtime` | `Realtime` | Live/streaming — voice, token streaming, anything perceived continuously | Strict-priority, rate-limited; sits above human so a stream never stutters |
| `human-interactive` | `Interactive` | A person actively waiting on a response (chat/UI) | Strict-priority, rate-limited |
| `agent-interactive` | `AgentInteractive` | An autonomous agent in a live loop (tool calls, multi-step); latency-sensitive but burstier and higher-volume than a human | Top weighted-fair tier |
| `batch-time-bound` | `BatchTimebound` | Bulk work with a soft deadline (a report due by a time); must progress steadily and not starve | Mid weighted-fair, with aging to meet the deadline |
| `batch-background` | `BatchBackground` | Best-effort bulk (embeddings backfill, evals); yields to everything | Lowest weighted-fair; drop-oldest under pressure |
| `default` | `Default` | Fallback for anything unclassified — required as every classifier's default | Modest weighted-fair |

`realtime` and `default` were added to the requested set: streaming genuinely wants to sit above ordinary human-interactive, and classification must always resolve to something. A `control-plane`/`system-maintenance` class was considered and left out because health/warmup/model-load traffic does not traverse the proxy admission gate; it can be reserved later if wanted.

---

## User-facing changes, stated explicitly

- **A new resource: QoS Profiles.** A new left-nav page where a tenant admin creates, edits, clones, and deletes profiles — classification rules, queue disciplines, hierarchy (with an interactive diagram), and limits.
- **A default FIFO profile exists automatically.** Seeded per tenant at startup and on tenant creation, named "Default (FIFO)", and non-deletable. Existing VMRs are backfilled to it on upgrade.
- **Standard traffic classes and a ready-made profile are seeded per tenant.** Every tenant gets the standard class catalog — `realtime`, `human-interactive`, `agent-interactive`, `batch-time-bound`, `batch-background`, `default` — plus a "Standard Workloads" profile that classifies (by the `X-Conductor-Class` header) and schedules them, ready to link or clone. Classes are editable in a Traffic Classes management surface.
- **Linking a QoS profile is now required to create a VMR.** The VMR form has a required **QoS Profile** dropdown (pre-selecting the tenant default). Creating a VMR without one is rejected.
- **Operator-chosen classification.** Rules can key on a custom header, a specific credential, the model, a body attribute, tenant/user/client-IP, or API family — the operator specifies what drives the mapping.
- **Behavior changes only under saturation.** For any VMR whose endpoints are all busy, requests now wait in the profile's queues and admit in scheduled order rather than bouncing immediately; a genuinely full queue or an expired wait returns `429 Too Many Requests` with `Retry-After`.
- **New REST and MCP endpoints** under `/v1.0/qosprofiles` (plus `validate` and `classifier-catalog`), documented in `REST_API.md`, Postman, and `MCP_API.md`.
- **New observability.** A "Conductor — QoS & Queueing" Grafana dashboard, a queue-wait trace segment (`inference.qos.admit`), and per-class QoS metrics — all Prometheus-scrapable through the existing collector.
- **Deleting a tenant now cleans up its QoS configuration** (and its load-balancing and model-access policies), alongside the entities the cascade already removed.
- **A new "Nuke Tenant" capability, system-admin only.** A global admin can fully purge a tenant. From the dashboard it requires typing the tenant's exact ID to confirm and shows a live progress window listing every category deleted and its count. Tenant admins cannot see or invoke it; the reserved `"default"` tenant is protected.
- **No new config-file settings.** All QoS configuration lives in the database.

---

## Gaps, risks, and things to watch

- **QoS must not double-count in-flight.** The scheduler *reads* free capacity from `HealthCheckService` and reserves against it; it must never itself increment either in-flight counter (the existing `TryIncrementInFlight` still does that post-admission). Getting this wrong deadlocks or over-admits — it gets dedicated tests.
- **Stale endpoint selection across a long wait.** Routing picks an endpoint before the QoS wait; re-validate after release and fall through to the existing fallback on a stale pick. Verify against `RoutingDecisionService`.
- **Streaming holds a slot for its whole duration.** In-flight accounting already spans the full request, so long streams naturally keep their slot and the queue schedules around them — no special handling, stated so it isn't "discovered" later.
- **Client-disconnect tombstones cost depth until they drain.** A cancelled waiter is skipped at the head, not plucked from the middle; `maxtotaldepth` bounds the effect. A future QoSKit item-removal API would improve it.
- **Dynamic-flow WFQ cardinality.** A WFQ node keyed per-tenant/user mints a series per flow; `enableperclassmetrics=false` (surfaced with a UI warning) keeps the scrape endpoint safe.
- **Cross-VMR fairness is out of scope.** Profiles govern one VMR's own capacity; a noisy VMR starving a *different* VMR that shares upstream endpoints would need a shared-capacity abstraction above the VMR.
- **SQLite `ON DELETE CASCADE` depends on a PRAGMA that may be off.** The plan deletes child rows in application code rather than relying on SQL cascade, consistent with the existing tenant-delete cascade.
- **Hot-reload drain policy.** Editing a linked profile swaps the runtime and drains the old pipeline; whether parked requests migrate or cancel at a deadline is a small choice to settle in Step 5.

---

## Definition of done

- [ ] From a clean checkout on `feature/qos`: create a QoS profile in the dashboard and see its hierarchy drawn and editable.
- [ ] Create a VMR — the form requires a QoS profile and defaults to the tenant's FIFO profile; creating without one is rejected.
- [ ] Drive a profiled VMR to saturation and watch requests queue and release in the profile's order rather than bounce; a full queue returns `429 + Retry-After`.
- [ ] Every tenant has a non-deletable default FIFO profile; existing VMRs were backfilled to it on upgrade.
- [ ] Every tenant is seeded with the standard traffic classes and the "Standard Workloads" profile, on upgrade and on new-tenant creation, without resurrecting deletions.
- [ ] The bundled Grafana natively shows all QoSKit metrics and traces in an organized "Conductor — QoS & Queueing" folder.
- [ ] Documentation is fully revised — `QOS_OVERVIEW.md` walks configuration and monitoring end to end, README carries the detail with examples, CHANGELOG summarizes, and every affected doc matches the shipped surface.
- [ ] Tests cover all new QoS capabilities with positive and negative variants and move coverage toward 100%.
- [ ] Open the "Conductor — QoS & Queueing" Grafana dashboard from the home page and read per-class wait and drops; open a slow request in Tempo and see the `inference.qos.admit` segment; confirm `qoskit_*`/`conductor_qos_*` series are in Prometheus.
- [ ] Deleting a tenant removes all its QoS config; the system-admin-only nuke API (`POST /v1.0/tenants/{id}/purge`) purges a tenant end to end, rejects tenant-admin callers with `403`, requires the typed `confirmTenantId`, and the dashboard shows the typed-ID confirm and the per-category progress window.
- [ ] All QoS configuration is in the database; nothing QoS-related is in `conductor.json`; all schema changes applied as startup migrations.
- [ ] Build warning-free, all three runners green, REST/MCP/Postman/TELEMETRY docs in sync, version reads `0.5.0` everywhere.
