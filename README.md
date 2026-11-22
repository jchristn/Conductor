<img src="./assets/icon-dark.png" alt="Conductor" width="128" height="128">

# Conductor

Project version: `1.1.0`

Conductor is a platform for managing models, model runners, model configurations, and virtualizing combinations into virtual model runners exposed to the network through OpenAI, vLLM, Gemini, Ollama, Anthropic, and TypeSafe System One (decision model) APIs. Upstream endpoints can be Ollama, Ollama Cloud, OpenAI, OpenAI-compatible servers, vLLM, Gemini, Anthropic, hosted TypeSafe Jev models, or a local Ollaya runtime.

## Features

- **Multi-tenant Architecture**: Full tenant isolation with tenant-scoped data access
- **Model Runner Endpoints**: Define and manage nine explicit endpoint types: `Ollama`, `OllamaCloud`, `OpenAI`, `OpenAICompatible`, `vLLM`, `Gemini`, `Anthropic`, `TypeSafe`, and `Ollaya`. Type defaults (host, port, SSL, health check) come only from the type the operator selects, never from a hostname
- **Anthropic Support**: Expose the Anthropic Messages API (streaming, token counting, model listing) through an `Anthropic` VMR, so Claude Code and the official Anthropic SDKs can run through Conductor with a Conductor credential
- **Decision Models**: Route TypeSafe System One decision requests to hosted Jev models (`TypeSafe`) or a local Ollaya runtime (`Ollaya`), with a provider-neutral decision capability on model definitions, VMRs, and model access policies
- **Upstream Key Masking**: Endpoint API keys are masked in every response and can be read back only through an audited reveal route
- **Model Definitions**: Catalog your models with metadata like family, parameter size, and quantization
- **Model Configurations**: Create reusable configurations with pinned properties for embeddings and completions
- **Endpoint Groups**: Reuse tenant-scoped endpoint collections across virtual model runners
- **Virtual Model Runners**: Combine endpoints, endpoint groups, and configurations into virtual endpoints with load balancing
- **VMR Reservations**: Schedule exclusive access windows for selected users or credentials while preserving normal on-demand access outside the window
- **Configuration Pinning**: Automatically inject model parameters into requests (like OllamaFlow)
- **Session Affinity**: Pin clients to specific backend endpoints based on IP address, API key, or custom headers to minimize context drops and model swapping
- **Load Balancing**: Round-robin, random, or first-available endpoint selection with weighted distribution and optional session affinity
- **Health Checking**: Automatic background health monitoring of endpoints with configurable thresholds, shared probes for duplicate health URLs, `GET`, `HEAD`, or `POST` probes with a request body, and a per-endpoint switch to turn probes off
- **RigMonitor Telemetry**: Optionally enrich endpoint health with cached host CPU, memory, disk, network, GPU, and Ollama telemetry from RigMonitor sidecars
- **Policy-Based Routing**: Create first-class load-balancing policies that filter or rank endpoints using health, capacity, and RigMonitor metrics
- **Model Access Policies**: Attach tenant-scoped ACL policies to virtual model runners to allow, deny, monitor, and explain model usage by credential, user, label, model, action, and VMR
- **Explainable Routing**: Simulate representative requests, inspect candidate elimination, review policy evidence, and persist routing explanations into request history
- **Model Load Or Verification Controls**: Warm local models or verify hosted-provider availability from endpoint and virtual model runner workflows
- **Preflight Validation**: Validate endpoints, model definitions, model configurations, load-balancing policies, and VMRs before saving them
- **Effective Configuration Preview**: Resolve the endpoint set, request permissions, policy attachment, model pinning, and session-affinity settings that a VMR will actually use
- **Operational Metrics**: Export Prometheus-friendly latency, denial, fallback, session-affinity, saturation, and telemetry-freshness signals
- **OpenTelemetry Observability**: Emit OTLP metrics and distributed traces across the HTTP, inference proxy, routing/load-balancing, database, health, and model-load subsystems, with a batteries-included Prometheus/Grafana/Loki/Tempo stack and per-subsystem Grafana dashboards
- **Drain And Quarantine Controls**: Keep endpoints visible for health diagnostics while intentionally excluding them from new routing
- **QoS & Queueing**: Link a per-VMR QoS profile that classifies incoming traffic (by custom header, credential, model, body attribute, tenant, user, and more) and queues it (FIFO, priority, weighted-fair, low-latency, or weighted round robin, chainable into a hierarchy). Requests are released in scheduled order as endpoint capacity frees instead of being rejected immediately, with per-class metrics and traces through the existing OpenTelemetry pipeline
- **Rate Limiting**: Per-endpoint maximum parallel request limits with automatic capacity management
- **Request History and Analytics**: Optional per-VMR request/response capture with trace IDs, stage timings, provider request IDs, token counts, throughput, dashboard drill-down, configurable retention, redaction, metadata-only retention modes, and a tenant-scoped Analytics workspace for TTFT, token usage, user/credential breakdowns, estimated cost, and failed-request reporting
- **React Dashboard**: Full-featured UI for managing all entities including real-time health status

## Quick Start

### Using Docker Compose

```bash
cd docker
docker compose up -d
```

The server will be available at `http://127.0.0.1:9000` and the dashboard at `http://127.0.0.1:9100`. Examples in this document use `127.0.0.1` rather than `localhost` for loopback addresses, because some clients (notably on Windows) try IPv6 `::1` first for `localhost` and stall before falling back to IPv4.
The Compose file uses the named Docker Hub images `jchristn77/conductor-server:v1.1.0` and `jchristn77/conductor-dashboard:v1.1.0`, starts PostgreSQL with a persisted `conductor-postgres-data` volume, and runs a one-shot `conductor-db-init` container to create the database schema and factory default records.

The dashboard container receives `CONDUCTOR_SERVER_URL=http://localhost:9000` from `docker/compose.yaml` (quoted as written in that file), so the login page points browsers at the host-exposed Conductor API by default.

The Compose file also brings up the observability stack (OpenTelemetry Collector, Prometheus, Tempo, Loki, and Grafana). Once running, open **Grafana at `http://127.0.0.1:3000`** (anonymous admin access is enabled) to view the pre-provisioned per-subsystem dashboards. See [Observability](#observability) for details.

> **Warning: change the default credentials immediately.** The Compose setup starts with fixed, well-known credentials that are the same on every installation:
>
> - Administrator and tenant user (tenant `default`, global admin): `admin@conductor` / `password`
> - Admin API key: `conductoradmin` (`AdminApiKeys` in `docker/conductor.json`)
> - Tenant API bearer token: `factory_default_bearer_token_0000000000000000000000000000000000` (seeded by `docker/factory/schema.sql`)
> - PostgreSQL: `conductor` / `conductor`, published on host port `5432`
>
> Change them before the server is reachable by anyone else. See [Production Hardening](#production-hardening).

### Building from Source

#### Prerequisites

- .NET 10 SDK
- Node.js 20+

#### Build and Run Server

```bash
cd src/Conductor.Server
dotnet run
```

The server reads its settings from `conductor.json` in the current working directory, and creates the file with defaults if it does not exist. With the default settings it also writes these paths, relative to the working directory:

| Path | Contents | Setting |
|------|----------|---------|
| `conductor.json` | Settings, including `AdminApiKeys` and database credentials | first command-line argument |
| `conductor.db` | SQLite database | `Database.Filename` |
| `logs/` | Log files (`conductor.log.<date>`) | `Logging.LogDirectory`, `Logging.LogFilename` |
| `request-history/` | Captured request and response bodies | `RequestHistory.Directory` |

The commands above therefore put these files in `src/Conductor.Server`. They are listed in `.gitignore`, but to keep settings and data out of the source tree, run the server from another directory and pass the settings path as the first argument:

```bash
mkdir -p ~/conductor-data && cd ~/conductor-data
dotnet run --project <path-to-repo>/src/Conductor.Server/Conductor.Server.csproj -- ./conductor.json
```

The default bind address is `localhost` on port `9000`, which the server listens on as `127.0.0.1`. Examples in this document call it at `http://127.0.0.1:9000`.

> **Warning: change the default credentials immediately.** On first run against an empty database the server creates an administrator and a global-admin tenant user, both `admin@conductor` / `password`, and prints them together with a randomly generated tenant bearer token to the console (they are not shown again). The admin API key is `conductoradmin` unless you set `AdminApiKeys` in `conductor.json`. Change the passwords and the admin API key before exposing the server. See [Production Hardening](#production-hardening).

The first-run tenant is `default`, and the printed "API Key" (the tenant bearer token) belongs to the global-admin user in that tenant. A global-admin credential can manage any tenant: it names the tenant with `"TenantId"` in create and update bodies, or `?tenantId=` on other routes. When a write leaves the tenant out, it acts on the credential's own tenant (`default` for the first-run token). When a read leaves it out, it returns every tenant's resources. The examples in this document name `default` explicitly. A credential for a non-admin user always acts in its own tenant, and the server ignores a `TenantId` it sends. System administrator credentials (the admin API key or the admin headers) have no tenant, so their create, update, delete, and action requests must name one or they get `400` (`TenantId is required: ...`).

A credential can call only the virtual model runners in its own tenant, and this includes global admins. To use the first-run token with Claude Code or an SDK, create the VMR in tenant `default`.

#### Build and Run Dashboard

```bash
cd dashboard
npm install
npm run dev
```

To allow the Vite dashboard dev server to accept requests from outside `localhost`, bind it to all interfaces:

```bash
npm run dev -- --host 0.0.0.0
```

The dashboard listens on port `9100`, so other machines on the network can open `http://<your-machine-ip>:9100`. On Windows, use `ipconfig` to find the machine IP. If the page is unreachable from another machine, allow inbound TCP traffic for port `9100` in Windows Firewall.

For a built preview that also accepts external requests:

```bash
npm run build
npm run preview -- --host 0.0.0.0
```

#### Dashboard Navigation

The sidebar groups pages into two sections. Pages with tabs keep the active tab in the URL (`?tab=`), so a tab can be bookmarked or linked directly.

| Section | Page | Tabs |
|---|---|---|
| Workspace | Dashboard | |
| Workspace | Virtual Runners (`/runners`) | Virtual Model Runners, Reservations, Model Access Policies |
| Workspace | Endpoints (`/endpoints`) | Model Runner Endpoints, Endpoint Groups, Load Balancing Policies |
| Workspace | Models (`/models`) | Model Definitions, Model Configurations |
| Workspace | Traffic (QoS) (`/qos`) | Monitor, Profiles, Traffic Classes |
| Workspace | Observability (`/observability`) | Request History, Analytics |
| Administration | Access (`/access`) | Tenants, Users, Credentials, Administrators (system admins) |
| Administration | System (`/system`) | API Explorer, Backup & Restore (admins) |

Replay Tour and Setup Wizard are in the **?** help menu in the page header. The previous page URLs (for example `/vmr`, `/reservations?vmrId=...`, `/qos-monitor`, `/analytics`) redirect to the matching page and tab, keeping their query parameters. The sidebar and tabs are defined in `dashboard/src/config/navConfig.jsx`.

## Documentation

- [REST_API.md](./REST_API.md): management API routes, resource shapes, proxy behavior, request history, analytics, and observability.
- [TELEMETRY.md](./TELEMETRY.md): OpenTelemetry metrics and traces, the Prometheus/Grafana/Loki/Tempo stack, dashboards, and how to integrate telemetry into your environment.
- [QOS_OVERVIEW.md](./QOS_OVERVIEW.md): per-VMR QoS queueing: concepts, traffic classes, disciplines, how to classify and configure profiles, and how to monitor them.
- [MCP_API.md](./MCP_API.md): the tools exposed by the optional MCP server.
- [ANALYTICS.md](./ANALYTICS.md): product plan for the Analytics workspace.
- [ANALYTICS_PLAN.md](./ANALYTICS_PLAN.md): implementation tracker for the Analytics workspace.
- [ADR 0002](./docs/adr/0002-analytics-workspace.md): Analytics workspace API, retention, authorization, saved-report, and export decisions.
- [ADR 0003](./docs/adr/0003-virtual-model-runner-reservations.md): VMR reservation admission, observability, API, and backup/restore decisions.
- [ADR 0005](./docs/adr/0005-anthropic-provider.md): the Anthropic provider, inbound `x-api-key` on Anthropic VMRs, error shape, and header forwarding.
- [ADR 0006](./docs/adr/0006-decision-models-and-explicit-types.md): decision models, the TypeSafe and Ollaya split, explicit endpoint types, the compatibility table, and classification by VMR type.
- [MANAGING_RESERVATIONS.md](./MANAGING_RESERVATIONS.md): VMR reservation operations, enforcement order, troubleshooting, and internals.
- [ACCESS_POLICIES.md](./ACCESS_POLICIES.md): practical model access policy authoring guide with real-world examples.
- [TESTING.md](./archive/TESTING.md): test architecture and commands.
- [Conductor.postman_collection.json](./Conductor.postman_collection.json): Postman collection covering management, validation, model access, routing, analytics, observability, and proxied Anthropic and decision routes.
- [CHANGELOG.md](./CHANGELOG.md): unreleased and historical change notes.

## Testing

Conductor's automated tests use **Touchstone** so the same shared test cases can run through multiple hosts.

- `src/Test.Shared/` contains the authoritative test definitions.
- `src/Test.Xunit/` exposes the shared suite through xUnit.
- `src/Test.Nunit/` exposes the same suite through NUnit.
- `src/Test.Automated/` runs the suite through the Touchstone console runner.

Common commands:

```bash
# Run framework-hosted tests
dotnet test src/Conductor.sln

# Run the console host
dotnet run --project src/Test.Automated/Test.Automated.csproj
```

See [TESTING.md](./archive/TESTING.md) for the full testing guide.

## AOT support

Not supported. Conductor is not built or tested as a native AOT binary, for these reasons:

- Test discovery uses reflection.
- JSON serialization relies on reflection-based `System.Text.Json`.
- Nullable reference types are disabled in the server projects.

## SDKs

Conductor ships lightweight SDKs for common management-plane workflows:

- `sdk/javascript/` for Node.js and browser-adjacent tooling
- `sdk/python/` for Python automation and ops scripts
- `sdk/csharp/` for .NET automation and ops tooling

The SDKs include helpers for:

- validation routes
- VMR effective configuration preview
- explain-routing simulations
- create, read, update, and delete for model runner endpoints, model definitions, and virtual model runners
- an `ApiType` definition with all nine endpoint types and the VMR compatibility table
- revealing an endpoint's full API key (`RevealModelRunnerEndpointApiKeyAsync`, `revealModelRunnerEndpointApiKey`, `reveal_model_runner_endpoint_api_key`)
- endpoint drain, resume, and quarantine actions
- endpoint and virtual model runner model load or verification requests
- model access policy CRUD, validation, evaluation, and effective-access simulation
- VMR reservation CRUD, validation, scoped listing, and effective-access simulation
- request-history search, summary, detail, analytics, and bulk deletion
- analytics catalog, query, summary, TTFT, token usage, estimate-only cost, user, and access/reliability routes
- observability metrics summary and text export

## Upgrading to v1.1.0

v1.1.0 adds new endpoint types and makes API type handling explicit. Database columns are added automatically on startup. Review these changes before upgrading:

- **Paths outside a VMR's namespace now return 404.** Every VMR classifies requests by its configured `ApiType`, and a path outside that type's namespace is answered with `404` and never forwarded. For example, an OpenAI SDK pointed at an Ollama VMR's `/v1/chat/completions` used to reach Ollama's own OpenAI-compatible API, only because of how paths were classified. It now gets `404` with the message `The path '/v1/chat/completions' is outside the '/api/' namespace of this Ollama virtual model runner.` To keep that traffic working, create an `OpenAI` VMR with an `OpenAICompatible` endpoint that points at the Ollama host (for example port `11434`). The namespaces are listed in [Virtual Model Runner Proxy](#virtual-model-runner-proxy).
- **Existing `OpenAI` endpoints keep their type.** If an `OpenAI` endpoint points at a server that is not OpenAI (LM Studio, llama.cpp, Ollama's `/v1` API, and so on), change its `ApiType` to `OpenAICompatible`. Conductor does not change it automatically. An `OpenAI` VMR accepts both types, so the VMR needs no change. After the change, model load reports `Verified` instead of `VerifiedRemote`, and the dashboard no longer flags its probes as billable. In the same way, an `Ollama` endpoint that points at Ollama's hosted service should become `OllamaCloud`.
- **Endpoint `ApiKey` values are masked in responses.** Reads, lists, creates, and updates return `****` plus the last four characters (keys of four characters or fewer return `****`), or `null` when no key is set. Use `GET /v1.0/modelrunnerendpoints/{id}/apikey` to read the full value; each call writes an audit log line that does not contain the key. Backup exports still contain the real keys, so treat backup files as sensitive.
- **Omitting `ApiKey` on update now keeps the stored key.** Previously an update without `ApiKey` cleared it. Now an omitted or `null` `ApiKey`, or the masked value returned by a read, keeps the stored key. Send an empty string (`""`) to clear it, or any other value to replace it.
- **Explicit header lists in `conductor.json` replace the new defaults.** If your `conductor.json` sets `RequestHistory.RedactedHeaders` explicitly, add `x-api-key` and `anthropic-api-key` to it so Anthropic credentials are not written to request history. If it sets `Webserver.Cors.AllowedHeaders` explicitly and browser clients call Anthropic VMRs, add `anthropic-version`, `anthropic-beta`, and `x-api-key`.
- **Seeded Inference First QoS profiles are not rewritten.** Tenants seeded before v1.1.0 keep their existing `Inference First` rules. To send Anthropic model listing, model lookup, and token counting, and TypeSafe model listing, to the `metadata` class, add rules for `AnthropicListModels`, `AnthropicGetModel`, `AnthropicCountTokens`, and `TypeSafeListModels`. See [QOS_OVERVIEW.md](./QOS_OVERVIEW.md).
- **Ollama-only features reject `OllamaCloud`.** Model management (list, pull, delete), keep-warm, and RigMonitor apply only to `Ollama` endpoints.
- **The dashboard no longer guesses types from hostnames.** Defaults are applied only when you select a type.
- **Credentials can call only VMRs in their own tenant.** A proxied request whose credential belongs to a different tenant than the VMR gets `403` with `X-Conductor-Error-Code: CredentialTenantMismatch` (`permission_error` on Anthropic VMRs), whatever the model access settings. Before v1.1.0 the check ran only when model access control was enabled. Global admin users are included: give them a credential in each tenant whose VMRs they call. System administrator API keys have no tenant and are not checked.
- **System administrator writes must name the tenant.** Create, update, delete, validate, drain, resume, and quarantine requests, plus endpoint and VMR `load-model`, `keep-warm/run`, `runtime-stats/reset`, `runtime-backoff/clear`, and Ollama model pull and delete, sent with the admin API key or admin headers return `400` with `TenantId is required: system administrator credentials have no tenant, so supply TenantId in the request body (create and update) or the tenantId query parameter (delete and other routes).` when the tenant is missing. They used to fail with `500`. A global admin user that leaves the tenant out now acts on its own tenant. Reads are unchanged: without a tenant they return every tenant. Analytics saved reports are not affected, because a report without a tenant is a global report.
- **Revealing an endpoint key needs a tenant admin.** `GET /v1.0/modelrunnerendpoints/{id}/apikey` now requires a tenant admin, a global admin, or a system administrator. Standard users get `401`.
- **Strict-mode rejections no longer return 401.** `StrictModeModelRejected`, `ModelNotAttached`, and `StrictModeNoModels` now return `404`, and `StrictModeModelRequired` returns `400`. All four used to return `401`, which clients read as a bad credential.
- **`Ollaya` and `OpenAICompatible` endpoints need a port.** A create request without `Port` fails with `PortRequired`. These endpoints used to fall back to port 11434, which is Ollama's port. An update that omits `Port` keeps the stored port, and existing endpoints are not changed.
- **Deleting a VMR or a tenant deletes its request history.** Conductor first removes the request history rows, analytics events, and detail files of the VMR (or of every VMR in the tenant), then returns `204`. Export anything you need to keep before deleting. Before this change the delete failed with `500` once the VMR had request history.
- **Tenant routes require authentication.** Before v1.1.0, every tenant route except purge accepted requests without credentials. Now creating, updating, deleting, and purging a tenant require a system administrator. Reading one tenant requires a global admin or a system administrator. Listing tenants works for any authenticated caller, but users who are not global admins see only their own tenant.
- **User responses no longer contain passwords.** Create, read, list, and update responses leave out `Password`. An update that omits `Password` keeps the stored one, so a client can send back a user it read without resetting the password.
- **Creating a user requires a password.** `POST /v1.0/users` without a non-empty `Password` now returns `400`. Before this change the user was created with the password `password`. Existing users are not changed.
- **The server exits if its port is in use.** When `Webserver.Port` is taken, or the listener does not start within `Webserver.StartupTimeoutMs`, the server logs the reason and exits with code `1`. A port that is already in use is detected before health checks start, and health checks, keep-warm, and request history cleanup start only after the listener is up, so a server that fails to listen never probes endpoints. The server used to keep running, and probing endpoints, without accepting connections.

## API Overview

### Supported Provider Types

Conductor supports nine model runner endpoint types. Six are client-facing: a virtual model runner can expose them as its API. The other three are endpoint-only types that you attach to a VMR of a compatible type.

| Type | `ApiType` value | Wire API | Upstream auth | Client-facing (VMR type) | Notes |
|------|-----------------|----------|---------------|--------------------------|-------|
| Ollama | `Ollama` (0) | Ollama `/api/*` | Bearer when a key is set | Yes | Local Ollama. Supports model management, keep-warm, and RigMonitor. |
| OpenAI | `OpenAI` (1) | OpenAI `/v1/*` | Bearer | Yes | Hosted OpenAI. Chat, completions, embeddings, and model listing. Model load reports `VerifiedRemote`. |
| Gemini | `Gemini` (2) | Gemini `/v1beta/*` | `x-goog-api-key` | Yes | Hosted Gemini. `models/{model}:generateContent`, streaming, embeddings, and model listing. |
| vLLM | `vLLM` (3) | OpenAI `/v1/*` | Bearer when a key is set | Yes | Self-hosted vLLM using the OpenAI-compatible surface. |
| Anthropic | `Anthropic` (4) | Anthropic `/v1/*` | `x-api-key` and `anthropic-version` | Yes | Anthropic Messages API, streaming, token counting, and models. See [Using Claude Code and the Anthropic SDKs with Conductor](#using-claude-code-and-the-anthropic-sdks-with-conductor). |
| TypeSafe | `TypeSafe` (5) | TypeSafe System One `/v1/*` | Bearer (required) | Yes | Hosted Jev decision models at `api.typesafe.ai`. See [Decision models (TypeSafe Jev and Ollaya)](#decision-models-typesafe-jev-and-ollaya). |
| Ollaya | `Ollaya` (6) | TypeSafe System One `/v1/*` | Bearer when a key is set | No (attach to a `TypeSafe` VMR) | Local Ollaya runtime serving the System One decision API. |
| Ollama Cloud | `OllamaCloud` (7) | Ollama `/api/*` | Bearer (required) | No (attach to an `Ollama` VMR) | Hosted Ollama at `ollama.com`. No model management, keep-warm, or RigMonitor. Model load reports `VerifiedRemote`. |
| OpenAI-compatible | `OpenAICompatible` (8) | OpenAI `/v1/*` | Bearer when a key is set | No (attach to an `OpenAI` VMR) | Any OpenAI-compatible server, such as LM Studio, llama.cpp, or Ollama's `/v1` API. Model load reports `Verified`. |

The API accepts and returns the type name. The number in parentheses is the value stored in the database; values are only ever appended.

#### VMR-to-endpoint compatibility

A VMR's `ApiType` is the API its clients call. Each VMR type accepts these endpoint types:

| VMR `ApiType` | Accepted endpoint `ApiType` |
|---------------|-----------------------------|
| `Ollama` | `Ollama`, `OllamaCloud` |
| `OpenAI` | `OpenAI`, `OpenAICompatible`, `vLLM` |
| `vLLM` | `vLLM` |
| `Gemini` | `Gemini` |
| `Anthropic` | `Anthropic` |
| `TypeSafe` | `TypeSafe`, `Ollaya` |

Validation enforces the table through REST, MCP, and backup restore. A VMR whose `ApiType` is `Ollaya`, `OllamaCloud`, or `OpenAICompatible` is rejected with `VmrApiTypeNotClientFacing`, and an attached endpoint whose type is not accepted is rejected with `EndpointApiTypeMismatch`. Conductor does not translate between wire formats: an OpenAI client cannot call an Anthropic upstream.

#### Everything is explicit

The operator chooses each endpoint's `ApiType` and each VMR's `ApiType`. Conductor never infers a type, a provider, or a default from a hostname, URL path, response field, or status code. Type defaults are applied only from the selected type: the dashboard fills them in when you pick a type, and the server applies them on create to fields the request omits. Existing types (`Ollama`, `OpenAI`, `Gemini`, `vLLM`) keep their previous defaults.

| Type | Host, port, SSL | Health check | Auth on health check | Health checks enabled | Interval |
|------|-----------------|--------------|----------------------|-----------------------|----------|
| `Anthropic` | `api.anthropic.com`, 443, on | `GET /v1/models?limit=1` | yes | yes | 60,000 ms |
| `TypeSafe` | `api.typesafe.ai`, 443, on | `POST /v1/systemone` with a one-question decision probe | yes | **no** | 60,000 ms |
| `Ollaya` | `127.0.0.1`, operator supplies the port (required), off | `GET /v1/models` | no | yes | 5,000 ms |
| `OllamaCloud` | `ollama.com`, 443, on | `GET /api/tags` | yes | yes | 60,000 ms |
| `OpenAICompatible` | `127.0.0.1`, operator supplies the port (required), off | `GET /v1/models` | no | yes | 5,000 ms |

`Ollaya` and `OpenAICompatible` have no default port, so a create request without `Port` fails validation with `PortRequired`. An update that omits `Port` keeps the stored port.

### Authentication

Conductor supports two authentication methods:

1. **Header-based**: Include `x-tenant-id`, `x-email`, and `x-password` headers
2. **Bearer Token**: Include `Authorization: Bearer {token}` header

On `Anthropic` VMRs only, a Conductor credential may also be sent in the `x-api-key` header, because that is where Anthropic clients send their key. Other VMR types never accept `x-api-key` as a credential. Inbound credentials are never forwarded upstream; Conductor authenticates to the upstream with the endpoint's own key.

### User Permission Model

Users have three permission levels:

| Permission | Description |
|------------|-------------|
| **Global Admin** (`IsAdmin=true`) | Cross-tenant access to management resources. Proxied requests still need a credential in the VMR's tenant |
| **Tenant Admin** (`IsTenantAdmin=true`) | Can manage users and credentials, and reveal endpoint API keys, within their own tenant |
| **Standard User** | Can only access model configurations, endpoints, runners, and virtual runners in their tenant. Cannot reveal endpoint API keys |

- **Global Admins** can operate on any tenant by specifying `TenantId` in their requests. A write without `TenantId` acts on their own tenant
- **Tenant Admins** have elevated privileges within their assigned tenant, including creating credentials for other users in that tenant
- **Standard Users** have read/write access to non-administrative resources
- **System administrators** (the admin API key or the admin headers) belong to no tenant, so their writes must name one. They alone can create, update, delete, and purge tenants
- Every credential, including a global admin's, can call only the virtual model runners in its own tenant. A credential from another tenant gets `403` (`CredentialTenantMismatch`)

### Endpoints

| Entity | Prefix | API Endpoint |
|--------|--------|--------------|
| Administrator | `admin_` | `/v1.0/administrators` |
| Tenant | `ten_` | `/v1.0/tenants` |
| User | `usr_` | `/v1.0/users` |
| Credential | `cred_` | `/v1.0/credentials` |
| Model Runner Endpoint | `mre_` | `/v1.0/modelrunnerendpoints` |
| Endpoint Group | `egp_` | `/v1.0/endpointgroups` |
| Model Definition | `md_` | `/v1.0/modeldefinitions` |
| Model Configuration | `mc_` | `/v1.0/modelconfigurations` |
| Load Balancing Policy | `lbp_` | `/v1.0/loadbalancingpolicies` |
| Model Access Policy | `map_` | `/v1.0/modelaccesspolicies` |
| Virtual Model Runner | `vmr_` | `/v1.0/virtualmodelrunners` |
| VMR Reservation | `vmrr_` | `/v1.0/vmrreservations` |
| VMR Reservation Subject | `vmrrs_` | nested in VMR reservation responses |
| QoS Runtime | - | `/v1.0/qosruntime` |
| Request History | `req_` | `/v1.0/requesthistory` |
| Request History Summary | - | `/v1.0/requesthistory/summary` |
| Request Analytics | `rae_` | `/v1.0/requesthistory/analytics/overview` |
| Analytics Workspace | - | `/v1.0/analytics` |
| Observability Metrics | - | `/v1.0/observability/metrics` |

### Model Loading

Tenant admins can ask Conductor to load or verify a model before user traffic reaches it:

- `POST /v1.0/modelrunnerendpoints/{id}/load-model`
- `POST /v1.0/virtualmodelrunners/{id}/load-model`

For Ollama, `ProbeKind=Auto` uses a minimal native generation probe and can keep the model resident with `KeepAlive`. The request's `TimeoutMs` (default 300,000, maximum 1,800,000) bounds the whole load. If the runner gives up first, for example Ollama's `OLLAMA_LOAD_TIMEOUT`, the outcome is `RunnerLoadTimeout` rather than `Failed`. For vLLM, OpenAI, OpenAI-compatible servers, and Gemini, `Auto` defaults to metadata verification because those providers generally do not expose a host-local load primitive through their public APIs. Explicit generation or embedding probes for hosted providers may be billable.

The newer types behave as follows:

- `OllamaCloud`: `Auto` sends the same native generation probe as Ollama, which is billable. `KeepAlive` is ignored and the outcome is `VerifiedRemote`.
- `OpenAICompatible`: `Auto` lists `/v1/models` and reports `Verified`. Probes are not flagged as billable.
- `Anthropic`: `Auto` and `MetadataOnly` call `GET /v1/models/{id}` and report `VerifiedRemote`. `ChatCompletion` sends `POST /v1/messages` with `max_tokens: 1`, which is billable. Other probe kinds report `UnsupportedApiType`.
- `TypeSafe`: `Auto` and `Decision` send a one-question decision probe to `POST /v1/systemone` and report `VerifiedRemote`. This is billable. Hosted TypeSafe has no model metadata API, so `MetadataOnly` reports `UnsupportedApiType`.
- `Ollaya`: `Auto` and `MetadataOnly` list `GET /v1/models` and report `Verified`. `Decision` sends the one-question probe.

See [REST_API.md](./REST_API.md#model-loading) for request fields, outcome codes, dashboard behavior, SDK examples, and provider caveats.

### Keep Warm

Set `KeepWarm.Enabled` on a VMR (dashboard: **Keep models warm** in the VMR form) to keep its models loaded on its eligible Ollama endpoints. Every `IntervalMs` (default 5 minutes) Conductor checks residency with `/api/ps` and loads any model that is not resident, using `KeepAlive` (default `30m`) and `TimeoutMs` (default 300,000). A warm-up load takes one of the endpoint's `MaxParallelRequests` slots, so client requests queue behind it, and a busy endpoint is retried at the next interval. Passes run one at a time across the server.

Status and a **Run now** action are in the VMR's **Effective Config** view, or through `GET /v1.0/virtualmodelrunners/{id}/keep-warm` and `POST /v1.0/virtualmodelrunners/{id}/keep-warm/run`. See [REST_API.md](./REST_API.md#keep-warm).

### VMR Reservations

VMR reservations schedule exclusive access windows for a Virtual Model Runner. During the active window, and during any configured admission drain lead time, Conductor admits only the users and credentials listed on the reservation. Outside the window, normal on-demand VMR access continues and existing ACL/model-access checks still apply after a reservation participant is allowed.

Operators can manage reservations from the **Reservations** tab of the dashboard's **Virtual Runners** page, from VMR row actions, or through `/v1.0/vmrreservations`. The dashboard supports listing, refreshing, VMR-scoped creation, validating, row-click editing, deactivating, row-level JSON inspection, VMR reservation badges, request-history reservation filters/detail fields, analytics reservation-denial cards, and evaluating effective reservation access for a candidate user or credential. Reservation denials are recorded in logs, request history, and request analytics with machine-readable reasons such as `ReservationDenied`, `ReservationDrainDenied`, `ReservationAuthenticationRequired`, and `ReservationConflict`. Backup/restore includes reservation records and subjects.

See [MANAGING_RESERVATIONS.md](./MANAGING_RESERVATIONS.md) for operational guidance, API examples, enforcement order, troubleshooting, and implementation details.

### Ollama Model Management

Ollama-type model runner endpoints also expose tenant-admin model management:

- `GET /v1.0/modelrunnerendpoints/{id}/ollama/models`
- `POST /v1.0/modelrunnerendpoints/{id}/ollama/models/pull`
- `POST /v1.0/modelrunnerendpoints/{id}/ollama/models/delete`

In the dashboard, use `Manage Models` from an Ollama endpoint action menu to list local models, pull a new model, or delete an installed model. These routes reject `OllamaCloud` endpoints with `400`, because the hosted service does not support model management.

## RigMonitor And Policy Routing

Model runner endpoints can optionally declare a `RigMonitor` sidecar configuration. Conductor collects that data during the normal endpoint health-check loop, caches it in memory, and exposes it through endpoint health and telemetry routes. The proxy path never performs live RigMonitor calls while handling client traffic.

### Endpoint RigMonitor Configuration

Each `ModelRunnerEndpoint` can include a `RigMonitor` object with fields such as:

- `Enabled`
- `HostnameOverride`
- `Port`
- `UseSsl`
- `TimeoutMs`
- `CollectDuringHealthCheck`
- `RequireReadyz`
- `HealthAffectedByRigMonitor`
- `MaxTelemetryAgeMs`
- `CapabilitiesRefreshIntervalMs`
- `TelemetryProfile`
- `TelemetrySelectors`

Useful routes:

- `GET /v1.0/modelrunnerendpoints/health`
- `GET /v1.0/modelrunnerendpoints/{id}/health`
- `GET /v1.0/modelrunnerendpoints/{id}/rigmonitor`

### First-Class Load Balancing Policies

Load-balancing policies are tenant-scoped resources attached to a VMR by `LoadBalancingPolicyId`.

- Policy CRUD: `/v1.0/loadbalancingpolicies`
- Metrics catalog: `GET /v1.0/loadbalancingpolicies/metrics`
- VMR attachment: set `LoadBalancingPolicyId` on `/v1.0/virtualmodelrunners`

Policies combine:

- `Filters`: hard eligibility checks such as `health.isHealthy == true` or `rig.gpu.available == true`
- `Ranking`: weighted numeric comparisons such as lowest CPU, lowest GPU utilization, or fewest in-flight requests
- `FallbackMode`: use the VMR's legacy load-balancing mode or fail closed
- `TieBreaker`: round-robin, random, or first available when scores are equal

Built-in VMR `LoadBalancingMode` values are `RoundRobin`, `Random`, `FirstAvailable`, `LeastRecentlyUsed`, and `Adaptive`. `LeastRecentlyUsed` selects the eligible endpoint with the oldest route-scoped assignment history and uses endpoint order as the deterministic tie-breaker for endpoints with no history. `Adaptive` samples eligible endpoints and scores them with runtime success, latency, time-to-first-token, pending-work, and configured endpoint-weight signals.

Adaptive routing is opt-in per VMR. Operators can configure adaptive scoring weights, cold-start behavior, transient backoff windows, and reusable endpoint groups with priorities or traffic weights. Endpoint groups are tenant-scoped resources managed through `/v1.0/endpointgroups` and attached to VMRs by `EndpointGroupIds`. Runtime stats are in-memory operational state, not durable configuration; they can be inspected with `GET /v1.0/virtualmodelrunners/{id}/runtime-stats`, reset with `POST /v1.0/virtualmodelrunners/{id}/runtime-stats/reset`, and cleared for transient backoff with `POST /v1.0/virtualmodelrunners/{id}/runtime-backoff/clear`.

Example policy payload:

```json
{
  "Name": "Lowest GPU Utilization",
  "MaxTelemetryAgeMs": 30000,
  "Filters": [
    { "Metric": "health.isHealthy", "Operator": "Equal", "ValueType": "Boolean", "Value": "true" },
    { "Metric": "health.hasCapacity", "Operator": "Equal", "ValueType": "Boolean", "Value": "true" },
    { "Metric": "rig.gpu.available", "Operator": "Equal", "ValueType": "Boolean", "Value": "true" }
  ],
  "Ranking": [
    { "Metric": "rig.gpu.avgUtilizationPercent", "Direction": "Ascending", "Weight": 1.0 }
  ],
  "FallbackMode": "UseLegacyLoadBalancingMode",
  "TieBreaker": "RoundRobin",
  "Active": true
}
```

Example VMR attachment:

```json
{
  "Name": "GPU Chat VMR",
  "BasePath": "/v1.0/api/gpu-chat/",
  "LoadBalancingMode": "RoundRobin",
  "LoadBalancingPolicyId": "lbp_example",
  "ModelRunnerEndpointIds": ["mre_a", "mre_b"],
  "Active": true
}
```

### Explain, Validate, And Preview

The management plane now exposes first-class safety and explainability routes:

- `POST /v1.0/modelrunnerendpoints/validate`
- `POST /v1.0/modeldefinitions/validate`
- `POST /v1.0/modelconfigurations/validate`
- `POST /v1.0/loadbalancingpolicies/validate`
- `POST /v1.0/virtualmodelrunners/validate`
- `GET /v1.0/virtualmodelrunners/{id}/effective`
- `GET /v1.0/virtualmodelrunners/{id}/runtime-stats`
- `POST /v1.0/virtualmodelrunners/{id}/runtime-stats/reset`
- `POST /v1.0/virtualmodelrunners/{id}/runtime-backoff/clear`
- `POST /v1.0/virtualmodelrunners/{id}/explain-routing`

Recommended operator flow:

1. Validate drafts before saving.
2. Inspect the effective VMR preview to confirm endpoint coverage, request permissions, policy attachment, and model pinning.
3. Inspect runtime stats when adaptive routing is enabled so you can see success/error EWMA, latency EWMA, TTFT EWMA, pending requests, and transient backoff state.
4. Use explain-routing with a representative request body when you need to understand why a request would route, mutate, reuse a session pin, or be denied.

Request-history search and detail responses also expose adaptive routing evidence, including selection strategy, endpoint group, selected score, policy fallback state, and transient backoff reason when history is enabled for the VMR.

### Model Access Policies

Model access policies are tenant-scoped ACL resources attached to virtual model runners through `ModelAccessPolicyId`. They decide whether a credential, user, tenant, or labeled subject can use a model definition, model name, model label, VMR, or any resource for a specific action such as completions, embeddings, list-models, and model management. See [ACCESS_POLICIES.md](./ACCESS_POLICIES.md) for authoring guidance and real-world policy examples.

Management routes require tenant-admin access:

- `GET /v1.0/modelaccesspolicies`
- `POST /v1.0/modelaccesspolicies`
- `GET /v1.0/modelaccesspolicies/{id}`
- `PUT /v1.0/modelaccesspolicies/{id}`
- `DELETE /v1.0/modelaccesspolicies/{id}?forceDetach=true`
- `POST /v1.0/modelaccesspolicies/validate`
- `POST /v1.0/modelaccesspolicies/{id}/evaluate`
- `GET /v1.0/modelaccesspolicies/effective`

Runtime behavior:

- `Disabled` mode allows traffic and records disabled state.
- `Monitor` mode allows traffic but records would-deny decisions in routing evidence, request history, analytics, and audit logs.
- `Enforce` mode blocks denied proxy requests with `403 Forbidden` before endpoint selection or provider calls.
- Missing or invalid proxy credentials return `401 Unauthorized` when `RequireCredentialForProxy` is enabled.
- Successful authentication followed by an ACL denial returns `403 Forbidden`.
- List-models responses can be filtered, synthesized from allowed VMR model definitions, or passed through raw according to `ListModelsBehavior`. Anthropic and TypeSafe model lists keep their provider shapes.
- Decision requests on `TypeSafe` VMRs use the `Decisions` action. Policies that allow `Completions` do not also allow `Decisions`.

Production rollout path:

1. Leave enforcement disabled or in monitor mode after upgrade.
2. Create and validate policies with explicit allow and deny rules.
3. Attach policies to a small number of VMRs.
4. Inspect would-deny request history, analytics, and routing explanations.
5. Enable enforce mode after expected traffic is clean.

See [ADR 0001](./docs/adr/0001-model-access-policy-semantics.md) for the accepted semantics and compatibility defaults.

### Operator Notes

- Keep unauthenticated RigMonitor sidecars on trusted networks only.
- `TelemetryProfile` now defaults to `Full`; narrow it only if you need to reduce health-check telemetry cost.
- Prefer `FallbackMode = UseLegacyLoadBalancingMode` first, then move selected VMRs to `FailClosed` once telemetry freshness and sidecar reliability are proven.
- Stale or missing telemetry can make a telemetry-dependent endpoint ineligible for policy evaluation.

### Virtual Model Runner Proxy

Virtual model runners expose an API at their configured base path. For example, a VMR with base path `/v1.0/api/my-vmr/` would expose:

- **OpenAI API**: `/v1.0/api/my-vmr/v1/chat/completions`, `/v1.0/api/my-vmr/v1/embeddings`
- **vLLM API**: `/v1.0/api/my-vmr/v1/chat/completions`, `/v1.0/api/my-vmr/v1/embeddings`
- **Gemini API**: `/v1.0/api/my-vmr/v1beta/models/gemini-2.5-flash:generateContent`, `/v1.0/api/my-vmr/v1beta/models/text-embedding-004:embedContent`
- **Ollama API**: `/v1.0/api/my-vmr/api/generate`, `/v1.0/api/my-vmr/api/chat`
- **Anthropic API**: `/v1.0/api/my-vmr/v1/messages`, `/v1.0/api/my-vmr/v1/messages/count_tokens`, `/v1.0/api/my-vmr/v1/models`
- **TypeSafe System One API**: `/v1.0/api/my-vmr/v1/systemone`, `/v1.0/api/my-vmr/v1/models`

Requests are classified by the VMR's configured `ApiType`, never by the path alone. Each type has one path namespace:

| VMR type | Namespace | Classified routes | Other paths inside the namespace | Paths outside the namespace |
|----------|-----------|-------------------|----------------------------------|-----------------------------|
| `Ollama` | `/api/` | `/api/generate`, `/api/chat`, `/api/tags`, `/api/embed`, `/api/embeddings`, `/api/pull`, `/api/delete`, `/api/ps`, `/api/show` | forwarded unclassified | `404` |
| `OpenAI`, `vLLM` | `/v1/` | `/v1/chat/completions`, `/v1/completions`, `/v1/models`, `/v1/embeddings` | forwarded unclassified | `404` |
| `Gemini` | `/v1beta/` | `/v1beta/models`, `models/{model}:generateContent`, `:streamGenerateContent`, `:embedContent` | forwarded unclassified | `404` |
| `Anthropic` | `/v1/` | `POST /v1/messages`, `POST /v1/messages/count_tokens`, `GET /v1/models`, `GET /v1/models/{id}` | `/v1/messages/batches*` and `/v1/files*` return `404`; others are forwarded unclassified | `404` |
| `TypeSafe` | `/v1/` | `POST /v1/systemone`, `GET /v1/models` | forwarded unclassified | `404` |

A `404` for a path outside the namespace says which namespace applies, for example `The path '/v1/chat/completions' is outside the '/api/' namespace of this Ollama virtual model runner.` The request is never forwarded upstream. `GET` and `HEAD` on the VMR base URL remain the unauthenticated health probe.

A path that contains a `.` or `..` segment, a backslash, or a percent-encoded `.`, `/`, or `\` (`%2e`, `%2f`, `%5c`, including double-encoded forms) also returns `404`, with `X-Conductor-Error-Code: UnsafePath`, and is never forwarded. This check runs before classification, so a path such as `/v1/x/../files` cannot reach a route the VMR blocks.

Errors that Conductor itself generates on any VMR type carry Conductor's reason code in the `X-Conductor-Error-Code` response header, for example `CredentialTenantMismatch`, `UnsafePath`, `RequestBodyInvalid`, `StrictModeModelRejected`, or `ModelDoesNotSupportDecisions`. A request body that is not valid JSON on a completion, embedding, or model management route returns `400` with `RequestBodyInvalid` (`invalid_request_error` on Anthropic VMRs) and is not forwarded. See [REST_API.md](./REST_API.md#proxy-error-codes) for the full list.

Conductor's upstream HTTP clients never store or send cookies, so a `Set-Cookie` from an upstream is not replayed on later requests from other clients.

#### Request timeouts

`TimeoutMs` on a VMR and on an endpoint is a **time to first byte** limit: how long Conductor waits for the upstream to return response headers. The smaller of the two applies, and both default to 300,000 ms (5 minutes). The VMR's **Effective Config** view in the dashboard, and the `effective` API, show the applied value per endpoint and which side set it.

- It must cover model load time. Ollama sends nothing while loading a model, and abandons the load if Conductor disconnects.
- Streamed requests are limited only until the first token. Non-streaming requests must finish generating within the limit, because Ollama sends headers only when generation ends.
- The runner has its own load limit (Ollama: `OLLAMA_LOAD_TIMEOUT`, default 5 minutes). Raising Conductor's limit beyond it does not help a model that cannot load in time.
- When Conductor's limit expires, the client gets `504` with `Code: "UpstreamTimeout"` and an `X-Conductor-Error-Code: UpstreamTimeout` header naming the endpoint, the VMR and the limit. A runner-side load timeout is passed through with `X-Conductor-Error-Code: RunnerLoadTimeout`.
- To avoid cold loads on live traffic, enable [Keep Warm](#keep-warm) on the VMR, or warm large models with `load-model` and a long `KeepAlive`.
- At startup, Conductor logs a warning listing VMRs and endpoints still at 60,000 ms, the default before v0.6.1.

See [REST_API.md](./REST_API.md#request-timeouts-time-to-first-byte) for details.

## Using Claude Code and the Anthropic SDKs with Conductor

An `Anthropic` VMR exposes the Anthropic Messages API, so Claude Code and the official Anthropic SDKs work through Conductor unchanged. Clients authenticate with a Conductor credential; Conductor calls Anthropic with the endpoint's own key.

1. Create an `Anthropic` endpoint with your Anthropic API key. The type defaults fill in `api.anthropic.com`, port 443, SSL, and an authenticated `GET /v1/models?limit=1` health check every 60 seconds.

   ```bash
   curl -X POST http://127.0.0.1:9000/v1.0/modelrunnerendpoints \
     -H "Authorization: Bearer $CONDUCTOR_TOKEN" -H "Content-Type: application/json" \
     -d '{"TenantId":"default","Name":"Anthropic","ApiType":"Anthropic","ApiKey":"<your Anthropic API key>"}'
   ```

   `$CONDUCTOR_TOKEN` is the "API Key" printed on first run, or any other credential bearer token. The first-run token belongs to a global admin in tenant `default`, so `"TenantId":"default"` is optional here; without it the endpoint is created in the token's own tenant. With the admin API key, `TenantId` is required. See [Build and Run Server](#build-and-run-server). The response contains the new endpoint's `Id` (`mre_...`).

   Conductor health-checks the new endpoint right away, and the first successful check marks it healthy, so the VMR can take traffic within seconds of being created. The same happens when the server starts.

2. Create an `Anthropic` VMR that uses the endpoint.

   ```bash
   curl -X POST http://127.0.0.1:9000/v1.0/virtualmodelrunners \
     -H "Authorization: Bearer $CONDUCTOR_TOKEN" -H "Content-Type: application/json" \
     -d '{"TenantId":"default","Name":"claude","BasePath":"/v1.0/api/claude/","ApiType":"Anthropic","ModelRunnerEndpointIds":["mre_..."]}'
   ```

   Replace `mre_...` with the `Id` returned in step 1.

3. Point the client at the VMR base URL with a Conductor credential. For Claude Code:

   ```bash
   export ANTHROPIC_BASE_URL="http://127.0.0.1:9000/v1.0/api/claude"
   export ANTHROPIC_AUTH_TOKEN="<your Conductor credential bearer token>"
   claude
   ```

   `ANTHROPIC_AUTH_TOKEN` is sent as `Authorization: Bearer`. `ANTHROPIC_API_KEY` is sent as `x-api-key`, which Conductor also accepts on Anthropic VMRs, so either variable works. In both cases the value is a Conductor credential, not an Anthropic key. The credential must belong to the VMR's tenant: a credential from another tenant, including a global admin's, gets `403` with `permission_error`. The dashboard shows these values in the VMR's **Connect a client** panel.

With the Python SDK:

```python
import anthropic

client = anthropic.Anthropic(
    base_url="http://127.0.0.1:9000/v1.0/api/claude",
    api_key="<your Conductor credential bearer token>",
)
message = client.messages.create(
    model="claude-opus-5-5",
    max_tokens=256,
    messages=[{"role": "user", "content": "Hello"}],
)
print(message.content[0].text)
```

What Conductor does on an Anthropic VMR:

- **Routes.** `POST /v1/messages` (including `"stream": true`, passed through as server-sent events), `POST /v1/messages/count_tokens`, `GET /v1/models`, and `GET /v1/models/{id}` are classified and governed by model access policies. Other `/v1/` paths are forwarded unclassified. Paths outside `/v1/` return `404`.
- **`anthropic-version`.** The client's header wins. If the client sends none, Conductor uses the endpoint's `AnthropicVersion`, then the server setting `Anthropic.DefaultVersion` (`2023-06-01`). `anthropic-beta` is forwarded as sent.
- **Credentials.** The Conductor credential can arrive in `Authorization` or `x-api-key`. Inbound `authorization`, `x-api-key`, and `anthropic-api-key` headers are never forwarded. Conductor sends the endpoint's key in `x-api-key`.
- **Response headers.** `request-id`, `retry-after`, `x-should-retry`, and any `anthropic-ratelimit-*` header from the upstream are copied to the client.
- **Errors.** Errors that Conductor itself generates use Anthropic's error shape, `{"type":"error","error":{"type":"...","message":"..."}}`, and keep the `X-Conductor-Error-Code` header, so the SDKs can parse them. A malformed JSON body gets `400` `invalid_request_error` (`RequestBodyInvalid`) instead of a `5xx` that the SDKs would retry. Upstream errors pass through unchanged.
- **Model configurations.** `MaxTokens` maps to `max_tokens`. `Temperature`, `TopP`, and `TopK` are injected only when the effective `anthropic-version` is earlier than `Anthropic.SamplingParametersRemovedFromVersion` (`2026-10-21`). `RepeatPenalty` and `ContextWindowSize` are never sent.
- **Usage.** Request history records input and output tokens, plus `CacheCreationInputTokens` and `CacheReadInputTokens`. `TotalTokens` is input plus output; cache tokens are recorded separately.
- **Not supported.** Message Batches (`/v1/messages/batches`) and Files (`/v1/files`) are stateful and return `404` with `not_found_error` and the message `Message Batches and Files are not supported through Conductor.`

See [REST_API.md](./REST_API.md#anthropic) for the full header, error, and usage rules.

## Decision models (TypeSafe Jev and Ollaya)

Decision models answer structured questions about an input (yes or no, a choice among options, or a score) instead of generating text. TypeSafe serves its hosted Jev models through the System One API, and Ollaya is a local runtime that serves the same API. Conductor treats decisions as a provider-neutral capability with two endpoint types:

- `TypeSafe`: hosted Jev models at `api.typesafe.ai`. An API key is required (`TypeSafeApiKeyMissing`).
- `Ollaya`: a local runtime, defaulting to `127.0.0.1` without SSL. The key is optional.

Both attach to a `TypeSafe` VMR, which must keep `AllowDecisions` set to `true` (`DecisionsDisabledOnDecisionRoute`). Mark the model definitions it serves, such as `jev-latest`, with `SupportsDecisions: true`; validation warns with `DecisionModelNotEnabled` otherwise.

```bash
curl -X POST http://127.0.0.1:9000/v1.0/api/decide/v1/systemone \
  -H "Authorization: Bearer $CONDUCTOR_TOKEN" -H "Content-Type: application/json" \
  -d '{
    "model": "jev-latest",
    "state": "My invoice was charged twice this month. Can you fix it?",
    "questions": {
      "is_question": { "type": "noul", "instructions": "Is the message a question?" },
      "category": { "type": "choice", "instructions": "Which team should handle it?", "criteria": { "billing": null, "technical": null } },
      "urgency": { "type": "score", "instructions": "How urgent is the message?", "criteria": ["not urgent", "somewhat urgent", "very urgent"] }
    }
  }'
```

Each question has a type: `noul` (binary, answered with a probability), `choice` (one of the `criteria` keys, with per-option probabilities), or `score` (a value on the scale given by the `criteria` list). The response holds one answer per question id plus `usage`.

Rules on a `TypeSafe` VMR:

- `POST /v1/systemone` is classified as `TypeSafeDecide` and `GET /v1/models` as `TypeSafeListModels`. Other `/v1/` paths are forwarded unclassified, and paths outside `/v1/` return `404`. Responses are not streamed.
- `GET /v1/models` is routed only to `Ollaya` endpoints, because hosted TypeSafe does not serve it. A VMR whose only endpoints are hosted `TypeSafe` answers it with `404` and code `ModelListingUnsupported`.
- A decision body that is not valid JSON, whose `questions` is not an object keyed by question id, that has no questions, or that has more than `TypeSafe.MaxQuestionsPerRequest` questions (default 64, minimum 1, maximum 1024) is rejected with `400` and code `DecisionRequestInvalid` before routing. The message names the problem, for example `'questions' must be an object keyed by question id, ...` when `questions` is an array.
- Model configurations are never applied to decision requests (`TypeSafeConfigurationIgnored` warns when any are attached).
- In strict mode, a decision request for a model definition without `SupportsDecisions` gets `404` (`ModelDoesNotSupportDecisions`), and a completion or embedding request for a model that supports only decisions is rejected the same way (`ModelSupportsOnlyDecisions`). The code is in the `X-Conductor-Error-Code` response header.
- Model access policies use the `Decisions` action for decision requests. A policy that allows `Completions` does not also allow `Decisions`.
- Request history records input and output tokens and the `DecisionQuestionCount`.

Health checks differ between the two endpoint types. Hosted TypeSafe has no `/v1/models` route, so its health check is a one-question `POST /v1/systemone` decision probe, the same probe PolyPrompt's `TypeSafeClient` uses to check connectivity. Each probe is a billed decision, so hosted TypeSafe health checks are **off by default** and the endpoint is treated as healthy. When you turn them on, the default interval is 60 seconds; you can lower it at a higher cost. The dashboard's health check toggle explains this in a tooltip. Ollaya does serve `GET /v1/models`, so Ollaya endpoints use that unauthenticated route every 5 seconds by default.

## QoS & Queueing

Every virtual model runner is linked to a **QoS profile** that governs how its traffic is queued under load. When a VMR's endpoints are all busy, a profiled VMR no longer bounces requests with an immediate `429`. It parks them in the discipline you configured and releases them in scheduled order as capacity frees. When there is spare capacity the layer is invisible: requests admit instantly and a priority queue behaves just like FIFO. Queueing is built on the [QoSKit](https://github.com/jchristn/qoskit) library; Conductor owns the configuration, the request path, seeding, and telemetry.

A profile has three parts. **Classification** maps a request to a named traffic class using whatever the operator chooses: a custom header, a specific credential, the requested model, a request-body attribute, the tenant, the user, the client IP, or the API family. **Topology** places each class into a queue discipline (FIFO, LIFO, priority, weighted-fair, class-based weighted-fair, low-latency, or weighted round robin) and chains nodes into a hierarchy ending at one tail. **Admission** gates against the VMR's existing endpoint capacity (the sum of its endpoints' `MaxParallelRequests`) and returns `429` with a `Retry-After` header when a queue is full or a request waits past its deadline.

Sensible defaults are seeded per tenant on startup and on tenant creation, so nothing is required to get started:

- A non-deletable **Default (FIFO)** profile that every VMR uses unless linked to another. Creating a VMR without a profile auto-assigns this default.
- A catalog of **standard traffic classes** (`realtime`, `human-interactive`, `agent-interactive`, `batch-time-bound`, `batch-background`, and `default`) that you can edit and extend.
- A ready-to-use **Standard Workloads** profile (a low-latency queue keyed on the `X-Conductor-Class` header) to link or clone.

Manage profiles and classes over REST at `/v1.0/qosprofiles` and `/v1.0/qostrafficclasses` (with `validate` and `classifier-catalog` helpers), or from the dashboard. Watch live queueing on the **Monitor** tab of the dashboard's **Traffic (QoS)** page or through `/v1.0/qosruntime`, which report each VMR's scheduler state, capacity in use, waiting requests, per-class admissions, rejections, and wait times, and endpoint slot usage. A client can select its class with a header:

```
POST /v1.0/api/{vmr}/v1/chat/completions
X-Conductor-Class: human-interactive
```

QoS emits through the same OpenTelemetry pipeline as the rest of Conductor: QoSKit's per-class metrics and hop-by-hop traces are joined by `conductor.qos.*` instruments (admissions, rejections, per-class wait duration, queue depth) and an `inference.qos.admit` trace span, all Prometheus-scrapable and surfaced by the bundled **QoS & Queueing** dashboard in the **Conductor** Grafana folder. All QoS configuration lives in the database; nothing about it is stored in `conductor.json`. See **[QOS_OVERVIEW.md](./QOS_OVERVIEW.md)** for the full guide.

## Configuration

### conductor.json

```json
{
  "Webserver": {
    "Hostname": "localhost",
    "Port": 9000,
    "Ssl": false,
    "StartupTimeoutMs": 10000,
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": ["http://localhost:9100", "http://127.0.0.1:9100"],
      "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      "AllowedHeaders": ["Content-Type", "Authorization", "x-tenant-id", "x-email", "x-password", "x-admin-apikey", "x-admin-email", "x-admin-password", "anthropic-version", "anthropic-beta", "x-api-key"],
      "ExposedHeaders": [],
      "AllowCredentials": false,
      "MaxAgeSeconds": 86400
    }
  },
  "Database": {
    "Type": "PostgreSql",
    "Hostname": "127.0.0.1",
    "Port": 5432,
    "DatabaseName": "conductor",
    "Username": "conductor",
    "Password": "conductor",
    "RequireEncryption": false
  },
  "Logging": {
    "Servers": [],
    "LogDirectory": "./logs/",
    "LogFilename": "conductor.log",
    "ConsoleLogging": true,
    "MinimumSeverity": 0
  },
  "RequestHistory": {
    "Enabled": true,
    "Directory": "./request-history/",
    "RetentionDays": 7,
    "MetadataRetentionDays": 30,
    "BodyRetentionDays": 7,
    "CleanupIntervalMinutes": 60,
    "CaptureRequestBody": true,
    "CaptureResponseBody": true,
    "RedactedHeaders": ["authorization", "x-password", "x-admin-password", "x-goog-api-key", "x-api-key", "anthropic-api-key", "cookie", "set-cookie"],
    "RedactedJsonFields": ["authorization", "api_key", "apikey", "password", "token", "bearertoken"],
    "MaxRequestBodyBytes": 65536,
    "MaxResponseBodyBytes": 65536
  },
  "Anthropic": {
    "DefaultVersion": "2023-06-01",
    "ForwardedRequestHeaders": ["anthropic-version", "anthropic-beta"],
    "ForwardedResponseHeaders": ["request-id", "retry-after", "x-should-retry"],
    "ForwardedResponseHeaderPrefixes": ["anthropic-ratelimit-"],
    "SamplingParametersRemovedFromVersion": "2026-10-21"
  },
  "TypeSafe": {
    "DefaultModel": "jev-latest",
    "MaxQuestionsPerRequest": 64
  },
  "ModelAccessControl": {
    "Enabled": false,
    "Mode": "Disabled",
    "DefaultDecision": "Permit",
    "RequireCredentialForProxy": false,
    "UnknownModelBehavior": "Deny",
    "ListModelsBehavior": "Filter",
    "CacheTtlMs": 30000,
    "AllowAdministratorBypass": false,
    "AllowGlobalAdministratorBypass": false
  }
}
```

`Webserver.Hostname` is the name the server binds to. The default, `localhost`, listens on `127.0.0.1` only; use `*` to listen on all interfaces. `Cors.AllowedOrigins` entries are compared with the browser's `Origin` header as whole values (case-insensitive), so they keep the host name the browser shows: the Vite dev server opens at `http://localhost:9100`, and a browser that opens `http://127.0.0.1:9100` sends that origin instead, which is why the example lists both.

At startup the server checks that `Webserver.Port` is free on `Webserver.Hostname`, then waits up to `Webserver.StartupTimeoutMs` (default 10,000 ms, minimum 1,000, maximum 300,000; out-of-range values are clamped) for the listener to start. If the port is in use, for example by another Conductor instance, the server writes the reason to the log and to standard error (`ERROR: port 9000 on localhost is already in use; stop the other process or change Webserver.Port in conductor.json. Exiting.`) and exits with code `1` before it starts health checks or any other background service. If the listener does not start in time, the server also logs the reason and exits with code `1`, again before any background service starts: health checks, keep-warm, and request history cleanup start only after the listener is verified, and the first health check of every endpoint then runs at once.

### Provider settings

The `Anthropic` and `TypeSafe` blocks configure provider behavior. Both are optional; omitted values use the defaults below.

| Setting | Default | Description |
|---------|---------|-------------|
| `Anthropic.DefaultVersion` | `2023-06-01` | `anthropic-version` sent upstream when neither the client nor the endpoint's `AnthropicVersion` sets one. Format `yyyy-MM-dd`; invalid values fall back to the default. |
| `Anthropic.ForwardedRequestHeaders` | `anthropic-version`, `anthropic-beta` | Client headers copied upstream on Anthropic VMRs. Credential headers are never copied, even if listed. |
| `Anthropic.ForwardedResponseHeaders` | `request-id`, `retry-after`, `x-should-retry` | Upstream response headers copied to the client. |
| `Anthropic.ForwardedResponseHeaderPrefixes` | `anthropic-ratelimit-` | Upstream response headers whose names start with one of these prefixes are copied to the client. |
| `Anthropic.SamplingParametersRemovedFromVersion` | `2026-10-21` | From this `anthropic-version` onward, model configurations do not inject `temperature`, `top_p`, or `top_k`. |
| `TypeSafe.DefaultModel` | `jev-latest` | Model used by the decision model-load probe when no model is given. |
| `TypeSafe.MaxQuestionsPerRequest` | `64` | Maximum questions in one decision request. Minimum 1, maximum 1024; out-of-range values are clamped. |

### Supported Databases

PostgreSQL is the default Docker database and is configured with:

```json
{
  "Type": "PostgreSql",
  "Hostname": "127.0.0.1",
  "Port": 5432,
  "DatabaseName": "conductor",
  "Username": "conductor",
  "Password": "conductor",
  "RequireEncryption": false
}
```

SQLite remains available for local development by setting:

```json
{
  "Type": "Sqlite",
  "Filename": "./conductor.db"
}
```

### Client IP Behind a Reverse Proxy

By default, request history, logs, and source-IP session affinity record the IP address of the connection Conductor receives. Behind a reverse proxy, or in Docker where published ports can replace the client address with the bridge gateway, that is the proxy's or gateway's address. To record the real client address, put a reverse proxy that sets `X-Forwarded-For` in front of Conductor and list it as trusted:

```json
{
  "Webserver": {
    "TrustedProxies": ["172.16.0.0/12"],
    "ForwardedForHeader": "X-Forwarded-For"
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TrustedProxies` | string[] | `[]` | IP addresses or CIDR ranges whose forwarded-for header is trusted. The header is read from right to left, skipping trusted hops, and the first untrusted address is used |
| `ForwardedForHeader` | string | `X-Forwarded-For` | Header carrying the forwarded client address chain |

Only list addresses that untrusted clients cannot connect from. Any client connecting from a trusted address can set the header to any value.

### CORS Configuration

Cross-Origin Resource Sharing (CORS) can be enabled to allow browser-based applications to access the Conductor API.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable or disable CORS support |
| `AllowedOrigins` | string[] | `[]` | List of allowed origins. Use `["*"]` for all origins |
| `AllowedMethods` | string[] | `["GET", "POST", "PUT", "DELETE", "OPTIONS"]` | Allowed HTTP methods |
| `AllowedHeaders` | string[] | `["Content-Type", "Authorization", ...]` | Allowed request headers |
| `ExposedHeaders` | string[] | `[]` | Headers exposed to the browser |
| `AllowCredentials` | bool | `false` | Allow credentials (cookies, auth headers). Cannot be used with `AllowedOrigins: ["*"]` |
| `MaxAgeSeconds` | int | `86400` | Preflight cache duration (0-86400 seconds) |

**Example: Allow all origins (development)**
```json
{
  "Webserver": {
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": ["*"]
    }
  }
}
```

**Example: Restrict to specific origins (production)**
```json
{
  "Webserver": {
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": ["https://app.example.com", "https://admin.example.com"],
      "AllowCredentials": true
    }
  }
}
```

### Request History Configuration

Request history captures request/response data for Virtual Model Runners with `RequestHistoryEnabled` set to `true`. New VMRs default this property to `true`, so Request History and Analytics populate unless an operator explicitly disables capture for that VMR. This is useful for debugging, auditing, troubleshooting, and latency analysis. Each completed entry records total response time and time to first token/byte (`FirstTokenTimeMs`). For non-streaming responses, `FirstTokenTimeMs` is set to the same value as `ResponseTimeMs`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable or disable request history globally |
| `Directory` | string | `"./request-history/"` | Directory for storing request detail JSON files |
| `RetentionDays` | int | `30` | Legacy retention knob used as a fallback when the newer retention settings are omitted |
| `MetadataRetentionDays` | int | `30` | Number of days to retain searchable ledger metadata before cleanup (1-365) |
| `BodyRetentionDays` | int | `30` | Number of days to retain request and response bodies inside detail files before they are scrubbed (1-365) |
| `CleanupIntervalMinutes` | int | `60` | Interval between cleanup runs in minutes (1-1440) |
| `CaptureRequestBody` | bool | `true` | Persist request bodies when request history is enabled for the VMR |
| `CaptureResponseBody` | bool | `true` | Persist response bodies when request history is enabled for the VMR |
| `RedactedHeaders` | string[] | `authorization`, `x-password`, `x-admin-password`, `x-goog-api-key`, `x-api-key`, `anthropic-api-key`, `cookie`, `set-cookie` | Header names redacted before persistence. An explicit list replaces the default, so deployments upgraded from earlier versions with their own list must add `x-api-key` and `anthropic-api-key`. `cookie` and `set-cookie` are always redacted, even when an explicit list leaves them out |
| `RedactedJsonFields` | string[] | built-in sensitive JSON fields | JSON field names redacted recursively before persistence |
| `MaxRequestBodyBytes` | int | `65536` | Maximum request body bytes to capture (1-10485760) |
| `MaxResponseBodyBytes` | int | `65536` | Maximum response body bytes to capture (1-10485760) |

Deleting a VMR deletes its request history (entries, analytics events, and detail files) before the VMR itself, and deleting a tenant does the same for every VMR in the tenant.

**Note:** Request history must be enabled both globally (in `conductor.json`) and per-VMR (via the `RequestHistoryEnabled` property). The per-VMR default is enabled for newly-created VMRs.

Captured request history entries include the VMR, routed model runner endpoint, matched model definition, matched model configuration, policy attachment, reservation gate dimensions when applicable, requested/effective model names, routing outcome, denial reason, mutation summary, HTTP status, body lengths, transfer type, total response time (`ResponseTimeMs`), time to first token/byte (`FirstTokenTimeMs`), trace ID, provider request ID, token counts, Anthropic prompt cache token counts (`CacheCreationInputTokens`, `CacheReadInputTokens`), the decision question count (`DecisionQuestionCount`), token throughput, analytics coverage, and dominant latency stage. Usage is parsed by the endpoint's configured type, never by guessing from the response shape. Reservation denials persist `ReservationGuid`, `ReservationName`, `ReservationDecision`, `ReservationReasonCode`, and the UTC reservation window, and request-history search/summary plus request-analytics overview can filter by reservation id and reason.

When `BodyRetentionDays` is shorter than `MetadataRetentionDays`, Conductor scrubs request and response bodies from detail files while preserving the searchable routing and latency ledger.

### Request History Summary API

The summary endpoint returns aggregated request counts grouped by time buckets, useful for charting request volume and success/failure rates over time.

```
GET /v1.0/requesthistory/summary?startUtc={ISO8601}&endUtc={ISO8601}&interval={hour|day}&vmrGuid={guid}
```

Request analytics extends the history ledger with trace-linked performance events and aggregate dashboard data:

```text
GET /v1.0/requesthistory/analytics/overview?range=lastDay&vmrGuid={guid}&endpointGuid={guid}
GET /v1.0/requesthistory/{id}/analytics
```

The overview response includes request counts, success/failure counts, reservation-denial counts, latency percentiles, analytics coverage, token totals, token throughput, time-series buckets, stage breakdowns, endpoint summaries, and slowest-request links.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `startUtc` | string | No | Start of time range (UTC, ISO 8601). Default: 1 hour ago |
| `endUtc` | string | No | End of time range (UTC, ISO 8601). Default: now |
| `interval` | string | No | Bucket interval: `minute`, `15minute`, `hour`, `6hour`, or `day`. Default: `hour` |
| `vmrGuid` | string | No | Filter by Virtual Model Runner GUID |
| `endpointGuid` | string | No | Filter by routed model runner endpoint GUID |
| `requestorUserGuid` | string | No | Filter by authenticated user GUID |
| `credentialGuid` | string | No | Filter by credential GUID |
| `loadBalancingPolicyGuid` | string | No | Filter by attached load-balancing policy GUID |
| `modelName` | string | No | Filter by requested or effective model |
| `mutationSummary` | string | No | Filter by mutation-summary substring |
| `denialReasonCode` | string | No | Filter by denial reason |
| `reservationGuid` | string | No | Filter by VMR reservation id |
| `reservationDecision` | string | No | Filter by reservation gate decision such as `Allowed` or `Denied` |
| `reservationReasonCode` | string | No | Filter by reservation reason such as `ReservationDenied` |
| `sessionAffinityOutcome` | string | No | Filter by session-affinity outcome |
| `statusClass` | string | No | Filter by status class such as `2xx`, `4xx`, or `5xx` |
| `sourceIp` | string | No | Filter by requestor source IP |
| `httpStatus` | integer | No | Filter by exact HTTP status code |

**Response:**
```json
{
  "Data": [
    {
      "TimestampUtc": "2026-03-20T10:00:00Z",
      "SuccessCount": 42,
      "FailureCount": 3,
      "TotalCount": 45
    }
  ],
  "StartUtc": "2026-03-20T10:00:00Z",
  "EndUtc": "2026-03-20T11:00:00Z",
  "Interval": "hour",
  "TotalSuccess": 42,
  "TotalFailure": 3,
  "StatusClassCounts": {
    "2xx": 42,
    "5xx": 3
  },
  "DenialReasonCounts": {
    "AllEndpointsAtCapacity": 2,
    "PolicyRejected": 1
  },
  "SessionAffinityOutcomeCounts": {
    "Hit": 20,
    "Miss": 25
  },
  "TotalRequests": 45
}
```

Success is defined as HTTP status 100-399; failure is HTTP status 400-599 or null (incomplete requests).

### Analytics Workspace API

The dashboard includes an **Analytics** workspace on the **Analytics** tab of the **Observability** page (`/observability?tab=analytics`; the old `/analytics` URL redirects there). This workspace builds on request-history data and answers first-release operator questions without requiring raw request-body access. See [ADR 0002](./docs/adr/0002-analytics-workspace.md) for API, retention, authorization, saved-report, and export decisions.

- What was the average, P50, P95, and P99 time-to-first-token for a model endpoint, VMR, model, tenant, user, credential, or provider?
- How many prompt, completion, and total tokens were used over time for a model, endpoint, VMR, provider, tenant, or user?
- What is the estimate-only cost for a user, model, VMR, endpoint, or tenant when the operator supplies a per-token unit cost?
- How many requests succeeded, failed, were denied, or were rate-limited over the selected window?
- Which users, tenants, models, VMRs, and endpoints are driving usage and cost estimates?
- Which saved report definitions should operators reuse for daily user cost, model usage, TTFT, or reliability review?

```text
GET  /v1.0/analytics/catalog
GET  /v1.0/analytics/summary?range=lastDay&bucketSeconds=3600&tokenUnitCost=0.00001
GET  /v1.0/analytics/ttft?range=lastDay&groupBy=RequestorUserId
GET  /v1.0/analytics/tokens?range=lastWeek&modelName={model}
GET  /v1.0/analytics/costs?range=lastDay&requestorUserGuid={user}&tokenUnitCost=0.00001
GET  /v1.0/analytics/reports
POST /v1.0/analytics/reports
POST /v1.0/analytics/query
```

Analytics data is retained for 30 days in the first release. Named ranges are `lastHour`, `lastDay`, `lastWeek`, `lastMonth`, or `custom` with `startUtc` and `endUtc`; custom ranges are clamped to the retained window. Operators can choose bucket granularity with `bucketSeconds`.

Analytics is populated from persisted request-history rows. Request history must be enabled globally in `conductor.json` and enabled on the Virtual Model Runner before the requests are sent; enabling it later only affects new requests. The Docker dashboard create form defaults new VMRs to Request History and Analytics enabled, and existing VMRs can be edited from the **Virtual Runners** page.

Cost output is an estimate only. Conductor multiplies successful reported token usage by the caller-supplied `tokenUnitCost`; it does not model provider billing rules, cached-token discounts, multimodal pricing, currency conversion, taxes, credits, or account-specific contracts. Missing provider token usage is reported as unknown and is not treated as zero.

System administrators can run global analytics and optionally filter to a tenant with `tenantId`. Tenant administrators are forced into their authenticated tenant scope. A tenant user can be granted read-only Analytics access without tenant-admin rights by adding the `analytics.read` label, or by setting a user tag such as `analytics.read=true` or `permissions=analytics.read`.

Saved reports persist the Analytics query, grouping, token-unit-cost, reservation filters, and dashboard display state. They do not schedule execution or snapshot results. The dashboard can load, update, delete, and copy a link to a saved report.

## Configuration Pinning

Model configurations can define pinned properties that are automatically merged into incoming requests:

```json
{
  "Name": "Low Temperature Config",
  "PinnedCompletionsProperties": {
    "temperature": 0.3,
    "top_p": 0.9,
    "max_tokens": 2048
  },
  "PinnedEmbeddingsProperties": {
    "model": "text-embedding-ada-002"
  }
}
```

When a request comes through a virtual model runner, the pinned properties are merged with the request body, allowing you to enforce specific model parameters.

## Production Hardening

Conductor's defaults are tuned for a quick local start, not for an exposed deployment. Work through this checklist before the server, dashboard, or MCP server is reachable by anyone other than you.

### Change the default credentials

Every installation starts with the same well-known credentials (see [Quick Start](#quick-start)). Change all of them:

- **Administrator password** (`admin@conductor` / `password`): sign in to the dashboard as the administrator and edit the account under **Access** > **Administrators**, or call `PUT /v1.0/administrators/{id}` with a new `Password`. Better still, create a new administrator with your own email address and then delete the default one (an administrator cannot delete the account it is signed in with). See [REST_API.md](./REST_API.md#administrators).
- **Tenant user password** (`admin@conductor` / `password` in tenant `default`, a global admin): edit the user under **Access** > **Users**, or call `PUT /v1.0/users/{id}` with the full user object and a new `Password` (the update replaces the whole record, so read it first; reads never include the password, and an update that omits `Password` keeps the current one). See [REST_API.md](./REST_API.md#users).
- **Admin API key** (`conductoradmin`): replace the value in the `AdminApiKeys` array of `conductor.json` (`docker/conductor.json` for Compose) with a long random value and restart the server. `conductoradmin` is also the built-in default when `AdminApiKeys` is omitted, so set the array explicitly. Any value in this list grants full administrator access through the `x-admin-apikey` header or a bearer token. See [REST_API.md](./REST_API.md#4-administrator-api-keys).
- **Tenant bearer tokens**: in the Compose setup the default credential's token is the fixed `factory_default_bearer_token_0000000000000000000000000000000000`. Delete that credential under **Access** > **Credentials** (or through `/v1.0/credentials`) and issue new ones.
- **Database password**: the Compose PostgreSQL account is `conductor` / `conductor`. Change `POSTGRES_PASSWORD` in `docker/compose.yaml` together with `Database.Password` in `docker/conductor.json`, and stop publishing port `5432` on the host unless you need it.

Note that `docker/factory/reset.sh` and `reset.bat` restore the factory defaults, including all of the credentials above.

### Use TLS behind a reverse proxy

Conductor serves plain HTTP by default (`Webserver.Ssl` is `false`). Terminate TLS in a reverse proxy or ingress controller (nginx, Caddy, Traefik, a cloud load balancer) in front of the server and the dashboard, and do not publish port `9000` or `9100` directly. Credentials travel in headers (`Authorization`, `x-password`, `x-admin-apikey`), so they must never cross an untrusted network unencrypted. Bind `Webserver.Hostname` to a specific interface rather than `*` when the proxy runs on the same host.

### Restrict CORS

The Compose configuration allows every origin (`"AllowedOrigins": ["*"]`). In production, list only the origin that serves your dashboard (for example `https://conductor.example.com`), or disable CORS if no browser client calls the API from another origin. See [CORS Configuration](#cors-configuration).

### Record the real client IP

Behind a reverse proxy, request history, logs, and source-IP session affinity see the proxy's address unless the proxy is listed in `Webserver.TrustedProxies`. List only the proxy's own addresses, since any client connecting from a trusted address can forge the forwarded-for header. See [Client IP Behind a Reverse Proxy](#client-ip-behind-a-reverse-proxy).

### Choose a production database and back it up

Use PostgreSQL, SQL Server, or MySQL for production rather than SQLite, enable `Database.RequireEncryption` when the database is on another host, and use a dedicated database account with a strong password. See [Supported Databases](#supported-databases). Back up the database with your provider's native tooling, and export configuration regularly with **System** > **Backup & Restore** in the dashboard or `GET /v1.0/backup` (see [REST_API.md](./REST_API.md#backup-and-restore)). Backups contain credentials, so store them as secrets.

### Set request-history retention

Request history captures request and response bodies by default, which can include prompts, completions, and other sensitive data. Set `MetadataRetentionDays` and `BodyRetentionDays` to the shortest periods your auditing needs allow, set `CaptureRequestBody` / `CaptureResponseBody` to `false` (or disable `RequestHistoryEnabled` on individual VMRs) where bodies must not be stored, and extend `RedactedHeaders` / `RedactedJsonFields` for any custom secrets. Protect the `RequestHistory.Directory` on disk. See [Request History Configuration](#request-history-configuration).

### Limit network exposure of the MCP server

The optional MCP server (`Conductor.McpServer`) exposes tenant data and management tools and listens on HTTP `localhost:9001` and TCP `127.0.0.1:9002` by default. Connect to the HTTP transport with the host name in `HttpHostname` (`localhost` by default), not `127.0.0.1`: it is served by .NET `HttpListener`, which can bind `localhost` to IPv6 `::1` only and matches the request's host name against that binding. Keep it bound to the loopback interface, and if remote agents need it, put it behind an authenticating reverse proxy with TLS rather than binding it to a public interface. Disable the transport you do not use (`EnableHttpServer`, `EnableTcpServer`). See [MCP_API.md](./MCP_API.md#transports).

### Keep logs and telemetry

Keep file logging enabled (`Logging.FileLogging`) or ship logs to syslog (`Logging.Servers`) so that authentication failures and administrative changes can be investigated, and keep `OpenTelemetry` export pointed at a collector you monitor. The Compose Grafana instance allows anonymous admin access (`GF_AUTH_ANONYMOUS_ORG_ROLE=Admin`), and Prometheus, Tempo, Loki, and the collector are published on host ports; disable anonymous access and stop publishing those ports before exposing the host. See [Observability](#observability) and [TELEMETRY.md](./TELEMETRY.md).

## Health Checking & Rate Limiting

### Endpoint Health Configuration

Model Runner Endpoints support comprehensive health checking with the following properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HealthCheckEnabled` | bool | `true` (`false` for `TypeSafe`) | Send health check probes. When `false`, no probe is sent and the endpoint is treated as healthy |
| `HealthCheckUrl` | string | `/` | URL path appended to endpoint base URL for health checks |
| `HealthCheckMethod` | enum | `GET` | HTTP method: `GET`, `HEAD`, or `POST` |
| `HealthCheckRequestBody` | string | `null` | Body sent with `Content-Type: application/json` when `HealthCheckMethod` is `POST`. Required for `POST` (`HealthCheckBodyRequired`) |
| `HealthCheckIntervalMs` | int | `5000` | Milliseconds between health checks |
| `HealthCheckTimeoutMs` | int | `5000` | Timeout for health check requests |
| `HealthCheckExpectedStatusCode` | int | `200` | Expected HTTP status code for healthy |
| `UnhealthyThreshold` | int | `2` | Consecutive failures before marking unhealthy |
| `HealthyThreshold` | int | `2` | Consecutive successes before marking healthy. The first success of an endpoint that has not been healthy since the server started marks it healthy at once |
| `HealthCheckUseAuth` | bool | `false` | Include the endpoint's API key in health check requests, using the type's header (`Authorization: Bearer`, `x-goog-api-key`, or `x-api-key`) |
| `MaxParallelRequests` | int | `4` | Maximum concurrent requests (0 = unlimited) |
| `Weight` | int | `1` | Relative weight for load balancing (1-1000) |
| `ServiceState` | enum | `Normal` | Operator-controlled traffic state: `Normal`, `Draining`, or `Quarantined` |

Selecting a type applies its health check defaults (see [Everything is explicit](#everything-is-explicit)). Notes per type:

- **Ollama**: the default `GET /` works against a local Ollama server. `GET /api/tags` also checks that the API is serving.
- **Ollama Cloud**: `GET /api/tags` with auth every 60 seconds by default.
- **OpenAI, OpenAI-compatible, and vLLM**: when using `api.openai.com` or another backend that requires authentication for model listing, set `HealthCheckUseAuth` to `true` and `HealthCheckUrl` to `/v1/models`. `OpenAICompatible` defaults to unauthenticated `GET /v1/models` every 5 seconds.
- **Gemini**: when using `generativelanguage.googleapis.com`, set `HealthCheckUseAuth` to `true` and `HealthCheckUrl` to `/v1beta/models`. Gemini uses the `x-goog-api-key` header rather than bearer token authentication.
- **Anthropic**: health checks are on by default, using `GET /v1/models?limit=1` with auth every 60 seconds. Conductor sends `x-api-key` and `anthropic-version`. Turning off `HealthCheckUseAuth` raises the `AnthropicHealthCheckUnauthenticated` warning, because Anthropic rejects unauthenticated requests and the endpoint would be reported unhealthy.
- **TypeSafe (hosted)**: hosted TypeSafe has no `/v1/models` route, so the health check is a one-question `POST /v1/systemone` decision probe that matches PolyPrompt's `TypeSafeClient`. Each probe is billed, so these checks are optional and **off by default**, and the endpoint is treated as healthy while they are off. When enabled, the default interval is 60 seconds, which the operator can lower at a higher cost.
- **Ollaya**: Ollaya serves `GET /v1/models`, so its default health check is that route without auth every 5 seconds. This differs from hosted TypeSafe even though both speak the same decision API.

### Health Check Behavior

- Endpoints start in an **unhealthy** state. Conductor probes an endpoint immediately when it is created, when the server starts, and when its probe target (health check URL, method, auth, or body) changes, instead of waiting a full interval
- The first successful probe of an endpoint that has not been healthy since the server started marks it healthy. After that, probes run every `HealthCheckIntervalMs`, and `HealthyThreshold` and `UnhealthyThreshold` apply as usual
- Endpoints with `HealthCheckEnabled` set to `false` receive no probes and are treated as healthy
- Background tasks continuously monitor each active endpoint at the configured interval
- Endpoints with the same effective health-check request share one upstream HTTP probe, then apply their own expected status codes and thresholds to that shared result
- The proxy automatically excludes unhealthy endpoints from request routing
- Draining endpoints continue to be probed and remain available for already-pinned session-affinity traffic, but they do not receive new assignments
- Quarantined endpoints continue to be probed for diagnostics, but they are excluded from all routing, including pinned-session reuse
- When all endpoints are unhealthy, requests return `502 Bad Gateway`
- When the upstream does not return response headers within the effective `TimeoutMs`, requests return `504 Gateway Timeout` (see [Request timeouts](#request-timeouts))
- When all endpoints are at capacity, requests queue in the VMR's QoS profile and are released as capacity frees; they return `429 Too Many Requests` only when the queue is full or no capacity frees before the profile's `MaxQueueWaitMs` deadline (see [QoS & Queueing](#qos--queueing))
- `GET` or `HEAD` on a VMR's base URL (`/v1.0/api/{vmr}/`) is an unauthenticated health probe that returns `204` when the VMR has a healthy endpoint and `503` when it has none, without queueing, using an endpoint slot, or reaching a model runner
- When all configured endpoints are quarantined or draining, requests are denied with an explicit service-state-specific error

### Rate Limiting

- Each endpoint tracks in-flight requests in real-time
- The `MaxParallelRequests` property enforces a per-endpoint concurrency limit
- Set to `0` for unlimited concurrent requests
- Requests are counted from start until the response completes (including streaming)

### Weighted Load Balancing

- The `Weight` property influences endpoint selection in round-robin and random modes
- Higher weight = more traffic directed to that endpoint
- Example: Endpoint A (weight=3) receives 3x more traffic than Endpoint B (weight=1)

### Health Status API

Monitor endpoint health via the REST API:

```bash
# Health of all endpoints in tenant
GET /v1.0/modelrunnerendpoints/health

# Put an endpoint into maintenance drain mode
POST /v1.0/modelrunnerendpoints/{id}/drain

# Resume normal traffic
POST /v1.0/modelrunnerendpoints/{id}/resume

# Exclude an endpoint from all routing while keeping health visibility
POST /v1.0/modelrunnerendpoints/{id}/quarantine

# Health of endpoints for a specific VMR
GET /v1.0/virtualmodelrunners/{id}/health
```

Response includes:
- Current health state (healthy/unhealthy)
- Operator-managed service state (`Normal`, `Draining`, `Quarantined`)
- In-flight request count
- Total uptime/downtime
- Uptime percentage
- Last check timestamp
- Last error message (if any)

## Docker

The included Docker Compose setup uses named Docker Hub images:

- **Server**: `jchristn77/conductor-server:v1.1.0`
- **Dashboard**: `jchristn77/conductor-dashboard:v1.1.0`

### Building Docker Images

Windows:

```bash
# Build and push server with the specified tag and latest
build-server.bat v1.1.0

# Build and push dashboard with the specified tag and latest
build-dashboard.bat v1.1.0

# Build and push both images with the specified tag and latest
build-all.bat v1.1.0
```

macOS and Linux:

```bash
./build-server.sh v1.1.0
./build-dashboard.sh v1.1.0
./build-all.sh v1.1.0
```

The scripts build with the `cloud-jchristn77-jchristn77` Docker Build Cloud builder. The `.sh` scripts build `linux/amd64` images by default, so building on Apple silicon still produces images that run on x86-64 hosts; set `DOCKER_PLATFORM` (for example `linux/arm64`) or `DOCKER_BUILDER` to override.

### Updating a Deployment

To refresh a running deployment with the latest published images, run `update.bat` from the
`docker` directory. It stops the stack, pulls the newest images (`docker compose pull`), and
brings everything back up:

```bash
update.bat
```

## Observability

Conductor is instrumented with [OpenTelemetry](https://opentelemetry.io/). Metrics and distributed traces are emitted through the .NET base class library primitives (`Meter`, `ActivitySource`) across the critical subsystems:

| Subsystem | Metrics (Prometheus names) | Traces |
| --- | --- | --- |
| HTTP server | `conductor_http_server_request_duration_seconds`, `conductor_http_server_active_requests` | None |
| Inference proxy | `conductor_inference_requests_total`, `conductor_inference_request_duration_seconds`, `conductor_inference_first_token_duration_seconds`, `conductor_inference_upstream_errors_total`, `conductor_decision_question_count` | `inference.proxy`, `inference.forward` |
| Routing / load balancing | `conductor_routing_decisions_total`, `conductor_routing_decision_duration_seconds`, `conductor_routing_denials_total` | `routing.evaluate` |
| Model load | `conductor_model_load_requests_total`, `conductor_model_load_request_duration_seconds`, `conductor_model_load_endpoint_attempts_total` | None |
| Database | `conductor_db_client_operations_total`, `conductor_db_client_operation_duration_seconds`, `conductor_db_client_errors_total` | `db <operation>` |
| Health / endpoints | `conductor_health_endpoints_healthy`, `conductor_health_endpoints_unhealthy`, `conductor_health_inflight_requests` | None |
| Process / runtime | `conductor_process_memory_usage_bytes`, `conductor_process_uptime_seconds`, `conductor_process_thread_count`, plus .NET runtime instrumentation | None |

The existing in-app `GET /v1.0/observability/metrics` endpoint is unchanged and remains available for the built-in operational metrics snapshot.

### Enabling telemetry

Telemetry is off by default. Enable it via the `OpenTelemetry` block in `conductor.json`:

```json
"OpenTelemetry": {
  "Enabled": true,
  "OtlpEnabled": true,
  "OtlpEndpoint": "http://otel-collector:4317",
  "Protocol": "Grpc",
  "MetricExportIntervalMs": 15000,
  "TracesSamplingRatio": 1.0,
  "IncludeRuntimeInstrumentation": true,
  "PrometheusEnabled": false
}
```

The OTLP endpoint and protocol can also be supplied through the standard `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL` environment variables, which take precedence. Set `PrometheusEnabled` to `true` to additionally serve an in-process Prometheus scrape endpoint on `:9464/metrics`.

### The observability stack

`docker/compose.yaml` provisions a full stack that collects and visualizes the signals above:

| Service | Purpose | URL |
| --- | --- | --- |
| OpenTelemetry Collector | Receives OTLP from Conductor; fans out to Prometheus/Tempo/Loki; tails server logs | `:4317` (gRPC), `:4318` (HTTP) |
| Prometheus | Metrics storage (scrapes the collector) | `http://127.0.0.1:9090` |
| Tempo | Trace storage | `http://127.0.0.1:3200` |
| Loki | Log aggregation | `http://127.0.0.1:3100` |
| Grafana | Dashboards and exploration (metrics ↔ traces ↔ logs correlation) | `http://127.0.0.1:3000` |

Grafana datasources and dashboards are provisioned automatically. Every dashboard (**Database**, **HTTP & API**, **Inference & Proxy**, **Routing & Load Balancing**, **QoS & Queueing**, **Runtime & Process**, and **Health & Endpoints**) lives directly under a single top-level **Conductor** folder, provisioned by a single file provider (`docker/grafana/provisioning/dashboards/dashboards.yaml`, `folder: Conductor`) from `docker/grafana/dashboards/Conductor/*.json`.

> **Note:** The observability stack requires a Conductor server image built from this source (telemetry export was added in this release). Rebuild the server image with `build-server.bat` if you are running an older published tag.

## License

MIT License - see [LICENSE.md](LICENSE.md) for details.

## Attributions

<a href="https://www.flaticon.com/free-icons/music" title="music icons">Music icons created by Freepik - Flaticon</a>

### Visual QoS editing and chart workspace (v0.6.0)

Traffic (QoS) → Profiles → Create/Edit → Diagram shows directional links from
**Start of queuing** through the configured queues to **End of queuing / Dispatch**.
Click a queue or Add node to configure scheduling, overflow, limits, flow keys,
class weights, priority bands and LLQ policers in a dialog. Apply queue updates the
profile draft; save the profile to persist. Connect handles to add links; cyclic,
self and duplicate connections are rejected. Rename updates links and ingress routes.
Server validation remains the final check for discipline-specific topology rules.

The dashboard chart tabs show request history, volume / average TTFT, response
latency / average TTFT, and token usage. Observability uses the same distinct chart
renderers. Latency charts use milliseconds for both series; volume uses a separate
TTFT axis. Missing timing samples remain gaps. Token categories are input excluding
cache, output, cached input, and other reported tokens. Cached usage is a subset of
input; overlapping multimodal totals are not added again. Cached/multimodal provider
fields currently remain unknown where not persisted, rather than inferred zeros.

Each chart has an image-copy control. On supported HTTPS/localhost browsers it
copies a PNG. Browser security prevents automatic image clipboard writes on remote
plain HTTP or when permission is denied; the control opens a PNG preview with
instructions to right-click → Copy Image, plus a download option. This manual image
copy path works without a secure origin. See the browser
[Clipboard API restrictions](https://developer.mozilla.org/en-US/docs/Web/API/Clipboard/write).
Controls and column headers receive contextual hover help, including dynamically
opened dialogs and menus.

Cluster assets and instructions: [Kubernetes](kubernetes/README.md) and
[Minikube / k3s](minikube/README.md). Run dashboard checks with `cd dashboard && npm test`
and `npm run build`; run the shared .NET suite with
`dotnet run --project src/Test.Automated -- --results /tmp/conductor-tests.json`.

Useful additional charts from existing metadata: failure/status rate by provider
and model; endpoint routing share alongside health and capacity; QoS wait/depth and
admission/rejection rates by class; reservation denials by identity; token throughput
by model; estimated token cost by tenant/user; and captured processing-stage duration
by stage. Cache-hit and multimodal trends need persisted provider usage fields first.

The v0.6.0 QoS editor uses responsive class and ingress-route cards with consistently
styled actions. Dropdowns display readable names (for example, “Priority class” and
“JSON body path”) while API enum values remain unchanged. Queue settings, placement
and telemetry options are grouped separately, with clearer helper text and validation.
