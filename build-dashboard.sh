#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ] || [ -z "${1:-}" ]; then
    echo "Error: build-dashboard accepts a single image tag argument." >&2
    echo "Usage: $(basename "$0") <tag>" >&2
    exit 1
fi

IMAGE_TAG="$1"
IMAGE_NAME="jchristn77/conductor-dashboard"
BUILDER="${DOCKER_BUILDER:-cloud-jchristn77-jchristn77}"
# Published images are linux/amd64; pin it so builds from Apple silicon do not produce arm64 images.
PLATFORM="${DOCKER_PLATFORM:-linux/amd64}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

cd "$ROOT_DIR"

echo "Building $IMAGE_NAME:$IMAGE_TAG and $IMAGE_NAME:latest for $PLATFORM..."
docker build --builder "$BUILDER" --platform "$PLATFORM" --load -f dashboard/Dockerfile -t "$IMAGE_NAME:$IMAGE_TAG" -t "$IMAGE_NAME:latest" dashboard

echo "Pushing $IMAGE_NAME:$IMAGE_TAG..."
docker push "$IMAGE_NAME:$IMAGE_TAG"

echo "Pushing $IMAGE_NAME:latest..."
docker push "$IMAGE_NAME:latest"

echo "Dashboard image build and push completed."
