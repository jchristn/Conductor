# Access Control Options for Credential-to-Model Enforcement

This document evaluates options for adding enforcement that specifies which
Conductor credential is allowed to access which models.

The analysis is based on the current repository shape:

- Credentials are tenant-scoped `Credential` resources with `Id`,
  `TenantId`, `UserId`, `BearerToken`, `Active`, `Labels`, `Tags`, and
  `Metadata`.
- Models are represented by tenant-scoped `ModelDefinition` resources. They
  have names, capability hints, labels, tags, and metadata.
- Virtual Model Runners (VMRs) are the proxy boundary. A VMR attaches endpoint
  IDs, model definition IDs, model configuration IDs, optional model-to-config
  mappings, request-type gates, load-balancing policy, and `StrictMode`.
- The live proxy resolves the VMR from the request path, runs routing in
  `RoutingDecisionService`, applies request model/configuration mutation in
  `RoutingModelMutationService`, then forwards to a selected endpoint.
- Request history already records credential ID/name, requested model,
  effective model, selected endpoint, model definition, routing decision, and
  denial reason fields when those values are available.
- The management API has route-level auth through `AuthorizationConfig`.
  Proxied API request types are present in that config, but the default proxy
  route currently calls the proxy controller directly. Credential-specific
  enforcement should first ensure proxy authentication is actually run and
  `ctx.Metadata` contains an `AuthenticationResult`.

## Key Design Questions

Before choosing an option, these decisions need to be made explicitly.

### What Is The Subject?

The user request is specifically "which credential is allowed", so the primary
subject should be `Credential.Id`, not bearer token text. Bearer tokens should
never appear in ACL data.

There are still related subject decisions:

- Header-based user authentication does not produce a credential. Either deny
  header-authenticated proxied traffic when model ACLs are enforced, or evaluate
  the user's `UserId` as a secondary subject.
- Global admin users and system administrators need a clear bypass setting.
- Tenant admin rights should not automatically imply model access unless that
  is an intentional policy choice.
- Future grouping can use credential labels, user labels, tenant labels, or
  explicit groups.

### What Is The Resource?

There are several plausible "model" identities:

- `ModelDefinition.Id`: stable and tenant-scoped. Best for internal ACLs.
- `ModelDefinition.Name`: convenient for operators and provider request bodies,
  but duplicate names are currently allowed with only a validation warning.
- Provider model path segment: required for Gemini-style URLs.
- Model labels/tags: useful for tiers like `gpu`, `pii-approved`, or
  `embeddings`.
- VMR ID: useful when the same model should be allowed through one route but
  denied through another.
- Model runner endpoint ID: useful for controlling access to hardware or
  upstream provider accounts, but not sufficient as model-level control.

The strongest model identity is the resolved active `ModelDefinition.Id`, with
model name and pattern support as optional convenience layers.

### Requested Model Or Effective Model?

The current mutation service can change the effective model:

- If one active model definition is attached to a VMR, requests can be rewritten
  to that model.
- In `StrictMode`, only attached model definitions are accepted.
- Gemini places the requested model in the URL path rather than only the JSON
  body.

Access control should evaluate both:

- Deny if the requested model is explicitly denied.
- Deny if the effective model is not permitted.
- Permit only when the final effective model is allowed.

This avoids a credential requesting an allowed alias that is later mutated to a
model it should not use, and also avoids confusing denials when a route pins one
safe model regardless of the requested body.

### Which Actions Are Controlled?

At minimum, enforce on proxied inference:

- OpenAI chat completions, completions, embeddings
- Gemini generate content, stream generate content, embed content
- Ollama generate, chat, embeddings

The design should also decide whether model access controls apply to:

- list models endpoints
- Ollama show model info
- Ollama pull/delete through a VMR
- management-plane model loading and verification
- endpoint-level Ollama model management routes

The cleanest long-term model is action-aware access:

- `Completions`
- `Embeddings`
- `ListModels`
- `ShowModel`
- `LoadModel`
- `PullModel`
- `DeleteModel`

### Default Permit Or Default Deny?

Default permit is safer for compatibility. Default deny is safer for security.

A practical rollout usually needs both:

1. `Disabled` or `Monitor` mode for discovery.
2. `DefaultPermit` with explicit deny rules for initial enforcement.
3. `DefaultDeny` for sensitive tenants, VMRs, or new deployments.

### Where Should Enforcement Run?

Recommended request-time order:

1. Parse URL into `UrlContext`.
2. Authenticate the proxy request and set `AuthenticationResult` metadata.
3. Resolve the VMR and verify tenant match.
4. Apply existing request-type gates.
5. Resolve requested and effective model before endpoint selection.
6. Evaluate model access control.
7. Build endpoint candidates, session affinity, and load-balancing decision.
8. Apply request mutation and forward.

The current code selects endpoints before model mutation. If enforcement must
avoid endpoint health/capacity leakage and avoid doing unnecessary routing work
for denied requests, model resolution should move earlier or be split into a
lightweight "resolve only" step before endpoint selection.

### Common Implementation Building Blocks

Most options below need some common pieces:

- Add a `ModelAccessControlService` or `CredentialModelAccessService`.
- Pass authenticated subject data into `RoutingDecisionService.EvaluateAsync`.
  Today it receives `RequestContext`, which already has credential/user fields
  when populated.
- Add a routing timeline stage such as `ModelAccessControl`.
- Use HTTP `401` only for unauthenticated requests. Use `403` for authenticated
  credentials that are not allowed to access the model.
- Add denial codes, for example:
  - `ProxyAuthenticationRequired`
  - `CredentialRequiredForModelAccess`
  - `CredentialModelAccessDenied`
  - `CredentialVmrAccessDenied`
  - `ModelAccessPolicyMissing`
- Extend request history and analytics only if the existing
  `DenialReasonCode`, `RequestedModel`, `EffectiveModel`, and
  `RoutingDecision` fields are not enough.
- Extend validation and effective preview so operators can see whether a VMR
  has enforceable model access.
- Update dashboard forms, API docs, Postman collection, and SDKs for any new
  public fields or resources.
- Add tests for bearer token auth, Gemini `?key=`, header auth behavior,
  strict mode, single-model mutation, list-model behavior, tenant isolation,
  admin bypass behavior, request history, and default decision modes.

## Option Summary

| Option | Main Idea | Best Fit | Effort | Security Strength |
| --- | --- | --- | --- | --- |
| 1 | ACLs attached to credentials | Simple per-key control | Medium | Medium to high |
| 2 | ACLs attached to model definitions | Protect sensitive models | Medium | High when strict model definitions are used |
| 3 | ACLs attached to VMRs | Route-specific entitlements | Medium | High per route |
| 4 | Dedicated access policy resources | Long-term flexible policy model | High | High |
| 5 | Global settings and defaults | Rollout and compatibility controls | Low to medium | Depends on paired policy |
| 6 | Default-deny permit-list behavior | Least privilege | Medium | Highest |
| 7 | Default-permit deny-list behavior | Low-risk incremental rollout | Low | Medium |
| 8 | Label/tag based entitlements | Tier-based bulk assignment | Medium | Medium to high |
| 9 | User or role inherited access | Human-centric access management | Medium to high | Medium to high |
| 10 | Endpoint ACLs | Protect hardware/provider accounts | Medium | Medium for model access |
| 11 | Per-credential VMRs with strict mode | Operational isolation using existing model pins | Low to medium | Medium to high |
| 12 | External policy engine | Enterprise centralized policy | High | High if operated well |
| 13 | Metadata-driven prototype | Fast experiment with no schema changes | Low | Low to medium |

## Option 1: Credential-Attached Permit And Deny Lists

Attach access control directly to `Credential`.

### Implementation

Add typed fields to `Credential`, preferably backed by JSON columns in the same
style as existing `LabelsJson`, `TagsJson`, and `MetadataJson` fields:

```json
{
  "ModelAccessMode": "Inherit",
  "AllowedModelDefinitionIds": ["md_safe_chat"],
  "DeniedModelDefinitionIds": ["md_sensitive"],
  "AllowedModelNames": ["llama3.2:latest"],
  "DeniedModelNames": ["phi-secret:*"],
  "AllowedVirtualModelRunnerIds": ["vmr_public_chat"],
  "DeniedVirtualModelRunnerIds": [],
  "AllowedActions": ["Completions", "Embeddings"]
}
```

Possible `ModelAccessMode` values:

- `Inherit`: use global or tenant default.
- `DefaultPermit`: allow unless a deny rule matches.
- `DefaultDeny`: deny unless an allow rule matches.

Evaluation:

1. Authenticate the proxy request.
2. Require `AuthenticationResult.Credential` when enforcement is active.
3. Resolve the VMR and requested/effective model.
4. Check deny lists first.
5. If credential mode is `DefaultDeny`, require a matching allow rule.
6. If credential mode is `DefaultPermit`, allow unless denied.
7. Record the matched rule in `RoutingDecision.Timeline`.

Management work:

- Extend `CredentialController`, database methods, all four table schemas, and
  migrations.
- Add validation that referenced model definition IDs and VMR IDs are in the
  same tenant.
- Add dashboard controls on the Credentials page.
- Add request history filters for credential/model denial if needed.

### Pros

- Matches the user's mental model directly: each credential says what it can
  access.
- Credential rotation and model entitlement changes happen in the same place.
- Easy to understand for small and medium installations.
- Backward-compatible if the default is `Inherit` plus global
  `DefaultPermit`.
- Works well for service-specific API keys where each app has its own
  credential.
- Does not require operators to edit every model definition.

### Cons

- Duplicates model lists across many credentials.
- Harder to answer "who can access this model?" without scanning credentials
  unless ACLs are normalized into a separate table.
- Model name rules are brittle because duplicate names are allowed today.
- Credential ACLs do not naturally express route-specific policy unless VMR IDs
  are also included.
- If a user has many credentials, keeping them consistent becomes manual unless
  user-level inheritance is added.
- Large JSON ACLs on credentials can make dashboard forms and update payloads
  noisy.

## Option 2: ModelDefinition-Attached ACLs

Attach the ACL to the model resource being protected.

### Implementation

Add fields to `ModelDefinition`:

```json
{
  "AccessMode": "DefaultDeny",
  "AllowedCredentialIds": ["cred_app_a", "cred_app_b"],
  "DeniedCredentialIds": ["cred_old"],
  "AllowedCredentialLabels": ["tier:paid", "team:search"],
  "DeniedCredentialLabels": ["suspended"],
  "AllowedActions": ["Completions"]
}
```

Evaluation:

1. Resolve the effective model definition for the request.
2. If no model definition exists, use the VMR or global fallback rule.
3. If the model definition has a deny match for the credential, deny.
4. If model definition mode is `DefaultDeny`, require a credential ID or label
   allow match.
5. If model definition mode is `DefaultPermit`, allow unless denied.

This option benefits strongly from `StrictMode` or a similar "only attached
model definitions are valid" behavior. Without a resolved model definition, the
server would need a default rule for arbitrary requested model names.

Management work:

- Extend `ModelDefinition` persistence, validation, dashboard forms, API docs,
  and SDKs.
- Add validation for same-tenant credential IDs.
- Extend VMR effective preview to show access mode per attached model.
- Add tests for duplicate model names and inactive model definitions.

### Pros

- Security policy lives with the thing being protected.
- Easy to inspect a sensitive model and see who may use it.
- Less duplication when many credentials share the same model policy.
- Good fit for high-risk models, expensive models, or compliance-scoped models.
- Works cleanly with active/inactive model definitions and VMR strict mode.

### Cons

- Less convenient when operators think in terms of "this API key can access
  these models".
- Requires model definitions to exist and be attached. Current non-strict VMRs
  can route arbitrary model names.
- Duplicate model names can make name-based policy confusing.
- Route-specific exceptions are awkward unless VMR scope is added to each ACL.
- A single model definition may become noisy if hundreds of credentials need
  explicit entries.
- If new credentials should inherit existing access, every protected model may
  need updates unless label/group rules are used.

## Option 3: VMR-Attached Credential And Model ACLs

Attach access rules to the VMR, which is already the proxy route boundary.

### Implementation

Add fields to `VirtualModelRunner`:

```json
{
  "ModelAccessMode": "DefaultDeny",
  "CredentialModelAccessRules": [
    {
      "Name": "App A chat models",
      "Effect": "Allow",
      "CredentialIds": ["cred_app_a"],
      "ModelDefinitionIds": ["md_llama", "md_mistral"],
      "Actions": ["Completions"]
    },
    {
      "Name": "Block embeddings for App A",
      "Effect": "Deny",
      "CredentialIds": ["cred_app_a"],
      "ModelNames": ["text-embedding-*"],
      "Actions": ["Embeddings"]
    }
  ]
}
```

Evaluation:

1. Resolve VMR from `BasePath`.
2. Authenticate credential and verify credential tenant matches VMR tenant.
3. Resolve requested/effective model.
4. Evaluate VMR rules in priority order, with explicit deny winning.
5. Apply the VMR `ModelAccessMode` if no rule matches.

Management work:

- Extend VMR model, table schemas, and migrations.
- Extend VMR validation to check credential IDs and model definition IDs.
- Extend effective VMR preview to include access rules and possibly a
  credential-model matrix.
- Add dashboard VMR editor controls.

### Pros

- Aligns with the current routing architecture. The VMR is the object that
  exposes the model API surface.
- Supports different access rules for the same model through different VMRs.
- Works naturally with attached model definitions, `StrictMode`, request-type
  gates, and load-balancing policy.
- Operators can reason about "who can use this route?" and "which models does
  this route expose?" in one place.
- Avoids changing every model definition for route-specific access.

### Cons

- Rules can be duplicated across VMRs.
- If one VMR is left permissive, a credential may still reach the model through
  that route.
- Large VMR configs can become hard to edit and review.
- Credential lifecycle is split: token fields are on `Credential`, but access
  lives on VMRs.
- This can blur VMR's role by mixing routing config and authorization config in
  one resource.

## Option 4: Dedicated Model Access Policy Resources

Create first-class policy and rule resources instead of embedding ACLs into
credentials, model definitions, or VMRs.

### Implementation

Add new core models, controllers, route modules, database interfaces, and
tables. One possible shape:

```json
{
  "Id": "map_xxx",
  "TenantId": "default",
  "Name": "Production Chat Access",
  "DefaultDecision": "Deny",
  "Active": true,
  "Rules": [
    {
      "Priority": 100,
      "Effect": "Allow",
      "SubjectType": "Credential",
      "SubjectId": "cred_app_a",
      "ResourceType": "ModelDefinition",
      "ResourceId": "md_llama",
      "VmrId": "vmr_prod_chat",
      "Actions": ["Completions"]
    },
    {
      "Priority": 50,
      "Effect": "Deny",
      "SubjectType": "CredentialLabel",
      "SubjectSelector": "suspended",
      "ResourceType": "ModelLabel",
      "ResourceSelector": "premium",
      "Actions": ["*"]
    }
  ]
}
```

Attachment choices:

- Tenant-level default policy.
- VMR `ModelAccessPolicyId`.
- Credential `ModelAccessPolicyId`.
- A global server setting that selects the default policy behavior.

Evaluation:

1. Build an access context with tenant, user, credential, VMR, action,
   requested model, effective model, model definition, and labels.
2. Load active policies that apply to the tenant and VMR.
3. Evaluate highest-priority matching rules.
4. Explicit deny wins over allow at the same priority.
5. Fall back to policy default, then global default.

Storage:

- A normalized table is preferred for queryability:
  - `modelaccesspolicies`
  - `modelaccessrules`
- JSON arrays are possible but less queryable.
- Add indexes on tenant, active, subject, resource, VMR, priority, and effect.
- Add a small in-memory cache keyed by tenant and policy version or
  `LastUpdateUtc`.

Management work:

- New REST routes such as `/v1.0/modelaccesspolicies`.
- New dashboard view.
- Backup/restore support.
- SDK support.
- Effective VMR preview should resolve the active policy and show matched
  rules for a simulated credential/model pair.

### Pros

- Most flexible and scalable long-term design.
- Clean separation between routing resources and authorization policy.
- Easy to answer both "what can this credential access?" and "who can access
  this model?" with proper indexes.
- Supports permit lists, deny lists, default permit, default deny, labels,
  users, future groups, action-specific rules, and route-specific overrides.
- Strong auditability and easier change review than hidden JSON blobs.
- Can grow into a general policy engine for more resource types later.

### Cons

- Largest implementation.
- Requires new APIs, dashboard surfaces, database implementations for SQLite,
  PostgreSQL, MySQL, and SQL Server, migrations, tests, backup/restore changes,
  docs, and SDK updates.
- More complex operator UX.
- Requires clear conflict-resolution semantics.
- Needs caching or efficient queries to avoid per-request overhead.
- May be overbuilt if the product only needs simple per-credential allow lists.

## Option 5: Global Settings And Defaults

Add server-level and possibly tenant-level settings that control whether model
access is enforced and what happens when no specific rule matches.

### Implementation

Extend `ServerSettings`:

```json
{
  "AccessControl": {
    "Enabled": true,
    "Mode": "Enforce",
    "DefaultDecision": "Permit",
    "AdminBypass": true,
    "TenantAdminBypass": false,
    "RequireCredentialForProxy": true,
    "NoCredentialDecision": "Deny",
    "ListModelsBehavior": "Filter",
    "UnknownModelBehavior": "Deny"
  }
}
```

Mode examples:

- `Disabled`: no access checks.
- `Monitor`: compute decisions and record what would happen, but route
  normally.
- `Enforce`: deny when the decision is deny.

This option is not a complete model access system by itself unless paired with
static settings lists. It is best used as the common control plane for any of
the ACL options.

### Pros

- Provides safe rollout controls.
- Preserves backward compatibility with `DefaultDecision = Permit`.
- Lets operators choose fail-open or fail-closed behavior.
- Can enforce "proxy requests must use credentials" globally.
- Useful even if the detailed ACL storage changes later.

### Cons

- Global settings alone are too coarse for "credential X can use model Y".
- Server settings are not tenant self-service and may require restart or
  configuration reload work.
- Does not provide good auditability for individual decisions unless paired
  with policy/rule data.
- Multi-tenant environments may need tenant-level settings, which adds another
  resource or config layer.
- If used as the only mechanism, it quickly becomes an unstructured config file
  policy language.

## Option 6: Default-Deny Permit Lists

Make the absence of a matching allow rule deny the request.

### Implementation

This is a behavior mode that can apply to options 1, 2, 3, or 4.

Rules:

1. Explicit deny always denies.
2. Explicit allow permits.
3. No match denies.
4. Empty allow lists deny.
5. Unknown requested models deny unless a policy explicitly allows name or
   pattern matching.

Rollout steps:

- Add `Monitor` mode first to detect what would be denied.
- Generate a report from request history: credential, VMR, requested model,
  effective model, request type, count.
- Seed allow rules for known production traffic.
- Enable default deny tenant by tenant or VMR by VMR.

### Pros

- Strongest least-privilege posture.
- New models are not exposed accidentally.
- New credentials have no model access until granted.
- Works well for compliance-sensitive tenants and paid model tiers.
- Simple mental model: no allow means no access.

### Cons

- Breaking change if enabled without migration.
- High operational burden for dynamic or exploratory environments.
- List-model behavior must be carefully designed or all listing breaks.
- Support teams need good diagnostics and "why denied" tooling.
- Bootstrap flows need an initial admin or setup credential that can create
  policies without being blocked.

## Option 7: Default-Permit Deny Lists

Allow access unless a deny rule matches.

### Implementation

This is also a behavior mode that can apply to options 1, 2, 3, or 4.

Rules:

1. Explicit deny denies.
2. Explicit allow may be recorded but is not required.
3. No match permits.
4. Unknown requested models permit unless explicitly denied.

Typical use:

- Deny sensitive models to general-purpose credentials.
- Deny expensive models to free-tier credentials.
- Deny model-management actions broadly while leaving inference alone.

### Pros

- Minimal backward compatibility risk.
- Fastest way to block a known-sensitive model or credential.
- Easy to explain during rollout.
- Requires fewer ACL entries.
- Useful as phase 1 before moving to default deny.

### Cons

- Not least privilege.
- New models are exposed by default.
- Deny lists can be forgotten during model creation.
- A typo in a model name deny rule can silently permit access unless validation
  catches it.
- It is harder to prove that a credential can only access an approved set.

## Option 8: Label/Tag Based Entitlements

Use labels or tags on credentials and models to decide access.

### Implementation

Conductor already has `Labels`, `Tags`, and `Metadata` on credentials, model
definitions, endpoints, VMRs, users, and tenants. Formalize conventions such as:

Credential:

```json
{
  "Labels": ["tier:paid", "team:search"],
  "Tags": {
    "access.tier": "paid",
    "access.region": "us"
  }
}
```

Model definition:

```json
{
  "Labels": ["tier:paid", "capability:chat"],
  "Tags": {
    "access.requiredTier": "paid"
  }
}
```

Evaluation models:

- Intersection: credential labels must intersect model labels.
- Required labels: model defines labels all credentials must have.
- Tier comparison: credential tier must be greater than or equal to model tier.
- Deny labels: credential with `suspended` or `blocked` is denied.

This can be implemented as:

- typed fields for reliability, or
- a policy service that reads existing labels/tags.

### Pros

- Great for bulk assignment.
- Avoids listing every credential on every model.
- Leverages existing model properties.
- Supports natural product tiers and team-based access.
- Good companion to default-deny permit lists because one label can unlock a
  family of models.

### Cons

- String conventions are easy to mistype.
- Existing labels may already be used informally, so introducing enforcement
  semantics can surprise operators.
- Harder to audit than explicit credential-model rules.
- Negative rules and priority ordering can become confusing.
- Needs strong validation and dashboard affordances to be safe.
- Pure label intersection is often too weak; required labels or explicit policy
  rules are usually needed.

## Option 9: User Or Role Inherited Access

Attach model access to users or roles, then let credentials inherit from their
owning user.

### Implementation

Add fields to `UserMaster`, or introduce a new role/group resource:

```json
{
  "AllowedModelDefinitionIds": ["md_standard"],
  "DeniedModelDefinitionIds": ["md_restricted"],
  "ModelAccessRoleIds": ["role_support_agent"]
}
```

Credential behavior:

- Credentials inherit the user's model access by default.
- Credentials can optionally have narrower overrides.
- A credential cannot grant more access than its user unless explicitly allowed
  by admin policy.

Evaluation:

1. Build subject claims from credential, user, and optional roles.
2. Apply user/role deny rules.
3. Apply credential-specific deny rules.
4. Apply credential-specific narrowing rules.
5. Use global or tenant default if no rule matches.

### Pros

- Reduces repeated policy work when users rotate credentials.
- Better for human users with multiple API keys.
- Supports role-based organization over time.
- Can keep credential ACLs small by using credential overrides only for
  exceptions.

### Cons

- The current repo has users but not general-purpose roles/groups.
- The user's request is credential-specific, so this may be one abstraction too
  high.
- Leaked credentials inherit the full user entitlement unless credentials are
  separately narrowed.
- Role management adds dashboard and API complexity.
- Service credentials may not map naturally to human users.

## Option 10: Endpoint Or Model Runner ACLs

Control which credentials can use particular model runner endpoints.

### Implementation

Add ACLs to `ModelRunnerEndpoint`:

```json
{
  "AccessMode": "DefaultPermit",
  "AllowedCredentialIds": ["cred_gpu_app"],
  "DeniedCredentialIds": ["cred_free_tier"],
  "AllowedActions": ["Completions"]
}
```

Enforcement location:

- During endpoint candidate screening in `RoutingDecisionService`.
- Add candidate exclusion code such as `CredentialEndpointAccessDenied`.
- Session affinity must re-check endpoint access before reusing a pinned
  endpoint.

This can be combined with endpoint labels:

- credential label `gpu`
- endpoint label `gpu`
- policy says credential needs matching label to route to endpoint

### Pros

- Protects expensive GPU hosts, private infrastructure, and upstream provider
  accounts.
- Fits existing endpoint candidate evidence and load-balancing diagnostics.
- Useful when the same model exists on cheap and expensive backends.
- Can prevent credentials from consuming capacity on restricted endpoints.

### Cons

- It does not directly answer "which model can this credential use?"
- If one endpoint hosts many models, endpoint ACLs are too coarse.
- If the same model is hosted on multiple endpoints, policy must be duplicated.
- Endpoint ACLs should complement, not replace, model ACLs.
- If endpoint screening happens before model authorization, denied users may
  still learn endpoint availability through error differences unless errors are
  normalized.

## Option 11: Per-Credential VMRs With Strict Mode

Use VMRs as access boundaries. Each credential or credential class gets a VMR
that attaches only allowed model definitions and enables `StrictMode`.

### Implementation

The current VMR model already supports:

- `ModelDefinitionIds`
- `StrictMode`
- `AllowEmbeddings`
- `AllowCompletions`
- `AllowModelManagement`
- endpoint attachments
- model configuration mappings

Add a small VMR access field:

```json
{
  "AllowedCredentialIds": ["cred_app_a"],
  "StrictMode": true,
  "ModelDefinitionIds": ["md_llama", "md_mistral"]
}
```

Operators would create:

- `/v1.0/api/app-a/` for App A with App A models.
- `/v1.0/api/app-b/` for App B with App B models.
- Separate credentials for each app.

### Pros

- Reuses the current VMR model-definition attachment and strict mode concepts.
- Easy to reason about at the route level.
- Good for small numbers of applications or isolated tenants.
- Different VMRs can have different load-balancing policies, timeouts, and
  request-type gates.
- Low conceptual risk if VMR-level credential ACLs are added.

### Cons

- Does not work securely without a VMR credential allow list, because a
  credential could call another VMR path.
- Can create a large number of VMRs.
- Duplicates endpoint attachments and load-balancing policies.
- Not ideal for many credentials with overlapping model sets.
- Operationally noisy for dashboards and request history unless naming is
  disciplined.

## Option 12: External Policy Engine

Delegate decisions to an external policy system such as OPA, Cedar, or a custom
HTTP authorization service.

### Implementation

Add an interface:

```text
IModelAccessDecisionProvider
```

Context sent to the provider:

```json
{
  "tenantId": "default",
  "credentialId": "cred_app_a",
  "credentialLabels": ["tier:paid"],
  "userId": "usr_app",
  "userIsAdmin": false,
  "vmrId": "vmr_chat",
  "requestType": "OpenAIChatCompletions",
  "action": "Completions",
  "requestedModel": "llama3.2:latest",
  "effectiveModel": "llama3.2:latest",
  "modelDefinitionId": "md_llama"
}
```

Provider response:

```json
{
  "Decision": "Permit",
  "ReasonCode": "MatchedPaidTier",
  "Reason": "Credential has required tier label."
}
```

Operational settings:

- endpoint URL
- timeout
- cache TTL
- fail-open/fail-closed mode
- health check
- monitor mode

### Pros

- Maximum flexibility.
- Centralizes policy for organizations that already use external authz.
- Can support complex contextual rules without growing Conductor's schema.
- Lets Conductor focus on routing while policy teams own authorization logic.
- Good for enterprise deployments.

### Cons

- Adds a runtime dependency to every proxied request unless aggressively cached.
- Harder to run locally and in simple Docker deployments.
- Failure modes are more complex.
- Dashboard cannot easily validate or explain external rules unless the policy
  engine returns rich diagnostics.
- Versioning and testing policies becomes an operational discipline outside this
  repository.

## Option 13: Metadata-Driven Prototype

Use existing `Metadata` fields to store access control objects without adding
new database columns or resources.

### Implementation

Example on `Credential.Metadata`:

```json
{
  "AccessControl": {
    "Mode": "DefaultDeny",
    "AllowedModelDefinitionIds": ["md_llama"],
    "DeniedModelNames": ["secret-*"]
  }
}
```

Example on `ModelDefinition.Metadata`:

```json
{
  "AccessControl": {
    "Mode": "DefaultDeny",
    "AllowedCredentialIds": ["cred_app_a"]
  }
}
```

Add a service that reads these keys and evaluates them. Validation could warn
when known metadata ACL fields reference missing credentials or model
definitions.

### Pros

- Fastest path to experiment.
- No schema migrations.
- No database interface changes.
- Backups already include metadata.
- Can help discover real policy needs before designing a permanent schema.

### Cons

- Weak typing and weak validation.
- Poor dashboard discoverability.
- Typos can silently create fail-open or fail-closed behavior depending on the
  default.
- Hard to query efficiently.
- Blurs the meaning of free-form metadata.
- Should not be the final design for security-sensitive enforcement.

## Cross-Cutting Concerns

### Proxy Authentication Must Be Closed First

Credential-to-model authorization depends on reliable credential identity. The
proxy path should explicitly authenticate before routing:

- Bearer token in `Authorization`.
- Gemini-compatible `?key=` fallback.
- Optional decision for header-based auth.
- Tenant match between credential tenant and VMR tenant.
- Clear admin bypass behavior.

If proxy auth remains optional or metadata is absent, model access checks will
either fail open or deny legitimate traffic unpredictably.

### Model List Endpoints Need Special Handling

List-model requests do not target a single model.

Possible behaviors:

- `Deny`: return 403 unless the credential has broad list permission.
- `Filter`: call upstream and remove models not allowed for the credential.
- `Synthesize`: return the VMR's attached `ModelDefinition` names instead of
  upstream inventory.
- `PassThrough`: allow listing even if generation is restricted.

Recommended default for enforcement mode is `Filter` when practical, otherwise
`Synthesize` for strict VMRs. `PassThrough` leaks model inventory.

### Unknown Models Need A Policy

When a VMR is not strict, a request can name a model that has no
`ModelDefinition`.

Possible behaviors:

- Deny unknown models under default-deny.
- Permit unknown models under default-permit.
- Require name or pattern rules for unknown models.
- Require all enforceable VMRs to use `StrictMode`.

The safest approach is: default-deny tenants should require strict VMRs or
explicit model-name/pattern allow rules.

### Deny Should Win

For options with both permit and deny lists:

1. Disabled mode bypasses all checks.
2. Explicit deny wins.
3. Explicit allow permits.
4. No match uses nearest default:
   - credential override
   - VMR policy default
   - tenant default
   - server default

This order is predictable and matches common ACL behavior.

### Normalized Tables Versus JSON Columns

The current resource models use JSON columns for many list/dictionary fields.
That makes embedded ACLs easy to add.

JSON columns are acceptable for small ACLs, but normalized tables are better
when:

- there are many credentials or models
- operators need reverse lookup
- policies need priority and matching evidence
- dashboard filtering matters
- enforcement must be fast without loading large resource blobs

Dedicated policy resources should use normalized tables. Simple credential or
VMR ACLs can start with JSON columns if the expected scale is small.

### Audit And Explainability

The existing `RoutingDecision` and request history system are strong foundations.
Any enforcement option should record:

- access decision stage
- subject credential ID/name
- requested model
- effective model
- matched rule or policy ID
- denial reason code
- whether monitor mode would have denied

This makes the dashboard and API explain why a request was denied without
requiring log access.

### Dashboard UX

Avoid making operators hand-edit large JSON blobs for final enforcement.

Useful UI patterns:

- On a Credential page: "Allowed models" and "Denied models" multi-selects.
- On a Model Definition page: "Allowed credentials" and "Denied credentials".
- On a VMR page: a matrix of credentials or labels to models/actions.
- On a Policy page: ordered rules with effect, subject, resource, action, and
  priority.
- In request history: surface access denial code and matched rule.
- In effective VMR preview: show whether access control is disabled, monitor,
  or enforced.

### API And SDK Compatibility

Backward-compatible rollout should:

- Keep new fields optional.
- Default to current behavior unless access control is enabled.
- Avoid changing existing credential bearer token semantics.
- Version docs clearly.
- Add SDK support after server API shape stabilizes.

### Performance

Avoid per-request full-table scans.

Recommended:

- Use data already loaded during authentication and VMR resolution.
- Cache policy/rule data by tenant and VMR.
- Invalidate by update time or short TTL.
- Keep access checks pure and fast.
- Defer expensive diagnostics to explain routes when possible.

## Recommended Paths

### Best Near-Term Path

Use Option 1 plus Option 5:

- Add proxy authentication enforcement.
- Add `AccessControl` global settings with `Disabled`, `Monitor`, and
  `Enforce`.
- Add credential-attached model ACLs.
- Start with `DefaultPermit` and explicit deny rules.
- Add monitor-mode reporting from request history.
- Later enable `DefaultDeny` on selected VMRs or tenants.

This is the smallest product-quality path that directly answers "which
credential can access which models" without creating an entire policy subsystem.

### Best Long-Term Path

Use Option 4 plus Option 5, with Option 8 support:

- Create first-class model access policy resources.
- Prefer `ModelDefinition.Id` resources.
- Allow model name/pattern resources for non-strict or external models.
- Support credential IDs and credential labels as subjects.
- Scope rules optionally to VMRs.
- Support action-specific permissions.
- Run with global `Monitor` mode first, then default-deny enforcement.

This gives the clearest long-term auditability, API shape, dashboard UX, and
scalability.

### Best High-Security Path

Use Option 4 with default-deny semantics:

- Require credential auth for proxy traffic.
- Deny header-authenticated proxy traffic unless user subject rules exist.
- Require strict VMRs for protected routes.
- Use explicit model definition allow rules.
- Filter or synthesize model list responses.
- Treat unknown models as denied.
- Make deny rules win.

This is the safest approach, but it needs careful migration tooling and strong
operator diagnostics.

## Suggested Phased Implementation

1. Foundation:
   - Authenticate the proxy path and populate `AuthenticationResult`.
   - Enforce tenant match between credential and VMR.
   - Add `ModelAccessControlService`.
   - Add access-control timeline and denial codes.

2. Model resolution:
   - Extract or refactor model resolution so requested/effective model can be
     known before endpoint selection.
   - Ensure OpenAI, Gemini, and Ollama model extraction is covered.
   - Decide list-model, show-model, pull, delete, and load-model behavior.

3. Initial policy storage:
   - For quickest value, add credential-attached ACLs.
   - For strategic value, add dedicated policy resources.

4. Observability:
   - Add monitor mode.
   - Record would-deny decisions.
   - Add request history filters and dashboard visibility.

5. Rollout:
   - Enable in monitor mode.
   - Generate suggested allow rules from request history.
   - Move selected tenants or VMRs to enforcement.
   - Move sensitive tenants to default deny.

6. Hardening:
   - Add response filtering for list-model endpoints.
   - Add caching.
   - Add SDK support.
   - Add backup/restore coverage.
   - Add migration tests for all new schema fields/tables.

## Minimum Test Matrix

Any implementation should include tests for:

- Unauthenticated proxy request returns 401 before routing.
- Bearer credential can access an allowed model.
- Bearer credential is denied for a denied model with HTTP 403.
- Gemini `?key=` authenticates as a credential and is checked.
- Header-based auth behavior is explicit and tested.
- Credential from tenant A cannot use tenant B VMR.
- Global admin bypass on and off.
- Tenant admin bypass on and off.
- VMR `StrictMode` plus ACL interactions.
- Single attached model definition mutation plus ACL on effective model.
- Explicit deny beats explicit allow.
- Default permit allows unmatched models.
- Default deny denies unmatched models.
- Unknown model behavior.
- OpenAI list models behavior.
- Gemini list models behavior.
- Ollama tags/list running models behavior.
- Ollama show, pull, and delete behavior if included.
- Session affinity re-checks access before reusing a pinned endpoint.
- Request history records credential, requested/effective model, access denial,
  and matched policy/rule.
- Validation rejects cross-tenant references.
- Backup and restore preserve ACL data.

## Final Recommendation

For this repository, the strongest design direction is:

1. Fix proxy authentication and tenant binding as a prerequisite.
2. Introduce a central access-control service used by the routing path.
3. Start with credential-attached ACLs if speed matters.
4. Move toward dedicated model access policy resources if the product needs
   scale, auditability, labels, route-specific overrides, and action-specific
   permissions.
5. Keep global `Monitor`, `DefaultPermit`, and `DefaultDeny` modes regardless
   of storage choice so rollout can be controlled safely.

