-- Conductor PostgreSQL Docker initialization
-- Creates the current schema and deterministic factory default records.

\set ON_ERROR_STOP on

-- Tenants
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

-- Users
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

-- Credentials
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

-- Model Runner Endpoints
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

-- Model Definitions
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

-- Model Configurations
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

-- Load Balancing Policies
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

-- Model Access Policies
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

-- Model Access Rules
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

-- Virtual Model Runners
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

-- Administrators
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

-- Request History
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
CREATE INDEX IF NOT EXISTS idx_requesthistory_requestoruserguid ON requesthistory(requestoruserguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_credentialguid ON requesthistory(credentialguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_loadbalancingpolicyguid ON requesthistory(loadbalancingpolicyguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_modelaccesspolicyguid ON requesthistory(modelaccesspolicyguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_modelaccessruleguid ON requesthistory(modelaccessruleguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_modelaccessdecision ON requesthistory(modelaccessdecision);
CREATE INDEX IF NOT EXISTS idx_requesthistory_modelaccesswoulddeny ON requesthistory(modelaccesswoulddeny);
CREATE INDEX IF NOT EXISTS idx_requesthistory_requestedmodel ON requesthistory(requestedmodel);
CREATE INDEX IF NOT EXISTS idx_requesthistory_effectivemodel ON requesthistory(effectivemodel);
CREATE INDEX IF NOT EXISTS idx_requesthistory_denialreasoncode ON requesthistory(denialreasoncode);
CREATE INDEX IF NOT EXISTS idx_requesthistory_reservationguid ON requesthistory(reservationguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_reservationreasoncode ON requesthistory(reservationreasoncode);
CREATE INDEX IF NOT EXISTS idx_requesthistory_sessionaffinityoutcome ON requesthistory(sessionaffinityoutcome);
CREATE INDEX IF NOT EXISTS idx_requesthistory_traceid ON requesthistory(traceid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_providerrequestid ON requesthistory(providerrequestid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_providername ON requesthistory(providername);
CREATE INDEX IF NOT EXISTS idx_requesthistory_analyticscaptured ON requesthistory(analyticscaptured);
CREATE INDEX IF NOT EXISTS idx_requesthistory_dominantstagekind ON requesthistory(dominantstagekind);
CREATE INDEX IF NOT EXISTS idx_requesthistory_analyticsfailurecode ON requesthistory(analyticsfailurecode);

-- Request Analytics Events
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

-- Factory default records.
-- SHA256 of 'password' = 5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8

INSERT INTO administrators (id, email, passwordsha256, firstname, lastname, active, createdutc, lastupdateutc)
VALUES (
    'admin_factory_default_000000000000000000000000',
    'admin@conductor',
    '5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8',
    'System',
    'Administrator',
    TRUE,
    now(),
    now()
)
ON CONFLICT DO NOTHING;

INSERT INTO tenants (id, name, active, createdutc, lastupdateutc, labels, tags)
VALUES (
    'default',
    'Default Tenant',
    TRUE,
    now(),
    now(),
    '[]',
    '{}'
)
ON CONFLICT DO NOTHING;

INSERT INTO users (id, tenantid, firstname, lastname, email, password, active, isadmin, istenantadmin, createdutc, lastupdateutc, labels, tags)
VALUES (
    'usr_factory_default_0000000000000000000000000',
    'default',
    'Admin',
    'User',
    'admin@conductor',
    'password',
    TRUE,
    TRUE,
    TRUE,
    now(),
    now(),
    '[]',
    '{}'
)
ON CONFLICT DO NOTHING;

INSERT INTO credentials (id, tenantid, userid, name, bearertoken, active, createdutc, lastupdateutc, labels, tags)
VALUES (
    'cred_factory_default_000000000000000000000000',
    'default',
    'usr_factory_default_0000000000000000000000000',
    'Default API Key',
    'factory_default_bearer_token_0000000000000000000000000000000000',
    TRUE,
    now(),
    now(),
    '[]',
    '{}'
)
ON CONFLICT DO NOTHING;
