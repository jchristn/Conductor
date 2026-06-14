# ADR 0001: Model Access Policy Semantics

Status: Accepted

Date: 2026-06-13

## Context

Conductor needs tenant-scoped authorization for proxied model use without overloading credentials or virtual model runner routing settings. The feature must preserve upgrade compatibility, explain decisions, support monitor-mode rollout, and avoid forwarding denied requests to provider endpoints.

## Decision

Model access is represented by tenant-scoped `ModelAccessPolicy` resources containing ordered `ModelAccessRule` entries. The first release attaches policies to virtual model runners through `VirtualModelRunner.ModelAccessPolicyId`. Credential-attached policies remain a follow-up unless a later product decision requires them.

Rules evaluate these dimensions:

- subject: credential, credential label, user, user label, tenant, or any
- resource: model definition, model name, model label, virtual model runner, or any
- action: completions, embeddings, list-models, show-model, load-model, unload-model, or model-management

Policy evaluation uses the requested and effective model context produced before endpoint selection. Explicit denies win over allows at the same priority. Higher priority rules win before lower priority rules. Ties within the same effect use deterministic created-time and id ordering.

If no rule matches, evaluation uses the policy default decision. If no active policy applies, evaluation uses the server global default decision.

Enforcement modes are:

- `Disabled`: allow traffic and record disabled state.
- `Monitor`: allow traffic but record would-deny decisions.
- `Enforce`: block denied requests with `403 Forbidden`.

Authentication failures return `401 Unauthorized`; authenticated requests denied by model access return `403 Forbidden`. Management routes for policies require tenant-admin access. Admin management access does not imply proxy model access unless the explicit administrator bypass setting is enabled.

List-models responses are controlled by `ModelAccessControl.ListModelsBehavior`:

- `Filter`: remove inaccessible models from successful upstream list responses.
- `Synthesize`: return provider-shaped model lists from allowed active model definitions attached to the VMR.
- `RawPassThrough`: return upstream list responses unchanged.

The default rollout path is compatible: new installs and upgraded installs default to non-blocking behavior until operators explicitly enable enforcement. Operators should enable policy creation, attach policies to selected VMRs, run monitor mode, inspect would-deny history and analytics, then promote to enforce mode.

Deleting an attached policy is blocked by default. Callers may pass `forceDetach=true` to detach referencing VMRs before deletion.

## Consequences

Routing evaluates model access after request/model resolution and before endpoint inventory, session affinity, load balancing, and provider proxying. Denied requests do not select endpoints or call providers.

Request history, routing explanations, analytics, and audit logs include policy id, rule id, decision, mode, denial code, and would-deny state where applicable. Logs intentionally avoid bearer tokens, API keys, request bodies, provider secrets, and provider URLs.

Backup and restore include model access policies and rules. Restore must continue to reject or safely remap tenant-owned references before this feature is considered complete for release.
