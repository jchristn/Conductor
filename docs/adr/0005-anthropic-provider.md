# ADR 0005: Anthropic Provider

Date: 2026-11-21

Status: Accepted

## Context

Operators want to run Claude Code and the official Anthropic SDKs through Conductor so that Anthropic traffic gets the same tenant isolation, credentials, model access policies, QoS, request history, and telemetry as the other providers. The Anthropic Messages API differs from the OpenAI API in ways that matter to a proxy:

- Clients send their key in `x-api-key`, not `Authorization`, and every request needs an `anthropic-version` header.
- Clients rely on response headers such as `request-id`, `retry-after`, and `anthropic-ratelimit-*`.
- The SDKs parse errors in the shape `{"type":"error","error":{"type","message"}}` and retry based on the error type.
- Usage includes prompt cache token counts, and streaming usage is split across `message_start` and `message_delta` events.
- Message Batches and Files are stateful APIs whose ids refer to resources held by one upstream account.

Conductor is a byte-level proxy. It does not translate between wire formats, and it never infers a provider from a hostname or path.

## Decision

Add `Anthropic` as a first-class endpoint type and client-facing VMR type (`ApiTypeEnum` value 4). An `Anthropic` VMR accepts only `Anthropic` endpoints and classifies requests in its `/v1/` namespace.

Inbound authentication uses normal Conductor credentials. Because Anthropic clients send their key in `x-api-key`, an `Anthropic` VMR also accepts a Conductor credential in that header, looked up exactly like a bearer token. Route-level authentication looks up the VMR for `/v1.0/api/{vmr}/` paths so the decision is made by the VMR's configured type. Other VMR types never accept `x-api-key`, and inbound `authorization`, `x-api-key`, and `anthropic-api-key` headers are never forwarded upstream for any family.

**Documented deviation from `AUTHENTICATION.md`.** That requirements document reserves `x-api-key` for administration. Conductor's administrator header is `x-admin-apikey`, so the two do not collide. On Anthropic VMRs only, `x-api-key` carries a normal, tenant-scoped Conductor credential that grants no administrative rights, is validated through the existing credential lookup, and is never forwarded. We accept the deviation because Anthropic clients cannot be configured to use another header.

Outbound requests use the endpoint's key in `x-api-key`. The effective `anthropic-version` is the client's header, then the endpoint's `AnthropicVersion`, then `Settings.Anthropic.DefaultVersion` (`2023-06-01`). Client headers named in `ForwardedRequestHeaders` are copied, and upstream response headers named in `ForwardedResponseHeaders` or matching `ForwardedResponseHeaderPrefixes` are copied back.

Errors that Conductor itself generates on Anthropic VMRs use the Anthropic shape, keeping Conductor's reason code in `X-Conductor-Error-Code`. Upstream errors pass through unchanged.

Message Batches and Files are not supported. They return `404` `not_found_error`, because a stateful resource created on one endpoint cannot be load balanced across endpoints.

Model configurations are applied conservatively: `MaxTokens` maps to `max_tokens`, sampling parameters are injected only for API versions that still accept them, and Ollama-specific settings are never sent.

## Public Contracts

`ApiTypeEnum` adds `Anthropic = 4`. `RequestTypeEnum` adds `AnthropicMessages`, `AnthropicCountTokens`, `AnthropicListModels`, and `AnthropicGetModel`.

`ModelRunnerEndpoint` adds `AnthropicVersion` (string, `null` by default, format `yyyy-MM-dd`).

`ServerSettings.Anthropic` (`AnthropicSettings`):

- `DefaultVersion`: `2023-06-01`
- `ForwardedRequestHeaders`: `anthropic-version`, `anthropic-beta`
- `ForwardedResponseHeaders`: `request-id`, `retry-after`, `x-should-retry`
- `ForwardedResponseHeaderPrefixes`: `anthropic-ratelimit-`
- `SamplingParametersRemovedFromVersion`: `2026-10-21`

`RequestHistory.RedactedHeaders` gains `x-api-key` and `anthropic-api-key`, and CORS `AllowedHeaders` gains `anthropic-version`, `anthropic-beta`, and `x-api-key`.

Error mapping for Conductor-originated errors:

| Condition | `error.type` |
| --- | --- |
| `401` | `authentication_error` |
| `403` | `permission_error` |
| `404` | `not_found_error` |
| `429` | `rate_limit_error` |
| No healthy endpoint, or `503` | `overloaded_error` |
| Other `5xx` | `api_error` |
| Other `4xx` | `invalid_request_error` |

Validation adds `AnthropicApiKeyMissing` and `AnthropicVersionInvalid` (errors) and `AnthropicConfigurationMismatch`, `ModelConfigurationSamplingIgnored`, and `AnthropicHealthCheckUnauthenticated` (warnings).

Request history adds `CacheCreationInputTokens` and `CacheReadInputTokens`. `TotalTokens` for Anthropic is input plus output tokens. Telemetry adds the `Anthropic` `api_family` and the `conductor.anthropic.version` span attribute.

Type defaults: `api.anthropic.com`, port 443, SSL, health check `GET /v1/models?limit=1` with auth, enabled, every 60,000 ms. Model load `Auto` and `MetadataOnly` call `GET /v1/models/{id}` and report `VerifiedRemote`; `ChatCompletion` sends `max_tokens: 1` and is billable.

## Consequences

- Claude Code works with `ANTHROPIC_BASE_URL` set to the VMR base URL and `ANTHROPIC_AUTH_TOKEN` or `ANTHROPIC_API_KEY` set to a Conductor credential, and Anthropic's upstream key never leaves the server.
- Operators who keep an explicit `RedactedHeaders` list must add `x-api-key` and `anthropic-api-key` themselves.
- Batches and Files users must call Anthropic directly.
- Cross-format translation (OpenAI clients calling Anthropic upstreams), Claude on Bedrock, Vertex, and Foundry, and encryption at rest for upstream keys remain out of scope.
