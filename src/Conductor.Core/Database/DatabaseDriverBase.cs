namespace Conductor.Core.Database
{
    using System;
    using System.Data;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;

    /// <summary>
    /// Abstract base class for database drivers.
    /// </summary>
    public abstract class DatabaseDriverBase
    {
        /// <summary>
        /// Tenant methods.
        /// </summary>
        public ITenantMethods Tenant { get; protected set; }

        /// <summary>
        /// User methods.
        /// </summary>
        public IUserMethods User { get; protected set; }

        /// <summary>
        /// Credential methods.
        /// </summary>
        public ICredentialMethods Credential { get; protected set; }

        /// <summary>
        /// Model runner endpoint methods.
        /// </summary>
        public IModelRunnerEndpointMethods ModelRunnerEndpoint { get; protected set; }

        /// <summary>
        /// Model definition methods.
        /// </summary>
        public IModelDefinitionMethods ModelDefinition { get; protected set; }

        /// <summary>
        /// Model configuration methods.
        /// </summary>
        public IModelConfigurationMethods ModelConfiguration { get; protected set; }

        /// <summary>
        /// Virtual model runner methods.
        /// </summary>
        public IVirtualModelRunnerMethods VirtualModelRunner { get; protected set; }

        /// <summary>
        /// Administrator methods.
        /// </summary>
        public IAdministratorMethods Administrator { get; protected set; }

        /// <summary>
        /// Connection string.
        /// </summary>
        protected string ConnectionString { get; set; }

        /// <summary>
        /// Boolean indicating if queries should be logged.
        /// </summary>
        protected bool LogQueries { get; set; }

        /// <summary>
        /// Instantiate the database driver base.
        /// </summary>
        protected DatabaseDriverBase()
        {
        }

        /// <summary>
        /// Initialize the database (create tables if needed).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Execute a query and return results.
        /// </summary>
        /// <param name="query">SQL query.</param>
        /// <param name="isTransaction">Execute within a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with results.</returns>
        public abstract Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Execute multiple queries.
        /// </summary>
        /// <param name="queries">SQL queries.</param>
        /// <param name="isTransaction">Execute within a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with last query results.</returns>
        public abstract Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Sanitize a string for SQL.
        /// </summary>
        /// <param name="value">String to sanitize.</param>
        /// <returns>Sanitized string.</returns>
        public virtual string Sanitize(string value)
        {
            if (String.IsNullOrEmpty(value)) return value;
            return value.Replace("'", "''");
        }

        /// <summary>
        /// Format a boolean for SQL.
        /// </summary>
        /// <param name="value">Boolean value.</param>
        /// <returns>SQL boolean string.</returns>
        public virtual string FormatBoolean(bool value)
        {
            return value ? "1" : "0";
        }

        /// <summary>
        /// Format a DateTime for SQL.
        /// </summary>
        /// <param name="value">DateTime value.</param>
        /// <returns>SQL datetime string.</returns>
        public virtual string FormatDateTime(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        /// <summary>
        /// Format a nullable value for SQL.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="value">Nullable value.</param>
        /// <returns>SQL value string or NULL.</returns>
        public virtual string FormatNullable<T>(T? value) where T : struct
        {
            if (!value.HasValue) return "NULL";
            return value.Value.ToString();
        }

        /// <summary>
        /// Format a nullable string for SQL.
        /// </summary>
        /// <param name="value">String value.</param>
        /// <returns>SQL string or NULL.</returns>
        public virtual string FormatNullableString(string value)
        {
            if (value == null) return "NULL";
            return "'" + Sanitize(value) + "'";
        }
    }
}
