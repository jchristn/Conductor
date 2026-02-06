namespace Conductor.Core.Database.SqlServer.Queries
{
    using System;

    /// <summary>
    /// SQL Server table creation queries.
    /// </summary>
    public static class TableQueries
    {
        /// <summary>
        /// Create tenants table.
        /// </summary>
        public static readonly string CreateTenantsTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tenants' AND xtype='U')
            CREATE TABLE tenants (
                id NVARCHAR(48) PRIMARY KEY,
                name NVARCHAR(255) NOT NULL,
                active BIT NOT NULL DEFAULT 1,
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL,
                labels NVARCHAR(MAX),
                tags NVARCHAR(MAX),
                metadata NVARCHAR(MAX)
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tenants_name')
            CREATE INDEX idx_tenants_name ON tenants(name);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tenants_active')
            CREATE INDEX idx_tenants_active ON tenants(active);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tenants_createdutc')
            CREATE INDEX idx_tenants_createdutc ON tenants(createdutc);
        ";

        /// <summary>
        /// Create users table.
        /// </summary>
        public static readonly string CreateUsersTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='users' AND xtype='U')
            CREATE TABLE users (
                id NVARCHAR(48) PRIMARY KEY,
                tenantid NVARCHAR(48) NOT NULL,
                firstname NVARCHAR(255) NOT NULL,
                lastname NVARCHAR(255) NOT NULL,
                email NVARCHAR(255) NOT NULL,
                password NVARCHAR(255) NOT NULL,
                active BIT NOT NULL DEFAULT 1,
                isadmin BIT NOT NULL DEFAULT 0,
                istenantadmin BIT NOT NULL DEFAULT 0,
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL,
                labels NVARCHAR(MAX),
                tags NVARCHAR(MAX),
                metadata NVARCHAR(MAX),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_tenantid')
            CREATE INDEX idx_users_tenantid ON users(tenantid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_email')
            CREATE INDEX idx_users_email ON users(email);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_active')
            CREATE INDEX idx_users_active ON users(active);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_createdutc')
            CREATE INDEX idx_users_createdutc ON users(createdutc);
        ";

        /// <summary>
        /// Create credentials table.
        /// </summary>
        public static readonly string CreateCredentialsTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='credentials' AND xtype='U')
            CREATE TABLE credentials (
                id NVARCHAR(48) PRIMARY KEY,
                tenantid NVARCHAR(48) NOT NULL,
                userid NVARCHAR(48) NOT NULL,
                name NVARCHAR(255),
                bearertoken NVARCHAR(255) NOT NULL UNIQUE,
                active BIT NOT NULL DEFAULT 1,
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL,
                labels NVARCHAR(MAX),
                tags NVARCHAR(MAX),
                metadata NVARCHAR(MAX),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE,
                FOREIGN KEY (userid) REFERENCES users(id)
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentials_tenantid')
            CREATE INDEX idx_credentials_tenantid ON credentials(tenantid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentials_userid')
            CREATE INDEX idx_credentials_userid ON credentials(userid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentials_bearertoken')
            CREATE INDEX idx_credentials_bearertoken ON credentials(bearertoken);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentials_active')
            CREATE INDEX idx_credentials_active ON credentials(active);
        ";

        /// <summary>
        /// Create model runner endpoints table.
        /// </summary>
        public static readonly string CreateModelRunnerEndpointsTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='modelrunnerendpoints' AND xtype='U')
            CREATE TABLE modelrunnerendpoints (
                id NVARCHAR(48) PRIMARY KEY,
                tenantid NVARCHAR(48) NOT NULL,
                name NVARCHAR(255) NOT NULL,
                hostname NVARCHAR(255) NOT NULL,
                port INT NOT NULL,
                apikey NVARCHAR(255),
                apitype INT NOT NULL DEFAULT 0,
                usessl BIT NOT NULL DEFAULT 0,
                timeoutms INT NOT NULL DEFAULT 60000,
                active BIT NOT NULL DEFAULT 1,
                healthcheckurl NVARCHAR(255) DEFAULT '/',
                healthcheckmethod INT NOT NULL DEFAULT 0,
                healthcheckintervalms INT NOT NULL DEFAULT 5000,
                healthchecktimeoutms INT NOT NULL DEFAULT 5000,
                healthcheckexpectedstatuscode INT NOT NULL DEFAULT 200,
                unhealthythreshold INT NOT NULL DEFAULT 2,
                healthythreshold INT NOT NULL DEFAULT 2,
                healthcheckuseauth BIT NOT NULL DEFAULT 0,
                maxparallelrequests INT NOT NULL DEFAULT 4,
                weight INT NOT NULL DEFAULT 1,
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL,
                labels NVARCHAR(MAX),
                tags NVARCHAR(MAX),
                metadata NVARCHAR(MAX),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mre_tenantid')
            CREATE INDEX idx_mre_tenantid ON modelrunnerendpoints(tenantid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mre_active')
            CREATE INDEX idx_mre_active ON modelrunnerendpoints(active);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mre_apitype')
            CREATE INDEX idx_mre_apitype ON modelrunnerendpoints(apitype);
        ";

        /// <summary>
        /// Create model definitions table.
        /// </summary>
        public static readonly string CreateModelDefinitionsTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='modeldefinitions' AND xtype='U')
            CREATE TABLE modeldefinitions (
                id NVARCHAR(48) PRIMARY KEY,
                tenantid NVARCHAR(48) NOT NULL,
                name NVARCHAR(255) NOT NULL,
                sourceurl NVARCHAR(MAX),
                family NVARCHAR(255),
                parametersize NVARCHAR(64),
                quantizationlevel NVARCHAR(64),
                contextwindowsize INT,
                supportsembeddings BIT NOT NULL DEFAULT 0,
                supportscompletions BIT NOT NULL DEFAULT 1,
                active BIT NOT NULL DEFAULT 1,
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL,
                labels NVARCHAR(MAX),
                tags NVARCHAR(MAX),
                metadata NVARCHAR(MAX),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_md_tenantid')
            CREATE INDEX idx_md_tenantid ON modeldefinitions(tenantid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_md_name')
            CREATE INDEX idx_md_name ON modeldefinitions(name);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_md_family')
            CREATE INDEX idx_md_family ON modeldefinitions(family);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_md_active')
            CREATE INDEX idx_md_active ON modeldefinitions(active);
        ";

        /// <summary>
        /// Create model configurations table.
        /// </summary>
        public static readonly string CreateModelConfigurationsTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='modelconfigurations' AND xtype='U')
            CREATE TABLE modelconfigurations (
                id NVARCHAR(48) PRIMARY KEY,
                tenantid NVARCHAR(48) NOT NULL,
                name NVARCHAR(255) NOT NULL,
                contextwindowsize INT,
                temperature FLOAT,
                topp FLOAT,
                topk INT,
                repeatpenalty FLOAT,
                maxtokens INT,
                model NVARCHAR(255),
                pinnedembeddingsproperties NVARCHAR(MAX),
                pinnedcompletionsproperties NVARCHAR(MAX),
                active BIT NOT NULL DEFAULT 1,
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL,
                labels NVARCHAR(MAX),
                tags NVARCHAR(MAX),
                metadata NVARCHAR(MAX),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mc_tenantid')
            CREATE INDEX idx_mc_tenantid ON modelconfigurations(tenantid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mc_name')
            CREATE INDEX idx_mc_name ON modelconfigurations(name);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mc_active')
            CREATE INDEX idx_mc_active ON modelconfigurations(active);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mc_model')
            CREATE INDEX idx_mc_model ON modelconfigurations(model);
        ";

        /// <summary>
        /// Create virtual model runners table.
        /// </summary>
        public static readonly string CreateVirtualModelRunnersTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='virtualmodelrunners' AND xtype='U')
            CREATE TABLE virtualmodelrunners (
                id NVARCHAR(48) PRIMARY KEY,
                tenantid NVARCHAR(48) NOT NULL,
                name NVARCHAR(255) NOT NULL,
                hostname NVARCHAR(255),
                basepath NVARCHAR(255) NOT NULL UNIQUE,
                apitype INT NOT NULL DEFAULT 0,
                loadbalancingmode INT NOT NULL DEFAULT 0,
                modelrunnerendpointids NVARCHAR(MAX),
                modelconfigurationids NVARCHAR(MAX),
                modeldefinitionids NVARCHAR(MAX),
                modelconfigurationmappings NVARCHAR(MAX),
                timeoutms INT NOT NULL DEFAULT 60000,
                allowembeddings BIT NOT NULL DEFAULT 1,
                allowcompletions BIT NOT NULL DEFAULT 1,
                allowmodelmanagement BIT NOT NULL DEFAULT 0,
                strictmode BIT NOT NULL DEFAULT 0,
                sessionaffinitymode INT NOT NULL DEFAULT 0,
                sessionaffinityheader NVARCHAR(255),
                sessiontimeoutms INT NOT NULL DEFAULT 600000,
                sessionmaxentries INT NOT NULL DEFAULT 10000,
                requesthistoryenabled BIT NOT NULL DEFAULT 0,
                active BIT NOT NULL DEFAULT 1,
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL,
                labels NVARCHAR(MAX),
                tags NVARCHAR(MAX),
                metadata NVARCHAR(MAX),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_vmr_tenantid')
            CREATE INDEX idx_vmr_tenantid ON virtualmodelrunners(tenantid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_vmr_basepath')
            CREATE INDEX idx_vmr_basepath ON virtualmodelrunners(basepath);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_vmr_active')
            CREATE INDEX idx_vmr_active ON virtualmodelrunners(active);
        ";

        /// <summary>
        /// Create administrators table.
        /// </summary>
        public static readonly string CreateAdministratorsTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='administrators' AND xtype='U')
            CREATE TABLE administrators (
                id NVARCHAR(48) PRIMARY KEY,
                email NVARCHAR(255) NOT NULL UNIQUE,
                passwordsha256 NVARCHAR(64) NOT NULL,
                firstname NVARCHAR(255),
                lastname NVARCHAR(255),
                active BIT NOT NULL DEFAULT 1,
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_administrators_email')
            CREATE INDEX idx_administrators_email ON administrators(email);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_administrators_active')
            CREATE INDEX idx_administrators_active ON administrators(active);
        ";

        /// <summary>
        /// Create request history table.
        /// </summary>
        public static readonly string CreateRequestHistoryTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='requesthistory' AND xtype='U')
            CREATE TABLE requesthistory (
                id NVARCHAR(48) PRIMARY KEY,
                tenantguid NVARCHAR(48) NOT NULL,
                virtualmodelrunnerguid NVARCHAR(48) NOT NULL,
                virtualmodelrunnername NVARCHAR(255) NOT NULL,
                modelendpointguid NVARCHAR(48),
                modelendpointname NVARCHAR(255),
                modelendpointurl NVARCHAR(MAX),
                modeldefinitionguid NVARCHAR(48),
                modeldefinitionname NVARCHAR(255),
                modelconfigurationguid NVARCHAR(48),
                requestorsourceip NVARCHAR(64) NOT NULL,
                httpmethod NVARCHAR(16) NOT NULL,
                httpurl NVARCHAR(MAX) NOT NULL,
                requestbodylength BIGINT NOT NULL,
                responsebodylength BIGINT,
                httpstatus INT,
                responsetimems INT,
                objectkey NVARCHAR(255) NOT NULL,
                createdutc DATETIME2 NOT NULL,
                completedutc DATETIME2,
                FOREIGN KEY (tenantguid) REFERENCES tenants(id),
                FOREIGN KEY (virtualmodelrunnerguid) REFERENCES virtualmodelrunners(id)
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requesthistory_tenantguid')
            CREATE INDEX idx_requesthistory_tenantguid ON requesthistory(tenantguid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requesthistory_vmrguid')
            CREATE INDEX idx_requesthistory_vmrguid ON requesthistory(virtualmodelrunnerguid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requesthistory_createdutc')
            CREATE INDEX idx_requesthistory_createdutc ON requesthistory(createdutc);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requesthistory_httpstatus')
            CREATE INDEX idx_requesthistory_httpstatus ON requesthistory(httpstatus);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requesthistory_requestorsourceip')
            CREATE INDEX idx_requesthistory_requestorsourceip ON requesthistory(requestorsourceip);
        ";

        /// <summary>
        /// Add requesthistoryenabled column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddRequestHistoryEnabledColumn = @"
            ALTER TABLE virtualmodelrunners ADD requesthistoryenabled BIT NOT NULL DEFAULT 0;
        ";
    }
}
