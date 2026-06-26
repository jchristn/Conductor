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
                servicestate INTEGER NOT NULL DEFAULT 0,
                rigmonitor TEXT,
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
        /// Create load-balancing policies table.
        /// </summary>
        public static readonly string CreateLoadBalancingPoliciesTable = @"
            CREATE TABLE IF NOT EXISTS loadbalancingpolicies (
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                name TEXT NOT NULL,
                description TEXT,
                maxtelemetryagems INTEGER NOT NULL DEFAULT 30000,
                filters TEXT,
                ranking TEXT,
                fallbackmode INTEGER NOT NULL DEFAULT 0,
                tiebreaker INTEGER NOT NULL DEFAULT 0,
                active INTEGER NOT NULL DEFAULT 1,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                name TEXT NOT NULL,
                description TEXT,
                defaultdecision INTEGER NOT NULL DEFAULT 0,
                active INTEGER NOT NULL DEFAULT 1,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                policyid TEXT NOT NULL,
                name TEXT NOT NULL,
                description TEXT,
                priority INTEGER NOT NULL DEFAULT 0,
                effect INTEGER NOT NULL DEFAULT 0,
                subjecttype INTEGER NOT NULL DEFAULT 5,
                subjectid TEXT,
                subjectselector TEXT,
                resourcetype INTEGER NOT NULL DEFAULT 4,
                resourceid TEXT,
                resourceselector TEXT,
                vmrid TEXT,
                actions TEXT,
                active INTEGER NOT NULL DEFAULT 1,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                name TEXT NOT NULL,
                hostname TEXT,
                basepath TEXT NOT NULL UNIQUE,
                apitype INTEGER NOT NULL DEFAULT 0,
                loadbalancingmode INTEGER NOT NULL DEFAULT 0,
                modelrunnerendpointids TEXT,
                adaptiveloadbalancing TEXT,
                endpointgroups TEXT,
                modelconfigurationids TEXT,
                modeldefinitionids TEXT,
                modelconfigurationmappings TEXT,
                timeoutms INTEGER NOT NULL DEFAULT 60000,
                allowembeddings INTEGER NOT NULL DEFAULT 1,
                allowcompletions INTEGER NOT NULL DEFAULT 1,
                allowmodelmanagement INTEGER NOT NULL DEFAULT 0,
                strictmode INTEGER NOT NULL DEFAULT 0,
                sessionaffinitymode INTEGER NOT NULL DEFAULT 0,
                sessionaffinityheader TEXT,
                sessiontimeoutms INTEGER NOT NULL DEFAULT 600000,
                sessionmaxentries INTEGER NOT NULL DEFAULT 10000,
                requesthistoryenabled INTEGER NOT NULL DEFAULT 1,
                loadbalancingpolicyid TEXT,
                modelaccesspolicyid TEXT,
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
            CREATE INDEX IF NOT EXISTS idx_vmr_modelaccesspolicyid ON virtualmodelrunners(modelaccesspolicyid);
        ";

        /// <summary>
        /// Create virtual model runner reservations table.
        /// </summary>
        public static readonly string CreateVirtualModelRunnerReservationsTable = @"
            CREATE TABLE IF NOT EXISTS virtualmodelrunnerreservations (
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                vmrid TEXT NOT NULL,
                name TEXT NOT NULL,
                description TEXT,
                startutc TEXT NOT NULL,
                endutc TEXT NOT NULL,
                admissiondrainleadms INTEGER NOT NULL DEFAULT 0,
                active INTEGER NOT NULL DEFAULT 1,
                createdbyuserid TEXT,
                createdbycredentialid TEXT,
                labels TEXT,
                tags TEXT,
                metadata TEXT,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT NOT NULL,
                reservationid TEXT NOT NULL,
                subjecttype INTEGER NOT NULL,
                subjectid TEXT NOT NULL,
                createdutc TEXT NOT NULL,
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

        /// <summary>
        /// Create request history table.
        /// </summary>
        public static readonly string CreateRequestHistoryTable = @"
            CREATE TABLE IF NOT EXISTS requesthistory (
                id TEXT PRIMARY KEY,
                tenantguid TEXT NOT NULL,
                virtualmodelrunnerguid TEXT NOT NULL,
                virtualmodelrunnername TEXT NOT NULL,
                requestoruserguid TEXT,
                requestoruseremail TEXT,
                credentialguid TEXT,
                credentialname TEXT,
                loadbalancingpolicyguid TEXT,
                loadbalancingpolicyname TEXT,
                modelaccesspolicyguid TEXT,
                modelaccesspolicyname TEXT,
                modelaccessruleguid TEXT,
                modelaccessrulename TEXT,
                modelaccessdecision TEXT,
                modelaccesswoulddeny INTEGER NOT NULL DEFAULT 0,
                modelendpointguid TEXT,
                modelendpointname TEXT,
                modelendpointurl TEXT,
                modeldefinitionguid TEXT,
                modeldefinitionname TEXT,
                modelconfigurationguid TEXT,
                requestedmodel TEXT,
                effectivemodel TEXT,
                requesttype TEXT,
                routingoutcomecode TEXT,
                selectionstrategy TEXT,
                endpointgroupguid TEXT,
                endpointgroupname TEXT,
                backoffreason TEXT,
                adaptiveselection INTEGER NOT NULL DEFAULT 0,
                policyfallbackused INTEGER NOT NULL DEFAULT 0,
                denialreasoncode TEXT,
                denialreason TEXT,
                reservationguid TEXT,
                reservationname TEXT,
                reservationdecision TEXT,
                reservationreasoncode TEXT,
                reservationwindowstartutc TEXT,
                reservationwindowendutc TEXT,
                sessionaffinityoutcome TEXT,
                mutationsummary TEXT,
                explanationsummary TEXT,
                requestbodyretained INTEGER NOT NULL DEFAULT 0,
                requestbodyredacted INTEGER NOT NULL DEFAULT 0,
                requestheadersredacted INTEGER NOT NULL DEFAULT 0,
                responsebodyretained INTEGER NOT NULL DEFAULT 0,
                responsebodyredacted INTEGER NOT NULL DEFAULT 0,
                responseheadersredacted INTEGER NOT NULL DEFAULT 0,
                requestorsourceip TEXT NOT NULL,
                httpmethod TEXT NOT NULL,
                httpurl TEXT NOT NULL,
                requestbodylength INTEGER NOT NULL,
                responsebodylength INTEGER,
                httpstatus INTEGER,
                firsttokentimems INTEGER,
                responsetimems INTEGER,
                traceid TEXT,
                providerrequestid TEXT,
                providername TEXT,
                prompttokens INTEGER,
                completiontokens INTEGER,
                totaltokens INTEGER,
                tokenspersecondoverall REAL,
                tokenspersecondgeneration REAL,
                analyticscaptured INTEGER NOT NULL DEFAULT 0,
                analyticsversion INTEGER NOT NULL DEFAULT 1,
                dominantstagekind TEXT,
                dominantstagedurationms INTEGER,
                analyticsfailurecode TEXT,
                objectkey TEXT NOT NULL,
                createdutc TEXT NOT NULL,
                requesttransfertype INTEGER NOT NULL DEFAULT 0,
                responsetransfertype INTEGER NOT NULL DEFAULT 0,
                completedutc TEXT,
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
                id TEXT PRIMARY KEY,
                tenantguid TEXT,
                requesthistoryid TEXT,
                traceid TEXT,
                virtualmodelrunnerguid TEXT,
                virtualmodelrunnername TEXT,
                modelendpointguid TEXT,
                modelendpointname TEXT,
                modelendpointurl TEXT,
                providername TEXT,
                apiformat TEXT,
                modelname TEXT,
                sequence INTEGER NOT NULL DEFAULT 0,
                stagekind TEXT,
                phase TEXT,
                stagename TEXT,
                startedutc TEXT NOT NULL,
                completedutc TEXT,
                durationms INTEGER,
                success INTEGER NOT NULL DEFAULT 1,
                httpstatus INTEGER,
                errortype TEXT,
                errormessage TEXT,
                reservationguid TEXT,
                reservationname TEXT,
                reservationdecision TEXT,
                reservationreasoncode TEXT,
                reservationwindowstartutc TEXT,
                reservationwindowendutc TEXT,
                endpointlimiterwaitms INTEGER,
                requesttoheadersms INTEGER,
                headerstofirsttokenms INTEGER,
                firsttokentolasttokenms INTEGER,
                clienttotalms INTEGER,
                prompttokens INTEGER,
                completiontokens INTEGER,
                totaltokens INTEGER,
                requestbytes INTEGER,
                responsebytes INTEGER,
                tokenspersecond REAL,
                rawprovidermetrics TEXT,
                createdutc TEXT NOT NULL,
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
                id TEXT PRIMARY KEY,
                tenantid TEXT,
                owneruserid TEXT,
                name TEXT NOT NULL,
                description TEXT,
                scope TEXT NOT NULL DEFAULT 'Tenant',
                queryjson TEXT NOT NULL,
                displaystatejson TEXT,
                labels TEXT,
                tags TEXT,
                createdutc TEXT NOT NULL,
                lastupdateutc TEXT NOT NULL
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
            ALTER TABLE virtualmodelrunners ADD COLUMN requesthistoryenabled INTEGER NOT NULL DEFAULT 1;
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
            ALTER TABLE virtualmodelrunners ADD COLUMN loadbalancingpolicyid TEXT;
        ";

        /// <summary>
        /// Add modelaccesspolicyid column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddModelAccessPolicyIdColumn = @"
            ALTER TABLE virtualmodelrunners ADD COLUMN modelaccesspolicyid TEXT;
        ";

        /// <summary>
        /// Add adaptiveloadbalancing column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddAdaptiveLoadBalancingColumn = @"
            ALTER TABLE virtualmodelrunners ADD COLUMN adaptiveloadbalancing TEXT;
        ";

        /// <summary>
        /// Add endpointgroups column to virtualmodelrunners table (migration).
        /// </summary>
        public static readonly string AddEndpointGroupsColumn = @"
            ALTER TABLE virtualmodelrunners ADD COLUMN endpointgroups TEXT;
        ";
    }
}
