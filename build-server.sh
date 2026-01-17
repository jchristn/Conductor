#!/bin/bash

echo "Building Conductor Server..."

TAG=${1:-latest}

cd "$(dirname "$0")"

echo "Building and pushing multi-platform Docker image..."
if [ "$TAG" = "latest" ]; then
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/conductor:latest -f src/Conductor.Server/Dockerfile --push .
else
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/conductor:$TAG -t jchristn77/conductor:latest -f src/Conductor.Server/Dockerfile --push .
fi

if [ $? -eq 0 ]; then
    echo ""
    echo "Build and push successful!"
    if [ "$TAG" = "latest" ]; then
        echo "Image: jchristn77/conductor:latest"
    else
        echo "Images: jchristn77/conductor:$TAG, jchristn77/conductor:latest"
    fi
    echo "Platforms: linux/amd64, linux/arm64/v8"
else
    echo ""
    echo "Build failed!"
    exit 1
fi
