# Load Balancing Policies

This document explains how to author Conductor load-balancing policies as JSON, how those policies are evaluated at runtime, and how to attach them to a Virtual Model Runner (VMR).

Primary audience: developers and sysadmins who are creating or troubleshooting non-trivial routing behavior.

## What a load-balancing policy does

A Conductor load-balancing policy is a tenant-scoped resource that:

- filters endpoint candidates by stable Conductor metric IDs
- ranks the remaining endpoints with weighted scoring
- chooses a tie-breaker for equally scored endpoints
- decides whether to fall back to the VMR's load balancing mode or fail closed

Policies are attached to a VMR with `LoadBalancingPolicyId`.

Without a policy, a VMR uses its `LoadBalancingMode` directly.

## Runtime evaluation order

When a proxied request reaches a VMR, Conductor evaluates routing in this order:

1. Conductor resolves the target VMR from the request path or host.
2. Conductor applies request-type gating, strict-model checks, and any model/configuration mutation logic required by the VMR.
3. Conductor checks session affinity before normal candidate selection.
4. Conductor builds a candidate list from the VMR's attached endpoints.
5. For new work, only endpoints that are active, not quarantined, not draining, healthy, and currently have capacity remain in the candidate pool.
6. A draining endpoint may still be reused when an existing sticky-session pin already targets it.
7. If the VMR has no active attached policy, Conductor uses the VMR's `LoadBalancingMode`.
8. If a policy is attached and active, every `Filters` rule must pass.
9. Every `Ranking` metric must also be available for a candidate to remain eligible.
10. Conductor normalizes each ranking metric, applies `Weight`, and sums the scores.
11. If the top candidates tie, `TieBreaker` decides between them.
12. If policy evaluation cannot produce a candidate:
   - `UseLegacyLoadBalancingMode` falls back to the VMR's `LoadBalancingMode`
   - `FailClosed` returns an error instead of routing the request

## Important authoring rules

- Use metric IDs such as `rig.gpu.avgUtilizationPercent`, not raw RigMonitor JSON paths.
- `Value` inside a filter is always a string, even for numbers and booleans.
- Ranking metrics must be numeric.
- Every ranking rule must have `Weight > 0`.
- Telemetry-backed metrics require fresh cached RigMonitor data.
- `MaxTelemetryAgeMs` controls how old cached telemetry may be before those metrics are treated as unavailable.
- `FiltersJson`, `RankingJson`, `LabelsJson`, `TagsJson`, and `MetadataJson` are persistence fields. Do not send them in REST payloads.
- `health.isHealthy` and `health.hasCapacity` are usually redundant because the proxy already pre-filters to healthy endpoints with capacity. They are still valid if you want to make that requirement explicit.

## Explainability and simulation

Policy behavior is now inspectable through two management-plane workflows:

- `POST /v1.0/virtualmodelrunners/{id}/explain-routing` simulates a representative request and returns the same structured `RoutingDecision` model that the live proxy path uses internally.
- Request-history detail responses can include the captured `RoutingDecision` when request history is enabled on the VMR.

Use these surfaces to answer questions such as:

- which candidates were eliminated before policy evaluation
- which filter rule removed a specific endpoint
- which ranking metrics produced the winning score
- whether a draining endpoint was reused because of session affinity
- which request properties were mutated and why

## Top-level policy JSON

Send policies to `/v1.0/loadbalancingpolicies` as normal JSON objects.

```json
{
  "TenantId": "default",
  "Name": "Lowest GPU Utilization",
  "Description": "Prefer GPU-capable endpoints with the lowest current utilization.",
  "MaxTelemetryAgeMs": 30000,
  "Filters": [
    {
      "Metric": "rig.gpu.available",
      "Operator": "Equal",
      "ValueType": "Boolean",
      "Value": "true"
    }
  ],
  "Ranking": [
    {
      "Metric": "rig.gpu.avgUtilizationPercent",
      "Direction": "Ascending",
      "Weight": 1.0
    }
  ],
  "FallbackMode": "UseLegacyLoadBalancingMode",
  "TieBreaker": "RoundRobin",
  "Active": true,
  "Labels": ["gpu", "production"],
  "Tags": {
    "team": "ml-platform",
    "service": "chat"
  },
  "Metadata": {
    "changeTicket": "CHG-1042"
  }
}
```

## Top-level keys

| Key | Type | Required on create | Notes |
| --- | --- | --- | --- |
| `Id` | string | No | Server-assigned if omitted. |
| `TenantId` | string | Usually yes | Required when creating across tenants with admin/global-admin access. Normal tenant users are scoped automatically. |
| `Name` | string | Yes | Human-friendly policy name. |
| `Description` | string or `null` | No | Free-form operational description. |
| `MaxTelemetryAgeMs` | integer | No | Minimum effective value is `1000`. Default is `30000`. |
| `Filters` | array | No | Zero or more filter clauses. |
| `Ranking` | array | No | Zero or more ranking rules. If empty, eligible endpoints tie and `TieBreaker` decides. |
| `FallbackMode` | string | No | `UseLegacyLoadBalancingMode` or `FailClosed`. |
| `TieBreaker` | string | No | `RoundRobin`, `Random`, or `FirstAvailable`. |
| `Active` | boolean | No | Inactive policies are ignored at runtime. |
| `Labels` | array of strings | No | Optional categorization labels. |
| `Tags` | object | No | Optional string-to-string metadata map. |
| `Metadata` | object, array, scalar, or `null` | No | Free-form JSON metadata. |
| `CreatedUtc` | string | No | Server-managed UTC timestamp. |
| `LastUpdateUtc` | string | No | Server-managed UTC timestamp. |

## Filter object reference

Each `Filters` element has this shape:

```json
{
  "Metric": "rig.cpu.utilizationPercent",
  "Operator": "LessThanOrEqual",
  "ValueType": "Number",
  "Value": "70"
}
```

| Key | Type | Required | Notes |
| --- | --- | --- | --- |
| `Metric` | string | Yes | Must match a supported metric ID from the catalog. |
| `Operator` | string | Yes | Must be valid for that metric. |
| `ValueType` | string | Yes | Must match the metric's declared type exactly. |
| `Value` | string | Yes | String-encoded literal value. |

### Value formatting

- For booleans, use `"true"` or `"false"`.
- For numbers, use invariant-culture strings such as `"15"`, `"0.25"`, or `"8192"`.
- For strings, send the literal string value.

## Ranking object reference

Each `Ranking` element has this shape:

```json
{
  "Metric": "health.inFlightRequests",
  "Direction": "Ascending",
  "Weight": 0.5
}
```

| Key | Type | Required | Notes |
| --- | --- | --- | --- |
| `Metric` | string | Yes | Must be a supported numeric ranking metric. |
| `Direction` | string | Yes | `Ascending` means lower is better. `Descending` means higher is better. |
| `Weight` | number | Yes | Must be greater than `0`. |

## Enumerated values

### `FallbackMode`

| Value | Meaning |
| --- | --- |
| `UseLegacyLoadBalancingMode` | If policy selection fails, use the VMR's `LoadBalancingMode`. |
| `FailClosed` | If policy selection fails, return an error instead of routing. |

### `TieBreaker`

| Value | Meaning |
| --- | --- |
| `RoundRobin` | Rotate among equal-score endpoints. |
| `Random` | Choose randomly among equal-score endpoints. |
| `FirstAvailable` | Use the first equal-score endpoint in candidate order. |

### `Direction`

| Value | Meaning |
| --- | --- |
| `Ascending` | Lower metric values are better. |
| `Descending` | Higher metric values are better. |

### `Operator`

| Value | Meaning |
| --- | --- |
| `Equal` | `actual == expected` |
| `NotEqual` | `actual != expected` |
| `LessThan` | `actual < expected` |
| `LessThanOrEqual` | `actual <= expected` |
| `GreaterThan` | `actual > expected` |
| `GreaterThanOrEqual` | `actual >= expected` |

### `ValueType`

| Value | Meaning |
| --- | --- |
| `Number` | Numeric scalar |
| `Boolean` | Boolean scalar |
| `String` | String scalar |

`String` is supported by the policy engine, but the current public metric catalog is almost entirely numeric and boolean.

## Supported metrics

Use `GET /v1.0/loadbalancingpolicies/metrics` to retrieve the live catalog from the server.

The current built-in metrics are:

| Metric ID | Type | Filter | Rank | Recommended direction | Notes |
| --- | --- | --- | --- | --- | --- |
| `health.isHealthy` | `Boolean` | Yes | No | n/a | Derived from Conductor health checks. Usually redundant because unhealthy endpoints are already excluded before policy evaluation. |
| `health.hasCapacity` | `Boolean` | Yes | No | n/a | Reflects whether the endpoint can accept more in-flight work. Usually redundant for the same reason. |
| `health.inFlightRequests` | `Number` | Yes | Yes | `Ascending` | Current in-flight proxied request count. |
| `endpoint.weight` | `Number` | Yes | Yes | `Descending` | Static endpoint weight configured in Conductor. |
| `endpoint.maxParallelRequests` | `Number` | Yes | Yes | `Descending` | Endpoint concurrency limit. `0` means unlimited. |
| `rig.ready` | `Boolean` | Yes | No | n/a | Latest cached `/readyz` result from RigMonitor. |
| `rig.telemetry.ageMs` | `Number` | Yes | Yes | `Ascending` | Age of cached telemetry in milliseconds. Does not itself require fresh telemetry, because it measures age directly. |
| `rig.cpu.utilizationPercent` | `Number` | Yes | Yes | `Ascending` | Host CPU utilization from RigMonitor. |
| `rig.memory.utilizationPercent` | `Number` | Yes | Yes | `Ascending` | Host memory pressure. |
| `rig.memory.availableBytes` | `Number` | Yes | Yes | `Descending` | Free system memory. |
| `rig.network.totalReceiveBytesPerSecond` | `Number` | Yes | Yes | `Ascending` | Aggregate inbound throughput. |
| `rig.network.totalTransmitBytesPerSecond` | `Number` | Yes | Yes | `Ascending` | Aggregate outbound throughput. |
| `rig.disk.maxVolumeUtilizationPercent` | `Number` | Yes | Yes | `Ascending` | Worst disk utilization across reported volumes. |
| `rig.gpu.available` | `Boolean` | Yes | No | n/a | Whether NVIDIA GPU telemetry is available. |
| `rig.gpu.avgUtilizationPercent` | `Number` | Yes | Yes | `Ascending` | Average utilization across reported GPUs. |
| `rig.gpu.minFreeMemoryMegabytes` | `Number` | Yes | Yes | `Descending` | Lowest free VRAM across reported GPUs. |
| `rig.gpu.maxTemperatureCelsius` | `Number` | Yes | Yes | `Ascending` | Highest reported GPU temperature. |
| `rig.ollama.available` | `Boolean` | Yes | No | n/a | Whether RigMonitor can reach an Ollama daemon. |
| `rig.ollama.loadedModelCount` | `Number` | Yes | Yes | `Descending` | Number of currently loaded Ollama models. |

## Telemetry freshness and missing metrics

These behaviors matter in real deployments:

- Most `rig.*` numeric metrics require fresh cached telemetry.
- If telemetry is older than `MaxTelemetryAgeMs`, those metrics behave as unavailable.
- If a filter references an unavailable metric, the endpoint fails the filter.
- If a ranking rule references an unavailable metric, the endpoint is removed from the candidate set.
- `rig.telemetry.ageMs` can be used to explicitly demand fresh telemetry.
- `rig.ready`, `rig.gpu.available`, and `rig.ollama.available` are resolved from cached readiness/capability state and do not behave exactly like the numeric telemetry metrics.

## Common policy scenarios

### 1. Pure least-in-flight routing

Use this when you want the simplest policy-driven equivalent of "send traffic to the least busy endpoint" and you do not want to depend on RigMonitor.

```json
{
  "Name": "Least In-Flight Requests",
  "Description": "Prefer the endpoint currently handling the fewest proxied requests.",
  "MaxTelemetryAgeMs": 30000,
  "Filters": [],
  "Ranking": [
    {
      "Metric": "health.inFlightRequests",
      "Direction": "Ascending",
      "Weight": 1.0
    }
  ],
  "FallbackMode": "UseLegacyLoadBalancingMode",
  "TieBreaker": "RoundRobin",
  "Active": true
}
```

### 2. GPU-only routing with fail-closed behavior

Use this when requests must never land on a non-GPU endpoint.

```json
{
  "Name": "GPU Required",
  "Description": "Only route to GPU-capable endpoints with fresh telemetry.",
  "MaxTelemetryAgeMs": 15000,
  "Filters": [
    {
      "Metric": "rig.gpu.available",
      "Operator": "Equal",
      "ValueType": "Boolean",
      "Value": "true"
    },
    {
      "Metric": "rig.telemetry.ageMs",
      "Operator": "LessThanOrEqual",
      "ValueType": "Number",
      "Value": "15000"
    }
  ],
  "Ranking": [
    {
      "Metric": "rig.gpu.avgUtilizationPercent",
      "Direction": "Ascending",
      "Weight": 1.0
    }
  ],
  "FallbackMode": "FailClosed",
  "TieBreaker": "RoundRobin",
  "Active": true
}
```

### 3. Prefer the endpoint with the most free VRAM

Use this for GPU workloads where placement should favor memory headroom first and queue depth second.

```json
{
  "Name": "Most Free VRAM",
  "Description": "Prefer GPU endpoints with the most free VRAM, then the lowest in-flight queue.",
  "MaxTelemetryAgeMs": 30000,
  "Filters": [
    {
      "Metric": "rig.gpu.available",
      "Operator": "Equal",
      "ValueType": "Boolean",
      "Value": "true"
    }
  ],
  "Ranking": [
    {
      "Metric": "rig.gpu.minFreeMemoryMegabytes",
      "Direction": "Descending",
      "Weight": 0.8
    },
    {
      "Metric": "health.inFlightRequests",
      "Direction": "Ascending",
      "Weight": 0.2
    }
  ],
  "FallbackMode": "UseLegacyLoadBalancingMode",
  "TieBreaker": "RoundRobin",
  "Active": true
}
```

### 4. Balanced CPU and GPU placement

Use this when both host pressure and GPU pressure matter.

```json
{
  "Name": "Balanced CPU + GPU",
  "Description": "Blend CPU utilization, GPU utilization, and queue depth for general-purpose inference routing.",
  "MaxTelemetryAgeMs": 30000,
  "Filters": [
    {
      "Metric": "rig.gpu.available",
      "Operator": "Equal",
      "ValueType": "Boolean",
      "Value": "true"
    }
  ],
  "Ranking": [
    {
      "Metric": "rig.cpu.utilizationPercent",
      "Direction": "Ascending",
      "Weight": 0.35
    },
    {
      "Metric": "rig.gpu.avgUtilizationPercent",
      "Direction": "Ascending",
      "Weight": 0.45
    },
    {
      "Metric": "health.inFlightRequests",
      "Direction": "Ascending",
      "Weight": 0.20
    }
  ],
  "FallbackMode": "UseLegacyLoadBalancingMode",
  "TieBreaker": "RoundRobin",
  "Active": true
}
```

### 5. CPU-only placement with a hard utilization ceiling

Use this to keep routing off hot machines.

```json
{
  "Name": "Lowest CPU Under 70 Percent",
  "Description": "Only use endpoints whose cached CPU utilization is 70 percent or lower.",
  "MaxTelemetryAgeMs": 20000,
  "Filters": [
    {
      "Metric": "rig.cpu.utilizationPercent",
      "Operator": "LessThanOrEqual",
      "ValueType": "Number",
      "Value": "70"
    }
  ],
  "Ranking": [
    {
      "Metric": "rig.cpu.utilizationPercent",
      "Direction": "Ascending",
      "Weight": 1.0
    }
  ],
  "FallbackMode": "UseLegacyLoadBalancingMode",
  "TieBreaker": "Random",
  "Active": true
}
```

### 6. Ollama-aware placement

Use this when the backend is Ollama and you want to prefer endpoints that already have more models loaded.

```json
{
  "Name": "Ollama Warmest Host",
  "Description": "Prefer endpoints where RigMonitor reports an available Ollama daemon with loaded models.",
  "MaxTelemetryAgeMs": 30000,
  "Filters": [
    {
      "Metric": "rig.ollama.available",
      "Operator": "Equal",
      "ValueType": "Boolean",
      "Value": "true"
    }
  ],
  "Ranking": [
    {
      "Metric": "rig.ollama.loadedModelCount",
      "Direction": "Descending",
      "Weight": 0.75
    },
    {
      "Metric": "health.inFlightRequests",
      "Direction": "Ascending",
      "Weight": 0.25
    }
  ],
  "FallbackMode": "UseLegacyLoadBalancingMode",
  "TieBreaker": "RoundRobin",
  "Active": true
}
```

### 7. Filter-only policy with tie-breaker control

Use this when you want policies only to gate which endpoints are eligible and do not care about weighted scoring.

```json
{
  "Name": "Only Large Endpoints",
  "Description": "Only allow endpoints with high configured parallelism and weight.",
  "MaxTelemetryAgeMs": 30000,
  "Filters": [
    {
      "Metric": "endpoint.maxParallelRequests",
      "Operator": "GreaterThanOrEqual",
      "ValueType": "Number",
      "Value": "8"
    },
    {
      "Metric": "endpoint.weight",
      "Operator": "GreaterThanOrEqual",
      "ValueType": "Number",
      "Value": "100"
    }
  ],
  "Ranking": [],
  "FallbackMode": "UseLegacyLoadBalancingMode",
  "TieBreaker": "Random",
  "Active": true
}
```

## Attaching a policy to a VMR

Policies are not used until a VMR references them.

Example VMR payload:

```json
{
  "TenantId": "default",
  "Name": "gpu-chat",
  "BasePath": "/v1.0/api/gpu-chat/",
  "ApiType": "OpenAI",
  "LoadBalancingPolicyId": "lbp_1234567890",
  "LoadBalancingMode": "RoundRobin",
  "ModelRunnerEndpointIds": [
    "mre_gpu_a",
    "mre_gpu_b"
  ],
  "TimeoutMs": 300000,
  "AllowEmbeddings": true,
  "AllowCompletions": true,
  "AllowModelManagement": false,
  "StrictMode": false,
  "SessionAffinityMode": "None",
  "Active": true
}
```

`LoadBalancingMode` still matters even when a policy is attached, because a policy can be configured to fall back to it.

## Recommended authoring workflow

1. Confirm the endpoint fleet exposes the telemetry you plan to use.
2. Start with a filter-only or single-metric policy.
3. Validate the metric IDs against `/v1.0/loadbalancingpolicies/metrics`.
4. Attach the policy to a non-critical VMR first.
5. Inspect endpoint health and RigMonitor cache data.
6. Add more ranking rules only after the first rule behaves as expected.
7. Switch to `FailClosed` only when you are sure the policy should never route outside its constrained target set.

## Troubleshooting

### `Unsupported filter metric '...'`

The metric ID is not in the live catalog. Use `/v1.0/loadbalancingpolicies/metrics` and copy the exact `Id` value.

### `Metric '...' requires value type '...'`

`ValueType` must exactly match the metric definition.

### `Value '...' is invalid for metric '...'`

The string in `Value` could not be parsed as the declared `ValueType`.

### `No endpoints satisfied the policy filters or telemetry requirements.`

Common causes:

- telemetry is stale relative to `MaxTelemetryAgeMs`
- a `rig.*` metric is unavailable on one or more endpoints
- the policy requires GPUs or Ollama, but those capabilities are not present
- the filter thresholds are too strict

### The policy exists but Conductor seems to ignore it

Check:

- the VMR's `LoadBalancingPolicyId`
- the policy's `Active` flag
- that the policy exists in the same tenant as the VMR

### How to inspect live state

Useful APIs:

- `GET /v1.0/loadbalancingpolicies/metrics`
- `GET /v1.0/modelrunnerendpoints/{id}/health`
- `GET /v1.0/modelrunnerendpoints/{id}/rigmonitor`
- `GET /v1.0/virtualmodelrunners/{id}/health`

## Related documentation

- [REST_API.md](REST_API.md)
