#!/bin/sh

# Generate runtime config from environment variables
SERVER_URL="${CONDUCTOR_SERVER_URL:-http://localhost:9000}"
ESCAPED_SERVER_URL="$(printf '%s' "$SERVER_URL" | sed 's/\\/\\\\/g; s/"/\\"/g')"

cat <<EOF > /usr/share/nginx/html/config.js
window.CONDUCTOR_SERVER_URL = "${ESCAPED_SERVER_URL}";
EOF

# Start nginx
exec nginx -g "daemon off;"
