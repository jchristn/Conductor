<img src="https://raw.githubusercontent.com/jchristn/Conductor/main/assets/icon-dark.png" alt="Conductor" width="128" height="128">

# Conductor

Conductor puts a single, controllable front door in front of all of your model runners. You register your backends — OpenAI, vLLM, Gemini, or Ollama — and Conductor virtualizes them into stable endpoints that speak the OpenAI, vLLM, Gemini, and Ollama APIs your clients already use. Load balancing, health checking, session affinity, access policies, and request analytics happen in between, without your applications having to know which backend actually served a request.

> **Alpha.** Conductor is v0.5.0 and under active development. APIs and behavior can change between releases.

This page covers running Conductor from the published Docker images. The full source, SDKs, and reference documentation live at [github.com/jchristn/Conductor](https://github.com/jchristn/Conductor).

## Images

| Image | Purpose | Default Port |
|-------|---------|--------------|
| [`jchristn77/conductor-server`](https://hub.docker.com/r/jchristn77/conductor-server) | REST API, inference proxy, routing engine | 9000 |
| [`jchristn77/conductor-dashboard`](https://hub.docker.com/r/jchristn77/conductor-dashboard) | React management UI | 9100 |

Both images publish versioned tags (the current published tag is `v0.5.0`) alongside `latest`.

## What you can do with it

The point of Conductor is to stop wiring individual model backends directly into applications. A virtual model runner (VMR) is the unit clients talk to: it bundles a set of endpoints, optional endpoint groups, and model configurations behind one address, then decides at request time where traffic should go.

That decision is where most of the interesting behavior lives. You can spread load across endpoints with round-robin, random, first-available, least-recently-used, or adaptive strategies, and weight the distribution when some hardware is faster than others. You can pin a client to the backend it started on — by IP, API key, or a header you choose — so a long conversation does not bounce between machines and pay the model-swap cost on every turn. When an endpoint stops answering health checks, it drops out of rotation on its own and rejoins when it recovers; you can also drain or quarantine one deliberately while keeping its health visible.

Under load you decide who waits and who goes first. Each VMR can be linked to a **QoS profile** that classifies incoming traffic — by a custom header, a specific credential, the model, a request-body attribute, and more — and queues it with the scheduling discipline you choose (FIFO, priority, weighted-fair, low-latency, or weighted round robin), releasing requests in order as endpoint capacity frees instead of rejecting them outright. Every tenant is seeded with a default FIFO profile and a catalog of standard traffic classes, and the per-class queue metrics and traces flow into the same Grafana stack. See `QOS_OVERVIEW.md` in the repository.

Access is governed rather than assumed. Tenants isolate data, users and credentials authenticate against the proxy, and model access policies decide — per credential, user, label, model, action, or VMR — what is allowed, denied, or merely monitored. When you need to guarantee capacity for a launch or a demo, VMR reservations carve out exclusive windows for specific users without disturbing on-demand traffic the rest of the time.

Nothing about a route has to be a guess before you save it. Preflight validation checks endpoints, definitions, configurations, policies, and VMRs; effective-configuration preview resolves exactly which endpoints, permissions, policy attachment, and pinned parameters a VMR will use; and explainable routing lets you simulate a representative request and watch candidates get eliminated with the evidence that drove each decision.

## Architecture

Conductor ships as two containers over a database. The **server** hosts the management REST API and the inference proxy in one process. Management calls create and configure resources; everything else falls through to the proxy, which resolves the target VMR, runs the routing decision, forwards the request to the chosen backend, and streams the response back — including token-by-token SSE and chunked responses, with time-to-first-token captured along the way. The **dashboard** is a static React app that talks to the server's API and gives you a full UI for every entity plus live health.

State lives in a relational database. PostgreSQL is the default in the Docker setup and runs as its own container with a persisted volume; SQLite, SQL Server, and MySQL are also supported, so a laptop can run entirely on a single SQLite file while production runs on Postgres.

Observability is built in rather than bolted on. The server emits OpenTelemetry metrics and distributed traces across its critical paths — HTTP, the inference proxy, routing and load balancing, model loading, the database layer, endpoint health, and process runtime. The repository's Compose file ships a full stack (OpenTelemetry Collector, Prometheus, Tempo, Loki, and Grafana) with datasources and per-subsystem dashboards already provisioned, and you can point the same OTLP export at your own collector or vendor instead.

## Getting started

The complete stack — server, dashboard, PostgreSQL, schema init, and the observability services — is defined in `docker/compose.yaml` in the repository. Clone it and bring everything up:

```bash
git clone https://github.com/jchristn/Conductor.git
cd Conductor/docker
docker compose up -d
```

The server comes up at `http://localhost:9000`, the dashboard at `http://localhost:9100`, and Grafana at `http://localhost:3000`. On first run the server prints a set of default administrator, tenant, user, and API-key credentials to its logs — save them, because they are not shown again:

```bash
docker compose logs conductor
```

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

Watch the logs for the first-run credentials, then either drive the API directly or run the dashboard container against it.

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

Conductor proxies four backend families in both the server and the dashboard.

| Provider | Runner type in UI | Proxied API shape |
|----------|-------------------|-------------------|
| OpenAI | `OpenAI` | OpenAI REST API — chat, embeddings, model listing |
| vLLM | `vLLM` | OpenAI-compatible REST API |
| Gemini | `Gemini` | `models/{model}:generateContent`, streaming, embeddings, listing |
| Ollama | `Ollama` | `/api/generate`, `/api/chat`, embeddings |

Clients authenticate with either the `Authorization: Bearer {token}` header or the `x-tenant-id` / `x-email` / `x-password` header set, and permissions run from standard users up through tenant admins to global admins.

## Learn more

- **Repository and full README:** [github.com/jchristn/Conductor](https://github.com/jchristn/Conductor)
- **Telemetry guide:** [TELEMETRY.md](https://github.com/jchristn/Conductor/blob/main/TELEMETRY.md)
- **REST API reference:** [REST_API.md](https://github.com/jchristn/Conductor/blob/main/REST_API.md)
- **Changelog:** [CHANGELOG.md](https://github.com/jchristn/Conductor/blob/main/CHANGELOG.md)

Conductor is released under the MIT license.
