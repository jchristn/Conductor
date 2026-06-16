# Virtual Model Runner Reservations Product Plan

This plan turns the reservation design in `RESERVATIONS.md` into an executable whole-product backlog. It is written as a tracker: keep the checkboxes in source control, add initials and dates to the `Progress` lines, and link PRs, test runs, screenshots, and docs reviews as work completes.

## Executive Summary

Virtual Model Runner reservations add a time-bound admission gate in front of VMR use. Outside a reservation window, VMRs continue to behave as they do today: any authenticated caller who satisfies tenant, credential, and model access rules can use the VMR. During an active reservation window, only the users or credentials listed on the reservation may use that VMR, and even those participants must still satisfy the normal ACL and model access policy checks.

The feature is a product surface, not just a backend switch. It needs durable data models, provider-neutral database methods, request routing enforcement, request history and analytics dimensions, administrator dashboard workflows, SDK coverage, Postman examples, documentation, and a broad positive and negative test matrix.

The core product invariant is:

> A VMR with an active reservation is not usable by any incoming identity that is not explicitly included in the active reservation, and every denial caused by that reservation is visible in logs, request history, analytics, and operational metrics without leaking secrets.

## How To Use This Plan

- `[ ]` means not started.
- `[~]` means in progress. Add initials and date on the `Progress` line.
- `[x]` means complete. Link the PR, commit, test run, or document update on the `Progress` line.
- Do not delete negative test cases after implementation. They are regression requirements.
- Any scope reduction must update this plan and explain the tradeoff in the relevant `Progress` line.

## Current Implementation Status

2026-06-15 Codex implementation on branch `feature/reservations` delivered the first functional product slice: reservation domain models, provider-neutral persistence, provider schema bootstrapping, validation/evaluation service, management routes, routing admission gate, VMR model-load admission gate, first-class request-history and analytics reservation dimensions, dashboard reservation workspace, VMR badges, request-history and analytics dashboard filters/cards, backup/restore handling, SDK helpers/docs, Postman examples, REST/README/TESTING/changelog docs, ADR, management guide, and shared tests.

Verification completed on 2026-06-15:

- `dotnet build src\Conductor.sln` passed.
- `dotnet test src\Test.Xunit\Test.Xunit.csproj --no-build` passed: 1049 tests.
- `dotnet test sdk\csharp\Conductor.Sdk.Tests\Conductor.Sdk.Tests.csproj` passed: 6 tests.
- `npm.cmd test --prefix sdk\javascript` passed: 13 tests.
- `$env:PYTHONPATH='src'; python -m pytest tests` from `sdk\python` passed: 13 tests.
- `npm.cmd run build --prefix dashboard` passed with the existing Vite large-chunk warning.
- `Conductor.postman_collection.json` parses as valid JSON.

Known remaining gaps from the whole-product plan:

- Request history and request analytics now expose first-class reservation dimensions, backend filters, dashboard filters, detail fields, and analytics reservation-denial cards/counts.
- VMR list/detail badges and VMR-scoped reservation panels are implemented.
- Backup/restore support for reservation tables is implemented and tested.
- Provider matrix tests beyond SQLite-backed shared tests are not yet run.
- Operational alert dashboards, live Postman smoke, provider matrix runtime, automated dashboard E2E/a11y/i18n screenshots, and release/security/compliance reviews remain open.
- Dashboard reservation workflows build successfully, and native hover tooltip coverage now exists for the VMR Reservations page plus dashboard-wide control fallback. Automated component/E2E, accessibility, responsive screenshot, and i18n checks remain open.

## Alignment Sources

This plan is aligned to the requirements in `C:\Code\Agents`:

- `requirements/AUTHENTICATION.md`
- `requirements/BACKEND_ARCHITECTURE.md`
- `requirements/BACKEND_TEST_ARCHITECTURE.md`
- `requirements/CODE_STYLE.md`
- `requirements/FRONTEND_ARCHITECTURE.md`
- `requirements/I18N.md`
- `requirements/REPOSITORY_REQUIREMENTS.md`
- `requirements/WRITING_DOCUMENTS.md`
- `requirements/EXAMPLE_APPLICATIONS.md`
- `personas/README.md` and the persona files listed there

Current Conductor implementation targets:

- Backend: `src/Conductor.Core`, `src/Conductor.Server`
- Route modules: `src/Conductor.Server/Routing`
- Controllers: `src/Conductor.Server/Controllers`
- Services: `src/Conductor.Server/Services`
- Database providers: `src/Conductor.Core/Database`
- Tests: `src/Test.Shared`, `src/Test.Automated`, `src/Test.Xunit`, `src/Test.Nunit`
- Dashboard: `dashboard/src`
- SDKs: `sdk/csharp`, `sdk/javascript`, `sdk/python`
- Postman: `Conductor.postman_collection.json`
- Public docs: `README.md`, `REST_API.md`, `TESTING.md`, `CHANGELOG.md`, `docs/adr`

## Product Definition

### Goals

- [x] Add tenant-scoped reservations for VMRs with UTC start and end times.
  - Owner: Product Manager
  - Progress: 2026-06-15 Codex added tenant-scoped reservation models, API, storage, dashboard form, SDK helpers, and docs.
- [x] Allow admins or authorized operators to select the exact users and credentials permitted during a reservation.
  - Owner: Product Manager, Security Engineer
  - Progress: 2026-06-15 Codex added user and credential subjects, participant matching, validation, duplicate prevention, and dashboard participant selection.
- [x] Enforce reservations for every VMR use path, including proxy inference, embeddings, list/show model operations where VMR access is exercised, model load and verification operations, and routing simulation where the result claims whether a request would be admitted.
  - Owner: Principal Architect
  - Progress: 2026-06-15 Codex added routing/proxy gate before provider calls, explanation timeline, management-plane model-load enforcement, and explicit routing tests for embeddings, list-model, and show-model reservation denials before endpoint inventory.
- [x] Preserve current on-demand access outside reservation windows.
  - Owner: Software Engineer
  - Progress: 2026-06-15 Codex reservation evaluator returns no-reservation allow and routing continues through existing policy checks.
- [x] Keep model access policy and ACL enforcement after a reservation allow decision.
  - Owner: Security Engineer
  - Progress: 2026-06-15 Codex runs reservation evaluation before model access and keeps existing model access evaluation after participant allow.
- [~] Emit request history, analytics, logs, and metrics for reservation denials.
  - Owner: SRE, Data Engineer
  - Progress: 2026-06-15 Codex added request-history reservation fields/filters, analytics reservation dimensions/counts, and runtime denial logs. Dedicated metric counters/alerts and log capture tests remain open.
- [~] Provide dashboard workflows that make reservations easy to create, inspect, edit, cancel, audit, and troubleshoot.
  - Owner: UX Designer
  - Progress: 2026-06-15 Codex added top-level Reservations workspace with create/edit/deactivate/list/validate/effective-access flows, VMR row action/deep-link preselection, VMR badges, VMR detail reservation context, request-history reservation filters/detail fields, analytics reservation-denial cards/counts, explicit VMR Reservations hover tooltips, and dashboard-wide control tooltip fallback. Automated E2E, i18n, accessibility, and responsive screenshot reviews remain open.
- [x] Update SDKs, Postman, and docs so the feature is usable without reading server code.
  - Owner: Documentation Engineer, Developer Relations
  - Progress: 2026-06-15 Codex added SDK helpers/tests, SDK README examples, Postman positive and negative reservation examples, REST API docs, README overview, `TESTING.md` gate, changelog notes, ADR 0003, and `MANAGING_RESERVATIONS.md`.

### Non-Goals For Initial Release

- [x] Do not add recurring reservations in the first release.
  - Rationale: Single windows are enough to validate data model, enforcement, and UX. Recurrence adds exception handling, calendar semantics, and complex conflict resolution.
  - Progress: 2026-06-15 Codex implemented single-window start/end reservations only.
- [x] Do not let system admins bypass active reservations unless they are explicitly listed as reservation subjects.
  - Rationale: `AUTHENTICATION.md` requires explicit bypass behavior to be documented and audited. The safer first release is no bypass.
  - Progress: 2026-06-15 Codex has no admin bypass in reservation evaluation.
- [x] Do not cancel in-flight streams admitted before a reservation starts in the first release.
  - Rationale: Use an admission drain lead window first. Forced cancellation can follow after request lifecycle hooks are proven.
  - Progress: 2026-06-15 Codex implemented admission-time drain denial and did not add stream cancellation.
- [x] Do not make reservations a replacement for ACL or model access policies.
  - Rationale: Reservations are a temporal admission gate, not an authorization policy system.
  - Progress: 2026-06-15 Codex keeps reservation allow as a precondition before existing ACL/model access policy checks.

### Definitions

- Reservation: A tenant-scoped VMR block from `StartUtc` inclusive to `EndUtc` exclusive.
- Participant: A user or credential explicitly listed on the reservation.
- Active reservation: A reservation where `Active = true` and `StartUtc <= nowUtc < EndUtc`.
- Drain window: An optional pre-start interval where nonparticipants are denied admission so long-running requests do not run into the reserved period.
- Reservation denial: A request that would otherwise continue routing, but is stopped because the active reservation does not include the incoming identity.

## Assumptions And Open Questions

### Assumptions

- [x] Reservation times are stored and evaluated in UTC only.
  - Progress: 2026-06-15 Codex stores and compares `StartUtc`/`EndUtc`; dashboard converts local inputs to UTC on submit.
- [~] UI may display local time, but must always expose UTC in review/audit contexts.
  - Progress: 2026-06-15 Codex labels list windows as UTC and shows UTC conversions in forms. Full audit-detail review remains open.
- [x] The incoming request identity can resolve to a tenant and, when applicable, a user, credential, or credential owner user.
  - Progress: 2026-06-15 Codex populates request context from auth result, bearer/Gemini/header credentials, and credential owner lookup.
- [x] A credential subject admits only that credential. A user subject admits the user and credentials owned by that user when the credential owner is known and active.
  - Progress: 2026-06-15 Codex implemented direct user, direct credential, and credential-owner user matching with tests.
- [x] Overlapping active reservations on the same tenant and VMR are invalid.
  - Progress: 2026-06-15 Codex validates overlap on create/update/validate and added tests.
- [x] Multiple active reservations discovered at runtime are treated as a fail-closed configuration error.
  - Progress: 2026-06-15 Codex returns `ReservationConflict` and denies when more than one active reservation applies.
- [x] A reservation allow decision never bypasses ACL, tenant isolation, request type authorization, or model access policy.
  - Progress: 2026-06-15 Codex keeps reservation gate before existing ACL/model access policy checks.
- [x] The existing request history and analytics model can be extended with nullable reservation fields without breaking old records.
  - Progress: 2026-06-15 Codex added nullable history/analytics fields, provider migrations, and schema tests.

### Open Questions

- [ ] Who can create and manage reservations: system admin, tenant admin, users with a new permission such as `vmr.reservations.manage`, or a combination?
  - Owner: Product Manager, Security Engineer
  - Decision: Current implementation grants management to tenant admins through `AuthorizationConfig`; product/security sign-off on finer-grained permissions remains open.
  - Progress: 2026-06-15 Codex implemented tenant-admin management mapping.
- [ ] Should read-only reservation visibility be available to all users who can read a VMR, or only to admins and analytics readers?
  - Owner: Product Manager
  - Decision: Current implementation exposes read/effective-access routes to authenticated users through the configured request types; final product decision remains open.
  - Progress: 2026-06-15 Codex added read/effective-access authorization mapping.
- [ ] What default `AdmissionDrainLeadMs` should apply when a reservation starts soon? Candidate: configured VMR request timeout.
  - Owner: Principal Architect, SRE
  - Decision: Current implementation requires per-reservation value with validation and max cap; configurable default remains open.
  - Progress: 2026-06-15 Codex added per-reservation `AdmissionDrainLeadMs`.
- [ ] Should cancelled reservations be hard deleted or soft deactivated with audit retention?
  - Owner: Compliance Officer, Database Engineer / DBA
  - Decision: Current implementation uses soft deactivate.
  - Progress: 2026-06-15 Codex implemented deactivate behavior for `DELETE`.
- [ ] Should active reservations be editable, or should edits create a new version and deactivate the old one?
  - Owner: Product Manager, Legal Counsel, Compliance Officer
  - Decision: Current implementation edits active reservations in place; versioning decision remains open.
  - Progress: 2026-06-15 Codex allows update of active reservations through API and dashboard.
- [ ] Should reservation denials appear in billing or cost reports as zero-cost denied requests?
  - Owner: CFO, Data Analyst
  - Decision:
  - Progress: 2026-06-15 Codex records request history and analytics denial dimensions, but no billing/cost-report behavior was added.
- [ ] Should participant lists allow groups later, or only users and credentials for the initial release?
  - Owner: Chief Product Officer
  - Decision:
  - Progress:

## Requirements Matrix

| Source | Requirement | Plan Commitment | Verification |
| --- | --- | --- | --- |
| `AUTHENTICATION.md` | Every request resolves to exactly one tenant. | Reservation CRUD, evaluation, history, analytics, and provider queries must include tenant id. | Tenant isolation tests across all DB providers and proxy integration tests. |
| `AUTHENTICATION.md` | Credentials and users are separate but related identities. | Reservation subjects support both `User` and `Credential`; credential owner can satisfy a user subject. | Positive and negative identity resolution tests. |
| `AUTHENTICATION.md` | Explicit deny beats allow. | Active reservation denial stops routing before provider calls and before model access allow can override it. | Routing tests proving model access allow does not bypass reservation denial. |
| `AUTHENTICATION.md` | No implicit admin bypass unless documented and audited. | Initial release has no admin bypass. Admins must be listed as participants. | Admin outsider denial test and docs. |
| `AUTHENTICATION.md` | Audit denied auth and authz decisions. | Denials write structured logs, request history, analytics events, and metrics with `ReservationDenied` or `ReservationDrainDenied`. | Log capture tests, history tests, analytics tests, metric tests. |
| `BACKEND_ARCHITECTURE.md` | Watson 7 only, route registrar pattern. | Add reservation routes through a route module and existing route registry conventions. | Build and route registration tests. |
| `BACKEND_ARCHITECTURE.md` | Provider-neutral DB abstractions and provider implementations. | Add `IVirtualModelRunnerReservationMethods` and implementations for SQLite, MySQL, PostgreSQL, SQL Server. | Shared provider matrix tests. |
| `BACKEND_ARCHITECTURE.md` | Domain-specific methods, not generic CRUD. | Reservation DB interface names methods around validation, active lookup, overlap detection, subject management, and search. | Code review and Test.Shared contract tests. |
| `BACKEND_ARCHITECTURE.md` | Typed request/response models. | Add concrete create, update, validate, search, effective access, and evaluation DTOs. | OpenAPI schema and serialization tests. |
| `BACKEND_ARCHITECTURE.md` | PrettyId string prefixes. | Use a new stable reservation prefix such as `vmrr_` and subject prefix such as `vmrrs_`. | Unit tests for id creation and parsing. |
| `BACKEND_ARCHITECTURE.md` | Request context auth/authorization. | Reservation evaluation consumes resolved tenant, user, credential, and credential owner from request context. | Proxy and controller auth tests. |
| `BACKEND_ARCHITECTURE.md` | All async accepts `CancellationToken` and uses `ConfigureAwait(false)`. | Every new service, DB, SDK, and controller async flow follows this style. | Code review and build warnings where available. |
| `CODE_STYLE.md` | No `var`, no tuples, XML docs for public members. | New C# code follows the style guide exactly. | Code review checklist. |
| `BACKEND_TEST_ARCHITECTURE.md` | Touchstone shared-suite pattern. | Put reusable reservation test suites in `src/Test.Shared` and run through automated, xUnit, and NUnit projects. | CI test output from all supported runners. |
| `FRONTEND_ARCHITECTURE.md` | React/Vite dashboard, fetch client, accessible responsive screens. | Add reservation API client, views, components, keyboard support, responsive checks, and route integration. | Dashboard unit/component/E2E tests and screenshots. |
| `FRONTEND_ARCHITECTURE.md` | API Explorer should reflect API capability. | Add reservation endpoints and effective access examples to API Explorer/OpenAPI-driven workflows. | API Explorer manual and automated checks. |
| `I18N.md` | Every visible and accessibility string is localizable. | Reservation dashboard strings use i18n resources, including status labels, validation messages, tooltips, and aria text. | Missing-key and hard-coded string checks; pseudo-locale UI review. |
| `REPOSITORY_REQUIREMENTS.md` | SDK directories and docs remain first-class. | Update C#, JavaScript, and Python SDKs plus README/tests; update Postman and docs. | SDK test runs, Postman smoke run, docs review. |
| `WRITING_DOCUMENTS.md` | Docs are specific and human-readable. | Reservation docs include concrete examples, denial semantics, troubleshooting, and UI screenshots where appropriate. | Docs review by Support, DevRel, and Product. |
| `EXAMPLE_APPLICATIONS.md` | Use Conductor itself as the reference for backend and dashboard patterns. | Follow existing controller, route, service, DB, dashboard view, and SDK conventions. | Code review against local patterns. |

## Persona Lens Review

| Persona | Primary Concern | Reservation Plan Implication | Required Artifact |
| --- | --- | --- | --- |
| CEO | Growth and durable value. | Position reservations as enterprise-grade capacity control without reducing on-demand use. | Launch summary and business value note. |
| CTO | Durable technical direction. | Implement as a first-class temporal authorization gate, not scattered checks. | ADR and architecture review. |
| CFO | Sustainable operating choices. | Expose denied usage and reserved windows for cost and utilization analysis. | Analytics report fields and pricing/billing decision note. |
| Chief Product Officer | Portfolio coherence. | Keep scope focused on single-window reservations first, with recurrence deferred. | Product definition and roadmap entry. |
| VP Engineering | Predictable execution. | Split work into backend, dashboard, SDK, docs, tests, and launch gates. | Delivery backlog and release criteria. |
| Product Manager | Buildable product decisions. | Define exact semantics for active windows, identity matching, conflict prevention, and management permissions. | PRD and acceptance criteria. |
| Product Marketing Manager | Market understanding. | Explain use cases: demos, evaluations, priority customer windows, incident isolation, scheduled tests. | Go-to-market brief. |
| Technical Program Manager | Cross-functional execution. | Track dependencies between schema, routing, dashboard, SDKs, docs, and testing. | Milestone plan and risk register. |
| UX Designer | Understandable and accessible UX. | Add calendar/list workflows, clear conflict handling, timezone clarity, and visible reservation status. | UX spec and prototype review. |
| UX Researcher | Credible user truth. | Validate whether operators understand who is allowed, when, and why denials happen. | Usability test script and findings. |
| Principal Architect | Coherent system decisions. | Centralize reservation evaluation in routing and service layers with provider-neutral persistence. | ADR and technical design. |
| Engineering Manager | Reliable team execution. | Assign focused owners and code review boundaries by subsystem. | Engineering plan. |
| Software Engineer | Maintainable implementation. | Add typed models, service interfaces, route modules, and tests following Conductor style. | PRs and test evidence. |
| DevOps Engineer | Repeatable delivery. | Add migrations/schema bootstrap, environment settings, CI checks, Postman run, and deployment notes. | CI/CD update and rollout plan. |
| Site Reliability Engineer | SLOs and observability. | Add logs, metrics, dashboards, alerts, and operational runbook entries for reservation denials and conflicts. | SLO/observability update. |
| Security Engineer | Risk reduction. | Prevent bypass, enforce tenant isolation, avoid secret logging, and test adversarial identity cases. | Security review. |
| Data Engineer | Trustworthy data. | Extend history and analytics schemas with reservation fields and dimensions. | Data model spec. |
| Database Engineer / DBA | Correct, performant, recoverable DB. | Add indexes, provider-specific DDL, migration/backfill, backup/export/import, and query performance checks. | Migration and recovery plan. |
| Data Analyst | Decision-grade evidence. | Add analytics dimensions for reservation id/name/decision/reason and dashboards for denial rates. | Analytics report. |
| AI/ML Engineer | Reliable ML product value. | Ensure VMR routing to model endpoints remains stable and measurable during reservations. | Model runner validation report. |
| QA Engineer | User quality perspective. | Own positive and negative test matrix across API, proxy, dashboard, SDKs, and docs. | Test plan. |
| Automation Engineer | Scalable quality. | Add reusable shared tests and dashboard automation for reservation workflows. | Automation framework updates. |
| Documentation Engineer | Accurate product knowledge. | Update REST API, admin guide, troubleshooting, SDK docs, and changelog. | User/API documentation. |
| Customer Success Manager | Adoption and renewal. | Provide customer-facing explanation of scheduled reserved access and denial troubleshooting. | Customer enablement note. |
| Technical Support Engineer | Fast diagnosis. | Ensure request history shows "denied by active reservation" with reservation id and window. | Support runbook. |
| Growth Marketing Manager | Demand efficiency. | Capture enterprise control story and demos once feature is stable. | Campaign note, optional. |
| Account Executive | Revenue conversion. | Provide sales-safe language for reserved demo capacity and premium controls. | Sales enablement note. |
| Sales Engineer | Technical fit proof. | Provide Postman collection and demo scenario for reserved VMR windows. | Demo script. |
| Developer Relations | Developer trust. | SDK examples and docs must make reservation APIs easy to use. | Developer guide. |
| Legal Counsel | Legal risk. | Retain audit trail for denials and management actions; document data retention. | Legal review note. |
| Compliance Officer | Repeatable controls. | Provide evidence that reservation access is enforced, logged, and reviewable. | Compliance audit package. |

## Cross-Persona Conflict Analysis

- [x] Product simplicity vs. enterprise scheduling depth.
  - Conflict: Product and UX may want recurrence, groups, and calendar import immediately; Engineering and QA need a contained first release.
  - Resolution: Ship single-window reservations first with explicit extension points for recurrence.
  - Progress: 2026-06-15 Codex implemented single-window reservations only.
- [x] Admin convenience vs. security auditability.
  - Conflict: Admins may expect emergency bypass; Security and Compliance require explicit, auditable behavior.
  - Resolution: No implicit bypass in first release. A later break-glass mode must be a separate audited feature.
  - Progress: 2026-06-15 Codex implemented no implicit admin bypass.
- [~] Low-latency routing vs. strongly current scheduling.
  - Conflict: SRE wants minimal per-request overhead; Product requires reservations to activate exactly on time.
  - Resolution: Start with direct DB lookup or a bounded cache that never crosses reservation transitions and invalidates on CRUD.
  - Progress: 2026-06-15 Codex implemented direct DB lookup in the reservation evaluator. Performance benchmarks and optional bounded cache design remain open.
- [ ] Dashboard speed vs. i18n/accessibility completeness.
  - Conflict: Fast UI delivery often leaves strings and keyboard paths behind.
  - Resolution: Treat i18n, keyboard, screen reader labels, and responsive checks as acceptance criteria, not polish.
  - Progress:
- [~] Analytics detail vs. privacy and secret safety.
  - Conflict: Support wants enough data to diagnose denials; Security requires no credential secrets or bearer tokens in logs.
  - Resolution: Store ids, names, reason codes, tenant, VMR, and request ids only. Never store tokens or raw secrets.
  - Progress: 2026-06-15 Codex added ids/names/reason/window fields to history and analytics without storing tokens or secrets. Explicit redaction tests remain open.

## Architecture And Technical Strategy

### Enforcement Order

The routing decision order should be:

1. Resolve tenant and VMR.
2. Resolve request identity: user, credential, credential owner user when applicable.
3. Evaluate reservation gate for active and drain windows.
4. Deny immediately if the reservation gate fails.
5. Continue existing request type, ACL, model resolution, model access, endpoint selection, and provider call behavior.
6. Capture routing timeline, request history, analytics, and metrics for both allowed and denied decisions.

Acceptance:

- [x] Reservation denial happens before any provider-facing network call.
  - Progress: 2026-06-15 Codex evaluates reservation gate before endpoint inventory/provider selection in `RoutingDecisionService`.
- [x] Reservation allow does not skip model access policy evaluation.
  - Progress: 2026-06-15 Codex continues through existing request type, model access, endpoint inventory, and provider selection after reservation allow.
- [x] Routing simulation and explain endpoints include reservation gate results.
  - Progress: 2026-06-15 Codex added reservation fields and a `ReservationGate` timeline stage.

### Data Model

Create two first-class tenant-scoped records.

`VirtualModelRunnerReservation`

- `GUID` or `Id` using prefix such as `vmrr_`
- `TenantGUID`
- `VirtualModelRunnerGUID`
- `Name`
- `Description`
- `StartUtc`
- `EndUtc`
- `AdmissionDrainLeadMs`
- `Active`
- `CreatedByUserGUID`
- `CreatedByCredentialGUID`
- `Labels`
- `Tags`
- `Metadata`
- `CreatedUtc`
- `LastUpdateUtc`

`VirtualModelRunnerReservationSubject`

- `GUID` or `Id` using prefix such as `vmrrs_`
- `TenantGUID`
- `ReservationGUID`
- `SubjectType`
- `SubjectGUID`
- `CreatedUtc`

Indexes:

- [x] `(tenantid, vmrid, active, startutc, endutc)` for active lookup.
  - Progress: 2026-06-15 Codex added provider schema indexes for active reservation lookup.
- [x] `(tenantid, reservationid)` for subject enumeration.
  - Progress: 2026-06-15 Codex added subject lookup indexes.
- [x] `(tenantid, subjecttype, subjectid)` for identity lookup and support queries.
  - Progress: 2026-06-15 Codex added subject lookup indexes.
- [x] Unique `(reservationid, subjecttype, subjectid)` to prevent duplicate participants.
  - Progress: 2026-06-15 Codex added uniqueness constraints/indexes and service validation.
- [x] Provider-compatible overlap query for active reservations on the same tenant and VMR.
  - Progress: 2026-06-15 Codex added provider-neutral overlap query implementation and tests.

Validation rules:

- [x] Tenant id is required and must match VMR, subjects, user, and credential tenants.
  - Progress: 2026-06-15 Codex validates tenant/VMR/subject consistency.
- [x] VMR must exist and be active unless product decides inactive VMR scheduling is allowed.
  - Progress: 2026-06-15 Codex validates active VMR existence.
- [x] `StartUtc < EndUtc`.
  - Progress: 2026-06-15 Codex validates reservation window ordering.
- [x] At least one subject is required.
  - Progress: 2026-06-15 Codex validates non-empty subjects.
- [x] Subject users and credentials must exist, be active, and belong to the same tenant.
  - Progress: 2026-06-15 Codex validates active tenant-local users and credentials.
- [x] Active reservations must not overlap the same tenant and VMR.
  - Progress: 2026-06-15 Codex validates active overlaps in create/update/validate.
- [x] `AdmissionDrainLeadMs` must be nonnegative and clamped to a configured maximum.
  - Progress: 2026-06-15 Codex validates nonnegative lead time and maximum cap.
- [~] Metadata, labels, names, and descriptions must honor existing length and serialization conventions.
  - Progress: 2026-06-15 Codex implemented name/description fields and JSON serialization conventions. Labels/tags/metadata were deferred.

### Core Models And Enums

- [x] Add `VirtualModelRunnerReservation` model in `src/Conductor.Core/Models`.
  - Owner: Software Engineer
  - Requirements: typed model, XML docs, one class per file, no tuples, no `var`.
  - Progress: 2026-06-15 Codex added model.
- [x] Add `VirtualModelRunnerReservationSubject` model in `src/Conductor.Core/Models`.
  - Owner: Software Engineer
  - Requirements: typed model, XML docs, tenant-safe parsing.
  - Progress: 2026-06-15 Codex added subject model.
- [~] Add reservation request and response DTOs under `src/Conductor.Core/Requests` and `src/Conductor.Core/Responses`.
  - Owner: Software Engineer
  - Requirements: create, update, validate, enumerate, effective access, evaluation result.
  - Progress: 2026-06-15 Codex reused core reservation/evaluation models directly in API payloads instead of adding separate DTO classes.
- [~] Add enums under `src/Conductor.Core/Enums`.
  - Owner: Principal Architect
  - Suggested enums: `ReservationSubjectTypeEnum`, `ReservationDecisionEnum`, `ReservationDenialReasonEnum`, `ReservationLifecycleStateEnum`.
  - Progress: 2026-06-15 Codex added `ReservationSubjectTypeEnum` and `ReservationDecisionEnum`. Separate denial-reason and lifecycle enums remain open.
- [x] Extend `RoutingDecision` with nullable reservation fields and a reservation timeline stage.
  - Owner: Principal Architect
  - Progress: 2026-06-15 Codex added reservation id/name/decision/reason/window fields and timeline attributes.
- [x] Extend request history and analytics models with nullable reservation fields.
  - Owner: Data Engineer
  - Suggested fields: `ReservationGUID`, `ReservationName`, `ReservationDecision`, `ReservationReasonCode`, `ReservationWindowStartUtc`, `ReservationWindowEndUtc`.
  - Progress: 2026-06-15 Codex added nullable reservation dimensions to history/detail/search/summary and analytics event/filter/overview models.

### Database And Provider Strategy

- [x] Add `IVirtualModelRunnerReservationMethods` in `src/Conductor.Core/Database/Interfaces`.
  - Owner: Database Engineer / DBA
  - Methods should be domain-specific: create, read, update, deactivate, enumerate, validate overlap, read active for VMR, read subjects, replace subjects, search by subject.
  - Progress: 2026-06-15 Codex added provider-neutral interface.
- [x] Implement SQLite methods.
  - Owner: Database Engineer / DBA
  - Progress: 2026-06-15 Codex wired provider-neutral reservation methods into SQLite driver and schema.
- [~] Implement MySQL methods.
  - Owner: Database Engineer / DBA
  - Progress: 2026-06-15 Codex wired provider-neutral reservation methods into MySQL driver and schema; provider-specific runtime test matrix remains open.
- [~] Implement PostgreSQL methods.
  - Owner: Database Engineer / DBA
  - Progress: 2026-06-15 Codex wired provider-neutral reservation methods into PostgreSQL driver and schema; provider-specific runtime test matrix remains open.
- [~] Implement SQL Server methods.
  - Owner: Database Engineer / DBA
  - Progress: 2026-06-15 Codex wired provider-neutral reservation methods into SQL Server driver and schema; provider-specific runtime test matrix remains open.
- [x] Add schema creation or migration changes following the existing Conductor database bootstrapping pattern.
  - Owner: DevOps Engineer, Database Engineer / DBA
  - Progress: 2026-06-15 Codex added provider table creation and migrations for reservation tables plus request-history/analytics reservation columns.
- [x] Add backup, restore, export, and import handling for reservation tables.
  - Owner: Database Engineer / DBA
  - Progress: 2026-06-15 Codex added backup schema version `1.3`, reservation export, validation, restore conflict handling, restore counters, dashboard backup/restore summaries, REST docs, and backup controller tests preserving reservations and subjects.
- [ ] Add query performance checks for active reservation lookup under expected tenant and VMR cardinality.
  - Owner: SRE, Database Engineer / DBA
  - Progress:

### Reservation Service

- [x] Add `VirtualModelRunnerReservationService` in `src/Conductor.Server/Services`.
  - Owner: Software Engineer
  - Responsibilities: validation, conflict checks, subject resolution, lifecycle state calculation, effective access evaluation, cache invalidation if used.
  - Progress: 2026-06-15 Codex added service covering validation, overlap checks, subject resolution, effective evaluation, lifecycle state, create/update/deactivate/list/read.
- [x] Add `ReservationEvaluationContext`.
  - Owner: Software Engineer
  - Required fields: tenant id, VMR id, now UTC, user id, credential id, credential owner user id, request type, model name when available.
  - Progress: 2026-06-15 Codex added evaluation context model.
- [x] Add `ReservationEvaluationResult`.
  - Owner: Software Engineer
  - Required fields: has reservation, in active window, in drain window, allowed, reservation id/name, reason code, reason text, start/end UTC, matched subject type/id.
  - Progress: 2026-06-15 Codex added evaluation result model.
- [~] Add a clock abstraction or use the existing time abstraction if present so boundary tests are deterministic.
  - Owner: Software Engineer
  - Progress: 2026-06-15 Codex made evaluator accept `AtUtc` from the caller, which supports deterministic boundary tests. A formal clock abstraction was not added.
- [~] Decide whether to query DB per request or add bounded cache.
  - Owner: Principal Architect, SRE
  - Acceptance: A cache must never keep a pre-reservation allow decision past `StartUtc`, `EndUtc`, or a CRUD invalidation.
  - Progress: 2026-06-15 Codex implemented direct DB lookup. Cache design/performance decision remains open.

### API Surface

Add route module and controller behavior following existing Conductor route conventions.

- [x] Add `VirtualModelRunnerReservationRouteModule`.
  - Owner: Software Engineer
  - Progress: 2026-06-15 Codex added `VirtualModelRunnerReservationRouteModule` and registered it in the route context/registry/server.
- [x] Add `VirtualModelRunnerReservationController`.
  - Owner: Software Engineer
  - Progress: 2026-06-15 Codex added `VirtualModelRunnerReservationController` for create, validate, list, get, update, deactivate, VMR-scoped list, and effective access evaluation.
- [x] Add request types to `RequestTypeEnum`.
  - Owner: Software Engineer
  - Suggested values: create, read, update, delete/deactivate, list, validate, evaluate, read effective reservation access.
  - Progress: 2026-06-15 Codex added reservation request types and resolver coverage.
- [x] Update `AuthorizationConfig` with management and read permissions.
  - Owner: Security Engineer
  - Progress: 2026-06-15 Codex mapped reservation management and read operations into the authorization configuration.

Candidate REST endpoints:

- [x] `POST /v1.0/vmrreservations`
  - Creates a reservation.
  - Progress: 2026-06-15 Codex implemented.
- [x] `POST /v1.0/vmrreservations/validate`
  - Validates a draft and returns conflicts without persisting.
  - Progress: 2026-06-15 Codex implemented.
- [x] `GET /v1.0/vmrreservations`
  - Lists reservations by tenant, VMR, state, time range, subject, and text search.
  - Progress: 2026-06-15 Codex implemented.
- [x] `GET /v1.0/vmrreservations/{id}`
  - Reads reservation details and subjects.
  - Progress: 2026-06-15 Codex implemented.
- [x] `PUT /v1.0/vmrreservations/{id}`
  - Updates editable fields and replaces subjects atomically.
  - Progress: 2026-06-15 Codex implemented.
- [x] `DELETE /v1.0/vmrreservations/{id}`
  - Deactivates or deletes according to the retention decision.
  - Progress: 2026-06-15 Codex implemented as deactivate.
- [x] `GET /v1.0/virtualmodelrunners/{id}/reservations`
  - Lists reservations scoped to a VMR.
  - Progress: 2026-06-15 Codex implemented.
- [x] `GET /v1.0/virtualmodelrunners/{id}/reservation-effective?credentialId=...&userId=...&atUtc=...`
  - Explains whether a candidate identity would be admitted at a time.
  - Progress: 2026-06-15 Codex implemented.

API response requirements:

- [x] Use `401` when an active reservation requires identity but the request has no usable identity.
  - Progress: 2026-06-15 Codex implemented in routing/model-load reservation gates.
- [x] Use `403` when identity is present but not listed on the active reservation.
  - Progress: 2026-06-15 Codex implemented in routing/model-load reservation gates.
- [x] Use `409` for overlap conflicts on create/update.
  - Progress: 2026-06-15 Codex implemented in reservation validation/create/update.
- [x] Use stable machine-readable reason codes such as `ReservationDenied`, `ReservationDrainDenied`, `ReservationAuthenticationRequired`, `ReservationConflict`.
  - Progress: 2026-06-15 Codex implemented across API responses, routing decisions, request history, and analytics events.
- [x] Never return credential secrets or bearer tokens in reservation responses.
  - Progress: 2026-06-15 Codex reservation subjects store ids only; responses do not include secret fields.

### Routing Integration

- [x] Integrate reservation evaluation into `RoutingDecisionService`.
  - Owner: Principal Architect, Software Engineer
  - Acceptance: decision timeline includes `ReservationGate` before model access and endpoint selection.
  - Progress: 2026-06-15 Codex evaluates reservations before request type, model access, and endpoint inventory, and records `ReservationGate` timeline attributes.
- [x] Integrate with proxy paths in the server so provider calls are never made for denied outsiders.
  - Owner: Software Engineer
  - Progress: 2026-06-15 Codex wired proxy identity extraction and routing denial before provider calls.
- [~] Ensure VMR model load and verify operations honor active reservations.
  - Owner: Software Engineer
  - Progress: 2026-06-15 Codex added reservation enforcement for the authenticated VMR model-load path. Dedicated verify/list-model metadata checks remain to be audited.
- [~] Ensure list models and model metadata operations honor reservations where they expose VMR use or capacity.
  - Owner: Software Engineer
  - Progress: 2026-06-15 Codex covers normal routing decisions. Dedicated list-model/model-metadata paths remain to be audited.
- [x] Update routing simulation and explain endpoints to show reservation outcomes.
  - Owner: Software Engineer
  - Progress: 2026-06-15 Codex extended `RoutingDecision` and explanation timeline with reservation fields.
- [x] Confirm request history is created for reservation denials even when provider call does not occur.
  - Owner: Data Engineer
  - Progress: 2026-06-15 Codex added reservation fields to request history and covered denial round-trip/filtering in database integration tests.

### Observability, Logs, Analytics, And Metrics

Structured log requirements:

- [~] Log create, update, delete/deactivate, and validation conflict events at appropriate levels.
  - Owner: SRE
  - Progress: 2026-06-15 Codex added management and conflict logs; log capture tests and exact request-id coverage remain open.
- [~] Log every runtime reservation denial with tenant id, VMR id/name, reservation id/name, subject ids when present, start/end UTC, reason code, and request id.
  - Owner: SRE, Security Engineer
  - Progress: 2026-06-15 Codex logs tenant/VMR/reservation/user/credential ids, UTC window, and reason code for runtime outsider denials. VMR name/reservation name/request-id and auth-required log capture remain open.
- [~] Never log bearer tokens, API keys, raw credential secrets, request bodies containing secrets, or provider secrets.
  - Owner: Security Engineer
  - Progress: 2026-06-15 Codex uses ids and reason codes in reservation logs and responses; explicit secret-redaction tests remain open.
- [~] Use consistent reason codes across logs, history, analytics, metrics, API responses, and dashboard filters.
  - Owner: Data Engineer
  - Progress: 2026-06-15 Codex uses shared reason-code strings across API responses, routing decisions, request history, analytics, SDK tests, and dashboard filters. Metrics-specific reason-code validation remains open.

Example log message shape:

```text
[RoutingDecisionService] reservation denied tenant=ten_... vmr=vmr_... reservation=vmrr_... user=usr_... credential=cred_... startUtc=2026-07-01T18:00:00Z endUtc=2026-07-01T19:00:00Z reason=ReservationDenied requestId=req_...
```

Analytics and history requirements:

- [x] Extend request history entries with reservation fields.
  - Owner: Data Engineer
  - Progress: 2026-06-15 Codex added nullable reservation id/name/decision/reason/window fields to request history models, provider schema/migrations, and detail conversion.
- [x] Extend request history search filters with reservation id and reservation reason.
  - Owner: Data Engineer
  - Progress: 2026-06-15 Codex added backend filters, REST query parsing/OpenAPI parameters, and database integration tests.
- [x] Extend request history detail view with reservation gate explanation.
  - Owner: Data Engineer, UX Designer
  - Progress: 2026-06-15 Codex exposes reservation fields in request history detail payloads and dashboard detail UI with reservation id/name, decision, reason, and UTC window.
- [x] Extend request analytics events with reservation dimensions.
  - Owner: Data Engineer
  - Progress: 2026-06-15 Codex added nullable reservation fields to analytics events, provider schema/migrations, analytics capture, filters, and service/database tests.
- [x] Add analytics catalog entries for reservation dimensions if the catalog is explicit.
  - Owner: Data Engineer
  - Progress: 2026-06-15 Codex added `request.reservation_denied` and reservation dimensions to the Analytics workspace catalog.
- [~] Add dashboard analytics cards and charts for reservation denials by VMR, reservation, tenant, reason, and time.
  - Owner: Data Analyst, UX Designer
  - Progress: 2026-06-15 Codex added Analytics workspace reservation filters, reservation-denial metric cards, and per-reservation denial counts. Dedicated charts by VMR/tenant/reason over time remain open.
- [ ] Add operational metric counter such as `conductor_denials_total{reason="ReservationDenied"}` or the existing equivalent metric naming pattern.
  - Owner: SRE
  - Progress:
- [ ] Add alert guidance for unexpected spikes in `ReservationConflict`, `ReservationDenied`, or reservation evaluation failures.
  - Owner: SRE
  - Progress:

## Dashboard And UX Plan

### Navigation And Information Architecture

- [x] Add a top-level or VMR-adjacent Reservations workspace.
  - Owner: UX Designer, Software Engineer
  - Acceptance: Users can find reservations from the main nav and from a VMR detail/context action.
  - Progress: 2026-06-15 Codex added a top-level Reservations route, sidebar entry, VMR row action, deep-link VMR filter, VMR list badges, and VMR effective-configuration reservation context.
- [x] Add reservation badges to VMR lists and detail views.
  - Owner: UX Designer
  - States: unreserved, upcoming, drain soon, active, past, conflict/error.
  - Progress: 2026-06-15 Codex added open/upcoming/drain/reserved/conflict state badges on VMR list rows and reservation summaries in VMR detail.
- [ ] Add reservation context to API Explorer when a selected VMR is active or upcoming.
  - Owner: UX Designer, Developer Relations
  - Progress:
- [x] Add request history filters and detail fields for reservation denials.
  - Owner: Data Analyst, UX Designer
  - Progress: 2026-06-15 Codex added dashboard request-history reservation filters, reservation table column, and detail fields for id/name, decision, reason, and UTC window.
- [x] Add analytics filters and grouping for reservation id, reservation name, and reservation reason.
  - Owner: Data Analyst, UX Designer
  - Progress: 2026-06-15 Codex added backend Analytics workspace filters/catalog/result fields, dashboard filters, saved-report URL/state persistence, reservation-denial cards, and per-reservation denial counts.

### Primary User Workflows

- [x] Create reservation from global Reservations page.
  - Steps: choose VMR, choose time range, choose participants, review conflicts, save.
  - Acceptance: User can create a valid reservation without opening another tool.
  - Progress: 2026-06-15 Codex added create modal with VMR, tenant-derived context, UTC/local time conversion, drain lead, participant selection, validation, and save.
- [x] Create reservation from a VMR detail or row action.
  - Steps: VMR preselected, user chooses time range and participants.
  - Acceptance: VMR context is preserved and visible throughout.
  - Progress: 2026-06-15 Codex added VMR row Reservations action and `/reservations?vmrId=...` create preselection.
- [x] Edit future reservation.
  - Acceptance: User can update name, description, window, drain lead, and participants; conflict validation runs before save.
  - Progress: 2026-06-15 Codex added edit modal with validation and full subject replacement.
- [~] Manage active reservation.
  - Acceptance: UI makes the risk clear when changing an active window or participant list; audit trail records the change.
  - Progress: 2026-06-15 Codex supports editing active reservations through the same modal; explicit active-window risk copy and audit-trail display remain open.
- [x] Cancel/deactivate reservation.
  - Acceptance: Destructive confirmation uses localized text, names the reservation and VMR, and explains immediate access impact.
  - Progress: 2026-06-15 Codex added deactivate action with confirmation naming the reservation and explaining enforcement impact.
- [ ] Duplicate reservation.
  - Acceptance: User can copy participants and VMR into a new future window without copying id or audit fields.
  - Progress:
- [x] Inspect "who can use this VMR now".
  - Acceptance: UI shows current reservation state, participants, and why a selected user/credential would be allowed or denied.
  - Progress: 2026-06-15 Codex added effective access simulator for selected VMR/user/credential/time and list participant/state display.
- [~] Troubleshoot denied request.
  - Acceptance: From request history, support can see reservation id/name/window/reason and navigate to reservation details if authorized.
  - Progress: 2026-06-15 Codex added backend history fields and filters; dashboard request-history navigation/detail rendering remains open.

### UI Components

- [~] Reservation list/table.
  - Required columns: state, VMR, reservation name, start, end, duration, participants summary, denial count, created by, last updated.
  - Required controls: search, VMR filter, state filter, time range filter, subject filter, sort, pagination.
  - Progress: 2026-06-15 Codex added table with reservation, VMR, tenant, UTC window, participants, state, enabled, search, VMR filter, state filter, and actions. Denial count, created/updated columns, pagination, subject filter, and time-range filter remain open.
- [ ] Reservation timeline/calendar band.
  - Required behavior: show upcoming and active reservations for selected VMRs without requiring a charting library.
  - Progress:
- [x] Create/edit drawer or modal.
  - Required fields: VMR selector, name, description, start, end, timezone display, drain lead, users, credentials, labels/tags/metadata if exposed.
  - Progress: 2026-06-15 Codex added modal covering VMR, name, description, local display time with UTC submission, drain lead, users, credentials, and active flag.
- [~] Participant picker.
  - Required behavior: search users and credentials, show type badges, show credential owner where available, prevent duplicates, show inactive or cross-tenant errors.
  - Progress: 2026-06-15 Codex added tenant-filtered user and credential checkbox selection with duplicate prevention through keyed subject selection. Search, owner display, inactive/cross-tenant error surfacing, and richer badges remain open.
- [~] Conflict resolver.
  - Required behavior: show overlapping reservation name, window, owner, and link; block save until resolved.
  - Progress: 2026-06-15 Codex added validate action that displays validation/conflict errors before save. Linked conflict detail/resolution flow remains open.
- [x] Effective access simulator.
  - Required behavior: choose user/credential/time and show allowed/denied reason plus next reservation transition.
  - Progress: 2026-06-15 Codex added evaluate access modal with VMR, user, credential, time, and result display.
- [ ] Reservation detail page or panel.
  - Required sections: summary, participant list, audit fields, recent denials, linked request history, analytics snapshot.
  - Progress:

### UX Acceptance Criteria

- [ ] A new operator can answer "is this VMR reserved now?" from the VMR list in under 10 seconds.
  - Validation: UX test.
  - Progress:
- [ ] A support engineer can answer "why was this request denied?" from request history without reading logs.
  - Validation: UX test and support review.
  - Progress:
- [ ] A tenant admin can create a reservation with 1 VMR, 2 users, and 2 credentials in under 2 minutes with no docs.
  - Validation: usability test.
  - Progress:
- [~] The UI clearly distinguishes local display time from UTC audit time.
  - Validation: design review and tests.
  - Progress: 2026-06-15 Codex labels list windows as UTC and uses local datetime inputs for editing. Further design review and automated viewport tests remain open.
- [~] Conflict errors explain exactly which reservation blocks save.
  - Validation: negative UI tests.
  - Progress: 2026-06-15 Codex surfaces backend validation errors; conflict-specific linking and negative UI tests remain open.
- [ ] Active reservation changes explain immediate access impact before save.
  - Validation: design review.
  - Progress:
- [ ] Participant selection does not require knowing ids, but ids are visible in details for support/debugging.
  - Validation: UX and support review.
  - Progress:
- [~] Empty states guide users to create the first reservation or change filters without marketing copy.
  - Validation: UI review.
  - Progress: 2026-06-15 Codex uses existing table/modal empty states for users/credentials; reservation-list empty-state review remains open.
- [ ] Large data states remain usable with hundreds of VMRs, thousands of users, and thousands of credentials.
  - Validation: seeded dashboard test.
  - Progress:
- [ ] Long names, long email addresses, long credential names, and localized labels do not overflow controls.
  - Validation: responsive and pseudo-locale screenshots.
  - Progress:
- [ ] Keyboard-only users can create, edit, cancel, and inspect reservations.
  - Validation: keyboard walkthrough.
  - Progress:
- [ ] Screen reader labels identify reservation status, time window, conflict state, and participant type.
  - Validation: accessibility review.
  - Progress:
- [ ] Mobile/tablet layouts preserve task completion, even if dense analytics tables collapse or simplify.
  - Validation: viewport screenshots.
  - Progress:

### Dashboard I18N And Accessibility

- [ ] Add translation keys for every visible reservation string.
  - Owner: UX Designer, Software Engineer
  - Progress:
- [~] Add translation keys for aria labels, tooltips, validation messages, confirmation prompts, and empty states.
  - Owner: UX Designer, Software Engineer
  - Progress: 2026-06-15 Codex added native hover tooltip coverage for reservation controls and a dashboard-wide fallback for unlabeled controls. Strings are still hard-coded and translation-key migration remains open.
- [ ] Use locale-aware formatting for dates, times, durations, numbers, and lists.
  - Owner: Software Engineer
  - Progress:
- [ ] Test English, a long Latin pseudo-locale, CJK, and RTL/pseudo-RTL.
  - Owner: QA Engineer
  - Progress:
- [ ] Ensure `document.lang` and direction are correct if dashboard i18n infrastructure exists or is added.
  - Owner: Software Engineer
  - Progress:
- [ ] Avoid raw enum display strings; map reason codes and states to localized labels.
  - Owner: Software Engineer
  - Progress:

## SDK Plan

### C# SDK

- [x] Add methods to list, create, validate, read, update, delete/deactivate reservations.
  - Owner: Developer Relations, Software Engineer
  - Progress: 2026-06-15 Codex added route helpers to `ConductorClient`.
- [x] Add method to read effective reservation access for a VMR.
  - Owner: Developer Relations
  - Progress: 2026-06-15 Codex added `GetVirtualModelRunnerReservationEffectiveAsync`.
- [~] Add tests for query serialization, body serialization, bearer token propagation, cancellation token propagation, and API error handling.
  - Owner: Automation Engineer
  - Progress: 2026-06-15 Codex added route/query/body tests for reservation helpers. Bearer-token coverage existed for the client; cancellation and API-error reservation-specific tests remain open.
- [x] Update SDK README examples.
  - Owner: Documentation Engineer
  - Progress: 2026-06-15 Codex added reservation examples to `sdk/csharp/README.md`, `sdk/javascript/README.md`, and `sdk/python/README.md`.

### JavaScript SDK

- [x] Add reservation client methods in `sdk/javascript/src/index.js`.
  - Owner: Developer Relations, Software Engineer
  - Progress: 2026-06-15 Codex added list/get/create/update/delete/validate/VMR-scoped/effective helpers.
- [~] Add tests in `sdk/javascript/test`.
  - Owner: Automation Engineer
  - Progress: 2026-06-15 Codex added request-construction tests for reservation helpers and request-history/analytics reservation filters. Error-response/proxy-denial tests remain open.
- [ ] Update package README examples.
  - Owner: Documentation Engineer
  - Progress:

### Python SDK

- [x] Add reservation methods following the existing Python SDK style.
  - Owner: Developer Relations, Software Engineer
  - Progress: 2026-06-15 Codex added list/get/create/update/delete/validate/VMR-scoped/effective helpers.
- [~] Add tests for success, validation errors, auth errors, and reservation denial responses where SDK surfaces proxy calls.
  - Owner: Automation Engineer
  - Progress: 2026-06-15 Codex added request-construction tests for reservation helpers and request-history/analytics reservation filters. Error-response/proxy-denial tests remain open.
- [ ] Update README examples.
  - Owner: Documentation Engineer
  - Progress:

## Postman Plan

- [x] Add a "VMR Reservations" folder to `Conductor.postman_collection.json`.
  - Owner: Developer Relations
  - Progress: 2026-06-15 Codex added folder.
- [x] Add create, validate, list, read, update, delete/deactivate, VMR-scoped list, and effective access requests.
  - Owner: Developer Relations
  - Progress: 2026-06-15 Codex added requests for each reservation endpoint.
- [~] Add example request bodies for user participant, credential participant, mixed participants, overlap validation, and effective access simulation.
  - Owner: Developer Relations, QA Engineer
  - Progress: 2026-06-15 Codex added mixed user/credential bodies, effective-access request variables, an overlap validation negative example, and an outsider effective-access negative example. Dedicated single-subject, missing-identity, cross-tenant, and invalid-time examples remain open.
- [~] Add negative examples for missing identity, outsider identity, overlap conflict, cross-tenant subject, and invalid time range.
  - Owner: QA Engineer
  - Progress: 2026-06-15 Codex added overlap and outsider negative examples. Missing identity, cross-tenant subject, and invalid time range examples remain open.
- [x] Add environment variables for tenant id, VMR id, user id, credential id, reservation id, and bearer token.
  - Owner: Developer Relations
  - Progress: 2026-06-15 Codex added reservation variables to the collection variable set.
- [ ] Run collection smoke tests against a local or CI environment.
  - Owner: Automation Engineer
  - Progress:

## Documentation Plan

- [x] Add ADR in `docs/adr` describing reservation semantics, enforcement order, identity matching, and no-admin-bypass decision.
  - Owner: Principal Architect
  - Progress: 2026-06-15 Codex added `docs/adr/0003-virtual-model-runner-reservations.md`.
- [x] Update `REST_API.md` with endpoints, request/response examples, status codes, and reason codes.
  - Owner: Documentation Engineer
  - Progress: 2026-06-15 Codex documented reservation endpoints, request history/analytics dimensions, status codes, and reason codes.
- [x] Update `README.md` with a short feature overview and link to detailed docs.
  - Owner: Documentation Engineer
  - Progress: 2026-06-15 Codex added reservation overview and request-history/analytics notes.
- [x] Update `TESTING.md` with reservation test suites and how to run them.
  - Owner: QA Engineer, Documentation Engineer
  - Progress: 2026-06-15 Codex added the VMR Reservations Release Gate with backend, dashboard, docs/tooling, and observability checks.
- [x] Update `CHANGELOG.md`.
  - Owner: Technical Program Manager
  - Progress: 2026-06-15 Codex added reservation feature notes.
- [x] Add an admin guide section: creating reservations, choosing participants, understanding drain windows, reading denials.
  - Owner: Documentation Engineer, UX Designer
  - Progress: 2026-06-15 Codex added `MANAGING_RESERVATIONS.md` with dashboard workflow, create/evaluate flows, drain-window semantics, and denial interpretation.
- [x] Add troubleshooting guide for `ReservationDenied`, `ReservationDrainDenied`, `ReservationAuthenticationRequired`, and `ReservationConflict`.
  - Owner: Technical Support Engineer, Documentation Engineer
  - Progress: 2026-06-15 Codex documented all reservation reason codes and troubleshooting steps in `MANAGING_RESERVATIONS.md`.
- [x] Add support runbook: how to diagnose a denied request from request id to reservation id.
  - Owner: Technical Support Engineer
  - Progress: 2026-06-15 Codex added request-history, analytics, effective-access, and reservation-inspection runbook steps in `MANAGING_RESERVATIONS.md`.
- [ ] Add compliance note describing audit evidence and retention assumptions.
  - Owner: Compliance Officer
  - Progress:
- [x] Add developer guide examples for SDKs and Postman.
  - Owner: Developer Relations
  - Progress: 2026-06-15 Codex added reservation examples to C#, JavaScript, and Python SDK READMEs plus Postman positive/negative examples.

## Delivery Backlog

### Phase 0: Product And Architecture Decisions

- [~] RES-0001: Confirm management permissions and reader permissions.
  - Owner: Product Manager, Security Engineer
  - Dependencies: none
  - Acceptance: Decision documented in ADR and `AuthorizationConfig` plan.
  - Tests: Authorization tests listed before implementation.
  - Requirement Sources: `AUTHENTICATION.md`, `BACKEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex implemented tenant-admin management and authenticated read/effective-access routing in `AuthorizationConfig`; ADR/product sign-off still needed.
- [x] RES-0002: Confirm lifecycle policy for delete vs. deactivate.
  - Owner: Compliance Officer, Database Engineer / DBA
  - Dependencies: none
  - Acceptance: Retention and audit behavior documented.
  - Tests: Delete/deactivate tests defined.
  - Requirement Sources: `AUTHENTICATION.md`, `REPOSITORY_REQUIREMENTS.md`
  - Progress: 2026-06-15 Codex implemented soft deactivate via `DELETE /v1.0/vmrreservations/{id}` with audit-friendly persistence.
- [~] RES-0003: Confirm drain window default and maximum.
  - Owner: SRE, Principal Architect
  - Dependencies: VMR timeout settings review
  - Acceptance: Configuration keys and defaults documented.
  - Tests: Boundary and configuration tests defined.
  - Requirement Sources: `BACKEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex implemented per-reservation `AdmissionDrainLeadMs` with 86400000 ms maximum; no configurable default added yet.
- [x] RES-0004: Write reservation ADR.
  - Owner: Principal Architect
  - Dependencies: RES-0001 through RES-0003
  - Acceptance: ADR reviewed by Security, SRE, Product, DBA.
  - Tests: N/A
  - Requirement Sources: `EXAMPLE_APPLICATIONS.md`, `WRITING_DOCUMENTS.md`
  - Progress: 2026-06-15 Codex added `docs/adr/0003-virtual-model-runner-reservations.md`; formal cross-functional approval remains a launch-readiness item.

### Phase 1: Core Domain And Persistence

- [x] RES-0101: Add reservation models, subjects, enums, requests, and responses.
  - Owner: Software Engineer
  - Dependencies: RES-0004
  - Acceptance: Models compile, serialize, parse DB rows, and include XML docs.
  - Tests: Model serialization and validation unit tests.
  - Requirement Sources: `BACKEND_ARCHITECTURE.md`, `CODE_STYLE.md`
  - Progress: 2026-06-15 Codex added reservation/subject/evaluation models, enums, id prefixes, and shared model/id tests.
- [x] RES-0102: Add provider-neutral reservation DB interface.
  - Owner: Database Engineer / DBA
  - Dependencies: RES-0101
  - Acceptance: Interface exposes domain methods, no generic CRUD.
  - Tests: Test.Shared contract compiled against fake/provider fixture.
  - Requirement Sources: `BACKEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added `IVirtualModelRunnerReservationMethods` and provider-neutral implementation.
- [x] RES-0103: Add schema and provider implementations for SQLite.
  - Owner: Database Engineer / DBA
  - Dependencies: RES-0102
  - Acceptance: CRUD, overlap, subject replacement, active lookup pass.
  - Tests: SQLite provider matrix.
  - Requirement Sources: `BACKEND_TEST_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added SQLite schema, driver wiring, and SQLite-backed integration tests for CRUD, filters, overlaps, and drain lookup.
- [~] RES-0104: Add schema and provider implementations for MySQL.
  - Owner: Database Engineer / DBA
  - Dependencies: RES-0102
  - Acceptance: Same behavior as SQLite.
  - Tests: MySQL provider matrix.
  - Requirement Sources: `BACKEND_TEST_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added MySQL schema and driver wiring; provider-specific runtime matrix still needed.
- [~] RES-0105: Add schema and provider implementations for PostgreSQL.
  - Owner: Database Engineer / DBA
  - Dependencies: RES-0102
  - Acceptance: Same behavior as SQLite.
  - Tests: PostgreSQL provider matrix.
  - Requirement Sources: `BACKEND_TEST_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added PostgreSQL schema and driver wiring; provider-specific runtime matrix still needed.
- [~] RES-0106: Add schema and provider implementations for SQL Server.
  - Owner: Database Engineer / DBA
  - Dependencies: RES-0102
  - Acceptance: Same behavior as SQLite.
  - Tests: SQL Server provider matrix.
  - Requirement Sources: `BACKEND_TEST_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added SQL Server schema and driver wiring, with tenant cascade paths avoided; provider-specific runtime matrix still needed.
- [x] RES-0107: Extend backup/export/import for reservations.
  - Owner: Database Engineer / DBA
  - Dependencies: RES-0103 through RES-0106
  - Acceptance: Backup and restore preserve reservations and subjects.
  - Tests: Backup/restore integration tests.
  - Requirement Sources: `REPOSITORY_REQUIREMENTS.md`
  - Progress: 2026-06-15 Codex added backup schema version `1.3`, export/validate/restore support, dashboard backup summaries, docs, and backup controller tests for preserving reservation subjects.

### Phase 2: Services, Authorization, And APIs

- [x] RES-0201: Add reservation service validation and evaluation.
  - Owner: Software Engineer
  - Dependencies: Phase 1
  - Acceptance: Service returns structured results for no reservation, allow, deny, drain deny, auth required, and conflict.
  - Tests: Unit tests for every decision path.
  - Requirement Sources: `AUTHENTICATION.md`, `BACKEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added validation/evaluation service and shared tests for valid drafts, overlaps, duplicate subjects, no-reservation allow, participant allow, outsider deny, and drain deny.
- [~] RES-0202: Add request types and authorization configuration.
  - Owner: Security Engineer
  - Dependencies: RES-0201
  - Acceptance: Manage/read permissions are explicit and tested.
  - Tests: Authorization positive and negative tests.
  - Requirement Sources: `AUTHENTICATION.md`
  - Progress: 2026-06-15 Codex added request types, resolver mapping, and authorization config; dedicated authorization tests still needed.
- [~] RES-0203: Add reservation controller and route module.
  - Owner: Software Engineer
  - Dependencies: RES-0201, RES-0202
  - Acceptance: All CRUD, validate, list, VMR-scoped list, and effective access endpoints work.
  - Tests: Controller/API integration tests.
  - Requirement Sources: `BACKEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added controller and route module for CRUD, validate, list, VMR-scoped list, and effective access; controller/API integration tests still needed.
- [x] RES-0204: Add OpenAPI metadata for reservation endpoints.
  - Owner: Software Engineer, Documentation Engineer
  - Dependencies: RES-0203
  - Acceptance: API Explorer and generated docs show schemas and status codes.
  - Tests: OpenAPI snapshot or schema validation test.
  - Requirement Sources: `BACKEND_ARCHITECTURE.md`, `FRONTEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added OpenAPI tags, summaries, parameters, bodies, and responses for reservation endpoints.

### Phase 3: Routing Enforcement And Observability

- [~] RES-0301: Integrate reservation gate into routing.
  - Owner: Principal Architect, Software Engineer
  - Dependencies: Phase 2
  - Acceptance: Active reservation gates all VMR use paths before provider call.
  - Tests: Proxy/routing integration tests.
  - Requirement Sources: `AUTHENTICATION.md`, `BACKEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added the reservation gate before request-type/model-access/endpoint selection, wired the authenticated management-plane VMR model-load path through the same reservation evaluator, and added tests for outsider deny, credential allow, user-owned credential allow, drain deny, and model-load anonymous deny/user allow. Broader proxy integration/per-request-type tests remain.
- [x] RES-0302: Extend `RoutingDecision` and explanation timeline.
  - Owner: Software Engineer
  - Dependencies: RES-0301
  - Acceptance: Simulation and explain endpoints show reservation gate.
  - Tests: Routing explanation tests.
  - Requirement Sources: `BACKEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added reservation fields to `RoutingDecision` and `ReservationGate` timeline attributes.
- [x] RES-0303: Extend request history capture and filters.
  - Owner: Data Engineer
  - Dependencies: RES-0301
  - Acceptance: Denied requests are searchable by reservation and reason.
  - Tests: History create/search/detail tests.
  - Requirement Sources: `AUTHENTICATION.md`
  - Progress: 2026-06-15 Codex added nullable reservation id/name/decision/reason/window fields, provider schema/migrations, search and summary filters, REST query parsing/OpenAPI metadata, and DB integration tests for reservation history filtering.
- [x] RES-0304: Extend analytics capture and query dimensions.
  - Owner: Data Engineer, Data Analyst
  - Dependencies: RES-0303
  - Acceptance: Reservation denial counts can be grouped by VMR, reservation, tenant, reason, and time.
  - Tests: Analytics query tests.
  - Requirement Sources: `BACKEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added nullable reservation dimensions to analytics events, event search filters, analytics overview `ReservationDeniedCount` and `ReservationDenialCounts`, Analytics workspace reservation filters/catalog/result fields, REST filters, dashboard analytics cards, and service/database tests.
- [~] RES-0305: Add metrics and structured logs.
  - Owner: SRE
  - Dependencies: RES-0301
  - Acceptance: Denials and conflicts are visible in logs and metrics with no secrets.
  - Tests: Log capture and metric counter tests.
  - Requirement Sources: `AUTHENTICATION.md`
  - Progress: 2026-06-15 Codex added create/update/deactivate and runtime-denial logs; existing denial metrics apply by reason code, but metric-specific tests/alert guidance remain open.

### Phase 4: Dashboard

- [x] RES-0401: Add reservation API client functions in `dashboard/src/api`.
  - Owner: Software Engineer
  - Dependencies: RES-0203
  - Acceptance: Client supports all reservation endpoints and propagates errors.
  - Tests: API client tests.
  - Requirement Sources: `FRONTEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added dashboard API methods for list, scoped list, read, create, update, deactivate, validate, and effective access.
- [x] RES-0402: Add reservations route, nav entry, list view, and detail panel/page.
  - Owner: Software Engineer, UX Designer
  - Dependencies: RES-0401
  - Acceptance: User can browse, filter, inspect, and navigate reservations.
  - Tests: Component and E2E tests.
  - Requirement Sources: `FRONTEND_ARCHITECTURE.md`, `I18N.md`
  - Progress: 2026-06-15 Codex added `/reservations` route, sidebar entry, refresh icon, filterable table, participant summaries, edit/detail modal data, effective-access modal, and VMR deep-link filtering.
- [~] RES-0403: Add create/edit/cancel workflows.
  - Owner: Software Engineer, UX Designer
  - Dependencies: RES-0402
  - Acceptance: User can complete all primary workflows with validation and conflict handling.
  - Tests: Positive and negative workflow tests.
  - Requirement Sources: `FRONTEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added create/edit/deactivate, validate, participant picker, UTC review text, VMR preselection from row action, and effective access simulator. Duplicate workflow and deeper active-change warnings remain open.
- [x] RES-0404: Add VMR badges and VMR-scoped reservation panels.
  - Owner: Software Engineer, UX Designer
  - Dependencies: RES-0402
  - Acceptance: VMR list/detail surfaces current and upcoming reservation state.
  - Tests: Responsive UI tests.
  - Requirement Sources: `FRONTEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added reservation state badges to VMR list rows, a Reservations action per VMR, and reservation context in the VMR effective-configuration detail view.
- [x] RES-0405: Add reservation fields to request history and analytics views.
  - Owner: Software Engineer, Data Analyst, UX Designer
  - Dependencies: RES-0303, RES-0304
  - Acceptance: Denials are discoverable and explainable without logs.
  - Tests: Dashboard filter/detail tests.
  - Requirement Sources: `FRONTEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added request-history reservation table column, filters, detail fields, dashboard API query params, analytics reservation filters, reservation-denial cards, per-reservation denial counts, and saved-report URL/state persistence.
- [~] RES-0406: Complete dashboard i18n, accessibility, and responsive review.
  - Owner: UX Designer, QA Engineer
  - Dependencies: RES-0402 through RES-0405
  - Acceptance: No hard-coded strings, keyboard path works, screenshots pass desktop/tablet/mobile and pseudo-locale checks.
  - Tests: I18N, a11y, and viewport checks.
  - Requirement Sources: `I18N.md`, `FRONTEND_ARCHITECTURE.md`
  - Progress: 2026-06-15 Codex added explicit native hover tooltips to the VMR Reservations page controls and a dashboard-wide runtime fallback that assigns titles to remaining buttons, labels, inputs, selects, textareas, and role=button controls. 2026-06-16 Codex fixed Create/Edit Reservation modal field alignment for rows with mixed helper-text/control heights, tightened participant card heading spacing, made reservation table row clicks open the edit dialog, and added a row-level View JSON action. Hard-coded string migration, keyboard walkthrough, screen-reader review, and responsive/pseudo-locale screenshots remain open.

### Phase 5: SDKs, Postman, And Docs

- [x] RES-0501: Update C# SDK and tests.
  - Owner: Developer Relations
  - Dependencies: RES-0203
  - Acceptance: Reservation methods are tested and documented.
  - Tests: C# SDK tests.
  - Requirement Sources: `REPOSITORY_REQUIREMENTS.md`
  - Progress: 2026-06-15 Codex added C# SDK reservation methods, route tests, reservation analytics filter tests, and README examples.
- [x] RES-0502: Update JavaScript SDK and tests.
  - Owner: Developer Relations
  - Dependencies: RES-0203
  - Acceptance: Reservation methods are tested and documented.
  - Tests: JavaScript SDK tests.
  - Requirement Sources: `REPOSITORY_REQUIREMENTS.md`
  - Progress: 2026-06-15 Codex added JavaScript SDK reservation methods, tests, reservation analytics filter tests, and README examples.
- [x] RES-0503: Update Python SDK and tests.
  - Owner: Developer Relations
  - Dependencies: RES-0203
  - Acceptance: Reservation methods are tested and documented.
  - Tests: Python SDK tests.
  - Requirement Sources: `REPOSITORY_REQUIREMENTS.md`
  - Progress: 2026-06-15 Codex added Python SDK reservation methods, tests, reservation analytics filter tests, and README examples.
- [~] RES-0504: Update Postman collection.
  - Owner: Developer Relations, QA Engineer
  - Dependencies: RES-0203
  - Acceptance: Positive and negative examples run against local server.
  - Tests: Postman smoke run.
  - Requirement Sources: `REPOSITORY_REQUIREMENTS.md`
  - Progress: 2026-06-15 Codex added a `VMR Reservations` folder, positive request examples, overlap and outsider negative examples, analytics reservation filter examples, `reservationId`/`reservationAtUtc` variables, and JSON parse verification. Live collection smoke run remains open.
- [x] RES-0505: Update public docs and support runbooks.
  - Owner: Documentation Engineer
  - Dependencies: Phase 3, Phase 4
  - Acceptance: Docs cover API, dashboard, SDKs, denials, troubleshooting, audit, and tests.
  - Tests: Docs review.
  - Requirement Sources: `WRITING_DOCUMENTS.md`
  - Progress: 2026-06-15 Codex updated `REST_API.md`, `README.md`, `TESTING.md`, `CHANGELOG.md`, SDK READMEs, `MANAGING_RESERVATIONS.md`, and ADR 0003. Formal compliance note/review remains a launch-readiness item.

### Phase 6: Release And Operations

- [ ] RES-0601: Add rollout configuration and deployment notes.
  - Owner: DevOps Engineer
  - Dependencies: Phases 1 through 5
  - Acceptance: Environments can deploy schema and feature safely.
  - Tests: Staging deployment.
  - Requirement Sources: `REPOSITORY_REQUIREMENTS.md`
  - Progress:
- [ ] RES-0602: Add operational dashboards and alert guidance.
  - Owner: SRE
  - Dependencies: RES-0305
  - Acceptance: SRE can observe denial spikes and reservation evaluation failures.
  - Tests: Synthetic event validation.
  - Requirement Sources: `AUTHENTICATION.md`
  - Progress:
- [ ] RES-0603: Complete security review.
  - Owner: Security Engineer
  - Dependencies: Phases 1 through 5
  - Acceptance: Tenant isolation, bypass, secret safety, and denial auditability are signed off.
  - Tests: Security test matrix complete.
  - Requirement Sources: `AUTHENTICATION.md`
  - Progress:
- [ ] RES-0604: Complete launch readiness review.
  - Owner: Technical Program Manager
  - Dependencies: RES-0601 through RES-0603
  - Acceptance: Launch checklist complete and release notes approved.
  - Tests: Full CI and manual UX smoke.
  - Requirement Sources: all
  - Progress:

## Exhaustive Test Plan

### Backend Unit Tests

- [x] No reservation exists, request is allowed to continue to existing checks.
  - Progress: 2026-06-15 Codex covered in `VirtualModelRunnerReservationServiceTests.EvaluateAsync_WithNoReservation_AllowsRequest`.
- [x] Active reservation exists and direct user participant is allowed to continue.
  - Progress: 2026-06-15 Codex covered in `ModelLoadServiceTests.LoadVirtualModelRunnerAsync_WithActiveReservation_RequiresParticipantIdentity`.
- [x] Active reservation exists and direct credential participant is allowed to continue.
  - Progress: 2026-06-15 Codex covered in `VirtualModelRunnerReservationServiceTests.EvaluateAsync_WithCredentialParticipant_AllowsRequest` and routing tests.
- [x] Active reservation exists and credential owner user participant is allowed when owner matches.
  - Progress: 2026-06-15 Codex covered in `RoutingDecisionServiceTests.Evaluate_WithUserReservationAndOwnedCredential_Routes`.
- [x] Active reservation exists and unrelated user is denied.
  - Progress: 2026-06-15 Codex covered in `EvaluateAsync_WithNonParticipant_DeniesRequest` and routing denial tests.
- [x] Active reservation exists and unrelated credential is denied.
  - Progress: 2026-06-15 Codex covered in reservation service and routing tests.
- [~] Active reservation exists and anonymous/no-credential request returns auth-required result.
  - Progress: 2026-06-15 Codex covered the VMR model-load anonymous denial path; direct reservation evaluator reason-code assertion remains open.
- [~] Admin user not listed on reservation is denied.
  - Progress: 2026-06-15 Codex implemented no bypass in the evaluator; explicit admin-identity test remains open.
- [~] Listed participant still denied by model access policy after reservation allow.
  - Progress: 2026-06-15 Codex keeps routing order so model access follows reservation allow; explicit combined positive/deny test remains open.
- [~] Reservation boundary allows at `StartUtc` and stops applying at `EndUtc`.
  - Progress: 2026-06-15 Codex evaluator uses inclusive start/exclusive end comparisons; explicit boundary test remains open.
- [~] Request one tick before `StartUtc` is allowed unless inside drain window.
  - Progress: 2026-06-15 Codex covers drain-window denial and database pre-start no-drain lookup; explicit one-tick allow test remains open.
- [~] Request at `EndUtc` is allowed by reservation gate.
  - Progress: 2026-06-15 Codex evaluator uses exclusive end comparison; explicit endpoint boundary test remains open.
- [x] Drain window denies nonparticipants before `StartUtc`.
  - Progress: 2026-06-15 Codex covered in reservation service and routing decision tests.
- [~] Drain window allows participants before `StartUtc`.
  - Progress: 2026-06-15 Codex participant matching applies to drain windows; explicit test remains open.
- [~] Inactive reservation is ignored.
  - Progress: 2026-06-15 Codex database active lookup excludes inactive reservations in overlap/active lookup tests; direct evaluator test remains open.
- [x] Overlapping active reservations fail validation.
  - Progress: 2026-06-15 Codex covered in `ValidateAsync_WithOverlappingReservation_ReturnsOverlapError`.
- [~] Multiple active reservations at runtime return fail-closed conflict.
  - Progress: 2026-06-15 Codex implemented `ReservationConflict`; explicit service/runtime test remains open.
- [~] Cross-tenant VMR is rejected on create.
  - Progress: 2026-06-15 Codex validation checks VMR tenant match; explicit named test remains open.
- [~] Cross-tenant user subject is rejected on create.
  - Progress: 2026-06-15 Codex validation checks user tenant match; explicit named test remains open.
- [~] Cross-tenant credential subject is rejected on create.
  - Progress: 2026-06-15 Codex validation checks credential tenant match; explicit named test remains open.
- [~] Inactive user subject is rejected.
  - Progress: 2026-06-15 Codex validation checks active users; explicit named test remains open.
- [~] Inactive credential subject is rejected.
  - Progress: 2026-06-15 Codex validation checks active credentials; explicit named test remains open.
- [x] Duplicate subjects are rejected or de-duplicated consistently.
  - Progress: 2026-06-15 Codex covered in `ValidateAsync_WithDuplicateSubjects_ReturnsDuplicateError`.
- [~] Empty subject list is rejected.
  - Progress: 2026-06-15 Codex validation rejects empty subjects; explicit named test remains open.
- [~] `StartUtc >= EndUtc` is rejected.
  - Progress: 2026-06-15 Codex validation rejects invalid windows; explicit named boundary test remains open.
- [~] Negative drain lead is rejected.
  - Progress: 2026-06-15 Codex validation rejects negative drain lead; explicit named test remains open.
- [~] Excessive drain lead is clamped or rejected according to config.
  - Progress: 2026-06-15 Codex validation rejects drain lead above maximum; explicit named test remains open.
- [ ] Invalid metadata JSON is rejected if metadata is typed or serialized.
  - Progress:
- [ ] Long name, description, labels, and tags follow validation limits.
  - Progress:
- [ ] Cancellation tokens are honored in service and DB methods.
  - Progress:

### Database Provider Tests

- [x] Create reservation with user subject.
  - Progress: 2026-06-15 Codex covered in SQLite-backed reservation database integration tests.
- [~] Create reservation with credential subject.
  - Progress: 2026-06-15 Codex covered credential subjects in service/SDK/Postman tests; dedicated DB create-only test remains open.
- [x] Create reservation with mixed subjects.
  - Progress: 2026-06-15 Codex covered mixed user and credential subjects in `Reservation_CreateAndRead_RoundTripsSubjectsAndMetadata`.
- [x] Read reservation by tenant and id.
  - Progress: 2026-06-15 Codex covered in `Reservation_CreateAndRead_RoundTripsSubjectsAndMetadata`.
- [ ] Read by id without tenant does not leak cross-tenant data where tenant is required.
  - Progress:
- [~] Enumerate by tenant.
  - Progress: 2026-06-15 Codex covered tenant-scoped enumeration with subject/VMR filters; standalone tenant-only list coverage remains open.
- [x] Enumerate by VMR.
  - Progress: 2026-06-15 Codex covered with `Reservation_Enumerate_WithSubjectFilter_ReturnsMatchingReservation`.
- [ ] Enumerate by state: active, upcoming, past, inactive.
  - Progress:
- [ ] Enumerate by time range.
  - Progress:
- [x] Search by subject.
  - Progress: 2026-06-15 Codex covered user subject filtering in SQLite-backed database tests.
- [~] Replace subjects atomically.
  - Progress: 2026-06-15 Codex update path replaces subjects transactionally through provider-neutral methods; dedicated atomic failure test remains open.
- [~] Update reservation window.
  - Progress: 2026-06-15 Codex implemented update path and SDK/dashboard support; dedicated DB window update assertion remains open.
- [~] Deactivate or delete according to lifecycle decision.
  - Progress: 2026-06-15 Codex covered soft deactivate as part of overlap exclusion; route/API lifecycle coverage remains open.
- [x] Active lookup finds current reservation.
  - Progress: 2026-06-15 Codex covered drain-inclusive active lookup in SQLite-backed database tests.
- [~] Active lookup returns none outside window.
  - Progress: 2026-06-15 Codex covered pre-start lookup without drain; post-end lookup remains open.
- [~] Overlap query detects partial overlap at start.
  - Progress: 2026-06-15 Codex covered overlap counting for a contained interval; explicit start/end partial overlap cases remain open.
- [ ] Overlap query detects partial overlap at end.
  - Progress:
- [ ] Overlap query detects containing overlap.
  - Progress:
- [ ] Overlap query allows adjacent windows where `EndUtc == next StartUtc`.
  - Progress:
- [~] Subject uniqueness constraint works.
  - Progress: 2026-06-15 Codex added provider schema constraints and duplicate-subject service validation; direct constraint violation test remains open.
- [x] Indexes are present or query plan is acceptable for active lookup.
  - Progress: 2026-06-15 Codex covered schema indexes in `RequestHistorySchemaTests`/provider schema assertions and reservation table creation.
- [ ] SQL injection-like names and metadata are stored safely and do not affect queries.
  - Progress:
- [ ] Backup and restore preserve reservations and subjects.
  - Progress:
- [ ] Tests pass on SQLite, MySQL, PostgreSQL, and SQL Server implementations.
  - Progress:

### API And Authorization Tests

- [ ] Create reservation as authorized system admin.
  - Progress:
- [ ] Create reservation as authorized tenant admin or reservation manager.
  - Progress:
- [ ] Create reservation as unauthorized user returns `403`.
  - Progress:
- [ ] Create reservation without auth returns `401`.
  - Progress:
- [ ] Read reservation with read permission succeeds.
  - Progress:
- [ ] Read reservation without read permission returns `403`.
  - Progress:
- [~] Validate overlapping draft returns conflict details without persisting.
  - Progress: 2026-06-15 Codex covered service-level overlap validation; HTTP API integration test remains open.
- [ ] Update future reservation succeeds.
  - Progress:
- [ ] Update active reservation follows product decision and logs audit event.
  - Progress:
- [ ] Delete/deactivate future reservation succeeds.
  - Progress:
- [ ] Delete/deactivate active reservation follows product decision and logs audit event.
  - Progress:
- [ ] List endpoint filters by VMR, state, subject, and time range.
  - Progress:
- [ ] VMR-scoped list rejects cross-tenant VMR.
  - Progress:
- [~] Effective access endpoint returns allow for participant.
  - Progress: 2026-06-15 Codex implemented endpoint and service behavior; HTTP API integration test remains open.
- [~] Effective access endpoint returns deny for outsider.
  - Progress: 2026-06-15 Codex implemented endpoint and service behavior; HTTP API integration test remains open.
- [~] Effective access endpoint returns no-reservation state outside window.
  - Progress: 2026-06-15 Codex implemented no-reservation evaluator response; HTTP API integration test remains open.
- [~] API errors include stable reason codes and safe messages.
  - Progress: 2026-06-15 Codex implemented stable reason codes in routing/model-load/API validation paths; full HTTP negative matrix remains open.
- [~] OpenAPI includes request/response schemas and error status codes.
  - Progress: 2026-06-15 Codex added reservation endpoint metadata; snapshot/schema validation test remains open.

### Routing And Proxy Integration Tests

- [~] Proxy request outside reservation continues to existing routing behavior.
  - Progress: 2026-06-15 Codex existing routing tests continue to pass with the reservation gate enabled; explicit proxy integration coverage remains open.
- [ ] Proxy request by listed user during reservation reaches provider or downstream stub.
  - Progress:
- [x] Proxy request by listed credential during reservation reaches provider or downstream stub.
  - Progress: 2026-06-15 Codex covered routing decision success for listed credential before endpoint selection; full HTTP proxy stub remains open.
- [x] Proxy request by credential owned by listed user reaches provider or downstream stub.
  - Progress: 2026-06-15 Codex covered routing decision success for a credential owned by a listed user.
- [x] Proxy request by outsider during reservation returns `403` and does not call provider.
  - Progress: 2026-06-15 Codex covered routing denial with `ReservationDenied` before `EndpointInventory`.
- [~] Proxy request with no identity during reservation returns `401` and does not call provider.
  - Progress: 2026-06-15 Codex covered anonymous model-load denial; full HTTP proxy no-identity integration test remains open.
- [ ] Proxy request by admin outsider returns `403`.
  - Progress:
- [ ] Proxy request by listed participant but denied by model access returns model access denial, not success.
  - Progress:
- [ ] List models path honors active reservation.
  - Progress:
- [ ] Embeddings path honors active reservation.
  - Progress:
- [~] Chat/completions path honors active reservation.
  - Progress: 2026-06-15 Codex routing tests exercise the `/api/chat` path; HTTP proxy integration remains open.
- [x] Model load path honors active reservation.
  - Progress: 2026-06-15 Codex covered anonymous denial and participant allow in `ModelLoadServiceTests.LoadVirtualModelRunnerAsync_WithActiveReservation_RequiresParticipantIdentity`.
- [ ] VMR validation or explain path reports reservation state correctly.
  - Progress:
- [x] Drain window denies nonparticipant before start and avoids provider call.
  - Progress: 2026-06-15 Codex covered routing denial with `ReservationDrainDenied` before endpoint inventory.
- [ ] At `EndUtc`, outsider request is no longer reservation denied.
  - Progress:
- [ ] Concurrent requests near boundary produce deterministic results based on evaluated `nowUtc`.
  - Progress:
- [ ] Reservation CRUD invalidates any cache before next request.
  - Progress:
- [ ] Cache, if used, expires at reservation transitions.
  - Progress:

### Observability Tests

- [~] Reservation denial writes structured log with reason code.
  - Progress: 2026-06-15 Codex logs runtime outsider denials with `ReservationDenied` or `ReservationDrainDenied`; log capture assertions remain open.
- [ ] Reservation auth-required denial writes structured log with reason code.
  - Progress: Auth-required routing response exists; dedicated structured log and log capture assertion remain open.
- [~] Reservation conflict fail-closed writes structured error log.
  - Progress: 2026-06-15 Codex logs reservation conflicts with tenant/VMR/count and fails closed; log capture assertion and severity review remain open.
- [~] Logs do not contain bearer token, credential secret, or provider secret.
  - Progress: 2026-06-15 Codex logs ids and reason codes only for reservation denials; explicit redaction tests remain open.
- [x] Request history entry is created for reservation denial.
  - Progress: 2026-06-15 Codex covered by routing/history integration and `RequestHistory_ReservationFields_RoundTripFilterAndAnalytics`.
- [x] Request history entry includes reservation id/name/reason/window.
  - Progress: 2026-06-15 Codex covered by model/detail unit tests and database round-trip tests.
- [x] Request history filters find denials by reservation id.
  - Progress: 2026-06-15 Codex covered by `RequestHistory_ReservationFields_RoundTripFilterAndAnalytics`.
- [x] Request history filters find denials by reason code.
  - Progress: 2026-06-15 Codex covered by `RequestHistory_ReservationFields_RoundTripFilterAndAnalytics`.
- [x] Analytics event is captured for reservation denial.
  - Progress: 2026-06-15 Codex covered by request analytics service and database integration tests.
- [~] Analytics query groups reservation denials by VMR.
  - Progress: 2026-06-15 Codex supports VMR filtering and overall reservation-denial counts; explicit grouped-by-VMR reservation-denial output remains open.
- [x] Analytics query groups reservation denials by reservation.
  - Progress: 2026-06-15 Codex added `ReservationDenialCounts` and service tests.
- [~] Analytics query groups reservation denials by reason.
  - Progress: 2026-06-15 Codex supports reservation reason filters and denial reason counts; explicit reservation-denial-by-reason output remains open.
- [ ] Metrics counter increments for `ReservationDenied`.
  - Progress:
- [ ] Metrics counter increments for `ReservationDrainDenied`.
  - Progress:
- [ ] Metrics counter or log captures `ReservationConflict`.
  - Progress:

### Dashboard Positive Tests

- [~] Reservations nav item loads.
  - Progress: 2026-06-15 Codex added route and sidebar item and dashboard build passed; automated navigation test remains open.
- [ ] Empty state appears when no reservations exist.
  - Progress:
- [~] List shows active, upcoming, past, and inactive reservations.
  - Progress: 2026-06-15 Codex added state rendering for inactive/past/active/upcoming/drain-soon; automated seeded UI test remains open.
- [~] Filters apply by VMR.
  - Progress: 2026-06-15 Codex added VMR filter in the Reservations view; automated UI test remains open.
- [~] Filters apply by state.
  - Progress: 2026-06-15 Codex added state filter in the Reservations view; automated UI test remains open.
- [ ] Filters apply by subject.
  - Progress:
- [~] Create form loads users and credentials.
  - Progress: 2026-06-15 Codex added user and credential loading/selection in the create modal; automated UI test remains open.
- [~] Create form saves valid reservation.
  - Progress: 2026-06-15 Codex wired create submit to reservation API; automated UI test remains open.
- [x] Create from VMR preselects VMR.
  - Progress: 2026-06-15 Codex added VMR row Reservations action and `/reservations?vmrId=...` filter/create preselection.
- [ ] Participant picker supports user search.
  - Progress:
- [ ] Participant picker supports credential search.
  - Progress:
- [ ] Participant picker shows credential owner.
  - Progress:
- [~] Detail panel shows participants and UTC window.
  - Progress: 2026-06-15 Codex shows participants and UTC window in the reservations table/edit flow and VMR effective-config detail. Dedicated standalone reservation detail panel remains open.
- [~] Edit form updates future reservation.
  - Progress: 2026-06-15 Codex wired edit submit to reservation API; automated UI test remains open.
- [~] Cancel flow deactivates reservation after confirmation.
  - Progress: 2026-06-15 Codex added deactivate confirmation modal and API call; automated UI test remains open.
- [~] VMR list badge updates after create and cancel.
  - Progress: 2026-06-15 Codex added VMR list reservation badges sourced from reservation reloads; automated create/cancel UI test remains open.
- [~] Request history detail links to reservation details.
  - Progress: 2026-06-15 Codex added request-history reservation fields and copyable reservation id; direct navigation link to reservation detail remains open.
- [x] Analytics page can filter by reservation id.
  - Progress: 2026-06-15 Codex added Analytics workspace reservation id, decision, and reason filters with URL and saved-report persistence.
- [ ] API Explorer shows reservation-aware warning for selected active VMR.
  - Progress:

### Dashboard Negative And Edge Tests

- [~] Create form rejects missing VMR.
  - Progress: 2026-06-15 Codex added client-side validation; automated UI test remains open.
- [~] Create form rejects missing name if name is required.
  - Progress: 2026-06-15 Codex added client-side validation; automated UI test remains open.
- [~] Create form rejects empty participants.
  - Progress: 2026-06-15 Codex added client-side validation; automated UI test remains open.
- [~] Create form rejects invalid time range.
  - Progress: 2026-06-15 Codex relies on backend validation and surfaced validation result; explicit client-side invalid-order check/automated UI test remains open.
- [~] Create form shows overlap conflict from validation endpoint.
  - Progress: 2026-06-15 Codex surfaces validation endpoint errors in the modal; automated UI test remains open.
- [ ] Create form shows cross-tenant subject error from API.
  - Progress:
- [ ] Edit form handles reservation deleted by another admin.
  - Progress:
- [ ] Save handles `409` conflict created by another admin.
  - Progress:
- [ ] Save handles `401` session expiration.
  - Progress:
- [ ] Save handles `403` permission loss.
  - Progress:
- [ ] Save handles network failure with retry-safe messaging.
  - Progress:
- [ ] Cancel confirmation cannot accidentally cancel wrong reservation after list refresh.
  - Progress:
- [ ] Long VMR names do not overflow.
  - Progress:
- [ ] Long user emails do not overflow.
  - Progress:
- [ ] Long credential names do not overflow.
  - Progress:
- [ ] Hundreds of participants remain manageable.
  - Progress:
- [ ] Thousands of users/credentials require search or pagination, not a giant uncontrolled dropdown.
  - Progress:
- [ ] Timezone changes do not alter stored UTC window unexpectedly.
  - Progress:
- [ ] RTL and pseudo-locale layouts remain usable.
  - Progress:
- [ ] Keyboard-only create/edit/cancel path works.
  - Progress:
- [ ] Screen reader announces validation errors and conflict summaries.
  - Progress:

### SDK Tests

- [~] C# create reservation serializes body and bearer token.
  - Progress: 2026-06-15 Codex added body/route tests for create; reservation-specific bearer assertion remains open.
- [x] C# list reservation serializes filters.
  - Progress: 2026-06-15 Codex covered in `ReservationMethods_UseExpectedRoutesAndTenantScope`.
- [x] C# update reservation serializes body.
  - Progress: 2026-06-15 Codex covered in `ReservationMethods_UseExpectedRoutesAndTenantScope`.
- [x] C# delete/deactivate reservation serializes tenant id.
  - Progress: 2026-06-15 Codex covered in `ReservationMethods_UseExpectedRoutesAndTenantScope`.
- [x] C# effective access serializes query.
  - Progress: 2026-06-15 Codex covered in `ReservationMethods_UseExpectedRoutesAndTenantScope`.
- [ ] C# cancellation token is propagated.
  - Progress:
- [ ] C# API errors throw expected exception.
  - Progress:
- [x] JavaScript methods build expected URLs and methods.
  - Progress: 2026-06-15 Codex covered in `builds virtual model runner reservation management requests`.
- [x] JavaScript methods serialize filters and bodies.
  - Progress: 2026-06-15 Codex covered in `builds virtual model runner reservation management requests`.
- [ ] JavaScript methods surface API errors consistently.
  - Progress:
- [x] Python methods build expected URLs and methods.
  - Progress: 2026-06-15 Codex covered in `test_virtual_model_runner_reservation_management_builds_requests`.
- [x] Python methods serialize filters and bodies.
  - Progress: 2026-06-15 Codex covered in `test_virtual_model_runner_reservation_management_builds_requests`.
- [ ] Python methods surface API errors consistently.
  - Progress:

### Postman Tests

- [ ] Create user-only reservation request succeeds.
  - Progress:
- [ ] Create credential-only reservation request succeeds.
  - Progress:
- [~] Create mixed reservation request succeeds.
  - Progress: 2026-06-15 Codex added mixed-subject Postman request body; live collection smoke test remains open.
- [~] Validate overlap request returns conflict.
  - Progress: 2026-06-15 Codex added overlap Postman negative example and backend tests; live Postman smoke remains open.
- [~] Effective access participant request returns allow.
  - Progress: 2026-06-15 Codex added effective-access Postman request and service tests; live Postman smoke remains open.
- [~] Effective access outsider request returns deny.
  - Progress: 2026-06-15 Codex added outsider effective-access Postman negative example and service tests; live Postman smoke remains open.
- [ ] Proxy outsider request during reservation returns `403`.
  - Progress:
- [ ] Proxy participant request during reservation succeeds or reaches model access result.
  - Progress:
- [~] Collection variables and examples are documented.
  - Progress: 2026-06-15 Codex added collection variables plus positive, overlap-negative, outsider-negative, and analytics reservation-filter examples; live collection smoke test remains open.

### Security And Abuse Tests

- [ ] User from tenant A cannot create reservation for tenant B VMR.
  - Progress:
- [ ] User from tenant A cannot add tenant B user subject.
  - Progress:
- [ ] User from tenant A cannot add tenant B credential subject.
  - Progress:
- [~] Outsider cannot infer participant secret values from denial response.
  - Progress: 2026-06-15 Codex denial responses use reservation reason/id/window fields, not secrets; explicit negative HTTP test remains open.
- [~] Denial response does not reveal credential bearer token.
  - Progress: 2026-06-15 Codex reservation response/history/analytics models do not include bearer token fields; explicit negative HTTP test remains open.
- [~] Logs do not reveal secrets.
  - Progress: 2026-06-15 Codex runtime reservation logs contain ids/reason/window only; log capture redaction test remains open.
- [~] Analytics do not reveal secrets.
  - Progress: 2026-06-15 Codex analytics reservation dimensions contain ids/names/reasons/windows only; explicit privacy assertion remains open.
- [ ] SQL injection strings in name/description/metadata do not affect DB behavior.
  - Progress:
- [ ] HTML/script strings in name/description render safely in dashboard.
  - Progress:
- [ ] Permission changes take effect for reservation management.
  - Progress:
- [ ] Deactivated users or credentials stop satisfying future reservation checks.
  - Progress:
- [ ] Credential owner changes, if supported, affect evaluation according to documented policy.
  - Progress:

### Performance And Reliability Tests

- [ ] Active reservation lookup adds acceptable latency at p50, p95, and p99.
  - Progress:
- [ ] Reservation lookup remains acceptable with many reservations per tenant.
  - Progress:
- [ ] Reservation list pagination performs with large tenants.
  - Progress:
- [ ] Participant picker performs with large user and credential sets.
  - Progress:
- [ ] Concurrent creates for overlapping windows cannot both succeed.
  - Progress:
- [ ] Concurrent update and proxy request has deterministic behavior based on committed data.
  - Progress:
- [ ] Database unavailable during reservation evaluation fails safe according to product decision.
  - Progress:
- [ ] Cache invalidation works across reservation create, update, delete/deactivate.
  - Progress:
- [ ] Multi-node clock skew risk is documented and operational guidance exists.
  - Progress:

### Documentation Tests

- [x] REST examples match actual API request and response shapes.
  - Progress: 2026-06-15 Codex updated REST reservation, backup/restore, request-history, and Analytics workspace shapes after implementation and validated builds/tests.
- [~] SDK examples compile or run where practical.
  - Progress: 2026-06-15 Codex added examples and ran SDK tests; README snippets themselves were not executed as standalone programs.
- [x] Postman examples match docs.
  - Progress: 2026-06-15 Codex updated collection examples to match REST docs and parsed JSON successfully.
- [x] Troubleshooting guide maps every reason code to operator actions.
  - Progress: 2026-06-15 Codex documented `ReservationDenied`, `ReservationDrainDenied`, `ReservationAuthenticationRequired`, and `ReservationConflict` in `MANAGING_RESERVATIONS.md`.
- [x] UI docs match current dashboard labels and workflows.
  - Progress: 2026-06-15 Codex updated `README.md`, `MANAGING_RESERVATIONS.md`, and `TESTING.md` for VMR Reservations, Request History, and Analytics dashboard flows.
- [x] Changelog names migration and compatibility impacts.
  - Progress: 2026-06-15 Codex updated `CHANGELOG.md` and REST backup schema notes for reservation tables and backup schema version `1.3`.

## Artifact Plan

- [x] ADR: `docs/adr/0003-virtual-model-runner-reservations.md`
  - Owner: Principal Architect
  - Progress: 2026-06-15 Codex added ADR 0003.
- [ ] Product requirements: update this file or add PRD under docs if repo convention exists.
  - Owner: Product Manager
  - Progress:
- [ ] Security review: attached to PR or docs/security if repo convention exists.
  - Owner: Security Engineer
  - Progress:
- [ ] Data model specification: linked from ADR and DB PR.
  - Owner: Data Engineer, DBA
  - Progress:
- [ ] UX spec and screenshots: linked from dashboard PR.
  - Owner: UX Designer
  - Progress:
- [x] Test plan: this file plus `TESTING.md` updates.
  - Owner: QA Engineer
  - Progress: 2026-06-15 Codex updated this plan and added the VMR Reservations Release Gate to `TESTING.md`.
- [x] API docs: `REST_API.md`.
  - Owner: Documentation Engineer
  - Progress: 2026-06-15 Codex updated reservation, request-history, backup/restore, and Analytics workspace API docs.
- [x] SDK docs: SDK README files.
  - Owner: Developer Relations
  - Progress: 2026-06-15 Codex added reservation examples to C#, JavaScript, and Python SDK READMEs.
- [x] Postman collection: `Conductor.postman_collection.json`.
  - Owner: Developer Relations
  - Progress: 2026-06-15 Codex added positive and negative reservation examples and parsed the collection successfully.
- [x] Release notes: `CHANGELOG.md`.
  - Owner: Technical Program Manager
  - Progress: 2026-06-15 Codex updated changelog reservation entry.
- [x] Support runbook: docs or support location chosen by team.
  - Owner: Technical Support Engineer
  - Progress: 2026-06-15 Codex added `MANAGING_RESERVATIONS.md`.
- [ ] Compliance evidence checklist.
  - Owner: Compliance Officer
  - Progress:

## Launch Readiness Checklist

- [ ] ADR approved by Principal Architect, Security, SRE, DBA, Product.
  - Progress:
- [x] Backend builds with no style regressions.
  - Progress: 2026-06-15 `dotnet build src\Conductor.sln` passed with 0 warnings and 0 errors.
- [ ] Provider matrix tests pass for SQLite, MySQL, PostgreSQL, SQL Server or documented supported subset.
  - Progress:
- [ ] Routing integration tests prove provider calls are blocked for outsiders.
  - Progress:
- [x] Request history and analytics tests pass.
  - Progress: 2026-06-15 `dotnet test src\Test.Xunit\Test.Xunit.csproj --no-build` passed 1049 tests.
- [ ] Metrics and log tests pass with secret-safety assertions.
  - Progress:
- [~] Dashboard tests pass for create/edit/cancel/list/detail/history/analytics flows.
  - Progress: 2026-06-15 `npm.cmd run build --prefix dashboard` passed with the existing Vite large-chunk warning. Automated component/E2E workflow tests remain open.
- [ ] Dashboard responsive screenshots reviewed for desktop, tablet, and mobile.
  - Progress:
- [ ] Dashboard i18n and pseudo-locale checks pass.
  - Progress:
- [ ] Keyboard and screen reader review complete.
  - Progress:
- [x] C# SDK tests pass.
  - Progress: 2026-06-15 `dotnet test sdk\csharp\Conductor.Sdk.Tests\Conductor.Sdk.Tests.csproj` passed 6 tests.
- [x] JavaScript SDK tests pass.
  - Progress: 2026-06-15 `npm.cmd test --prefix sdk\javascript` passed 13 tests.
- [x] Python SDK tests pass.
  - Progress: 2026-06-15 `$env:PYTHONPATH='src'; python -m pytest tests` from `sdk\python` passed 13 tests.
- [~] Postman smoke run passes.
  - Progress: 2026-06-15 `Conductor.postman_collection.json` parsed as valid JSON; live collection smoke against a running environment remains open.
- [x] REST API docs reviewed.
  - Progress: 2026-06-15 Codex updated and cross-checked REST reservation, request-history, analytics, and backup/restore docs against implementation.
- [x] Admin guide reviewed.
  - Progress: 2026-06-15 Codex added and reviewed `MANAGING_RESERVATIONS.md`.
- [x] Support runbook reviewed.
  - Progress: 2026-06-15 Codex added request-denial troubleshooting and support runbook steps to `MANAGING_RESERVATIONS.md`.
- [ ] Security review signed off.
  - Progress:
- [ ] Compliance review signed off.
  - Progress:
- [ ] Migration and rollback plan reviewed.
  - Progress:
- [ ] Staging rollout completed.
  - Progress:
- [ ] Release notes approved.
  - Progress:

## Final Recommendation

Build reservations as a first-class VMR admission-control product area. The implementation should begin with a short decision phase for permissions, retention, and drain defaults, then land persistence and service evaluation before touching routing. Routing enforcement is the critical path: no dashboard, SDK, or documentation work should be considered complete until an outsider request during an active reservation is denied before provider access and that denial is visible in logs, request history, analytics, and metrics.

The first release should stay intentionally narrow on scheduling semantics: one VMR, one non-recurring UTC window, explicit user and credential participants, no implicit admin bypass, and no in-flight cancellation. That scope satisfies the core requirement while leaving recurrence, groups, break-glass override, and forced cancellation as deliberate follow-on features rather than hidden complexity in the initial launch.
