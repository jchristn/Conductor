namespace Conductor.Core.Database.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Conductor.Core.Database.Sqlite.Implementations;
    using Conductor.Core.Database.Sqlite.Queries;
    using Conductor.Core.Settings;

    /// <summary>
    /// SQLite database driver implementation.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        private readonly SemaphoreSlim _Gate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Instantiate the SQLite database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        public SqliteDatabaseDriver(DatabaseSettings settings)
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
            VirtualModelRunnerReservation = new Conductor.Core.Database.VirtualModelRunnerReservationMethods(this, Conductor.Core.Database.RequestAnalyticsSqlDialect.Sqlite);
            LoadBalancingPolicy = new LoadBalancingPolicyMethods(this);
            ModelAccessPolicy = new Conductor.Core.Database.ModelAccessPolicyMethods(this, Conductor.Core.Database.RequestAnalyticsSqlDialect.Sqlite);
            Administrator = new AdministratorMethods(this);
            RequestHistory = new RequestHistoryMethods(this);
            RequestAnalytics = new Conductor.Core.Database.RequestAnalyticsMethods(this, Conductor.Core.Database.RequestAnalyticsSqlDialect.Sqlite);
            AnalyticsSavedReport = new Conductor.Core.Database.AnalyticsSavedReportMethods(this, Conductor.Core.Database.RequestAnalyticsSqlDialect.Sqlite);
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
                TableQueries.CreateVirtualModelRunnerReservationsTable,
                TableQueries.CreateVirtualModelRunnerReservationSubjectsTable,
                TableQueries.CreateAdministratorsTable,
                TableQueries.CreateRequestHistoryTable,
                TableQueries.CreateRequestAnalyticsEventsTable,
                TableQueries.CreateAnalyticsSavedReportsTable
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

            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (SqliteConnection conn = new SqliteConnection(ConnectionString))
                {
                    await conn.OpenAsync(token).ConfigureAwait(false);

                    SqliteTransaction transaction = null;
                    if (isTransaction)
                    {
                        transaction = conn.BeginTransaction();
                    }

                    try
                    {
                        using (SqliteCommand cmd = new SqliteCommand(query, conn, transaction))
                        {
                            using (SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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
            }
            finally
            {
                _Gate.Release();
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

            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (SqliteConnection conn = new SqliteConnection(ConnectionString))
                {
                    await conn.OpenAsync(token).ConfigureAwait(false);

                    SqliteTransaction transaction = null;
                    if (isTransaction)
                    {
                        transaction = conn.BeginTransaction();
                    }

                    try
                    {
                        foreach (string query in queries)
                        {
                            if (String.IsNullOrEmpty(query)) continue;

                            using (SqliteCommand cmd = new SqliteCommand(query, conn, transaction))
                            {
                                using (SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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
            }
            finally
            {
                _Gate.Release();
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
            await EnsureColumnAsync("virtualmodelrunners", "adaptiveloadbalancing", TableQueries.AddAdaptiveLoadBalancingColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("virtualmodelrunners", "endpointgroups", TableQueries.AddEndpointGroupsColumn, token).ConfigureAwait(false);

            await EnsureColumnAsync("modelrunnerendpoints", "rigmonitor", TableQueries.AddRigMonitorColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("modelrunnerendpoints", "servicestate", "ALTER TABLE modelrunnerendpoints ADD COLUMN servicestate INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);

            await EnsureColumnAsync("requesthistory", "requesttransfertype", TableQueries.AddRequestTransferTypeColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "responsetransfertype", TableQueries.AddResponseTransferTypeColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "firsttokentimems", TableQueries.AddFirstTokenTimeMsColumn, token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestoruserguid", "ALTER TABLE requesthistory ADD COLUMN requestoruserguid TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestoruseremail", "ALTER TABLE requesthistory ADD COLUMN requestoruseremail TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "credentialguid", "ALTER TABLE requesthistory ADD COLUMN credentialguid TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "credentialname", "ALTER TABLE requesthistory ADD COLUMN credentialname TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "loadbalancingpolicyguid", "ALTER TABLE requesthistory ADD COLUMN loadbalancingpolicyguid TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "loadbalancingpolicyname", "ALTER TABLE requesthistory ADD COLUMN loadbalancingpolicyname TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccesspolicyguid", "ALTER TABLE requesthistory ADD COLUMN modelaccesspolicyguid TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccesspolicyname", "ALTER TABLE requesthistory ADD COLUMN modelaccesspolicyname TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccessruleguid", "ALTER TABLE requesthistory ADD COLUMN modelaccessruleguid TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccessrulename", "ALTER TABLE requesthistory ADD COLUMN modelaccessrulename TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccessdecision", "ALTER TABLE requesthistory ADD COLUMN modelaccessdecision TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "modelaccesswoulddeny", "ALTER TABLE requesthistory ADD COLUMN modelaccesswoulddeny INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestedmodel", "ALTER TABLE requesthistory ADD COLUMN requestedmodel TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "effectivemodel", "ALTER TABLE requesthistory ADD COLUMN effectivemodel TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requesttype", "ALTER TABLE requesthistory ADD COLUMN requesttype TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "routingoutcomecode", "ALTER TABLE requesthistory ADD COLUMN routingoutcomecode TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "selectionstrategy", "ALTER TABLE requesthistory ADD COLUMN selectionstrategy TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "endpointgroupguid", "ALTER TABLE requesthistory ADD COLUMN endpointgroupguid TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "endpointgroupname", "ALTER TABLE requesthistory ADD COLUMN endpointgroupname TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "backoffreason", "ALTER TABLE requesthistory ADD COLUMN backoffreason TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "adaptiveselection", "ALTER TABLE requesthistory ADD COLUMN adaptiveselection INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "policyfallbackused", "ALTER TABLE requesthistory ADD COLUMN policyfallbackused INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "denialreasoncode", "ALTER TABLE requesthistory ADD COLUMN denialreasoncode TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "denialreason", "ALTER TABLE requesthistory ADD COLUMN denialreason TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "reservationguid", "ALTER TABLE requesthistory ADD COLUMN reservationguid TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "reservationname", "ALTER TABLE requesthistory ADD COLUMN reservationname TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "reservationdecision", "ALTER TABLE requesthistory ADD COLUMN reservationdecision TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "reservationreasoncode", "ALTER TABLE requesthistory ADD COLUMN reservationreasoncode TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "reservationwindowstartutc", "ALTER TABLE requesthistory ADD COLUMN reservationwindowstartutc TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "reservationwindowendutc", "ALTER TABLE requesthistory ADD COLUMN reservationwindowendutc TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "sessionaffinityoutcome", "ALTER TABLE requesthistory ADD COLUMN sessionaffinityoutcome TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "mutationsummary", "ALTER TABLE requesthistory ADD COLUMN mutationsummary TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "explanationsummary", "ALTER TABLE requesthistory ADD COLUMN explanationsummary TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestbodyretained", "ALTER TABLE requesthistory ADD COLUMN requestbodyretained INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestbodyredacted", "ALTER TABLE requesthistory ADD COLUMN requestbodyredacted INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "requestheadersredacted", "ALTER TABLE requesthistory ADD COLUMN requestheadersredacted INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "responsebodyretained", "ALTER TABLE requesthistory ADD COLUMN responsebodyretained INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "responsebodyredacted", "ALTER TABLE requesthistory ADD COLUMN responsebodyredacted INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "responseheadersredacted", "ALTER TABLE requesthistory ADD COLUMN responseheadersredacted INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "traceid", "ALTER TABLE requesthistory ADD COLUMN traceid TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "providerrequestid", "ALTER TABLE requesthistory ADD COLUMN providerrequestid TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "providername", "ALTER TABLE requesthistory ADD COLUMN providername TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "prompttokens", "ALTER TABLE requesthistory ADD COLUMN prompttokens INTEGER;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "completiontokens", "ALTER TABLE requesthistory ADD COLUMN completiontokens INTEGER;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "totaltokens", "ALTER TABLE requesthistory ADD COLUMN totaltokens INTEGER;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "tokenspersecondoverall", "ALTER TABLE requesthistory ADD COLUMN tokenspersecondoverall REAL;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "tokenspersecondgeneration", "ALTER TABLE requesthistory ADD COLUMN tokenspersecondgeneration REAL;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "analyticscaptured", "ALTER TABLE requesthistory ADD COLUMN analyticscaptured INTEGER NOT NULL DEFAULT 0;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "analyticsversion", "ALTER TABLE requesthistory ADD COLUMN analyticsversion INTEGER NOT NULL DEFAULT 1;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "dominantstagekind", "ALTER TABLE requesthistory ADD COLUMN dominantstagekind TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "dominantstagedurationms", "ALTER TABLE requesthistory ADD COLUMN dominantstagedurationms INTEGER;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requesthistory", "analyticsfailurecode", "ALTER TABLE requesthistory ADD COLUMN analyticsfailurecode TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requestanalyticsevents", "reservationguid", "ALTER TABLE requestanalyticsevents ADD COLUMN reservationguid TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requestanalyticsevents", "reservationname", "ALTER TABLE requestanalyticsevents ADD COLUMN reservationname TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requestanalyticsevents", "reservationdecision", "ALTER TABLE requestanalyticsevents ADD COLUMN reservationdecision TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requestanalyticsevents", "reservationreasoncode", "ALTER TABLE requestanalyticsevents ADD COLUMN reservationreasoncode TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requestanalyticsevents", "reservationwindowstartutc", "ALTER TABLE requestanalyticsevents ADD COLUMN reservationwindowstartutc TEXT;", token).ConfigureAwait(false);
            await EnsureColumnAsync("requestanalyticsevents", "reservationwindowendutc", "ALTER TABLE requestanalyticsevents ADD COLUMN reservationwindowendutc TEXT;", token).ConfigureAwait(false);

            await EnsureIndexAsync("idx_requesthistory_requestoruserguid", "CREATE INDEX IF NOT EXISTS idx_requesthistory_requestoruserguid ON requesthistory(requestoruserguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_credentialguid", "CREATE INDEX IF NOT EXISTS idx_requesthistory_credentialguid ON requesthistory(credentialguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_loadbalancingpolicyguid", "CREATE INDEX IF NOT EXISTS idx_requesthistory_loadbalancingpolicyguid ON requesthistory(loadbalancingpolicyguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_modelaccesspolicyguid", "CREATE INDEX IF NOT EXISTS idx_requesthistory_modelaccesspolicyguid ON requesthistory(modelaccesspolicyguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_modelaccessruleguid", "CREATE INDEX IF NOT EXISTS idx_requesthistory_modelaccessruleguid ON requesthistory(modelaccessruleguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_modelaccessdecision", "CREATE INDEX IF NOT EXISTS idx_requesthistory_modelaccessdecision ON requesthistory(modelaccessdecision);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_modelaccesswoulddeny", "CREATE INDEX IF NOT EXISTS idx_requesthistory_modelaccesswoulddeny ON requesthistory(modelaccesswoulddeny);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_requestedmodel", "CREATE INDEX IF NOT EXISTS idx_requesthistory_requestedmodel ON requesthistory(requestedmodel);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_effectivemodel", "CREATE INDEX IF NOT EXISTS idx_requesthistory_effectivemodel ON requesthistory(effectivemodel);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_selectionstrategy", "CREATE INDEX IF NOT EXISTS idx_requesthistory_selectionstrategy ON requesthistory(selectionstrategy);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_endpointgroupguid", "CREATE INDEX IF NOT EXISTS idx_requesthistory_endpointgroupguid ON requesthistory(endpointgroupguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_backoffreason", "CREATE INDEX IF NOT EXISTS idx_requesthistory_backoffreason ON requesthistory(backoffreason);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_adaptiveselection", "CREATE INDEX IF NOT EXISTS idx_requesthistory_adaptiveselection ON requesthistory(adaptiveselection);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_policyfallbackused", "CREATE INDEX IF NOT EXISTS idx_requesthistory_policyfallbackused ON requesthistory(policyfallbackused);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_denialreasoncode", "CREATE INDEX IF NOT EXISTS idx_requesthistory_denialreasoncode ON requesthistory(denialreasoncode);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_reservationguid", "CREATE INDEX IF NOT EXISTS idx_requesthistory_reservationguid ON requesthistory(reservationguid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_reservationreasoncode", "CREATE INDEX IF NOT EXISTS idx_requesthistory_reservationreasoncode ON requesthistory(reservationreasoncode);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_sessionaffinityoutcome", "CREATE INDEX IF NOT EXISTS idx_requesthistory_sessionaffinityoutcome ON requesthistory(sessionaffinityoutcome);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_traceid", "CREATE INDEX IF NOT EXISTS idx_requesthistory_traceid ON requesthistory(traceid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_providerrequestid", "CREATE INDEX IF NOT EXISTS idx_requesthistory_providerrequestid ON requesthistory(providerrequestid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_providername", "CREATE INDEX IF NOT EXISTS idx_requesthistory_providername ON requesthistory(providername);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_analyticscaptured", "CREATE INDEX IF NOT EXISTS idx_requesthistory_analyticscaptured ON requesthistory(analyticscaptured);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_dominantstagekind", "CREATE INDEX IF NOT EXISTS idx_requesthistory_dominantstagekind ON requesthistory(dominantstagekind);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requesthistory_analyticsfailurecode", "CREATE INDEX IF NOT EXISTS idx_requesthistory_analyticsfailurecode ON requesthistory(analyticsfailurecode);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_tenant_created", "CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_tenant_created ON requestanalyticsevents(tenantguid, createdutc);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_requesthistoryid", "CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_requesthistoryid ON requestanalyticsevents(requesthistoryid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_traceid", "CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_traceid ON requestanalyticsevents(traceid);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_stagekind", "CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_stagekind ON requestanalyticsevents(stagekind);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_endpoint_created", "CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_endpoint_created ON requestanalyticsevents(modelendpointguid, createdutc);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_vmr_created", "CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_vmr_created ON requestanalyticsevents(virtualmodelrunnerguid, createdutc);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_requestanalyticsevents_reservation_created", "CREATE INDEX IF NOT EXISTS idx_requestanalyticsevents_reservation_created ON requestanalyticsevents(reservationguid, createdutc);", token).ConfigureAwait(false);
            await EnsureIndexAsync("idx_vmr_modelaccesspolicyid", "CREATE INDEX IF NOT EXISTS idx_vmr_modelaccesspolicyid ON virtualmodelrunners(modelaccesspolicyid);", token).ConfigureAwait(false);
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
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (SqliteConnection conn = new SqliteConnection(ConnectionString))
                {
                    await conn.OpenAsync(token).ConfigureAwait(false);

                    string query = "PRAGMA table_info(" + Sanitize(tableName) + ");";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        using (SqliteDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                string name = reader["name"]?.ToString();
                                if (String.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                _Gate.Release();
            }

            return false;
        }

        private async Task<bool> IndexExistsAsync(string indexName, CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) AS cnt FROM sqlite_master WHERE type = 'index' AND name = '" + Sanitize(indexName) + "';";
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
