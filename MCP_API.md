# Conductor MCP API

Conductor ships an optional [Model Context Protocol](https://modelcontextprotocol.io) server
(`Conductor.McpServer`) that exposes read and light management operations as MCP tools, so an
LLM agent can inspect and reason about a Conductor deployment. The server can run over HTTP
(JSON-RPC at `/mcp/rpc`, plus SSE events at `/mcp/events`) or over TCP; both expose the same tool
set. Tools are invoked with the standard MCP `tools/call` request, passing the tool `name` and an
`arguments` object matching the input schema below. Every tool returns a JSON result object (or an
`{ "error": "<message>" }` object on failure).

All tenant-scoped tools require a `tenant_id`. Identifiers use Conductor's prefixed forms
(`ten_`, `md_`, `mre_`, `vmr_`, `mc_`, `qos_`, `qtc_`).

## Model discovery

### `conductor_list_models`
List model definitions for a tenant.

| Parameter | Type | Required | Description |
| --- | --- | --- | --- |
| `tenant_id` | string | yes | Tenant to query. |
| `family` | string | no | Filter by model family (e.g. `llama`, `qwen`). |
| `active_only` | boolean | no | Only active models (default `true`). |

Returns `{ models: [...], count }`.

### `conductor_get_model`
Get a model definition by ID. Params: `tenant_id` (req), `model_id` (req). Returns the model object.

## Endpoints

### `conductor_list_endpoints`
List model runner endpoints. Params: `tenant_id` (req), `active_only` (default `true`). Returns `{ endpoints, count }`.

### `conductor_get_endpoint`
Get an endpoint by ID. Params: `tenant_id` (req), `endpoint_id` (req).

### `conductor_get_endpoint_health`
Get endpoint health (state, in-flight, uptime). Params: `tenant_id` (req), `endpoint_id` (optional — all endpoints if omitted).

## Virtual model runners

### `conductor_list_vmrs`
List VMRs. Params: `tenant_id` (req), `active_only` (default `true`).

### `conductor_get_vmr`
Get a VMR by ID. Params: `tenant_id` (req), `vmr_id` (req).

### `conductor_create_vmr`
Create a VMR. Params: `tenant_id` (req), `name` (req), `api_type` (`Ollama`/`OpenAI`/`vLLM`/`Gemini`), `endpoint_ids` (array), `configuration_ids` (array), `load_balancing`, `allow_completions`, `allow_embeddings`.

## Model configurations

### `conductor_list_configs`
List configurations. Params: `tenant_id` (req), `active_only` (default `true`).

### `conductor_get_config`
Get a configuration by ID. Params: `tenant_id` (req), `config_id` (req).

### `conductor_create_config`
Create a configuration. Params: `tenant_id` (req), `name` (req), `temperature`, `top_p`, `top_k`, `max_tokens`, `pinned_completions` (object), `pinned_embeddings` (object).

## Tenants

### `conductor_list_tenants`
List tenants. Params: `active_only` (default `true`).

### `conductor_get_tenant`
Get a tenant by ID. Params: `tenant_id` (req).

## QoS

### `conductor_list_qos_profiles`
List a tenant's QoS profiles (which classify and queue VMR traffic).

| Parameter | Type | Required | Description |
| --- | --- | --- | --- |
| `tenant_id` | string | yes | Tenant to query. |
| `active_only` | boolean | no | Only active profiles (default `false`). |

Returns `{ profiles: [{ id, name, isDefault, active, tailNode, ingressMode }], count }`.

### `conductor_get_qos_profile`
Get a QoS profile's full definition — classification rules, queue nodes and their classes, links, and limits.

| Parameter | Type | Required | Description |
| --- | --- | --- | --- |
| `tenant_id` | string | yes | Tenant. |
| `profile_id` | string | yes | Profile ID (`qos_xxx`). |

Returns the assembled profile: scalar fields, `ruleCount`, `nodes` (each with `classes`), and `links`.

Example:

```json
{ "name": "conductor_get_qos_profile", "arguments": { "tenant_id": "default", "profile_id": "qos_ab12" } }
```

### `conductor_list_qos_traffic_classes`
List the tenant's QoS traffic class catalog. Params: `tenant_id` (req). Returns `{ trafficClasses: [{ id, name, tier, isSystem, description }], count }`.

### `conductor_get_qos_traffic_class`
Get a traffic class by ID. Params: `tenant_id` (req), `class_id` (req, `qtc_xxx`).

### `conductor_create_qos_profile`
Create a QoS profile from a full JSON definition.

| Parameter | Type | Required | Description |
| --- | --- | --- | --- |
| `tenant_id` | string | yes | Tenant. |
| `profile_json` | string | yes | The full profile as a JSON object string — scalar fields plus `Rules`, `Nodes` (each with `Classes`), `Links`, `IngressRoutes`. Enum values are names (e.g. `Discipline: "Fifo"`, `Operator: "Equals"`). |

The server assigns the id, forces `IsDefault=false`, structurally validates (node names, tail/ingress/link references), and persists it. Returns `{ id, name, tenantId }`.

### `conductor_update_qos_profile`
Update a profile (replaces its child rows). Params: `tenant_id` (req), `profile_id` (req), `profile_json` (req).

### `conductor_delete_qos_profile`
Delete a profile. Params: `tenant_id` (req), `profile_id` (req). The **default profile cannot be deleted**; referencing VMRs are reassigned to the tenant default.

### `conductor_validate_qos_profile`
Structurally validate a profile JSON (node names, tail/ingress/link references) without saving. Params: `profile_json` (req). Returns `{ valid, errors }`. Note: this is a structural check; full discipline/topology compilation happens server-side when the profile is used.

### `conductor_create_qos_traffic_class`
Create a traffic class. Params: `tenant_id` (req), `name` (req), `description`, `tier` (`Realtime`/`Interactive`/`AgentInteractive`/`BatchTimebound`/`BatchBackground`/`Default`).

### `conductor_update_qos_traffic_class`
Update a traffic class. Params: `tenant_id` (req), `class_id` (req), `name`, `description`, `tier` (all optional).

### `conductor_delete_qos_traffic_class`
Delete a traffic class. Params: `tenant_id` (req), `class_id` (req).
