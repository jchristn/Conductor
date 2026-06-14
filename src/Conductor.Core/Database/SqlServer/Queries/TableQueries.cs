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
                servicestate INT NOT NULL DEFAULT 0,
                rigmonitor NVARCHAR(MAX),
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
        /// Create load-balancing policies table.
        /// </summary>
        public static readonly string CreateLoadBalancingPoliciesTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='loadbalancingpolicies' AND xtype='U')
            CREATE TABLE loadbalancingpolicies (
                id NVARCHAR(48) PRIMARY KEY,
                tenantid NVARCHAR(48) NOT NULL,
                name NVARCHAR(255) NOT NULL,
                description NVARCHAR(MAX),
                maxtelemetryagems INT NOT NULL DEFAULT 30000,
                filters NVARCHAR(MAX),
                ranking NVARCHAR(MAX),
                fallbackmode INT NOT NULL DEFAULT 0,
                tiebreaker INT NOT NULL DEFAULT 0,
                active BIT NOT NULL DEFAULT 1,
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL,
                labels NVARCHAR(MAX),
                tags NVARCHAR(MAX),
                metadata NVARCHAR(MAX),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_lbp_tenantid')
            CREATE INDEX idx_lbp_tenantid ON loadbalancingpolicies(tenantid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_lbp_active')
            CREATE INDEX idx_lbp_active ON loadbalancingpolicies(active);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_lbp_name')
            CREATE INDEX idx_lbp_name ON loadbalancingpolicies(name);
        ";

        /// <summary>
        /// Create model access policies table.
        /// </summary>
        public static readonly string CreateModelAccessPoliciesTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='modelaccesspolicies' AND xtype='U')
            CREATE TABLE modelaccesspolicies (
                id NVARCHAR(48) PRIMARY KEY,
                tenantid NVARCHAR(48) NOT NULL,
                name NVARCHAR(255) NOT NULL,
                description NVARCHAR(MAX),
                defaultdecision INT NOT NULL DEFAULT 0,
                active BIT NOT NULL DEFAULT 1,
                labels NVARCHAR(MAX),
                tags NVARCHAR(MAX),
                metadata NVARCHAR(MAX),
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_map_tenantid')
            CREATE INDEX idx_map_tenantid ON modelaccesspolicies(tenantid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_map_active')
            CREATE INDEX idx_map_active ON modelaccesspolicies(active);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_map_name')
            CREATE INDEX idx_map_name ON modelaccesspolicies(name);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_map_lastupdateutc')
            CREATE INDEX idx_map_lastupdateutc ON modelaccesspolicies(lastupdateutc);
        ";

        /// <summary>
        /// Create model access rules table.
        /// </summary>
        public static readonly string CreateModelAccessRulesTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='modelaccessrules' AND xtype='U')
            CREATE TABLE modelaccessrules (
                id NVARCHAR(48) PRIMARY KEY,
                tenantid NVARCHAR(48) NOT NULL,
                policyid NVARCHAR(48) NOT NULL,
                name NVARCHAR(255) NOT NULL,
                description NVARCHAR(MAX),
                priority INT NOT NULL DEFAULT 0,
                effect INT NOT NULL DEFAULT 0,
                subjecttype INT NOT NULL DEFAULT 5,
                subjectid NVARCHAR(255),
                subjectselector NVARCHAR(MAX),
                resourcetype INT NOT NULL DEFAULT 4,
                resourceid NVARCHAR(255),
                resourceselector NVARCHAR(MAX),
                vmrid NVARCHAR(48),
                actions NVARCHAR(MAX),
                active BIT NOT NULL DEFAULT 1,
                createdutc DATETIME2 NOT NULL,
                lastupdateutc DATETIME2 NOT NULL,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE,
                FOREIGN KEY (policyid) REFERENCES modelaccesspolicies(id) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_tenantid')
            CREATE INDEX idx_mar_tenantid ON modelaccessrules(tenantid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_policyid')
            CREATE INDEX idx_mar_policyid ON modelaccessrules(policyid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_active')
            CREATE INDEX idx_mar_active ON modelaccessrules(active);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_priority')
            CREATE INDEX idx_mar_priority ON modelaccessrules(priority);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_effect')
            CREATE INDEX idx_mar_effect ON modelaccessrules(effect);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_subjecttype')
            CREATE INDEX idx_mar_subjecttype ON modelaccessrules(subjecttype);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_subjectid')
            CREATE INDEX idx_mar_subjectid ON modelaccessrules(subjectid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_resourcetype')
            CREATE INDEX idx_mar_resourcetype ON modelaccessrules(resourcetype);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_resourceid')
            CREATE INDEX idx_mar_resourceid ON modelaccessrules(resourceid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_vmrid')
            CREATE INDEX idx_mar_vmrid ON modelaccessrules(vmrid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_mar_lastupdateutc')
            CREATE INDEX idx_mar_lastupdateutc ON modelaccessrules(lastupdateutc);
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
                loadbalancingpolicyid NVARCHAR(48),
                modelaccesspolicyid NVARCHAR(48),
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
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_vmr_modelaccesspolicyid')
            CREATE INDEX idx_vmr_modelaccesspolicyid ON virtualmodelrunners(modelaccesspolicyid);
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
                requestoruserguid NVARCHAR(48),
                requestoruseremail NVARCHAR(255),
                credentialguid NVARCHAR(48),
                credentialname NVARCHAR(255),
                loadbalancingpolicyguid NVARCHAR(48),
                loadbalancingpolicyname NVARCHAR(255),
                modelaccesspolicyguid NVARCHAR(48),
                modelaccesspolicyname NVARCHAR(255),
                modelaccessruleguid NVARCHAR(48),
                modelaccessrulename NVARCHAR(255),
                modelaccessdecision NVARCHAR(32),
                modelaccesswoulddeny BIT NOT NULL DEFAULT 0,
                modelendpointguid NVARCHAR(48),
                modelendpointname NVARCHAR(255),
                modelendpointurl NVARCHAR(MAX),
                modeldefinitionguid NVARCHAR(48),
                modeldefinitionname NVARCHAR(255),
                modelconfigurationguid NVARCHAR(48),
                requestedmodel NVARCHAR(255),
                effectivemodel NVARCHAR(255),
                requesttype NVARCHAR(128),
                routingoutcomecode NVARCHAR(128),
                denialreasoncode NVARCHAR(128),
                denialreason NVARCHAR(MAX),
                sessionaffinityoutcome NVARCHAR(128),
                mutationsummary NVARCHAR(MAX),
                explanationsummary NVARCHAR(MAX),
                requestbodyretained BIT NOT NULL DEFAULT 0,
                requestbodyredacted BIT NOT NULL DEFAULT 0,
                requestheadersredacted BIT NOT NULL DEFAULT 0,
                responsebodyretained BIT NOT NULL DEFAULT 0,
                responsebodyredacted BIT NOT NULL DEFAULT 0,
                responseheadersredacted BIT NOT NULL DEFAULT 0,
                requestorsourceip NVARCHAR(64) NOT NULL,
                httpmethod NVARCHAR(16) NOT NULL,
                httpurl NVARCHAR(MAX) NOT NULL,
                requestbodylength BIGINT NOT NULL,
                responsebodylength BIGINT,
                httpstatus INT,
                firsttokentimems INT,
                responsetimems INT,
                traceid NVARCHAR(48),
                providerrequestid NVARCHAR(255),
                providername NVARCHAR(128),
                prompttokens INT,
                completiontokens INT,
                totaltokens INT,
                tokenspersecondoverall DECIMAL(18,6),
                tokenspersecondgeneration DECIMAL(18,6),
                analyticscaptured BIT NOT NULL DEFAULT 0,
                analyticsversion INT NOT NULL DEFAULT 1,
                dominantstagekind NVARCHAR(128),
                dominantstagedurationms INT,
                analyticsfailurecode NVARCHAR(128),
                objectkey NVARCHAR(255) NOT NULL,
                createdutc DATETIME2 NOT NULL,
                requesttransfertype INT NOT NULL DEFAULT 0,
                responsetransfertype INT NOT NULL DEFAULT 0,
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
        /// Create request analytics events table.
        /// </summary>
        public static readonly string CreateRequestAnalyticsEventsTable = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='requestanalyticsevents' AND xtype='U')
            CREATE TABLE requestanalyticsevents (
                id NVARCHAR(48) PRIMARY KEY,
                tenantguid NVARCHAR(48),
                requesthistoryid NVARCHAR(48),
                traceid NVARCHAR(48),
                virtualmodelrunnerguid NVARCHAR(48),
                virtualmodelrunnername NVARCHAR(255),
                modelendpointguid NVARCHAR(48),
                modelendpointname NVARCHAR(255),
                modelendpointurl NVARCHAR(MAX),
                providername NVARCHAR(128),
                apiformat NVARCHAR(128),
                modelname NVARCHAR(255),
                sequence INT NOT NULL DEFAULT 0,
                stagekind NVARCHAR(128),
                phase NVARCHAR(128),
                stagename NVARCHAR(255),
                startedutc DATETIME2 NOT NULL,
                completedutc DATETIME2,
                durationms INT,
                success BIT NOT NULL DEFAULT 1,
                httpstatus INT,
                errortype NVARCHAR(128),
                errormessage NVARCHAR(MAX),
                endpointlimiterwaitms INT,
                requesttoheadersms INT,
                headerstofirsttokenms INT,
                firsttokentolasttokenms INT,
                clienttotalms INT,
                prompttokens INT,
                completiontokens INT,
                totaltokens INT,
                requestbytes BIGINT,
                responsebytes BIGINT,
                tokenspersecond DECIMAL(18,6),
                rawprovidermetrics NVARCHAR(MAX),
                createdutc DATETIME2 NOT NULL,
                FOREIGN KEY (requesthistoryid) REFERENCES requesthistory(id) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requestanalyticsevents_tenant_created')
            CREATE INDEX idx_requestanalyticsevents_tenant_created ON requestanalyticsevents(tenantguid, createdutc);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requestanalyticsevents_requesthistoryid')
            CREATE INDEX idx_requestanalyticsevents_requesthistoryid ON requestanalyticsevents(requesthistoryid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requestanalyticsevents_traceid')
            CREATE INDEX idx_requestanalyticsevents_traceid ON requestanalyticsevents(traceid);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requestanalyticsevents_stagekind')
            CREATE INDEX idx_requestanalyticsevents_stagekind ON requestanalyticsevents(stagekind);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requestanalyticsevents_endpoint_created')
            CREATE INDEX idx_requestanalyticsevents_endpoint_created ON requestanalyticsevents(modelendpointguid, createdutc);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requestanalyticsevents_vmr_created')
            CREATE INDEX idx_requestanalyticsevents_vmr_created ON requestanalyticsevents(virtualmodelrunnerguid, createdutc);
        ";

        /// <summary>
        /// Add requesthistoryenabled column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddRequestHistoryEnabledColumn = @"
            ALTER TABLE virtualmodelrunners ADD requesthistoryenabled BIT NOT NULL DEFAULT 0;
        ";

        /// <summary>
        /// Add requesttransfertype column to requesthistory table (migration).
        /// </summary>
        public static readonly string AddRequestTransferTypeColumn = @"
            ALTER TABLE requesthistory ADD requesttransfertype INT NOT NULL DEFAULT 0;
        ";

        /// <summary>
        /// Add responsetransfertype column to requesthistory table (migration).
        /// </summary>
        public static readonly string AddResponseTransferTypeColumn = @"
            ALTER TABLE requesthistory ADD responsetransfertype INT NOT NULL DEFAULT 0;
        ";

        /// <summary>
        /// Add firsttokentimems column to requesthistory table (migration).
        /// </summary>
        public static readonly string AddFirstTokenTimeMsColumn = @"
            ALTER TABLE requesthistory ADD firsttokentimems INT;
        ";

        /// <summary>
        /// Add rigmonitor column to modelrunnerendpoints table (migration).
        /// </summary>
        public static readonly string AddRigMonitorColumn = @"
            ALTER TABLE modelrunnerendpoints ADD rigmonitor NVARCHAR(MAX);
        ";

        /// <summary>
        /// Add loadbalancingpolicyid column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddLoadBalancingPolicyIdColumn = @"
            ALTER TABLE virtualmodelrunners ADD loadbalancingpolicyid NVARCHAR(48);
        ";

        /// <summary>
        /// Add modelaccesspolicyid column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddModelAccessPolicyIdColumn = @"
            ALTER TABLE virtualmodelrunners ADD modelaccesspolicyid NVARCHAR(48);
        ";
    }
}
