# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Build the application
RUN npm run build

# Production stage
FROM nginx:alpine AS production

# Install networking and troubleshooting tools
RUN apk add --no-cache \
    iputils \
    net-tools \
    busybox-extras \
    wget \
    curl \
    vim \
    bind-tools \
    netcat-openbsd \
    iproute2 \
    tcpdump

# Copy custom nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy built files
COPY --from=build /app/dist /usr/share/nginx/html

# Copy entrypoint script
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

# Expose port
EXPOSE 9100

# Use entrypoint to generate runtime config
ENTRYPOINT ["/docker-entrypoint.sh"]
