# Changelog

## Unreleased

- Reworked test plumbing onto Touchstone NuGet packages with a shared `Test.Shared` suite and thin `Test.Automated`, `Test.Xunit`, and `Test.Nunit` hosts.
- Expanded automated coverage with additional positive and negative controller and session-affinity test cases.
- Fixed model runner endpoint deletion to remove endpoint references from attached virtual model runners before persistence.
- Added request history time-to-first-token/byte capture as `FirstTokenTimeMs`.
- Added database startup migrations for the new request history TTFT column across SQLite, SQL Server, MySQL, and PostgreSQL.
- Updated the dashboard request history table and detail view to display TTFT.
- Updated the Docker factory SQLite schema to include request history TTFT storage.
- Updated Docker Compose to build server and dashboard images from local build contexts instead of pulling named images.
