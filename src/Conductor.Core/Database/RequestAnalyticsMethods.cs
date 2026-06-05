namespace Conductor.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Models;

    /// <summary>
    /// Provider-neutral request analytics database implementation.
    /// </summary>
    public class RequestAnalyticsMethods : IRequestAnalyticsMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly RequestAnalyticsSqlDialect _Dialect;

        /// <summary>
        /// Instantiate request analytics methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <param name="dialect">SQL dialect.</param>
        public RequestAnalyticsMethods(DatabaseDriverBase driver, RequestAnalyticsSqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Dialect = dialect;
        }

        /// <inheritdoc />
        public async Task<RequestAnalyticsEvent> CreateAsync(RequestAnalyticsEvent entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query = "INSERT INTO requestanalyticsevents (id, tenantguid, requesthistoryid, traceid, virtualmodelrunnerguid, virtualmodelrunnername, " +
                           "modelendpointguid, modelendpointname, modelendpointurl, providername, apiformat, modelname, sequence, stagekind, phase, stagename, " +
                           "startedutc, completedutc, durationms, success, httpstatus, errortype, errormessage, endpointlimiterwaitms, requesttoheadersms, " +
                           "headerstofirsttokenms, firsttokentolasttokenms, clienttotalms, prompttokens, completiontokens, totaltokens, requestbytes, responsebytes, " +
                           "tokenspersecond, rawprovidermetrics, createdutc) VALUES (" +
                           "'" + _Driver.Sanitize(entry.Id) + "', " +
                           _Driver.FormatNullableString(entry.TenantGuid) + ", " +
                           _Driver.FormatNullableString(entry.RequestHistoryId) + ", " +
                           _Driver.FormatNullableString(entry.TraceId) + ", " +
                           _Driver.FormatNullableString(entry.VirtualModelRunnerGuid) + ", " +
                           _Driver.FormatNullableString(entry.VirtualModelRunnerName) + ", " +
                           _Driver.FormatNullableString(entry.ModelEndpointGuid) + ", " +
                           _Driver.FormatNullableString(entry.ModelEndpointName) + ", " +
                           _Driver.FormatNullableString(entry.ModelEndpointUrl) + ", " +
                           _Driver.FormatNullableString(entry.ProviderName) + ", " +
                           _Driver.FormatNullableString(entry.ApiFormat) + ", " +
                           _Driver.FormatNullableString(entry.ModelName) + ", " +
                           entry.Sequence + ", " +
                           _Driver.FormatNullableString(entry.StageKind) + ", " +
                           _Driver.FormatNullableString(entry.Phase) + ", " +
                           _Driver.FormatNullableString(entry.StageName) + ", " +
                           "'" + _Driver.FormatDateTime(entry.StartedUtc) + "', " +
                           (entry.CompletedUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.CompletedUtc.Value) + "'" : "NULL") + ", " +
                           _Driver.FormatNullable(entry.DurationMs) + ", " +
                           _Driver.FormatBoolean(entry.Success) + ", " +
                           _Driver.FormatNullable(entry.HttpStatus) + ", " +
                           _Driver.FormatNullableString(entry.ErrorType) + ", " +
                           _Driver.FormatNullableString(entry.ErrorMessage) + ", " +
                           _Driver.FormatNullable(entry.EndpointLimiterWaitMs) + ", " +
                           _Driver.FormatNullable(entry.RequestToHeadersMs) + ", " +
                           _Driver.FormatNullable(entry.HeadersToFirstTokenMs) + ", " +
                           _Driver.FormatNullable(entry.FirstTokenToLastTokenMs) + ", " +
                           _Driver.FormatNullable(entry.ClientTotalMs) + ", " +
                           _Driver.FormatNullable(entry.PromptTokens) + ", " +
                           _Driver.FormatNullable(entry.CompletionTokens) + ", " +
                           _Driver.FormatNullable(entry.TotalTokens) + ", " +
                           _Driver.FormatNullable(entry.RequestBytes) + ", " +
                           _Driver.FormatNullable(entry.ResponseBytes) + ", " +
                           FormatNullableDecimal(entry.TokensPerSecond) + ", " +
                           _Driver.FormatNullableString(entry.RawProviderMetrics) + ", " +
                           "'" + _Driver.FormatDateTime(entry.CreatedUtc) + "');";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return entry;
        }

        /// <inheritdoc />
        public async Task CreateManyAsync(List<RequestAnalyticsEvent> entries, CancellationToken token = default)
        {
            if (entries == null) return;

            foreach (RequestAnalyticsEvent entry in entries)
            {
                token.ThrowIfCancellationRequested();
                await CreateAsync(entry, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<List<RequestAnalyticsEvent>> ListByRequestHistoryIdAsync(string requestHistoryId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(requestHistoryId)) throw new ArgumentNullException(nameof(requestHistoryId));

            string query = "SELECT * FROM requestanalyticsevents WHERE requesthistoryid = '" + _Driver.Sanitize(requestHistoryId) + "' ORDER BY sequence ASC, startedutc ASC;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return RequestAnalyticsEvent.FromDataTable(result) ?? new List<RequestAnalyticsEvent>();
        }

        /// <inheritdoc />
        public async Task<List<RequestAnalyticsEvent>> SearchAsync(RequestAnalyticsFilter filter, CancellationToken token = default)
        {
            if (filter == null) filter = new RequestAnalyticsFilter();
            NormalizeLimit(filter);

            string whereClause = BuildWhereClause(filter);
            string query;
            if (_Dialect == RequestAnalyticsSqlDialect.SqlServer)
            {
                query = "SELECT TOP " + filter.Limit + " * FROM requestanalyticsevents " + whereClause + " ORDER BY createdutc DESC;";
            }
            else
            {
                query = "SELECT * FROM requestanalyticsevents " + whereClause + " ORDER BY createdutc DESC LIMIT " + filter.Limit + ";";
            }

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return RequestAnalyticsEvent.FromDataTable(result) ?? new List<RequestAnalyticsEvent>();
        }

        /// <inheritdoc />
        public async Task DeleteByRequestHistoryIdAsync(string requestHistoryId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(requestHistoryId)) throw new ArgumentNullException(nameof(requestHistoryId));

            string query = "DELETE FROM requestanalyticsevents WHERE requesthistoryid = '" + _Driver.Sanitize(requestHistoryId) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<long> DeleteExpiredAsync(DateTime cutoff, CancellationToken token = default)
        {
            string cutoffStr = _Driver.FormatDateTime(cutoff);
            string countQuery = "SELECT COUNT(*) AS cnt FROM requestanalyticsevents WHERE createdutc < '" + cutoffStr + "';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                count = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            if (count == 0) return 0;

            string query = "DELETE FROM requestanalyticsevents WHERE createdutc < '" + cutoffStr + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return count;
        }

        private string BuildWhereClause(RequestAnalyticsFilter filter)
        {
            List<string> conditions = new List<string>();

            if (!String.IsNullOrEmpty(filter.TenantGuid))
            {
                conditions.Add("tenantguid = '" + _Driver.Sanitize(filter.TenantGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.RequestHistoryId))
            {
                conditions.Add("requesthistoryid = '" + _Driver.Sanitize(filter.RequestHistoryId) + "'");
            }
            if (!String.IsNullOrEmpty(filter.VirtualModelRunnerGuid))
            {
                conditions.Add("virtualmodelrunnerguid = '" + _Driver.Sanitize(filter.VirtualModelRunnerGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelEndpointGuid))
            {
                conditions.Add("modelendpointguid = '" + _Driver.Sanitize(filter.ModelEndpointGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ProviderName))
            {
                conditions.Add("LOWER(COALESCE(providername, '')) = '" + _Driver.Sanitize(filter.ProviderName).ToLowerInvariant() + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelName))
            {
                string modelName = _Driver.Sanitize(filter.ModelName).ToLowerInvariant();
                conditions.Add("LOWER(COALESCE(modelname, '')) LIKE '%" + modelName + "%'");
            }
            if (!String.IsNullOrEmpty(filter.StageKind))
            {
                conditions.Add("stagekind = '" + _Driver.Sanitize(filter.StageKind) + "'");
            }
            if (!String.IsNullOrEmpty(filter.StatusClass))
            {
                string statusClassCondition = BuildStatusClassCondition(filter.StatusClass);
                if (!String.IsNullOrEmpty(statusClassCondition))
                {
                    conditions.Add(statusClassCondition);
                }
            }
            if (filter.StartUtc.HasValue)
            {
                conditions.Add("createdutc >= '" + _Driver.FormatDateTime(filter.StartUtc.Value) + "'");
            }
            if (filter.EndUtc.HasValue)
            {
                conditions.Add("createdutc < '" + _Driver.FormatDateTime(filter.EndUtc.Value) + "'");
            }

            return conditions.Count > 0 ? "WHERE " + String.Join(" AND ", conditions) : "";
        }

        private static string BuildStatusClassCondition(string statusClass)
        {
            if (String.IsNullOrWhiteSpace(statusClass))
            {
                return null;
            }

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

        private static void NormalizeLimit(RequestAnalyticsFilter filter)
        {
            if (filter.Limit < 1)
            {
                filter.Limit = 10000;
            }
            if (filter.Limit > 50000)
            {
                filter.Limit = 50000;
            }
        }

        private static string FormatNullableDecimal(decimal? value)
        {
            if (!value.HasValue) return "NULL";
            return value.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
