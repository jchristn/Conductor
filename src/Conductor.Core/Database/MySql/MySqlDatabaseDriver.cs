namespace Conductor.Core.Database.MySql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using global::MySql.Data.MySqlClient;
    using Conductor.Core.Database.MySql.Implementations;
    using Conductor.Core.Database.MySql.Queries;
    using Conductor.Core.Settings;

    /// <summary>
    /// MySQL database driver implementation.
    /// </summary>
    public class MySqlDatabaseDriver : DatabaseDriverBase
    {
        private readonly object _Lock = new object();

        /// <summary>
        /// Instantiate the MySQL database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        public MySqlDatabaseDriver(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ConnectionString = settings.GetConnectionString();
            LogQueries = settings.LogQueries;

            // Initialize method implementations
            Tenant = new TenantMethods(this);
            User = new UserMethods(this);
            Credential = new CredentialMethods(this);
            ModelRunnerEndpoint = new ModelRunnerEndpointMethods(this);
            ModelDefinition = new ModelDefinitionMethods(this);
            ModelConfiguration = new ModelConfigurationMethods(this);
            VirtualModelRunner = new VirtualModelRunnerMethods(this);
            LoadBalancingPolicy = new LoadBalancingPolicyMethods(this);
            ModelAccessPolicy = new Conductor.Core.Database.ModelAccessPolicyMethods(this, Conductor.Core.Database.RequestAnalyticsSqlDialect.MySql);
            Administrator = new AdministratorMethods(this);
            RequestHistory = new RequestHistoryMethods(this);
            RequestAnalytics = new Conductor.Core.Database.RequestAnalyticsMethods(this, Conductor.Core.Database.RequestAnalyticsSqlDialect.MySql);
        }

        /// <summary>
        /// Initialize the database (create tables if needed).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            List<string> queries = new List<string>
            {
                TableQueries.CreateTenantsTable,
                TableQueries.CreateUsersTable,
                TableQueries.CreateCredentialsTable,
                TableQueries.CreateModelRunnerEndpointsTable,
                TableQueries.CreateModelDefinitionsTable,
                TableQueries.CreateModelConfigurationsTable,
                TableQueries.CreateLoadBalancingPoliciesTable,
                TableQueries.CreateModelAccessPoliciesTable,
                TableQueries.CreateModelAccessRulesTable,
                TableQueries.CreateVirtualModelRunnersTable,
                TableQueries.CreateAdministratorsTable,
                TableQueries.CreateRequestHistoryTable,
                TableQueries.CreateRequestAnalyticsEventsTable
            };

            await ExecuteQueriesAsync(queries, false, token).ConfigureAwait(false);

            // Run migrations for existing databases
            await RunMigrationsAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute a query and return results.
        /// </summary>
        /// <param name="query">SQL query.</param>
        /// <param name="isTransaction">Execute within a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with results.</returns>
        public override async Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));

            DataTable result = new DataTable();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                await conn.OpenAsync(token).ConfigureAwait(false);

                MySqlTransaction transaction = null;
                if (isTransaction)
                {
                    transaction = await conn.BeginTransactionAsync(token).ConfigureAwait(false);
                }

                try
                {
                    using (MySqlCommand cmd = new MySqlCommand(query, conn, transaction))
                    {
                        using (MySqlDataReader reader = (MySqlDataReader)await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            result.Load(reader);
                        }
                    }

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                    }
                }
                catch
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    }
                    throw;
                }
                finally
                {
                    if (transaction != null)
                    {
                        transaction.Dispose();
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Execute multiple queries.
        /// </summary>
        /// <param name="queries">SQL queries.</param>
        /// <param name="isTransaction">Execute within a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with last query results.</returns>
        public override async Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            if (queries == null) throw new ArgumentNullException(nameof(queries));

            DataTable result = new DataTable();

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                await conn.OpenAsync(token).ConfigureAwait(false);

                MySqlTransaction transaction = null;
                if (isTransaction)
                {
                    transaction = await conn.BeginTransactionAsync(token).ConfigureAwait(false);
                }

                try
                {
                    foreach (string query in queries)
                    {
                        if (String.IsNullOrEmpty(query)) continue;

                        using (MySqlCommand cmd = new MySqlCommand(query, conn, transaction))
                        {
                            using (MySqlDataReader reader = (MySqlDataReader)await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                            {
                                result = new DataTable();
                                result.Load(reader);
                            }
                        }
                    }

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                    }
                }
                catch
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    }
                    throw;
                }
                finally
                {
                    if (transaction != null)
                    {
                        transaction.Dispose();
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Run migrations for existing databases.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task RunMigrationsAsync(CancellationToken token = default)
        {
            await EnsureColumnAsync("virtualmodelrunners", "requesthistoryenabled", TableQueries.AddRequestHistoryEnabledColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("virtualmodelrunners", "loadbalancingpolicyid", TableQueries.AddLoadBalancingPolicyIdColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("virtualmodelrunners", "modelaccesspolicyid", TableQueries.AddModelAccessPolicyIdColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("virtualmodelrunners", "modelconfigurationmappings", "ALTER TABLE virtualmodelrunners ADD COLUMN modelconfigurationmappings TEXT;", token).ConfigureAwait(false);

            await EnsureColumnAsync("modelrunnerendpoints", "rigmonitor", TableQueries.AddRigMonitorColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("modelrunnerendpoints", "servicestate", "ALTER TABLE modelrunnerendpoints ADD COLUMN servicestate INT NOT NULL DEFAULT 0;", token).ConfigureAwait(false);

            await EnsureColumnAsync("requesthistory", "requesttransfertype", TableQueries.AddRequestTransferTypeColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "responsetransfertype", TableQueries.AddResponseTransferTypeColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "firsttokentimems", TableQueries.AddFirstTokenTimeMsColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestoruserguid", "ALTER TABLE requesthistory ADD COLUMN requestoruserguid VARCHAR(48);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestoruseremail", "ALTER TABLE requesthistory ADD COLUMN requestoruseremail VARCHAR(255);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "credentialguid", "ALTER TABLE requesthistory ADD COLUMN credentialguid VARCHAR(48);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "credentialname", "ALTER TABLE requesthistory ADD COLUMN credentialname VARCHAR(255);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "loadbalancingpolicyguid", "ALTER TABLE requesthistory ADD COLUMN loadbalancingpolicyguid VARCHAR(48);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "loadbalancingpolicyname", "ALTER TABLE requesthistory ADD COLUMN loadbalancingpolicyname VARCHAR(255);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccesspolicyguid", "ALTER TABLE requesthistory ADD COLUMN modelaccesspolicyguid VARCHAR(48);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccesspolicyname", "ALTER TABLE requesthistory ADD COLUMN modelaccesspolicyname VARCHAR(255);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccessruleguid", "ALTER TABLE requesthistory ADD COLUMN modelaccessruleguid VARCHAR(48);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccessrulename", "ALTER TABLE requesthistory ADD COLUMN modelaccessrulename VARCHAR(255);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccessdecision", "ALTER TABLE requesthistory ADD COLUMN modelaccessdecision VARCHAR(32);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccesswoulddeny", "ALTER TABLE requesthistory ADD COLUMN modelaccesswoulddeny BOOLEAN NOT NULL DEFAULT FALSE;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestedmodel", "ALTER TABLE requesthistory ADD COLUMN requestedmodel VARCHAR(255);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "effectivemodel", "ALTER TABLE requesthistory ADD COLUMN effectivemodel VARCHAR(255);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requesttype", "ALTER TABLE requesthistory ADD COLUMN requesttype VARCHAR(128);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "routingoutcomecode", "ALTER TABLE requesthistory ADD COLUMN routingoutcomecode VARCHAR(128);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "denialreasoncode", "ALTER TABLE requesthistory ADD COLUMN denialreasoncode VARCHAR(128);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "denialreason", "ALTER TABLE requesthistory ADD COLUMN denialreason TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "sessionaffinityoutcome", "ALTER TABLE requesthistory ADD COLUMN sessionaffinityoutcome VARCHAR(128);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "mutationsummary", "ALTER TABLE requesthistory ADD COLUMN mutationsummary TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "explanationsummary", "ALTER TABLE requesthistory ADD COLUMN explanationsummary TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestbodyretained", "ALTER TABLE requesthistory ADD COLUMN requestbodyretained TINYINT(1) NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestbodyredacted", "ALTER TABLE requesthistory ADD COLUMN requestbodyredacted TINYINT(1) NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestheadersredacted", "ALTER TABLE requesthistory ADD COLUMN requestheadersredacted TINYINT(1) NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "responsebodyretained", "ALTER TABLE requesthistory ADD COLUMN responsebodyretained TINYINT(1) NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "responsebodyredacted", "ALTER TABLE requesthistory ADD COLUMN responsebodyredacted TINYINT(1) NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "responseheadersredacted", "ALTER TABLE requesthistory ADD COLUMN responseheadersredacted TINYINT(1) NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "traceid", "ALTER TABLE requesthistory ADD COLUMN traceid VARCHAR(48);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "providerrequestid", "ALTER TABLE requesthistory ADD COLUMN providerrequestid VARCHAR(255);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "providername", "ALTER TABLE requesthistory ADD COLUMN providername VARCHAR(128);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "prompttokens", "ALTER TABLE requesthistory ADD COLUMN prompttokens INT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "completiontokens", "ALTER TABLE requesthistory ADD COLUMN completiontokens INT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "totaltokens", "ALTER TABLE requesthistory ADD COLUMN totaltokens INT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "tokenspersecondoverall", "ALTER TABLE requesthistory ADD COLUMN tokenspersecondoverall DECIMAL(18,6);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "tokenspersecondgeneration", "ALTER TABLE requesthistory ADD COLUMN tokenspersecondgeneration DECIMAL(18,6);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "analyticscaptured", "ALTER TABLE requesthistory ADD COLUMN analyticscaptured TINYINT(1) NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "analyticsversion", "ALTER TABLE requesthistory ADD COLUMN analyticsversion INT NOT NULL DEFAULT 1;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "dominantstagekind", "ALTER TABLE requesthistory ADD COLUMN dominantstagekind VARCHAR(128);", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "dominantstagedurationms", "ALTER TABLE requesthistory ADD COLUMN dominantstagedurationms INT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "analyticsfailurecode", "ALTER TABLE requesthistory ADD COLUMN analyticsfailurecode VARCHAR(128);", token).ConfigureAwait(false);

            await EnsureIndexAsync("idx_requesthistory_requestoruserguid", "CREATE INDEX idx_requesthistory_requestoruserguid ON requesthistory(requestoruserguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_credentialguid", "CREATE INDEX idx_requesthistory_credentialguid ON requesthistory(credentialguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_loadbalancingpolicyguid", "CREATE INDEX idx_requesthistory_loadbalancingpolicyguid ON requesthistory(loadbalancingpolicyguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_modelaccesspolicyguid", "CREATE INDEX idx_requesthistory_modelaccesspolicyguid ON requesthistory(modelaccesspolicyguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_modelaccessruleguid", "CREATE INDEX idx_requesthistory_modelaccessruleguid ON requesthistory(modelaccessruleguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_modelaccessdecision", "CREATE INDEX idx_requesthistory_modelaccessdecision ON requesthistory(modelaccessdecision);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_modelaccesswoulddeny", "CREATE INDEX idx_requesthistory_modelaccesswoulddeny ON requesthistory(modelaccesswoulddeny);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_requestedmodel", "CREATE INDEX idx_requesthistory_requestedmodel ON requesthistory(requestedmodel);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_effectivemodel", "CREATE INDEX idx_requesthistory_effectivemodel ON requesthistory(effectivemodel);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_denialreasoncode", "CREATE INDEX idx_requesthistory_denialreasoncode ON requesthistory(denialreasoncode);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_sessionaffinityoutcome", "CREATE INDEX idx_requesthistory_sessionaffinityoutcome ON requesthistory(sessionaffinityoutcome);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_traceid", "CREATE INDEX idx_requesthistory_traceid ON requesthistory(traceid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_providerrequestid", "CREATE INDEX idx_requesthistory_providerrequestid ON requesthistory(providerrequestid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_providername", "CREATE INDEX idx_requesthistory_providername ON requesthistory(providername);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_analyticscaptured", "CREATE INDEX idx_requesthistory_analyticscaptured ON requesthistory(analyticscaptured);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_dominantstagekind", "CREATE INDEX idx_requesthistory_dominantstagekind ON requesthistory(dominantstagekind);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_analyticsfailurecode", "CREATE INDEX idx_requesthistory_analyticsfailurecode ON requesthistory(analyticsfailurecode);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_tenant_created", "CREATE INDEX idx_requestanalyticsevents_tenant_created ON requestanalyticsevents(tenantguid, createdutc);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_requesthistoryid", "CREATE INDEX idx_requestanalyticsevents_requesthistoryid ON requestanalyticsevents(requesthistoryid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_traceid", "CREATE INDEX idx_requestanalyticsevents_traceid ON requestanalyticsevents(traceid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_stagekind", "CREATE INDEX idx_requestanalyticsevents_stagekind ON requestanalyticsevents(stagekind);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_endpoint_created", "CREATE INDEX idx_requestanalyticsevents_endpoint_created ON requestanalyticsevents(modelendpointguid, createdutc);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_vmr_created", "CREATE INDEX idx_requestanalyticsevents_vmr_created ON requestanalyticsevents(virtualmodelrunnerguid, createdutc);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_vmr_modelaccesspolicyid", "CREATE INDEX idx_vmr_modelaccesspolicyid ON virtualmodelrunners(modelaccesspolicyid);", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a column exists in a table.
        /// </summary>
        /// <param name="tableName">Table name.</param>
        /// <param name="columnName">Column name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the column exists.</returns>
        private async Task<bool> ColumnExistsAsync(string tableName, string columnName, CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) AS cnt FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '" + Sanitize(tableName) + "' AND COLUMN_NAME = '" + Sanitize(columnName) + "';";
            DataTable result = await ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        private async Task<bool> IndexExistsAsync(string indexName, CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) AS cnt FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME = '" + Sanitize(indexName) + "';";
            DataTable result = await ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        private async Task EnsureColumnAsync(string tableName, string columnName, string query, CancellationToken token = default)
        {
            bool exists = await ColumnExistsAsync(tableName, columnName, token).ConfigureAwait(false);
            if (exists)
            {
                return;
            }

            try
            {
                await ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private async Task EnsureIndexAsync(string indexName, string query, CancellationToken token = default)
        {
            bool exists = await IndexExistsAsync(indexName, token).ConfigureAwait(false);
            if (exists)
            {
                return;
            }

            try
            {
                await ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }
}
