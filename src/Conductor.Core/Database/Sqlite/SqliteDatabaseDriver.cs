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
        private readonly object _Lock = new object();

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
            Administrator = new AdministratorMethods(this);
            RequestHistory = new RequestHistoryMethods(this);
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
                TableQueries.CreateVirtualModelRunnersTable,
                TableQueries.CreateAdministratorsTable,
                TableQueries.CreateRequestHistoryTable
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

            return result;
        }

        /// <summary>
        /// Run migrations for existing databases.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task RunMigrationsAsync(CancellationToken token = default)
        {
            // Check if requesthistoryenabled column exists in virtualmodelrunners
            bool columnExists = await ColumnExistsAsync("virtualmodelrunners", "requesthistoryenabled", token).ConfigureAwait(false);
            if (!columnExists)
            {
                try
                {
                    await ExecuteQueryAsync(TableQueries.AddRequestHistoryEnabledColumn, false, token).ConfigureAwait(false);
                }
                catch
                {
                    // Column may already exist in some edge cases, ignore the error
                }
            }
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

            return false;
        }
    }
}
