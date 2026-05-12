# Changelog

## Unreleased

- Added request history time-to-first-token/byte capture as `FirstTokenTimeMs`.
- Added database startup migrations for the new request history TTFT column across SQLite, SQL Server, MySQL, and PostgreSQL.
- Updated the dashboard request history table and detail view to display TTFT.
- Updated the Docker factory SQLite schema to include request history TTFT storage.
- Updated Docker Compose to build server and dashboard images from local build contexts instead of pulling named images.
