# Request History Feature - Implementation Plan

## Overview

This document outlines the implementation plan for adding a Request History feature to Conductor. This feature allows capturing, persisting, and querying HTTP request/response data for Virtual Model Runners.

---

## Table of Contents

1. [Settings Configuration](#1-settings-configuration)
2. [Database Schema](#2-database-schema)
3. [Core Models](#3-core-models)
4. [Server Components](#4-server-components)
5. [Dashboard Changes](#5-dashboard-changes)
6. [API Endpoints](#6-api-endpoints)
7. [Documentation Updates](#7-documentation-updates)
8. [Postman Collection](#8-postman-collection)
9. [Implementation Order](#9-implementation-order)

---

## 1. Settings Configuration

### 1.1 New Settings in `conductor.json`

Add the following settings to the `ServerSettings` class and `conductor.json`:

```json
{
  "RequestHistory": {
    "Enabled": true,
    "RetentionDays": 7,
    "Directory": "./request-history",
    "CleanupIntervalMinutes": 5,
    "MaxRequestBodyBytes": 65536,
    "MaxResponseBodyBytes": 65536
  }
}
```

### 1.2 Settings Model

**File:** `src/Conductor.Core/Settings/RequestHistorySettings.cs`

| Property | Type | Default | Min | Max | Description |
|----------|------|---------|-----|-----|-------------|
| `Enabled` | `bool` | `false` | - | - | Global enable/disable for request history |
| `RetentionDays` | `int` | `7` | `1` | `365` | Days to retain request history records |
| `Directory` | `string` | `"./request-history"` | - | - | Directory for storing request/response files |
| `CleanupIntervalMinutes` | `int` | `5` | `1` | `1440` | Interval between cleanup runs |
| `MaxRequestBodyBytes` | `int` | `65536` | `1024` | `1048576` | Maximum request body size to capture |
| `MaxResponseBodyBytes` | `int` | `65536` | `1024` | `1048576` | Maximum response body size to capture |

### 1.3 Virtual Model Runner Setting

Add a new property to the `VirtualModelRunner` model:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RequestHistoryEnabled` | `bool` | `false` | Enable request history for this VMR |

---

## 2. Database Schema

### 2.1 New Table: `requesthistory`

Create this table in all four database providers (SQLite, SQL Server, MySQL, PostgreSQL).

#### Column Definitions

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | VARCHAR(64) | NO (PK) | K-sortable identifier (e.g., `req_...`) |
| `tenantguid` | VARCHAR(64) | NO (FK) | Tenant association (inherited from VMR) |
| `virtualmodelrunnerguid` | VARCHAR(64) | NO (FK) | Virtual Model Runner ID |
| `virtualmodelrunnername` | VARCHAR(256) | NO | VMR name at time of request |
| `modelendpointguid` | VARCHAR(64) | YES | Model Endpoint ID (null if not routed) |
| `modelendpointname` | VARCHAR(256) | YES | Endpoint name at time of request |
| `modelendpointurl` | VARCHAR(2048) | YES | Endpoint URL at time of request |
| `modeldefinitionguid` | VARCHAR(64) | YES | Model Definition ID |
| `modeldefinitionname` | VARCHAR(256) | YES | Model name from definition |
| `modelconfigurationguid` | VARCHAR(64) | YES | Model Configuration ID |
| `requestorsourceip` | VARCHAR(64) | NO | Client IP address |
| `httpmethod` | VARCHAR(16) | NO | HTTP method (GET, POST, etc.) |
| `httpurl` | VARCHAR(2048) | NO | Request URL |
| `requestbodylength` | BIGINT | NO | Request body length in bytes |
| `responsebodylength` | BIGINT | YES | Response body length in bytes |
| `httpstatus` | INT | YES | HTTP response status code |
| `responsetimems` | INT | YES | Response time in milliseconds |
| `objectkey` | VARCHAR(256) | NO | Filesystem object key |
| `createdutc` | DATETIME/TIMESTAMP | NO | Record creation timestamp |
| `completedutc` | DATETIME/TIMESTAMP | YES | Response completion timestamp |

#### SQL Scripts

**SQLite:**
```sql
CREATE TABLE IF NOT EXISTS requesthistory (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantguid VARCHAR(64) NOT NULL,
    virtualmodelrunnerguid VARCHAR(64) NOT NULL,
    virtualmodelrunnername VARCHAR(256) NOT NULL,
    modelendpointguid VARCHAR(64),
    modelendpointname VARCHAR(256),
    modelendpointurl VARCHAR(2048),
    modeldefinitionguid VARCHAR(64),
    modeldefinitionname VARCHAR(256),
    modelconfigurationguid VARCHAR(64),
    requestorsourceip VARCHAR(64) NOT NULL,
    httpmethod VARCHAR(16) NOT NULL,
    httpurl VARCHAR(2048) NOT NULL,
    requestbodylength BIGINT NOT NULL,
    responsebodylength BIGINT,
    httpstatus INTEGER,
    responsetimems INTEGER,
    objectkey VARCHAR(256) NOT NULL,
    createdutc TEXT NOT NULL,
    completedutc TEXT,
    FOREIGN KEY (tenantguid) REFERENCES tenants(guid),
    FOREIGN KEY (virtualmodelrunnerguid) REFERENCES virtualmodelrunners(guid)
);

CREATE INDEX IF NOT EXISTS idx_requesthistory_tenantguid ON requesthistory(tenantguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_vmrguid ON requesthistory(virtualmodelrunnerguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_createdutc ON requesthistory(createdutc);
CREATE INDEX IF NOT EXISTS idx_requesthistory_httpstatus ON requesthistory(httpstatus);
CREATE INDEX IF NOT EXISTS idx_requesthistory_requestorsourceip ON requesthistory(requestorsourceip);
```

**SQL Server:**
```sql
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='requesthistory' AND xtype='U')
CREATE TABLE requesthistory (
    id NVARCHAR(64) NOT NULL PRIMARY KEY,
    tenantguid NVARCHAR(64) NOT NULL,
    virtualmodelrunnerguid NVARCHAR(64) NOT NULL,
    virtualmodelrunnername NVARCHAR(256) NOT NULL,
    modelendpointguid NVARCHAR(64),
    modelendpointname NVARCHAR(256),
    modelendpointurl NVARCHAR(MAX),
    modeldefinitionguid NVARCHAR(64),
    modeldefinitionname NVARCHAR(256),
    modelconfigurationguid NVARCHAR(64),
    requestorsourceip NVARCHAR(64) NOT NULL,
    httpmethod NVARCHAR(16) NOT NULL,
    httpurl NVARCHAR(MAX) NOT NULL,
    requestbodylength BIGINT NOT NULL,
    responsebodylength BIGINT,
    httpstatus INT,
    responsetimems INT,
    objectkey NVARCHAR(256) NOT NULL,
    createdutc DATETIME2 NOT NULL,
    completedutc DATETIME2,
    CONSTRAINT FK_requesthistory_tenant FOREIGN KEY (tenantguid) REFERENCES tenants(guid),
    CONSTRAINT FK_requesthistory_vmr FOREIGN KEY (virtualmodelrunnerguid) REFERENCES virtualmodelrunners(guid)
);

CREATE INDEX idx_requesthistory_tenantguid ON requesthistory(tenantguid);
CREATE INDEX idx_requesthistory_vmrguid ON requesthistory(virtualmodelrunnerguid);
CREATE INDEX idx_requesthistory_createdutc ON requesthistory(createdutc);
CREATE INDEX idx_requesthistory_httpstatus ON requesthistory(httpstatus);
CREATE INDEX idx_requesthistory_requestorsourceip ON requesthistory(requestorsourceip);
```

**MySQL:**
```sql
CREATE TABLE IF NOT EXISTS requesthistory (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantguid VARCHAR(64) NOT NULL,
    virtualmodelrunnerguid VARCHAR(64) NOT NULL,
    virtualmodelrunnername VARCHAR(256) NOT NULL,
    modelendpointguid VARCHAR(64),
    modelendpointname VARCHAR(256),
    modelendpointurl TEXT,
    modeldefinitionguid VARCHAR(64),
    modeldefinitionname VARCHAR(256),
    modelconfigurationguid VARCHAR(64),
    requestorsourceip VARCHAR(64) NOT NULL,
    httpmethod VARCHAR(16) NOT NULL,
    httpurl TEXT NOT NULL,
    requestbodylength BIGINT NOT NULL,
    responsebodylength BIGINT,
    httpstatus INT,
    responsetimems INT,
    objectkey VARCHAR(256) NOT NULL,
    createdutc DATETIME(6) NOT NULL,
    completedutc DATETIME(6),
    CONSTRAINT FK_requesthistory_tenant FOREIGN KEY (tenantguid) REFERENCES tenants(guid),
    CONSTRAINT FK_requesthistory_vmr FOREIGN KEY (virtualmodelrunnerguid) REFERENCES virtualmodelrunners(guid)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_requesthistory_tenantguid ON requesthistory(tenantguid);
CREATE INDEX idx_requesthistory_vmrguid ON requesthistory(virtualmodelrunnerguid);
CREATE INDEX idx_requesthistory_createdutc ON requesthistory(createdutc);
CREATE INDEX idx_requesthistory_httpstatus ON requesthistory(httpstatus);
CREATE INDEX idx_requesthistory_requestorsourceip ON requesthistory(requestorsourceip);
```

**PostgreSQL:**
```sql
CREATE TABLE IF NOT EXISTS requesthistory (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantguid VARCHAR(64) NOT NULL,
    virtualmodelrunnerguid VARCHAR(64) NOT NULL,
    virtualmodelrunnername VARCHAR(256) NOT NULL,
    modelendpointguid VARCHAR(64),
    modelendpointname VARCHAR(256),
    modelendpointurl TEXT,
    modeldefinitionguid VARCHAR(64),
    modeldefinitionname VARCHAR(256),
    modelconfigurationguid VARCHAR(64),
    requestorsourceip VARCHAR(64) NOT NULL,
    httpmethod VARCHAR(16) NOT NULL,
    httpurl TEXT NOT NULL,
    requestbodylength BIGINT NOT NULL,
    responsebodylength BIGINT,
    httpstatus INTEGER,
    responsetimems INTEGER,
    objectkey VARCHAR(256) NOT NULL,
    createdutc TIMESTAMP NOT NULL,
    completedutc TIMESTAMP,
    CONSTRAINT FK_requesthistory_tenant FOREIGN KEY (tenantguid) REFERENCES tenants(guid),
    CONSTRAINT FK_requesthistory_vmr FOREIGN KEY (virtualmodelrunnerguid) REFERENCES virtualmodelrunners(guid)
);

CREATE INDEX IF NOT EXISTS idx_requesthistory_tenantguid ON requesthistory(tenantguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_vmrguid ON requesthistory(virtualmodelrunnerguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_createdutc ON requesthistory(createdutc);
CREATE INDEX IF NOT EXISTS idx_requesthistory_httpstatus ON requesthistory(httpstatus);
CREATE INDEX IF NOT EXISTS idx_requesthistory_requestorsourceip ON requesthistory(requestorsourceip);
```

### 2.2 Virtual Model Runner Table Modification

Add column to `virtualmodelrunners` table in all four providers:

```sql
ALTER TABLE virtualmodelrunners ADD COLUMN requesthistoryenabled BOOLEAN DEFAULT 0;
```

---

## 3. Core Models

### 3.1 Request History Database Model

**File:** `src/Conductor.Core/Models/RequestHistoryEntry.cs`

```csharp
namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Represents a request history entry stored in the database.
    /// </summary>
    public class RequestHistoryEntry
    {
        /// <summary>
        /// K-sortable unique identifier (e.g., req_...).
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tenant GUID (inherited from Virtual Model Runner).
        /// </summary>
        public string TenantGuid { get; set; }

        /// <summary>
        /// Virtual Model Runner GUID.
        /// </summary>
        public string VirtualModelRunnerGuid { get; set; }

        /// <summary>
        /// Virtual Model Runner name at time of request.
        /// </summary>
        public string VirtualModelRunnerName { get; set; }

        /// <summary>
        /// Model Endpoint GUID, if routed.
        /// </summary>
        public string ModelEndpointGuid { get; set; }

        /// <summary>
        /// Model Endpoint name at time of request.
        /// </summary>
        public string ModelEndpointName { get; set; }

        /// <summary>
        /// Model Endpoint URL at time of request.
        /// </summary>
        public string ModelEndpointUrl { get; set; }

        /// <summary>
        /// Model Definition GUID.
        /// </summary>
        public string ModelDefinitionGuid { get; set; }

        /// <summary>
        /// Model name from the model definition.
        /// </summary>
        public string ModelDefinitionName { get; set; }

        /// <summary>
        /// Model Configuration GUID.
        /// </summary>
        public string ModelConfigurationGuid { get; set; }

        /// <summary>
        /// Requestor's source IP address.
        /// </summary>
        public string RequestorSourceIp { get; set; }

        /// <summary>
        /// HTTP method (GET, POST, etc.).
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// HTTP request URL.
        /// </summary>
        public string HttpUrl { get; set; }

        /// <summary>
        /// Request body length in bytes.
        /// </summary>
        public long RequestBodyLength { get; set; }

        /// <summary>
        /// Response body length in bytes.
        /// </summary>
        public long? ResponseBodyLength { get; set; }

        /// <summary>
        /// HTTP response status code.
        /// </summary>
        public int? HttpStatus { get; set; }

        /// <summary>
        /// Response time in milliseconds.
        /// </summary>
        public int? ResponseTimeMs { get; set; }

        /// <summary>
        /// Filesystem object key for the full request/response data.
        /// </summary>
        public string ObjectKey { get; set; }

        /// <summary>
        /// Record creation timestamp (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Response completion timestamp (UTC).
        /// </summary>
        public DateTime? CompletedUtc { get; set; }
    }
}
```

### 3.2 Request History Full Detail Model

**File:** `src/Conductor.Core/Models/RequestHistoryDetail.cs`

This model extends `RequestHistoryEntry` with the full request/response data (loaded from filesystem):

```csharp
namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Full request history detail including headers and bodies.
    /// This data is persisted to the filesystem as JSON.
    /// </summary>
    public class RequestHistoryDetail : RequestHistoryEntry
    {
        /// <summary>
        /// HTTP request headers.
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; }

        /// <summary>
        /// HTTP request body (truncated to MaxRequestBodyBytes).
        /// </summary>
        public string RequestBody { get; set; }

        /// <summary>
        /// Indicates if the request body was truncated.
        /// </summary>
        public bool RequestBodyTruncated { get; set; }

        /// <summary>
        /// HTTP response headers.
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; set; }

        /// <summary>
        /// HTTP response body (truncated to MaxResponseBodyBytes).
        /// </summary>
        public string ResponseBody { get; set; }

        /// <summary>
        /// Indicates if the response body was truncated.
        /// </summary>
        public bool ResponseBodyTruncated { get; set; }
    }
}
```

### 3.3 Request History Search Filter

**File:** `src/Conductor.Core/Models/RequestHistorySearchFilter.cs`

```csharp
namespace Conductor.Core.Models
{
    /// <summary>
    /// Filter criteria for searching request history.
    /// </summary>
    public class RequestHistorySearchFilter
    {
        /// <summary>
        /// Filter by tenant GUID.
        /// </summary>
        public string TenantGuid { get; set; }

        /// <summary>
        /// Filter by Virtual Model Runner GUID.
        /// </summary>
        public string VirtualModelRunnerGuid { get; set; }

        /// <summary>
        /// Filter by Model Endpoint GUID.
        /// </summary>
        public string ModelEndpointGuid { get; set; }

        /// <summary>
        /// Filter by requestor source IP.
        /// </summary>
        public string RequestorSourceIp { get; set; }

        /// <summary>
        /// Filter by HTTP status code.
        /// </summary>
        public int? HttpStatus { get; set; }

        /// <summary>
        /// Page number (1-based).
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size. Default is 10.
        /// </summary>
        public int PageSize { get; set; } = 10;
    }
}
```

---

## 4. Server Components

### 4.1 Database Methods

Create request history database methods for all four providers:

**Files:**
- `src/Conductor.Core/Database/Sqlite/Implementations/RequestHistoryMethods.cs`
- `src/Conductor.Core/Database/SqlServer/Implementations/RequestHistoryMethods.cs`
- `src/Conductor.Core/Database/MySql/Implementations/RequestHistoryMethods.cs`
- `src/Conductor.Core/Database/PostgreSql/Implementations/RequestHistoryMethods.cs`

**Methods to implement:**

| Method | Description |
|--------|-------------|
| `CreateAsync(RequestHistoryEntry)` | Insert a new request history record |
| `UpdateAsync(RequestHistoryEntry)` | Update record with response data |
| `GetByIdAsync(string id)` | Get single record by ID |
| `SearchAsync(RequestHistorySearchFilter)` | Search with pagination |
| `CountAsync(RequestHistorySearchFilter)` | Count matching records |
| `DeleteByIdAsync(string id)` | Delete single record |
| `DeleteBulkAsync(RequestHistorySearchFilter)` | Delete matching records |
| `DeleteExpiredAsync(DateTime cutoff)` | Delete records older than cutoff |
| `GetExpiredObjectKeysAsync(DateTime cutoff)` | Get object keys for cleanup |

### 4.2 Request History Service

**File:** `src/Conductor.Server/Services/RequestHistoryService.cs`

Responsibilities:
- Create request history entries at request start
- Update entries with response data
- Persist full detail to filesystem as JSON
- Load full detail from filesystem
- Handle truncation of request/response bodies
- Delete records and associated files

### 4.3 Request History Cleanup Background Service

**File:** `src/Conductor.Server/Services/RequestHistoryCleanupService.cs`

Responsibilities:
- Run on startup and at configured interval
- Query for expired records
- Delete filesystem files
- Delete database records
- Log cleanup activity

```csharp
// Pseudocode structure
public class RequestHistoryCleanupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup
        await CleanupExpiredRecordsAsync(stoppingToken);

        // Then run at configured interval
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(_Settings.CleanupIntervalMinutes), stoppingToken);
            await CleanupExpiredRecordsAsync(stoppingToken);
        }
    }
}
```

### 4.4 Proxy Controller Integration

**File:** `src/Conductor.Server/Controllers/ProxyController.cs`

Modify the proxy flow to:

1. Check if request history is enabled globally AND on the VMR
2. If enabled, create a `RequestHistoryEntry` with request-time data
3. Insert the database record
4. Execute the proxy request
5. Capture response data (status, headers, body with truncation)
6. Update the database record
7. Persist the full `RequestHistoryDetail` to filesystem

**Error handling:** If any request history operation fails, log the error but allow the proxy request to proceed normally.

### 4.5 Request History Controller

**File:** `src/Conductor.Server/Controllers/RequestHistoryController.cs`

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v1.0/requesthistory` | GET | List/search request history (paginated) |
| `/v1.0/requesthistory/{id}` | GET | Get single record (metadata only) |
| `/v1.0/requesthistory/{id}/detail` | GET | Get full detail including headers/bodies |
| `/v1.0/requesthistory/{id}` | DELETE | Delete single record and file |
| `/v1.0/requesthistory/bulk` | DELETE | Bulk delete matching filter |

**Authorization:**
- Admins can see/delete all records
- Tenant admins can only see/delete records within their tenant

---

## 5. Dashboard Changes

### 5.1 New View: Request History

**File:** `dashboard/src/views/RequestHistory.jsx`

Features:
- Paginated table with columns:
  - ID (truncated, with copy button)
  - Created (timestamp)
  - VMR Name
  - Endpoint Name
  - Source IP
  - Method
  - Status
  - Response Time
  - Actions (View, Delete)
- Search/filter controls:
  - Source IP (text input)
  - Virtual Model Runner (dropdown)
  - Model Endpoint (dropdown)
  - HTTP Status (dropdown: All, 2xx, 3xx, 4xx, 5xx, or specific code)
- Pagination controls (Previous, Next, page info)
- Bulk delete button (deletes all matching current filter)

### 5.2 Request History Detail Modal

**File:** `dashboard/src/components/RequestHistoryDetailModal.jsx`

Modal displaying:
- All metadata fields in a formatted layout
- Collapsible sections for:
  - Request Headers (key-value table)
  - Request Body (syntax-highlighted JSON if applicable, with truncation indicator)
  - Response Headers (key-value table)
  - Response Body (syntax-highlighted JSON if applicable, with truncation indicator)
- Close button
- Delete button

### 5.3 Sidebar Update

**File:** `dashboard/src/components/Sidebar.jsx`

Add "Request History" link after "Virtual Model Runners" in the same section.

### 5.4 Virtual Model Runner Form Update

**Files:**
- `dashboard/src/views/VirtualModelRunners.jsx`
- `dashboard/src/components/VirtualModelRunnerForm.jsx` (if exists)

Add toggle for "Enable Request History" in the VMR create/edit form.

### 5.5 API Service Update

**File:** `dashboard/src/services/api.js`

Add methods:
- `getRequestHistory(filter)` - List/search
- `getRequestHistoryDetail(id)` - Get full detail
- `deleteRequestHistory(id)` - Delete single
- `deleteRequestHistoryBulk(filter)` - Bulk delete

---

## 6. API Endpoints

### 6.1 Request History Endpoints

#### List/Search Request History

```
GET /v1.0/requesthistory
```

Query parameters:
| Parameter | Type | Description |
|-----------|------|-------------|
| `vmrGuid` | string | Filter by Virtual Model Runner GUID |
| `endpointGuid` | string | Filter by Model Endpoint GUID |
| `sourceIp` | string | Filter by requestor source IP |
| `httpStatus` | int | Filter by HTTP status code |
| `page` | int | Page number (default: 1) |
| `pageSize` | int | Page size (default: 10, max: 100) |

Response:
```json
{
  "Data": [
    {
      "Id": "req_01HXYZ...",
      "TenantGuid": "...",
      "VirtualModelRunnerGuid": "...",
      "VirtualModelRunnerName": "My VMR",
      "HttpMethod": "POST",
      "HttpUrl": "/v1/chat/completions",
      "HttpStatus": 200,
      "ResponseTimeMs": 1234,
      "CreatedUtc": "2026-02-05T12:00:00Z",
      ...
    }
  ],
  "Page": 1,
  "PageSize": 10,
  "TotalCount": 150,
  "TotalPages": 15
}
```

#### Get Request History Entry

```
GET /v1.0/requesthistory/{id}
```

Returns: `RequestHistoryEntry` (metadata only)

#### Get Request History Detail

```
GET /v1.0/requesthistory/{id}/detail
```

Returns: `RequestHistoryDetail` (includes headers and bodies)

#### Delete Request History Entry

```
DELETE /v1.0/requesthistory/{id}
```

Returns: `204 No Content` on success

#### Bulk Delete Request History

```
DELETE /v1.0/requesthistory/bulk
```

Query parameters: Same as List/Search (filters which records to delete)

Response:
```json
{
  "DeletedCount": 25
}
```

### 6.2 Virtual Model Runner Endpoint Update

Update existing VMR create/update endpoints to accept `RequestHistoryEnabled` property.

---

## 7. Documentation Updates

### 7.1 README.md

Add section describing:
- Request history feature overview
- Configuration options in `conductor.json`
- How to enable on Virtual Model Runners
- Storage location and cleanup behavior

### 7.2 REST_API.md

Add documentation for:
- All new request history endpoints
- Updated VMR endpoints with `RequestHistoryEnabled` property
- Example requests and responses

---

## 8. Postman Collection

**File:** `postman/Conductor.postman_collection.json`

Create a comprehensive Postman collection including:

### 8.1 Existing Endpoints (to document)
- Tenants (CRUD)
- Users (CRUD)
- Administrators (CRUD)
- Credentials (CRUD)
- Model Definitions (CRUD)
- Model Configurations (CRUD)
- Model Runner Endpoints (CRUD + Health)
- Virtual Model Runners (CRUD + Health)
- Backup/Restore
- Proxy endpoints

### 8.2 New Request History Endpoints
- List/Search Request History
- Get Request History Entry
- Get Request History Detail
- Delete Request History Entry
- Bulk Delete Request History

### 8.3 Collection Structure
```
Conductor API
├── Authentication
├── Tenants
│   ├── List Tenants
│   ├── Get Tenant
│   ├── Create Tenant
│   ├── Update Tenant
│   └── Delete Tenant
├── Users
│   └── ...
├── Administrators
│   └── ...
├── Credentials
│   └── ...
├── Model Definitions
│   └── ...
├── Model Configurations
│   └── ...
├── Model Runner Endpoints
│   ├── List Endpoints
│   ├── Get Endpoint
│   ├── Create Endpoint
│   ├── Update Endpoint
│   ├── Delete Endpoint
│   └── Health Check
├── Virtual Model Runners
│   ├── List VMRs
│   ├── Get VMR
│   ├── Create VMR
│   ├── Update VMR
│   ├── Delete VMR
│   └── Health Check
├── Request History
│   ├── List/Search Request History
│   ├── Get Request History Entry
│   ├── Get Request History Detail
│   ├── Delete Request History Entry
│   └── Bulk Delete Request History
├── Backup & Restore
│   ├── Create Backup
│   └── Restore Backup
└── Proxy
    ├── Chat Completions
    ├── Completions
    └── Models
```

---

## 9. Implementation Order

### Phase 1: Core Infrastructure
1. `RequestHistorySettings.cs` - Settings model with validation
2. Update `ServerSettings.cs` to include `RequestHistorySettings`
3. Update `conductor.json` parsing
4. `RequestHistoryEntry.cs` - Database model
5. `RequestHistoryDetail.cs` - Full detail model
6. `RequestHistorySearchFilter.cs` - Search filter model

### Phase 2: Database Layer
7. Add `requesthistoryenabled` column to `virtualmodelrunners` table (all 4 providers)
8. Create `requesthistory` table (all 4 providers)
9. Update `VirtualModelRunner.cs` model
10. Update VMR database methods (all 4 providers)
11. Implement `RequestHistoryMethods.cs` (all 4 providers)

### Phase 3: Server Services
12. `RequestHistoryService.cs` - Core service
13. `RequestHistoryCleanupService.cs` - Background cleanup
14. Register services in DI container

### Phase 4: API Layer
15. `RequestHistoryController.cs` - REST endpoints
16. Update `ProxyController.cs` - Integration with request history
17. Update `VirtualModelRunnerController.cs` - Support `RequestHistoryEnabled`

### Phase 5: Dashboard
18. Update `Sidebar.jsx` - Add navigation
19. `RequestHistory.jsx` - Main view
20. `RequestHistoryDetailModal.jsx` - Detail modal
21. Update `VirtualModelRunners.jsx` - Add toggle
22. Update `api.js` - Add API methods

### Phase 6: Documentation & Postman
23. Update `README.md`
24. Update `REST_API.md`
25. Create `Conductor.postman_collection.json`

### Phase 7: Testing
26. Unit tests for `RequestHistoryService`
27. Unit tests for `RequestHistoryController`
28. Integration tests for database methods
29. Manual end-to-end testing

---

## File Summary

### New Files

| File | Description |
|------|-------------|
| `src/Conductor.Core/Settings/RequestHistorySettings.cs` | Settings model |
| `src/Conductor.Core/Models/RequestHistoryEntry.cs` | Database model |
| `src/Conductor.Core/Models/RequestHistoryDetail.cs` | Full detail model |
| `src/Conductor.Core/Models/RequestHistorySearchFilter.cs` | Search filter |
| `src/Conductor.Core/Database/Sqlite/Implementations/RequestHistoryMethods.cs` | SQLite impl |
| `src/Conductor.Core/Database/SqlServer/Implementations/RequestHistoryMethods.cs` | SQL Server impl |
| `src/Conductor.Core/Database/MySql/Implementations/RequestHistoryMethods.cs` | MySQL impl |
| `src/Conductor.Core/Database/PostgreSql/Implementations/RequestHistoryMethods.cs` | PostgreSQL impl |
| `src/Conductor.Server/Services/RequestHistoryService.cs` | Core service |
| `src/Conductor.Server/Services/RequestHistoryCleanupService.cs` | Background cleanup |
| `src/Conductor.Server/Controllers/RequestHistoryController.cs` | REST controller |
| `dashboard/src/views/RequestHistory.jsx` | Dashboard view |
| `dashboard/src/components/RequestHistoryDetailModal.jsx` | Detail modal |
| `postman/Conductor.postman_collection.json` | Postman collection |

### Modified Files

| File | Changes |
|------|---------|
| `src/Conductor.Core/Settings/ServerSettings.cs` | Add `RequestHistory` property |
| `src/Conductor.Core/Models/VirtualModelRunner.cs` | Add `RequestHistoryEnabled` |
| `src/Conductor.Core/Database/*/Queries/TableQueries.cs` | Add table creation SQL |
| `src/Conductor.Core/Database/*/Implementations/VirtualModelRunnerMethods.cs` | Support new column |
| `src/Conductor.Server/Controllers/ProxyController.cs` | Integrate request history |
| `src/Conductor.Server/Controllers/VirtualModelRunnerController.cs` | Support new property |
| `src/Conductor.Server/ConductorServer.cs` | Register new services |
| `dashboard/src/components/Sidebar.jsx` | Add navigation link |
| `dashboard/src/views/VirtualModelRunners.jsx` | Add enable toggle |
| `dashboard/src/services/api.js` | Add API methods |
| `README.md` | Document feature |
| `REST_API.md` | Document endpoints |

---

## Questions Resolved

| Question | Answer |
|----------|--------|
| File format | JSON |
| Body text search | Deferred (metadata search only) |
| Max body size | 64KB request, 64KB response |
| Compression | No |
| File organization | Flat directory |
| Default page size | 10 |
| Bulk delete | Yes |
| Auth pattern | Same as other APIs (admin/tenant admin) |
| Tenant association | Inherited from VMR |
| Streaming responses | Capture with 64KB truncation |
| File deletion | Yes, with database record |
| Failure behavior | Don't break proxy |
| Cleanup frequency | Configurable (1-1440 minutes) |
| Dashboard placement | After Virtual Model Runners |
| Default retention | 7 days |
