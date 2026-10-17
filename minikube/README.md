# Minikube and k3s

Prerequisites: kubectl, Python 3, a default StorageClass and an amd64 Kubernetes
node (published images currently target linux/amd64). On Apple silicon, use an
amd64 VM/remote node or rebuild both images for linux/arm64 and load them locally.

```sh
minikube start --cpus=2 --memory=4096
./minikube/deploy.sh
kubectl -n conductor port-forward svc/conductor-dashboard 9100:9100
```

For k3s, select its kubeconfig/context and run the same deploy.sh command; do not
run minikube start. k3s's local-path provisioner and Minikube's default provisioner
satisfy this overlay's 2Gi PVC. Check `kubectl config current-context` before deployment.
Open http://localhost:9100 and authenticate using the generated API key in
`kubernetes/conductor.json`. The server URL is `/api`.

The overlay reuses the base deployments, storage and networking; only the storage
request is reduced. No cluster-wide ingress controller is required. Test persistence
by restarting the server pod and checking that your profiles remain. Local model
servers must be reachable from cluster pods (pod localhost refers to the pod).
For Minikube, use host.minikube.internal where supported; k3s needs a reachable host
address. Teardown: `kubectl delete -k minikube` (deletes namespace and its PVC).
Read [the base deployment notes](../kubernetes/README.md) before exposing it remotely.
