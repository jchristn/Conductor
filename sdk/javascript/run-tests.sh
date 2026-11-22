#!/usr/bin/env bash
# Run the Conductor JavaScript SDK test harness (node --test).
# Usage: ./run-tests.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if ! command -v node >/dev/null 2>&1; then
  echo "node was not found on PATH. Install Node.js 20 or later." >&2
  exit 1
fi

# The SDK has no runtime dependencies today. Install from the lock file only
# when one exists, so adding dependencies later does not require editing this script.
if [ -f package-lock.json ]; then
  npm ci --no-audit --no-fund
fi

npm test
