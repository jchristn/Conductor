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
                servicestate INTEGER NOT NULL DEFAULT 0,
                rigmonitor TEXT,
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
        /// Create load-balancing policies table.
        /// </summary>
        public static readonly string CreateLoadBalancingPoliciesTable = @"
            CREATE TABLE IF NOT EXISTS loadbalancingpolicies (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                maxtelemetryagems INTEGER NOT NULL DEFAULT 30000,
                filters TEXT,
                ranking TEXT,
                fallbackmode INTEGER NOT NULL DEFAULT 0,
                tiebreaker INTEGER NOT NULL DEFAULT 0,
                active BOOLEAN NOT NULL DEFAULT TRUE,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_lbp_tenantid ON loadbalancingpolicies(tenantid);
            CREATE INDEX IF NOT EXISTS idx_lbp_active ON loadbalancingpolicies(active);
            CREATE INDEX IF NOT EXISTS idx_lbp_name ON loadbalancingpolicies(name);
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
                defaultdecision INTEGER NOT NULL DEFAULT 0,
                active BOOLEAN NOT NULL DEFAULT TRUE,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_map_tenantid ON modelaccesspolicies(tenantid);
            CREATE INDEX IF NOT EXISTS idx_map_active ON modelaccesspolicies(active);
            CREATE INDEX IF NOT EXISTS idx_map_name ON modelaccesspolicies(name);
            CREATE INDEX IF NOT EXISTS idx_map_lastupdateutc ON modelaccesspolicies(lastupdateutc);
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
                priority INTEGER NOT NULL DEFAULT 0,
                effect INTEGER NOT NULL DEFAULT 0,
                subjecttype INTEGER NOT NULL DEFAULT 5,
                subjectid VARCHAR(255),
                subjectselector TEXT,
                resourcetype INTEGER NOT NULL DEFAULT 4,
                resourceid VARCHAR(255),
                resourceselector TEXT,
                vmrid VARCHAR(48),
                actions TEXT,
                active BOOLEAN NOT NULL DEFAULT TRUE,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE,
                FOREIGN KEY (policyid) REFERENCES modelaccesspolicies(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_mar_tenantid ON modelaccessrules(tenantid);
            CREATE INDEX IF NOT EXISTS idx_mar_policyid ON modelaccessrules(policyid);
            CREATE INDEX IF NOT EXISTS idx_mar_active ON modelaccessrules(active);
            CREATE INDEX IF NOT EXISTS idx_mar_priority ON modelaccessrules(priority);
            CREATE INDEX IF NOT EXISTS idx_mar_effect ON modelaccessrules(effect);
            CREATE INDEX IF NOT EXISTS idx_mar_subjecttype ON modelaccessrules(subjecttype);
            CREATE INDEX IF NOT EXISTS idx_mar_subjectid ON modelaccessrules(subjectid);
            CREATE INDEX IF NOT EXISTS idx_mar_resourcetype ON modelaccessrules(resourcetype);
            CREATE INDEX IF NOT EXISTS idx_mar_resourceid ON modelaccessrules(resourceid);
            CREATE INDEX IF NOT EXISTS idx_mar_vmrid ON modelaccessrules(vmrid);
            CREATE INDEX IF NOT EXISTS idx_mar_lastupdateutc ON modelaccessrules(lastupdateutc);
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
                sessionaffinitymode INTEGER NOT NULL DEFAULT 0,
                sessionaffinityheader VARCHAR(255),
                sessiontimeoutms INTEGER NOT NULL DEFAULT 600000,
                sessionmaxentries INTEGER NOT NULL DEFAULT 10000,
                requesthistoryenabled BOOLEAN NOT NULL DEFAULT TRUE,
                loadbalancingpolicyid VARCHAR(48),
                modelaccesspolicyid VARCHAR(48),
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
            CREATE INDEX IF NOT EXISTS idx_vmr_modelaccesspolicyid ON virtualmodelrunners(modelaccesspolicyid);
        ";

        /// <summary>
        /// Create virtual model runner reservations table.
        /// </summary>
        public static readonly string CreateVirtualModelRunnerReservationsTable = @"
            CREATE TABLE IF NOT EXISTS virtualmodelrunnerreservations (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                vmrid VARCHAR(48) NOT NULL,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                startutc TIMESTAMP NOT NULL,
                endutc TIMESTAMP NOT NULL,
                admissiondrainleadms INTEGER NOT NULL DEFAULT 0,
                active BOOLEAN NOT NULL DEFAULT TRUE,
                createdbyuserid VARCHAR(48),
                createdbycredentialid VARCHAR(48),
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE,
                FOREIGN KEY (vmrid) REFERENCES virtualmodelrunners(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_vmrr_tenant_vmr_window ON virtualmodelrunnerreservations(tenantid, vmrid, active, startutc, endutc);
            CREATE INDEX IF NOT EXISTS idx_vmrr_tenant_created ON virtualmodelrunnerreservations(tenantid, createdutc);
            CREATE INDEX IF NOT EXISTS idx_vmrr_tenant_name ON virtualmodelrunnerreservations(tenantid, name);
        ";

        /// <summary>
        /// Create virtual model runner reservation subjects table.
        /// </summary>
        public static readonly string CreateVirtualModelRunnerReservationSubjectsTable = @"
            CREATE TABLE IF NOT EXISTS virtualmodelrunnerreservationsubjects (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48) NOT NULL,
                reservationid VARCHAR(48) NOT NULL,
                subjecttype INTEGER NOT NULL,
                subjectid VARCHAR(48) NOT NULL,
                createdutc TIMESTAMP NOT NULL,
                FOREIGN KEY (tenantid) REFERENCES tenants(id) ON DELETE CASCADE,
                FOREIGN KEY (reservationid) REFERENCES virtualmodelrunnerreservations(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_vmrrs_tenant_reservation ON virtualmodelrunnerreservationsubjects(tenantid, reservationid);
            CREATE INDEX IF NOT EXISTS idx_vmrrs_tenant_subject ON virtualmodelrunnerreservationsubjects(tenantid, subjecttype, subjectid);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_vmrrs_unique_subject ON virtualmodelrunnerreservationsubjects(reservationid, subjecttype, subjectid);
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
                reservationguid VARCHAR(48),
                reservationname VARCHAR(255),
                reservationdecision VARCHAR(32),
                reservationreasoncode VARCHAR(128),
                reservationwindowstartutc TIMESTAMP,
                reservationwindowendutc TIMESTAMP,
                sessionaffinityoutcome VARCHAR(128),
                mutationsummary TEXT,
                explanationsummary TEXT,
                requestbodyretained BOOLEAN NOT NULL DEFAULT FALSE,
                requestbodyredacted BOOLEAN NOT NULL DEFAULT FALSE,
                requestheadersredacted BOOLEAN NOT NULL DEFAULT FALSE,
                responsebodyretained BOOLEAN NOT NULL DEFAULT FALSE,
                responsebodyredacted BOOLEAN NOT NULL DEFAULT FALSE,
                responseheadersredacted BOOLEAN NOT NULL DEFAULT FALSE,
                requestorsourceip VARCHAR(64) NOT NULL,
                httpmethod VARCHAR(16) NOT NULL,
                httpurl VARCHAR(2048) NOT NULL,
                requestbodylength BIGINT NOT NULL,
                responsebodylength BIGINT,
                httpstatus INTEGER,
                firsttokentimems INTEGER,
                responsetimems INTEGER,
                traceid VARCHAR(48),
                providerrequestid VARCHAR(255),
                providername VARCHAR(128),
                prompttokens INTEGER,
                completiontokens INTEGER,
                totaltokens INTEGER,
                tokenspersecondoverall NUMERIC(18,6),
                tokenspersecondgeneration NUMERIC(18,6),
                analyticscaptured BOOLEAN NOT NULL DEFAULT FALSE,
                analyticsversion INTEGER NOT NULL DEFAULT 1,
                dominantstagekind VARCHAR(128),
                dominantstagedurationms INTEGER,
                analyticsfailurecode VARCHAR(128),
                objectkey VARCHAR(255) NOT NULL,
                createdutc TIMESTAMP NOT NULL,
                requesttransfertype INTEGER NOT NULL DEFAULT 0,
                responsetransfertype INTEGER NOT NULL DEFAULT 0,
                completedutc TIMESTAMP,
                FOREIGN KEY (tenantguid) REFERENCES tenants(id),
                FOREIGN KEY (virtualmodelrunnerguid) REFERENCES virtualmodelrunners(id)
            );
            CREATE INDEX IF NOT EXISTS idx_requesthistory_tenantguid ON requesthistory(tenantguid);
            CREATE INDEX IF NOT EXISTS idx_requesthistory_vmrguid ON requesthistory(virtualmodelrunnerguid);
            CREATE INDEX IF NOT EXISTS idx_requesthistory_createdutc ON requesthistory(createdutc);
            CREATE INDEX IF NOT EXISTS idx_requesthistory_httpstatus ON requesthistory(httpstatus);
            CREATE INDEX IF NOT EXISTS idx_requesthistory_requestorsourceip ON requesthistory(requestorsourceip);
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
                sequence INTEGER NOT NULL DEFAULT 0,
                stagekind VARCHAR(128),
                phase VARCHAR(128),
                stagename VARCHAR(255),
                startedutc TIMESTAMP NOT NULL,
                completedutc TIMESTAMP,
                durationms INTEGER,
                success BOOLEAN NOT NULL DEFAULT TRUE,
                httpstatus INTEGER,
                errortype VARCHAR(128),
                errormessage TEXT,
                reservationguid VARCHAR(48),
                reservationname VARCHAR(255),
                reservationdecision VARCHAR(32),
                reservationreasoncode VARCHAR(128),
                reservationwindowstartutc TIMESTAMP,
                reservationwindowendutc TIMESTAMP,
                endpointlimiterwaitms INTEGER,
                requesttoheadersms INTEGER,
                headerstofirsttokenms INTEGER,
                firsttokentolasttokenms INTEGER,
                clienttotalms INTEGER,
                prompttokens INTEGER,
                completiontokens INTEGER,
                totaltokens INTEGER,
                requestbytes BIGINT,
                responsebytes BIGINT,
                tokenspersecond NUMERIC(18,6),
                rawprovidermetrics TEXT,
                createdutc TIMESTAMP NOT NULL,
                FOREIGN KEY (requesthistoryid) REFERENCES requesthistory(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_tenant_created ON requestanalyticsevents(tenantguid, createdutc);
            CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_requesthistoryid ON requestanalyticsevents(requesthistoryid);
            CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_traceid ON requestanalyticsevents(traceid);
            CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_stagekind ON requestanalyticsevents(stagekind);
            CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_endpoint_created ON requestanalyticsevents(modelendpointguid, createdutc);
            CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_vmr_created ON requestanalyticsevents(virtualmodelrunnerguid, createdutc);
            CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_reservation_created ON requestanalyticsevents(reservationguid, createdutc);
        ";

        /// <summary>
        /// Create analytics saved reports table.
        /// </summary>
        public static readonly string CreateAnalyticsSavedReportsTable = @"
            CREATE TABLE IF NOT EXISTS analyticssavedreports (
                id VARCHAR(48) PRIMARY KEY,
                tenantid VARCHAR(48),
                owneruserid VARCHAR(48),
                name VARCHAR(255) NOT NULL,
                description TEXT,
                scope VARCHAR(32) NOT NULL DEFAULT 'Tenant',
                queryjson TEXT NOT NULL,
                displaystatejson TEXT,
                labels TEXT,
                tags TEXT,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_asr_tenantid ON analyticssavedreports(tenantid);
            CREATE INDEX IF NOT EXISTS idx_asr_owneruserid ON analyticssavedreports(owneruserid);
            CREATE INDEX IF NOT EXISTS idx_asr_scope ON analyticssavedreports(scope);
            CREATE INDEX IF NOT EXISTS idx_asr_name ON analyticssavedreports(name);
            CREATE INDEX IF NOT EXISTS idx_asr_lastupdateutc ON analyticssavedreports(lastupdateutc);
        ";

        /// <summary>
        /// Add requesthistoryenabled column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddRequestHistoryEnabledColumn = @"
            ALTER TABLE virtualmodelrunners ADD COLUMN requesthistoryenabled BOOLEAN NOT NULL DEFAULT TRUE;
        ";

        /// <summary>
        /// Add requesttransfertype column to requesthistory table (migration).
        /// </summary>
        public static readonly string AddRequestTransferTypeColumn = @"
            ALTER TABLE requesthistory ADD COLUMN requesttransfertype INTEGER NOT NULL DEFAULT 0;
        ";

        /// <summary>
        /// Add responsetransfertype column to requesthistory table (migration).
        /// </summary>
        public static readonly string AddResponseTransferTypeColumn = @"
            ALTER TABLE requesthistory ADD COLUMN responsetransfertype INTEGER NOT NULL DEFAULT 0;
        ";

        /// <summary>
        /// Add firsttokentimems column to requesthistory table (migration).
        /// </summary>
        public static readonly string AddFirstTokenTimeMsColumn = @"
            ALTER TABLE requesthistory ADD COLUMN firsttokentimems INTEGER;
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
