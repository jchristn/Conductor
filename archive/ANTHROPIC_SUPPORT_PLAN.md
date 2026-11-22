# Anthropic, Decision Model and Endpoint Type Plan (v1.1.0)

## Purpose

This plan covers Conductor v1.1.0, which adds new upstream provider types and makes API type handling fully explicit. It applies to every product surface: server, core library, the four databases, REST API, MCP server, dashboard, SDKs, Postman, documentation, website, deployment assets, telemetry, CI and tests.

- **Part A: Anthropic.** A first-class `Anthropic` endpoint and VMR type for the Anthropic Messages API.
- **Part B: Jev-style decision models.** Two explicit endpoint types that share the TypeSafe System One API: `TypeSafe` (hosted Jev models) and `Ollaya` (the local runtime). They come with a provider-neutral "decision" capability.
- **Part C: Explicit endpoint types.**
  - `OllamaCloud` is split from `Ollama`, and `OpenAICompatible` is split from `OpenAI`.
  - A VMR-to-endpoint compatibility table replaces exact type matching.
  - Every VMR classifies requests by its configured type.
  - Nothing is inferred from URLs or hostnames.
- **Part D: Cross-cutting fixes.**
  - Upstream API keys are masked, with an audited reveal route.
  - Health checks can be turned off, and can use `POST` with a request body.
  - Allowlisted upstream response headers are forwarded.
  - CI is added.
- **Part E: PolyPrompt.** No dependency update is needed.

The plan complies with `~/Code/Agents/requirements/`. Where Conductor already departs from a requirement document, the plan says so and states whether this work closes the gap or records it as debt.

## Status Legend

- `[ ]` Not started
- `[~]` In progress
- `[x]` Completed
- `[!]` Blocked or needs a decision

## Guiding Rule: Everything Is Explicit

The operator chooses an endpoint's `ApiType` and a VMR's `ApiType`. Conductor never infers a type, a provider or a default from a hostname, URL path, response field or status code. Defaults are applied only from the type the operator selected.

## Decisions (resolved with the product owner)

| # | Decision |
|---|---|
| R1 | Endpoint types are explicit and separate wherever behavior differs: `Ollama` and `OllamaCloud`, `OpenAI` and `OpenAICompatible`, `TypeSafe` and `Ollaya`. |
| R2 | A VMR's type is the API clients call. An explicit compatibility table says which endpoint types each VMR type accepts. |
| R3 | Every VMR classifies requests by its configured type. A request outside that type's path namespace returns 404. This is a fix. |
| R4 | The dashboard's hostname heuristics are removed. Defaults come only from the selected type. |
| R5 | Outbound `x-api-key` is used when the endpoint type needs it (Anthropic). Inbound requests use Conductor's normal credentials. On Anthropic VMRs only, the Conductor credential may also arrive in `x-api-key`, because that is where Anthropic clients send it. Other VMR types never accept it, and it is never forwarded upstream. |
| R6 | The default `anthropic-version` is `2023-06-01`. The client's header wins. |
| R7 | Upstream API keys are masked in responses (`****` plus the last 4 characters), and an audited route reveals the full value. This is a fix. |
| R8 | Hosted TypeSafe health checks follow PolyPrompt: a one-question decision probe. They are optional and off by default, and when off the endpoint is treated as healthy. When on, the default interval is 60 seconds, which the operator can lower. A tooltip explains the cost. The Ollaya and hosted `/v1/models` discrepancy is documented. |
| R9 | Anthropic health checks are on by default at a 60-second interval. |
| R10 | The release is v1.1.0, and every version stays in sync, including the C#, JavaScript and Python SDKs. |

## Contract

The tables in this section are the binding contract for every surface.

### `ApiTypeEnum`

Values are appended and never renumbered, because the column is an integer.

| Value | Name | Client-facing (VMR type) | Wire API | Upstream auth |
|---|---|---|---|---|
| 0 | `Ollama` | yes | Ollama `/api/*` | Bearer if a key is set |
| 1 | `OpenAI` | yes | OpenAI `/v1/*` | Bearer |
| 2 | `Gemini` | yes | Gemini `/v1beta/*` | `x-goog-api-key` |
| 3 | `vLLM` | yes | OpenAI `/v1/*` | Bearer if a key is set |
| 4 | `Anthropic` | yes | Anthropic `/v1/*` | `x-api-key` and `anthropic-version` |
| 5 | `TypeSafe` | yes | System One `/v1/*` | Bearer (required) |
| 6 | `Ollaya` | no | System One `/v1/*` | Bearer if a key is set |
| 7 | `OllamaCloud` | no | Ollama `/api/*` | Bearer (required) |
| 8 | `OpenAICompatible` | no | OpenAI `/v1/*` | Bearer if a key is set |

### VMR-to-endpoint compatibility

| VMR `ApiType` | Accepted endpoint `ApiType` |
|---|---|
| `Ollama` | `Ollama`, `OllamaCloud` |
| `OpenAI` | `OpenAI`, `OpenAICompatible`, `vLLM` |
| `vLLM` | `vLLM` |
| `Gemini` | `Gemini` |
| `Anthropic` | `Anthropic` |
| `TypeSafe` | `TypeSafe`, `Ollaya` |

Two validation errors enforce this:

- A VMR whose `ApiType` is `Ollaya`, `OllamaCloud` or `OpenAICompatible` is rejected with `VmrApiTypeNotClientFacing`.
- An attached endpoint whose type is outside the table is rejected with the existing `EndpointApiTypeMismatch`.

Both apply through REST, MCP and backup restore.

### Request classification by VMR type

| VMR type | Path namespace | Classified routes | Inside the namespace, not classified | Outside the namespace |
|---|---|---|---|---|
| Ollama | `/api/` | existing Ollama table | forwarded as `Unknown` (today's behavior) | 404 |
| OpenAI, vLLM | `/v1/` | `/v1/chat/completions`, `/v1/completions`, `/v1/models`, `/v1/embeddings` | forwarded as `Unknown` | 404 |
| Gemini | `/v1beta/` | existing Gemini table | forwarded as `Unknown` | 404 |
| Anthropic | `/v1/` | `POST /v1/messages` (`AnthropicMessages`), `POST /v1/messages/count_tokens` (`AnthropicCountTokens`), `GET /v1/models` (`AnthropicListModels`), `GET /v1/models/{id}` (`AnthropicGetModel`) | `/v1/messages/batches*` and `/v1/files*` return 404 `not_found_error` (stateful, not supported). Other paths are forwarded as `Unknown`. | 404 |
| TypeSafe | `/v1/` | `POST /v1/systemone` (`TypeSafeDecide`), `GET /v1/models` (`TypeSafeListModels`) | forwarded as `Unknown` | 404 |

`GET` and `HEAD` on the VMR base URL remain the health probe.

### New and changed enums

| Enum | Change |
|---|---|
| `RequestTypeEnum` | add `AnthropicMessages`, `AnthropicCountTokens`, `AnthropicListModels`, `AnthropicGetModel`, `TypeSafeDecide`, `TypeSafeListModels` |
| `ModelAccessActionEnum` | add `Decisions = 7` |
| `ModelLoadProbeKindEnum` | add `Decision = 6` |
| `HealthCheckMethodEnum` | add `POST = 2` |

### New fields

| Model | Field | Type, default | Notes |
|---|---|---|---|
| `ModelRunnerEndpoint` | `AnthropicVersion` | `string`, `null` | Format `yyyy-MM-dd`. `null` means `Settings.Anthropic.DefaultVersion`. Anthropic only. |
| `ModelRunnerEndpoint` | `HealthCheckEnabled` | `bool`, `true` (type default `false` for `TypeSafe`) | When `false`, the endpoint is treated as healthy. |
| `ModelRunnerEndpoint` | `HealthCheckRequestBody` | `string`, `null` | Sent with `HealthCheckMethod = POST`. |
| `ModelDefinition` | `SupportsDecisions` | `bool`, `false` | |
| `VirtualModelRunner` | `AllowDecisions` | `bool`, `true` | |
| `RequestHistoryEntry` and analytics | `CacheCreationInputTokens`, `CacheReadInputTokens`, `DecisionQuestionCount` | `int?`, `null` | |

### Type defaults

The dashboard applies these when the operator selects a type. The server applies them on create for fields the request omits. Values for existing types are unchanged.

| Type | Host, port, SSL | Health method and URL | Auth on health | Enabled | Interval |
|---|---|---|---|---|---|
| `Anthropic` | `api.anthropic.com`, 443, on | `GET /v1/models?limit=1` | yes | yes | 60000 ms |
| `TypeSafe` | `api.typesafe.ai`, 443, on | `POST /v1/systemone` with the PolyPrompt probe body | yes | **no** | 60000 ms |
| `Ollaya` | `127.0.0.1`, operator supplies port, off | `GET /v1/models` | no | yes | 5000 ms |
| `OllamaCloud` | `ollama.com`, 443, on | `GET /api/tags` | yes | yes | 60000 ms |
| `OpenAICompatible` | `127.0.0.1`, operator supplies port, off | `GET /v1/models` | no | yes | 5000 ms |

The TypeSafe probe body matches the 404 branch of PolyPrompt's `TypeSafeClient.ValidateConnectivityAsync`:

```json
{"model":"jev-latest","state":"ping","questions":{"reachable":{"type":"noul","instructions":"Is this a test message?"}}}
```

### Routes

| Route | Behavior |
|---|---|
| `GET /v1.0/modelrunnerendpoints/{id}/apikey` (new) | Returns `{"ApiKey":"<full value>"}`. Tenant scoped, and requires the same permission as updating the endpoint. Writes an audit log line that does not contain the value. |
| Endpoint read and enumerate (existing) | `ApiKey` is masked (`****abcd`), or `null` when unset. |
| Endpoint create and update (existing) | If `ApiKey` is omitted, `null`, or equal to the masked value, the stored key is kept. An empty string clears it. Any other value replaces it. Responses are masked. |
| Backup export and restore (existing) | Keep the real keys. Backup files are documented as sensitive. |

### Settings

| Class (`ServerSettings` property) | Members and defaults |
|---|---|
| `AnthropicSettings` (`Anthropic`) | `DefaultVersion` `2023-06-01`; `ForwardedRequestHeaders` `anthropic-version`, `anthropic-beta`; `ForwardedResponseHeaders` `request-id`, `retry-after`, `x-should-retry`; `ForwardedResponseHeaderPrefixes` `anthropic-ratelimit-`; `SamplingParametersRemovedFromVersion` `2026-10-21` |
| `TypeSafeSettings` (`TypeSafe`) | `DefaultModel` `jev-latest`; `MaxQuestionsPerRequest` 64 (min 1, max 1024) |
| `RequestHistorySettings` | `RedactedHeaders` gains `x-api-key` and `anthropic-api-key` |
| `CorsSettings` | `AllowedHeaders` gains `anthropic-version`, `anthropic-beta` and `x-api-key` |

### Validation codes

| Code | Severity | Condition |
|---|---|---|
| `VmrApiTypeNotClientFacing` | error | VMR type is `Ollaya`, `OllamaCloud` or `OpenAICompatible` |
| `EndpointApiTypeMismatch` | error (existing, now table based) | endpoint type not accepted by the VMR type |
| `AnthropicApiKeyMissing` | error | Anthropic endpoint without a key |
| `TypeSafeApiKeyMissing` | error | TypeSafe endpoint without a key |
| `OllamaCloudApiKeyMissing` | error | OllamaCloud endpoint without a key |
| `AnthropicConfigurationMismatch` | warning | a configuration on an Anthropic VMR sets `RepeatPenalty` or `ContextWindowSize` |
| `ModelConfigurationSamplingIgnored` | warning | a configuration sets `Temperature`, `TopP` or `TopK` while the Anthropic endpoint version is `2026-10-21` or later |
| `AnthropicHealthCheckUnauthenticated` | warning | an Anthropic endpoint has health checks enabled without auth |
| `TypeSafeConfigurationIgnored` | warning | model configurations attached to a TypeSafe VMR |
| `DecisionModelNotEnabled` | warning | a TypeSafe VMR whose model definitions have no `SupportsDecisions` |
| `DecisionsDisabledOnDecisionRoute` | error | a TypeSafe VMR with `AllowDecisions = false` |
| `HealthCheckBodyRequired` | error | `HealthCheckMethod = POST` with no `HealthCheckRequestBody` |

### Anthropic proxy behavior

**Headers**

- Outbound `x-api-key` is the endpoint key.
- Outbound `anthropic-version` is the client's value if it sent one, then the endpoint's `AnthropicVersion`, then the settings default.
- Client headers listed in `ForwardedRequestHeaders` are copied upstream. `Authorization` is never sent upstream.
- Inbound `x-api-key`, `anthropic-api-key` and `authorization` are never forwarded, for any family.
- Response headers listed in `ForwardedResponseHeaders`, or matching `ForwardedResponseHeaderPrefixes`, are copied to the client.

**Errors**

Errors that Conductor itself generates on Anthropic VMRs use the shape `{"type":"error","error":{"type","message"}}`, and the `X-Conductor-Error-Code` header is kept. Upstream errors pass through unchanged.

| Condition | Anthropic error type |
|---|---|
| Authentication failure | `authentication_error` |
| Access denied | `permission_error` |
| Not found or unsupported | `not_found_error` |
| QoS rejection | `rate_limit_error` |
| Upstream failure or timeout | `api_error` |
| No healthy endpoint | `overloaded_error` |

**Model configurations**

- `RepeatPenalty` and `ContextWindowSize` are never injected.
- `MaxTokens` maps to `max_tokens`.
- `Temperature`, `TopP` and `TopK` are injected only when the effective version is earlier than `SamplingParametersRemovedFromVersion`.

**Usage**

- Non-streaming responses: `usage.input_tokens` and `usage.output_tokens`, plus the two cache counts.
- Streaming responses: input tokens come from `message_start.message.usage.input_tokens`. Output tokens come from the last `message_delta.usage.output_tokens`.
- The request id comes from the `request-id` header, then from `message.id`.

**Model load**

- `Auto` and `MetadataOnly` call `GET /v1/models/{id}` and report `VerifiedRemote`.
- `ChatCompletion` sends `POST /v1/messages` with `max_tokens: 1`. This is billable.
- Other probe kinds report `UnsupportedApiType`.

### TypeSafe and Ollaya behavior

- **Transport:** outbound Bearer, no streaming.
- **Request limits:** a request with an empty `questions` object, or more than `MaxQuestionsPerRequest` questions, gets a 400.
- **Model configurations:** never applied.
- **Usage:** `usage.input_tokens` and `usage.output_tokens`, plus the recorded question count.
- **Model load:**
  - `Auto` on `Ollaya` calls `GET /v1/models` and reports `Verified`.
  - `Auto` on `TypeSafe` sends the one-question `Decision` probe and reports `VerifiedRemote`. This is billable.
- **Capability gating:**
  - A decision request on a VMR with `AllowDecisions = false` gets a 403.
  - In strict mode, a decision request for a model definition without `SupportsDecisions` gets a 404.
  - A completion or embedding request for a model definition that only has `SupportsDecisions` is rejected the same way.
- **Model access:** `TypeSafeDecide` maps to the `Decisions` action. Policies that allow `Completions` do not also allow `Decisions`.

### OllamaCloud and OpenAICompatible behavior

**`OllamaCloud`**

- Same wire API as Ollama, with a required Bearer key.
- No model management (pull, delete), keep-warm or RigMonitor. Validation and the services reject them.
- Model load reports `VerifiedRemote`, and probes are billable.

**`OpenAICompatible`**

- Same wire API as OpenAI, with an optional key.
- Model load reports `Verified`, not `VerifiedRemote`, and probes are not flagged as billable.

## Requirements Compliance Map

| Document | How this plan complies |
|---|---|
| `CODE_STYLE.md`, `CLAUDE.md` | One class or enum per file. Usings inside the namespace. No `var`. XML docs on public members. Settings instead of constants for values an operator may change. No em-dashes in any text. |
| `BACKEND_ARCHITECTURE.md` | Typed DTOs for the new wire formats under `ThirdParty/Anthropic` and `ThirdParty/TypeSafe`. Additive `EnsureColumnAsync` migrations on all four providers. Tenant scoping. A settings class per provider. Redaction of `x-api-key`. Telemetry on the new integrations. **Recorded debt:** `RequestAnalyticsCaptureProcessor` still navigates `JsonNode` for the older families. |
| `AUTHENTICATION.md` | Keys are masked, with an audited reveal route (R7). **Documented deviation (R5):** the document reserves `x-api-key` for administration. Conductor's admin header is `x-admin-apikey`, so the two do not collide. On Anthropic VMRs only, `x-api-key` carries a normal Conductor credential, validated through the existing credential lookup, and it is never forwarded. Recorded in `docs/adr/0005-anthropic-provider.md`. **Recorded debt:** encryption at rest. |
| `TELEMETRY_REQUIREMENTS.md` | New `api_family` values. Model names appear on spans only. Span status is set on upstream failures. |
| `REPOSITORY_REQUIREMENTS.md` | REST_API.md, MCP_API.md, Postman and the SDK READMEs are updated. Adds `.github/workflows/tests.yml`. Records `AOT support` in the README. SDK harnesses use `127.0.0.1`. |
| `BACKEND_TEST_ARCHITECTURE.md` | New tests live in `Test.Shared` and run through every host. Fake upstreams bind to `127.0.0.1`. **Recorded debt:** Touchstone 0.2.1, reflection-based discovery, FluentAssertions, and the deprecated xUnit adapter. |
| `SIMULATED_USER_TESTING.md` | Covered by Phase F. |
| `FRONTEND_ARCHITECTURE.md`, `DASHBOARD_STYLE_AND_USABILITY.md`, `I18N.md` | One provider registry serves as the display-label layer. Selects for bounded sets. Secret inputs with a reveal control. Semantic badge tokens. **Recorded conflict:** the dashboard has no i18n runtime. New strings go through the registry, and the gap is stated. |
| `BENCHMARKING.md` | Conductor has no benchmark harness and none is built here (recorded). The fake upstreams can be reused by one later. |
| `VERSIONING.md` | Version 1.1.0 everywhere, as explicitly approved (R10). |
| `WRITING_DOCUMENTS.md` | No em-dashes. |

---

## Phase 0: Prerequisites

- [x] Record the baseline before changes: build clean (0 warnings, 0 errors), `Test.Automated` 1323 of 1323 passing.
- [x] Add `.github/workflows/tests.yml`. It runs on push and pull request, with a `Full suite (<os>)` job on ubuntu, windows and macos (`fail-fast: false`). The job covers `Test.Automated`, `Test.Xunit`, `Test.Nunit`, `Test.McpServer`, the C# SDK tests, the JavaScript and Python SDK harnesses through `run-tests.sh`, and dashboard `npm test`. Results are uploaded with `if: always()`.
- [x] Add `run-tests.sh` and `run-tests.bat` to each SDK.
- [x] Add an `AOT support` heading to README.md (not supported, with reasons).
- [x] Add `user-testing/evidence/` to `.gitignore`.

## Phase 1: Core Taxonomy, Models and Settings

- [x] Make the enum changes from the Contract, with XML docs.
- [x] Add `ApiTypeCompatibility`, a static helper in `Conductor.Core/Helpers/`, with `IsClientFacing`, `AcceptsEndpoint` and `GetAcceptedEndpointTypes`.
- [x] Add `ApiTypeDefaults`, a static helper with the type-defaults table. `ModelRunnerEndpoint` tracks which health fields were set explicitly, so create fills in only the ones left unset.
- [x] Add the new `ModelRunnerEndpoint`, `ModelDefinition` and `VirtualModelRunner` fields, with validating backing fields.
- [x] Add `AnthropicSettings` and `TypeSafeSettings`, wire them into `ServerSettings`, and update the default redacted headers and CORS headers.
- [x] Add typed wire models in `ThirdParty/Anthropic/Models/` and `ThirdParty/TypeSafe/Models/`, one class per file.
- [x] Update the classification helpers:
  - `IsCompletionsRequest` includes `AnthropicMessages`.
  - Anthropic list and get count as model listing.
  - Add `IsDecisionRequest` and `IsTokenCountRequest`.

## Phase 2: Database

- [x] Add the new columns on all four providers, in both the `CREATE TABLE` text and `EnsureColumnAsync`:
  - `modelrunnerendpoints`: `anthropicversion`, `healthcheckenabled` (default 1), `healthcheckrequestbody`.
  - `modeldefinitions`: `supportsdecisions` (default 0).
  - `virtualmodelrunners`: `allowdecisions` (default 1).
  - Request history: `cachecreationinputtokens`, `cachereadinputtokens`, `decisionquestioncount`.
- [x] Update insert, update and `FromDataRow` for the new fields.
- [x] Update `docker/factory/schema.sql` and `docker/postgres/init.sql`.
- [x] Make backup and restore round-trip the new fields and the unmasked keys.
- [x] Make `DataTableHelper.GetEnumValue` log a warning when it reads an unknown integer.

## Phase 3: Classification, Authentication and Proxy

- [x] `UrlContext.Parse` takes the VMR type and applies the classification table. `ProxyController`, `RoutingDecisionService`, `ModelLoadService` and `ConductorRouteModule` pass it in.
- [x] Replace the two `GetApiFamily` copies with one `ApiFamilyResolver`.
- [x] Inbound: accept a Conductor credential in `x-api-key` on Anthropic VMRs only.
- [x] Outbound: set per-family headers. Inbound secrets are never forwarded.
- [x] Forward allowlisted response headers.
- [x] Add the Anthropic error mapper for Conductor-originated errors.
- [x] Add the TypeSafe question-count guard.
- [x] Apply the body mutation rules: Anthropic sampling by version and `max_tokens`, and no configuration injection for TypeSafe.
- [x] Add decision capability gating and the model access mapping.
- [x] Add model-listing filter builders for Anthropic and TypeSafe.
- [x] Add the QoS metadata types.
- [x] Add the `AuthorizationConfig` entries.

## Phase 4: Health Checks and Model Loading

- [x] When `HealthCheckEnabled = false`, treat the endpoint as healthy and send no probe.
- [x] `POST` health checks send `HealthCheckRequestBody` with `Content-Type: application/json`.
- [x] Use per-family auth headers for health checks and the model load transport.
- [x] Implement probe builders and outcomes per the Contract, including `Decision`, `VerifiedRemote` for OllamaCloud, and `Verified` for OpenAICompatible.
- [x] Make Ollama-only features (model management, keep-warm, RigMonitor) reject OllamaCloud endpoints.

## Phase 5: Validation, Key Masking and MCP

- [x] Implement every validation code in the Contract.
- [x] Mask keys in responses, and add the reveal route with an audit log line.
- [x] MCP:
  - VMR writes go through validation.
  - The `api_type` description lists every type.
  - Output masks keys.

## Phase 6: Analytics and Telemetry

- [x] Parse Anthropic and TypeSafe usage with typed DTOs, choosing the parser by endpoint type.
- [x] Capture and store cache token counts and the decision question count.
- [x] Add the new telemetry `api_family` values, the span attributes, and `conductor.decision.question_count`.

## Phase 7: Dashboard

- [x] Add a provider registry (`dashboard/src/utils/providers.js`) and use it everywhere the type list or type behavior appears.
- [x] Remove the hostname heuristics.
- [x] Endpoint form:
  - All nine types, with the type defaults applied when a type is selected.
  - An `Anthropic version` field.
  - A `Health checks enabled` toggle, with a TypeSafe tooltip explaining billing.
  - A POST health method with a body field.
  - A masked API key with a reveal control that calls the reveal route.
  - Duplicate uses the reveal route.
- [x] VMR form:
  - Only client-facing types can be selected.
  - The endpoint picker is filtered by the compatibility table.
  - An `AllowDecisions` setting.
  - An Anthropic "Connect a client" panel.
- [x] Model definitions: a `SupportsDecisions` setting.
- [x] API Explorer: Anthropic operations (messages, streaming, count tokens, models), a TypeSafe Decide operation with a question builder, and code generators for both.
- [x] Load model modal: billable flags from the registry, and the `Decision` probe.
- [x] Analytics and history: a provider filter, and cache and question-count columns that are hidden by default. (Provider filter is on Analytics, which has no column selector; the hidden columns are on Request History, which has no provider filter.)
- [x] Onboarding copy and badge tokens.
- [x] Vitest coverage. (Playwright visual QA in light and dark themes not done.)

## Phase 8: SDKs

- [x] C#, JavaScript and Python:
  - An `ApiType` definition with all nine values.
  - The new fields.
  - A method to reveal an endpoint key.
  - README examples: creating Anthropic and TypeSafe resources, and calling an Anthropic VMR with the official Anthropic SDK.
  - Tests.
- [x] The C# test base URL uses `127.0.0.1`.
- [x] SDK versions set to 1.1.0.

## Phase 9: Postman, Documentation, Website, Deployment

- [x] Postman:
  - Create requests for every new endpoint type.
  - The reveal route.
  - Validation examples.
  - A Proxied APIs folder with Anthropic and Decision subfolders.
  - Descriptions added to every folder and request, with new variables for the provider resources (upstream key variables are empty).
- [x] Update README, DOCKERHUB_README, REST_API.md, MCP_API.md, TELEMETRY.md, QOS_OVERVIEW.md, CHANGELOG.md (v1.1.0) and `www/index.html`.
- [x] Add `docs/adr/0005-anthropic-provider.md` and `docs/adr/0006-decision-models-and-explicit-types.md`.
- [x] Docker and Kubernetes settings: redacted headers and provider settings blocks. Docker `conductor.json` files are done. Kubernetes has no settings template of its own: `kubernetes/configure.sh` generates its private `conductor.json` from `docker/conductor.json`, so it inherits the new headers and blocks.
- [x] Set version 1.1.0 in every location the 1.0.0 bump touched (commit `ca95c2c`), plus the SDK manifests. (done)

## Phase 10: Tests

- [x] Fake Anthropic and TypeSafe upstreams bound to `127.0.0.1`.
- [x] Unit tests for every row of the Contract tables:
  - enums and compatibility;
  - classification for every VMR type, including 404 outside the namespace;
  - type defaults and settings;
  - masking and reveal;
  - every validation code;
  - mutation rules;
  - probe builders;
  - usage parsing;
  - health checks (enabled flag and POST);
  - access mapping;
  - list filters.
- [x] Proxy end-to-end tests against the fakes:
  - headers in both directions;
  - SSE passthrough;
  - inbound `x-api-key` on Anthropic VMRs;
  - errors in Anthropic shape;
  - 404 for batches;
  - a TypeSafe decide round trip;
  - the question limit;
  - an OpenAI regression case.
- [x] Database round trips through the shared contract tests (SQLite; the PostgreSQL, MySQL and SQL Server changes are compile-checked only because no servers are available here).
- [x] MCP, SDK and dashboard cases.
- [x] All suites pass.

## Phase F: Simulated User Testing

- [x] Run a focused session against a local instance with the fake upstreams, following `SIMULATED_USER_TESTING.md`. Personas:
  - a Claude Code user;
  - a decision model evaluator;
  - an operator migrating an OpenAI-typed LM Studio endpoint to `OpenAICompatible`;
  - a careless user.
- [x] Write the report in `user-testing/`. The report is `user-testing/2026-11-21-v1.1.0.md`. It has 25 findings: S1 2, S2 7, S3 9, S4 7. Not covered: the dashboard, Docker Compose and Kubernetes, and the non-SQLite databases.
- [x] Review the findings with the product owner. Decision: address all 25 before release. Revealing an endpoint key requires tenant admin or higher (UT-017). Endpoints get an immediate health check on create and at startup (UT-007).
- [x] Fix the approved findings, each with a regression test. All 32 findings (UT-001 to UT-032, including seven found during the fix and re-verification passes) are fixed.
- [x] Re-verify the fixes on a fresh instance, record each outcome in the report, and add CHANGELOG entries for user-visible fixes.

## Phase R: Release

- [x] All tests pass: Test.Automated, Test.Xunit and Test.Nunit 1475 of 1475 each, Test.McpServer 36, C# SDK 52, JavaScript SDK 26, Python SDK 30, dashboard 270. Build has 0 warnings and 0 errors.
- [x] Run `./build-all.sh v1.1.0`. Server and dashboard images were built and pushed as `v1.1.0` and `latest`.
- [x] Commit and push `feature/v1.1.0`, merge to `main`, push, verify on GitHub, then delete the feature branch locally and remotely. Merge commit `0ee5570` is on `origin/main`.
- [x] Move this plan to `archive/`, then commit and push.

---

## Part E: PolyPrompt

PolyPrompt is not referenced by any Conductor project, so there is no dependency to update. It is also not added as a server dependency: Conductor is a byte-level proxy, while PolyPrompt is a client that parses responses into its own models. PolyPrompt's `TypeSafeClient` is the reference for the System One wire format and for the hosted TypeSafe health probe.

## Out of Scope

- Cross-format translation, such as OpenAI clients calling Anthropic upstreams.
- Message Batches and Files through Conductor.
- Claude on Bedrock, Vertex and Foundry.
- Encryption at rest for upstream keys.
- A dashboard i18n runtime.
- The Touchstone 1.0 harness upgrade.
- A benchmark harness.

## Migration Notes for v1.1.0

- **Paths outside a VMR's namespace now return 404.** For example, an OpenAI SDK pointed at an Ollama VMR's `/v1/` routes used to reach Ollama's own OpenAI-compatible API, only because of how paths were classified. It now gets a 404. Create an `OpenAI` VMR with an `OpenAICompatible` endpoint pointing at the Ollama host instead.
- **Existing `OpenAI` endpoints keep their type.** If an endpoint points at a non-OpenAI server, the operator should change it to `OpenAICompatible`. Conductor does not change it automatically.
- **Endpoint `ApiKey` values are masked in responses.** Use `GET /v1.0/modelrunnerendpoints/{id}/apikey` to read the full value.
