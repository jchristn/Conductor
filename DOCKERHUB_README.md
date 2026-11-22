<img src="https://raw.githubusercontent.com/jchristn/Conductor/main/assets/icon-dark.png" alt="Conductor" width="128" height="128">

# Conductor

Conductor puts a single, controllable front door in front of all of your model runners. You register your backends (Ollama, Ollama Cloud, OpenAI, OpenAI-compatible servers, vLLM, Gemini, Anthropic, hosted TypeSafe Jev decision models, or a local Ollaya runtime), and Conductor virtualizes them into stable endpoints that speak the OpenAI, vLLM, Gemini, Ollama, Anthropic, and TypeSafe System One APIs your clients already use. Claude Code and the official Anthropic SDKs can run through an Anthropic virtual model runner with a Conductor credential. Load balancing, health checking, session affinity, access policies, and request analytics happen in between, without your applications having to know which backend actually served a request.

This page covers running Conductor from the published Docker images. The full source, SDKs, and reference documentation live at [github.com/jchristn/Conductor](https://github.com/jchristn/Conductor).

## Images

| Image | Purpose | Default Port |
|-------|---------|--------------|
| [`jchristn77/conductor-server`](https://hub.docker.com/r/jchristn77/conductor-server) | REST API, inference proxy, routing engine | 9000 |
| [`jchristn77/conductor-dashboard`](https://hub.docker.com/r/jchristn77/conductor-dashboard) | React management UI | 9100 |

Both images publish versioned tags (the current published tag is `v1.1.0`) alongside `latest`.

## What you can do with it

The point of Conductor is to stop wiring individual model backends directly into applications. A virtual model runner (VMR) is the unit clients talk to: it bundles a set of endpoints, optional endpoint groups, and model configurations behind one address, then decides at request time where traffic should go.

That decision is where most of the interesting behavior lives. You can spread load across endpoints with round-robin, random, first-available, least-recently-used, or adaptive strategies, and weight the distribution when some hardware is faster than others. You can pin a client to the backend it started on (by IP, API key, or a header you choose), so a long conversation does not bounce between machines and pay the model-swap cost on every turn. When an endpoint stops answering health checks, it drops out of rotation on its own and rejoins when it recovers; you can also drain or quarantine one deliberately while keeping its health visible.

Under load you decide who waits and who goes first. Each VMR can be linked to a **QoS profile** that classifies incoming traffic (by a custom header, a specific credential, the model, a request-body attribute, and more) and queues it with the scheduling discipline you choose (FIFO, priority, weighted-fair, low-latency, or weighted round robin), releasing requests in order as endpoint capacity frees instead of rejecting them outright. Every tenant is seeded with a default FIFO profile and a catalog of standard traffic classes, and the per-class queue metrics and traces flow into the same Grafana stack. See `QOS_OVERVIEW.md` in the repository.

Access is governed rather than assumed. Tenants isolate data, users and credentials authenticate against the proxy, and model access policies decide, per credential, user, label, model, action, or VMR, what is allowed, denied, or merely monitored. When you need to guarantee capacity for a launch or a demo, VMR reservations carve out exclusive windows for specific users without disturbing on-demand traffic the rest of the time.

Nothing about a route has to be a guess before you save it. Preflight validation checks endpoints, definitions, configurations, policies, and VMRs; effective-configuration preview resolves exactly which endpoints, permissions, policy attachment, and pinned parameters a VMR will use; and explainable routing lets you simulate a representative request and watch candidates get eliminated with the evidence that drove each decision.

## Architecture

Conductor ships as two containers over a database. The **server** hosts the management REST API and the inference proxy in one process. Management calls create and configure resources; everything else falls through to the proxy, which resolves the target VMR, runs the routing decision, forwards the request to the chosen backend, and streams the response back, including token-by-token SSE and chunked responses, with time-to-first-token captured along the way. The **dashboard** is a static React app that talks to the server's API and gives you a full UI for every entity plus live health.

State lives in a relational database. PostgreSQL is the default in the Docker setup and runs as its own container with a persisted volume; SQLite, SQL Server, and MySQL are also supported, so a laptop can run entirely on a single SQLite file while production runs on Postgres.

Observability is built in rather than bolted on. The server emits OpenTelemetry metrics and distributed traces across its critical paths: HTTP, the inference proxy, routing and load balancing, model loading, the database layer, endpoint health, and process runtime. The repository's Compose file ships a full stack (OpenTelemetry Collector, Prometheus, Tempo, Loki, and Grafana) with datasources and per-subsystem dashboards already provisioned, and you can point the same OTLP export at your own collector or vendor instead.

## Getting started

The complete stack (server, dashboard, PostgreSQL, schema init, and the observability services) is defined in `docker/compose.yaml` in the repository. Clone it and bring everything up:

```bash
git clone https://github.com/jchristn/Conductor.git
cd Conductor/docker
docker compose up -d
```

The server comes up at `http://127.0.0.1:9000`, the dashboard at `http://127.0.0.1:9100`, and Grafana at `http://127.0.0.1:3000` (`127.0.0.1` rather than `localhost`, because some clients try IPv6 `::1` first for `localhost` and stall). Sign in with the default credentials below. They are fixed, well-known values, not generated secrets, and they are the same on every installation:

| What | Default | Where it comes from |
| --- | --- | --- |
| Administrator (admin dashboard) | `admin@conductor` / `password` | Created on first run |
| Tenant user in tenant `default` (global admin) | `admin@conductor` / `password` | Created on first run |
| Admin API key (`x-admin-apikey` header) | `conductoradmin` | `AdminApiKeys` in `docker/conductor.json` (also the built-in default when `AdminApiKeys` is omitted) |
| Tenant API bearer token | `factory_default_bearer_token_0000000000000000000000000000000000` | Seeded by `docker/factory/schema.sql` in the Compose setup; a standalone server generates a random token and prints it once to the console on first run |
| PostgreSQL | `conductor` / `conductor` | `docker/compose.yaml` and `docker/conductor.json` |

> **Warning:** anyone who can reach the server can sign in with these defaults. Change the administrator and user passwords, replace the admin API key, and delete or rotate the default bearer token before exposing the server beyond your own machine. See [Production Hardening](https://github.com/jchristn/Conductor#production-hardening) in the repository README for the full checklist.

If you only want the server and are content with a single-file SQLite database, run the image on its own with a mounted configuration. Create `conductor.json`:

```json
{
  "Webserver": { "Hostname": "*", "Port": 9000, "Ssl": false },
  "Database": { "Type": "Sqlite", "Filename": "/app/data/conductor.db" }
}
```

Then start the container:

```bash
docker run -d --name conductor \
  -p 9000:9000 \
  -v "$(pwd)/conductor.json:/app/conductor.json:ro" \
  -v "$(pwd)/data:/app/data" \
  jchristn77/conductor-server:latest
```

On first run the server creates the `admin@conductor` / `password` administrator and tenant user and prints a randomly generated tenant bearer token to its console (`docker logs conductor`); the admin API key is `conductoradmin` because this configuration does not set `AdminApiKeys`. Change all of them as described above, then either drive the API directly or run the dashboard container against it. The bearer token belongs to a global-admin user in tenant `default`. Its writes act on `default` unless they name another tenant (`"TenantId"` in create and update bodies, `?tenantId=` on other routes), and it can call only VMRs in `default`. The admin API key has no tenant, so writes made with it must name one, and creating, updating, or deleting tenants requires it (or the admin headers).

## Ports

The Compose stack exposes the following. When you run containers individually, publish only what you need.

| Service | Port | Notes |
|---------|------|-------|
| Conductor server | 9000 | REST API and inference proxy |
| Conductor dashboard | 9100 | Management UI |
| PostgreSQL | 5432 | Default database |
| Grafana | 3000 | Dashboards; anonymous admin access |
| Prometheus | 9090 | Metrics |
| Tempo | 3200 | Traces |
| Loki | 3100 | Logs |
| OpenTelemetry Collector | 4317 / 4318 | OTLP gRPC / HTTP ingest |

## Configuration

The server reads a JSON configuration file (`conductor.json`, mounted at `/app/conductor.json`). If the file is absent it is created from defaults on first boot. The blocks that matter most are the web server binding, the database, logging, request history, model access control, and OpenTelemetry.

The database block selects the provider. PostgreSQL is the Docker default:

```json
{
  "Database": {
    "Type": "PostgreSql",
    "Hostname": "conductor-postgres",
    "Port": 5432,
    "DatabaseName": "conductor",
    "Username": "conductor",
    "Password": "conductor",
    "RequireEncryption": false
  }
}
```

Switching to SQLite for local work is a two-line change:

```json
{ "Database": { "Type": "Sqlite", "Filename": "/app/data/conductor.db" } }
```

Telemetry is off until you enable it. Turn it on and point it at a collector through the `OpenTelemetry` block, or override the endpoint with the standard `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL` environment variables:

```json
{
  "OpenTelemetry": {
    "Enabled": true,
    "OtlpEndpoint": "http://otel-collector:4317",
    "Protocol": "Grpc"
  }
}
```

## Provider types

Conductor supports nine explicit endpoint types. Six are client-facing, so a virtual model runner (VMR) can expose them as its API; the other three are endpoint-only types that you attach to a compatible VMR. Defaults are applied only from the type you select, never guessed from a hostname.

| Type | Wire API | Upstream auth | Client-facing | Notes |
|------|----------|---------------|---------------|-------|
| `Ollama` | Ollama `/api/*` | Bearer when a key is set | Yes | Local Ollama, with model management, keep-warm, and RigMonitor |
| `OpenAI` | OpenAI `/v1/*` | Bearer | Yes | Hosted OpenAI: chat, completions, embeddings, model listing |
| `Gemini` | Gemini `/v1beta/*` | `x-goog-api-key` | Yes | `models/{model}:generateContent`, streaming, embeddings, listing |
| `vLLM` | OpenAI `/v1/*` | Bearer when a key is set | Yes | Self-hosted vLLM |
| `Anthropic` | Anthropic `/v1/*` | `x-api-key` and `anthropic-version` | Yes | Messages, streaming, token counting, models |
| `TypeSafe` | System One `/v1/*` | Bearer (required) | Yes | Hosted Jev decision models |
| `Ollaya` | System One `/v1/*` | Bearer when a key is set | No (attach to a `TypeSafe` VMR) | Local decision runtime |
| `OllamaCloud` | Ollama `/api/*` | Bearer (required) | No (attach to an `Ollama` VMR) | Hosted Ollama at `ollama.com` |
| `OpenAICompatible` | OpenAI `/v1/*` | Bearer when a key is set | No (attach to an `OpenAI` VMR) | LM Studio, llama.cpp, Ollama's `/v1` API, and other compatible servers |

Each VMR type accepts a fixed set of endpoint types: `Ollama` accepts `Ollama` and `OllamaCloud`; `OpenAI` accepts `OpenAI`, `OpenAICompatible`, and `vLLM`; `vLLM`, `Gemini`, and `Anthropic` accept their own type; `TypeSafe` accepts `TypeSafe` and `Ollaya`. A VMR classifies requests by its own type, and a path outside that type's namespace returns `404`. Upstream API keys are masked in API responses and can be read back by tenant admins and global admins through an audited reveal route.

Upgrading from an earlier version? See "Upgrading to v1.1.0" in the repository README for the migration notes.

Clients authenticate with either the `Authorization: Bearer {token}` header or the `x-tenant-id` / `x-email` / `x-password` header set, and permissions run from standard users up through tenant admins to global admins. A credential can call only VMRs in its own tenant; one from another tenant gets `403`. On Anthropic VMRs a Conductor credential may also be sent in `x-api-key`, where Anthropic clients put it; it is never forwarded upstream.

## Learn more

- **Repository and full README:** [github.com/jchristn/Conductor](https://github.com/jchristn/Conductor)
- **Telemetry guide:** [TELEMETRY.md](https://github.com/jchristn/Conductor/blob/main/TELEMETRY.md)
- **REST API reference:** [REST_API.md](https://github.com/jchristn/Conductor/blob/main/REST_API.md)
- **Changelog:** [CHANGELOG.md](https://github.com/jchristn/Conductor/blob/main/CHANGELOG.md)

Conductor is released under the MIT license.
