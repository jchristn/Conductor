#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ ! -f "$ROOT_DIR/conductor.json" ]; then
  umask 077
  python3 - "$ROOT_DIR" <<'PY'
import json, pathlib, secrets, sys
root = pathlib.Path(sys.argv[1])
config = json.loads((root.parent / 'docker/conductor.json').read_text())
config['Database'] = {'Type': 'Sqlite', 'Filename': '/app/data/conductor.db'}
config['Webserver']['Hostname'] = '*'
config['Webserver']['Cors']['Enabled'] = False
config['OpenTelemetry']['Enabled'] = False
config['AdminApiKeys'] = [secrets.token_urlsafe(32)]
(root / 'conductor.json').write_text(json.dumps(config, indent=2) + '\n')
PY
fi
kubectl apply -f "$ROOT_DIR/namespace.yaml"
kubectl -n conductor create secret generic conductor-settings --from-file=conductor.json="$ROOT_DIR/conductor.json" --dry-run=client -o yaml | kubectl apply -f -
printf '%s\n' "Settings stored in $ROOT_DIR/conductor.json (keep private). Review before deploying."
