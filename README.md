<img src="./assets/icon-dark.png" alt="Conductor" width="128" height="128">

# Conductor

Conductor is a platform for managing models, model runners, model configurations, and virtualizing combinations into virtual model runners exposed to the network via OpenAI or Ollama compatible APIs.

## Features

- **Multi-tenant Architecture**: Full tenant isolation with tenant-scoped data access
- **Model Runner Endpoints**: Define and manage connections to Ollama or OpenAI-compatible model runners
- **Model Definitions**: Catalog your models with metadata like family, parameter size, and quantization
- **Model Configurations**: Create reusable configurations with pinned properties for embeddings and completions
- **Virtual Model Runners**: Combine endpoints and configurations into virtual endpoints with load balancing
- **Configuration Pinning**: Automatically inject model parameters into requests (like OllamaFlow)
- **Session Affinity**: Pin clients to specific backend endpoints based on IP address, API key, or custom headers to minimize context drops and model swapping
- **Load Balancing**: Round-robin, random, or first-available endpoint selection with weighted distribution and optional session affinity
- **Health Checking**: Automatic background health monitoring of endpoints with configurable thresholds
- **Rate Limiting**: Per-endpoint maximum parallel request limits with automatic capacity management
- **React Dashboard**: Full-featured UI for managing all entities including real-time health status

## Quick Start

### Using Docker Compose

```bash
cd docker
docker compose up -d
```

The server will be available at `http://localhost:9000` and the dashboard at `http://localhost:9100`.

### Building from Source

#### Prerequisites

- .NET 10 SDK
- Node.js 20+

#### Build and Run Server

```bash
cd src/Conductor.Server
dotnet run
```

#### Build and Run Dashboard

```bash
cd dashboard
npm install
npm run dev
```

## API Overview

### Authentication

Conductor supports two authentication methods:

1. **Header-based**: Include `x-tenant-id`, `x-email`, and `x-password` headers
2. **Bearer Token**: Include `Authorization: Bearer {token}` header

### User Permission Model

Users have three permission levels:

| Permission | Description |
|------------|-------------|
| **Global Admin** (`IsAdmin=true`) | Full cross-tenant access to all resources |
| **Tenant Admin** (`IsTenantAdmin=true`) | Can manage users and credentials within their own tenant |
| **Standard User** | Can only access model configurations, endpoints, runners, and virtual runners in their tenant |

- **Global Admins** can operate on any tenant by specifying `TenantId` in their requests
- **Tenant Admins** have elevated privileges within their assigned tenant
- **Standard Users** have read/write access to non-administrative resources

### Endpoints

| Entity | Prefix | API Endpoint |
|--------|--------|--------------|
| Administrator | `admin_` | `/v1.0/administrators` |
| Tenant | `ten_` | `/v1.0/tenants` |
| User | `usr_` | `/v1.0/users` |
| Credential | `cred_` | `/v1.0/credentials` |
| Model Runner Endpoint | `mre_` | `/v1.0/modelrunnerendpoints` |
| Model Definition | `md_` | `/v1.0/modeldefinitions` |
| Model Configuration | `mc_` | `/v1.0/modelconfigurations` |
| Virtual Model Runner | `vmr_` | `/v1.0/virtualmodelrunners` |

### Virtual Model Runner Proxy

Virtual model runners expose an API at their configured base path. For example, a VMR with base path `/v1.0/api/my-vmr/` would expose:

- **OpenAI API**: `/v1.0/api/my-vmr/v1/chat/completions`, `/v1.0/api/my-vmr/v1/embeddings`
- **Ollama API**: `/v1.0/api/my-vmr/api/generate`, `/v1.0/api/my-vmr/api/chat`

## Configuration

### conductor.json

```json
{
  "Webserver": {
    "Hostname": "localhost",
    "Port": 9000,
    "Ssl": false,
    "Cors": {
      "Enabled": false,
      "AllowedOrigins": [],
      "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
      "AllowedHeaders": ["Content-Type", "Authorization"],
      "ExposedHeaders": [],
      "AllowCredentials": false,
      "MaxAgeSeconds": 86400
    }
  },
  "Database": {
    "Type": "Sqlite",
    "Filename": "./conductor.db"
  },
  "Logging": {
    "Servers": [],
    "LogDirectory": "./logs/",
    "LogFilename": "conductor.log",
    "ConsoleLogging": true,
    "MinimumSeverity": 0
  }
}
```

### Supported Databases

- **SQLite** (default): `"Type": "Sqlite", "Filename": "./conductor.db"`
- **PostgreSQL**: `"Type": "PostgreSql", "ConnectionString": "Host=..."`
- **SQL Server**: `"Type": "SqlServer", "ConnectionString": "Server=..."`
- **MySQL**: `"Type": "MySql", "ConnectionString": "Server=..."`

### CORS Configuration

Cross-Origin Resource Sharing (CORS) can be enabled to allow browser-based applications to access the Conductor API.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable or disable CORS support |
| `AllowedOrigins` | string[] | `[]` | List of allowed origins. Use `["*"]` for all origins |
| `AllowedMethods` | string[] | `["GET", "POST", "PUT", "DELETE", "OPTIONS"]` | Allowed HTTP methods |
| `AllowedHeaders` | string[] | `["Content-Type", "Authorization", ...]` | Allowed request headers |
| `ExposedHeaders` | string[] | `[]` | Headers exposed to the browser |
| `AllowCredentials` | bool | `false` | Allow credentials (cookies, auth headers). Cannot be used with `AllowedOrigins: ["*"]` |
| `MaxAgeSeconds` | int | `86400` | Preflight cache duration (0-86400 seconds) |

**Example: Allow all origins (development)**
```json
{
  "Webserver": {
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": ["*"]
    }
  }
}
```

**Example: Restrict to specific origins (production)**
```json
{
  "Webserver": {
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": ["https://app.example.com", "https://admin.example.com"],
      "AllowCredentials": true
    }
  }
}
```

## Configuration Pinning

Model configurations can define pinned properties that are automatically merged into incoming requests:

```json
{
  "Name": "Low Temperature Config",
  "PinnedCompletionsProperties": {
    "temperature": 0.3,
    "top_p": 0.9,
    "max_tokens": 2048
  },
  "PinnedEmbeddingsProperties": {
    "model": "text-embedding-ada-002"
  }
}
```

When a request comes through a virtual model runner, the pinned properties are merged with the request body, allowing you to enforce specific model parameters.

## Health Checking & Rate Limiting

### Endpoint Health Configuration

Model Runner Endpoints support comprehensive health checking with the following properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HealthCheckUrl` | string | `/` | URL path appended to endpoint base URL for health checks |
| `HealthCheckMethod` | enum | `GET` | HTTP method (`GET` or `HEAD`) |
| `HealthCheckIntervalMs` | int | `5000` | Milliseconds between health checks |
| `HealthCheckTimeoutMs` | int | `5000` | Timeout for health check requests |
| `HealthCheckExpectedStatusCode` | int | `200` | Expected HTTP status code for healthy |
| `UnhealthyThreshold` | int | `2` | Consecutive failures before marking unhealthy |
| `HealthyThreshold` | int | `2` | Consecutive successes before marking healthy |
| `HealthCheckUseAuth` | bool | `false` | Include API key (Bearer token) in health check requests |
| `MaxParallelRequests` | int | `4` | Maximum concurrent requests (0 = unlimited) |
| `Weight` | int | `1` | Relative weight for load balancing (1-1000) |

**Note for OpenAI API**: When using `api.openai.com`, set `HealthCheckUseAuth` to `true` and `HealthCheckUrl` to `/v1/models` to properly authenticate health check requests.

### Health Check Behavior

- Endpoints start in an **unhealthy** state and transition to healthy after meeting the `HealthyThreshold`
- Background tasks continuously monitor each active endpoint at the configured interval
- The proxy automatically excludes unhealthy endpoints from request routing
- When all endpoints are unhealthy, requests return `502 Bad Gateway`
- When all endpoints are at capacity, requests return `429 Too Many Requests`

### Rate Limiting

- Each endpoint tracks in-flight requests in real-time
- The `MaxParallelRequests` property enforces a per-endpoint concurrency limit
- Set to `0` for unlimited concurrent requests
- Requests are counted from start until the response completes (including streaming)

### Weighted Load Balancing

- The `Weight` property influences endpoint selection in round-robin and random modes
- Higher weight = more traffic directed to that endpoint
- Example: Endpoint A (weight=3) receives 3x more traffic than Endpoint B (weight=1)

### Health Status API

Monitor endpoint health via the REST API:

```bash
# Health of all endpoints in tenant
GET /v1.0/modelrunnerendpoints/health

# Health of endpoints for a specific VMR
GET /v1.0/virtualmodelrunners/{id}/health
```

Response includes:
- Current health state (healthy/unhealthy)
- In-flight request count
- Total uptime/downtime
- Uptime percentage
- Last check timestamp
- Last error message (if any)

## Docker Images

- **Server**: `jchristn77/conductor:latest`
- **Dashboard**: `jchristn77/conductor-ui:latest`

### Building Docker Images

```bash
# Build server
./build-server.sh  # or build-server.bat on Windows

# Build dashboard
./build-dashboard.sh  # or build-dashboard.bat on Windows
```

## License

MIT License - see [LICENSE.md](LICENSE.md) for details.

## Attributions

<a href="https://www.flaticon.com/free-icons/music" title="music icons">Music icons created by Freepik - Flaticon</a>
