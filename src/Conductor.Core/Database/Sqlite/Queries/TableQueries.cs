namespace Conductor.Core.Database.Sqlite.Queries
{
    using System;

    /// <summary>
    /// SQLite table creation queries.
    /// </summary>
    public static class TableQueries
    {
        /// <summary>
        /// Create tenants table.
        /// </summary>
        public static readonly string CreateTenantsTable = @"
            CREATE TABLE IF NOT EXISTS tenants (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                active INTEGER NOT NULL DEFAULT 1,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                firstname TEXT NOT NULL,
                lastname TEXT NOT NULL,
                email TEXT NOT NULL,
                password TEXT NOT NULL,
                active INTEGER NOT NULL DEFAULT 1,
                isadmin INTEGER NOT NULL DEFAULT 0,
                istenantadmin INTEGER NOT NULL DEFAULT 0,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                userid TEXT NOT NULL,
                name TEXT,
                bearertoken TEXT NOT NULL UNIQUE,
                active INTEGER NOT NULL DEFAULT 1,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                name TEXT NOT NULL,
                hostname TEXT NOT NULL,
                port INTEGER NOT NULL,
                apikey TEXT,
                apitype INTEGER NOT NULL DEFAULT 0,
                usessl INTEGER NOT NULL DEFAULT 0,
                timeoutms INTEGER NOT NULL DEFAULT 60000,
                active INTEGER NOT NULL DEFAULT 1,
                healthcheckurl TEXT DEFAULT '/',
                healthcheckmethod INTEGER NOT NULL DEFAULT 0,
                healthcheckintervalms INTEGER NOT NULL DEFAULT 5000,
                healthchecktimeoutms INTEGER NOT NULL DEFAULT 5000,
                healthcheckexpectedstatuscode INTEGER NOT NULL DEFAULT 200,
                unhealthythreshold INTEGER NOT NULL DEFAULT 2,
                healthythreshold INTEGER NOT NULL DEFAULT 2,
                healthcheckuseauth INTEGER NOT NULL DEFAULT 0,
                maxparallelrequests INTEGER NOT NULL DEFAULT 4,
                weight INTEGER NOT NULL DEFAULT 1,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                name TEXT NOT NULL,
                sourceurl TEXT,
                family TEXT,
                parametersize TEXT,
                quantizationlevel TEXT,
                contextwindowsize INTEGER,
                supportsembeddings INTEGER NOT NULL DEFAULT 0,
                supportscompletions INTEGER NOT NULL DEFAULT 1,
                active INTEGER NOT NULL DEFAULT 1,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                name TEXT NOT NULL,
                contextwindowsize INTEGER,
                temperature REAL,
                topp REAL,
                topk INTEGER,
                repeatpenalty REAL,
                maxtokens INTEGER,
                model TEXT,
                pinnedembeddingsproperties TEXT,
                pinnedcompletionsproperties TEXT,
                active INTEGER NOT NULL DEFAULT 1,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                name TEXT NOT NULL,
                hostname TEXT,
                basepath TEXT NOT NULL UNIQUE,
                apitype INTEGER NOT NULL DEFAULT 0,
                loadbalancingmode INTEGER NOT NULL DEFAULT 0,
                modelrunnerendpointids TEXT,
                modelconfigurationids TEXT,
                modeldefinitionids TEXT,
                modelconfigurationmappings TEXT,
                timeoutms INTEGER NOT NULL DEFAULT 60000,
                allowembeddings INTEGER NOT NULL DEFAULT 1,
                allowcompletions INTEGER NOT NULL DEFAULT 1,
                allowmodelmanagement INTEGER NOT NULL DEFAULT 0,
                strictmode INTEGER NOT NULL DEFAULT 0,
                active INTEGER NOT NULL DEFAULT 1,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                email TEXT NOT NULL UNIQUE,
                passwordsha256 TEXT NOT NULL,
                firstname TEXT,
                lastname TEXT,
                active INTEGER NOT NULL DEFAULT 1,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_administrators_email ON administrators(email);
            CREATE INDEX IF NOT EXISTS idx_administrators_active ON administrators(active);
        ";
    }
}
