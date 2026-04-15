# vLLM + Gemini Implementation Plan

## Purpose

This document is the implementation plan for adding:

- `vLLM` as a new model runner type that is functionally OpenAI-compatible
- `Gemini` as a new model runner type with its own backend and dashboard support
- website and documentation updates describing support for both

This plan is intended to be actionable and progress-trackable. A developer should be able to work through it item by item, annotate status, add notes, and record deviations.

## Status Legend

- `[ ]` Not started
- `[~]` In progress
- `[x]` Completed
- `[!]` Blocked / needs decision

## Summary Of Expected Scope

### `vLLM`

`vLLM` should be treated as a first-class `ApiType`, but it is expected to reuse the OpenAI-compatible request/response model and routing behavior. The work is broader than just copying the OpenAI client classes, because the system also has:

- persisted API type values
- URL parsing and request classification
- dashboard dropdowns and explorer behavior
- tests and docs that currently assume only OpenAI and Ollama

### `Gemini`

`Gemini` should be treated like a third protocol family similar in architectural weight to the existing OpenAI vs Ollama split. The core proxy transport is generic, but the following areas are provider-aware and will need explicit Gemini support:

- request type enums
- URL parsing and route classification
- VMR API family selection
- model/config pinning assumptions
- API Explorer request templates and stream parsing
- dashboard wording and provider choices
- docs and website messaging

## High-Level Decisions

### Decisions Already Assumed By This Plan

- `vLLM` is a distinct `ApiType`, not merely a UI alias for `OpenAI`
- `vLLM` uses the OpenAI-compatible API family
- `Gemini` is a separate provider implementation, not an OpenAI alias
- this work includes backend, dashboard, tests, main repo docs, and the website repo

### Open Decisions To Resolve Before Or During Implementation

- `[!]` Gemini v1 endpoint surface area
  - Recommendation: start with text/chat generation, streaming, embeddings, and model listing only if it maps cleanly
- `[!]` Gemini request/response canonical form inside Conductor
  - Recommendation: preserve Gemini-native payloads at the proxy layer and add only the minimum mapping required for model/config enforcement
- `[!]` Whether to create dedicated `Conductor.Core.ThirdParty.vLLM` classes
  - Recommendation: yes, if the product wants explicit provider classes and namespaces even though behavior mirrors OpenAI
- `[!]` Whether `vLLM` VMRs should expose OpenAI-compatible routes only
  - Recommendation: yes
- `[!]` Whether Gemini VMRs should support model-management-style operations
  - Recommendation: no in v1 unless a concrete Gemini-equivalent need is identified

## Current Relevant Code Areas

### Core / Backend

- `src/Conductor.Core/Enums/ApiTypeEnum.cs`
- `src/Conductor.Core/Enums/RequestTypeEnum.cs`
- `src/Conductor.Core/Models/UrlContext.cs`
- `src/Conductor.Core/Models/ModelRunnerEndpoint.cs`
- `src/Conductor.Core/Models/VirtualModelRunner.cs`
- `src/Conductor.Core/ThirdParty/OpenAI/*`
- `src/Conductor.Core/ThirdParty/Ollama/*`
- `src/Conductor.Server/Services/RequestTypeResolver.cs`
- `src/Conductor.Server/Controllers/ProxyController.cs`
- `src/Conductor.Server/Services/HealthCheckService.cs`

### Dashboard

- `dashboard/src/views/ModelRunnerEndpoints.jsx`
- `dashboard/src/views/VirtualModelRunners.jsx`
- `dashboard/src/views/Dashboard.jsx`
- `dashboard/src/views/ApiExplorer.jsx`
- `dashboard/src/hooks/useApiExplorer.js`
- `dashboard/src/utils/requestTemplates.js`
- `dashboard/src/utils/codeGenerators.js`
- `dashboard/src/components/ApiExplorerConfig.jsx`
- `dashboard/src/components/SetupWizard.jsx`
- `dashboard/src/components/Tour.jsx`

### Tests

- `src/Conductor.Core.Tests/Enums/EnumTests.cs`
- `src/Conductor.Core.Tests/Models/UrlContextTests.cs`
- any tests touching `ApiType`, proxied routes, or request classification

### Docs / Website

- `README.md`
- `c:\code\conductor\conductor.github.io\index.html`
- `c:\code\conductor\conductor.github.io\script.js`
- `c:\code\conductor\conductor.github.io/styles.css`

## Implementation Strategy

Recommended order:

1. Add `vLLM` plumbing first
2. Add Gemini backend routing and request classification
3. Add Gemini dashboard/API Explorer support
4. Update tests
5. Update docs and website
6. Run end-to-end validation

Rationale:

- `vLLM` is the lower-risk change and validates the provider-expansion plumbing
- Gemini will likely require some refactoring in request typing and dashboard behavior; that is easier after the `ApiType` surface has already been generalized

---

## Phase 1: Expand Provider Taxonomy In Core Models And Enums

### Goals

- make provider support explicitly extensible beyond OpenAI and Ollama
- introduce `vLLM` and `Gemini` in persisted/configurable types

### Tasks

- [ ] Update `src/Conductor.Core/Enums/ApiTypeEnum.cs`
  - add `Gemini`
  - add `vLLM`
  - decide and preserve enum numeric ordering carefully if compatibility matters
- [ ] Update `src/Conductor.Core/Enums/RequestTypeEnum.cs`
  - add any new Gemini request types needed for supported Gemini endpoints
  - do not add `vLLM`-specific request types if `vLLM` will share OpenAI request semantics
- [ ] Review all code paths that assume only two `ApiTypeEnum` values
- [ ] Review any serialization, DB round-trip, or enum parsing behavior that may be impacted

### Notes

- `ModelRunnerEndpoint` and `VirtualModelRunner` already persist `ApiType`, so no schema change is expected if enum values are stored as strings or integral enums through current serialization paths
- still verify DB behavior against all database drivers

### Validation

- [ ] `ApiType` can round-trip through create/read/update flows
- [ ] existing OpenAI and Ollama records remain readable
- [ ] enum tests updated and passing

---

## Phase 2: Add `vLLM` Backend Support

### Goals

- make `vLLM` selectable and persisted as its own runner type
- route `vLLM` traffic through the OpenAI-compatible code path

### Tasks

- [ ] Create `Conductor.Core.ThirdParty.vLLM` namespace and classes if explicit duplication is desired
  - recommendation: mirror the OpenAI client/model class layout for clarity and product semantics
  - confirm whether these classes are currently consumed anywhere or are mostly library-style wrappers
- [ ] If duplicating OpenAI classes for `vLLM`, audit for any hard-coded `OpenAI` naming or comments that would become misleading
- [ ] Update request classification so `vLLM` VMRs are treated as OpenAI-family routes
- [ ] Confirm whether any provider-specific auth or header behavior differs for `vLLM`
  - expected answer: no for v1
- [ ] Update health-check defaults and UX expectations where needed
  - likely same guidance as OpenAI-compatible endpoints

### Affected Files

- `src/Conductor.Core/Enums/ApiTypeEnum.cs`
- `src/Conductor.Core/Models/UrlContext.cs`
- `src/Conductor.Server/Services/RequestTypeResolver.cs`
- optional new `src/Conductor.Core/ThirdParty/vLLM/*`

### Notes

- `ProxyController` is already transport-generic and forwards JSON plus auth headers. `vLLM` should not need a distinct proxy implementation if it remains OpenAI-compatible.
- The main cost here is taxonomy and UI/test coverage, not transport complexity.

### Validation

- [ ] A model runner endpoint can be created with `ApiType = vLLM`
- [ ] A VMR can be created with `ApiType = vLLM`
- [ ] OpenAI-style paths on a `vLLM` VMR route correctly
- [ ] Streaming behavior matches existing OpenAI path expectations

---

## Phase 3: Add Gemini Backend Routing And Request Classification

### Goals

- support Gemini as a first-class VMR and endpoint API type
- classify Gemini requests correctly and route them through the proxy

### Tasks

- [ ] Define supported Gemini endpoint patterns for v1
  - record exact URL patterns to support
  - record corresponding HTTP methods
- [ ] Add Gemini request types to `RequestTypeEnum`
- [ ] Update `src/Conductor.Core/Models/UrlContext.cs`
  - detect Gemini route family
  - assign `ApiType = Gemini`
  - map request paths to Gemini request types
  - decide how `IsEmbeddingsRequest`, `IsCompletionsRequest`, and `IsModelManagementRequest` behave for Gemini
- [ ] Update `src/Conductor.Server/Services/RequestTypeResolver.cs`
  - add proxied Gemini route detection
  - ensure Gemini routes are classified consistently with `UrlContext`
- [ ] Review `BuildTargetUrl` assumptions
  - current implementation mostly appends relative paths directly; confirm this is sufficient for Gemini path forms

### Notes

- This is the first place where Gemini becomes materially different from `vLLM`.
- Keep the first implementation narrow and explicit; do not attempt to generalize all providers at once unless the current code becomes too awkward.

### Validation

- [ ] each supported Gemini path parses to the correct `ApiType` and `RequestType`
- [ ] unsupported Gemini paths fail predictably
- [ ] current OpenAI and Ollama parsing remains unchanged

---

## Phase 4: Add Gemini Proxy Behavior And Request Handling

### Goals

- forward Gemini requests successfully
- preserve existing model-definition/configuration features where feasible

### Tasks

- [ ] Review `src/Conductor.Server/Controllers/ProxyController.cs` for assumptions about request shape
- [ ] Identify all places that assume a top-level `model` field
  - model resolution
  - strict mode
  - request history model naming
- [ ] Implement Gemini-aware model extraction/injection if Gemini request bodies differ
- [ ] Review `ApplyConfigurationToBody`
  - current fields are OpenAI/Ollama-centric: `temperature`, `top_p`, `top_k`, `max_tokens`, `repeat_penalty`, `num_ctx`
  - define which config fields map cleanly to Gemini
  - decide whether unsupported config values are ignored, transformed, or blocked
- [ ] Review `ApplyPinningAsync`
  - confirm pinned property merging is still valid for Gemini request shapes
  - if Gemini request schema is nested, simple top-level JSON merge may be insufficient
- [ ] Review response handling in `SendProxyResponse`
  - ensure content type and streaming modes for Gemini are passed through correctly

### Risks

- Gemini may not fit the current assumption that the requested model lives at `body["model"]`
- pinned properties may need provider-aware merge logic
- request history model extraction may be incomplete for Gemini until adapter logic is added

### Recommended v1 Behavior

- preserve raw Gemini request and response payloads wherever possible
- add only the minimal provider-specific model/config logic needed for:
  - strict mode
  - model definition enforcement
  - common configuration fields such as temperature and token limits if applicable

### Validation

- [ ] non-streaming generation request proxies successfully
- [ ] streaming generation request proxies successfully
- [ ] embeddings request proxies successfully
- [ ] strict mode works correctly for Gemini
- [ ] config pinning behavior is either supported or explicitly documented as limited

---

## Phase 5: Update Health Checks And Endpoint Defaults

### Goals

- ensure setup flows for `vLLM` and Gemini are practical from the dashboard

### Tasks

- [ ] Review `src/Conductor.Server/Services/HealthCheckService.cs`
  - confirm no provider-specific code other than auth header behavior
- [ ] Add provider-friendly default health-check guidance in the dashboard
- [ ] Extend hostname auto-parse/default logic in `dashboard/src/views/ModelRunnerEndpoints.jsx`
  - currently special-cases `api.openai.com`
  - consider whether Gemini endpoints need similar defaults
- [ ] Decide whether provider-specific helper text is needed in the endpoint form

### Validation

- [ ] endpoint health checks still function for OpenAI and Ollama
- [ ] `vLLM` endpoint health check guidance is correct
- [ ] Gemini endpoint health check guidance is correct for the supported deployment model

---

## Phase 6: Update Dashboard Endpoint And VMR Management

### Goals

- allow users to create, edit, view, and filter `vLLM` and Gemini endpoints/VMRs

### Tasks

- [ ] Update `dashboard/src/views/ModelRunnerEndpoints.jsx`
  - add `vLLM` and `Gemini` to API type selectors
  - update tooltips and explanatory text
  - review defaults for port/health-check URL/auth wording
- [ ] Update `dashboard/src/views/VirtualModelRunners.jsx`
  - add `vLLM` and `Gemini` to API type selectors
  - update explanatory text currently stating OpenAI/Ollama only
  - review `AllowModelManagement` wording so it remains accurate
- [ ] Update dashboard summary text in `dashboard/src/views/Dashboard.jsx`
- [ ] Update onboarding/tour/setup copy
  - `dashboard/src/components/SetupWizard.jsx`
  - `dashboard/src/components/Tour.jsx`
- [ ] Audit any filtering/sorting/display logic that assumes only two provider names

### Notes

- `vLLM` can mostly inherit OpenAI-family UX
- Gemini will need distinct labels and likely different API Explorer operations

### Validation

- [ ] endpoint create/edit screens support all four provider values
- [ ] VMR create/edit screens support all intended provider values
- [ ] no UI copy incorrectly says “OpenAI or Ollama only”

---

## Phase 7: Update API Explorer For `vLLM` And Gemini

### Goals

- allow dashboard users to test `vLLM` and Gemini VMRs directly

### Tasks

- [ ] Update `dashboard/src/utils/requestTemplates.js`
  - `vLLM` should reuse OpenAI operation definitions and templates
  - add Gemini operation definitions and request templates
- [ ] Update `dashboard/src/hooks/useApiExplorer.js`
  - treat `vLLM` like OpenAI for request template generation and stream parsing
  - add Gemini streaming parsing logic
  - add Gemini response content extraction logic
- [ ] Update `dashboard/src/components/ApiExplorerConfig.jsx`
  - add `vLLM` and `Gemini` choices
- [ ] Review `dashboard/src/views/ApiExplorer.jsx`
  - ensure labels and explanatory text reflect the new providers
- [ ] Review `dashboard/src/utils/codeGenerators.js`
  - decide whether:
    - `vLLM` should use the same code generators as OpenAI
    - Gemini gets no SDK code generators in v1
    - Gemini gets generic HTTP code only in v1

### Notes

- `vLLM` should be low effort in the explorer if it is treated as OpenAI-compatible
- Gemini is likely the single largest dashboard-specific change because templates, operations, and stream parsing are all provider-aware

### Validation

- [ ] API Explorer can send OpenAI-style requests to `vLLM`
- [ ] API Explorer can send Gemini requests to Gemini VMRs
- [ ] streaming preview works for supported `vLLM` and Gemini operations
- [ ] operation lists switch correctly when API type changes

---

## Phase 8: Add Or Update Third-Party Client Layers

### Goals

- keep provider integrations organized and maintainable

### Tasks

- [ ] Decide whether backend code will directly proxy Gemini without a dedicated `ThirdParty/Gemini` client
  - if the proxy is fully raw, a client may not be strictly necessary
  - if tests/utilities or future direct integrations benefit, add one
- [ ] If adding a Gemini client layer:
  - create `src/Conductor.Core/ThirdParty/Gemini/*`
  - define request/response models for the supported Gemini operations
  - add stream parsing helpers if useful
- [ ] If adding a `vLLM` client layer:
  - create `src/Conductor.Core/ThirdParty/vLLM/*`
  - mirror OpenAI classes as requested, but verify naming/comments/usings

### Notes

- This phase may be optional for runtime proxy support if everything is raw pass-through.
- It is still useful if the codebase wants explicit provider wrappers for testability or future direct usage.

### Validation

- [ ] any newly added provider clients compile and have smoke coverage
- [ ] no dead code is introduced without a clear ownership reason

---

## Phase 9: Tests

### Goals

- preserve current behavior
- add strong coverage for the new provider types

### Tasks

- [ ] Update `src/Conductor.Core.Tests/Enums/EnumTests.cs`
  - add `ApiTypeEnum` coverage for `Gemini` and `vLLM`
  - add `RequestTypeEnum` coverage for Gemini operations
- [ ] Update `src/Conductor.Core.Tests/Models/UrlContextTests.cs`
  - add parsing tests for `vLLM` OpenAI-family routes if needed
  - add Gemini route parsing tests
  - add `IsEmbeddingsRequest` / `IsCompletionsRequest` tests for Gemini
- [ ] Add or update `RequestTypeResolver` tests
  - ensure proxied Gemini routes resolve correctly
- [ ] Add or update backend proxy tests if present
  - request forwarding
  - provider-specific request handling
  - streaming behavior
- [ ] Add dashboard tests if the project currently has a frontend test setup
  - provider selector behavior
  - API Explorer operation switching
  - stream parsing behavior

### Regression Focus

- [ ] OpenAI behavior remains unchanged
- [ ] Ollama behavior remains unchanged
- [ ] `vLLM` behaves identically to OpenAI where intended
- [ ] Gemini paths do not break non-Gemini VMR routing

---

## Phase 10: Main Repo Documentation

### Goals

- document actual support accurately
- avoid claiming more Gemini coverage than exists

### Tasks

- [ ] Update `README.md`
  - overview sentence
  - features list
  - model runner endpoint description
  - VMR proxy examples
  - setup notes and health-check guidance as needed
- [ ] Document `vLLM` clearly as OpenAI-compatible
- [ ] Document Gemini support scope explicitly
  - supported endpoints
  - unsupported or deferred features
- [ ] Add any provider-specific setup notes if necessary

### Validation

- [ ] README examples match implemented routes
- [ ] docs do not overstate Gemini support

---

## Phase 11: Website Repo Updates

### Goals

- advertise Gemini and `vLLM` support accurately and consistently

### Tasks

- [ ] Update `c:\code\conductor\conductor.github.io\index.html`
  - metadata keywords/description
  - hero/provider illustrations if desired
  - feature copy currently naming only OpenAI/Ollama
  - pills/badges/provider labels
- [ ] Update `c:\code\conductor\conductor.github.io\script.js` if any provider text or animated labels are script-driven
- [ ] Update `c:\code\conductor\conductor.github.io\styles.css` only if layout changes require it
- [ ] Ensure website wording distinguishes:
  - OpenAI-compatible providers such as `vLLM`
  - native Gemini support

### Suggested Messaging

- Conductor supports OpenAI, Ollama, Gemini, and OpenAI-compatible backends such as `vLLM`
- avoid implying that all providers expose identical APIs

### Validation

- [ ] homepage mentions Gemini and `vLLM`
- [ ] no stale “OpenAI and Ollama only” copy remains
- [ ] visual layout still works after any copy changes

---

## Phase 12: Manual End-To-End Validation

### `vLLM` Validation Checklist

- [ ] create `vLLM` endpoint from dashboard
- [ ] create `vLLM` VMR from dashboard
- [ ] call OpenAI-style non-streaming generation through VMR
- [ ] call OpenAI-style streaming generation through VMR
- [ ] call embeddings through VMR
- [ ] verify request history and health views still behave correctly

### Gemini Validation Checklist

- [ ] create Gemini endpoint from dashboard
- [ ] create Gemini VMR from dashboard
- [ ] call supported Gemini generation endpoint through VMR
- [ ] test Gemini streaming path through VMR
- [ ] test Gemini embeddings path through VMR if included in v1
- [ ] verify strict mode / model definition behavior
- [ ] verify configuration pinning behavior or confirm/document limitation

### Regression Validation Checklist

- [ ] OpenAI endpoint and VMR still work
- [ ] Ollama endpoint and VMR still work
- [ ] dashboard API Explorer still works for OpenAI and Ollama
- [ ] health checks and request history still work globally

---

## Recommended Implementation Breakdown By PR Or Work Batch

### Batch 1: Core Enum + UI Taxonomy Plumbing

- add `ApiTypeEnum` values
- add dashboard provider options
- update baseline tests

### Batch 2: `vLLM`

- add `vLLM` backend routing behavior
- add `vLLM` dashboard/API Explorer behavior
- update docs

### Batch 3: Gemini Backend

- add Gemini request types and routing
- add Gemini proxy/model/config behavior
- add backend tests

### Batch 4: Gemini Dashboard + Explorer

- add Gemini dashboard forms and explorer support
- add frontend tests

### Batch 5: Docs + Website + Final Validation

- update README
- update website repo
- run full regression validation

---

## Risks And Watchouts

- `[!]` Enum value changes may affect stored data or serialized API contracts if numeric values are assumed anywhere outside the repo
- `[!]` Gemini request schema may not fit the current top-level `model` assumption
- `[!]` JSON merge pinning may be too naive for Gemini request bodies
- `[!]` API Explorer streaming parser is explicitly split between OpenAI SSE and Ollama JSON-lines today; Gemini may require a third path
- `[!]` website and README copy must avoid claiming “fully interchangeable provider APIs” if Gemini support is narrower

---

## Suggested Developer Notes Section

Use this section during implementation.

### Notes

- 

### Deviations From Plan

- 

### Follow-Up Work

- 

### Final Outcome Summary

- 

