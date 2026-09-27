# Conductor MCP API

Conductor ships an optional [Model Context Protocol](https://modelcontextprotocol.io) server
(`Conductor.McpServer`, built on [Voltaic](https://github.com/jchristn/voltaic)) that exposes read and
light management operations as MCP tools, so an LLM agent can inspect and reason about a Conductor
deployment. Tools are invoked with the standard MCP `tools/call` request, passing the tool `name` and an
`arguments` object matching the input schema below.

## Transports

| Transport | Default address | Notes |
| --- | --- | --- |
| Streamable HTTP | `http://localhost:9001/mcp` (`HttpMcpPath`) | Use this for MCP clients such as Claude Code and the MCP Inspector. Serves both the `initialize` handshake revisions (up to `2025-11-25`, with an `MCP-Session-Id`) and the stateless `2026-07-28` revision (`server/discover` plus per-request `MCP-Protocol-Version`, `Mcp-Method`, and `Mcp-Name` headers). |
| Legacy HTTP JSON-RPC | `/mcp/rpc` (`HttpRpcPath`), SSE at `/mcp/events` (`HttpEventsPath`) | Voltaic compatibility endpoints. |
| TCP | `127.0.0.1:9002` | `Content-Length`-framed JSON-RPC. |

All transports expose the same tool set, and only the Conductor tools listed below. Voltaic's optional
diagnostic tools (`echo`, `getTime`) are disabled, so they are not listed and calling them returns "not found".

Tools are reachable only through `tools/call`; sending a tool name as a bare JSON-RPC method returns `-32601`
(method not found). The MCP `ping` method is answered on every transport with an empty result (`{}`, or
`{"resultType":"complete"}` under the stateless `2026-07-28` revision).

For example, to connect Claude Code: `claude mcp add --transport http conductor http://localhost:9001/mcp`.

## Results and errors

A successful call returns a text content block holding the JSON result object described for each tool.

A tool that runs but fails (for example an unknown ID, or a missing health service) returns a result with
`"isError": true` and a text content block holding `{ "error": true, "message": "<message>" }`.

A call that cannot run at all is rejected with a JSON-RPC error `-32602` (invalid params): an unknown tool
name, or arguments that fail the tool's input schema (for example a missing required parameter).

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

## QoS runtime

Read-only views of live QoS admission state, equivalent to the `/v1.0/qosruntime` REST routes. Runtime state is
held in memory by the Conductor server's QoS admission service and resets when the server restarts. The MCP
server reaches it through functions supplied by the host with `ConductorMcpServer.ConfigureQosRuntime(...)`
(the same decoupling used for `ConfigureHealthCheck`); until they are configured, these tools return
`isError` with "QoS runtime service not configured".

### `conductor_list_qos_runtime`
List the QoS runtime state of every VMR in a tenant, ordered by VMR name. Params: `tenant_id` (req).
Returns `{ runners: [...], count }`, each runner shaped as described for `conductor_get_qos_runtime`.

### `conductor_get_qos_runtime`
Get the QoS runtime state of one VMR. Params: `tenant_id` (req), `vmr_id` (req, `vmr_xxx`).

Returns `{ tenantId, vmrId, vmrName, qosProfileId, qosProfileName, schedulerState, schedulerFaultCount,
lastSchedulerError, lastSchedulerErrorUtc, capacity, inUse, waiting, maxQueueWaitMs, classes, endpoints }`, where
`schedulerState` is `Running`, `Recovering`, `PassThrough`, or `Idle`; `capacity` of `0` means unbounded;
`classes` holds `{ className, waiting, admitted, rejected, timedOut, aborted, endpointSlotTimeouts, averageWaitMs,
p95WaitMs, maxWaitMs, lastAdmittedUtc, lastRejectedUtc }`; and `endpoints` holds `{ endpointId, endpointName,
inFlight, maxParallelRequests, isHealthy, active }` when the host's snapshot function supplies them.

### `conductor_get_qos_runtime_history`
Get time-bucketed QoS admission history for one VMR.

| Parameter | Type | Required | Description |
| --- | --- | --- | --- |
| `tenant_id` | string | yes | Tenant. |
| `vmr_id` | string | yes | VMR ID (`vmr_xxx`). |
| `start_utc` | string | no | Window start (ISO-8601 UTC). Defaults to one hour before `end_utc`. |
| `end_utc` | string | no | Window end (ISO-8601 UTC). Defaults to now. |
| `interval` | string | no | Bucket interval: `minute` (default), `5minute`, `15minute`, or `hour`. |

The window may not exceed 24 hours, and `start_utc` must be before `end_utc`; otherwise the tool returns `isError`.
Returns `{ vmrId, startUtc, endUtc, interval, classes, buckets: [{ timestampUtc, className, admitted, rejected,
timedOut, aborted, endpointSlotTimeouts, averageWaitMs, maxWaitMs, peakWaiting }], count }`. Buckets without
activity are omitted.

Example:

```json
{ "name": "conductor_get_qos_runtime_history", "arguments": { "tenant_id": "default", "vmr_id": "vmr_ab12", "interval": "5minute" } }
```
