# Conductor REST API

This document describes the public HTTP APIs exposed by Conductor, including authentication, tenant scoping, core resources, operational endpoints, and proxied Virtual Model Runner APIs.

Primary audience: developers, automation engineers, and sysadmins integrating directly with Conductor.

## API version and base URL

The management API is versioned under `/v1.0`.

Examples in this document assume:

```text
http://localhost:9000
```

So a full resource URL looks like:

```text
http://localhost:9000/v1.0/virtualmodelrunners
```

## Content type and enum format

- Request and response bodies use JSON unless noted otherwise.
- Enum values are serialized as strings such as `OpenAI`, `LeastRecentlyUsed`, or `FailClosed`.
- Successful deletes return HTTP `204 No Content`.
- Successful creates generally return HTTP `201 Created`.

## Authentication

Conductor supports four practical authentication styles.

### 1. Bearer token authentication

Use a credential bearer token in the `Authorization` header:

```http
Authorization: Bearer cred_xxx_or_token_value
```

This is the standard authentication mode for tenant users and API clients.

### 2. Header-based user authentication

You can also authenticate a tenant user directly with headers:

```http
x-tenant-id: default
x-email: admin@conductor
x-password: password
```

This is less common than bearer tokens, but it is supported by the server.

### 3. Administrator header authentication

System administrator routes accept:

```http
x-admin-email: admin@example.com
x-admin-password: secret
```

### 4. Administrator API keys

If admin API keys are configured in server settings, they can be sent as either:

```http
x-admin-apikey: your-admin-api-key
```

or:

```http
Authorization: Bearer your-admin-api-key
```

### Gemini-compatible `?key=` behavior

For bearer-token-authenticated proxied Gemini requests, Conductor also checks `?key=` if no bearer token is present. This is intended to support Gemini-style clients.

## Authorization levels

Conductor's route permissions are split across four levels:

| Level | Meaning |
| --- | --- |
| `Public` | No authentication required. |
| `Authenticated` | Any authenticated tenant user, global admin user, or system administrator. |
| `TenantAdmin` | A user with `IsTenantAdmin=true`, any global admin user, or any system administrator. |
| `GlobalAdmin` | A user with `IsAdmin=true` or any system administrator. |
| `AdminOnly` | Only system administrator authentication or admin API keys. |

Important distinction:

- A global admin user is a normal tenant user credential whose user record has `IsAdmin=true`.
- A system administrator is an `Administrator` account authenticated with admin headers or an admin API key.

## Tenant scoping rules

Most resources are tenant-scoped:

- users
- credentials
- model runner endpoints
- model definitions
- model configurations
- load-balancing policies
- virtual model runners
- request history

Rules:

- Normal tenant users are always scoped to their own tenant.
- Global admin users and system administrators can work across tenants.
- On create routes, cross-tenant callers should supply `TenantId` in the request body.
- On read, update, delete, list, and health routes for tenant-scoped resources, cross-tenant callers can use `?tenantId=<tenant-id>`.

Example:

```text
GET /v1.0/modelrunnerendpoints/mre_123?tenantId=default
```

## Common list query parameters

Many list endpoints accept these query parameters:

| Query parameter | Type | Notes |
| --- | --- | --- |
| `maxResults` | integer | Maximum items to return. Server clamps to `1..1000`. Default is `100`. |
| `continuationToken` | string | Opaque pagination token from a previous response. |
| `nameFilter` | string | Partial name filter. Only available on routes whose controller supports it. |
| `activeFilter` | boolean | Filter active vs inactive resources. |

Typical list response shape:

```json
{
  "Data": [],
  "ContinuationToken": null,
  "TotalCount": 0,
  "HasMore": false
}
```

## Common error behavior

Typical response codes:

| Status | Meaning |
| --- | --- |
| `200` | Success |
| `201` | Resource created |
| `204` | Delete succeeded, no body |
| `400` | Validation failure or malformed request |
| `401` | Authentication or authorization failure |
| `403` | Authenticated caller is forbidden, including model access denials on proxied requests |
| `404` | Resource not found |
| `409` | Conflict, such as deleting an attached model access policy without `forceDetach=true` |
| `500` | Unexpected server failure |

The dashboard client expects error payloads to include `Message` or `message`, but exact error bodies may vary by route.

## Core resource shapes

These are the primary JSON resources used across the API.

### Tenant

```jsonc
{
  "Id": "default",                // server-generated unless explicitly provided
  "Name": "Default Tenant",       // required
  "Active": true,
  "Labels": ["production"],
  "Tags": { "region": "us-west" },
  "Metadata": { "owner": "ops" },
  "CreatedUtc": "2026-05-18T12:00:00Z",
  "LastUpdateUtc": "2026-05-18T12:00:00Z"
}
```

### User

```jsonc
{
  "Id": "usr_xxx",
  "TenantId": "default",
  "FirstName": "Admin",           // required
  "LastName": "User",             // required
  "Email": "admin@conductor",     // required
  "Password": "password",         // required in create/update payloads
  "Active": true,
  "IsAdmin": true,
  "IsTenantAdmin": true,
  "Labels": [],
  "Tags": {},
  "Metadata": null,
  "CreatedUtc": "2026-05-18T12:00:00Z",
  "LastUpdateUtc": "2026-05-18T12:00:00Z"
}
```

### Credential

```jsonc
{
  "Id": "cred_xxx",
  "TenantId": "default",
  "UserId": "usr_xxx",
  "Name": "Primary API Key",
  "BearerToken": "token-value",   // generated if omitted on create
  "Active": true,
  "Labels": [],
  "Tags": {},
  "Metadata": null,
  "CreatedUtc": "2026-05-18T12:00:00Z",
  "LastUpdateUtc": "2026-05-18T12:00:00Z"
}
```

### Model Runner Endpoint

```jsonc
{
  "Id": "mre_xxx",
  "TenantId": "default",
  "Name": "GPU Host A",                 // required
  "Hostname": "gpu-a.local",            // required
  "Port": 11434,
  "ApiKey": null,
  "ApiType": "Ollama",                  // Ollama, OpenAI, Gemini, vLLM
  "UseSsl": false,
  "TimeoutMs": 60000,
  "Active": true,
  "HealthCheckUrl": "/",
  "HealthCheckMethod": "GET",
  "HealthCheckIntervalMs": 5000,
  "HealthCheckTimeoutMs": 5000,
  "HealthCheckExpectedStatusCode": 200,
  "UnhealthyThreshold": 2,
  "HealthyThreshold": 2,
  "HealthCheckUseAuth": false,
  "MaxParallelRequests": 4,
  "Weight": 1,
  "ServiceState": "Normal",             // Normal, Draining, Quarantined
  "RigMonitor": {
    "Enabled": true,
    "HostnameOverride": null,
    "Port": 9990,
    "UseSsl": false,
    "TimeoutMs": 5000,
    "CollectDuringHealthCheck": true,
    "RequireReadyz": true,
    "HealthAffectedByRigMonitor": false,
    "MaxTelemetryAgeMs": 30000,
    "CapabilitiesRefreshIntervalMs": 60000,
    "TelemetryProfile": "Full",         // Basic, GpuPlacement, OllamaPlacement, Full, Custom
    "TelemetrySelectors": []
  },
  "Labels": [],
  "Tags": {},
  "Metadata": null,
  "CreatedUtc": "2026-05-18T12:00:00Z",
  "LastUpdateUtc": "2026-05-18T12:00:00Z"
}
```

### Model Definition

```jsonc
{
  "Id": "md_xxx",
  "TenantId": "default",
  "Name": "llama3.2:latest",      // required
  "SourceUrl": null,
  "Family": "llama",
  "ParameterSize": "8B",
  "QuantizationLevel": "Q4_K_M",
  "ContextWindowSize": 8192,
  "SupportsEmbeddings": false,
  "SupportsCompletions": true,
  "Active": true,
  "Labels": [],
  "Tags": {},
  "Metadata": null,
  "CreatedUtc": "2026-05-18T12:00:00Z",
  "LastUpdateUtc": "2026-05-18T12:00:00Z"
}
```

### Model Configuration

```jsonc
{
  "Id": "mc_xxx",
  "TenantId": "default",
  "Name": "Low Temperature",      // required
  "ContextWindowSize": 8192,
  "Temperature": 0.3,
  "TopP": 0.95,
  "TopK": 40,
  "RepeatPenalty": 1.1,
  "MaxTokens": 1024,
  "Model": null,                  // null means applies to any model
  "PinnedEmbeddingsProperties": {},
  "PinnedCompletionsProperties": {
    "temperature": 0.3
  },
  "Active": true,
  "Labels": [],
  "Tags": {},
  "Metadata": null,
  "CreatedUtc": "2026-05-18T12:00:00Z",
  "LastUpdateUtc": "2026-05-18T12:00:00Z"
}
```

### Load Balancing Policy

See [LOAD_BALANCING_POLICIES.md](LOAD_BALANCING_POLICIES.md) for the full schema, supported metrics, and authoring guidance.

Minimal example:

```jsonc
{
  "Id": "lbp_xxx",
  "TenantId": "default",
  "Name": "Least In-Flight",
  "Description": "Prefer the least busy endpoint.",
  "MaxTelemetryAgeMs": 30000,
  "Filters": [],
  "Ranking": [
    {
      "Metric": "health.inFlightRequests",
      "Direction": "Ascending",
      "Weight": 1.0
    }
  ],
  "FallbackMode": "UseLegacyLoadBalancingMode",
  "TieBreaker": "RoundRobin",
  "Active": true,
  "Labels": [],
  "Tags": {},
  "Metadata": null,
  "CreatedUtc": "2026-05-18T12:00:00Z",
  "LastUpdateUtc": "2026-05-18T12:00:00Z"
}
```

### Model Access Policy

Model access policies are tenant-scoped ACL resources attached to VMRs through `VirtualModelRunner.ModelAccessPolicyId`.

```jsonc
{
  "Id": "map_xxx",
  "TenantId": "default",
  "Name": "Production model access",
  "Description": "Default deny with explicit model grants.",
  "DefaultDecision": "Deny",
  "Active": true,
  "Rules": [
    {
      "Id": "mar_xxx",
      "TenantId": "default",
      "PolicyId": "map_xxx",
      "Name": "Finance credential can use chat model",
      "Priority": 100,
      "Effect": "Allow",
      "SubjectType": "CredentialLabel",
      "SubjectSelector": { "label": "finance" },
      "ResourceType": "ModelLabel",
      "ResourceSelector": { "label": "chat" },
      "VirtualModelRunnerId": null,
      "Actions": ["Completions", "ListModels"],
      "Active": true
    }
  ],
  "Labels": [],
  "Tags": {},
  "Metadata": null,
  "CreatedUtc": "2026-06-13T12:00:00Z",
  "LastUpdateUtc": "2026-06-13T12:00:00Z"
}
```

Supported enum values:

- `DefaultDecision`: `Permit`, `Deny`
- `Effect`: `Allow`, `Deny`
- `SubjectType`: `Credential`, `CredentialLabel`, `User`, `UserLabel`, `Tenant`, `Any`
- `ResourceType`: `ModelDefinition`, `ModelName`, `ModelLabel`, `VirtualModelRunner`, `Any`
- `Actions`: `Completions`, `Embeddings`, `ListModels`, `ShowModel`, `LoadModel`, `UnloadModel`, `ModelManagement`

Selector objects support simple keys such as `label`, `labels`, `value`, `equals`, `prefix`, and `contains`.

### Virtual Model Runner

```jsonc
{
  "Id": "vmr_xxx",
  "TenantId": "default",
  "Name": "gpu-chat",                     // required
  "Hostname": null,
  "BasePath": "/v1.0/api/gpu-chat/",      // required; exactly one segment after /v1.0/api/
  "ApiType": "OpenAI",
  "LoadBalancingMode": "RoundRobin",      // RoundRobin, Random, FirstAvailable, LeastRecentlyUsed
  "LoadBalancingPolicyId": "lbp_xxx",
  "ModelAccessPolicyId": "map_xxx",
  "ModelRunnerEndpointIds": ["mre_a", "mre_b"],
  "ModelConfigurationIds": ["mc_default"],
  "ModelConfigurationMappings": {
    "llama3.2:latest": "mc_default"
  },
  "ModelDefinitionIds": ["md_llama"],
  "TimeoutMs": 300000,
  "AllowEmbeddings": true,
  "AllowCompletions": true,
  "AllowModelManagement": false,
  "StrictMode": false,
  "SessionAffinityMode": "None",          // None, SourceIP, ApiKey, Header
  "SessionAffinityHeader": null,
  "SessionTimeoutMs": 600000,
  "SessionMaxEntries": 10000,
  "RequestHistoryEnabled": true,
  "Active": true,
  "Labels": [],
  "Tags": {},
  "Metadata": null,
  "CreatedUtc": "2026-05-18T12:00:00Z",
  "LastUpdateUtc": "2026-05-18T12:00:00Z"
}
```

### Administrator response

```jsonc
{
  "Id": "adm_xxx",
  "Email": "admin@example.com",
  "FirstName": "System",
  "LastName": "Administrator",
  "Active": true,
  "CreatedUtc": "2026-05-18T12:00:00Z",
  "LastUpdateUtc": "2026-05-18T12:00:00Z"
}
```

Create/update administrator request bodies use:

```jsonc
{
  "Email": "admin@example.com",
  "Password": "secret",
  "FirstName": "System",
  "LastName": "Administrator",
  "Active": true
}
```

## Endpoint reference

### Health

| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| `GET` | `/` | Public | Root health check, returns `200 OK`. |
| `HEAD` | `/` | Public | Root health check without a body. |
| `GET` | `/health` | Public | Returns a small JSON health payload. |

Example `/health` response:

```json
{
  "status": "healthy"
}
```

### Authentication

| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| `POST` | `/v1.0/auth/login/credential` | Public | Exchange `TenantId`, `Email`, and `Password` for a bearer token. |
| `POST` | `/v1.0/auth/login/apikey` | Public | Validate a credential API key or configured admin API key. |
| `POST` | `/v1.0/auth/login/admin` | Public | Authenticate an `Administrator` by email/password. |

Credential login request:

```json
{
  "TenantId": "default",
  "Email": "admin@conductor",
  "Password": "password"
}
```

Credential login response:

```json
{
  "Success": true,
  "BearerToken": "token-value",
  "Tenant": {
    "Id": "default",
    "Name": "Default Tenant"
  },
  "User": {
    "Id": "usr_xxx",
    "Email": "admin@conductor",
    "FirstName": "Admin",
    "LastName": "User",
    "IsAdmin": true,
    "IsTenantAdmin": true
  }
}
```

Admin login response:

```json
{
  "Success": true,
  "Administrator": {
    "Id": "adm_xxx",
    "Email": "admin@example.com",
    "FirstName": "System",
    "LastName": "Administrator"
  }
}
```

Admin API key login response:

```json
{
  "Success": true,
  "IsAdmin": true,
  "ApiKey": "your-admin-api-key"
}
```

### Administrators

Auth level: `AdminOnly`

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/administrators` | List administrators. Query: `maxResults`, `continuationToken`, `activeFilter`. |
| `POST` | `/v1.0/administrators` | Create administrator. |
| `GET` | `/v1.0/administrators/{id}` | Read administrator. |
| `PUT` | `/v1.0/administrators/{id}` | Update administrator. |
| `DELETE` | `/v1.0/administrators/{id}` | Delete administrator. Cannot delete the currently authenticated administrator account. |

### Tenants

Auth:

- `GET /v1.0/tenants/{id}`: `GlobalAdmin`
- all other tenant routes: `AdminOnly`

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/tenants` | List tenants. Query: `maxResults`, `continuationToken`, `nameFilter`, `activeFilter`. |
| `POST` | `/v1.0/tenants` | Create tenant. |
| `GET` | `/v1.0/tenants/{id}` | Read tenant. |
| `PUT` | `/v1.0/tenants/{id}` | Update tenant. |
| `DELETE` | `/v1.0/tenants/{id}` | Delete tenant and all subordinate tenant data. |

### Users

Auth level: `TenantAdmin`

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/users` | List users. Query: `maxResults`, `continuationToken`, `nameFilter`, `activeFilter`, optional `tenantId` for cross-tenant callers. |
| `POST` | `/v1.0/users` | Create user. Cross-tenant callers supply `TenantId` in the body. |
| `GET` | `/v1.0/users/{id}` | Read user. Optional `tenantId` query for cross-tenant callers. |
| `PUT` | `/v1.0/users/{id}` | Update user. Cross-tenant callers supply `TenantId` in the body. |
| `DELETE` | `/v1.0/users/{id}` | Delete user. Optional `tenantId` query for cross-tenant callers. Also deletes associated credentials. |

### Credentials

Auth level: `TenantAdmin`

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/credentials` | List credentials. Query: `maxResults`, `continuationToken`, `activeFilter`, optional `tenantId`. |
| `POST` | `/v1.0/credentials` | Create credential. Cross-tenant callers supply `TenantId` and `UserId` in the body. `BearerToken` is generated if omitted. |
| `GET` | `/v1.0/credentials/{id}` | Read credential. Optional `tenantId` query. |
| `PUT` | `/v1.0/credentials/{id}` | Update credential. Optional new `BearerToken` must be unique. |
| `DELETE` | `/v1.0/credentials/{id}` | Delete credential. Optional `tenantId` query. |

### Model Runner Endpoints

Auth level: `Authenticated`

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/modelrunnerendpoints` | List endpoints. Query: `maxResults`, `continuationToken`, `nameFilter`, `activeFilter`, optional `tenantId`. |
| `POST` | `/v1.0/modelrunnerendpoints` | Create endpoint. Cross-tenant callers supply `TenantId` in the body. |
| `POST` | `/v1.0/modelrunnerendpoints/validate` | Validate an endpoint draft without saving it. Optional query: `tenantId`, `existingId`. |
| `GET` | `/v1.0/modelrunnerendpoints/{id}` | Read endpoint. Optional `tenantId` query. |
| `PUT` | `/v1.0/modelrunnerendpoints/{id}` | Update endpoint. Cross-tenant callers supply `TenantId` in the body. |
| `DELETE` | `/v1.0/modelrunnerendpoints/{id}` | Delete endpoint. Optional `tenantId` query. Also removes the endpoint from referenced VMRs. |
| `POST` | `/v1.0/modelrunnerendpoints/{id}/drain` | Move the endpoint to `Draining` service state. Optional `tenantId` query. |
| `POST` | `/v1.0/modelrunnerendpoints/{id}/resume` | Move the endpoint back to `Normal` service state. Optional `tenantId` query. |
| `POST` | `/v1.0/modelrunnerendpoints/{id}/quarantine` | Move the endpoint to `Quarantined` service state. Optional `tenantId` query. |
| `POST` | `/v1.0/modelrunnerendpoints/{id}/load-model` | Tenant-admin route to load or verify a model on one concrete endpoint. Optional `tenantId` query. |
| `GET` | `/v1.0/modelrunnerendpoints/{id}/ollama/models` | Tenant-admin route to list local models on an Ollama endpoint. Optional `tenantId` query. |
| `POST` | `/v1.0/modelrunnerendpoints/{id}/ollama/models/pull` | Tenant-admin route to pull a model onto an Ollama endpoint. Optional `tenantId` query. |
| `POST` | `/v1.0/modelrunnerendpoints/{id}/ollama/models/delete` | Tenant-admin route to delete a local model from an Ollama endpoint. Optional `tenantId` query. |
| `GET` | `/v1.0/modelrunnerendpoints/health` | List cached health state for all endpoints in scope. Optional `tenantId` query. |
| `GET` | `/v1.0/modelrunnerendpoints/{id}/health` | Get cached health state for one endpoint. Optional `tenantId` query. |
| `GET` | `/v1.0/modelrunnerendpoints/{id}/rigmonitor` | Get cached RigMonitor status for one endpoint. Optional `tenantId` query. |

Endpoint health response fields include:

- `EndpointId`
- `EndpointName`
- `IsHealthy`
- `LastCheckUtc`
- `LastHealthyUtc`
- `LastUnhealthyUtc`
- `FirstCheckUtc`
- `TotalUptimeMs`
- `TotalDowntimeMs`
- `UptimePercentage`
- `ConsecutiveSuccesses`
- `ConsecutiveFailures`
- `InFlightRequests`
- `MaxParallelRequests`
- `Weight`
- `ServiceState`
- `LastError`
- `LastStateChangeUtc`
- `History`
- `RigMonitor`

Health checks are coalesced by effective request. When multiple active model runner endpoints use the same health-check method, base URL, health-check path, and auth context, Conductor sends one upstream health-check HTTP request and applies the result to each endpoint's own expected status code, thresholds, service state, and history.

RigMonitor status response fields include:

- `Enabled`
- `BaseUrl`
- `Ready`
- `ReadyStatus`
- `ReadyMessage`
- `LastReadyzUtc`
- `LastCapabilitiesUtc`
- `LastTelemetryUtc`
- `LastError`
- `Capabilities`
- `Telemetry`

`Capabilities` is a cached summary of platform support. `Telemetry` contains nested `System`, `Cpu`, `Memory`, `Network`, `Disk`, `Gpu`, and `Ollama` sections.

#### Ollama Endpoint Model Management

Auth level: `TenantAdmin`

These routes are available only when the target model runner endpoint has `ApiType=Ollama`. They call the configured endpoint directly and return upstream status details without creating request-history entries.

| Method | Path | Upstream Ollama API | Notes |
| --- | --- | --- | --- |
| `GET` | `/v1.0/modelrunnerendpoints/{id}/ollama/models` | `GET /api/tags` | Lists local models available on the endpoint. |
| `POST` | `/v1.0/modelrunnerendpoints/{id}/ollama/models/pull` | `POST /api/pull` | Pulls a model with `stream=false`; large downloads should use a larger `TimeoutMs`. |
| `POST` | `/v1.0/modelrunnerendpoints/{id}/ollama/models/delete` | `DELETE /api/delete` | Deletes a local model. Conductor uses a POST control-plane wrapper so the model name can be sent as JSON reliably. |

Pull request:

```json
{
  "Model": "gemma3:4b",
  "Insecure": false,
  "TimeoutMs": 1800000
}
```

Delete request:

```json
{
  "Model": "gemma3:4b",
  "TimeoutMs": 300000
}
```

The dashboard exposes these routes from the `Manage Models` action on Ollama-type model runner endpoints.

### Model Definitions

Auth level: `Authenticated`

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/modeldefinitions` | List definitions. Query: `maxResults`, `continuationToken`, `nameFilter`, `activeFilter`, optional `tenantId`. |
| `POST` | `/v1.0/modeldefinitions` | Create definition. Cross-tenant callers supply `TenantId` in the body. |
| `POST` | `/v1.0/modeldefinitions/validate` | Validate a model-definition draft without saving it. Optional query: `tenantId`, `existingId`. |
| `GET` | `/v1.0/modeldefinitions/{id}` | Read definition. Optional `tenantId` query. |
| `PUT` | `/v1.0/modeldefinitions/{id}` | Update definition. Cross-tenant callers supply `TenantId` in the body. |
| `DELETE` | `/v1.0/modeldefinitions/{id}` | Delete definition. Optional `tenantId` query. Also removes it from referenced VMRs. |

### Model Configurations

Auth level: `Authenticated`

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/modelconfigurations` | List configurations. Query: `maxResults`, `continuationToken`, `nameFilter`, `activeFilter`, optional `tenantId`. |
| `POST` | `/v1.0/modelconfigurations` | Create configuration. Cross-tenant callers supply `TenantId` in the body. |
| `POST` | `/v1.0/modelconfigurations/validate` | Validate a model-configuration draft without saving it. Optional query: `tenantId`, `existingId`. |
| `GET` | `/v1.0/modelconfigurations/{id}` | Read configuration. Optional `tenantId` query. |
| `PUT` | `/v1.0/modelconfigurations/{id}` | Update configuration. Cross-tenant callers supply `TenantId` in the body. |
| `DELETE` | `/v1.0/modelconfigurations/{id}` | Delete configuration. Optional `tenantId` query. Also removes it from referenced VMRs. |

### Load Balancing Policies

Auth level: `Authenticated`

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/loadbalancingpolicies` | List policies. Query: `maxResults`, `continuationToken`, `nameFilter`, `activeFilter`, optional `tenantId`. |
| `POST` | `/v1.0/loadbalancingpolicies` | Create policy. Cross-tenant callers supply `TenantId` in the body. |
| `POST` | `/v1.0/loadbalancingpolicies/validate` | Validate a load-balancing policy draft without saving it. Optional query: `tenantId`, `existingId`. |
| `GET` | `/v1.0/loadbalancingpolicies/{id}` | Read policy. Optional `tenantId` query. |
| `PUT` | `/v1.0/loadbalancingpolicies/{id}` | Update policy. Cross-tenant callers supply `TenantId` in the body. |
| `DELETE` | `/v1.0/loadbalancingpolicies/{id}` | Delete policy. Optional `tenantId` query. Also detaches it from any VMRs that reference it. |
| `GET` | `/v1.0/loadbalancingpolicies/metrics` | Return the live supported metric catalog used by the policy evaluator. |

The `metrics` response shape is:

```json
{
  "Metrics": [
    {
      "Id": "rig.cpu.utilizationPercent",
      "Name": "CPU Utilization",
      "Description": "Current host CPU utilization percentage.",
      "Source": "rigmonitor",
      "ValueType": "Number",
      "SupportsFiltering": true,
      "SupportsRanking": true,
      "RecommendedDirection": "asc",
      "SupportedOperators": [
        "Equal",
        "NotEqual",
        "LessThan",
        "LessThanOrEqual",
        "GreaterThan",
        "GreaterThanOrEqual"
      ]
    }
  ]
}
```

### Model Access Policies

Auth level: `TenantAdmin`

For a user-facing authoring guide with rollout advice and real-world policy examples, see [ACCESS_POLICIES.md](./ACCESS_POLICIES.md).

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/modelaccesspolicies` | List policies. Query: `maxResults`, `continuationToken`, `nameFilter`, `activeFilter`, optional `tenantId`. |
| `POST` | `/v1.0/modelaccesspolicies` | Create a policy with optional nested rules. Cross-tenant callers supply `TenantId` in the body. |
| `POST` | `/v1.0/modelaccesspolicies/validate` | Validate a policy draft and nested rules without saving it. |
| `GET` | `/v1.0/modelaccesspolicies/{id}` | Read a policy and its rules. Optional `tenantId` query. |
| `PUT` | `/v1.0/modelaccesspolicies/{id}` | Update a policy and replace its nested rules. Cross-tenant callers supply `TenantId` in the body. |
| `DELETE` | `/v1.0/modelaccesspolicies/{id}` | Delete a policy. Optional `tenantId` query. Returns `409 Conflict` if attached to any VMR unless `forceDetach=true` is supplied. |
| `POST` | `/v1.0/modelaccesspolicies/{id}/evaluate` | Evaluate a supplied `ModelAccessEvaluationContext` against a policy. Optional `tenantId` query. |
| `GET` | `/v1.0/modelaccesspolicies/effective` | Evaluate effective access using query parameters. Query: `tenantId`, `credentialId`, `userId`, `vmrId`, `modelDefinitionId`, `modelName`, `action`. |

Create and update validate references to credentials, users, model definitions, and VMRs in the same tenant. Evaluation returns a `ModelAccessEvaluationResult` with `Allowed`, `Decision`, `Mode`, `DefaultSource`, matched policy/rule metadata, `ReasonCode`, `ReasonText`, and `WouldDeny`.

Example evaluation request:

```json
{
  "TenantId": "default",
  "CredentialId": "cred_xxx",
  "UserId": "usr_xxx",
  "VirtualModelRunnerId": "vmr_xxx",
  "ModelAccessPolicyId": "map_xxx",
  "RequestedModel": "gpt-4o-mini",
  "EffectiveModel": "gpt-4o-mini",
  "Action": "Completions",
  "RequestType": "OpenAIChatCompletions",
  "ApiType": "OpenAI"
}
```

Example evaluation response:

```json
{
  "Allowed": true,
  "Decision": "Permit",
  "Mode": "Enforce",
  "DefaultSource": null,
  "PolicyId": "map_xxx",
  "PolicyName": "Production model access",
  "RuleId": "mar_xxx",
  "RuleName": "Finance credential can use chat model",
  "ReasonCode": "MatchedAllowRule",
  "ReasonText": "Matched model access rule 'Finance credential can use chat model'.",
  "WouldDeny": false
}
```

Proxy enforcement occurs after request/model resolution and before endpoint inventory, session affinity, load balancing, and provider calls. Authentication failures return `401`; authenticated model access denials return `403`. In monitor mode, denied decisions are recorded as `WouldDeny=true` while the request continues.

List-models responses are governed by server setting `ModelAccessControl.ListModelsBehavior`: `Filter` removes denied upstream models, `Synthesize` returns a provider-shaped list from allowed active VMR model definitions, and `RawPassThrough` leaves upstream responses unchanged.

Label subject/resource rules can match a literal label by `SubjectId` or `ResourceId`, or by selectors such as `{ "label": "finance" }` and `{ "labels": "finance,production" }`. `ModelName` rules can match exact names with `ResourceId`, or text selectors such as `{ "equals": "gpt-4o-mini" }`, `{ "prefix": "gpt-4" }`, and `{ "contains": "70b" }`.

Example policy patterns:

Default deny with a credential allow-list:

```jsonc
{
  "TenantId": "default",
  "Name": "Credential allow-list",
  "DefaultDecision": "Deny",
  "Rules": [
    {
      "Name": "Primary credential can use chat model",
      "Priority": 100,
      "Effect": "Allow",
      "SubjectType": "Credential",
      "SubjectId": "cred_xxx",
      "ResourceType": "ModelDefinition",
      "ResourceId": "md_chat",
      "Actions": ["Completions", "ListModels"],
      "Active": true
    }
  ],
  "Active": true
}
```

Label-based access:

```jsonc
{
  "Name": "Finance label access",
  "DefaultDecision": "Deny",
  "Rules": [
    {
      "Name": "Finance subjects can use finance models",
      "Priority": 100,
      "Effect": "Allow",
      "SubjectType": "CredentialLabel",
      "SubjectSelector": { "label": "finance" },
      "ResourceType": "ModelLabel",
      "ResourceSelector": { "label": "finance" },
      "Actions": ["Completions", "Embeddings", "ListModels"],
      "Active": true
    }
  ]
}
```

Embeddings-only access:

```jsonc
{
  "Name": "Embeddings only",
  "DefaultDecision": "Deny",
  "Rules": [
    {
      "Name": "Allow embeddings model",
      "Priority": 100,
      "Effect": "Allow",
      "SubjectType": "Any",
      "ResourceType": "ModelName",
      "ResourceId": "text-embedding-004",
      "Actions": ["Embeddings", "ListModels"],
      "Active": true
    }
  ]
}
```

List-model filtering uses the same `ListModels` action. Include `ListModels` in allow rules for models that should be visible when `ModelAccessControl.ListModelsBehavior` is `Filter` or `Synthesize`.

For monitor-mode rollout, configure `ModelAccessControl.Enabled=true` and `ModelAccessControl.Mode=Monitor`, attach the policy to selected VMRs, and inspect request history for `ModelAccessWouldDeny=true` before switching to `Enforce`.

### VMR Reservations

Auth level: `TenantAdmin` for create, update, deactivate, and validate. Auth level: `Authenticated` for read, list, and effective-access evaluation.

VMR reservations are tenant-scoped time windows that block VMR admission for every identity except explicitly listed users and credentials. Outside an active reservation or admission drain window, VMR access is unchanged and continues through normal tenant, VMR, request-type, ACL, model access, routing, and endpoint checks.

For an operator and implementation guide, see [MANAGING_RESERVATIONS.md](./MANAGING_RESERVATIONS.md).

The dashboard exposes these routes through the **Reservations** workspace. The table supports refresh, filtering, row-click editing, row-level JSON inspection, create/edit/deactivate workflows, validation, and effective-access evaluation for a candidate user or credential.

Tenant-scoped authenticated callers are resolved to their tenant automatically. System-admin or cross-tenant callers should include `tenantId` in query strings or request bodies when operating on a specific tenant; omitting `tenantId` on list-style routes returns cross-tenant results only for callers authorized to see them.

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/vmrreservations` | List reservations. Query: `tenantId`, `vmrId`, `state`, `subjectType`, `subjectId`, `startsBeforeUtc`, `endsAfterUtc`, `nameFilter`, `activeFilter`, `maxResults`, `continuationToken`. Tenant callers may omit `tenantId`; system admins may omit `tenantId` to list all tenants. |
| `POST` | `/v1.0/vmrreservations` | Create a reservation with nested `Subjects`. Cross-tenant callers supply `TenantId` in the body. |
| `POST` | `/v1.0/vmrreservations/validate` | Validate a reservation draft and participant list without saving it. |
| `GET` | `/v1.0/vmrreservations/{id}` | Read a reservation and its participants. Tenant callers may omit `tenantId`; system admins should pass `tenantId`. |
| `PUT` | `/v1.0/vmrreservations/{id}` | Update a reservation and replace its participant list. |
| `DELETE` | `/v1.0/vmrreservations/{id}` | Soft deactivate a reservation. Tenant callers may omit `tenantId`; system admins should pass `tenantId`. |
| `GET` | `/v1.0/virtualmodelrunners/{id}/reservations` | List reservations scoped to one VMR. Tenant callers may omit `tenantId`; system admins may omit `tenantId` to search all tenants by globally unique VMR id. |
| `GET` | `/v1.0/virtualmodelrunners/{id}/reservation-effective` | Evaluate whether a user or credential would pass the reservation gate. Query: `tenantId`, `userId`, `credentialId`, `atUtc`. System admins should pass `tenantId`; tenant callers may omit it. |

Reservation request shape:

```json
{
  "TenantId": "ten_xxx",
  "VirtualModelRunnerId": "vmr_xxx",
  "Name": "Customer demo window",
  "Description": "Reserved capacity for evaluation team",
  "StartUtc": "2026-06-16T17:00:00Z",
  "EndUtc": "2026-06-16T19:00:00Z",
  "AdmissionDrainLeadMs": 300000,
  "Active": true,
  "Subjects": [
    { "SubjectType": "User", "SubjectId": "usr_xxx" },
    { "SubjectType": "Credential", "SubjectId": "cred_xxx" }
  ]
}
```

During an active reservation, a credential subject admits only that credential. A user subject admits that user and credentials owned by that user when the credential owner can be resolved. Reservation allow decisions do not bypass model access policies or ACLs.

Nonparticipant requests are denied before endpoint inventory, session affinity, load balancing, and upstream provider calls. Denials return:

- `401 ReservationAuthenticationRequired` when a reserved VMR receives an unauthenticated request.
- `403 ReservationDenied` when a nonparticipant calls during the active window.
- `403 ReservationDrainDenied` when a nonparticipant calls during the configured admission drain window.
- `503 ReservationConflict` if multiple active reservations somehow overlap at runtime.

The server writes reservation-denial log messages with tenant, VMR, reservation id, request user, request credential, UTC window, and reason code. Request history and request analytics also persist nullable reservation dimensions: `ReservationGuid`, `ReservationName`, `ReservationDecision`, `ReservationReasonCode`, `ReservationWindowStartUtc`, and `ReservationWindowEndUtc`. Search, summary, and analytics overview routes accept `reservationGuid`, `reservationDecision`, and `reservationReasonCode` filters for reservation-denial investigations.

### Virtual Model Runners

Auth level: `Authenticated`

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/virtualmodelrunners` | List VMRs. Query: `maxResults`, `continuationToken`, `nameFilter`, `activeFilter`, optional `tenantId`. |
| `POST` | `/v1.0/virtualmodelrunners` | Create VMR. Cross-tenant callers supply `TenantId` in the body. `BasePath` must be `/v1.0/api/{name}/`. |
| `POST` | `/v1.0/virtualmodelrunners/validate` | Validate a VMR draft without saving it. Optional query: `tenantId`, `existingId`. |
| `GET` | `/v1.0/virtualmodelrunners/{id}` | Read VMR. Optional `tenantId` query. |
| `PUT` | `/v1.0/virtualmodelrunners/{id}` | Update VMR. Cross-tenant callers supply `TenantId` in the body. `BasePath` must stay `/v1.0/api/{name}/`. |
| `DELETE` | `/v1.0/virtualmodelrunners/{id}` | Delete VMR. Optional `tenantId` query. |
| `GET` | `/v1.0/virtualmodelrunners/{id}/health` | Get aggregated health for the VMR and its endpoints. Optional `tenantId` query. |
| `GET` | `/v1.0/virtualmodelrunners/{id}/effective` | Return the resolved read-only effective configuration for the VMR. Optional `tenantId` query. |
| `POST` | `/v1.0/virtualmodelrunners/{id}/explain-routing` | Simulate routing for a representative request. Optional `tenantId` query. |
| `POST` | `/v1.0/virtualmodelrunners/{id}/load-model` | Tenant-admin route to resolve VMR targets, then load or verify a model. Optional `tenantId` query. |

VMR health response fields include:

- `VirtualModelRunnerId`
- `VirtualModelRunnerName`
- `CheckedUtc`
- `OverallHealthy`
- `HealthyEndpointCount`
- `DrainingEndpointCount`
- `QuarantinedEndpointCount`
- `TotalEndpointCount`
- `ActiveSessionCount`
- `Endpoints`

Validation routes return a `ResourceValidationResult` with `Errors` and `Warnings`. The VMR `effective` response resolves the endpoint inventory, request permissions, session-affinity settings, attached policy metadata, model definitions, model configurations, and `ModelConfigurationMappings` that the proxy path will use. The VMR `explain-routing` response returns a `RoutingDecision` containing a timeline, selected endpoint, session-affinity outcome, policy metadata, mutation summary, and per-candidate evidence.

### Model loading

Auth level: `TenantAdmin`

Model loading is a management-plane operation. It can consume GPU memory on local runners and can trigger billable upstream calls when an operator explicitly selects a hosted-provider generation or embedding probe.

Model loading calls are not proxied VMR inference requests and are not written to request history. Use the typed response, server logs, and `conductor_model_load_*` Prometheus metrics for audit and troubleshooting.

Routes:

| Method | Path | Notes |
| --- | --- | --- |
| `POST` | `/v1.0/modelrunnerendpoints/{id}/load-model` | Load or verify a model on one concrete endpoint. Direct endpoint loading is allowed even when the endpoint is inactive, so operators can warm before resuming traffic. |
| `POST` | `/v1.0/virtualmodelrunners/{id}/load-model` | Resolve the VMR model and endpoint target set, then load or verify the model. Inactive endpoints are skipped unless `IncludeInactive=true`. |

Request body:

```json
{
  "Model": "gemma3:4b",
  "ModelDefinitionId": null,
  "ProbeKind": "Auto",
  "TargetMode": "SelectedEndpoint",
  "EndpointIds": [],
  "InputText": "conductor warmup",
  "KeepAlive": "30m",
  "TimeoutMs": 300000,
  "MaxRetries": 0,
  "VerifyLoaded": true,
  "IncludeInactive": false,
  "DryRun": false
}
```

Fields:

| Field | Notes |
| --- | --- |
| `Model` | Required for direct endpoint loading. For VMR loading it may be omitted only when exactly one active attached `ModelDefinition` exists. |
| `ModelDefinitionId` | Optional VMR selector. If supplied, `Model` must be empty or match the definition name. |
| `ProbeKind` | `Auto`, `MetadataOnly`, `ChatCompletion`, `Completion`, `Embeddings`, or `NativeGenerate`. |
| `TargetMode` | VMR only: `SelectedEndpoint`, `AllEligibleEndpoints`, `AllConfiguredEndpoints`, or `SpecificEndpointIds`. |
| `EndpointIds` | Required only when `TargetMode=SpecificEndpointIds`; IDs must already be attached to the VMR. |
| `InputText` | Tiny non-sensitive probe text. It may be sent to hosted providers for explicit generation or embedding probes. |
| `KeepAlive` | Ollama retention hint. Other providers report it in `IgnoredFields`. |
| `TimeoutMs` | Per-attempt upstream timeout, clamped by the server from 1,000 to 1,800,000 ms. |
| `MaxRetries` | Per-endpoint retry count, clamped from 0 to 3. |
| `VerifyLoaded` | Runs provider-specific verification after a probe, such as Ollama `/api/ps` or metadata list checks. |
| `IncludeInactive` | VMR target selection includes inactive configured endpoints when true. |
| `DryRun` | Returns the planned mechanism and target endpoints without sending upstream traffic. |

Response body:

```json
{
  "Success": true,
  "TenantId": "default",
  "TargetType": "ModelRunnerEndpoint",
  "TargetId": "mre_123",
  "Model": "gemma3:4b",
  "ProbeKind": "Auto",
  "OutcomeCode": "Loaded",
  "Message": "Model load probe completed on 1 of 1 endpoint(s).",
  "DurationMs": 4211,
  "EndpointResults": [
    {
      "EndpointId": "mre_123",
      "EndpointName": "gpu-host-01",
      "ApiType": "Ollama",
      "BaseUrl": "http://gpu-host-01:11434",
      "Success": true,
      "OutcomeCode": "Loaded",
      "ProviderStatusCode": 200,
      "Mechanism": "OllamaGenerate",
      "RequestPath": "/api/generate",
      "DurationMs": 4211,
      "VerifiedLoaded": true,
      "IgnoredFields": [],
      "ErrorMessage": null
    }
  ]
}
```

Stable outcome codes include `Loaded`, `AlreadyAvailable`, `Verified`, `VerifiedRemote`, `DryRun`, `Skipped`, `Failed`, `TimedOut`, `UnauthorizedUpstream`, `ModelRequired`, `ModelNotAttached`, `NoEligibleEndpoints`, and `UnsupportedApiType`.

Provider behavior:

| Provider | `Auto` behavior | Result semantics |
| --- | --- | --- |
| Ollama | `POST /api/generate` with minimal output and optional `keep_alive`; embeddings use `/api/embed`. | `Loaded` or `AlreadyAvailable` when `/api/ps` confirms residency. Missing models fail; Conductor does not pull models. |
| vLLM | `GET /v1/models` metadata verification unless an explicit probe is requested. | `Verified` when the model is served. Explicit probes can warm tokenization or graph paths, but do not load arbitrary new weights. |
| OpenAI | `GET /v1/models` metadata verification unless an explicit probe is requested. | `VerifiedRemote`; hosted OpenAI has no host-local load primitive. Explicit generation or embedding probes may be billable. |
| Gemini | `GET /v1beta/models` metadata verification unless an explicit probe is requested. | `VerifiedRemote`; hosted Gemini has no host-local load primitive. Explicit generation or embedding probes may be billable. |

### Backup and restore

Auth level: `GlobalAdmin`

Both global admin users and system administrators can use these routes.

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/backup` | Export all configuration data into a `BackupPackage`. |
| `POST` | `/v1.0/backup/validate` | Validate a backup package without applying it. Request body is a `BackupPackage`. Uses the same shared validators as create, update, and restore flows. |
| `POST` | `/v1.0/backup/restore` | Restore from a package. Request body is a `RestoreRequest`. |

Backup package shape:

```jsonc
{
  "SchemaVersion": "1.3",
  "CreatedUtc": "2026-05-18T12:00:00Z",
  "SourceInstance": "host-name",
  "CreatedBy": "admin@example.com",
  "Tenants": [],
  "Users": [],
  "Credentials": [],
  "ModelDefinitions": [],
  "ModelConfigurations": [],
  "ModelRunnerEndpoints": [],
  "VirtualModelRunners": [],
  "VirtualModelRunnerReservations": [],
  "LoadBalancingPolicies": [],
  "ModelAccessPolicies": [],
  "ModelAccessRules": [],
  "Administrators": []
}
```

Backups include VMR reservations and nested reservation subjects. Validation checks reservation tenant, VMR, user, credential, subject, and overlap rules before restore; restore uses the same `Skip`, `Overwrite`, or `Fail` conflict mode as the other configuration entities.

Restore request shape:

```jsonc
{
  "Package": {},
  "Options": {
    "ConflictResolution": "Skip",      // Skip, Overwrite, Fail
    "RestoreAdministrators": false,
    "RestoreCredentials": true,
    "TenantFilter": []
  }
}
```

Validation response shape:

```jsonc
{
  "IsValid": true,
  "Errors": [],
  "Conflicts": [],
  "Summary": {
    "TenantCount": 0,
    "UserCount": 0,
    "CredentialCount": 0,
    "ModelDefinitionCount": 0,
    "ModelConfigurationCount": 0,
    "ModelRunnerEndpointCount": 0,
    "VirtualModelRunnerCount": 0,
    "VirtualModelRunnerReservationCount": 0,
    "AdministratorCount": 0,
    "LoadBalancingPolicyCount": 0,
    "ModelAccessPolicyCount": 0,
    "ModelAccessRuleCount": 0
  }
}
```

Restore response shape:

```jsonc
{
  "Success": true,
  "ErrorMessage": null,
  "Summary": {
    "Tenants": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "Users": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "Credentials": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "ModelDefinitions": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "ModelConfigurations": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "ModelRunnerEndpoints": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "VirtualModelRunners": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "VirtualModelRunnerReservations": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "Administrators": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "LoadBalancingPolicies": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "ModelAccessPolicies": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "ModelAccessRules": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 }
  },
  "Warnings": []
}
```

### Request history

Auth level: `Authenticated`

Request history routes are registered only when request history is enabled in server settings. When it is disabled, `/v1.0/requesthistory/summary`, `/v1.0/requesthistory/analytics/overview`, and `/v1.0/analytics/*` aggregate routes remain available and return empty payloads.

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/requesthistory/analytics/overview` | Chart-ready aggregate analytics. Query: `tenantId`, `range`, `startUtc`, `endUtc`, `bucketSeconds`, `limit`, `vmrGuid`, `endpointGuid`, `providerName`, `modelName`, `modelAccessPolicyGuid`, `modelAccessRuleGuid`, `modelAccessDecision`, `modelAccessWouldDeny`, `reservationGuid`, `reservationDecision`, `reservationReasonCode`, `stageKind`, `statusClass`. |
| `GET` | `/v1.0/requesthistory/summary` | Aggregated time buckets. Query: `tenantId`, `vmrGuid`, `endpointGuid`, `requestorUserGuid`, `credentialGuid`, `loadBalancingPolicyGuid`, `modelAccessPolicyGuid`, `modelAccessRuleGuid`, `modelAccessDecision`, `modelAccessWouldDeny`, `modelName`, `mutationSummary`, `denialReasonCode`, `reservationGuid`, `reservationDecision`, `reservationReasonCode`, `sessionAffinityOutcome`, `statusClass`, `sourceIp`, `httpStatus`, `startUtc`, `endUtc`, `interval`. |
| `GET` | `/v1.0/requesthistory` | Search entries. Query: `tenantId`, `vmrGuid`, `endpointGuid`, `requestorUserGuid`, `credentialGuid`, `loadBalancingPolicyGuid`, `modelAccessPolicyGuid`, `modelAccessRuleGuid`, `modelAccessDecision`, `modelAccessWouldDeny`, `modelName`, `mutationSummary`, `denialReasonCode`, `reservationGuid`, `reservationDecision`, `reservationReasonCode`, `sessionAffinityOutcome`, `statusClass`, `sourceIp`, `httpStatus`, `createdAfterUtc`, `createdBeforeUtc`, `page`, `pageSize`. |
| `GET` | `/v1.0/requesthistory/{id}` | Read entry metadata. Query: optional `tenantId` for cross-tenant callers. |
| `GET` | `/v1.0/requesthistory/{id}/detail` | Read full request/response detail. Query: optional `tenantId`. |
| `GET` | `/v1.0/requesthistory/{id}/analytics` | Read normalized analytics events for one request history entry. Query: optional `tenantId`. |
| `DELETE` | `/v1.0/requesthistory/{id}` | Delete one entry. Query: optional `tenantId`. |
| `POST` | `/v1.0/requesthistory/delete` | Delete selected entries by ID. Body: `{ "Ids": ["req_..."] }`. Query: optional `tenantId`. |
| `DELETE` | `/v1.0/requesthistory/bulk` | Bulk delete by filter. Query: `tenantId`, `vmrGuid`, `endpointGuid`, `requestorUserGuid`, `credentialGuid`, `loadBalancingPolicyGuid`, `modelName`, `mutationSummary`, `denialReasonCode`, `reservationGuid`, `reservationDecision`, `reservationReasonCode`, `sessionAffinityOutcome`, `statusClass`, `sourceIp`, `httpStatus`, `createdAfterUtc`, `createdBeforeUtc`. |

Summary interval values:

- `minute`
- `15minute`
- `hour`
- `6hour`
- `day`

Search response shape:

```jsonc
{
  "Data": [],
  "Page": 1,
  "PageSize": 10,
  "TotalCount": 0,
  "TotalPages": 0
}
```

Selected delete response shape:

```jsonc
{
  "DeletedCount": 2
}
```

Summary response shape:

```jsonc
{
  "Data": [
    {
      "TimestampUtc": "2026-05-18T12:00:00Z",
      "SuccessCount": 120,
      "FailureCount": 3,
      "TotalCount": 123
    }
  ],
  "StartUtc": "2026-05-18T11:00:00Z",
  "EndUtc": "2026-05-18T12:00:00Z",
  "Interval": "hour",
  "TotalSuccess": 120,
  "TotalFailure": 3,
  "StatusClassCounts": {
    "2xx": 120,
    "5xx": 3
  },
  "DenialReasonCounts": {
    "AllEndpointsAtCapacity": 2,
    "PolicyRejected": 1
  },
  "SessionAffinityOutcomeCounts": {
    "Hit": 40,
    "Miss": 83
  },
  "TotalRequests": 123
}
```

Analytics overview response shape:

```jsonc
{
  "StartUtc": "2026-06-05T00:00:00Z",
  "EndUtc": "2026-06-06T00:00:00Z",
  "BucketSeconds": 900,
  "TotalRequests": 123,
  "SuccessCount": 120,
  "FailureCount": 3,
  "AnalyticsCapturedCount": 118,
  "AnalyticsCoveragePercent": 95.93,
  "ReservationDeniedCount": 2,
  "ReservationDenialCounts": {
    "vmrr_xxx": 2
  },
  "AverageDurationMs": 842.4,
  "P50DurationMs": 600,
  "P95DurationMs": 1900,
  "P99DurationMs": 3200,
  "TotalTokens": 456789,
  "AverageTokensPerSecond": 42.5,
  "TimeSeries": [],
  "StageBreakdown": [],
  "EndpointSummaries": [],
  "SlowestRequests": []
}
```

Analytics stage events use stable `StageKind` values such as `routing`, `capacity_wait`, `upstream_headers`, `first_token_wait`, `generation`, `completion`, `provider_load`, `provider_prompt_eval`, and `provider_generation`. Provider-native timings are best-effort and missing values are returned as `null`, not zero.

Request history entry fields include:

- `Id`
- `TenantGuid`
- `VirtualModelRunnerGuid`
- `VirtualModelRunnerName`
- `RequestorUserGuid`
- `RequestorUserEmail`
- `CredentialGuid`
- `CredentialName`
- `LoadBalancingPolicyGuid`
- `LoadBalancingPolicyName`
- `ModelAccessPolicyGuid`
- `ModelAccessPolicyName`
- `ModelAccessRuleGuid`
- `ModelAccessRuleName`
- `ModelAccessDecision`
- `ModelAccessWouldDeny`
- `ModelEndpointGuid`
- `ModelEndpointName`
- `ModelEndpointUrl`
- `ModelDefinitionGuid`
- `ModelDefinitionName`
- `ModelConfigurationGuid`
- `RequestedModel`
- `EffectiveModel`
- `RequestType`
- `RoutingOutcomeCode`
- `DenialReasonCode`
- `DenialReason`
- `ReservationGuid`
- `ReservationName`
- `ReservationDecision`
- `ReservationReasonCode`
- `ReservationWindowStartUtc`
- `ReservationWindowEndUtc`
- `SessionAffinityOutcome`
- `MutationSummary`
- `ExplanationSummary`
- `RequestBodyRetained`
- `RequestBodyRedacted`
- `RequestHeadersRedacted`
- `ResponseBodyRetained`
- `ResponseBodyRedacted`
- `ResponseHeadersRedacted`
- `RequestorSourceIp`
- `HttpMethod`
- `HttpUrl`
- `RequestBodyLength`
- `ResponseBodyLength`
- `HttpStatus`
- `FirstTokenTimeMs`
- `ResponseTimeMs`
- `TraceId`
- `ProviderRequestId`
- `ProviderName`
- `PromptTokens`
- `CompletionTokens`
- `TotalTokens`
- `TokensPerSecondOverall`
- `TokensPerSecondGeneration`
- `AnalyticsCaptured`
- `AnalyticsVersion`
- `DominantStageKind`
- `DominantStageDurationMs`
- `AnalyticsFailureCode`
- `ObjectKey`
- `RequestTransferType`
- `ResponseTransferType`
- `CreatedUtc`
- `CompletedUtc`

Detail responses extend the entry with:

- `RequestHeaders`
- `RequestBody`
- `RequestBodyTruncated`
- `ResponseHeaders`
- `ResponseBody`
- `ResponseBodyTruncated`
- `RoutingDecision`

If request or response bodies have aged past `BodyRetentionDays`, detail records remain readable but the body content is scrubbed while the searchable routing and latency metadata stays available until `MetadataRetentionDays` expires.

### Analytics workspace

Auth level: `TenantAdmin` or `SystemAdmin`

The Analytics workspace API is the dashboard-oriented reporting surface for TTFT, token usage, estimate-only cost, user/model/endpoint breakdowns, and failed-request-type counts. It is separate from `/v1.0/requesthistory/analytics/*`, which exposes trace-linked per-request timing stages. See [ADR 0002](./docs/adr/0002-analytics-workspace.md) for the accepted first-release design.

System administrators can query globally or pass `tenantId` to restrict results to one tenant. Tenant administrators and other tenant-scoped callers are forced into their authenticated tenant scope even if they pass a different `tenantId`. First-release analytics data is bounded to the last 30 days.

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/analytics/catalog` | Supported metrics, dimensions, named ranges, granularities, retention, and unavailable future export formats. |
| `POST` | `/v1.0/analytics/query` | General analytics query. Body: `AnalyticsQueryRequest`. |
| `GET` | `/v1.0/analytics/reports` | List saved reports. Query: `tenantId`, `maxResults`, `continuationToken`, `nameFilter`, `ownerUserId`. |
| `POST` | `/v1.0/analytics/reports` | Create saved report. Body: `AnalyticsSavedReport`. |
| `GET` | `/v1.0/analytics/reports/{id}` | Read saved report. Query: optional `tenantId` for system administrators. |
| `PUT` | `/v1.0/analytics/reports/{id}` | Update saved report. Body: `AnalyticsSavedReport`. |
| `DELETE` | `/v1.0/analytics/reports/{id}` | Delete saved report. Query: optional `tenantId` for system administrators. |
| `GET` | `/v1.0/analytics/summary` | Convenience summary query. |
| `GET` | `/v1.0/analytics/timeseries` | Convenience time-series query. |
| `GET` | `/v1.0/analytics/ttft` | TTFT-focused query; defaults to grouping by user. |
| `GET` | `/v1.0/analytics/tokens` | Token-usage query; defaults to grouping by effective model. |
| `GET` | `/v1.0/analytics/costs` | Estimate-only cost query; defaults to grouping by user. |
| `GET` | `/v1.0/analytics/users` | User-oriented usage query. |
| `GET` | `/v1.0/analytics/access` | Access/reliability query for success, failure, denied, and rate-limited counts. |

Analytics routes resolve to `RequestTypeEnum.ReadAnalytics`. System administrators can query global scope and may filter to a tenant with `tenantId`. Tenant administrators and tenant users granted `analytics.read` are forced into their authenticated tenant scope. Grant first-release dedicated Analytics access by adding the `analytics.read` user label, or by setting a user tag such as `analytics.read=true` or `permissions=analytics.read`.

GET query parameters:

| Parameter | Type | Notes |
| --- | --- | --- |
| `tenantId` | string | Optional tenant filter for system administrators only. |
| `range` | string | `lastHour`, `lastDay`, `lastWeek`, `lastMonth`, or `custom`. Default `lastDay`. |
| `startUtc` | string | Custom start timestamp, UTC ISO 8601. Used with `endUtc`; clamped to the 30-day retained window. |
| `endUtc` | string | Custom end timestamp, UTC ISO 8601. |
| `bucketSeconds` | integer | Bucket size. Common values: `60`, `900`, `3600`, `21600`, `86400`. |
| `timezone` | string | Display timezone hint. Aggregation remains UTC in the first release. |
| `limit` | integer | Maximum raw request-history rows to scan. Server caps the value. |
| `tokenUnitCost` | decimal | Optional per-token unit cost used for estimate-only cost. GET parsing ignores invalid or negative values; POST query validation rejects negative values. |
| `costCurrency` | string | Optional display label such as `USD`, `EUR`, or `estimate`. |
| `groupBy` | string | One dimension: `TenantId`, `RequestedModel`, `EffectiveModel`, `ModelDefinitionId`, `ModelRunnerEndpointId`, `VirtualModelRunnerId`, `RequestorUserId`, `CredentialId`, `ProviderName`, `ReservationGuid`, `ReservationName`, `ReservationDecision`, or `ReservationReasonCode`. |
| `vmrGuid` | string | Filter by Virtual Model Runner ID. CSV values are accepted. |
| `endpointGuid` | string | Filter by model runner endpoint ID. CSV values are accepted. |
| `modelName` | string | Filter by requested, effective, or model-definition name. CSV values are accepted. |
| `requestorUserGuid` / `userId` | string | Filter by user ID. CSV values are accepted. |
| `credentialGuid` / `credentialId` | string | Filter by credential ID. CSV values are accepted. |
| `providerName` | string | Filter by provider family/name. CSV values are accepted. |
| `reservationGuid` / `reservationId` | string | Filter by VMR reservation ID. CSV values are accepted. |
| `reservationDecision` | string | Filter by reservation gate decision, such as `Allowed`, `Denied`, `NoReservation`, or `Conflict`. CSV values are accepted. |
| `reservationReasonCode` | string | Filter by reservation reason, such as `ReservationDenied`, `ReservationDrainDenied`, `ReservationAuthenticationRequired`, or `ReservationConflict`. CSV values are accepted. |
| `statusClass` | string | Filter by HTTP status class such as `2xx`, `4xx`, or `5xx`. CSV values are accepted. |
| `stageKind` | string | Filter by normalized dominant stage kind where request analytics capture is available, such as `routing`, `first_token_wait`, `generation`, `completion`, or `denial`. |
| `modelAccessWouldDeny` | boolean | Filter model-access monitor-mode would-deny rows. |
| `successfulCompletionsOnly` | boolean | Usage-style metrics use successful completions by default. |

`AnalyticsQueryRequest` shape:

```jsonc
{
  "TenantId": "tenant_123",
  "Range": "lastDay",
  "StartUtc": null,
  "EndUtc": null,
  "BucketSeconds": 3600,
  "Timezone": "UTC",
  "TokenUnitCost": 0.00001,
  "CostCurrency": "USD",
  "Metrics": [
    "latency.ttft.avg",
    "tokens.total",
    "cost.estimated"
  ],
  "GroupBy": [
    "RequestorUserId"
  ],
  "Filters": {
    "VirtualModelRunnerIds": [
      "vmr_123"
    ],
    "ModelRunnerEndpointIds": [],
    "ModelNames": [],
    "RequestorUserIds": [],
    "CredentialIds": [],
    "ProviderNames": [],
    "ReservationIds": [],
    "ReservationDecisions": [],
    "ReservationReasonCodes": [],
    "StatusClasses": [],
    "StageKinds": [],
    "ModelAccessWouldDeny": null,
    "SuccessfulCompletionsOnly": true
  },
  "Limit": 10000,
  "ContinuationToken": null
}
```

`AnalyticsQueryResult` shape:

```jsonc
{
  "TenantId": "tenant_123",
  "IsGlobalScope": false,
  "StartUtc": "2026-06-13T00:00:00Z",
  "EndUtc": "2026-06-14T00:00:00Z",
  "BucketSeconds": 3600,
  "RetentionDays": 30,
  "TotalRequests": 120,
  "SuccessfulCompletionCount": 118,
  "FailedRequestCount": 2,
  "DeniedRequestCount": 1,
  "ReservationDeniedCount": 1,
  "ReservationDenialCounts": {
    "vmrr_123": 1
  },
  "RateLimitedRequestCount": 1,
  "AverageTimeToFirstTokenMs": 214.5,
  "P50TimeToFirstTokenMs": 180,
  "P95TimeToFirstTokenMs": 490,
  "P99TimeToFirstTokenMs": 850,
  "PromptTokens": 240000,
  "CompletionTokens": 62000,
  "TotalTokens": 302000,
  "CachedTokens": null,
  "MultimodalTokens": null,
  "UnknownTokenUsageCount": 3,
  "TokenUnitCost": 0.00001,
  "CostCurrency": "USD",
  "EstimatedCost": 3.02,
  "TimeSeries": [
    {
      "TimestampUtc": "2026-06-13T12:00:00Z",
      "RequestCount": 20,
      "SuccessfulCompletionCount": 19,
      "FailedRequestCount": 1,
      "DeniedRequestCount": 0,
      "RateLimitedRequestCount": 1,
      "AverageTimeToFirstTokenMs": 230.0,
      "PromptTokens": 42000,
      "CompletionTokens": 12000,
      "TotalTokens": 54000,
      "CachedTokens": null,
      "MultimodalTokens": null,
      "UnknownTokenUsageCount": 0,
      "EstimatedCost": 0.54
    }
  ],
  "Groups": [
    {
      "Dimension": "RequestorUserId",
      "Value": "usr_123",
      "Label": "operator@example.com",
      "RequestCount": 20,
      "SuccessfulCompletionCount": 19,
      "FailedRequestCount": 1,
      "DeniedRequestCount": 0,
      "RateLimitedRequestCount": 1,
      "AverageTimeToFirstTokenMs": 230.0,
      "P95TimeToFirstTokenMs": 490,
      "TotalTokens": 54000,
      "UnknownTokenUsageCount": 0,
      "TimeToFirstTokenCoveragePercent": 100.0,
      "EstimatedCost": 0.54,
      "LastSeenUtc": "2026-06-13T12:57:00Z"
    }
  ]
}
```

Cost is estimate-only: Conductor multiplies successful reported token usage by the caller-supplied `tokenUnitCost`. It does not model provider price books, cached-token discounts, multimodal pricing, account credits, taxes, currency conversion, or provider invoice rounding. Missing provider usage is reported through `UnknownTokenUsageCount`, not treated as zero. Cached and multimodal token fields are nullable until provider parsers persist those categories. Grouped summaries include failure, denial, rate-limit, unknown-token, TTFT coverage, and last-seen fields so dashboards can answer user and credential breakdown questions without separate request-history reads.

`AnalyticsSavedReport` shape:

```jsonc
{
  "Id": "asr_123",
  "TenantId": "tenant_123",
  "OwnerUserId": "usr_123",
  "Name": "Daily user cost",
  "Description": "Estimate user token cost over the last day.",
  "Scope": "Tenant",
  "Query": {
    "Range": "lastDay",
    "BucketSeconds": 3600,
    "TokenUnitCost": 0.00001,
    "CostCurrency": "USD",
    "GroupBy": [
      "RequestorUserId"
    ],
    "Filters": {
      "RequestorUserIds": [
        "usr_123"
      ],
      "SuccessfulCompletionsOnly": true
    },
    "Limit": 10000
  },
  "DisplayState": {
    "workspace": "Analytics",
    "chart": "VolumeAndTtft"
  },
  "Labels": [
    "analytics"
  ],
  "Tags": {
    "range": "lastDay"
  },
  "CreatedUtc": "2026-06-14T10:00:00Z",
  "LastUpdateUtc": "2026-06-14T10:00:00Z"
}
```

Saved reports can be global (`TenantId` null, system-admin scope) or tenant-scoped. Tenant administrators cannot create or load reports outside their authenticated tenant. Saved reports store query and display-state definitions only; they do not schedule execution or snapshot historical results.

Export endpoints are not included in the first route slice. The catalog returns export formats as unavailable until those APIs are implemented.

### Observability

Auth level: `Authenticated`

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/observability/metrics` | Prometheus text exposition for low-cardinality operational metrics. |
| `GET` | `/v1.0/observability/metrics/summary` | JSON snapshot of counters and latency summaries grouped overall and by VMR. |

The export includes counters and histograms such as `conductor_requests_total`, `conductor_denials_total`, `conductor_policy_fallbacks_total`, `conductor_session_affinity_total`, `conductor_saturation_denials_total`, `conductor_telemetry_freshness_failures_total`, `conductor_route_decision_duration_ms`, `conductor_total_duration_ms`, and `conductor_first_token_time_ms`.

## Proxied Virtual Model Runner APIs

These are not fixed management routes. They are mounted dynamically under each VMR's `BasePath`.

If a VMR has:

```text
/v1.0/api/my-vmr/
```

then the proxy surface begins there.

### Supported API families

#### OpenAI

Available when the VMR `ApiType` is `OpenAI`.

Typical proxied routes:

- `POST /v1.0/api/{vmr}/v1/chat/completions`
- `POST /v1.0/api/{vmr}/v1/completions`
- `GET /v1.0/api/{vmr}/v1/models`
- `POST /v1.0/api/{vmr}/v1/embeddings`

#### vLLM

Available when the VMR `ApiType` is `vLLM`.

Conductor uses the OpenAI-compatible surface:

- `POST /v1.0/api/{vmr}/v1/chat/completions`
- `POST /v1.0/api/{vmr}/v1/completions`
- `GET /v1.0/api/{vmr}/v1/models`
- `POST /v1.0/api/{vmr}/v1/embeddings`

#### Gemini

Available when the VMR `ApiType` is `Gemini`.

Typical proxied routes:

- `POST /v1.0/api/{vmr}/v1beta/models/{model}:generateContent`
- `POST /v1.0/api/{vmr}/v1beta/models/{model}:streamGenerateContent`
- `POST /v1.0/api/{vmr}/v1beta/models/{model}:embedContent`
- `GET /v1.0/api/{vmr}/v1beta/models`

#### Ollama

Available when the VMR `ApiType` is `Ollama`.

Typical proxied routes:

- `POST /v1.0/api/{vmr}/api/generate`
- `POST /v1.0/api/{vmr}/api/chat`
- `GET /v1.0/api/{vmr}/api/tags`
- `POST /v1.0/api/{vmr}/api/embed`
- `POST /v1.0/api/{vmr}/api/embeddings`
- `POST /v1.0/api/{vmr}/api/pull`
- `DELETE /v1.0/api/{vmr}/api/delete`
- `GET /v1.0/api/{vmr}/api/ps`
- `POST /v1.0/api/{vmr}/api/show`

### Proxy behavior controls on the VMR

These VMR fields affect proxied behavior:

| Field | Meaning |
| --- | --- |
| `LoadBalancingMode` | Built-in selection mode used directly or as a policy fallback. Supported values: `RoundRobin`, `Random`, `FirstAvailable`, `LeastRecentlyUsed`. |
| `LoadBalancingPolicyId` | Optional advanced policy attachment. |
| `AllowEmbeddings` | If `false`, embedding routes are rejected. |
| `AllowCompletions` | If `false`, completion/chat routes are rejected. |
| `AllowModelManagement` | If `false`, model management routes such as list/pull/delete are rejected. |
| `StrictMode` | If `true`, only attached `ModelDefinitions` are accepted. |
| `SessionAffinityMode` | Enables sticky routing by source IP, API key, or a custom header. |
| `TimeoutMs` | Maximum proxy wait time per request. |

## Practical examples

### Create a tenant

```bash
curl -X POST http://localhost:9000/v1.0/tenants \
  -H "Content-Type: application/json" \
  -H "x-admin-email: admin@example.com" \
  -H "x-admin-password: secret" \
  -d '{"Name":"Production"}'
```

### Login and receive a bearer token

```bash
curl -X POST http://localhost:9000/v1.0/auth/login/credential \
  -H "Content-Type: application/json" \
  -d '{"TenantId":"default","Email":"admin@conductor","Password":"password"}'
```

### Create a load-balancing policy

```bash
curl -X POST http://localhost:9000/v1.0/loadbalancingpolicies \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "Name":"Least In-Flight",
    "Filters":[],
    "Ranking":[
      {
        "Metric":"health.inFlightRequests",
        "Direction":"Ascending",
        "Weight":1.0
      }
    ],
    "FallbackMode":"UseLegacyLoadBalancingMode",
    "TieBreaker":"RoundRobin",
    "Active":true
  }'
```

### Call a VMR's OpenAI-compatible chat route

```bash
curl -X POST http://localhost:9000/v1.0/api/my-vmr/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "model":"llama3.2:latest",
    "messages":[
      {"role":"user","content":"Hello"}
    ]
  }'
```

### Load or verify a model on an endpoint

```bash
curl -X POST "http://localhost:9000/v1.0/modelrunnerendpoints/mre_123/load-model?tenantId=default" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "Model":"gemma3:4b",
    "ProbeKind":"Auto",
    "KeepAlive":"30m",
    "VerifyLoaded":true
  }'
```

### Load or verify a model through a VMR

```bash
curl -X POST "http://localhost:9000/v1.0/virtualmodelrunners/vmr_123/load-model?tenantId=default" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "Model":"gemma3:4b",
    "TargetMode":"SelectedEndpoint",
    "ProbeKind":"Auto",
    "VerifyLoaded":true
  }'
```

## Related documentation

- [LOAD_BALANCING_POLICIES.md](LOAD_BALANCING_POLICIES.md)
