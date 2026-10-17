# Kubernetes

This Kustomize base deploys Conductor v0.6.0, its dashboard, ClusterIP services,
a 10Gi persistent volume, resource limits and health probes. It requires kubectl,
Python 3, a default StorageClass and outbound access to Docker Hub. Images default
to linux/amd64; use amd64 cluster nodes or publish the images for your node architecture.

The default is a **single server with SQLite**, using Recreate to prevent simultaneous
writers on the PVC. This is a functional starting point, not a highly available cluster.
QoS queues and in-flight requests are process-local. Do not increase server replicas
without a separate routing/scheduling design. SQLite and request history live under
`/app/data`; logs are ephemeral.

```sh
./kubernetes/configure.sh
# Review private kubernetes/conductor.json before deploying.
kubectl apply -k kubernetes
kubectl -n conductor rollout status deployment/conductor
kubectl -n conductor rollout status deployment/conductor-dashboard
kubectl -n conductor port-forward svc/conductor-dashboard 9100:9100
```

Open http://localhost:9100. Use `/api` as the server URL if your browser saved an
older override. The dashboard proxies `/api/` to the internal server, including
streamed responses. Find the generated admin API key in the local settings file;
keep that file private. First-run initialization also creates the existing factory
`admin@conductor` / `password` accounts: change those passwords before exposing
this deployment. The configure script preserves settings on subsequent runs.

For an existing PostgreSQL service, replace the Database section in the private
settings file with your PostgreSql hostname, port, database, username, password and
RequireEncryption settings; the server initializes its schema. Run configure.sh again
and restart the server after changing the secret (subPath mounts do not auto-refresh):

```sh
./kubernetes/configure.sh
kubectl -n conductor rollout restart deployment/conductor
```

For external access, attach your ingress controller to service conductor-dashboard
port 9100 and configure its TLS certificate. Both API and UI then share the same
HTTPS origin. Set ingress request/idle timeouts for long inference streams. No
IngressClass or certificate issuer is assumed. Optional telemetry is disabled by
default; configure your OTLP collector in the private settings file to enable it.
Back up the PVC and private configuration. Deleting the namespace deletes its PVC;
the storage provider's reclaim policy determines whether data survives.

Validate rendered resources before deploying:

```sh
kubectl kustomize kubernetes
kubectl apply --dry-run=server -k kubernetes
```

See [Kustomize documentation](https://kubernetes.io/docs/tasks/manage-kubernetes-objects/kustomization/).
