# Model Access Policies

Model access policies control which authenticated subjects can use which models through a virtual model runner (VMR). They are tenant-scoped, attached to VMRs, and evaluated before Conductor selects an endpoint or forwards a request to a provider.

Use model access policies when you need controls such as:

- A service credential can only call approved production models.
- Search workers can create embeddings but cannot run chat completions.
- Contractors can use general models but not restricted or high-cost models.
- Only operations users can load, unload, or manage local models.
- Canary credentials can test a new VMR before broad rollout.

## Core Concepts

A `ModelAccessPolicy` contains a default decision and an ordered list of rules.

Each rule matches three dimensions:

- Subject: `Credential`, `CredentialLabel`, `User`, `UserLabel`, `Tenant`, or `Any`
- Resource: `ModelDefinition`, `ModelName`, `ModelLabel`, `VirtualModelRunner`, or `Any`
- Action: `Completions`, `Embeddings`, `ListModels`, `ShowModel`, `LoadModel`, `UnloadModel`, or `ModelManagement`

The subject, resource, and action must all match for a rule to apply.

Rule selection:

- Higher `Priority` values are evaluated first.
- If allow and deny rules match at the same priority, `Deny` wins.
- If no active rule matches, Conductor uses the policy `DefaultDecision`.
- If no active policy is attached to a VMR, Conductor uses `ModelAccessControl.DefaultDecision`.

Policy enforcement is controlled by server configuration:

```json
{
  "ModelAccessControl": {
    "Enabled": true,
    "Mode": "Monitor",
    "DefaultDecision": "Permit",
    "RequireCredentialForProxy": true,
    "UnknownModelBehavior": "Deny",
    "ListModelsBehavior": "Filter",
    "CacheTtlMs": 30000,
    "AllowAdministratorBypass": false,
    "AllowGlobalAdministratorBypass": false
  }
}
```

Recommended rollout:

1. Create policies with `ModelAccessControl.Mode` set to `Monitor`.
2. Attach policies to a small number of VMRs.
3. Inspect request history and routing explanations for would-deny decisions.
4. Switch to `Enforce` after expected traffic is clean.

In `Monitor` mode, denied decisions are recorded but requests continue. In `Enforce` mode, authenticated denied requests return `403 Forbidden`. Missing or invalid authentication returns `401 Unauthorized`.

## Creating Policies in the Dashboard

1. Open the dashboard and sign in as a tenant admin or global admin.
2. Go to `Model Access Policies`.
3. Select `Create Policy`.
4. Choose the tenant, name, default decision, and active state.
5. Add one or more rules.
6. Use `Validate` to check references and rule shape.
7. Save the policy.
8. Go to `Virtual Model Runners`.
9. Create or edit a VMR and set its `Model Access Policy`.
10. Use the VMR effective configuration and routing explanation views to confirm the attached policy.

Labels and tags on policies are edited as structured rows in the dashboard. Rule selector fields, such as `SubjectSelector` and `ResourceSelector`, are intentionally JSON because they represent matcher expressions.

## Creating Policies with the API

Create a policy:

```bash
curl -X POST "http://localhost:9000/v1.0/modelaccesspolicies" \
  -H "Content-Type: application/json" \
  -H "x-admin-apikey: conductoradmin" \
  -d @policy.json
```

Validate a policy draft without saving:

```bash
curl -X POST "http://localhost:9000/v1.0/modelaccesspolicies/validate?tenantId=ten_example" \
  -H "Content-Type: application/json" \
  -H "x-admin-apikey: conductoradmin" \
  -d @policy.json
```

Evaluate a saved policy:

```bash
curl -X POST "http://localhost:9000/v1.0/modelaccesspolicies/map_example/evaluate?tenantId=ten_example" \
  -H "Content-Type: application/json" \
  -H "x-admin-apikey: conductoradmin" \
  -d '{
    "CredentialId": "cred_customer_support",
    "VirtualModelRunnerId": "vmr_chat",
    "ModelDefinitionId": "md_gpt_4o_mini",
    "RequestedModel": "gpt-4o-mini",
    "Action": "Completions"
  }'
```

Evaluate effective access through the policy attached to a VMR:

```bash
curl "http://localhost:9000/v1.0/modelaccesspolicies/effective?tenantId=ten_example&credentialId=cred_customer_support&vmrId=vmr_chat&modelName=gpt-4o-mini&action=Completions" \
  -H "x-admin-apikey: conductoradmin"
```

Delete an attached policy only after detaching it, or pass `forceDetach=true`:

```bash
curl -X DELETE "http://localhost:9000/v1.0/modelaccesspolicies/map_example?tenantId=ten_example&forceDetach=true" \
  -H "x-admin-apikey: conductoradmin"
```

## Matching Labels and Names

For `CredentialLabel`, `UserLabel`, and `ModelLabel`, either use `SubjectId` or `ResourceId` as the label value:

```json
{
  "SubjectType": "CredentialLabel",
  "SubjectId": "service:search"
}
```

Or use selectors:

```json
{
  "SubjectType": "CredentialLabel",
  "SubjectSelector": {
    "labels": "service:search,environment:prod"
  }
}
```

For `ModelName`, use `ResourceId` for an exact model-name match:

```json
{
  "ResourceType": "ModelName",
  "ResourceId": "gpt-4o-mini"
}
```

Or use `ResourceSelector` for text matching:

```json
{
  "ResourceType": "ModelName",
  "ResourceSelector": {
    "prefix": "gpt-4"
  }
}
```

Supported selector keys are `label`, `labels`, `value`, `equals`, `prefix`, and `contains`.

## Examples

These examples use placeholder IDs. Replace them with tenant, credential, user, VMR, and model definition IDs from your environment.

### 1. Default-Deny Production Chat Access

Use this when a production VMR should only allow customer-support service credentials to use approved production chat models.

```json
{
  "TenantId": "ten_prod",
  "Name": "Production support chat access",
  "Description": "Only customer-support service credentials can use production chat models.",
  "DefaultDecision": "Deny",
  "Active": true,
  "Labels": ["production", "support"],
  "Tags": {
    "owner": "support-platform",
    "change-ticket": "SEC-1042"
  },
  "Rules": [
    {
      "Name": "Allow support services to see production models",
      "Priority": 200,
      "Effect": "Allow",
      "SubjectType": "CredentialLabel",
      "SubjectId": "service:customer-support",
      "ResourceType": "ModelLabel",
      "ResourceId": "production",
      "Actions": ["ListModels"],
      "Active": true
    },
    {
      "Name": "Allow support services to chat with production models",
      "Priority": 200,
      "Effect": "Allow",
      "SubjectType": "CredentialLabel",
      "SubjectId": "service:customer-support",
      "ResourceType": "ModelLabel",
      "ResourceId": "production",
      "Actions": ["Completions"],
      "Active": true
    }
  ]
}
```

Attach this policy to the production chat VMR. With `ListModelsBehavior` set to `Filter` or `Synthesize`, the `ListModels` rule controls what models appear to the caller.

### 2. Embeddings-Only Search Worker

Use this when an indexing worker should create embeddings but should not use chat/completions.

```json
{
  "TenantId": "ten_prod",
  "Name": "Search service embeddings only",
  "Description": "Search indexing workers may use embedding models and nothing else.",
  "DefaultDecision": "Deny",
  "Active": true,
  "Rules": [
    {
      "Name": "Search workers can list embedding models",
      "Priority": 100,
      "Effect": "Allow",
      "SubjectType": "CredentialLabel",
      "SubjectId": "service:search-indexer",
      "ResourceType": "ModelLabel",
      "ResourceId": "embedding",
      "Actions": ["ListModels"],
      "Active": true
    },
    {
      "Name": "Search workers can create embeddings",
      "Priority": 100,
      "Effect": "Allow",
      "SubjectType": "CredentialLabel",
      "SubjectId": "service:search-indexer",
      "ResourceType": "ModelLabel",
      "ResourceId": "embedding",
      "Actions": ["Embeddings"],
      "Active": true
    }
  ]
}
```

This pattern works best when embedding model definitions carry a label such as `embedding`, and search-worker credentials carry `service:search-indexer`.

### 3. Deny Restricted Models to Contractors

Use this when most tenant users can use general models, but users labeled `contractor` must not use models labeled `restricted`.

```json
{
  "TenantId": "ten_prod",
  "Name": "Contractor restricted model guardrail",
  "Description": "Contractors can use general models but not restricted models.",
  "DefaultDecision": "Permit",
  "Active": true,
  "Rules": [
    {
      "Name": "Contractors cannot use restricted models",
      "Priority": 500,
      "Effect": "Deny",
      "SubjectType": "UserLabel",
      "SubjectId": "contractor",
      "ResourceType": "ModelLabel",
      "ResourceId": "restricted",
      "Actions": ["Completions", "Embeddings", "ListModels", "ShowModel"],
      "Active": true
    }
  ]
}
```

Because this policy uses `DefaultDecision: Permit`, only the explicit restricted-model case is blocked. This is useful for guardrails on an otherwise open VMR.

### 4. High-Cost Model Guardrail

Use this when you want to block expensive model names for general callers while still allowing lower-cost models.

```json
{
  "TenantId": "ten_prod",
  "Name": "Block high-cost models",
  "Description": "Prevent broad access to large or premium models by name.",
  "DefaultDecision": "Permit",
  "Active": true,
  "Rules": [
    {
      "Name": "Block GPT-4 family by prefix",
      "Priority": 300,
      "Effect": "Deny",
      "SubjectType": "Any",
      "ResourceType": "ModelName",
      "ResourceSelector": {
        "prefix": "gpt-4"
      },
      "Actions": ["Completions", "ListModels"],
      "Active": true
    },
    {
      "Name": "Block 70B models by name fragment",
      "Priority": 300,
      "Effect": "Deny",
      "SubjectType": "Any",
      "ResourceType": "ModelName",
      "ResourceSelector": {
        "contains": "70b"
      },
      "Actions": ["Completions", "ListModels"],
      "Active": true
    }
  ]
}
```

For a more permissive exception, add an allow rule for a specific credential or user label at a higher priority than the deny rules.

```json
{
  "Name": "Finance analytics can use premium models",
  "Priority": 400,
  "Effect": "Allow",
  "SubjectType": "CredentialLabel",
  "SubjectId": "team:finance-analytics",
  "ResourceType": "ModelName",
  "ResourceSelector": {
    "prefix": "gpt-4"
  },
  "Actions": ["Completions", "ListModels"],
  "Active": true
}
```

### 5. Operations-Only Model Management

Use this for local model runners where normal callers can use models, but only operations users can load, unload, or manage models.

```json
{
  "TenantId": "ten_ops",
  "Name": "Operations model management",
  "Description": "All callers can use active models. Only ML operations can manage model lifecycle.",
  "DefaultDecision": "Deny",
  "Active": true,
  "Rules": [
    {
      "Name": "Allow normal inference for all callers",
      "Priority": 100,
      "Effect": "Allow",
      "SubjectType": "Any",
      "ResourceType": "Any",
      "Actions": ["Completions", "Embeddings", "ListModels", "ShowModel"],
      "Active": true
    },
    {
      "Name": "Allow ML operations model lifecycle",
      "Priority": 200,
      "Effect": "Allow",
      "SubjectType": "UserLabel",
      "SubjectId": "role:ml-ops",
      "ResourceType": "Any",
      "Actions": ["LoadModel", "UnloadModel", "ModelManagement"],
      "Active": true
    }
  ]
}
```

Attach this policy to VMRs that expose Ollama model-management operations.

### 6. Canary Access to a New VMR

Use this when a new VMR should be visible only to credentials labeled `canary` until rollout is complete.

```json
{
  "TenantId": "ten_prod",
  "Name": "Canary VMR access",
  "Description": "Only canary credentials can use the new VMR.",
  "DefaultDecision": "Deny",
  "Active": true,
  "Rules": [
    {
      "Name": "Canary credentials can use this VMR",
      "Priority": 100,
      "Effect": "Allow",
      "SubjectType": "CredentialLabel",
      "SubjectId": "canary",
      "ResourceType": "VirtualModelRunner",
      "ResourceId": "vmr_new_chat",
      "Actions": ["Completions", "ListModels", "ShowModel"],
      "Active": true
    }
  ]
}
```

This rule can also set `VirtualModelRunnerId` to the same VMR ID if you want an additional rule-level scope.

## Testing a Policy

Before enforcing a policy:

1. Validate it with the dashboard `Validate` button or `POST /v1.0/modelaccesspolicies/validate`.
2. Attach it to a VMR.
3. Run `GET /v1.0/modelaccesspolicies/effective` with representative credential, user, VMR, model, and action values.
4. Send real traffic while `Mode` is `Monitor`.
5. Inspect request history for policy name, rule name, decision, denial reason, and `WouldDeny`.
6. Switch `Mode` to `Enforce` only after expected traffic is permitted.

Useful effective-access query:

```bash
curl "http://localhost:9000/v1.0/modelaccesspolicies/effective?tenantId=ten_prod&credentialId=cred_search_indexer&vmrId=vmr_embeddings&modelName=text-embedding-3-small&action=Embeddings" \
  -H "x-admin-apikey: conductoradmin"
```

Expected allow response shape:

```json
{
  "Decision": "Permit",
  "Effect": "Allow",
  "Mode": "Monitor",
  "PolicyName": "Search service embeddings only",
  "RuleName": "Search workers can create embeddings",
  "ReasonCode": "MatchedAllowRule",
  "WouldDeny": false
}
```

Expected monitor-mode deny response shape:

```json
{
  "Decision": "Deny",
  "Effect": null,
  "Mode": "Monitor",
  "DefaultSource": "Policy",
  "PolicyName": "Search service embeddings only",
  "ReasonCode": "PolicyDefaultDeny",
  "WouldDeny": true
}
```

## Practical Tips

- Prefer `DefaultDecision: Deny` for service-specific VMRs and narrow allow rules.
- Prefer `DefaultDecision: Permit` only for broad VMRs where you need targeted guardrails.
- Include `ListModels` in allow rules when callers should see allowed models in filtered or synthesized model-list responses.
- Label credentials, users, and model definitions consistently. Good labels are stable identifiers such as `service:search-indexer`, `team:finance`, `role:ml-ops`, `production`, `restricted`, and `embedding`.
- Use higher priority allow rules for explicit exceptions, then lower priority deny or default-deny behavior for the general case.
- Keep deny rules for sensitive models at higher priority than broad allow rules.
- Validate references before saving. Create/update validation checks tenant scope and references for credentials, users, model definitions, and VMRs.
- Deleting a policy attached to a VMR returns `409 Conflict` unless you detach it first or use `forceDetach=true`.
