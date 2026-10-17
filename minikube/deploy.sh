#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Uses the currently selected kubectl context (minikube or k3s).
"$ROOT_DIR/../kubernetes/configure.sh"
kubectl apply -k "$ROOT_DIR"
kubectl -n conductor rollout status deployment/conductor --timeout=300s
kubectl -n conductor rollout status deployment/conductor-dashboard --timeout=300s
printf '%s\n' 'Run: kubectl -n conductor port-forward svc/conductor-dashboard 9100:9100'
