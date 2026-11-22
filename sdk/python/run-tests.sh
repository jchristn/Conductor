#!/usr/bin/env bash
# Run the Conductor Python SDK test harness (unittest).
# Usage: ./run-tests.sh
# Set PYTHON to choose an interpreter; otherwise python3, then python, is used.
# When the interpreter cannot import requests, a local virtual environment is
# created in .venv and requests is installed into it.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

PY="${PYTHON:-}"
if [ -z "$PY" ]; then
  if command -v python3 >/dev/null 2>&1 && python3 -c "import sys" >/dev/null 2>&1; then
    PY="python3"
  elif command -v python >/dev/null 2>&1 && python -c "import sys" >/dev/null 2>&1; then
    PY="python"
  else
    echo "Python was not found on PATH. Install Python 3.10 or later." >&2
    exit 1
  fi
fi

if ! "$PY" -c "import requests" >/dev/null 2>&1; then
  if [ ! -d .venv ]; then
    "$PY" -m venv .venv
  fi
  if [ -x .venv/bin/python ]; then
    PY=".venv/bin/python"
  else
    PY=".venv/Scripts/python.exe"
  fi
  "$PY" -m pip install --quiet --disable-pip-version-check "requests>=2.31.0"
fi

PYTHONPATH="src${PYTHONPATH:+:$PYTHONPATH}" "$PY" -m unittest discover -s tests -v
