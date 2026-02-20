#!/bin/sh

# Generate runtime config from environment variables
cat <<EOF > /usr/share/nginx/html/config.js
window.CONDUCTOR_SERVER_URL = "${CONDUCTOR_SERVER_URL:-http://localhost:9000}";
EOF

# Start nginx
exec nginx -g "daemon off;"
