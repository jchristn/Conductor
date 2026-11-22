#!/usr/bin/env bash
# Run the Conductor C# SDK test harness.
# Usage: ./run-tests.sh [additional dotnet test arguments]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet was not found on PATH. Install the .NET 10 SDK." >&2
  exit 1
fi

dotnet test Conductor.Sdk.slnx "$@"
