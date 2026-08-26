namespace Conductor.Core.Telemetry
{
    using System;
    using System.Data;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <summary>
    /// Helper that wraps database command execution with an OpenTelemetry span and metrics.
    /// <para>
    /// Emission rides the .NET base class library primitives exposed by
    /// <see cref="ConductorTelemetry"/>, so this helper takes no dependency on OpenTelemetry and
    /// is a no-op when nothing is listening. This type is stateless and thread-safe.
    /// </para>
    /// </summary>
    public static class DatabaseTelemetry
    {
        /// <summary>
        /// Execute an instrumented database operation, recording a client span, an operation
        /// counter, a duration histogram, and (on failure) an error counter.
        /// </summary>
        /// <param name="databaseSystem">Database system label (for example "sqlite", "postgresql"). Nullable.</param>
        /// <param name="sql">The SQL statement whose leading verb is used as the operation label. Nullable.</param>
        /// <param name="core">The delegate that performs the actual execution. Must not be null.</param>
        /// <returns>The <see cref="DataTable"/> produced by <paramref name="core"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="core"/> is null.</exception>
        public static async Task<DataTable> ExecuteAsync(string databaseSystem, string sql, Func<Task<DataTable>> core)
        {
            if (core == null) throw new ArgumentNullException(nameof(core));

            string system = String.IsNullOrWhiteSpace(databaseSystem) ? "unknown" : databaseSystem;
            string operation = ResolveOperation(sql);

            TagList tags = new TagList
            {
                { ConductorTelemetry.TagDbSystem, system },
                { ConductorTelemetry.TagDbOperation, operation }
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            using (Activity activity = ConductorTelemetry.DatabaseSource.StartActivity("db " + operation, ActivityKind.Client))
            {
                if (activity != null)
                {
                    activity.SetTag("db.system", system);
                    activity.SetTag("db.operation", operation);
                }

                try
                {
                    DataTable result = await core().ConfigureAwait(false);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return result;
                }
                catch (Exception ex)
                {
                    ConductorTelemetry.DbErrors.Add(1, tags);
                    if (activity != null)
                    {
                        activity.SetTag("exception.type", ex.GetType().FullName);
                        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                    }
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                    ConductorTelemetry.DbOperations.Add(1, tags);
                    ConductorTelemetry.DbOperationDuration.Record(stopwatch.Elapsed.TotalSeconds, tags);
                }
            }
        }

        /// <summary>
        /// Resolve the leading SQL verb into a low-cardinality operation label.
        /// </summary>
        /// <param name="sql">The SQL statement. Nullable.</param>
        /// <returns>A lowercase operation label; "batch" when multiple statements or unknown.</returns>
        private static string ResolveOperation(string sql)
        {
            if (String.IsNullOrWhiteSpace(sql)) return "other";

            int index = 0;
            while (index < sql.Length && Char.IsWhiteSpace(sql[index])) index++;

            int start = index;
            while (index < sql.Length && (Char.IsLetter(sql[index]))) index++;

            if (index <= start) return "other";

            string verb = sql.Substring(start, index - start).ToLowerInvariant();
            switch (verb)
            {
                case "select":
                case "insert":
                case "update":
                case "delete":
                case "create":
                case "alter":
                case "drop":
                case "begin":
                case "commit":
                    return verb;
                default:
                    return "other";
            }
        }
    }
}
