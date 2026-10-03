#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ] || [ -z "${1:-}" ]; then
    echo "Error: build-all accepts a single image tag argument." >&2
    echo "Usage: $(basename "$0") <tag>" >&2
    exit 1
fi

IMAGE_TAG="$1"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$ROOT_DIR/build-server.sh" "$IMAGE_TAG"
"$ROOT_DIR/build-dashboard.sh" "$IMAGE_TAG"

echo "All Docker images built and pushed with tag $IMAGE_TAG and latest."
