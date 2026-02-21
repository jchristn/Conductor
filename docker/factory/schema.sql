-- Conductor Factory Database Schema
-- This file creates all tables, indices, and default records
-- matching a clean first-run initialization of Conductor.
--
-- Usage: sqlite3 conductor.db < schema.sql

PRAGMA journal_mode=WAL;
PRAGMA foreign_keys=ON;

-- Tenants
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

-- Users
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

-- Credentials
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

-- Administrators
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

-- Model Runner Endpoints
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

-- Model Definitions
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

-- Model Configurations
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

-- Virtual Model Runners
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
    sessionaffinitymode INTEGER NOT NULL DEFAULT 0,
    sessionaffinityheader TEXT,
    sessiontimeoutms INTEGER NOT NULL DEFAULT 600000,
    sessionmaxentries INTEGER NOT NULL DEFAULT 10000,
    requesthistoryenabled INTEGER NOT NULL DEFAULT 0,
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

-- Request History
CREATE TABLE IF NOT EXISTS requesthistory (
    id TEXT PRIMARY KEY,
    tenantguid TEXT NOT NULL,
    virtualmodelrunnerguid TEXT NOT NULL,
    virtualmodelrunnername TEXT NOT NULL,
    modelendpointguid TEXT,
    modelendpointname TEXT,
    modelendpointurl TEXT,
    modeldefinitionguid TEXT,
    modeldefinitionname TEXT,
    modelconfigurationguid TEXT,
    requestorsourceip TEXT NOT NULL,
    httpmethod TEXT NOT NULL,
    httpurl TEXT NOT NULL,
    requestbodylength INTEGER NOT NULL,
    responsebodylength INTEGER,
    httpstatus INTEGER,
    responsetimems INTEGER,
    objectkey TEXT NOT NULL,
    createdutc TEXT NOT NULL,
    completedutc TEXT,
    FOREIGN KEY (tenantguid) REFERENCES tenants(id),
    FOREIGN KEY (virtualmodelrunnerguid) REFERENCES virtualmodelrunners(id)
);
CREATE INDEX IF NOT EXISTS idx_requesthistory_tenantguid ON requesthistory(tenantguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_vmrguid ON requesthistory(virtualmodelrunnerguid);
CREATE INDEX IF NOT EXISTS idx_requesthistory_createdutc ON requesthistory(createdutc);
CREATE INDEX IF NOT EXISTS idx_requesthistory_httpstatus ON requesthistory(httpstatus);
CREATE INDEX IF NOT EXISTS idx_requesthistory_requestorsourceip ON requesthistory(requestorsourceip);

-- Default Records (matching InitializeFirstRunAsync)
-- Note: The application generates unique K-sortable IDs and a random bearer token
-- on first run. These factory defaults use fixed values for reproducibility.
-- SHA256 of 'password' = 5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8

INSERT INTO administrators (id, email, passwordsha256, firstname, lastname, active, createdutc, lastupdateutc)
VALUES (
    'admin_factory_default_000000000000000000000000',
    'admin@conductor',
    '5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8',
    'System',
    'Administrator',
    1,
    datetime('now'),
    datetime('now')
);

INSERT INTO tenants (id, name, active, createdutc, lastupdateutc, labels, tags)
VALUES (
    'default',
    'Default Tenant',
    1,
    datetime('now'),
    datetime('now'),
    '[]',
    '{}'
);

INSERT INTO users (id, tenantid, firstname, lastname, email, password, active, isadmin, istenantadmin, createdutc, lastupdateutc, labels, tags)
VALUES (
    'usr_factory_default_0000000000000000000000000',
    'default',
    'Admin',
    'User',
    'admin@conductor',
    'password',
    1,
    1,
    1,
    datetime('now'),
    datetime('now'),
    '[]',
    '{}'
);

INSERT INTO credentials (id, tenantid, userid, name, bearertoken, active, createdutc, lastupdateutc, labels, tags)
VALUES (
    'cred_factory_default_000000000000000000000000',
    'default',
    'usr_factory_default_0000000000000000000000000',
    'Default API Key',
    'factory_default_bearer_token_0000000000000000000000000000000000',
    1,
    datetime('now'),
    datetime('now'),
    '[]',
    '{}'
);
