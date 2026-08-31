# Conductor Telemetry Guide

Conductor is instrumented end-to-end with [OpenTelemetry](https://opentelemetry.io/). This
document describes what telemetry Conductor emits, how it is grouped, what each signal
represents, which backend systems can consume it, how to reach those systems, and how to wire
the data into your own environment.

- [Architecture](#architecture)
- [Enabling telemetry](#enabling-telemetry)
- [Signals by subsystem](#signals-by-subsystem)
  - [HTTP server](#http-server)
  - [Inference proxy](#inference-proxy)
  - [Routing & load balancing](#routing--load-balancing)
  - [Model load (control plane)](#model-load-control-plane)
  - [Database](#database)
  - [Health & endpoints](#health--endpoints)
  - [Process & runtime](#process--runtime)
- [Traces](#traces)
- [The bundled observability stack](#the-bundled-observability-stack)
- [Grafana dashboards](#grafana-dashboards)
- [Integrating with your environment](#integrating-with-your-environment)
- [In-app operational metrics endpoint](#in-app-operational-metrics-endpoint)

---

## Architecture

Conductor follows a decoupled telemetry model:

- **Emission rides the .NET base class library.** Application and library code emit through
  `System.Diagnostics.Metrics.Meter` and `System.Diagnostics.ActivitySource` only. The core
  library (`Conductor.Core`) takes **no dependency on OpenTelemetry** — when nothing is
  listening, every measurement is a cheap no-op. All instruments and activity sources are
  declared centrally in `Conductor.Core/Telemetry/ConductorTelemetry.cs`.
- **The server owns the pipeline.** At startup `Conductor.Server` builds an OpenTelemetry
  `MeterProvider` and `TracerProvider` (`Conductor.Server/Telemetry/ConductorTelemetryHost.cs`)
  that subscribe to the meter/activity-source **names** exposed by the core, apply histogram
  bucket views, register process/runtime gauges, and export via OTLP (and optionally an
  in-process Prometheus scrape endpoint).

Emitters and the exporter meet only at string names, so instrumentation is completely isolated
from the export configuration.

**Signal flow:** Conductor → OTLP → OpenTelemetry Collector → { Prometheus (metrics), Tempo
(traces), Loki (logs) } → Grafana.

### Naming conventions

Instruments are declared with dot-namespaced OpenTelemetry names (for example
`conductor.inference.request.duration`). When exported to Prometheus (through the collector or
the in-process exporter), names are converted to Prometheus conventions:

- dots become underscores;
- monotonic counters gain a `_total` suffix;
- the unit is appended to histograms/gauges (`s` → `_seconds`, `By` → `_bytes`).

So `conductor.inference.request.duration` (unit `s`) becomes
`conductor_inference_request_duration_seconds` with the usual `_bucket` / `_sum` / `_count`
series. **All names in the tables below are the final Prometheus names** — use them verbatim in
PromQL.

---

## Enabling telemetry

Telemetry is **off by default**. Configure it with the `OpenTelemetry` block in `conductor.json`:

```json
"OpenTelemetry": {
  "Enabled": true,
  "ServiceName": "conductor",
  "ServiceInstanceId": null,
  "OtlpEnabled": true,
  "OtlpEndpoint": "http://otel-collector:4317",
  "Protocol": "Grpc",
  "OtlpHeaders": null,
  "OtlpTimeoutMs": 10000,
  "MetricExportIntervalMs": 15000,
  "TracesSamplingRatio": 1.0,
  "IncludeRuntimeInstrumentation": true,
  "PrometheusEnabled": false,
  "PrometheusHostname": "localhost",
  "PrometheusPort": 9464,
  "PrometheusPath": "/metrics"
}
```

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `false` | Master switch. When false, no pipeline is built. |
| `ServiceName` | `conductor` | `service.name` on the telemetry resource. |
| `ServiceInstanceId` | *(generated)* | `service.instance.id`; a GUID is generated when unset. |
| `OtlpEnabled` | `true` | Push metrics/traces to an OTLP collector. |
| `OtlpEndpoint` | `http://localhost:4317` | Collector endpoint. Use port `4318` for HTTP. |
| `Protocol` | `Grpc` | `Grpc` or `HttpProtobuf`. |
| `OtlpHeaders` | `null` | Extra OTLP headers as `key1=value1,key2=value2` (e.g. an auth token). |
| `OtlpTimeoutMs` | `10000` | OTLP export timeout (1000–120000). |
| `MetricExportIntervalMs` | `15000` | Metric push interval (1000–300000). |
| `TracesSamplingRatio` | `1.0` | Parent-based trace sampling ratio (0.0–1.0). |
| `IncludeRuntimeInstrumentation` | `true` | Include .NET runtime metrics (GC, JIT, threads). |
| `PrometheusEnabled` | `false` | Serve an in-process Prometheus scrape endpoint. |
| `PrometheusHostname` / `PrometheusPort` / `PrometheusPath` | `localhost` / `9464` / `/metrics` | In-process scrape binding. |

### Environment variable overrides

The standard OTLP environment variables take precedence over `conductor.json` and are the
easiest way to configure containers:

| Variable | Example |
| --- | --- |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://otel-collector:4317` |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` or `http/protobuf` |
| `OTEL_EXPORTER_OTLP_HEADERS` | `Authorization=Bearer <token>` |

Telemetry failures never abort startup — if the pipeline cannot be built, Conductor logs a
warning and continues with instrumentation disabled.

---

## Signals by subsystem

Metrics are grouped by instrumentation scope (meter). Every metric also carries the resource
labels `service_name` and `service_instance_id`.

### HTTP server

Scope `Conductor.Http`. Covers every request handled by the web server (management API,
health, and proxy traffic).

| Metric | Type | Unit | Labels | Represents |
| --- | --- | --- | --- | --- |
| `conductor_http_server_request_duration_seconds` | histogram | s | `http_method`, `http_status_code`, `status_class`, `route` | Per-request latency; its `_count` is the request counter. |
| `conductor_http_server_active_requests` | gauge | — | `http_method` | Requests currently in flight. |

`route` is a low-cardinality class (e.g. `/health`, `/v1.0/tenants`, `proxy`) so per-entity IDs
do not explode cardinality. `status_class` is `2xx`/`4xx`/`5xx`/etc.

### Inference proxy

Scope `Conductor.Inference`. Covers the OpenAI/Ollama/Gemini-compatible proxy path.

| Metric | Type | Unit | Labels | Represents |
| --- | --- | --- | --- | --- |
| `conductor_inference_requests_total` | counter | — | `api_family`, `vmr`, `http_status_code`, `streaming` | Proxied inference requests. |
| `conductor_inference_request_duration_seconds` | histogram | s | `api_family`, `vmr`, `http_status_code`, `streaming` | End-to-end proxied request duration. |
| `conductor_inference_first_token_duration_seconds` | histogram | s | same | Time to first token / first response byte (streaming). |
| `conductor_inference_upstream_errors_total` | counter | — | `api_family`, `vmr`, `exception_type` | Upstream call failures. |

`api_family` is `OpenAI` / `Ollama` / `Gemini` / `Management`; `vmr` is the virtual model runner
name; `streaming` is `true`/`false`.

### Routing & load balancing

Scope `Conductor.Routing`.

| Metric | Type | Unit | Labels | Represents |
| --- | --- | --- | --- | --- |
| `conductor_routing_decisions_total` | counter | — | `api_family`, `outcome`, `strategy`, `vmr` | Routing decisions evaluated. |
| `conductor_routing_decision_duration_seconds` | histogram | s | `api_family`, `outcome`, `strategy`, `vmr` | Time spent evaluating a routing decision. |
| `conductor_routing_denials_total` | counter | — | `api_family`, `reason`, `vmr` | Requests denied before forwarding. |

`outcome` is `Routed`/`Denied`; `strategy` is the load-balancing mode; `reason` is the denial
reason code (e.g. `EndpointAtCapacity`).

### Model load (control plane)

Scope `Conductor.ModelLoad`.

| Metric | Type | Unit | Labels | Represents |
| --- | --- | --- | --- | --- |
| `conductor_model_load_requests_total` | counter | — | `target_type`, `success`, `outcome` | Control-plane model-load requests. |
| `conductor_model_load_request_duration_seconds` | histogram | s | `target_type`/`api_family`, `success`, `outcome` | Model-load duration. |
| `conductor_model_load_endpoint_attempts_total` | counter | — | `api_family`, `success`, `outcome` | Per-endpoint load attempts. |

### Database

Scope `Conductor.Database`. Instrumented once at the driver chokepoint, so every provider
(SQLite, PostgreSQL, SQL Server, MySQL) and every query is covered.

| Metric | Type | Unit | Labels | Represents |
| --- | --- | --- | --- | --- |
| `conductor_db_client_operations_total` | counter | — | `db_system`, `db_operation` | Database operations executed. |
| `conductor_db_client_operation_duration_seconds` | histogram | s | `db_system`, `db_operation` | Query latency. |
| `conductor_db_client_errors_total` | counter | — | `db_system`, `db_operation` | Operations that threw. |

`db_system` is `sqlite`/`postgresql`/`mssql`/`mysql`; `db_operation` is the SQL verb
(`select`/`insert`/`update`/`delete`/…).

### QoS & queueing

Two sources. Conductor's own admission-boundary instruments live on scope `Conductor.Qos`; the
embedded [QoSKit](https://github.com/jchristn/qoskit) library emits per-class queue internals on a
meter and activity source named `QoSKit`, both of which Conductor subscribes so they export through
the same pipeline.

| Metric | Type | Unit | Labels | Represents |
| --- | --- | --- | --- | --- |
| `conductor_qos_admissions_total` | counter | — | `vmr`, `qos_class`, `outcome` | Admission decisions (`admitted`/`rejected`/`timed_out`/`aborted`). |
| `conductor_qos_rejections_total` | counter | — | `vmr`, `reason` | Requests turned away (`queue_full`/`total_depth`/`wait_timeout`/`aborted`). |
| `conductor_qos_queue_wait_duration_seconds` | histogram | s | `vmr`, `qos_class` | Time a request waited before admission. |
| `conductor_qos_queue_depth` | gauge | — | `vmr` | Requests currently parked in QoS queues. |
| `qoskit_queue_enqueued_total` / `_dequeued_total` | counter | — | `queue_name`, `queue_type`, `queue_class` | Admitted / serviced items per class. |
| `qoskit_queue_dropped_total` | counter | — | `queue_class`, `drop_reason` | Drops, split by reason (`newest`/`oldest`/`unknown_class`/`unroutable`). |
| `qoskit_queue_wait_duration_milliseconds` | histogram | ms | `queue_name`, `queue_class` | Per-class wait time (millisecond buckets applied as a view). |
| `qoskit_policer_conformed_total` / `_exceeded_total` | counter | — | `queue_class` | LLQ token-bucket conform vs. throttle. |
| `qoskit_queue_capacity` / `qoskit_queue_peak_depth` / `qoskit_queue_resident_bytes` | gauge | — | `queue_name` | Configured limit, high-water mark, resident cost (pull-based). |

`qos_class` is a closed set defined by the profile; on a weighted-fair node with dynamic flows the
profile can drop the per-class tag to bound cardinality. QoS metrics reach Prometheus by the same two
paths as the rest of Conductor (OTLP → collector `:8889`, or the in-process scrape endpoint); nothing
extra is required. They are provisioned in the **Conductor — QoS & Queueing** Grafana folder.

### Health & endpoints

Scope `Conductor.Health`. Observable gauges sampled from the live health-check state.

| Metric | Type | Represents |
| --- | --- | --- |
| `conductor_health_endpoints_healthy` | gauge | Endpoints currently healthy. |
| `conductor_health_endpoints_unhealthy` | gauge | Endpoints currently unhealthy. |
| `conductor_health_endpoints_total` | gauge | Total monitored endpoints. |
| `conductor_health_inflight_requests` | gauge | In-flight proxied requests across all endpoints. |

### Process & runtime

Scope `Conductor.Process`, plus the OpenTelemetry .NET runtime instrumentation (when
`IncludeRuntimeInstrumentation` is true).

| Metric | Type | Unit | Represents |
| --- | --- | --- | --- |
| `conductor_process_memory_usage_bytes` | gauge | By | Process working set. |
| `conductor_process_uptime_seconds` | gauge | s | Process uptime. |
| `conductor_process_thread_count` | gauge | — | OS thread count. |
| `process_runtime_dotnet_*` | various | — | GC, heap, thread-pool, and exception metrics from the .NET runtime. |

### Histogram buckets

Latency histograms use explicit, subsystem-appropriate bucket boundaries (applied as views so
they are consistent across exporters):

| Preset | Range | Used by |
| --- | --- | --- |
| Default | 5 ms – 10 s | HTTP request duration |
| Fast | 100 µs – 1 s | routing decision, database operation duration |
| Network | 10 ms – 2 min | inference duration, first-token, model-load duration |

---

## Traces

Distributed traces focus on the performance-critical request path. Spans are emitted by these
activity sources and exported over OTLP to Tempo.

| Span | Kind | Source | Key attributes |
| --- | --- | --- | --- |
| `inference.proxy` | Server | `Conductor.Inference` | `conductor.vmr`, `conductor.api_family`, `conductor.model`, `http.request.method`, `http.response.status_code`, `conductor.streaming` |
| `inference.forward` | Client | `Conductor.Inference` | `server.address`, `server.port`, `conductor.endpoint_id`, `http.response.status_code` |
| `routing.evaluate` | Internal | `Conductor.Routing` | `conductor.outcome`, `conductor.endpoint_id`, `conductor.denial_reason` |
| `inference.qos.admit` | Internal | `Conductor.Inference` | `conductor.qos_class`, `conductor.qos_outcome` |
| `queue.enqueue` / `queue.dequeue` / `link.move` | Internal | `QoSKit` | `queue.name`, `queue.class`, `qoskit.outcome`, `qoskit.wait_ms` |
| `db <operation>` | Client | `Conductor.Database` | `db.system`, `db.operation` |

Within a proxied request the spans nest: `inference.proxy` → `routing.evaluate`, `inference.qos.admit`,
and `inference.forward`. The `inference.qos.admit` segment shows the time a request spent waiting in the
QoS queue, and QoSKit's `link.move` spans decompose a multi-node hierarchy underneath it. Database spans nest under whatever operation triggers them (and appear as
standalone traces for management-plane calls). Sampling is parent-based and controlled by
`TracesSamplingRatio`.

---

## The bundled observability stack

`docker/compose.yaml` provisions a complete, ready-to-use stack. Bring it up with:

```bash
cd docker
docker compose up -d
```

| Service | Purpose | Host URL |
| --- | --- | --- |
| OpenTelemetry Collector | Receives OTLP; fans out to Prometheus/Tempo/Loki; tails Conductor logs to Loki | `:4317` (gRPC), `:4318` (HTTP) |
| Prometheus | Metrics storage; scrapes the collector at `:8889` | `http://localhost:9090` |
| Tempo | Trace storage | `http://localhost:3200` |
| Loki | Log aggregation | `http://localhost:3100` |
| Grafana | Dashboards and exploration | `http://localhost:3000` |

Grafana is configured with anonymous Admin access (no login). Prometheus, Tempo, and Loki run
without authentication in this local stack. Config files live under `docker/otel`,
`docker/prometheus`, `docker/tempo`, and `docker/grafana`.

Conductor's Compose service is pre-wired to push OTLP to the collector
(`OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317`) with telemetry enabled in
`docker/conductor.json`. Logs are collected by the collector's filelog receiver tailing the
mounted `./logs` directory. These service links (with URLs and credentials) are also surfaced as
cards on the dashboard's **Dashboard** page.

> The stack requires a Conductor server image built from this source. If you run an older
> published image tag, rebuild with `build-server.bat`.

---

## Grafana dashboards

Dashboards are provisioned automatically under a single top-level **Conductor** folder, organized
into per-subsystem subfolders (via `foldersFromFilesStructure` with Grafana 11 nested folders), so
they are grouped rather than piled into a single folder:

| Subfolder (under Conductor) | Dashboard | Highlights |
| --- | --- | --- |
| Database | Conductor - Database | Query rate & latency by operation and system, errors |
| HTTP and API | Conductor - HTTP & API | Request rate, latency percentiles, status classes, active requests, by route |
| Inference | Conductor - Inference & Proxy | Request rate & latency by API family / VMR, time-to-first-token, upstream errors, by status |
| QoS and Queueing | Conductor - QoS & Queueing | Admissions & rejections, per-class wait duration, queue depth, admit spans |
| Routing and Load Balancing | Conductor - Routing & Load Balancing | Decisions by outcome & strategy, denials by reason, decision latency, model-load |
| Runtime | Conductor - Runtime & Process | Memory, threads, uptime, GC |
| Health and Endpoints | Conductor - Health & Endpoints | Healthy/unhealthy endpoints, in-flight requests |

Datasources are provisioned with cross-correlation: Prometheus exemplars link to Tempo, Tempo
traces link to Loki logs, and Loki log lines link back to Tempo traces.

The dashboard JSON lives in `docker/grafana/dashboards/Conductor/<subsystem>/*.json` and can be
imported into any Grafana instance.

---

## Integrating with your environment

You do not have to use the bundled stack. Because Conductor speaks OTLP, it drops into any
OpenTelemetry-compatible pipeline.

**1. Point at your own collector or backend.** Set the endpoint (and headers for auth) to your
collector or a vendor's OTLP ingest — Grafana Cloud, Honeycomb, New Relic, Datadog (OTLP),
Elastic, etc.

```json
"OpenTelemetry": {
  "Enabled": true,
  "OtlpEnabled": true,
  "OtlpEndpoint": "https://otlp.your-vendor.example:4318",
  "Protocol": "HttpProtobuf",
  "OtlpHeaders": "Authorization=Bearer <ingest-token>"
}
```

or via environment variables:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp.your-vendor.example:4318
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
OTEL_EXPORTER_OTLP_HEADERS=Authorization=Bearer <ingest-token>
```

**2. Scrape metrics directly with Prometheus.** Enable the in-process exporter and scrape it —
no collector required for metrics:

```json
"OpenTelemetry": { "Enabled": true, "PrometheusEnabled": true, "PrometheusPort": 9464 }
```

```yaml
scrape_configs:
  - job_name: conductor
    metrics_path: /metrics
    static_configs:
      - targets: ["conductor-host:9464"]
```

You can run OTLP push and in-process Prometheus scrape simultaneously.

**3. Bring your own Grafana.** Import the dashboard JSON from `docker/grafana/dashboards` and
point them at your Prometheus/Tempo/Loki. The PromQL uses the stable metric names in this
document, so the dashboards work against any Prometheus that has the Conductor metrics.

**4. Tune volume.** Lower `TracesSamplingRatio` (e.g. `0.1`) to reduce trace volume in
production; raise `MetricExportIntervalMs` to reduce metric export frequency.

**Cardinality note.** Metric labels are deliberately low-cardinality (coarse routes, API family,
VMR name, SQL verb). Avoid adding high-cardinality labels (raw URLs, per-request IDs) if you
extend the instrumentation.

---

## In-app operational metrics endpoint

Independent of the OpenTelemetry pipeline, Conductor also exposes its built-in operational
metrics snapshot (unchanged from earlier releases):

- `GET /v1.0/observability/metrics` — Prometheus text exposition (`conductor_*` operational
  counters/histograms), bearer-authenticated.
- `GET /v1.0/observability/metrics/summary` — JSON snapshot consumed by the dashboard's
  Operational Signals panel.

This endpoint is useful for a quick in-app view without standing up the full stack; the
OpenTelemetry pipeline described above is the path for durable storage, dashboards, and
distributed tracing.
