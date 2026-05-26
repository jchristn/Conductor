# Changelog

## Unreleased

- Added first-class routing explanation via VMR explain-routing APIs, persisted request-history routing decisions, and matching dashboard inspection UX.
- Added shared draft validation routes for endpoints, model definitions, model configurations, load-balancing policies, and VMRs, plus VMR effective-configuration preview APIs.
- Expanded request history into a richer operational ledger with new indexed routing fields, additional summary/search filters, metadata-vs-body retention controls, and body scrubbing during cleanup.
- Added operational metrics export and JSON summaries for request volume, denials, session affinity, routing latency, total duration, first-token latency, saturation, and telemetry freshness failures.
- Added explicit endpoint drain, resume, and quarantine actions with service-state persistence, routing behavior, health visibility, dashboard controls, and Docker factory schema updates.
- Added JavaScript and Python SDK helpers for validation, explanation, request-history, endpoint service-state actions, and observability routes.
- Reworked test plumbing onto Touchstone NuGet packages with a shared `Test.Shared` suite and thin `Test.Automated`, `Test.Xunit`, and `Test.Nunit` hosts.
- Expanded automated coverage with additional positive and negative controller and session-affinity test cases.
- Fixed model runner endpoint deletion to remove endpoint references from attached virtual model runners before persistence.
- Added request history time-to-first-token/byte capture as `FirstTokenTimeMs`.
- Added database startup migrations for the new request history TTFT column across SQLite, SQL Server, MySQL, and PostgreSQL.
- Updated the dashboard request history table and detail view to display TTFT.
- Updated the Docker factory SQLite schema to include request history TTFT storage.
- Updated Docker Compose to build server and dashboard images from local build contexts instead of pulling named images.
