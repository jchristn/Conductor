#!/bin/bash

echo "Building Conductor Dashboard..."

TAG=${1:-latest}

cd "$(dirname "$0")/dashboard"

echo "Building and pushing multi-platform Docker image..."
if [ "$TAG" = "latest" ]; then
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/conductor-ui:latest --push .
else
    docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 -t jchristn77/conductor-ui:$TAG -t jchristn77/conductor-ui:latest --push .
fi

if [ $? -eq 0 ]; then
    echo ""
    echo "Build and push successful!"
    if [ "$TAG" = "latest" ]; then
        echo "Image: jchristn77/conductor-ui:latest"
    else
        echo "Images: jchristn77/conductor-ui:$TAG, jchristn77/conductor-ui:latest"
    fi
    echo "Platforms: linux/amd64, linux/arm64/v8"
else
    echo ""
    echo "Build failed!"
    exit 1
fi
