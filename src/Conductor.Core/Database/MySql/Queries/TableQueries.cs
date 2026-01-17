namespace Conductor.Core.Database.MySql.Queries
{
    using System;

    /// <summary>
    /// MySQL table creation queries.
    /// </summary>
    public static class TableQueries
    {
        /// <summary>
        /// Create tenants table.
        /// </summary>
        public static readonly string CreateTenantsTable = @"
            CREATE TABLE IF NOT EXISTS tenants (
                id VARCHAR(48) PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                active TINYINT(1) NOT NULL DEFAULT 1,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                INDEX idx_tenants_name (name),
                INDEX idx_tenants_active (active),
                INDEX idx_tenants_createdutc (createdutc)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Create users table.
        /// </summary>
        public static readonly string CreateUsersTable = @"
            CREATE TABLE IF NOT EXISTS users (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                firstname VARCHAR(255) NOT NULL,
                lastname VARCHAR(255) NOT NULL,
                email VARCHAR(255) NOT NULL,
                password VARCHAR(255) NOT NULL,
                active TINYINT(1) NOT NULL DEFAULT 1,
                isadmin TINYINT(1) NOT NULL DEFAULT 0,
                istenantadmin TINYINT(1) NOT NULL DEFAULT 0,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                INDEX idx_users_tenantid (tenantid),
                INDEX idx_users_email (email),
                INDEX idx_users_active (active),
                INDEX idx_users_createdutc (createdutc),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Create credentials table.
        /// </summary>
        public static readonly string CreateCredentialsTable = @"
            CREATE TABLE IF NOT EXISTS credentials (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                userid VARCHAR(48) NOT NULL,
                name VARCHAR(255),
                bearertoken VARCHAR(255) NOT NULL UNIQUE,
                active TINYINT(1) NOT NULL DEFAULT 1,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                INDEX idx_credentials_tenantid (tenantid),
                INDEX idx_credentials_userid (userid),
                INDEX idx_credentials_bearertoken (bearertoken),
                INDEX idx_credentials_active (active),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE,
                FOREIGN KEY (userid) REFERENCES users(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Create model runner endpoints table.
        /// </summary>
        public static readonly string CreateModelRunnerEndpointsTable = @"
            CREATE TABLE IF NOT EXISTS modelrunnerendpoints (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                name VARCHAR(255) NOT NULL,
                hostname VARCHAR(255) NOT NULL,
                port INT NOT NULL,
                apikey VARCHAR(255),
                apitype INT NOT NULL DEFAULT 0,
                usessl TINYINT(1) NOT NULL DEFAULT 0,
                timeoutms INT NOT NULL DEFAULT 60000,
                active TINYINT(1) NOT NULL DEFAULT 1,
                healthcheckurl VARCHAR(255) DEFAULT '/',
                healthcheckmethod INT NOT NULL DEFAULT 0,
                healthcheckintervalms INT NOT NULL DEFAULT 5000,
                healthchecktimeoutms INT NOT NULL DEFAULT 5000,
                healthcheckexpectedstatuscode INT NOT NULL DEFAULT 200,
                unhealthythreshold INT NOT NULL DEFAULT 2,
                healthythreshold INT NOT NULL DEFAULT 2,
                healthcheckuseauth TINYINT(1) NOT NULL DEFAULT 0,
                maxparallelrequests INT NOT NULL DEFAULT 4,
                weight INT NOT NULL DEFAULT 1,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                INDEX idx_mre_tenantid (tenantid),
                INDEX idx_mre_active (active),
                INDEX idx_mre_apitype (apitype),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Create model definitions table.
        /// </summary>
        public static readonly string CreateModelDefinitionsTable = @"
            CREATE TABLE IF NOT EXISTS modeldefinitions (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                name VARCHAR(255) NOT NULL,
                sourceurl TEXT,
                family VARCHAR(255),
                parametersize VARCHAR(64),
                quantizationlevel VARCHAR(64),
                contextwindowsize INT,
                supportsembeddings TINYINT(1) NOT NULL DEFAULT 0,
                supportscompletions TINYINT(1) NOT NULL DEFAULT 1,
                active TINYINT(1) NOT NULL DEFAULT 1,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                INDEX idx_md_tenantid (tenantid),
                INDEX idx_md_name (name),
                INDEX idx_md_family (family),
                INDEX idx_md_active (active),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Create model configurations table.
        /// </summary>
        public static readonly string CreateModelConfigurationsTable = @"
            CREATE TABLE IF NOT EXISTS modelconfigurations (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                name VARCHAR(255) NOT NULL,
                contextwindowsize INT,
                temperature DOUBLE,
                topp DOUBLE,
                topk INT,
                repeatpenalty DOUBLE,
                maxtokens INT,
                model VARCHAR(255),
                pinnedembeddingsproperties TEXT,
                pinnedcompletionsproperties TEXT,
                active TINYINT(1) NOT NULL DEFAULT 1,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                INDEX idx_mc_tenantid (tenantid),
                INDEX idx_mc_name (name),
                INDEX idx_mc_active (active),
                INDEX idx_mc_model (model),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Create virtual model runners table.
        /// </summary>
        public static readonly string CreateVirtualModelRunnersTable = @"
            CREATE TABLE IF NOT EXISTS virtualmodelrunners (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                name VARCHAR(255) NOT NULL,
                hostname VARCHAR(255),
                basepath VARCHAR(255) NOT NULL UNIQUE,
                apitype INT NOT NULL DEFAULT 0,
                loadbalancingmode INT NOT NULL DEFAULT 0,
                modelrunnerendpointids TEXT,
                modelconfigurationids TEXT,
                modeldefinitionids TEXT,
                modelconfigurationmappings TEXT,
                timeoutms INT NOT NULL DEFAULT 60000,
                allowembeddings TINYINT(1) NOT NULL DEFAULT 1,
                allowcompletions TINYINT(1) NOT NULL DEFAULT 1,
                allowmodelmanagement TINYINT(1) NOT NULL DEFAULT 0,
                strictmode TINYINT(1) NOT NULL DEFAULT 0,
                active TINYINT(1) NOT NULL DEFAULT 1,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                INDEX idx_vmr_tenantid (tenantid),
                INDEX idx_vmr_basepath (basepath),
                INDEX idx_vmr_active (active),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Create administrators table.
        /// </summary>
        public static readonly string CreateAdministratorsTable = @"
            CREATE TABLE IF NOT EXISTS administrators (
                id VARCHAR(48) PRIMARY KEY,
                email VARCHAR(255) NOT NULL UNIQUE,
                passwordsha256 VARCHAR(64) NOT NULL,
                firstname VARCHAR(255),
                lastname VARCHAR(255),
                active TINYINT(1) NOT NULL DEFAULT 1,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                INDEX idx_administrators_email (email),
                INDEX idx_administrators_active (active)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";
    }
}
