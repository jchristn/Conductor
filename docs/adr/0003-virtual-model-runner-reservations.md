# ADR 0003: Virtual Model Runner Reservations

Date: 2026-06-15

Status: Accepted for the first VMR reservations slice

## Context

Conductor allows on-demand access to Virtual Model Runners (VMRs) when tenant, route, ACL, credential, model access policy, endpoint health, and load-balancing checks allow it. Operators also need temporary exclusive windows for demos, benchmarks, maintenance, partner traffic, and incident response without converting those temporary windows into long-lived access policy.

The reservation feature must block general use only during a scheduled UTC window, preserve normal on-demand access outside that window, and produce enough evidence for support and audit when a request is explicitly denied by the reservation gate.

## Decision

Add tenant-scoped VMR reservations as an admission gate before endpoint inventory and provider access.

A reservation targets one VMR, has `StartUtc`, `EndUtc`, optional `AdmissionDrainLeadMs`, an active flag, and explicit subjects. The first release supports user and credential subjects only. Recurrence, group subjects, and implicit admin bypass are deferred.

During an active or drain window:

- listed credential subjects admit only that credential
- listed user subjects admit the user and credentials owned by that user when ownership can be resolved
- unlisted users, unlisted credentials, and unlisted admins are denied
- missing identity returns an authentication-required reservation denial
- overlapping active reservations on the same tenant and VMR are invalid at write time and fail closed at runtime

Reservation allow decisions do not bypass tenant isolation, request-type authorization, ACLs, model access policy, endpoint state, session affinity, load balancing, or provider failures.

## API Shape

The management API is:

- `GET /v1.0/vmrreservations`
- `POST /v1.0/vmrreservations`
- `POST /v1.0/vmrreservations/validate`
- `GET /v1.0/vmrreservations/{id}`
- `PUT /v1.0/vmrreservations/{id}`
- `DELETE /v1.0/vmrreservations/{id}`
- `GET /v1.0/virtualmodelrunners/{id}/reservations`
- `GET /v1.0/virtualmodelrunners/{id}/reservation-effective`

`DELETE` is a soft deactivate for audit retention. System administrators may list all reservations without `tenantId`; tenant callers are scoped to their authenticated tenant. Read, delete, and effective-access calls should include `tenantId` for system-admin cross-tenant operations.

## Enforcement

Reservations are enforced in routing and VMR model-load admission. The gate runs before provider-facing calls so outsiders are denied without consuming upstream capacity or leaking provider behavior.

Stable reason codes are:

- `ReservationAuthenticationRequired`
- `ReservationDenied`
- `ReservationDrainDenied`
- `ReservationConflict`

## Observability

Runtime denials write structured server logs and request-history rows with tenant id, VMR id/name, reservation id/name, reason code, decision, UTC window, user id when available, credential id when available, and request correlation data. Logs and analytics must not contain bearer tokens, credential secrets, provider API keys, or raw secret-bearing request bodies.

Request history, request-history analytics overview, and Analytics workspace queries include reservation filters for `reservationGuid`, `reservationDecision`, and `reservationReasonCode`. The dashboard exposes reservation fields in request history detail and reservation-denial cards in Analytics.

## Backup And Restore

Backup schema version `1.3` includes VMR reservations and nested subjects. Restore validates tenant, VMR, user, credential, subject, and overlap rules before applying records and uses the existing `Skip`, `Overwrite`, or `Fail` conflict modes.

## Consequences

The first release favors explicit, auditable scheduling over enterprise scheduling breadth. Operators get predictable exclusive windows, but recurring schedules, group subjects, automatic conflict resolution, and formal alert dashboards remain future work.

Every new VMR use path should be evaluated against this ADR. A path that exposes VMR capacity or provider access must either call the reservation gate or document why it is not a VMR-use path.
