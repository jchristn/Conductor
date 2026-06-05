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
- Enum values are serialized as strings such as `OpenAI`, `RoundRobin`, or `FailClosed`.
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
| `404` | Resource not found |
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

### Virtual Model Runner

```jsonc
{
  "Id": "vmr_xxx",
  "TenantId": "default",
  "Name": "gpu-chat",                     // required
  "Hostname": null,
  "BasePath": "/v1.0/api/gpu-chat/",      // required; exactly one segment after /v1.0/api/
  "ApiType": "OpenAI",
  "LoadBalancingMode": "RoundRobin",
  "LoadBalancingPolicyId": "lbp_xxx",
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
  "RequestHistoryEnabled": false,
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
  "SchemaVersion": "1.1",
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
  "LoadBalancingPolicies": [],
  "Administrators": []
}
```

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
    "AdministratorCount": 0,
    "LoadBalancingPolicyCount": 0
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
    "Administrators": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 },
    "LoadBalancingPolicies": { "Created": 0, "Updated": 0, "Skipped": 0, "Failed": 0 }
  },
  "Warnings": []
}
```

### Request history

Auth level: `Authenticated`

Request history routes are registered only when request history is enabled in server settings. When it is disabled, `/v1.0/requesthistory/summary` and `/v1.0/requesthistory/analytics/overview` remain available and return empty payloads.

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/v1.0/requesthistory/analytics/overview` | Chart-ready aggregate analytics. Query: `tenantId`, `range`, `startUtc`, `endUtc`, `bucketSeconds`, `limit`, `vmrGuid`, `endpointGuid`, `providerName`, `modelName`, `stageKind`, `statusClass`. |
| `GET` | `/v1.0/requesthistory/summary` | Aggregated time buckets. Query: `tenantId`, `vmrGuid`, `endpointGuid`, `requestorUserGuid`, `credentialGuid`, `loadBalancingPolicyGuid`, `modelName`, `mutationSummary`, `denialReasonCode`, `sessionAffinityOutcome`, `statusClass`, `sourceIp`, `httpStatus`, `startUtc`, `endUtc`, `interval`. |
| `GET` | `/v1.0/requesthistory` | Search entries. Query: `tenantId`, `vmrGuid`, `endpointGuid`, `requestorUserGuid`, `credentialGuid`, `loadBalancingPolicyGuid`, `modelName`, `mutationSummary`, `denialReasonCode`, `sessionAffinityOutcome`, `statusClass`, `sourceIp`, `httpStatus`, `createdAfterUtc`, `createdBeforeUtc`, `page`, `pageSize`. |
| `GET` | `/v1.0/requesthistory/{id}` | Read entry metadata. Query: optional `tenantId` for cross-tenant callers. |
| `GET` | `/v1.0/requesthistory/{id}/detail` | Read full request/response detail. Query: optional `tenantId`. |
| `GET` | `/v1.0/requesthistory/{id}/analytics` | Read normalized analytics events for one request history entry. Query: optional `tenantId`. |
| `DELETE` | `/v1.0/requesthistory/{id}` | Delete one entry. Query: optional `tenantId`. |
| `POST` | `/v1.0/requesthistory/delete` | Delete selected entries by ID. Body: `{ "Ids": ["req_..."] }`. Query: optional `tenantId`. |
| `DELETE` | `/v1.0/requesthistory/bulk` | Bulk delete by filter. Query: `tenantId`, `vmrGuid`, `endpointGuid`, `requestorUserGuid`, `credentialGuid`, `loadBalancingPolicyGuid`, `modelName`, `mutationSummary`, `denialReasonCode`, `sessionAffinityOutcome`, `statusClass`, `sourceIp`, `httpStatus`, `createdAfterUtc`, `createdBeforeUtc`. |

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
| `LoadBalancingMode` | Built-in selection mode used directly or as a policy fallback. |
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

## Related documentation

- [LOAD_BALANCING_POLICIES.md](LOAD_BALANCING_POLICIES.md)
