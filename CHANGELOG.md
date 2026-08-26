# Changelog

## v0.4.0

- Set .NET project versions to `0.4.0`.
- Added end-to-end OpenTelemetry instrumentation across the critical subsystems — HTTP server, inference proxy, routing and load balancing, model load, database, endpoint health, and process/runtime — emitting metrics and distributed traces through the .NET base class library primitives with no OpenTelemetry dependency in `Conductor.Core`.
- Added an OpenTelemetry export pipeline in the server (`ConductorTelemetryHost`) with OTLP metric/trace export, an optional in-process Prometheus scrape endpoint, explicit histogram bucket views, .NET runtime instrumentation, and process gauges, driven by a new `OpenTelemetrySettings` block in `conductor.json` and the standard `OTEL_EXPORTER_OTLP_*` environment variables.
- Added a batteries-included observability stack to Docker Compose (OpenTelemetry Collector, Prometheus, Tempo, Loki, and Grafana) with provisioned datasources wired for metrics/traces/logs correlation and per-subsystem Grafana dashboards organized into folders (HTTP, Inference, Routing, Database, Runtime, Health).
- Added `TELEMETRY.md` describing the telemetry surface, metric and trace reference, the observability stack, and how to integrate the data into external environments.
- Updated .NET dependencies to their latest stable releases: `Watson` (7.1.0 to 7.1.1), `SyslogLogging` (2.2.1 to 2.2.2), and `RestWrapper` (3.2.0 to 3.3.0), plus new `OpenTelemetry` 1.18.0 packages. `Voltaic` is intentionally held at 0.6.0 pending a separate MCP server migration to its 1.0.0 contract.
- Added dashboard cards on the Dashboard page linking to the companion observability services (Grafana, Prometheus, Tempo, Loki) with their URLs and default credentials.
- Reworked table row action menus to render in a portal with viewport-aware positioning so they open above the row and are never clipped by the table's scroll container.
- Added a "Select Columns" control to every data table for choosing which columns to display.
- Added a "Duplicate" row action across the model definition, model configuration, load-balancing policy, model access policy, endpoint group, model runner endpoint, and virtual model runner tables that opens a pre-filled create form.
- Reduced the Dashboard request-history chart axis label sizes, made the Model Access Policies KPI cards more compact, widened the Backup & Restore cards to full width, and renamed the Request History "Ledger Summary" heading to "Summary".
- Fixed the draft validation routes to honor the `existingId` query parameter so updating an existing resource no longer reports a false uniqueness collision against itself (for example a virtual model runner's BasePath colliding with its own record); applied across the virtual model runner, model runner endpoint, endpoint group, model definition, model configuration, load-balancing policy, model access policy, and VMR reservation validation endpoints.
- Changed `docker/update.bat` to pull the published images (`docker compose pull`) instead of building them locally.

## v0.3.0

- Set .NET project versions to `0.3.0`.
- Updated .NET dependencies across the main solution and C# SDK to their latest stable releases, including `MySql.Data` (9.7.0 to 26.7.0), `Voltaic` (0.4.0 to 0.6.0), `Watson` (7.0.14 to 7.1.0), `SyslogLogging` (2.1.0 to 2.2.1), `Microsoft.Data.Sqlite` (10.0.9 to 10.0.11), `SQLitePCLRaw.bundle_e_sqlite3` (3.0.3 to 3.0.5), `Microsoft.NET.Test.Sdk` (18.7.0 to 18.9.0), and `xunit.runner.visualstudio` (3.1.5 to 4.0.0).
- Migrated the MCP tool registry to the Voltaic 0.6.0 handler contract, adapting Conductor tool handlers to the new `RpcParameters` request-parameter type.
- Enabled symbol package (`snupkg`) generation for the `Conductor.Core` and `Conductor.McpServer` NuGet packages.
- Updated the dashboard build tooling to Vite 8 and `@vitejs/plugin-react` 6, with React and React Router refreshed to their latest releases.
- Added `DatabaseSettings` unit tests covering defaults, guarded property normalization, port clamping, and per-provider connection-string construction, plus positive and negative `ConductorClient` SDK tests for constructor guards, authentication modes, base-URL and query-string handling, HttpClient ownership, and error surfacing.

## v0.2.0

- Set .NET project versions to `0.2.0`.
- Added `LeastRecentlyUsed` and `Adaptive` virtual model runner load-balancing modes, with route-scoped recency tracking, endpoint-group routing, traffic weights, adaptive runtime scoring, transient backoff, runtime stats management APIs, dashboard controls, SDK helpers, Postman coverage, REST documentation, and shared backend tests.
- Updated .NET package dependencies across the main solution and C# SDK, including the SQLite native bundle remediation and the MCP dependency namespace migration.
- Added tenant-scoped VMR reservations with time-window admission enforcement, user and credential participants, validation, reservation-denial logs, request-history/analytics denial attribution, dashboard management, VMR badges, backup/restore support, SDK helpers, and REST documentation.
- Added tenant-scoped model access policies with VMR attachment, enforce/monitor/disabled modes, proxy credential attribution, denied/would-deny request history, analytics counters, audit logging, list-model filtering or synthesis, backup/restore support, SDK helpers, Postman examples, and documented rollout semantics.
- Added `ACCESS_POLICIES.md`, a user-facing model access policy authoring guide with dashboard/API workflow and real-world policy examples.
- Added a tenant-scoped dashboard Analytics workspace at `/analytics` for TTFT, token usage, estimate-only cost, user/credential/model/endpoint breakdowns, and denied/rate-limited request reporting over the retained 30-day request-history window.
- Added `/v1.0/analytics` catalog, query, summary, time-series, TTFT, tokens, costs, users, and access/reliability APIs with system-admin global scope, tenant-admin forced scope, and successful-completion token/cost semantics.
- Added dedicated tenant-scoped Analytics reader access through the `analytics.read` user label/tag convention.
- Added Analytics saved-report persistence, CRUD APIs, dashboard load/save/update/delete/link controls, SDK helpers, and Postman examples.
- Added JavaScript, Python, and C# SDK helpers plus Postman examples for the first Analytics workspace API slice.
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
