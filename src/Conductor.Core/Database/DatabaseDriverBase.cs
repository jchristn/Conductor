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
        /// Endpoint group methods.
        /// </summary>
        public IEndpointGroupMethods EndpointGroup { get; protected set; }

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
        /// Virtual model runner reservation methods.
        /// </summary>
        public IVirtualModelRunnerReservationMethods VirtualModelRunnerReservation { get; protected set; }

        /// <summary>
        /// Load-balancing policy methods.
        /// </summary>
        public ILoadBalancingPolicyMethods LoadBalancingPolicy { get; protected set; }

        /// <summary>
        /// QoS profile methods.
        /// </summary>
        public IQosProfileMethods QosProfile { get; protected set; }

        /// <summary>
        /// QoS traffic class methods.
        /// </summary>
        public IQosTrafficClassMethods QosTrafficClass { get; protected set; }

        /// <summary>
        /// Model access policy methods.
        /// </summary>
        public IModelAccessPolicyMethods ModelAccessPolicy { get; protected set; }

        /// <summary>
        /// Administrator methods.
        /// </summary>
        public IAdministratorMethods Administrator { get; protected set; }

        /// <summary>
        /// Request history methods.
        /// </summary>
        public IRequestHistoryMethods RequestHistory { get; protected set; }

        /// <summary>
        /// Request analytics methods.
        /// </summary>
        public IRequestAnalyticsMethods RequestAnalytics { get; protected set; }

        /// <summary>
        /// Analytics saved report methods.
        /// </summary>
        public IAnalyticsSavedReportMethods AnalyticsSavedReport { get; protected set; }

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
        /// Database system label reported on database telemetry (for example "sqlite",
        /// "postgresql", "mssql", "mysql"). Never null.
        /// </summary>
        protected abstract string TelemetryDatabaseSystem { get; }

        /// <summary>
        /// Execute a query and return results. Execution is wrapped with an OpenTelemetry client
        /// span and database metrics; the provider-specific work is performed by
        /// <see cref="ExecuteQueryCoreAsync"/>.
        /// </summary>
        /// <param name="query">SQL query.</param>
        /// <param name="isTransaction">Execute within a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with results.</returns>
        public Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default)
        {
            return Conductor.Core.Telemetry.DatabaseTelemetry.ExecuteAsync(
                TelemetryDatabaseSystem,
                query,
                () => ExecuteQueryCoreAsync(query, isTransaction, token));
        }

        /// <summary>
        /// Execute multiple queries. Execution is wrapped with an OpenTelemetry client span and
        /// database metrics; the provider-specific work is performed by
        /// <see cref="ExecuteQueriesCoreAsync"/>.
        /// </summary>
        /// <param name="queries">SQL queries.</param>
        /// <param name="isTransaction">Execute within a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with last query results.</returns>
        public Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            return Conductor.Core.Telemetry.DatabaseTelemetry.ExecuteAsync(
                TelemetryDatabaseSystem,
                "batch",
                () => ExecuteQueriesCoreAsync(queries, isTransaction, token));
        }

        /// <summary>
        /// Provider-specific implementation of <see cref="ExecuteQueryAsync"/>.
        /// </summary>
        /// <param name="query">SQL query.</param>
        /// <param name="isTransaction">Execute within a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with results.</returns>
        protected abstract Task<DataTable> ExecuteQueryCoreAsync(string query, bool isTransaction = false, CancellationToken token = default);

        /// <summary>
        /// Provider-specific implementation of <see cref="ExecuteQueriesAsync"/>.
        /// </summary>
        /// <param name="queries">SQL queries.</param>
        /// <param name="isTransaction">Execute within a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable with last query results.</returns>
        protected abstract Task<DataTable> ExecuteQueriesCoreAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default);

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
