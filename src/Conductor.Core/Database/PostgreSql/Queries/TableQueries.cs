namespace Conductor.Core.Database.PostgreSql.Queries
{
    using System;

    /// <summary>
    /// PostgreSQL table creation queries.
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
                active BOOLEAN NOT NULL DEFAULT TRUE,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_tenants_name ON tenants(name);
            CREATE INDEX IF NOT EXISTS idx_tenants_active ON tenants(active);
            CREATE INDEX IF NOT EXISTS idx_tenants_createdutc ON tenants(createdutc);
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
                active BOOLEAN NOT NULL DEFAULT TRUE,
                isadmin BOOLEAN NOT NULL DEFAULT FALSE,
                istenantadmin BOOLEAN NOT NULL DEFAULT FALSE,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_users_tenantid ON users(tenantid);
            CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);
            CREATE INDEX IF NOT EXISTS idx_users_active ON users(active);
            CREATE INDEX IF NOT EXISTS idx_users_createdutc ON users(createdutc);
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
                active BOOLEAN NOT NULL DEFAULT TRUE,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE,
                FOREIGN KEY (userid) REFERENCES users(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_credentials_tenantid ON credentials(tenantid);
            CREATE INDEX IF NOT EXISTS idx_credentials_userid ON credentials(userid);
            CREATE INDEX IF NOT EXISTS idx_credentials_bearertoken ON credentials(bearertoken);
            CREATE INDEX IF NOT EXISTS idx_credentials_active ON credentials(active);
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
                port INTEGER NOT NULL,
                apikey VARCHAR(255),
                apitype INTEGER NOT NULL DEFAULT 0,
                usessl BOOLEAN NOT NULL DEFAULT FALSE,
                timeoutms INTEGER NOT NULL DEFAULT 60000,
                active BOOLEAN NOT NULL DEFAULT TRUE,
                healthcheckurl VARCHAR(255) DEFAULT '/',
                healthcheckmethod INTEGER NOT NULL DEFAULT 0,
                healthcheckintervalms INTEGER NOT NULL DEFAULT 5000,
                healthchecktimeoutms INTEGER NOT NULL DEFAULT 5000,
                healthcheckexpectedstatuscode INTEGER NOT NULL DEFAULT 200,
                unhealthythreshold INTEGER NOT NULL DEFAULT 2,
                healthythreshold INTEGER NOT NULL DEFAULT 2,
                healthcheckuseauth BOOLEAN NOT NULL DEFAULT FALSE,
                maxparallelrequests INTEGER NOT NULL DEFAULT 4,
                weight INTEGER NOT NULL DEFAULT 1,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_mre_tenantid ON modelrunnerendpoints(tenantid);
            CREATE INDEX IF NOT EXISTS idx_mre_active ON modelrunnerendpoints(active);
            CREATE INDEX IF NOT EXISTS idx_mre_apitype ON modelrunnerendpoints(apitype);
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
                contextwindowsize INTEGER,
                supportsembeddings BOOLEAN NOT NULL DEFAULT FALSE,
                supportscompletions BOOLEAN NOT NULL DEFAULT TRUE,
                active BOOLEAN NOT NULL DEFAULT TRUE,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_md_tenantid ON modeldefinitions(tenantid);
            CREATE INDEX IF NOT EXISTS idx_md_name ON modeldefinitions(name);
            CREATE INDEX IF NOT EXISTS idx_md_family ON modeldefinitions(family);
            CREATE INDEX IF NOT EXISTS idx_md_active ON modeldefinitions(active);
        ";

        /// <summary>
        /// Create model configurations table.
        /// </summary>
        public static readonly string CreateModelConfigurationsTable = @"
            CREATE TABLE IF NOT EXISTS modelconfigurations (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                name VARCHAR(255) NOT NULL,
                contextwindowsize INTEGER,
                temperature REAL,
                topp REAL,
                topk INTEGER,
                repeatpenalty REAL,
                maxtokens INTEGER,
                model VARCHAR(255),
                pinnedembeddingsproperties TEXT,
                pinnedcompletionsproperties TEXT,
                active BOOLEAN NOT NULL DEFAULT TRUE,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_mc_tenantid ON modelconfigurations(tenantid);
            CREATE INDEX IF NOT EXISTS idx_mc_name ON modelconfigurations(name);
            CREATE INDEX IF NOT EXISTS idx_mc_active ON modelconfigurations(active);
            CREATE INDEX IF NOT EXISTS idx_mc_model ON modelconfigurations(model);
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
                apitype INTEGER NOT NULL DEFAULT 0,
                loadbalancingmode INTEGER NOT NULL DEFAULT 0,
                modelrunnerendpointids TEXT,
                modelconfigurationids TEXT,
                modeldefinitionids TEXT,
                modelconfigurationmappings TEXT,
                timeoutms INTEGER NOT NULL DEFAULT 60000,
                allowembeddings BOOLEAN NOT NULL DEFAULT TRUE,
                allowcompletions BOOLEAN NOT NULL DEFAULT TRUE,
                allowmodelmanagement BOOLEAN NOT NULL DEFAULT FALSE,
                strictmode BOOLEAN NOT NULL DEFAULT FALSE,
                active BOOLEAN NOT NULL DEFAULT TRUE,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_vmr_tenantid ON virtualmodelrunners(tenantid);
            CREATE INDEX IF NOT EXISTS idx_vmr_basepath ON virtualmodelrunners(basepath);
            CREATE INDEX IF NOT EXISTS idx_vmr_active ON virtualmodelrunners(active);
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
                active BOOLEAN NOT NULL DEFAULT TRUE,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_administrators_email ON administrators(email);
            CREATE INDEX IF NOT EXISTS idx_administrators_active ON administrators(active);
        ";
    }
}
