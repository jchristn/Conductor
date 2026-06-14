# Access Control Plan: Dedicated Model Access Policies

Status: Implementation, documentation, SDK tests, dashboard build, and shared automated tests complete; external provider matrix, E2E, manual, security, and performance validation remain open  
Branch: feature/acls  
Date: 2026-06-14  
Owner: TBD  
Source: Option 4 from ACCESS_CONTROL_OPTIONS.md  
Review trigger: Review when API contracts, persistence schema, and policy decision semantics are stable enough for the first implementation PR.

## Purpose

Dedicated model access policies make model authorization a first-class product feature instead of an inline credential or VMR setting. A tenant administrator should be able to define which credentials, users, or labeled subjects may use which model definitions, model names, model labels, and VMRs for specific actions such as completions, embeddings, list-models, and model-management operations.

The feature must enforce access before a provider request is sent to a runner, must preserve tenant isolation, and must produce explainable decisions for API clients, the dashboard, request history, audits, and tests.

## Product Outcomes

- [x] Administrators can create, update, validate, activate, deactivate, and delete tenant-scoped model access policies.
- [x] Administrators can attach a policy to a VMR and, later if required, to a credential or tenant default.
- [x] Proxied requests are authenticated before routing when model access enforcement requires a credential.
- [x] Routing evaluates model access after request/model resolution and before endpoint selection.
- [x] Explicit denies win over allows at the same priority.
- [x] Higher-priority rules win before lower-priority rules.
- [x] Missing matches fall back to the policy default decision, then the server global default decision.
- [x] Monitor mode records what would have been denied without blocking traffic.
- [x] Request history shows denied and would-deny decisions with the matched policy and rule.
- [x] Dashboard users can manage policies and simulate an effective decision for a credential, model, action, and VMR.
- [x] JavaScript and Python SDKs expose the management and simulation API.
- [x] REST_API, README, CHANGELOG, TESTING, Postman, and Docker factory schema stay current.

## Policy Semantics

- [x] Define enforcement modes: `Disabled`, `Monitor`, `Enforce`.
- [x] Define default decisions: `Permit`, `Deny`.
- [x] Define rule effects: `Allow`, `Deny`.
- [x] Define subject types: `Credential`, `CredentialLabel`, `User`, `UserLabel`, `Tenant`, `Any`.
- [x] Define resource types: `ModelDefinition`, `ModelName`, `ModelLabel`, `VirtualModelRunner`, `Any`.
- [x] Define actions: `Completions`, `Embeddings`, `ListModels`, `ShowModel`, `LoadModel`, `UnloadModel`, `ModelManagement`.
- [x] Define unknown model behavior: deny, allow, or require strict VMR resolution. Recommended default for enforcement is deny.
- [x] Define list-models behavior: filter inaccessible models or synthesize from allowed model definitions. Recommended enforcement default is filter or synthesize, never raw pass-through.
- [x] Define admin behavior explicitly. Admin route access does not automatically imply model proxy access unless the server setting allows an admin bypass.
- [x] Return `401 Unauthorized` only when authentication is missing or invalid.
- [x] Return `403 Forbidden` when authentication succeeds but model access denies the action.
- [x] Include a stable denial code, matched policy id, matched rule id, and decision mode in routing decisions and request history.

## Implementation Phases

- [x] Phase 0: Record an ADR for final policy semantics, compatibility defaults, and rollout mode.
- [x] Phase 1: Add domain models, enums, DTOs, validation, settings, and API contracts.
- [x] Phase 2: Add database tables, provider implementations, migrations, indexes, backup, and restore support.
- [x] Phase 3: Add the policy evaluator, cache, explain API, and validation service.
- [x] Phase 4: Close proxy authentication gaps and integrate policy evaluation into routing.
- [x] Phase 5: Add REST management routes and update the API explorer contract.
- [x] Phase 6: Add dashboard management, simulation, VMR integration, and request-history visibility.
- [x] Phase 7: Add JavaScript and Python SDK support.
- [x] Phase 8: Update product documentation, Postman collection, Docker factory schema, and changelog.
- [ ] Phase 9: Complete automated, provider, dashboard, SDK, E2E, security, and performance testing.

## Backend Plan

### Domain Contracts

- [x] Add `ModelAccessPolicy` under `src/Conductor.Core/Models`.
- [x] Add `ModelAccessRule` under `src/Conductor.Core/Models`.
- [x] Add `ModelAccessEvaluationContext` for tenant, user, credential, VMR, requested model, effective model, model definition, labels, action, request type, and API type.
- [x] Add `ModelAccessEvaluationResult` with decision, effect, mode, default source, policy id, rule id, reason code, reason text, and would-deny flag.
- [x] Add `ModelAccessPolicySummary` for effective VMR and dashboard responses.
- [x] Add one enum per file for effect, default decision, enforcement mode, subject type, resource type, action, unknown-model behavior, and list-models behavior.
- [x] Add PrettyId prefixes for policy and rule ids, for example `map_` and `mar_`.
- [x] Add `ModelAccessPolicyId` to `VirtualModelRunner`.
- [x] Keep `Credential.ModelAccessPolicyId` as a follow-up unless a product decision requires credential-attached policies in the first release.
- [x] Add XML documentation to all public models, DTOs, interfaces, and enums.
- [x] Avoid `JsonElement` in fixed contracts and avoid tuple return types.

### Settings

- [x] Add `ModelAccessControlSettings` to server settings.
- [x] Include `Enabled`, `Mode`, `DefaultDecision`, `RequireCredentialForProxy`, `UnknownModelBehavior`, `ListModelsBehavior`, `CacheTtlMs`, and admin bypass flags.
- [x] Default new installs to monitor or disabled mode until migration and dashboard docs are complete.
- [x] Default existing installs to compatible behavior so upgrades do not unexpectedly block traffic.
- [x] Document the production hardening path from monitor to enforce.

### Database

- [x] Add `IModelAccessPolicyMethods` with domain-specific methods, not generic SQL wrappers.
- [x] Add driver property `DatabaseDriverBase.ModelAccessPolicy`.
- [x] Add provider implementations for SQLite, MySQL, PostgreSQL, and SQL Server.
- [x] Add `modelaccesspolicies` table.
- [x] Add `modelaccessrules` table.
- [x] Add nullable `modelaccesspolicyid` column to `virtualmodelrunners`.
- [x] Store JSON label, tag, metadata, selector, and action collections consistently with current provider patterns.
- [x] Add indexes for tenant, active, policy, priority, effect, subject, resource, VMR, and last-update fields.
- [x] Add migration logic for all providers.
- [x] Add Docker factory schema changes.
- [x] Add backup package fields for model access policies and rules.
- [x] Add restore validation and restore counts.
- [x] Ensure restore remaps tenant-owned references safely and rejects cross-tenant references.

Suggested normalized storage:

```text
modelaccesspolicies
- id
- tenantid
- name
- description
- defaultdecision
- active
- labels
- tags
- metadata
- createdutc
- lastupdateutc

modelaccessrules
- id
- tenantid
- policyid
- name
- description
- priority
- effect
- subjecttype
- subjectid
- subjectselector
- resourcetype
- resourceid
- resourceselector
- vmrid
- actions
- active
- createdutc
- lastupdateutc
```

### Policy Evaluation Service

- [x] Add `IModelAccessControlService`.
- [x] Add `ModelAccessControlService.EvaluateAsync(...)`.
- [x] Add `ValidatePolicyAsync(...)` to catch invalid references, duplicate priorities, empty action sets, invalid selectors, and cross-tenant references.
- [x] Add `ExplainAsync(...)` for dashboard and SDK simulation.
- [x] Cache active policies by tenant id, policy id, and last update timestamp or version.
- [x] Invalidate cache on create, update, delete, and activation changes.
- [x] Invalidate cache on restore.
- [x] Evaluate only tenant-scoped policy data.
- [x] Match subjects by credential id, credential labels, user id, user labels, tenant, and any.
- [x] Match resources by model definition id, model name, model labels, VMR id, and any.
- [x] Match actions by normalized request type and API type.
- [x] Preserve a deterministic tie-breaker after priority and deny-over-allow rules.
- [x] Return structured reasons instead of free-form-only messages.
- [x] Add monitor-mode result recording without blocking the request.

### Routing Integration

- [x] Split request model extraction and model-definition resolution into a read-only service that can run before endpoint selection.
- [x] Call policy evaluation after VMR active/request-type checks and before endpoint inventory, session affinity, load balancing, and provider calls.
- [x] Preserve current strict-mode model behavior, but route strict-mode failures through consistent status and denial codes.
- [x] Ensure effective model mutation does not let a request bypass a denied requested model.
- [x] Define whether access is checked against requested model, effective model, or both. Recommended first release: deny if either requested or effective resolved resource is denied.
- [x] Add list-models filtering or synthesis before returning models to the caller.
- [x] Ensure session-affinity pins cannot bypass a later access denial.
- [x] Add `ModelAccessPolicyId`, `ModelAccessPolicyName`, `ModelAccessRuleId`, `ModelAccessRuleName`, `ModelAccessDecision`, and `ModelAccessWouldDeny` to `RoutingDecision`.
- [x] Add timeline entries for policy loading, rule match, default decision, monitor mode, and enforcement result.

### Proxy Authentication

- [x] Authenticate proxied requests before routing when model access settings require credentials, and opportunistically attach valid presented credentials for attribution otherwise.
- [x] Populate `RequestContext.CredentialId`, `CredentialName`, `UserId`, and `TenantId` from authentication metadata.
- [x] Reject missing or invalid credentials with `401` when `RequireCredentialForProxy` is enabled.
- [x] Reject valid credentials from the wrong tenant with `403`.
- [x] Keep admin route authentication separate from proxy credential authentication.
- [x] Support OpenAI bearer-token and Gemini key query authentication consistently.
- [x] Audit failed authentication and model access denials.

### REST API

- [x] Add `ModelAccessPolicyController`.
- [x] Add a typed route registrar class for model access policies.
- [x] Register routes through the existing route registry pattern.
- [x] Add request types and authorization configuration entries.
- [x] Add validation for tenant ownership on every referenced credential, user, model definition, and VMR.
- [x] Return consistent success, validation, not-found, conflict, and forbidden responses.

Proposed routes:

```text
GET    /v1.0/modelaccesspolicies
POST   /v1.0/modelaccesspolicies
GET    /v1.0/modelaccesspolicies/{id}
PUT    /v1.0/modelaccesspolicies/{id}
DELETE /v1.0/modelaccesspolicies/{id}
POST   /v1.0/modelaccesspolicies/validate
POST   /v1.0/modelaccesspolicies/{id}/evaluate
GET    /v1.0/modelaccesspolicies/effective?credentialId=&userId=&vmrId=&modelDefinitionId=&modelName=&action=
```

### Observability And Audit

- [x] Add request-history fields or detail payload entries for model access policy id, rule id, decision, and would-deny state.
- [x] Add request-history filters for access denied, policy id, rule id, and would-deny.
- [x] Add analytics counters for allowed, denied, would-deny, default-allowed, default-denied, and evaluator errors.
- [x] Ensure denial logs do not leak bearer tokens, API keys, or model-provider secrets.
- [ ] Add dashboard-visible operational signals for policy denial rate by VMR and action.

## Frontend Dashboard Plan

- [x] Add API client methods in `dashboard/src/api/api.js`.
- [x] Add route and sidebar navigation for model access policies.
- [x] Add `ModelAccessPolicies.jsx`.
- [x] Add list view with tenant, active state, default decision, rule count, last updated, and actions.
- [x] Add create and edit forms.
- [x] Add rule builder with priority, effect, subject type, subject selector, resource type, resource selector, VMR, action set, and active state.
- [x] Add validation panel that shows invalid references, duplicate priority conflicts, selector problems, and unreachable rules.
- [x] Add simulation panel for credential, user, VMR, model name/model definition, and action.
- [x] Add conflict indicators for same-priority allow and deny matches.
- [x] Add effective-policy summary to the VMR create/edit flow.
- [x] Add policy selector to VMR create/edit flow.
- [x] Add model access decision details to explain-routing output.
- [x] Add model access filters and details to request history.
- [x] Add credential detail affordance to view effective access for a credential.
- [x] Add model definition detail affordance to view policies affecting that model.
- [x] Use existing dashboard components and avoid nested card layouts.
- [x] Keep controls dense and operational, matching the current admin dashboard style.
- [ ] Route all new user-facing strings through the i18n foundation or the agreed string catalog task.
- [ ] Validate responsive behavior on desktop, tablet, and mobile widths.
- [ ] Add loading, empty, validation-error, forbidden, and retry states.

## SDK Plan

### JavaScript SDK

- [x] Add `listModelAccessPolicies`.
- [x] Add `getModelAccessPolicy`.
- [x] Add `createModelAccessPolicy`.
- [x] Add `updateModelAccessPolicy`.
- [x] Add `deleteModelAccessPolicy`.
- [x] Add `validateModelAccessPolicy`.
- [x] Add `evaluateModelAccessPolicy`.
- [x] Add `getEffectiveModelAccess`.
- [x] Add exact URL, method, body, and query tests with `node:test`.
- [x] Update the JavaScript SDK README.

### Python SDK

- [x] Add `list_model_access_policies`.
- [x] Add `get_model_access_policy`.
- [x] Add `create_model_access_policy`.
- [x] Add `update_model_access_policy`.
- [x] Add `delete_model_access_policy`.
- [x] Add `validate_model_access_policy`.
- [x] Add `evaluate_model_access_policy`.
- [x] Add `get_effective_model_access`.
- [x] Add exact URL, method, body, and query tests.
- [x] Update the Python SDK README.

## Documentation Plan

- [x] Update `README.md` with a concise feature overview and rollout guidance.
- [x] Update `REST_API.md` with all model access policy routes, request bodies, response bodies, error codes, and examples.
- [x] Update `CHANGELOG.md` under Unreleased.
- [x] Update `TESTING.md` with model access test commands and manual validation flows.
- [x] Update `Conductor.postman_collection.json`.
- [x] Update Docker factory schema documentation and seed data if applicable.
- [x] Add example policies for default deny, credential allow-list, label-based access, embeddings-only access, list-model filtering, and monitor-mode rollout.
- [x] Document upgrade compatibility defaults.
- [x] Document the difference between authentication failure and model access denial.
- [x] Document how request history records matched policy and rule data.

## World-Class Test Engineering Coverage Plan

Owner: QA engineer  
Status: Local automated coverage complete for the implemented baseline; provider matrix, dashboard E2E/manual, security, abuse, and performance coverage remain open  
Assumptions: The first implementation includes VMR-attached policies, global compatibility defaults, monitor mode, enforce mode, REST management APIs, dashboard management, and JS/Python SDK methods.  
Dependencies: Final policy semantics ADR, database schema, route contracts, and dashboard wireframes.

The test strategy should prove both correctness and explainability. Authorization failures are security-sensitive, so test coverage should bias toward exhaustive decision tables, cross-tenant negative cases, provider matrix coverage, and end-to-end proxy verification.

### Coverage Targets

- [ ] Add unit coverage for every policy evaluator branch.
- [ ] Add decision-table tests for every subject type, resource type, action, effect, default decision, and enforcement mode.
- [ ] Add provider integration coverage for SQLite, MySQL, PostgreSQL, and SQL Server where the suite supports those providers.
- [ ] Add migration tests from pre-ACL schemas to current schema.
- [ ] Add controller tests for every REST route.
- [ ] Add routing tests that prove denied requests never reach endpoint selection or provider proxying.
- [ ] Add dashboard component and E2E coverage for policy creation, editing, validation, simulation, and VMR attachment.
- [x] Add SDK tests for every new method.
- [ ] Add negative security tests for cross-tenant references and missing credentials.
- [ ] Add manual release validation for upgrade compatibility and monitor-to-enforce rollout.

### Unit Tests

- [x] Policy id and rule id generation uses the new PrettyId prefixes.
- [x] Policy validation rejects missing tenant id.
- [x] Policy validation rejects empty name.
- [x] Policy validation rejects invalid default decision.
- [x] Policy validation rejects duplicate rule ids.
- [x] Policy validation rejects inactive or missing referenced VMRs.
- [x] Policy validation rejects inactive or missing referenced model definitions.
- [x] Policy validation rejects inactive or missing referenced credentials.
- [x] Policy validation rejects inactive or missing referenced users.
- [x] Policy validation rejects cross-tenant VMR references.
- [x] Policy validation rejects cross-tenant model references.
- [x] Policy validation rejects cross-tenant credential references.
- [x] Policy validation rejects cross-tenant user references.
- [x] Policy validation rejects empty action sets when action matching is required.
- [x] Policy validation rejects malformed subject selectors.
- [x] Policy validation rejects malformed resource selectors.
- [x] Policy validation warns about duplicate same-priority rules.
- [ ] Policy validation warns about unreachable lower-priority rules.
- [x] Evaluator allows explicit credential allow.
- [x] Evaluator denies explicit credential deny.
- [x] Evaluator allows credential-label allow.
- [x] Evaluator denies credential-label deny.
- [x] Evaluator allows user allow.
- [x] Evaluator denies user deny.
- [x] Evaluator allows user-label allow.
- [x] Evaluator denies user-label deny.
- [x] Evaluator applies tenant-wide rules.
- [x] Evaluator applies any-subject rules.
- [x] Evaluator matches model definition id.
- [x] Evaluator matches requested model name.
- [x] Evaluator matches effective model name.
- [x] Evaluator matches model labels.
- [x] Evaluator matches VMR resource rules.
- [x] Evaluator matches any-resource rules.
- [x] Evaluator filters by action.
- [x] Evaluator ignores inactive rules.
- [x] Evaluator ignores inactive policies.
- [x] Evaluator applies highest-priority match.
- [x] Evaluator applies explicit deny over allow at the same priority.
- [x] Evaluator uses policy default when no rule matches.
- [x] Evaluator uses global default when no policy applies.
- [x] Evaluator returns deterministic results for tied same-effect rules.
- [x] Evaluator returns structured reason codes for every deny path.
- [x] Monitor mode returns allow plus would-deny details.
- [x] Disabled mode bypasses enforcement but records disabled state.
- [x] Unknown model deny behavior denies unresolved model names.
- [x] Unknown model allow behavior allows unresolved model names only when configured.
- [x] List-model action maps from OpenAI, Gemini, and Ollama request types.

### Database Tests

- [ ] SQLite creates policy and rule tables on fresh initialization.
- [ ] SQLite migrates legacy databases without policy tables.
- [ ] SQLite adds `virtualmodelrunners.modelaccesspolicyid`.
- [ ] SQLite creates expected indexes.
- [ ] MySQL creates policy and rule tables on fresh initialization.
- [ ] MySQL migrates legacy databases without policy tables.
- [ ] PostgreSQL creates policy and rule tables on fresh initialization.
- [ ] PostgreSQL migrates legacy databases without policy tables.
- [ ] SQL Server creates policy and rule tables on fresh initialization.
- [ ] SQL Server migrates legacy databases without policy tables.
- [x] CRUD persists policy fields exactly.
- [x] CRUD persists rule fields exactly.
- [x] Listing policies filters by tenant.
- [x] Listing policies filters by active state.
- [x] Listing rules filters by policy id and tenant.
- [x] Delete policy deletes or rejects dependent rules according to the chosen contract.
- [x] Delete policy fails when attached to an active VMR unless force behavior is explicitly implemented.
- [x] Updating policy invalidates cache.
- [ ] Updating rule invalidates cache.
- [x] Backup exports policies and rules.
- [x] Restore imports policies and rules.
- [x] Restore rejects references that cannot be remapped safely.

### REST Controller Tests

- [x] List policies requires admin authentication.
- [x] Create policy requires admin authentication.
- [x] Read policy requires admin authentication.
- [x] Update policy requires admin authentication.
- [x] Delete policy requires admin authentication.
- [x] Validate policy requires admin authentication.
- [x] Evaluate policy requires admin authentication.
- [ ] Tenant admin can only access tenant-owned policies.
- [ ] Global admin access follows existing administrator semantics.
- [ ] Create returns validation errors for invalid references.
- [ ] Update returns not found for unknown policy id.
- [ ] Update rejects cross-tenant rule updates.
- [x] Delete returns conflict when a policy is attached and force is not used.
- [ ] Evaluate returns allow with matched allow rule details.
- [ ] Evaluate returns deny with matched deny rule details.
- [ ] Evaluate returns default decision when no rule matches.
- [ ] Evaluate handles missing credential according to selected defaults.
- [ ] API responses do not include secrets.
- [ ] API errors have stable machine-readable codes.

### Routing And Proxy Tests

- [ ] Proxy request without credential returns `401` when enforcement requires credentials.
- [ ] Proxy request with invalid bearer token returns `401`.
- [ ] Proxy request with credential from another tenant returns `403`.
- [ ] Valid credential with allow rule reaches routing.
- [ ] Valid credential with deny rule returns `403`.
- [ ] Denied request does not select endpoint.
- [ ] Denied request does not call the provider proxy.
- [ ] Monitor-mode would-deny request still routes.
- [ ] Monitor-mode would-deny request records would-deny details.
- [ ] Strict VMR with unknown model denies before provider call.
- [ ] Non-strict VMR unknown-model behavior follows settings.
- [ ] Requested model deny blocks even if mutation would map to an allowed model.
- [ ] Effective model deny blocks even if requested model is allowed.
- [ ] Embeddings action uses embeddings ACLs.
- [ ] Completions action uses completions ACLs.
- [ ] Model-management action uses management ACLs.
- [x] List-models returns only accessible models.
- [ ] Session affinity cannot reuse a pinned endpoint after a policy denial.
- [ ] Request history records policy id, rule id, decision, status code, and denial code.
- [x] Request analytics increments allow, deny, and would-deny counters.

### Dashboard Tests

- [ ] Policy list renders loading, empty, loaded, forbidden, and error states.
- [ ] Create policy form validates required fields.
- [ ] Edit policy form preserves existing rule order.
- [ ] Rule builder prevents saving invalid selector/action combinations.
- [ ] Validation panel displays API validation results.
- [ ] Simulation panel displays allow, deny, default, and monitor results.
- [ ] VMR form can attach and clear a policy.
- [ ] Explain-routing view shows model access decision timeline.
- [ ] Request-history detail shows matched policy and rule.
- [ ] Request-history filters include policy denied and would-deny cases.
- [ ] Credential view can navigate to effective model access.
- [ ] Model definition view can navigate to affected policies.
- [ ] All new strings are routed through the i18n foundation or catalog.
- [ ] Pseudo-locale test catches clipped labels.
- [ ] Responsive layout works at mobile, tablet, and desktop widths.
- [ ] Keyboard navigation works for rule editing and simulation.

### SDK Tests

- [x] JavaScript list method builds the correct URL and query.
- [x] JavaScript get method builds the correct URL.
- [x] JavaScript create method sends the correct body.
- [x] JavaScript update method sends the correct body.
- [x] JavaScript delete method uses the correct method and URL.
- [x] JavaScript validate method sends the correct body.
- [x] JavaScript evaluate method sends the correct body.
- [x] JavaScript effective-access method encodes query parameters correctly.
- [x] Python list method builds the correct URL and query.
- [x] Python get method builds the correct URL.
- [x] Python create method sends the correct body.
- [x] Python update method sends the correct body.
- [x] Python delete method uses the correct method and URL.
- [x] Python validate method sends the correct body.
- [x] Python evaluate method sends the correct body.
- [x] Python effective-access method encodes query parameters correctly.

### End-To-End Tests

- [ ] Seed tenant, user, credential, model definition, endpoint, VMR, and policy.
- [ ] Verify allowed OpenAI chat completion succeeds.
- [ ] Verify denied OpenAI chat completion returns `403`.
- [ ] Verify allowed OpenAI embeddings request succeeds.
- [ ] Verify denied OpenAI embeddings request returns `403`.
- [ ] Verify OpenAI list-models filters denied models.
- [ ] Verify Gemini key authentication participates in policy decisions.
- [ ] Verify Gemini model-name URL extraction participates in policy decisions.
- [ ] Verify Ollama request types map to correct actions.
- [ ] Verify dashboard-created policy affects live proxy behavior.
- [ ] Verify SDK-created policy affects live proxy behavior.
- [ ] Verify backup and restore preserve policy behavior.
- [ ] Verify upgrade from a pre-ACL database keeps traffic compatible until enforcement is enabled.
- [ ] Verify monitor mode can be enabled, inspected, and then promoted to enforce mode.

### Security, Abuse, And Performance Tests

- [x] Cross-tenant policy id in VMR attachment is rejected.
- [x] Cross-tenant model id in a rule is rejected.
- [x] Cross-tenant credential id in a rule is rejected.
- [ ] Selector injection attempts are rejected or treated as plain data.
- [ ] Very large selector payloads hit validation limits.
- [ ] Very large rule sets have bounded evaluation time.
- [ ] Cache invalidation prevents stale allows after a deny rule is added.
- [ ] Cache invalidation prevents stale denies after an allow rule is added.
- [ ] Concurrent policy updates produce deterministic final state.
- [ ] Denial responses do not leak internal schema, SQL, stack traces, tokens, or provider URLs.
- [ ] Metrics labels avoid high-cardinality model names unless deliberately approved.
- [ ] Load test policy evaluation overhead on hot-cache and cold-cache paths.
- [ ] Fault injection verifies evaluator database failures fail closed in enforce mode.
- [ ] Fault injection verifies evaluator database failures record errors in monitor mode.

## Open Decisions

- [x] Decide whether first release includes credential-attached policies or only VMR-attached policies plus global defaults.
- [x] Decide whether policy rules evaluate requested model, effective model, or both. Recommended: both.
- [x] Decide default compatibility mode for upgraded installs. Recommended: monitor or disabled with explicit migration docs.
- [x] Decide list-models behavior in enforce mode. Recommended: filter or synthesize.
- [x] Decide whether deleting an attached policy is blocked, soft-deactivated, or force-detached.
- [x] Decide whether tenant admins can simulate decisions for all tenant credentials.
- [ ] Decide whether policy names must be unique per tenant.
- [x] Decide exact selector grammar for label matching and pattern matching.

## Definition Of Done

- [x] ADR accepted for policy semantics and rollout defaults.
- [x] Backend builds with no new compiler warnings.
- [x] All existing automated tests pass.
- [x] New shared test suites pass under xUnit and NUnit runners.
- [ ] Provider integration tests pass for every supported database available in CI.
- [x] Dashboard production build passes (`npm.cmd run build`; no unit/component test script exists in `dashboard/package.json`).
- [ ] Dashboard manual validation completed for desktop, tablet, and mobile.
- [x] JavaScript SDK tests pass.
- [x] Python SDK tests pass.
- [x] REST_API, README, CHANGELOG, TESTING, Postman, and Docker schema are updated.
- [x] Request history and analytics expose denied and would-deny decisions.
- [x] Backup and restore preserve policies and rules.
- [ ] Upgrade compatibility path is documented and tested.
- [ ] Security review completed for tenant isolation, authentication, authorization, logging, and denial behavior.
