# Virtual Model Runner Reservations

This note sketches what would be involved in adding reservations and scheduling to Conductor's Virtual Model Runners (VMRs). The goal is to preserve the current on-demand access model for any caller allowed by the VMR's ACL/model access policy, while making a VMR exclusive to a selected set of users and credentials during a scheduled window.

## Existing Fit

Conductor already has most of the enforcement and observability shape this needs:

- VMRs are the proxy boundary. `ProxyController` resolves a VMR from the request path, creates request history, calls `RoutingDecisionService`, and only forwards upstream after a successful routing decision.
- Model access policies already evaluate tenant, user, credential, VMR, model, and action before endpoint selection. The reservation gate should be a separate conjunctive gate, not a replacement for ACLs.
- Request history already records user ID, credential ID, VMR ID, routing outcome, denial reason code, model access fields, and routing explanations.
- Request analytics and observability already count denied requests by `DenialReasonCode` through request history and `conductor_denials_total{reason=...}`.
- The management plane has repeatable CRUD patterns: core model, database interface, provider implementations, controller, route module with OpenAPI metadata, dashboard methods, SDK helpers, tests, and docs.

The recommended shape is a first-class tenant-scoped `VirtualModelRunnerReservation` resource, enforced in `RoutingDecisionService` before endpoint selection and before any provider request is sent.

## Product Semantics

A reservation is an exclusive-use schedule for one VMR.

- Time windows are UTC and use `[StartUtc, EndUtc)`: inclusive start, exclusive end.
- If no active reservation applies, traffic behaves exactly as it does today: normal request-type gates, model resolution, model access policy, routing, health, session affinity, and load balancing.
- If an active reservation applies, the caller must be authenticated as one of the reservation's allowed subjects.
- Allowed subjects should initially be direct `User` and `Credential` references. If a credential is presented and its `UserId` matches an allowed user subject, the request is allowed by the reservation gate.
- Passing the reservation gate does not grant model access. The caller must still pass the existing VMR ACL/model access policy.
- Administrators should not bypass reservations implicitly. If an admin needs proxy access during a reservation, list the admin's user or credential on the reservation. This matches the requirement that only selected identities can use the VMR during the window.
- VMR management CRUD can remain available to authorized administrators. "Use" should cover proxied inference, embeddings, list/show model operations, proxied model-management operations, and VMR model load/verification workflows.

## Hard Boundary For Long Requests

There is a subtle exclusivity issue: admission-time enforcement alone prevents new non-reserved requests during the window, but it does not stop a streaming or slow request admitted before `StartUtc` from continuing into the reservation.

To provide a stronger guarantee, reservations should include one of these behaviors:

1. `AdmissionDrainLeadMs`: deny non-participant requests in `[StartUtc - AdmissionDrainLeadMs, StartUtc)`. A practical default is the VMR timeout, since proxied upstream requests are already bounded by VMR/endpoint timeout. This is the simplest reliable approach.
2. `CancelNonParticipantAtStart`: for requests admitted before the window, attach a cancellation token that fires at `StartUtc` if the caller is not a reservation participant. This is more complex and can produce partial streaming responses.

The first release should implement the drain lead and make the behavior visible in validation and the dashboard. Use `ReservationPending` or `ReservationDrainDenied` as the denial code for pre-start drain denials, and `ReservationDenied` for active-window denials.

## Core Data Model

Add a reservation resource and subject rows rather than embedding schedules directly on VMRs. This keeps schedule ownership separate from routing configuration and mirrors the existing policy/rule pattern.

```csharp
public class VirtualModelRunnerReservation
{
    public string Id { get; set; }              // res_...
    public string TenantId { get; set; }
    public string VirtualModelRunnerId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int AdmissionDrainLeadMs { get; set; }
    public bool Active { get; set; }
    public string CreatedByUserId { get; set; }
    public string CreatedByCredentialId { get; set; }
    public List<VirtualModelRunnerReservationSubject> Subjects { get; set; }
    public List<string> Labels { get; set; }
    public Dictionary<string, string> Tags { get; set; }
    public object Metadata { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastUpdateUtc { get; set; }
}

public class VirtualModelRunnerReservationSubject
{
    public string Id { get; set; }              // ress_...
    public string TenantId { get; set; }
    public string ReservationId { get; set; }
    public ReservationSubjectTypeEnum SubjectType { get; set; } // User, Credential
    public string SubjectId { get; set; }
    public DateTime CreatedUtc { get; set; }
}
```

Suggested tables:

- `virtualmodelrunnerreservations`: `id`, `tenantid`, `vmrid`, `name`, `description`, `startutc`, `endutc`, `admissiondrainleadms`, `active`, `createdbyuserid`, `createdbycredentialid`, `labels`, `tags`, `metadata`, `createdutc`, `lastupdateutc`
- `virtualmodelrunnerreservationsubjects`: `id`, `tenantid`, `reservationid`, `subjecttype`, `subjectid`, `createdutc`

Indexes:

- `(tenantid, vmrid, active, startutc, endutc)` for active-window lookup
- `(tenantid, reservationid)` for subject hydration
- `(tenantid, subjecttype, subjectid)` for "my reservations" and audit views
- Optional uniqueness on `(reservationid, subjecttype, subjectid)`

Validation:

- `TenantId`, `VirtualModelRunnerId`, `Name`, `StartUtc`, and `EndUtc` are required.
- `StartUtc < EndUtc`.
- Referenced VMR, users, and credentials must exist in the same tenant and be active.
- At least one subject is required for an active reservation.
- Reject overlapping active reservations for the same VMR by default. Overlap rejection prevents accidental privilege broadening.
- `AdmissionDrainLeadMs` should clamp to a bounded range, for example `0` to `86400000`.

## Reservation Evaluation

Add `IReservationMethods` and `VirtualModelRunnerReservationMethods` for every database provider, then a `VirtualModelRunnerReservationService` with these responsibilities:

- Load active or draining reservations for `(tenantId, vmrId, nowUtc)`.
- Validate reservation payloads and references.
- Detect overlapping active reservations.
- Evaluate whether the current request identity is a participant.
- Return a structured result for routing, history, analytics, and explain views.

Suggested result shape:

```csharp
public class ReservationEvaluationResult
{
    public bool HasReservation { get; set; }
    public bool InActiveWindow { get; set; }
    public bool InDrainWindow { get; set; }
    public bool Allowed { get; set; }
    public string ReservationId { get; set; }
    public string ReservationName { get; set; }
    public string ReasonCode { get; set; }      // ReservationAllowed, ReservationDenied, ReservationPending, ReservationConflict
    public string ReasonText { get; set; }
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
}
```

Evaluation rules:

1. If no active or drain-window reservation exists, return `HasReservation=false` and allow normal routing.
2. If multiple active reservations exist because of data corruption or a race, fail closed with `ReservationConflict` and log an error. Creation/update validation should make this rare.
3. If a reservation applies and no user or credential identity is available, deny with `401` and `ReservationAuthenticationRequired`.
4. If the authenticated credential ID matches a credential subject, allow the reservation gate.
5. If the authenticated user ID matches a user subject, allow the reservation gate. This includes user identity resolved through the presented credential's `UserId`.
6. Otherwise deny with `403` and `ReservationDenied` during the active window, or `ReservationPending`/`ReservationDrainDenied` during the drain window.

## Enforcement Point

Wire the reservation service into the proxy and routing flow.

Recommended order:

1. `ProxyController` resolves the VMR by base path.
2. Determine whether the VMR has an active or drain-window reservation.
3. If a reservation applies, authenticate the proxy request even when model access control is disabled or `RequireCredentialForProxy=false`.
4. Populate `RequestContext` with user and credential IDs.
5. Create request history if enabled.
6. `RoutingDecisionService.EvaluateAsync` runs the reservation gate before provider-facing work:
   - VMR resolved
   - reservation gate
   - request-type gate
   - model resolution and mutation planning
   - model access policy
   - endpoint inventory, affinity, policy evaluation, and forwarding

This keeps reservation denials upstream-free and ensures an allowed reservation participant still has to satisfy the existing ACL/model access policy.

`RoutingDecision` should get reservation fields:

- `ReservationId`
- `ReservationName`
- `ReservationStartUtc`
- `ReservationEndUtc`
- `ReservationDecision`
- `ReservationDenialReasonCode`

The timeline should get a `ReservationGate` stage with attributes such as `ReservationId`, `StartUtc`, `EndUtc`, `CredentialId`, `UserId`, and `ReasonCode`. Do not include bearer tokens, request bodies, provider API keys, or Gemini query keys.

## API Surface

Add routes under `/v1.0/vmrreservations` or `/v1.0/virtualmodelrunnerreservations`.

Minimum CRUD and utility routes:

- `POST /v1.0/vmrreservations`
- `POST /v1.0/vmrreservations/validate`
- `GET /v1.0/vmrreservations`
- `GET /v1.0/vmrreservations/{id}`
- `PUT /v1.0/vmrreservations/{id}`
- `DELETE /v1.0/vmrreservations/{id}`
- `GET /v1.0/virtualmodelrunners/{id}/reservations`
- `GET /v1.0/virtualmodelrunners/{id}/reservation-effective?credentialId=...&userId=...&atUtc=...`

Useful list filters:

- `tenantId`
- `vmrId`
- `subjectType`
- `subjectId`
- `startsBeforeUtc`
- `endsAfterUtc`
- `activeFilter`
- `nameFilter`

The effective route should answer "would this identity be allowed by the reservation gate at this instant?" It should not replace model access effective evaluation; callers may need to run both checks or a combined routing explanation.

## Dashboard

Add a `Reservations` workspace and VMR-specific reservation views:

- Calendar/list view grouped by VMR, with active, upcoming, expired, and canceled filters.
- Create/edit reservation modal with VMR selector, UTC date/time range, optional drain lead, and subject pickers for users and credentials.
- Conflict warnings before save.
- VMR detail page badge for active/upcoming reservation and a quick link to the schedule.
- Routing explanation view should include the reservation gate stage.
- Request history filters should include reservation denial codes and, if reservation fields are persisted, reservation ID/name.
- Analytics should expose reservation denials in access/reliability views.

The dashboard should make the drain behavior explicit, since it can deny non-participants before the visible reservation start.

## Logging

Add explicit audit logs for reservation decisions. The important one is the outsider denial:

```text
[RoutingDecisionService] reservation denied tenant=ten_... vmr=vmr_... reservation=res_... user=usr_... credential=cred_... startUtc=2026-06-15T18:00:00Z endUtc=2026-06-15T20:00:00Z reason=ReservationDenied
```

Other useful logs:

```text
[RoutingDecisionService] reservation authentication required tenant=ten_... vmr=vmr_... reservation=res_... sourceIp=... reason=ReservationAuthenticationRequired
[RoutingDecisionService] reservation drain denied tenant=ten_... vmr=vmr_... reservation=res_... user=usr_... credential=cred_... reason=ReservationPending
[RoutingDecisionService] reservation conflict tenant=ten_... vmr=vmr_... activeReservations=res_1,res_2 reason=ReservationConflict
[VirtualModelRunnerReservationController] reservation created tenant=ten_... vmr=vmr_... reservation=res_... startUtc=... endUtc=... subjects=3 createdByUser=usr_...
[VirtualModelRunnerReservationController] reservation updated tenant=ten_... vmr=vmr_... reservation=res_...
[VirtualModelRunnerReservationController] reservation canceled tenant=ten_... vmr=vmr_... reservation=res_...
```

Use `Warn` for denied proxy requests and conflicts, `Info` for create/update/delete, and `Debug` for allowed reservation passes if needed. Avoid logging tokens, passwords, request bodies, or upstream provider URLs.

## Request History, Analytics, And Metrics

At minimum, reservation denials can be visible with existing fields:

- `HttpStatus = 403`
- `RoutingOutcomeCode = Denied`
- `DenialReasonCode = ReservationDenied`
- `DenialReason = Virtual model runner is reserved for selected identities.`
- `ExplanationSummary` from the routing decision message
- Request analytics event with `StageKind = denial`, `ErrorType = ReservationDenied`
- Prometheus counter `conductor_denials_total{reason="ReservationDenied"}`

For richer reporting, add reservation fields to `RequestHistoryEntry`, `RequestAnalyticsEvent`, and analytics filters:

- `ReservationGuid`
- `ReservationName`
- `ReservationDecision`
- `ReservationReasonCode`

Then add indexes and filters:

- `idx_requesthistory_reservationguid`
- `idx_requesthistory_reservationreasoncode`
- request history query params `reservationGuid` and `reservationReasonCode`
- analytics dimensions `ReservationId` and `ReservationName`
- analytics filters `ReservationIds` and `ReservationReasonCodes`

Even without new analytics columns, operators can immediately count reservation denials through `DenialReasonCode`. The richer fields are useful when multiple reservations exist on the same VMR over time.

## Caching And Clock Behavior

Reservation evaluation is time-sensitive, so a normal fixed 30-second policy cache would be risky.

Recommended cache behavior:

- Key by `(tenantId, vmrId)`.
- Cache the current active reservation plus the next transition time.
- Never cache past the next `StartUtc` or `EndUtc`.
- Invalidate on create, update, delete, and subject changes.
- Use `DateTime.UtcNow` consistently and document that multi-node deployments need synchronized clocks.

If the first implementation favors simplicity, query the database on each proxied request and add the transition-aware cache later.

## Backup, SDKs, And Postman

Reservations should be included anywhere other first-class management entities are represented:

- Backup export/import and validation.
- JavaScript dashboard API client.
- Python SDK.
- C# SDK.
- JavaScript SDK tests if that package exposes management helpers.
- Postman collection examples for create, validate, list by VMR, and effective evaluation.
- `REST_API.md`, `README.md`, and a focused operator guide if the feature ships beyond prototype.

## Tests

Focused backend tests should cover:

- Model default values and JSON round trips for reservations and subjects.
- Validation rejects missing VMR, cross-tenant subjects, inactive users/credentials, empty subject lists, invalid time ranges, and overlapping active reservations.
- Active reservation allows listed credential.
- Active reservation allows listed user via a credential owned by that user.
- Active reservation denies an authenticated nonparticipant with `403` and `ReservationDenied`.
- Active reservation with no identity returns `401` and `ReservationAuthenticationRequired`.
- Reservation participant still loses if model access policy denies the request.
- No active reservation preserves existing ACL/routing behavior.
- Drain window denies nonparticipants before `StartUtc` when configured.
- Request history records reservation denial code, status, user, credential, and routing timeline.
- Request analytics emits a denial event with `ErrorType=ReservationDenied`.
- Observability increments `conductor_denials_total` with reason `ReservationDenied`.
- Explain-routing includes `ReservationGate` evidence.
- List-model and model-management proxy requests are blocked during reservations for nonparticipants.
- VMR model-load operations honor reservations.

Release-gate checks should mirror the model access gate:

```powershell
dotnet build src/Conductor.sln --no-restore
dotnet test src/Test.Xunit/Test.Xunit.csproj --logger "console;verbosity=minimal"
dotnet test src/Test.Nunit/Test.Nunit.csproj --logger "console;verbosity=minimal"
Push-Location dashboard; npm.cmd run build; Pop-Location
Push-Location sdk\javascript; npm.cmd test; Pop-Location
Push-Location sdk\python; $env:PYTHONPATH='src'; python -m unittest discover -s tests; Pop-Location
Get-Content Conductor.postman_collection.json -Raw | ConvertFrom-Json | Out-Null
```

## Rollout Plan

1. Add core reservation models, enums, ID helpers, database tables, provider methods, and validation.
2. Add CRUD and effective-evaluation management routes.
3. Add `VirtualModelRunnerReservationService` and integrate it into proxy authentication and `RoutingDecisionService`.
4. Emit reservation timeline evidence, request history fields, denial reason codes, analytics events, and Prometheus denial counters.
5. Add dashboard reservation management and request-history filters.
6. Add SDK/Postman/docs coverage.
7. Add transition-aware caching only after the simple database-backed implementation is correct.

The smallest useful version can skip new analytics columns and rely on `DenialReasonCode=ReservationDenied`, but the enforcement gate, request history update, and denial logs should be part of the first implementation.
