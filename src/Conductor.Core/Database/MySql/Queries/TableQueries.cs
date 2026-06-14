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
                servicestate INT NOT NULL DEFAULT 0,
                rigmonitor TEXT,
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
        /// Create load-balancing policies table.
        /// </summary>
        public static readonly string CreateLoadBalancingPoliciesTable = @"
            CREATE TABLE IF NOT EXISTS loadbalancingpolicies (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                maxtelemetryagems INT NOT NULL DEFAULT 30000,
                filters TEXT,
                ranking TEXT,
                fallbackmode INT NOT NULL DEFAULT 0,
                tiebreaker INT NOT NULL DEFAULT 0,
                active TINYINT(1) NOT NULL DEFAULT 1,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                INDEX idx_lbp_tenantid (tenantid),
                INDEX idx_lbp_active (active),
                INDEX idx_lbp_name (name),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Create model access policies table.
        /// </summary>
        public static readonly string CreateModelAccessPoliciesTable = @"
            CREATE TABLE IF NOT EXISTS modelaccesspolicies (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                defaultdecision INT NOT NULL DEFAULT 0,
                active TINYINT(1) NOT NULL DEFAULT 1,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                INDEX idx_map_tenantid (tenantid),
                INDEX idx_map_active (active),
                INDEX idx_map_name (name),
                INDEX idx_map_lastupdateutc (lastupdateutc),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Create model access rules table.
        /// </summary>
        public static readonly string CreateModelAccessRulesTable = @"
            CREATE TABLE IF NOT EXISTS modelaccessrules (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                policyid VARCHAR(48) NOT NULL,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                priority INT NOT NULL DEFAULT 0,
                effect INT NOT NULL DEFAULT 0,
                subjecttype INT NOT NULL DEFAULT 5,
                subjectid VARCHAR(255),
                subjectselector TEXT,
                resourcetype INT NOT NULL DEFAULT 4,
                resourceid VARCHAR(255),
                resourceselector TEXT,
                vmrid VARCHAR(48),
                actions TEXT,
                active TINYINT(1) NOT NULL DEFAULT 1,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                INDEX idx_mar_tenantid (tenantid),
                INDEX idx_mar_policyid (policyid),
                INDEX idx_mar_active (active),
                INDEX idx_mar_priority (priority),
                INDEX idx_mar_effect (effect),
                INDEX idx_mar_subjecttype (subjecttype),
                INDEX idx_mar_subjectid (subjectid),
                INDEX idx_mar_resourcetype (resourcetype),
                INDEX idx_mar_resourceid (resourceid),
                INDEX idx_mar_vmrid (vmrid),
                INDEX idx_mar_lastupdateutc (lastupdateutc),
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE,
                FOREIGN KEY (policyid) REFERENCES modelaccesspolicies(id) ON DELETE CASCADE
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
                sessionaffinitymode INT NOT NULL DEFAULT 0,
                sessionaffinityheader VARCHAR(255),
                sessiontimeoutms INT NOT NULL DEFAULT 600000,
                sessionmaxentries INT NOT NULL DEFAULT 10000,
                requesthistoryenabled TINYINT(1) NOT NULL DEFAULT 0,
                loadbalancingpolicyid VARCHAR(48),
                modelaccesspolicyid VARCHAR(48),
                active TINYINT(1) NOT NULL DEFAULT 1,
                createdutc DATETIME(3) NOT NULL,
                lastupdateutc DATETIME(3) NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                INDEX idx_vmr_tenantid (tenantid),
                INDEX idx_vmr_basepath (basepath),
                INDEX idx_vmr_active (active),
                INDEX idx_vmr_modelaccesspolicyid (modelaccesspolicyid),
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

        /// <summary>
        /// Create request history table.
        /// </summary>
        public static readonly string CreateRequestHistoryTable = @"
            CREATE TABLE IF NOT EXISTS requesthistory (
                id VARCHAR(48) PRIMARY KEY,
                tenantguid VARCHAR(48) NOT NULL,
                virtualmodelrunnerguid VARCHAR(48) NOT NULL,
                virtualmodelrunnername VARCHAR(255) NOT NULL,
                requestoruserguid VARCHAR(48),
                requestoruseremail VARCHAR(255),
                credentialguid VARCHAR(48),
                credentialname VARCHAR(255),
                loadbalancingpolicyguid VARCHAR(48),
                loadbalancingpolicyname VARCHAR(255),
                modelaccesspolicyguid VARCHAR(48),
                modelaccesspolicyname VARCHAR(255),
                modelaccessruleguid VARCHAR(48),
                modelaccessrulename VARCHAR(255),
                modelaccessdecision VARCHAR(32),
                modelaccesswoulddeny BOOLEAN NOT NULL DEFAULT FALSE,
                modelendpointguid VARCHAR(48),
                modelendpointname VARCHAR(255),
                modelendpointurl VARCHAR(512),
                modeldefinitionguid VARCHAR(48),
                modeldefinitionname VARCHAR(255),
                modelconfigurationguid VARCHAR(48),
                requestedmodel VARCHAR(255),
                effectivemodel VARCHAR(255),
                requesttype VARCHAR(128),
                routingoutcomecode VARCHAR(128),
                denialreasoncode VARCHAR(128),
                denialreason TEXT,
                sessionaffinityoutcome VARCHAR(128),
                mutationsummary TEXT,
                explanationsummary TEXT,
                requestbodyretained TINYINT(1) NOT NULL DEFAULT 0,
                requestbodyredacted TINYINT(1) NOT NULL DEFAULT 0,
                requestheadersredacted TINYINT(1) NOT NULL DEFAULT 0,
                responsebodyretained TINYINT(1) NOT NULL DEFAULT 0,
                responsebodyredacted TINYINT(1) NOT NULL DEFAULT 0,
                responseheadersredacted TINYINT(1) NOT NULL DEFAULT 0,
                requestorsourceip VARCHAR(64) NOT NULL,
                httpmethod VARCHAR(16) NOT NULL,
                httpurl VARCHAR(2048) NOT NULL,
                requestbodylength BIGINT NOT NULL,
                responsebodylength BIGINT,
                httpstatus INT,
                firsttokentimems INT,
                responsetimems INT,
                traceid VARCHAR(48),
                providerrequestid VARCHAR(255),
                providername VARCHAR(128),
                prompttokens INT,
                completiontokens INT,
                totaltokens INT,
                tokenspersecondoverall DECIMAL(18,6),
                tokenspersecondgeneration DECIMAL(18,6),
                analyticscaptured TINYINT(1) NOT NULL DEFAULT 0,
                analyticsversion INT NOT NULL DEFAULT 1,
                dominantstagekind VARCHAR(128),
                dominantstagedurationms INT,
                analyticsfailurecode VARCHAR(128),
                objectkey VARCHAR(512) NOT NULL,
                createdutc DATETIME(6) NOT NULL,
                requesttransfertype INT NOT NULL DEFAULT 0,
                responsetransfertype INT NOT NULL DEFAULT 0,
                completedutc DATETIME(6),
                INDEX idx_requesthistory_tenantguid (tenantguid),
                INDEX idx_requesthistory_vmrguid (virtualmodelrunnerguid),
                INDEX idx_requesthistory_createdutc (createdutc),
                INDEX idx_requesthistory_httpstatus (httpstatus),
                INDEX idx_requesthistory_requestorsourceip (requestorsourceip),
                FOREIGN KEY (tenantguid) REFERENCES tenants(id),
                FOREIGN KEY (virtualmodelrunnerguid) REFERENCES virtualmodelrunners(id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Create request analytics events table.
        /// </summary>
        public static readonly string CreateRequestAnalyticsEventsTable = @"
            CREATE TABLE IF NOT EXISTS requestanalyticsevents (
                id VARCHAR(48) PRIMARY KEY,
                tenantguid VARCHAR(48),
                requesthistoryid VARCHAR(48),
                traceid VARCHAR(48),
                virtualmodelrunnerguid VARCHAR(48),
                virtualmodelrunnername VARCHAR(255),
                modelendpointguid VARCHAR(48),
                modelendpointname VARCHAR(255),
                modelendpointurl VARCHAR(512),
                providername VARCHAR(128),
                apiformat VARCHAR(128),
                modelname VARCHAR(255),
                sequence INT NOT NULL DEFAULT 0,
                stagekind VARCHAR(128),
                phase VARCHAR(128),
                stagename VARCHAR(255),
                startedutc DATETIME(6) NOT NULL,
                completedutc DATETIME(6),
                durationms INT,
                success TINYINT(1) NOT NULL DEFAULT 1,
                httpstatus INT,
                errortype VARCHAR(128),
                errormessage TEXT,
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
                rawprovidermetrics TEXT,
                createdutc DATETIME(6) NOT NULL,
                INDEX idx_requestanalyticsevents_tenant_created (tenantguid, createdutc),
                INDEX idx_requestanalyticsevents_requesthistoryid (requesthistoryid),
                INDEX idx_requestanalyticsevents_traceid (traceid),
                INDEX idx_requestanalyticsevents_stagekind (stagekind),
                INDEX idx_requestanalyticsevents_endpoint_created (modelendpointguid, createdutc),
                INDEX idx_requestanalyticsevents_vmr_created (virtualmodelrunnerguid, createdutc),
                FOREIGN KEY (requesthistoryid) REFERENCES requesthistory(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ";

        /// <summary>
        /// Add requesthistoryenabled column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddRequestHistoryEnabledColumn = @"
            ALTER TABLE virtualmodelrunners ADD COLUMN requesthistoryenabled TINYINT(1) NOT NULL DEFAULT 0;
        ";

        /// <summary>
        /// Add requesttransfertype column to requesthistory table (migration).
        /// </summary>
        public static readonly string AddRequestTransferTypeColumn = @"
            ALTER TABLE requesthistory ADD COLUMN requesttransfertype INT NOT NULL DEFAULT 0;
        ";

        /// <summary>
        /// Add responsetransfertype column to requesthistory table (migration).
        /// </summary>
        public static readonly string AddResponseTransferTypeColumn = @"
            ALTER TABLE requesthistory ADD COLUMN responsetransfertype INT NOT NULL DEFAULT 0;
        ";

        /// <summary>
        /// Add firsttokentimems column to requesthistory table (migration).
        /// </summary>
        public static readonly string AddFirstTokenTimeMsColumn = @"
            ALTER TABLE requesthistory ADD COLUMN firsttokentimems INT;
        ";

        /// <summary>
        /// Add rigmonitor column to modelrunnerendpoints table (migration).
        /// </summary>
        public static readonly string AddRigMonitorColumn = @"
            ALTER TABLE modelrunnerendpoints ADD COLUMN rigmonitor TEXT;
        ";

        /// <summary>
        /// Add loadbalancingpolicyid column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddLoadBalancingPolicyIdColumn = @"
            ALTER TABLE virtualmodelrunners ADD COLUMN loadbalancingpolicyid VARCHAR(48);
        ";

        /// <summary>
        /// Add modelaccesspolicyid column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddModelAccessPolicyIdColumn = @"
            ALTER TABLE virtualmodelrunners ADD COLUMN modelaccesspolicyid VARCHAR(48);
        ";
    }
}
