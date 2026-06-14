# Changelog

## Unreleased

- Added tenant-scoped model access policies with VMR attachment, enforce/monitor/disabled modes, proxy credential attribution, denied/would-deny request history, analytics counters, audit logging, list-model filtering or synthesis, backup/restore support, SDK helpers, Postman examples, and documented rollout semantics.
- Added `ACCESS_POLICIES.md`, a user-facing model access policy authoring guide with dashboard/API workflow and real-world policy examples.
- Coalesced duplicate model runner endpoint health checks so endpoints sharing the same effective health-check URL reuse one upstream probe while retaining per-endpoint health state.
- Added Ollama endpoint model management APIs and dashboard `Manage Models` action for listing local models, pulling a model, and deleting a model from an Ollama-type runner endpoint.
- Added tenant-admin model load and verification APIs for model runner endpoints and virtual model runners, with dashboard actions, SDK helpers, Postman examples, provider-specific outcome semantics, and Prometheus model-load metrics.
- Added first-class routing explanation via VMR explain-routing APIs, persisted request-history routing decisions, and matching dashboard inspection UX.
- Added shared draft validation routes for endpoints, model definitions, model configurations, load-balancing policies, and VMRs, plus VMR effective-configuration preview APIs.
- Expanded request history into a richer operational ledger with new indexed routing fields, additional summary/search filters, metadata-vs-body retention controls, and body scrubbing during cleanup.
- Added request analytics telemetry with trace IDs, provider request IDs, token counts, token throughput, normalized stage events, startup migrations, aggregate analytics APIs, and dashboard analytics drill-down.
- Added a dashboard Request Analytics view with range filters, volume/latency charting, stage breakdowns, endpoint summaries, slowest-request drill-down, and per-request timing bars in request history detail.
- Added operational metrics export and JSON summaries for request volume, denials, session affinity, routing latency, total duration, first-token latency, saturation, and telemetry freshness failures.
- Added explicit endpoint drain, resume, and quarantine actions with service-state persistence, routing behavior, health visibility, dashboard controls, and Docker factory schema updates.
- Added JavaScript and Python SDK helpers for validation, explanation, request-history, request-analytics, endpoint service-state actions, and observability routes.
- Reworked test plumbing onto Touchstone NuGet packages with a shared `Test.Shared` suite and thin `Test.Automated`, `Test.Xunit`, and `Test.Nunit` hosts.
- Expanded automated coverage with additional positive and negative controller and session-affinity test cases.
- Fixed model runner endpoint deletion to remove endpoint references from attached virtual model runners before persistence.
- Added request history time-to-first-token/byte capture as `FirstTokenTimeMs`.
- Added database startup migrations for the new request history TTFT column across SQLite, SQL Server, MySQL, and PostgreSQL.
- Updated the dashboard request history table and detail view to display TTFT.
- Updated the Docker factory SQLite schema to include request history TTFT storage.
- Updated Docker Compose to build server and dashboard images from local build contexts instead of pulling named images.
- Updated Docker Compose to run PostgreSQL by default with a persisted data volume, an idempotent init container, and dashboard runtime server URL configuration from `CONDUCTOR_SERVER_URL`.
- Replaced dashboard label/tag JSON textareas with structured row editors for `Labels` and `Tags` on all create/edit modals that expose those fields.
