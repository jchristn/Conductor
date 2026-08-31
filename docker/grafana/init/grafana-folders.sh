#!/bin/sh
#
# Create the nested Grafana folder hierarchy that file-based dashboard provisioning
# cannot build on its own in Grafana 11.x: a single top-level "Conductor" folder with
# one subfolder per subsystem. Dashboards are bound to these subfolders by folderUid in
# provisioning/dashboards/dashboards.yaml.
#
# Each subfolder is created directly under "Conductor" via the Folder API (parentUid;
# nested folders are GA in Grafana 11). A move call follows in case dashboard provisioning
# raced ahead and created the folder at root first — reparenting is idempotent, so the end
# state is Conductor > <subsystem> regardless of ordering. Safe to re-run on every start.
#
# Env:
#   GRAFANA_URL   Base URL of the Grafana API (default http://grafana:3000)
#   GRAFANA_AUTH  Basic-auth credentials for the API (default admin:admin)
#
set -eu

GRAFANA_URL="${GRAFANA_URL:-http://grafana:3000}"
GRAFANA_AUTH="${GRAFANA_AUTH:-admin:admin}"

echo "grafana-folders: waiting for Grafana at ${GRAFANA_URL} ..."
attempt=0
until curl -fsS -u "${GRAFANA_AUTH}" "${GRAFANA_URL}/api/health" >/dev/null 2>&1; do
  attempt=$((attempt + 1))
  if [ "${attempt}" -ge 60 ]; then
    echo "grafana-folders: Grafana did not become ready in time" >&2
    exit 1
  fi
  sleep 2
done
echo "grafana-folders: Grafana is ready."

# Top-level parent folder (idempotent: a 409/412 on re-run is ignored).
curl -fsS -u "${GRAFANA_AUTH}" -X POST "${GRAFANA_URL}/api/folders" \
  -H 'Content-Type: application/json' \
  -d '{"uid":"conductor","title":"Conductor"}' >/dev/null 2>&1 || true

# One "<uid>|<title>" per line; keep in sync with provisioning/dashboards/dashboards.yaml.
SUBFOLDERS='conductor-folder-database|Database
conductor-folder-http|HTTP and API
conductor-folder-inference|Inference
conductor-folder-qos|QoS and Queueing
conductor-folder-routing|Routing and Load Balancing
conductor-folder-runtime|Runtime
conductor-folder-health|Health and Endpoints'

echo "${SUBFOLDERS}" | while IFS='|' read -r uid title; do
  [ -z "${uid}" ] && continue
  # Create the subfolder directly under Conductor (idempotent).
  curl -fsS -u "${GRAFANA_AUTH}" -X POST "${GRAFANA_URL}/api/folders" \
    -H 'Content-Type: application/json' \
    -d "{\"uid\":\"${uid}\",\"title\":\"${title}\",\"parentUid\":\"conductor\"}" >/dev/null 2>&1 || true
  # Reparent under Conductor in case provisioning created it at root first (idempotent).
  curl -fsS -u "${GRAFANA_AUTH}" -X POST "${GRAFANA_URL}/api/folders/${uid}/move" \
    -H 'Content-Type: application/json' \
    -d '{"parentUid":"conductor"}' >/dev/null 2>&1 || true
  echo "grafana-folders: ensured Conductor > ${title}"
done

echo "grafana-folders: done."
