namespace Conductor.Core.Database.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Models;

    /// <summary>
    /// SQLite request history methods implementation.
    /// </summary>
    public class RequestHistoryMethods : IRequestHistoryMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the request history methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
        public RequestHistoryMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a request history entry.
        /// </summary>
        public async Task<RequestHistoryEntry> CreateAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query = "INSERT INTO requesthistory (id, tenantguid, virtualmodelrunnerguid, virtualmodelrunnername, " +
                           "requestoruserguid, requestoruseremail, credentialguid, credentialname, loadbalancingpolicyguid, loadbalancingpolicyname, " +
                           "modelaccesspolicyguid, modelaccesspolicyname, modelaccessruleguid, modelaccessrulename, modelaccessdecision, modelaccesswoulddeny, " +
                           "modelendpointguid, modelendpointname, modelendpointurl, modeldefinitionguid, modeldefinitionname, " +
                           "modelconfigurationguid, requestedmodel, effectivemodel, requesttype, routingoutcomecode, denialreasoncode, denialreason, " +
                           "reservationguid, reservationname, reservationdecision, reservationreasoncode, reservationwindowstartutc, reservationwindowendutc, " +
                           "sessionaffinityoutcome, mutationsummary, explanationsummary, requestbodyretained, requestbodyredacted, requestheadersredacted, " +
                           "responsebodyretained, responsebodyredacted, responseheadersredacted, requestorsourceip, httpmethod, httpurl, requestbodylength, responsebodylength, " +
                           "httpstatus, firsttokentimems, responsetimems, traceid, providerrequestid, providername, prompttokens, completiontokens, totaltokens, " +
                           "tokenspersecondoverall, tokenspersecondgeneration, analyticscaptured, analyticsversion, dominantstagekind, dominantstagedurationms, analyticsfailurecode, " +
                           "objectkey, requesttransfertype, responsetransfertype, createdutc, completedutc) " +
                           "VALUES ('" + _Driver.Sanitize(entry.Id) + "', " +
                           "'" + _Driver.Sanitize(entry.TenantGuid) + "', " +
                           "'" + _Driver.Sanitize(entry.VirtualModelRunnerGuid) + "', " +
                           "'" + _Driver.Sanitize(entry.VirtualModelRunnerName) + "', " +
                           _Driver.FormatNullableString(entry.RequestorUserGuid) + ", " +
                           _Driver.FormatNullableString(entry.RequestorUserEmail) + ", " +
                           _Driver.FormatNullableString(entry.CredentialGuid) + ", " +
                           _Driver.FormatNullableString(entry.CredentialName) + ", " +
                           _Driver.FormatNullableString(entry.LoadBalancingPolicyGuid) + ", " +
                           _Driver.FormatNullableString(entry.LoadBalancingPolicyName) + ", " +
                           _Driver.FormatNullableString(entry.ModelAccessPolicyGuid) + ", " +
                           _Driver.FormatNullableString(entry.ModelAccessPolicyName) + ", " +
                           _Driver.FormatNullableString(entry.ModelAccessRuleGuid) + ", " +
                           _Driver.FormatNullableString(entry.ModelAccessRuleName) + ", " +
                           _Driver.FormatNullableString(entry.ModelAccessDecision) + ", " +
                           _Driver.FormatBoolean(entry.ModelAccessWouldDeny) + ", " +
                           _Driver.FormatNullableString(entry.ModelEndpointGuid) + ", " +
                           _Driver.FormatNullableString(entry.ModelEndpointName) + ", " +
                           _Driver.FormatNullableString(entry.ModelEndpointUrl) + ", " +
                           _Driver.FormatNullableString(entry.ModelDefinitionGuid) + ", " +
                           _Driver.FormatNullableString(entry.ModelDefinitionName) + ", " +
                           _Driver.FormatNullableString(entry.ModelConfigurationGuid) + ", " +
                           _Driver.FormatNullableString(entry.RequestedModel) + ", " +
                           _Driver.FormatNullableString(entry.EffectiveModel) + ", " +
                           _Driver.FormatNullableString(entry.RequestType) + ", " +
                           _Driver.FormatNullableString(entry.RoutingOutcomeCode) + ", " +
                           _Driver.FormatNullableString(entry.DenialReasonCode) + ", " +
                           _Driver.FormatNullableString(entry.DenialReason) + ", " +
                           _Driver.FormatNullableString(entry.ReservationGuid) + ", " +
                           _Driver.FormatNullableString(entry.ReservationName) + ", " +
                           _Driver.FormatNullableString(entry.ReservationDecision) + ", " +
                           _Driver.FormatNullableString(entry.ReservationReasonCode) + ", " +
                           (entry.ReservationWindowStartUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.ReservationWindowStartUtc.Value) + "'" : "NULL") + ", " +
                           (entry.ReservationWindowEndUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.ReservationWindowEndUtc.Value) + "'" : "NULL") + ", " +
                           _Driver.FormatNullableString(entry.SessionAffinityOutcome) + ", " +
                           _Driver.FormatNullableString(entry.MutationSummary) + ", " +
                           _Driver.FormatNullableString(entry.ExplanationSummary) + ", " +
                           _Driver.FormatBoolean(entry.RequestBodyRetained) + ", " +
                           _Driver.FormatBoolean(entry.RequestBodyRedacted) + ", " +
                           _Driver.FormatBoolean(entry.RequestHeadersRedacted) + ", " +
                           _Driver.FormatBoolean(entry.ResponseBodyRetained) + ", " +
                           _Driver.FormatBoolean(entry.ResponseBodyRedacted) + ", " +
                           _Driver.FormatBoolean(entry.ResponseHeadersRedacted) + ", " +
                           "'" + _Driver.Sanitize(entry.RequestorSourceIp) + "', " +
                           "'" + _Driver.Sanitize(entry.HttpMethod) + "', " +
                           "'" + _Driver.Sanitize(entry.HttpUrl) + "', " +
                           entry.RequestBodyLength + ", " +
                           _Driver.FormatNullable(entry.ResponseBodyLength) + ", " +
                           _Driver.FormatNullable(entry.HttpStatus) + ", " +
                           _Driver.FormatNullable(entry.FirstTokenTimeMs) + ", " +
                           _Driver.FormatNullable(entry.ResponseTimeMs) + ", " +
                           _Driver.FormatNullableString(entry.TraceId) + ", " +
                           _Driver.FormatNullableString(entry.ProviderRequestId) + ", " +
                           _Driver.FormatNullableString(entry.ProviderName) + ", " +
                           _Driver.FormatNullable(entry.PromptTokens) + ", " +
                           _Driver.FormatNullable(entry.CompletionTokens) + ", " +
                           _Driver.FormatNullable(entry.TotalTokens) + ", " +
                           _Driver.FormatNullable(entry.TokensPerSecondOverall) + ", " +
                           _Driver.FormatNullable(entry.TokensPerSecondGeneration) + ", " +
                           _Driver.FormatBoolean(entry.AnalyticsCaptured) + ", " +
                           entry.AnalyticsVersion + ", " +
                           _Driver.FormatNullableString(entry.DominantStageKind) + ", " +
                           _Driver.FormatNullable(entry.DominantStageDurationMs) + ", " +
                           _Driver.FormatNullableString(entry.AnalyticsFailureCode) + ", " +
                           "'" + _Driver.Sanitize(entry.ObjectKey) + "', " +
                           (int)entry.RequestTransferType + ", " +
                           (int)entry.ResponseTransferType + ", " +
                           "'" + _Driver.FormatDateTime(entry.CreatedUtc) + "', " +
                           (entry.CompletedUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.CompletedUtc.Value) + "'" : "NULL") + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return entry;
        }

        /// <summary>
        /// Update a request history entry with response data.
        /// </summary>
        public async Task<RequestHistoryEntry> UpdateAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query = "UPDATE requesthistory SET " +
                           "requestoruserguid = " + _Driver.FormatNullableString(entry.RequestorUserGuid) + ", " +
                           "requestoruseremail = " + _Driver.FormatNullableString(entry.RequestorUserEmail) + ", " +
                           "credentialguid = " + _Driver.FormatNullableString(entry.CredentialGuid) + ", " +
                           "credentialname = " + _Driver.FormatNullableString(entry.CredentialName) + ", " +
                           "loadbalancingpolicyguid = " + _Driver.FormatNullableString(entry.LoadBalancingPolicyGuid) + ", " +
                           "loadbalancingpolicyname = " + _Driver.FormatNullableString(entry.LoadBalancingPolicyName) + ", " +
                           "modelaccesspolicyguid = " + _Driver.FormatNullableString(entry.ModelAccessPolicyGuid) + ", " +
                           "modelaccesspolicyname = " + _Driver.FormatNullableString(entry.ModelAccessPolicyName) + ", " +
                           "modelaccessruleguid = " + _Driver.FormatNullableString(entry.ModelAccessRuleGuid) + ", " +
                           "modelaccessrulename = " + _Driver.FormatNullableString(entry.ModelAccessRuleName) + ", " +
                           "modelaccessdecision = " + _Driver.FormatNullableString(entry.ModelAccessDecision) + ", " +
                           "modelaccesswoulddeny = " + _Driver.FormatBoolean(entry.ModelAccessWouldDeny) + ", " +
                           "modelendpointguid = " + _Driver.FormatNullableString(entry.ModelEndpointGuid) + ", " +
                           "modelendpointname = " + _Driver.FormatNullableString(entry.ModelEndpointName) + ", " +
                           "modelendpointurl = " + _Driver.FormatNullableString(entry.ModelEndpointUrl) + ", " +
                           "modeldefinitionguid = " + _Driver.FormatNullableString(entry.ModelDefinitionGuid) + ", " +
                           "modeldefinitionname = " + _Driver.FormatNullableString(entry.ModelDefinitionName) + ", " +
                           "modelconfigurationguid = " + _Driver.FormatNullableString(entry.ModelConfigurationGuid) + ", " +
                           "requestedmodel = " + _Driver.FormatNullableString(entry.RequestedModel) + ", " +
                           "effectivemodel = " + _Driver.FormatNullableString(entry.EffectiveModel) + ", " +
                           "requesttype = " + _Driver.FormatNullableString(entry.RequestType) + ", " +
                           "routingoutcomecode = " + _Driver.FormatNullableString(entry.RoutingOutcomeCode) + ", " +
                           "denialreasoncode = " + _Driver.FormatNullableString(entry.DenialReasonCode) + ", " +
                           "denialreason = " + _Driver.FormatNullableString(entry.DenialReason) + ", " +
                           "reservationguid = " + _Driver.FormatNullableString(entry.ReservationGuid) + ", " +
                           "reservationname = " + _Driver.FormatNullableString(entry.ReservationName) + ", " +
                           "reservationdecision = " + _Driver.FormatNullableString(entry.ReservationDecision) + ", " +
                           "reservationreasoncode = " + _Driver.FormatNullableString(entry.ReservationReasonCode) + ", " +
                           "reservationwindowstartutc = " + (entry.ReservationWindowStartUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.ReservationWindowStartUtc.Value) + "'" : "NULL") + ", " +
                           "reservationwindowendutc = " + (entry.ReservationWindowEndUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.ReservationWindowEndUtc.Value) + "'" : "NULL") + ", " +
                           "sessionaffinityoutcome = " + _Driver.FormatNullableString(entry.SessionAffinityOutcome) + ", " +
                           "mutationsummary = " + _Driver.FormatNullableString(entry.MutationSummary) + ", " +
                           "explanationsummary = " + _Driver.FormatNullableString(entry.ExplanationSummary) + ", " +
                           "requestbodyretained = " + _Driver.FormatBoolean(entry.RequestBodyRetained) + ", " +
                           "requestbodyredacted = " + _Driver.FormatBoolean(entry.RequestBodyRedacted) + ", " +
                           "requestheadersredacted = " + _Driver.FormatBoolean(entry.RequestHeadersRedacted) + ", " +
                           "responsebodyretained = " + _Driver.FormatBoolean(entry.ResponseBodyRetained) + ", " +
                           "responsebodyredacted = " + _Driver.FormatBoolean(entry.ResponseBodyRedacted) + ", " +
                           "responseheadersredacted = " + _Driver.FormatBoolean(entry.ResponseHeadersRedacted) + ", " +
                           "responsebodylength = " + _Driver.FormatNullable(entry.ResponseBodyLength) + ", " +
                           "httpstatus = " + _Driver.FormatNullable(entry.HttpStatus) + ", " +
                           "firsttokentimems = " + _Driver.FormatNullable(entry.FirstTokenTimeMs) + ", " +
                           "responsetimems = " + _Driver.FormatNullable(entry.ResponseTimeMs) + ", " +
                           "traceid = " + _Driver.FormatNullableString(entry.TraceId) + ", " +
                           "providerrequestid = " + _Driver.FormatNullableString(entry.ProviderRequestId) + ", " +
                           "providername = " + _Driver.FormatNullableString(entry.ProviderName) + ", " +
                           "prompttokens = " + _Driver.FormatNullable(entry.PromptTokens) + ", " +
                           "completiontokens = " + _Driver.FormatNullable(entry.CompletionTokens) + ", " +
                           "totaltokens = " + _Driver.FormatNullable(entry.TotalTokens) + ", " +
                           "tokenspersecondoverall = " + _Driver.FormatNullable(entry.TokensPerSecondOverall) + ", " +
                           "tokenspersecondgeneration = " + _Driver.FormatNullable(entry.TokensPerSecondGeneration) + ", " +
                           "analyticscaptured = " + _Driver.FormatBoolean(entry.AnalyticsCaptured) + ", " +
                           "analyticsversion = " + entry.AnalyticsVersion + ", " +
                           "dominantstagekind = " + _Driver.FormatNullableString(entry.DominantStageKind) + ", " +
                           "dominantstagedurationms = " + _Driver.FormatNullable(entry.DominantStageDurationMs) + ", " +
                           "analyticsfailurecode = " + _Driver.FormatNullableString(entry.AnalyticsFailureCode) + ", " +
                           "requesttransfertype = " + (int)entry.RequestTransferType + ", " +
                           "responsetransfertype = " + (int)entry.ResponseTransferType + ", " +
                           "completedutc = " + (entry.CompletedUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.CompletedUtc.Value) + "'" : "NULL") + " " +
                           "WHERE id = '" + _Driver.Sanitize(entry.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return entry;
        }

        /// <summary>
        /// Read a request history entry by ID.
        /// </summary>
        public async Task<RequestHistoryEntry> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM requesthistory WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return RequestHistoryEntry.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Search request history entries with pagination.
        /// </summary>
        public async Task<RequestHistorySearchResult> SearchAsync(RequestHistorySearchFilter filter, CancellationToken token = default)
        {
            if (filter == null) filter = new RequestHistorySearchFilter();

            string whereClause = BuildWhereClause(filter);
            int offset = (filter.Page - 1) * filter.PageSize;

            // Run count and data queries in parallel
            string countQuery = "SELECT COUNT(*) AS cnt FROM requesthistory " + whereClause + ";";
            string dataQuery = "SELECT * FROM requesthistory " + whereClause +
                               " ORDER BY createdutc DESC LIMIT " + filter.PageSize + " OFFSET " + offset + ";";

            Task<DataTable> countTask = _Driver.ExecuteQueryAsync(countQuery, false, token);
            Task<DataTable> dataTask = _Driver.ExecuteQueryAsync(dataQuery, false, token);

            await Task.WhenAll(countTask, dataTask).ConfigureAwait(false);

            DataTable countResult = countTask.Result;
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            List<RequestHistoryEntry> data = RequestHistoryEntry.FromDataTable(dataTask.Result) ?? new List<RequestHistoryEntry>();

            return new RequestHistorySearchResult
            {
                Data = data,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = totalCount
            };
        }

        /// <summary>
        /// Count request history entries matching the filter.
        /// </summary>
        public async Task<long> CountAsync(RequestHistorySearchFilter filter, CancellationToken token = default)
        {
            if (filter == null) filter = new RequestHistorySearchFilter();

            string whereClause = BuildWhereClause(filter);
            string query = "SELECT COUNT(*) AS cnt FROM requesthistory " + whereClause + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return 0;
            return Convert.ToInt64(result.Rows[0]["cnt"]);
        }

        /// <summary>
        /// Delete a request history entry by ID.
        /// </summary>
        public async Task DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM requesthistory WHERE id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete request history entries matching the filter.
        /// </summary>
        public async Task<long> DeleteBulkAsync(RequestHistorySearchFilter filter, CancellationToken token = default)
        {
            if (filter == null) filter = new RequestHistorySearchFilter();

            // First count how many will be deleted
            long count = await CountAsync(filter, token).ConfigureAwait(false);
            if (count == 0) return 0;

            string whereClause = BuildWhereClause(filter);
            string query = "DELETE FROM requesthistory " + whereClause + ";";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            return count;
        }

        /// <summary>
        /// Delete request history entries older than the specified cutoff.
        /// </summary>
        public async Task<long> DeleteExpiredAsync(DateTime cutoff, CancellationToken token = default)
        {
            string cutoffStr = _Driver.FormatDateTime(cutoff);

            // First count
            string countQuery = "SELECT COUNT(*) AS cnt FROM requesthistory WHERE createdutc < '" + cutoffStr + "';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                count = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            if (count == 0) return 0;

            string query = "DELETE FROM requesthistory WHERE createdutc < '" + cutoffStr + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            return count;
        }

        /// <summary>
        /// Get object keys for expired request history entries.
        /// </summary>
        public async Task<List<string>> GetExpiredObjectKeysAsync(DateTime cutoff, CancellationToken token = default)
        {
            string cutoffStr = _Driver.FormatDateTime(cutoff);

            string query = "SELECT objectkey FROM requesthistory WHERE createdutc < '" + cutoffStr + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<string> keys = new List<string>();
            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    string key = row["objectkey"]?.ToString();
                    if (!String.IsNullOrEmpty(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            return keys;
        }

        /// <summary>
        /// Get aggregated request counts grouped by time buckets.
        /// </summary>
        public async Task<RequestHistorySummaryResult> GetSummaryAsync(RequestHistorySummaryFilter filter, CancellationToken token = default)
        {
            if (filter == null) filter = new RequestHistorySummaryFilter();

            string startStr = _Driver.FormatDateTime(filter.StartUtc);
            string endStr = _Driver.FormatDateTime(filter.EndUtc);

            string dateTrunc;
            switch (filter.Interval)
            {
                case "minute":
                    dateTrunc = "strftime('%Y-%m-%d %H:%M:00', createdutc)";
                    break;
                case "15minute":
                    dateTrunc = "strftime('%Y-%m-%d %H:', createdutc) || printf('%02d:00', (CAST(strftime('%M', createdutc) AS INTEGER) / 15) * 15)";
                    break;
                case "6hour":
                    dateTrunc = "strftime('%Y-%m-%d ', createdutc) || printf('%02d:00:00', (CAST(strftime('%H', createdutc) AS INTEGER) / 6) * 6)";
                    break;
                case "day":
                    dateTrunc = "strftime('%Y-%m-%d 00:00:00', createdutc)";
                    break;
                default:
                    dateTrunc = "strftime('%Y-%m-%d %H:00:00', createdutc)";
                    break;
            }

            string whereClause = BuildSummaryWhereClause(filter, startStr, endStr);

            string query = "SELECT " + dateTrunc + " AS bucket_time, " +
                           "SUM(CASE WHEN httpstatus IS NOT NULL AND httpstatus >= 100 AND httpstatus < 400 THEN 1 ELSE 0 END) AS success_count, " +
                           "SUM(CASE WHEN httpstatus IS NULL OR httpstatus >= 400 THEN 1 ELSE 0 END) AS failure_count " +
                           "FROM requesthistory " + whereClause + " " +
                           "GROUP BY bucket_time " +
                           "ORDER BY bucket_time ASC;";
            string statusClassExpression = BuildStatusClassExpression();
            string statusClassQuery = "SELECT " + statusClassExpression + " AS bucket_key, COUNT(*) AS bucket_count " +
                                      "FROM requesthistory " + whereClause + " GROUP BY " + statusClassExpression + ";";
            string denialReasonQuery = "SELECT COALESCE(NULLIF(denialreasoncode, ''), 'None') AS bucket_key, COUNT(*) AS bucket_count " +
                                       "FROM requesthistory " + whereClause + " GROUP BY COALESCE(NULLIF(denialreasoncode, ''), 'None');";
            string sessionOutcomeQuery = "SELECT COALESCE(NULLIF(sessionaffinityoutcome, ''), 'None') AS bucket_key, COUNT(*) AS bucket_count " +
                                         "FROM requesthistory " + whereClause + " GROUP BY COALESCE(NULLIF(sessionaffinityoutcome, ''), 'None');";

            Task<DataTable> bucketTask = _Driver.ExecuteQueryAsync(query, false, token);
            Task<DataTable> statusTask = _Driver.ExecuteQueryAsync(statusClassQuery, false, token);
            Task<DataTable> denialTask = _Driver.ExecuteQueryAsync(denialReasonQuery, false, token);
            Task<DataTable> sessionTask = _Driver.ExecuteQueryAsync(sessionOutcomeQuery, false, token);

            await Task.WhenAll(bucketTask, statusTask, denialTask, sessionTask).ConfigureAwait(false);

            List<RequestHistorySummaryBucket> buckets = RequestHistorySummaryBucket.FromDataTable(bucketTask.Result) ?? new List<RequestHistorySummaryBucket>();

            long totalSuccess = 0;
            long totalFailure = 0;
            foreach (RequestHistorySummaryBucket bucket in buckets)
            {
                totalSuccess += bucket.SuccessCount;
                totalFailure += bucket.FailureCount;
            }

            return new RequestHistorySummaryResult
            {
                Data = buckets,
                StartUtc = filter.StartUtc,
                EndUtc = filter.EndUtc,
                Interval = filter.Interval,
                TotalSuccess = totalSuccess,
                TotalFailure = totalFailure,
                StatusClassCounts = ReadBucketCounts(statusTask.Result),
                DenialReasonCounts = ReadBucketCounts(denialTask.Result),
                SessionAffinityOutcomeCounts = ReadBucketCounts(sessionTask.Result)
            };
        }

        private string BuildWhereClause(RequestHistorySearchFilter filter)
        {
            List<string> conditions = new List<string>();

            if (!String.IsNullOrEmpty(filter.TenantGuid))
            {
                conditions.Add("tenantguid = '" + _Driver.Sanitize(filter.TenantGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.VirtualModelRunnerGuid))
            {
                conditions.Add("virtualmodelrunnerguid = '" + _Driver.Sanitize(filter.VirtualModelRunnerGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelEndpointGuid))
            {
                conditions.Add("modelendpointguid = '" + _Driver.Sanitize(filter.ModelEndpointGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.RequestorUserGuid))
            {
                conditions.Add("requestoruserguid = '" + _Driver.Sanitize(filter.RequestorUserGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.CredentialGuid))
            {
                conditions.Add("credentialguid = '" + _Driver.Sanitize(filter.CredentialGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.LoadBalancingPolicyGuid))
            {
                conditions.Add("loadbalancingpolicyguid = '" + _Driver.Sanitize(filter.LoadBalancingPolicyGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelAccessPolicyGuid))
            {
                conditions.Add("modelaccesspolicyguid = '" + _Driver.Sanitize(filter.ModelAccessPolicyGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelAccessRuleGuid))
            {
                conditions.Add("modelaccessruleguid = '" + _Driver.Sanitize(filter.ModelAccessRuleGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelAccessDecision))
            {
                conditions.Add("modelaccessdecision = '" + _Driver.Sanitize(filter.ModelAccessDecision) + "'");
            }
            if (filter.ModelAccessWouldDeny.HasValue)
            {
                conditions.Add("modelaccesswoulddeny = " + _Driver.FormatBoolean(filter.ModelAccessWouldDeny.Value));
            }
            if (!String.IsNullOrEmpty(filter.ModelName))
            {
                string modelName = _Driver.Sanitize(filter.ModelName).ToLowerInvariant();
                conditions.Add("(LOWER(COALESCE(requestedmodel, '')) LIKE '%" + modelName + "%' OR LOWER(COALESCE(effectivemodel, '')) LIKE '%" + modelName + "%')");
            }
            if (!String.IsNullOrEmpty(filter.MutationSummary))
            {
                string mutationSummary = _Driver.Sanitize(filter.MutationSummary).ToLowerInvariant();
                conditions.Add("LOWER(COALESCE(mutationsummary, '')) LIKE '%" + mutationSummary + "%'");
            }
            if (!String.IsNullOrEmpty(filter.DenialReasonCode))
            {
                conditions.Add("denialreasoncode = '" + _Driver.Sanitize(filter.DenialReasonCode) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ReservationGuid))
            {
                conditions.Add("reservationguid = '" + _Driver.Sanitize(filter.ReservationGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ReservationDecision))
            {
                conditions.Add("reservationdecision = '" + _Driver.Sanitize(filter.ReservationDecision) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ReservationReasonCode))
            {
                conditions.Add("reservationreasoncode = '" + _Driver.Sanitize(filter.ReservationReasonCode) + "'");
            }
            if (!String.IsNullOrEmpty(filter.SessionAffinityOutcome))
            {
                conditions.Add("sessionaffinityoutcome = '" + _Driver.Sanitize(filter.SessionAffinityOutcome) + "'");
            }
            if (!String.IsNullOrEmpty(filter.StatusClass))
            {
                string statusClassCondition = BuildStatusClassCondition(filter.StatusClass);
                if (!String.IsNullOrEmpty(statusClassCondition))
                {
                    conditions.Add(statusClassCondition);
                }
            }
            if (filter.CreatedAfterUtc.HasValue)
            {
                conditions.Add("createdutc >= '" + _Driver.FormatDateTime(filter.CreatedAfterUtc.Value) + "'");
            }
            if (filter.CreatedBeforeUtc.HasValue)
            {
                conditions.Add("createdutc < '" + _Driver.FormatDateTime(filter.CreatedBeforeUtc.Value) + "'");
            }
            if (!String.IsNullOrEmpty(filter.RequestorSourceIp))
            {
                conditions.Add("requestorsourceip = '" + _Driver.Sanitize(filter.RequestorSourceIp) + "'");
            }
            if (filter.HttpStatus.HasValue)
            {
                conditions.Add("httpstatus = " + filter.HttpStatus.Value);
            }

            return conditions.Count > 0 ? "WHERE " + String.Join(" AND ", conditions) : "";
        }

        private string BuildSummaryWhereClause(RequestHistorySummaryFilter filter, string startStr, string endStr)
        {
            List<string> conditions = new List<string>();
            conditions.Add("createdutc >= '" + startStr + "'");
            conditions.Add("createdutc < '" + endStr + "'");

            if (!String.IsNullOrEmpty(filter.TenantGuid))
            {
                conditions.Add("tenantguid = '" + _Driver.Sanitize(filter.TenantGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.VirtualModelRunnerGuid))
            {
                conditions.Add("virtualmodelrunnerguid = '" + _Driver.Sanitize(filter.VirtualModelRunnerGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelEndpointGuid))
            {
                conditions.Add("modelendpointguid = '" + _Driver.Sanitize(filter.ModelEndpointGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.RequestorUserGuid))
            {
                conditions.Add("requestoruserguid = '" + _Driver.Sanitize(filter.RequestorUserGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.CredentialGuid))
            {
                conditions.Add("credentialguid = '" + _Driver.Sanitize(filter.CredentialGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.LoadBalancingPolicyGuid))
            {
                conditions.Add("loadbalancingpolicyguid = '" + _Driver.Sanitize(filter.LoadBalancingPolicyGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelAccessPolicyGuid))
            {
                conditions.Add("modelaccesspolicyguid = '" + _Driver.Sanitize(filter.ModelAccessPolicyGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelAccessRuleGuid))
            {
                conditions.Add("modelaccessruleguid = '" + _Driver.Sanitize(filter.ModelAccessRuleGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelAccessDecision))
            {
                conditions.Add("modelaccessdecision = '" + _Driver.Sanitize(filter.ModelAccessDecision) + "'");
            }
            if (filter.ModelAccessWouldDeny.HasValue)
            {
                conditions.Add("modelaccesswoulddeny = " + _Driver.FormatBoolean(filter.ModelAccessWouldDeny.Value));
            }
            if (!String.IsNullOrEmpty(filter.ModelName))
            {
                string modelName = _Driver.Sanitize(filter.ModelName).ToLowerInvariant();
                conditions.Add("(LOWER(COALESCE(requestedmodel, '')) LIKE '%" + modelName + "%' OR LOWER(COALESCE(effectivemodel, '')) LIKE '%" + modelName + "%')");
            }
            if (!String.IsNullOrEmpty(filter.MutationSummary))
            {
                string mutationSummary = _Driver.Sanitize(filter.MutationSummary).ToLowerInvariant();
                conditions.Add("LOWER(COALESCE(mutationsummary, '')) LIKE '%" + mutationSummary + "%'");
            }
            if (!String.IsNullOrEmpty(filter.DenialReasonCode))
            {
                conditions.Add("denialreasoncode = '" + _Driver.Sanitize(filter.DenialReasonCode) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ReservationGuid))
            {
                conditions.Add("reservationguid = '" + _Driver.Sanitize(filter.ReservationGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ReservationDecision))
            {
                conditions.Add("reservationdecision = '" + _Driver.Sanitize(filter.ReservationDecision) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ReservationReasonCode))
            {
                conditions.Add("reservationreasoncode = '" + _Driver.Sanitize(filter.ReservationReasonCode) + "'");
            }
            if (!String.IsNullOrEmpty(filter.SessionAffinityOutcome))
            {
                conditions.Add("sessionaffinityoutcome = '" + _Driver.Sanitize(filter.SessionAffinityOutcome) + "'");
            }
            if (!String.IsNullOrEmpty(filter.StatusClass))
            {
                string statusClassCondition = BuildStatusClassCondition(filter.StatusClass);
                if (!String.IsNullOrEmpty(statusClassCondition))
                {
                    conditions.Add(statusClassCondition);
                }
            }
            if (!String.IsNullOrEmpty(filter.RequestorSourceIp))
            {
                conditions.Add("requestorsourceip = '" + _Driver.Sanitize(filter.RequestorSourceIp) + "'");
            }
            if (filter.HttpStatus.HasValue)
            {
                conditions.Add("httpstatus = " + filter.HttpStatus.Value);
            }

            return "WHERE " + String.Join(" AND ", conditions);
        }

        private static string BuildStatusClassExpression()
        {
            return "CASE " +
                   "WHEN httpstatus IS NULL THEN 'NoStatus' " +
                   "WHEN httpstatus >= 100 AND httpstatus < 200 THEN '1xx' " +
                   "WHEN httpstatus >= 200 AND httpstatus < 300 THEN '2xx' " +
                   "WHEN httpstatus >= 300 AND httpstatus < 400 THEN '3xx' " +
                   "WHEN httpstatus >= 400 AND httpstatus < 500 THEN '4xx' " +
                   "ELSE '5xx' END";
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

        private static Dictionary<string, long> ReadBucketCounts(DataTable table)
        {
            Dictionary<string, long> counts = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);
            if (table == null)
            {
                return counts;
            }

            foreach (DataRow row in table.Rows)
            {
                string key = row["bucket_key"]?.ToString();
                if (String.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                counts[key] = Convert.ToInt64(row["bucket_count"]);
            }

            return counts;
        }
    }
}
