# Conductor QoS & Queueing — Overview

Conductor can put a Quality-of-Service queueing layer in front of any virtual model runner (VMR). Instead of letting requests race to an endpoint and bounce with `429` the moment every endpoint is busy, a VMR linked to a **QoS profile** classifies incoming traffic, parks it in the queue discipline you choose — FIFO, priority, weighted-fair, low-latency, weighted round robin, or a hierarchy of them — and releases requests in scheduled order as endpoint capacity frees. The queueing engine is built on [QoSKit](https://github.com/jchristn/qoskit); Conductor owns the configuration, the request path, the seeding, and the telemetry.

This document is the practical guide: what QoS does, how it is put together, how to configure it, and how to watch it work.

## The problem it solves

An inference endpoint has a finite number of slots (`MaxParallelRequests`). When a VMR's endpoints are all full, something has to give. Conductor's older behavior was to reject immediately — correct, but blunt: a burst of low-value batch traffic and a human waiting on a chat response were treated identically, and both got bounced. QoS changes that. Under contention it becomes an admission controller that decides *who waits and who goes first*, and it only rejects when a queue is genuinely full or a request has waited past its deadline. When there is spare capacity, it does nothing measurable — a priority queue with idle endpoints behaves exactly like FIFO, because every request is released the instant it arrives.

The key idea worth internalizing: **scheduling is invisible until requests actually wait.** QoS earns its keep exactly when a VMR is saturated. A VMR whose endpoints are configured with unlimited concurrency never builds a backlog and never pays for queueing.

## Core concepts

A **QoS profile** is a tenant-scoped, reusable record. A VMR references one by id; linking a profile is required, and new VMRs default to the tenant's built-in FIFO profile. A profile answers three questions and nothing else.

**Classification** decides which traffic class a request belongs to. You choose what drives it: a custom header, a specific credential, the requested model, a body attribute, the tenant, the user, the client IP, or the API family. Rules are evaluated in order; the first match wins; anything unmatched takes the profile's default class. Classification never blocks and never touches the network — it reads fields Conductor has already parsed.

**Topology** is the set of queue nodes and the links between them, ending in a single tail queue. Each node is a discipline with its own depth bound and overflow policy. A class maps onto a priority band, a weighted-fair flow, a CBWFQ/LLQ class, or a WRR sub-queue depending on the discipline.

**Admission** is the runtime behavior. A per-VMR scheduler drains the tail in the discipline's order and releases one request each time an endpoint slot frees. A full queue is rejected immediately; a request that waits past the profile's deadline is rejected with `Retry-After`. Admission gates against the VMR's *existing* endpoint capacity — the sum of its endpoints' `MaxParallelRequests` — so QoS never introduces a second concurrency limit to reason about.

### The traffic classes

Every tenant is seeded with a standard class catalog. These names are the vocabulary a profile's classifier and topology reference; you can edit the catalog and add your own.

| Class | Meaning | Typical scheduling |
| --- | --- | --- |
| `realtime` | Live/streaming — voice, token streaming | Strict-priority, rate-limited; above human |
| `human-interactive` | A person waiting on a response | Strict-priority, rate-limited |
| `agent-interactive` | An autonomous agent in a live loop | Top weighted-fair tier |
| `batch-time-bound` | Bulk work with a soft deadline | Mid weighted-fair |
| `batch-background` | Best-effort bulk (backfills, evals) | Lowest weighted-fair |
| `default` | Fallback for unclassified traffic | Modest weighted-fair |

## How configuration becomes behavior

A stored profile is inert rows in the database. On first use for a VMR, Conductor's compiler reads the profile aggregate and builds a live runtime: a classifier delegate, one QoSKit queue per node, the pipeline that moves work to the tail, and the scheduler that drains it. The item carried through the queues is a lightweight ticket — the class it was assigned plus a release signal — never the request body, so a deep backlog stays cheap and no payload data ever reaches a metric label. Editing a linked profile rebuilds its runtime; a compile failure fails open (the VMR admits without queueing) rather than blocking traffic.

## How to configure it

### The default is already there

Do nothing and every VMR uses the tenant's **Default (FIFO)** profile: a single first-in-first-out queue that is a transparent pass-through when capacity is free and a fair first-come-first-served line when the VMR is saturated. The default profile is non-deletable. There is also a seeded **Standard Workloads** profile you can link or clone to get class-aware scheduling immediately.

### Classifying by a custom header

The Standard Workloads profile classifies on the `X-Conductor-Class` header — a client simply declares its class:

```
POST /v1.0/api/{vmr}/v1/chat/completions
X-Conductor-Class: human-interactive
```

To build your own, a classifier rule names a source, a key into that source, an operator, a value, and the class to assign. Creating a profile over REST:

```bash
curl -X POST http://127.0.0.1:9000/v1.0/qosprofiles \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{
    "Name": "Team priority",
    "DefaultClass": "default",
    "IngressMode": "Single",
    "IngressDefaultNode": "egress",
    "TailNode": "egress",
    "Rules": [
      { "Ordinal": 0, "Source": "Header",     "MatchKey": "X-Team",  "Operator": "Equals",   "MatchValue": "payments", "ClassName": "gold" },
      { "Ordinal": 1, "Source": "Credential", "Operator": "Equals",  "MatchValue": "cred_batch", "ClassName": "batch-background" },
      { "Ordinal": 2, "Source": "BodyJsonPath","MatchKey": "$.stream","Operator": "Equals",   "MatchValue": "true",     "ClassName": "human-interactive" }
    ],
    "Nodes": [
      { "Name": "egress", "Discipline": "Priority", "MaxDepth": 0, "OverflowPolicy": "Reject", "AgingThresholdMs": 2000,
        "Classes": [
          { "Ordinal": 0, "Kind": "Band", "ClassName": "gold", "Band": 0 },
          { "Ordinal": 1, "Kind": "Band", "ClassName": "human-interactive", "Band": 1 },
          { "Ordinal": 2, "Kind": "Band", "ClassName": "default", "Band": 2 },
          { "Ordinal": 3, "Kind": "Band", "ClassName": "batch-background", "Band": 3 }
        ] }
    ],
    "Limits": { "MaxQueueWaitMs": 30000 }
  }'
```

`POST /v1.0/qosprofiles/validate` compiles a draft without saving it and returns the errors, so a bad topology (a cycle, an unknown class, a `Block` overflow on an admission node) is caught at design time. `GET /v1.0/qosprofiles/classifier-catalog` returns the available sources, operators, disciplines, and the tenant's classes.

### Linking a profile to a VMR

Set `QosProfileId` on the VMR. If you leave it empty on create, Conductor assigns the tenant default automatically; if you set an unknown id, the create is rejected.

```bash
curl -X PUT http://127.0.0.1:9000/v1.0/virtualmodelrunners/$VMR_ID \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{ ..., "QosProfileId": "qos_..." }'
```

### What a caller sees under load

When a profile's queue is full, or a request waits past `MaxQueueWaitMs`, the caller gets `429 Too Many Requests` with a `Retry-After` header. Existing retry logic that already handles `429` works unchanged.

## How to monitor it

QoS emits through Conductor's existing OpenTelemetry pipeline, so it lands in the bundled Prometheus/Grafana/Tempo stack with no extra wiring.

**Metrics.** QoSKit's own per-class instruments (`qoskit_queue_enqueued_total`, `qoskit_queue_dropped_total` by `drop_reason`, `qoskit_queue_wait_duration_milliseconds` per `queue_class`, `qoskit_policer_conformed_total` vs `qoskit_policer_exceeded_total`, depth and capacity gauges) flow alongside Conductor's admission-boundary instruments: `conductor_qos_admissions_total` (by `outcome`), `conductor_qos_rejections_total` (by `reason`), `conductor_qos_queue_wait_duration_seconds`, and `conductor_qos_queue_depth`. All are tagged with `vmr` and, where meaningful, `qos_class`. Example PromQL:

```promql
# per-class p95 wait time
histogram_quantile(0.95, sum by (le, queue_class) (rate(qoskit_queue_wait_duration_milliseconds_bucket[5m])))

# admission outcomes for a VMR
sum by (outcome) (rate(conductor_qos_admissions_total{vmr="my-runner"}[5m]))
```

**Traces.** A proxied request gains an `inference.qos.admit` span (a sibling of `inference.forward` under the `inference.proxy` root), so the time a request spent waiting is a visible segment in Tempo. QoSKit's `queue.enqueue` / `queue.dequeue` / `link.move` spans decompose a multi-node hierarchy underneath it.

**Grafana.** The bundled stack ships a **QoS & Queueing** dashboard (in the **Conductor** Grafana folder) with admission, throughput, backpressure/drops, per-class latency, and policer rows. The home page links to it.

## Operations: purging a tenant

Deleting a tenant now also removes its QoS configuration. For a full, reportable wipe there is a system-admin-only nuke endpoint, `POST /v1.0/tenants/{id}/purge`, which requires the caller to echo the tenant id (`{"confirmTenantId":"..."}`), refuses the reserved `default` tenant, and returns an itemized report of everything deleted. Tenant admins cannot invoke it.

## The discipline catalog

- **FIFO / LIFO** — the baseline; order by arrival (or reverse).
- **Priority** — strict bands, lowest band first, with optional aging so a busy high band cannot starve the low ones forever.
- **WFQ** — weighted fair queuing across flows; heavier flows get proportionally more service.
- **CBWFQ** — weighted fair queuing over named classes matched by the assigned class.
- **LLQ** — strict-priority classes served ahead of weighted-fair classes, with an optional token-bucket rate limit on the priority classes so they cannot starve everything else. This is what the seeded Standard Workloads profile uses.
- **WRR** — sub-queues served in proportion to weight using deficit round robin.

Nodes chain into a hierarchy that ends at one tail. The classic shape — a low-latency queue and a weighted-fair queue on the input side, chosen by classifier, both feeding a strict-priority tail — is expressed as two ingress nodes, a class-routed ingress, and links into the tail.

The honest limit worth stating: queues live in memory. An in-flight HTTP request cannot survive a server restart — its socket is already gone — so the backlog is not persisted; only the profile configuration is durable. On restart, parked waiters are cancelled and their clients retry.
