namespace Conductor.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Models;

    /// <summary>
    /// Provider-neutral database-side request history aggregation.
    /// Bucket indexes are computed as whole seconds elapsed since the filter anchor divided by the
    /// bucket size, which is independent of the database session time zone.
    /// Instances are stateless and safe for concurrent use.
    /// </summary>
    public class RequestHistoryAggregateMethods : IRequestHistoryAggregateMethods
    {
        private static readonly string[] _ReservationDeniedReasonCodes = new string[]
        {
            "reservationdenied",
            "reservationdraindenied",
            "reservationauthenticationrequired",
            "reservationconflict"
        };

        /// <summary>
        /// Every requesthistory column the bucket aggregate reads besides the createdutc key. Covering indexes
        /// created by the database drivers carry these columns so the aggregate never touches the wide table rows.
        /// Keep this in sync with <see cref="GetTimeBucketsAsync"/>; a column missing here forces a row lookup
        /// for every matching request.
        /// </summary>
        public const string CoveringIndexColumns = "httpstatus, prompttokens, completiontokens, totaltokens, responsetimems, firsttokentimems, modelaccessdecision, denialreasoncode, reservationreasoncode";

        private const string SuccessCondition = "httpstatus IS NOT NULL AND httpstatus >= 100 AND httpstatus < 400";

        private readonly DatabaseDriverBase _Driver;
        private readonly RequestAnalyticsSqlDialect _Dialect;

        /// <summary>
        /// Instantiate request history aggregate methods.
        /// </summary>
        /// <param name="driver">Database driver. Must not be null.</param>
        /// <param name="dialect">SQL dialect.</param>
        /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
        public RequestHistoryAggregateMethods(DatabaseDriverBase driver, RequestAnalyticsSqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Dialect = dialect;
        }

        /// <inheritdoc />
        public async Task<List<RequestHistoryTimeBucket>> GetTimeBucketsAsync(RequestHistoryAggregateFilter filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            if (filter.AnchorUtc > filter.StartUtc) throw new ArgumentException("AnchorUtc must be less than or equal to StartUtc.", nameof(filter));

            string successCase = "CASE WHEN " + SuccessCondition + " THEN 1 ELSE 0 END";
            string reservationCodes = String.Join(", ", _ReservationDeniedReasonCodes.Select(item => "'" + item + "'"));

            string inner = "SELECT " + BuildBucketIndexExpression(filter) + " AS bucket_index, " +
                           successCase + " AS is_success, " +
                           "CASE WHEN LOWER(COALESCE(modelaccessdecision, '')) = 'deny' OR COALESCE(denialreasoncode, '') <> '' THEN 1 ELSE 0 END AS is_denied, " +
                           "CASE WHEN LOWER(COALESCE(reservationreasoncode, '')) IN (" + reservationCodes + ") THEN 1 ELSE 0 END AS is_reservation_denied, " +
                           "CASE WHEN httpstatus = 429 THEN 1 ELSE 0 END AS is_rate_limited, " +
                           "CASE WHEN " + SuccessCondition + " AND firsttokentimems IS NOT NULL THEN 1 ELSE 0 END AS has_ttft, " +
                           "CASE WHEN " + SuccessCondition + " AND firsttokentimems IS NOT NULL THEN " + ToBigInt("firsttokentimems") + " ELSE 0 END AS ttft_ms, " +
                           "CASE WHEN " + SuccessCondition + " AND responsetimems IS NOT NULL THEN 1 ELSE 0 END AS has_response_time, " +
                           "CASE WHEN " + SuccessCondition + " AND responsetimems IS NOT NULL THEN " + ToBigInt("responsetimems") + " ELSE 0 END AS response_ms, " +
                           "CASE WHEN " + SuccessCondition + " AND prompttokens IS NOT NULL THEN " + ToBigInt("prompttokens") + " ELSE 0 END AS prompt_tokens, " +
                           "CASE WHEN " + SuccessCondition + " AND completiontokens IS NOT NULL THEN " + ToBigInt("completiontokens") + " ELSE 0 END AS completion_tokens, " +
                           "CASE WHEN " + SuccessCondition + " AND totaltokens IS NOT NULL THEN " + ToBigInt("totaltokens") + " ELSE 0 END AS total_tokens, " +
                           "CASE WHEN " + SuccessCondition + " AND totaltokens IS NULL AND prompttokens IS NULL AND completiontokens IS NULL THEN 1 ELSE 0 END AS is_unknown_tokens " +
                           "FROM requesthistory " + BuildWhereClause(filter);

            string query = "SELECT bucket_index, " +
                           "COUNT(*) AS request_count, " +
                           "SUM(is_success) AS success_count, " +
                           "SUM(is_denied) AS denied_count, " +
                           "SUM(is_reservation_denied) AS reservation_denied_count, " +
                           "SUM(is_rate_limited) AS rate_limited_count, " +
                           "SUM(has_ttft) AS ttft_count, " +
                           "SUM(ttft_ms) AS ttft_sum, " +
                           "SUM(has_response_time) AS response_count, " +
                           "SUM(response_ms) AS response_sum, " +
                           "SUM(prompt_tokens) AS prompt_tokens, " +
                           "SUM(completion_tokens) AS completion_tokens, " +
                           "SUM(total_tokens) AS total_tokens, " +
                           "SUM(is_unknown_tokens) AS unknown_tokens_count " +
                           "FROM (" + inner + ") bucketed " +
                           "GROUP BY bucket_index " +
                           "ORDER BY bucket_index ASC;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<RequestHistoryTimeBucket> buckets = new List<RequestHistoryTimeBucket>();
            if (result == null)
            {
                return buckets;
            }

            foreach (DataRow row in result.Rows)
            {
                buckets.Add(new RequestHistoryTimeBucket
                {
                    BucketIndex = ReadInt64(row, "bucket_index"),
                    RequestCount = ReadInt64(row, "request_count"),
                    SuccessfulCompletionCount = ReadInt64(row, "success_count"),
                    DeniedRequestCount = ReadInt64(row, "denied_count"),
                    ReservationDeniedCount = ReadInt64(row, "reservation_denied_count"),
                    RateLimitedRequestCount = ReadInt64(row, "rate_limited_count"),
                    TimeToFirstTokenSampleCount = ReadInt64(row, "ttft_count"),
                    TimeToFirstTokenSumMs = ReadInt64(row, "ttft_sum"),
                    ResponseTimeSampleCount = ReadInt64(row, "response_count"),
                    ResponseTimeSumMs = ReadInt64(row, "response_sum"),
                    PromptTokens = ReadInt64(row, "prompt_tokens"),
                    CompletionTokens = ReadInt64(row, "completion_tokens"),
                    TotalTokens = ReadInt64(row, "total_tokens"),
                    UnknownTokenUsageCount = ReadInt64(row, "unknown_tokens_count")
                });
            }

            return buckets;
        }

        /// <inheritdoc />
        public async Task<List<RequestHistoryEntry>> ListRangeAsync(RequestHistoryAggregateFilter filter, int maxRows, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            if (maxRows < 1) throw new ArgumentOutOfRangeException(nameof(maxRows), "maxRows must be at least 1.");

            string whereClause = BuildWhereClause(filter);
            string query;
            if (_Dialect == RequestAnalyticsSqlDialect.SqlServer)
            {
                query = "SELECT TOP " + maxRows + " * FROM requesthistory " + whereClause + " ORDER BY createdutc DESC;";
            }
            else
            {
                query = "SELECT * FROM requesthistory " + whereClause + " ORDER BY createdutc DESC LIMIT " + maxRows + ";";
            }

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return RequestHistoryEntry.FromDataTable(result) ?? new List<RequestHistoryEntry>();
        }

        private string BuildBucketIndexExpression(RequestHistoryAggregateFilter filter)
        {
            DateTime anchor = TruncateToSecond(filter.AnchorUtc);
            string bucketSeconds = filter.BucketSeconds.ToString(CultureInfo.InvariantCulture);
            string anchorLiteral = _Driver.FormatDateTime(anchor);

            switch (_Dialect)
            {
                case RequestAnalyticsSqlDialect.PostgreSql:
                    return "CAST(FLOOR(EXTRACT(EPOCH FROM (createdutc - TIMESTAMP '" + anchorLiteral + "')) / " + bucketSeconds + ") AS BIGINT)";
                case RequestAnalyticsSqlDialect.MySql:
                    return "(TIMESTAMPDIFF(SECOND, '" + anchorLiteral + "', createdutc) DIV " + bucketSeconds + ")";
                case RequestAnalyticsSqlDialect.SqlServer:
                    return "(DATEDIFF_BIG(SECOND, CAST('" + anchorLiteral + "' AS DATETIME2), createdutc) / " + bucketSeconds + ")";
                case RequestAnalyticsSqlDialect.Sqlite:
                default:
                    long anchorEpoch = (long)(anchor - DateTime.UnixEpoch).TotalSeconds;
                    return "((CAST(strftime('%s', createdutc) AS INTEGER) - " + anchorEpoch.ToString(CultureInfo.InvariantCulture) + ") / " + bucketSeconds + ")";
            }
        }

        private string ToBigInt(string expression)
        {
            if (_Dialect == RequestAnalyticsSqlDialect.MySql)
            {
                return "CAST(" + expression + " AS SIGNED)";
            }

            return "CAST(" + expression + " AS BIGINT)";
        }

        private string BuildWhereClause(RequestHistoryAggregateFilter filter)
        {
            List<string> conditions = new List<string>();
            conditions.Add("createdutc >= '" + _Driver.FormatDateTime(filter.StartUtc) + "'");
            conditions.Add("createdutc < '" + _Driver.FormatDateTime(filter.EndUtc) + "'");

            if (!String.IsNullOrEmpty(filter.TenantGuid))
            {
                conditions.Add("tenantguid = '" + _Driver.Sanitize(filter.TenantGuid) + "'");
            }

            AddExactInCondition(conditions, "virtualmodelrunnerguid", filter.VirtualModelRunnerGuids);
            AddExactInCondition(conditions, "modelendpointguid", filter.ModelEndpointGuids);
            AddExactInCondition(conditions, "requestoruserguid", filter.RequestorUserGuids);
            AddExactInCondition(conditions, "credentialguid", filter.CredentialGuids);
            AddExactInCondition(conditions, "reservationguid", filter.ReservationGuids);
            AddCaseInsensitiveInCondition(conditions, "providername", filter.ProviderNames);
            AddCaseInsensitiveInCondition(conditions, "reservationdecision", filter.ReservationDecisions);
            AddCaseInsensitiveInCondition(conditions, "reservationreasoncode", filter.ReservationReasonCodes);
            AddCaseInsensitiveInCondition(conditions, "dominantstagekind", filter.DominantStageKinds);

            List<string> modelNames = CleanValues(filter.ModelNames);
            if (modelNames.Count > 0)
            {
                List<string> modelConditions = new List<string>();
                foreach (string modelName in modelNames)
                {
                    string pattern = "'%" + _Driver.Sanitize(modelName.ToLowerInvariant()) + "%'";
                    modelConditions.Add("LOWER(COALESCE(requestedmodel, '')) LIKE " + pattern);
                    modelConditions.Add("LOWER(COALESCE(effectivemodel, '')) LIKE " + pattern);
                    modelConditions.Add("LOWER(COALESCE(modeldefinitionname, '')) LIKE " + pattern);
                }

                conditions.Add("(" + String.Join(" OR ", modelConditions) + ")");
            }

            List<string> statusConditions = CleanValues(filter.StatusClasses)
                .Select(BuildStatusClassCondition)
                .Where(item => !String.IsNullOrEmpty(item))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (statusConditions.Count > 0)
            {
                conditions.Add("((" + String.Join(") OR (", statusConditions) + "))");
            }

            if (filter.ModelAccessWouldDeny.HasValue)
            {
                conditions.Add("modelaccesswoulddeny = " + _Driver.FormatBoolean(filter.ModelAccessWouldDeny.Value));
            }

            return "WHERE " + String.Join(" AND ", conditions);
        }

        private void AddExactInCondition(List<string> conditions, string column, List<string> values)
        {
            List<string> cleaned = CleanValues(values);
            if (cleaned.Count < 1)
            {
                return;
            }

            conditions.Add(column + " IN (" + String.Join(", ", cleaned.Select(item => "'" + _Driver.Sanitize(item) + "'")) + ")");
        }

        private void AddCaseInsensitiveInCondition(List<string> conditions, string column, List<string> values)
        {
            List<string> cleaned = CleanValues(values);
            if (cleaned.Count < 1)
            {
                return;
            }

            conditions.Add("LOWER(COALESCE(" + column + ", '')) IN (" + String.Join(", ", cleaned.Select(item => "'" + _Driver.Sanitize(item.ToLowerInvariant()) + "'")) + ")");
        }

        private static List<string> CleanValues(List<string> values)
        {
            if (values == null)
            {
                return new List<string>();
            }

            return values
                .Where(item => !String.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static string BuildStatusClassCondition(string statusClass)
        {
            switch (statusClass.Trim().ToLowerInvariant())
            {
                case "1xx":
                case "1":
                    return "httpstatus >= 100 AND httpstatus < 200";
                case "2xx":
                case "2":
                    return "httpstatus >= 200 AND httpstatus < 300";
                case "3xx":
                case "3":
                    return "httpstatus >= 300 AND httpstatus < 400";
                case "4xx":
                case "4":
                    return "httpstatus >= 400 AND httpstatus < 500";
                case "5xx":
                case "5":
                    return "httpstatus >= 500 AND httpstatus < 600";
                case "nostatus":
                    return "httpstatus IS NULL";
                default:
                    return null;
            }
        }

        private static DateTime TruncateToSecond(DateTime value)
        {
            return new DateTime(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
        }

        private static long ReadInt64(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column))
            {
                return 0;
            }

            object value = row[column];
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
    }
}
