# Managing VMR Reservations

VMR reservations let operators schedule exclusive access windows for a Virtual Model Runner (VMR). During a reservation window, the VMR is blocked from general use and only the selected reservation participants can be admitted. Outside a reservation window, the VMR keeps its normal on-demand behavior.

Reservations are an admission gate. They do not replace tenant isolation, route authorization, ACLs, model access policies, endpoint health, load balancing, or provider-side checks.

## When To Use Reservations

Use a reservation when a VMR needs predictable exclusive access for a specific period:

- Customer demos that need protected capacity.
- Benchmarking or model evaluation runs where unrelated traffic would distort results.
- Maintenance windows where only a release engineer or automation credential should use the VMR.
- Incident response where access must be narrowed temporarily without changing the long-lived ACL/model-access policy.
- Scheduled partner or tenant workloads that should not compete with general traffic.

Do not use reservations for permanent authorization policy. Use credentials, tenant permissions, and model access policies for standing access rules.

## Core Concepts

- Reservation: A tenant-scoped record with `StartUtc`, `EndUtc`, a target `VirtualModelRunnerId`, an optional `AdmissionDrainLeadMs`, and one or more subjects.
- Participant: A user or credential explicitly listed on the reservation.
- Active window: `StartUtc <= nowUtc < EndUtc`.
- Drain window: Optional pre-start interval. If `AdmissionDrainLeadMs` is set, nonparticipants are denied before `StartUtc` so long-running requests do not enter the reserved period.
- Soft deactivate: `DELETE /v1.0/vmrreservations/{id}` marks a reservation inactive for audit retention instead of removing it.

Times are stored and evaluated in UTC. The dashboard may show local input controls, but saved reservation windows are UTC.

## Who Is Allowed During A Reservation

During the active or drain window:

- A listed credential subject admits only that credential.
- A listed user subject admits that user.
- A listed user subject also admits credentials owned by that user when Conductor can resolve the credential owner.
- A system admin is not admitted unless that admin identity, or the credential being used, is explicitly listed.
- If no usable request identity is present, the request is denied with `ReservationAuthenticationRequired`.

After a participant passes the reservation gate, all normal checks still run. A listed participant can still be denied by model access policy, ACLs, tenant mismatch, inactive VMRs, endpoint inventory, health, load balancing, or provider failure.

## Dashboard Workflow

Open **VMR Reservations** in the dashboard.

The page supports:

- Refreshing the reservations table with the icon button in the page header.
- Listing reservations across the tenants visible to the current operator.
- Filtering by state and VMR.
- Creating a reservation globally or from a VMR row action with the VMR preselected.
- Validating a draft before save.
- Editing a reservation.
- Deactivating a reservation.
- Evaluating effective access for a candidate user, credential, and time.
- Seeing reservation state badges on the VMR list and reservation context in VMR configuration detail.
- Filtering request history and analytics by reservation id, decision, and reason code.

Create flow:

1. Choose the tenant and VMR.
2. Enter a reservation name and optional description.
3. Choose start and end times.
4. Set `AdmissionDrainLeadMs` if nonparticipant requests should stop before the window starts.
5. Select at least one user or credential participant.
6. Click **Validate** to catch invalid subjects, time errors, and overlap conflicts.
7. Save the reservation.

Evaluate flow:

1. Click the evaluate-access action from the page header or a reservation row.
2. Choose the VMR.
3. Select a candidate user and/or credential.
4. Choose a time.
5. Review `Allowed`, `Decision`, `ReasonCode`, and reservation details.

## REST API

Main routes:

```text
GET    /v1.0/vmrreservations
POST   /v1.0/vmrreservations
POST   /v1.0/vmrreservations/validate
GET    /v1.0/vmrreservations/{id}
PUT    /v1.0/vmrreservations/{id}
DELETE /v1.0/vmrreservations/{id}
GET    /v1.0/virtualmodelrunners/{id}/reservations
GET    /v1.0/virtualmodelrunners/{id}/reservation-effective
```

Tenant users and tenant admins can usually omit `tenantId`; Conductor resolves their tenant from authentication. System admins can list all reservations with:

```text
GET /v1.0/vmrreservations?maxResults=1000
```

For read, deactivate, and effective-access requests, system admins should pass `tenantId` unless the route explicitly documents global behavior.

Example create request:

```json
{
  "TenantId": "ten_xxx",
  "VirtualModelRunnerId": "vmr_xxx",
  "Name": "Customer demo window",
  "Description": "Exclusive access for the evaluation team",
  "StartUtc": "2026-06-16T17:00:00Z",
  "EndUtc": "2026-06-16T19:00:00Z",
  "AdmissionDrainLeadMs": 300000,
  "Active": true,
  "Subjects": [
    {
      "SubjectType": "User",
      "SubjectId": "usr_xxx"
    },
    {
      "SubjectType": "Credential",
      "SubjectId": "cred_xxx"
    }
  ]
}
```

Example effective access request:

```text
GET /v1.0/virtualmodelrunners/vmr_xxx/reservation-effective?tenantId=ten_xxx&userId=usr_xxx&credentialId=cred_xxx&atUtc=2026-06-16T18:00:00Z
```

Common reservation denial reason codes:

- `ReservationAuthenticationRequired`: an active reservation exists, but the request has no usable user or credential identity.
- `ReservationDenied`: an active reservation exists, and the request identity is not listed.
- `ReservationDrainDenied`: the request is in the pre-start drain window, and the request identity is not listed.
- `ReservationConflict`: multiple active reservations unexpectedly overlap at runtime, so Conductor fails closed.

## Enforcement Order

For proxy traffic, Conductor evaluates reservations before selecting endpoint inventory or making provider calls:

1. Resolve VMR and tenant context.
2. Resolve request identity from bearer auth, credential auth, or supported provider-style credential headers.
3. Look up active or drain-window reservations for the tenant and VMR.
4. If there is no reservation, continue normal routing.
5. If exactly one reservation applies, compare the request identity to reservation subjects.
6. Deny outsiders immediately with the appropriate reservation reason.
7. Let listed participants continue through request type resolution, ACL/model access policy, endpoint inventory, session affinity, load balancing, and provider calls.
8. If more than one active reservation applies, deny with `ReservationConflict`.

The VMR model-load management path also evaluates the reservation gate when loading through `/v1.0/virtualmodelrunners/{id}/load-model`.

## Observability

Reservation denials are intended to be diagnosable without reading provider logs.

Server logs include the reservation denial reason plus tenant, VMR, reservation, user id, credential id, and UTC window. Logs must not include bearer tokens, credential secrets, provider API keys, or request bodies containing secrets.

Request history entries include nullable reservation fields:

- `ReservationGuid`
- `ReservationName`
- `ReservationDecision`
- `ReservationReasonCode`
- `ReservationWindowStartUtc`
- `ReservationWindowEndUtc`

Request history search, summary, and analytics overview filters include:

- `reservationGuid`
- `reservationDecision`
- `reservationReasonCode`

The Analytics workspace includes reservation denial totals, reservation-denial counts by reservation id, saved-report support for reservation filters, and filters for reservation id, decision, and reason code.

## Data Model And Internals

Reservation data is stored in two tables:

- `virtualmodelrunnerreservations`
- `virtualmodelrunnerreservationsubjects`

Reservation IDs use the `vmrr_` prefix. Subject IDs use the `vmrrs_` prefix.

Important indexes support:

- Active lookup by tenant, VMR, start, end, and active state.
- Subject enumeration by reservation.
- Subject searches by tenant, subject type, and subject id.
- Duplicate prevention for `(reservationid, subjecttype, subjectid)`.

The core implementation is split across:

- `VirtualModelRunnerReservation` and `VirtualModelRunnerReservationSubject` models.
- `IVirtualModelRunnerReservationMethods` and provider-neutral reservation persistence.
- Provider schema bootstrapping/migrations for SQLite, MySQL, PostgreSQL, and SQL Server.
- `VirtualModelRunnerReservationService` validation and evaluation.
- `VirtualModelRunnerReservationController` and `VirtualModelRunnerReservationRouteModule`.
- `RoutingDecisionService` reservation admission gate.
- `ModelLoadService` VMR model-load admission gate.
- Request history and analytics capture for reservation fields.
- Backup/restore package export, validation, and restore handling for reservation records and subjects.

## Validation Rules

Create, update, and validate enforce:

- `TenantId` is required for management requests.
- `VirtualModelRunnerId` must reference an active VMR in the same tenant.
- `StartUtc` must be before `EndUtc`.
- At least one subject is required.
- User subjects must exist, be active, and belong to the reservation tenant.
- Credential subjects must exist, be active, and belong to the reservation tenant.
- Duplicate subjects are invalid.
- Active reservations cannot overlap the same tenant and VMR.
- `AdmissionDrainLeadMs` must be nonnegative and within the configured maximum.

## Troubleshooting

If the dashboard shows `TenantId is required` while listing reservations, refresh the deployment after rebuilding. The list route supports system-admin global listing, and the dashboard also sends tenant-scoped list requests after loading tenant metadata.

If a request is denied unexpectedly:

1. Check request history for `ReservationReasonCode`.
2. Confirm the request timestamp falls inside `ReservationWindowStartUtc` and `ReservationWindowEndUtc`, or inside the drain lead time.
3. Open the reservation and verify the user or credential is listed.
4. If a credential should be admitted by a listed user, verify the credential owner is set and active.
5. Use the dashboard effective-access evaluator for the same VMR, identity, and time.
6. If the evaluator allows the identity, check model access policy and endpoint routing decisions next.
7. If the reason is `ReservationConflict`, list active reservations for that VMR and deactivate or adjust the overlap.

If a listed participant is still denied:

- Check model access policy decisions.
- Check whether the VMR and endpoints are active.
- Check request type permissions.
- Check tenant mismatch between the credential/user and VMR.

## Current Product Boundaries

The first reservation implementation is intentionally single-window scheduling:

- No recurring reservations.
- No group subjects yet.
- No implicit admin bypass.
- No forced cancellation of in-flight streams that were admitted before a window started.
- Soft deactivate is used instead of hard delete.

Operational alert dashboards, full provider runtime matrices beyond the shared test suite, group subjects, recurring reservations, and formal accessibility/i18n signoff remain tracked follow-up work.
