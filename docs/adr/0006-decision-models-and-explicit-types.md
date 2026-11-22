# ADR 0006: Decision Models and Explicit Endpoint Types

Date: 2026-11-21

Status: Accepted

## Context

Two needs arrived together.

First, operators want to route decision models: TypeSafe's hosted Jev models and the local Ollaya runtime. Both speak the TypeSafe System One API (`POST /v1/systemone`), which answers structured questions (binary, choice, score) instead of generating text. The hosted service and the local runtime behave differently: hosted TypeSafe requires a key and has no `/v1/models` route, while Ollaya serves `/v1/models` and runs without a key.

Second, the existing four endpoint types hid real differences. `Ollama` covered both local Ollama and Ollama's hosted service, and `OpenAI` covered both hosted OpenAI and any OpenAI-compatible server. VMRs required an exact endpoint type match, and requests were classified by path, so an OpenAI SDK pointed at an Ollama VMR's `/v1/` routes reached Ollama's own OpenAI-compatible API by accident. The dashboard also guessed defaults from hostnames.

## Decision

**Everything is explicit.** The operator chooses each endpoint's `ApiType` and each VMR's `ApiType`. Conductor never infers a type, provider, or default from a hostname, URL path, response field, or status code. Type defaults are applied only from the selected type: by the dashboard when a type is chosen, and by the server on create for omitted fields. The dashboard's hostname heuristics are removed.

**Separate types wherever behavior differs.** `Ollama` and `OllamaCloud`, `OpenAI` and `OpenAICompatible`, and `TypeSafe` and `Ollaya` are distinct endpoint types. `Ollaya`, `OllamaCloud`, and `OpenAICompatible` are endpoint-only: they cannot be a VMR type.

**A VMR's type is the API its clients call.** An explicit compatibility table says which endpoint types each VMR type accepts, replacing exact matching:

| VMR `ApiType` | Accepted endpoint `ApiType` |
| --- | --- |
| `Ollama` | `Ollama`, `OllamaCloud` |
| `OpenAI` | `OpenAI`, `OpenAICompatible`, `vLLM` |
| `vLLM` | `vLLM` |
| `Gemini` | `Gemini` |
| `Anthropic` | `Anthropic` |
| `TypeSafe` | `TypeSafe`, `Ollaya` |

**Classification by VMR type.** Every VMR classifies requests by its configured type, within that type's path namespace (`/api/`, `/v1/`, or `/v1beta/`). Unrecognized paths inside the namespace are forwarded as `Unknown`, as before. A path outside the namespace returns `404` and is never forwarded. This is a fix: the accidental Ollama `/v1/` behavior is replaced by an `OpenAI` VMR with an `OpenAICompatible` endpoint.

**Decisions are a provider-neutral capability.** Model definitions declare `SupportsDecisions`, VMRs declare `AllowDecisions`, and model access policies have a `Decisions` action that `Completions` does not imply. A `TypeSafe` VMR must allow decisions. Model configurations are never applied to decision requests, and the proxy rejects decision bodies with no questions or more than `TypeSafe.MaxQuestionsPerRequest` questions before forwarding.

**Health checks follow PolyPrompt.** Hosted TypeSafe has no `/v1/models`, so its health check is a one-question `POST /v1/systemone` probe that matches the 404 branch of PolyPrompt's `TypeSafeClient.ValidateConnectivityAsync`. Each probe is billed, so hosted TypeSafe health checks are optional and off by default, and the endpoint is treated as healthy while they are off; when enabled, the default interval is 60 seconds and the operator may lower it. Ollaya uses `GET /v1/models` every 5 seconds. To support this, every endpoint gains `HealthCheckEnabled`, the `POST` method, and `HealthCheckRequestBody`. PolyPrompt itself is not added as a dependency: Conductor proxies bytes, while PolyPrompt parses responses into its own models.

## Public Contracts

`ApiTypeEnum` appends `TypeSafe = 5`, `Ollaya = 6`, `OllamaCloud = 7`, and `OpenAICompatible = 8`. Values are never renumbered because the column is an integer.

| Value | Name | Client-facing | Wire API | Upstream auth |
| --- | --- | --- | --- | --- |
| 5 | `TypeSafe` | yes | System One `/v1/*` | Bearer (required) |
| 6 | `Ollaya` | no | System One `/v1/*` | Bearer if a key is set |
| 7 | `OllamaCloud` | no | Ollama `/api/*` | Bearer (required) |
| 8 | `OpenAICompatible` | no | OpenAI `/v1/*` | Bearer if a key is set |

Other enum changes: `RequestTypeEnum` adds `TypeSafeDecide` and `TypeSafeListModels`; `ModelAccessActionEnum` adds `Decisions = 7`; `ModelLoadProbeKindEnum` adds `Decision = 6`; `HealthCheckMethodEnum` adds `POST = 2`.

New fields: `ModelDefinition.SupportsDecisions` (`false`), `VirtualModelRunner.AllowDecisions` (`true`), `ModelRunnerEndpoint.HealthCheckEnabled` (`true`, type default `false` for `TypeSafe`), `ModelRunnerEndpoint.HealthCheckRequestBody` (`null`), and request history `DecisionQuestionCount`.

`ServerSettings.TypeSafe` (`TypeSafeSettings`): `DefaultModel` `jev-latest`; `MaxQuestionsPerRequest` 64 (minimum 1, maximum 1024).

Validation: `VmrApiTypeNotClientFacing`, `EndpointApiTypeMismatch` (now table based), `TypeSafeApiKeyMissing`, `OllamaCloudApiKeyMissing`, `OllamaCloudRigMonitorUnsupported`, `DecisionsDisabledOnDecisionRoute`, and `HealthCheckBodyRequired` are errors; `TypeSafeConfigurationIgnored` and `DecisionModelNotEnabled` are warnings. The VMR rules apply through REST, MCP, and backup restore.

Model load: `TypeSafe` `Auto` sends the one-question `Decision` probe and reports `VerifiedRemote` (billable); `Ollaya` `Auto` calls `GET /v1/models` and reports `Verified`; `OllamaCloud` reports `VerifiedRemote` with billable probes; `OpenAICompatible` reports `Verified` and its probes are not flagged as billable. Model management, keep-warm, and RigMonitor reject `OllamaCloud`.

Strict-mode gating: a decision request for a model definition without `SupportsDecisions`, or a completion or embedding request for a decision-only model definition, returns `404`. A decision request on a VMR with `AllowDecisions = false` returns `403`.

## Consequences

- Existing `OpenAI` endpoints keep their type. Operators whose `OpenAI` endpoints point at other servers should retype them to `OpenAICompatible`; Conductor does not change them automatically. The same applies to `Ollama` endpoints that point at Ollama's hosted service.
- Clients that relied on reaching paths outside a VMR's namespace now get `404` and must use a VMR of the matching type.
- Each type's defaults, auth headers, health checks, model load behavior, and usage parsing are chosen from one explicit value, which keeps every surface (server, dashboard, SDKs, MCP, Postman) consistent.
- Hosted TypeSafe endpoints are treated as healthy unless the operator opts into billed health checks, so an unreachable hosted endpoint is detected by request failures rather than by probes in the default configuration.
- Inference First QoS profiles seeded before v1.1.0 do not gain the new metadata request types automatically.
