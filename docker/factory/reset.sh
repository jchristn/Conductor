#!/usr/bin/env bash
#
# Conductor Docker Factory Reset
#
# Resets the Docker deployment to its default configuration by:
# - Stopping all containers
# - Restoring the factory conductor.json configuration
# - Rebuilding a clean database with default records
# - Removing all log files and request history
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "========================================================================"
echo "  CONDUCTOR FACTORY RESET"
echo "========================================================================"
echo ""
echo "  This will completely reset your Conductor Docker deployment to the"
echo "  factory default state. ALL data will be lost, including:"
echo ""
echo "    - All tenants, users, and credentials"
echo "    - All model runner endpoints, definitions, and configurations"
echo "    - All virtual model runners"
echo "    - All request history"
echo "    - All log files"
echo "    - Any custom configuration"
echo ""
echo "  Default credentials after reset:"
echo "    Admin Email:    admin@conductor"
echo "    Admin Password: password"
echo "    Admin API Key:  conductoradmin"
echo "    API Bearer Token: factory_default_bearer_token_0000000000000000000000000000000000"
echo ""
echo "========================================================================"
echo ""
read -p "  Type RESET to proceed: " CONFIRM
echo ""

if [ "$CONFIRM" != "RESET" ]; then
    echo "  Reset cancelled."
    exit 1
fi

echo "  [1/5] Stopping Docker containers..."
cd "$DOCKER_DIR"
docker compose down 2>/dev/null || docker-compose down 2>/dev/null || true

echo "  [2/5] Restoring factory configuration..."
cp "$SCRIPT_DIR/conductor.json" "$DOCKER_DIR/conductor.json"

echo "  [3/5] Removing database and request history..."
rm -f "$DOCKER_DIR/data/conductor.db"
rm -f "$DOCKER_DIR/data/conductor.db-wal"
rm -f "$DOCKER_DIR/data/conductor.db-shm"
rm -rf "$DOCKER_DIR/data/request-history"
mkdir -p "$DOCKER_DIR/data/request-history"

echo "  [4/5] Removing log files..."
find "$DOCKER_DIR/logs" -type f ! -name '.gitkeep' -delete 2>/dev/null || true

echo "  [5/5] Rebuilding factory database..."
if command -v sqlite3 &>/dev/null; then
    sqlite3 "$DOCKER_DIR/data/conductor.db" < "$SCRIPT_DIR/schema.sql"
    echo "         Factory database created with default records."
else
    echo "         sqlite3 not found. The database will be created"
    echo "         automatically with new default credentials on next startup."
    echo "         Note: The auto-generated credentials will differ from the"
    echo "         factory defaults listed above."
fi

echo ""
echo "========================================================================"
echo "  RESET COMPLETE"
echo ""
echo "  Start the deployment with:"
echo "    cd $DOCKER_DIR"
echo "    docker compose up -d"
echo ""
echo "  Dashboard: http://localhost:9100"
echo "  API:       http://localhost:9000"
echo "========================================================================"
