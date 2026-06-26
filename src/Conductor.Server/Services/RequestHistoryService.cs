namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Service for capturing and persisting request/response history.
    /// </summary>
    public class RequestHistoryService
    {
        private static readonly string _Header = "[RequestHistoryService] ";

        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly RequestHistorySettings _Settings;
        private readonly Serializer _Serializer;
        private readonly RequestAnalyticsCaptureProcessor _AnalyticsProcessor;
        private readonly string _Directory;

        /// <summary>
        /// Instantiate the request history service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="settings">Request history settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when database, logging, or settings is null.</exception>
        public RequestHistoryService(DatabaseDriverBase database, LoggingModule logging, RequestHistorySettings settings)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Serializer = new Serializer();
            _AnalyticsProcessor = new RequestAnalyticsCaptureProcessor(_Settings);
            _Directory = Path.GetFullPath(settings.Directory);

            // Ensure directory exists
            if (!System.IO.Directory.Exists(_Directory))
            {
                System.IO.Directory.CreateDirectory(_Directory);
                _Logging.Info(_Header + "created request history directory: " + _Directory);
            }
        }

        /// <summary>
        /// Check if request history is enabled globally.
        /// </summary>
        public bool IsEnabled => _Settings.Enabled;

        /// <summary>
        /// Maximum request body bytes to capture.
        /// </summary>
        public int MaxRequestBodyBytes => _Settings.MaxRequestBodyBytes;

        /// <summary>
        /// Maximum response body bytes to capture.
        /// </summary>
        public int MaxResponseBodyBytes => _Settings.MaxResponseBodyBytes;

        /// <summary>
        /// Create a new request history entry at the start of a request.
        /// </summary>
        /// <param name="vmr">Virtual model runner handling the request.</param>
        /// <param name="requestContext">Request context with authenticated caller information.</param>
        /// <param name="httpUrl">HTTP URL.</param>
        /// <param name="requestHeaders">Request headers.</param>
        /// <param name="requestBody">Request body (will be truncated if too large).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>RequestHistoryDetail with the entry data, or null if creation fails.</returns>
        public async Task<RequestHistoryDetail> CreateEntryAsync(
            VirtualModelRunner vmr,
            RequestContext requestContext,
            string httpUrl,
            Dictionary<string, string> requestHeaders,
            string requestBody,
            CancellationToken token = default)
        {
            if (vmr == null) throw new ArgumentNullException(nameof(vmr));
            if (requestContext == null) throw new ArgumentNullException(nameof(requestContext));
            if (String.IsNullOrEmpty(httpUrl)) throw new ArgumentNullException(nameof(httpUrl));

            try
            {
                string id = IdGenerator.NewRequestHistoryId();
                string objectKey = GenerateObjectKey(id);

                Dictionary<string, string> persistedRequestHeaders = RedactHeaders(requestHeaders, out bool requestHeadersRedacted);
                string persistedRequestBody = PrepareBodyForPersistence(
                    requestBody,
                    _Settings.CaptureRequestBody,
                    _Settings.MaxRequestBodyBytes,
                    out bool requestBodyRetained,
                    out bool requestBodyRedacted,
                    out bool requestBodyTruncated);

                RequestHistoryDetail detail = new RequestHistoryDetail
                {
                    Id = id,
                    TenantGuid = vmr.TenantId,
                    VirtualModelRunnerGuid = vmr.Id,
                    VirtualModelRunnerName = vmr.Name,
                    RequestorUserGuid = requestContext.UserId,
                    RequestorUserEmail = requestContext.UserEmail,
                    CredentialGuid = requestContext.CredentialId,
                    CredentialName = requestContext.CredentialName,
                    RequestorSourceIp = requestContext.ClientIpAddress ?? "unknown",
                    HttpMethod = requestContext.HttpMethod ?? "GET",
                    HttpUrl = httpUrl,
                    RequestBodyLength = requestBody?.Length ?? 0,
                    ObjectKey = objectKey,
                    CreatedUtc = DateTime.UtcNow,
                    RequestHeaders = persistedRequestHeaders,
                    RequestHeadersRedacted = requestHeadersRedacted,
                    RequestBody = persistedRequestBody,
                    RequestBodyRetained = requestBodyRetained,
                    RequestBodyRedacted = requestBodyRedacted,
                    RequestBodyTruncated = requestBodyTruncated,
                    RequestTransferType = requestContext.IsChunkedRequest ? TransferTypeEnum.Chunked : TransferTypeEnum.Normal
                };

                // Create database entry
                await _Database.RequestHistory.CreateAsync(detail, token).ConfigureAwait(false);

                _Logging.Debug(_Header + "created request history entry " + id + " for VMR " + vmr.Name);

                return detail;
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to create request history entry: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Update a request history entry with response data.
        /// </summary>
        /// <param name="detail">Request history detail to update.</param>
        /// <param name="routingDecision">Structured routing decision captured during request processing.</param>
        /// <param name="endpoint">Model endpoint that handled the request (may be null).</param>
        /// <param name="modelDefinition">Model definition used (may be null).</param>
        /// <param name="modelConfiguration">Model configuration used (may be null).</param>
        /// <param name="httpStatus">HTTP response status code.</param>
        /// <param name="responseHeaders">Response headers.</param>
        /// <param name="responseBody">Response body (will be truncated if too large).</param>
        /// <param name="stopwatch">Stopwatch started at request beginning.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="firstTokenTimeMs">Time to first token/byte in milliseconds. If null, response time is used.</param>
        /// <param name="analyticsCapture">Captured proxy-stage timings and transfer sizes.</param>
        /// <returns>Task.</returns>
        public async Task UpdateWithResponseAsync(
            RequestHistoryDetail detail,
            RoutingDecision routingDecision,
            ModelRunnerEndpoint endpoint,
            ModelDefinition modelDefinition,
            ModelConfiguration modelConfiguration,
            int httpStatus,
            Dictionary<string, string> responseHeaders,
            string responseBody,
            Stopwatch stopwatch,
            CancellationToken token = default,
            int? firstTokenTimeMs = null,
            RequestAnalyticsCapture analyticsCapture = null)
        {
            if (detail == null) return;

            try
            {
                // Update with endpoint/model info
                ApplyRoutingDecision(detail, routingDecision);

                if (endpoint != null)
                {
                    detail.ModelEndpointGuid = endpoint.Id;
                    detail.ModelEndpointName = endpoint.Name;
                    detail.ModelEndpointUrl = endpoint.GetBaseUrl();
                }

                if (modelDefinition != null)
                {
                    detail.ModelDefinitionGuid = modelDefinition.Id;
                    detail.ModelDefinitionName = modelDefinition.Name;
                }

                if (modelConfiguration != null)
                {
                    detail.ModelConfigurationGuid = modelConfiguration.Id;
                }

                // Update response data
                detail.HttpStatus = httpStatus;
                detail.ResponseBodyLength = responseBody?.Length ?? 0;
                detail.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;
                detail.FirstTokenTimeMs = firstTokenTimeMs ?? detail.ResponseTimeMs;
                detail.CompletedUtc = DateTime.UtcNow;
                detail.ResponseHeaders = RedactHeaders(responseHeaders, out bool responseHeadersRedacted);
                detail.ResponseHeadersRedacted = responseHeadersRedacted;

                detail.ResponseBody = PrepareBodyForPersistence(
                    responseBody,
                    _Settings.CaptureResponseBody,
                    _Settings.MaxResponseBodyBytes,
                    out bool responseBodyRetained,
                    out bool responseBodyRedacted,
                    out bool responseBodyTruncated);
                detail.ResponseBodyRetained = responseBodyRetained;
                detail.ResponseBodyRedacted = responseBodyRedacted;
                detail.ResponseBodyTruncated = responseBodyTruncated;

                RequestProviderMetrics providerMetrics = _AnalyticsProcessor.ApplyProviderAnalytics(detail, endpoint, responseHeaders, responseBody, analyticsCapture);
                List<RequestAnalyticsEvent> analyticsEvents = _AnalyticsProcessor.BuildAnalyticsEvents(
                    detail,
                    routingDecision,
                    endpoint,
                    httpStatus,
                    analyticsCapture,
                    providerMetrics,
                    firstTokenTimeMs);
                _AnalyticsProcessor.ApplyAnalyticsSummary(detail, analyticsEvents);

                // Update database entry
                await _Database.RequestHistory.UpdateAsync(detail, token).ConfigureAwait(false);

                if (analyticsEvents.Count > 0 && _Database.RequestAnalytics != null)
                {
                    try
                    {
                        await _Database.RequestAnalytics.CreateManyAsync(analyticsEvents, token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _Logging.Warn(_Header + "failed to persist request analytics events for " + detail.Id + ": " + ex.Message);
                    }
                }

                // Persist full detail to filesystem
                await PersistDetailAsync(detail, token).ConfigureAwait(false);

                _Logging.Debug(_Header + "updated request history entry " + detail.Id + " status=" + httpStatus + " time=" + detail.ResponseTimeMs + "ms");
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to update request history entry " + detail.Id + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Get a request history entry by ID.
        /// </summary>
        /// <param name="id">Entry ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>RequestHistoryEntry or null if not found.</returns>
        public async Task<RequestHistoryEntry> GetEntryAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) return null;
            return await _Database.RequestHistory.ReadByIdAsync(id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the full request history detail including headers and bodies.
        /// </summary>
        /// <param name="id">Entry ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>RequestHistoryDetail or null if not found.</returns>
        public async Task<RequestHistoryDetail> GetDetailAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) return null;

            RequestHistoryEntry entry = await _Database.RequestHistory.ReadByIdAsync(id, token).ConfigureAwait(false);
            if (entry == null) return null;

            return await LoadDetailAsync(entry, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get request analytics events for a request history entry.
        /// </summary>
        /// <param name="id">Request history ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Analytics detail result.</returns>
        public async Task<RequestAnalyticsDetailResult> GetAnalyticsAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) return null;

            RequestHistoryEntry entry = await _Database.RequestHistory.ReadByIdAsync(id, token).ConfigureAwait(false);
            if (entry == null) return null;

            List<RequestAnalyticsEvent> events = _Database.RequestAnalytics != null
                ? await _Database.RequestAnalytics.ListByRequestHistoryIdAsync(id, token).ConfigureAwait(false)
                : new List<RequestAnalyticsEvent>();

            return new RequestAnalyticsDetailResult
            {
                RequestHistoryId = entry.Id,
                TraceId = entry.TraceId,
                AnalyticsCaptured = entry.AnalyticsCaptured,
                AnalyticsFailureCode = entry.AnalyticsFailureCode,
                Events = events
            };
        }

        /// <summary>
        /// Get aggregate request analytics.
        /// </summary>
        /// <param name="filter">Analytics filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Aggregate request analytics.</returns>
        public async Task<RequestAnalyticsOverviewResult> GetAnalyticsOverviewAsync(RequestAnalyticsFilter filter, CancellationToken token = default)
        {
            filter ??= new RequestAnalyticsFilter();
            RequestAnalyticsOverviewBuilder.NormalizeFilter(filter);
            if (RequestAnalyticsOverviewBuilder.IsLargeScan(filter))
            {
                _Logging.Warn(
                    _Header
                    + "large request analytics overview scan requested"
                    + " tenant=" + (filter.TenantGuid ?? "(all)")
                    + " startUtc=" + filter.StartUtc
                    + " endUtc=" + filter.EndUtc
                    + " limit=" + filter.Limit
                    + " bucketSeconds=" + filter.BucketSeconds);
            }

            RequestHistorySearchFilter historyFilter = new RequestHistorySearchFilter
            {
                TenantGuid = filter.TenantGuid,
                VirtualModelRunnerGuid = filter.VirtualModelRunnerGuid,
                ModelEndpointGuid = filter.ModelEndpointGuid,
                ModelName = filter.ModelName,
                ModelAccessPolicyGuid = filter.ModelAccessPolicyGuid,
                ModelAccessRuleGuid = filter.ModelAccessRuleGuid,
                ModelAccessDecision = filter.ModelAccessDecision,
                ModelAccessWouldDeny = filter.ModelAccessWouldDeny,
                ReservationGuid = filter.ReservationGuid,
                ReservationDecision = filter.ReservationDecision,
                ReservationReasonCode = filter.ReservationReasonCode,
                StatusClass = filter.StatusClass,
                CreatedAfterUtc = filter.StartUtc,
                CreatedBeforeUtc = filter.EndUtc,
                Page = 1,
                PageSize = filter.Limit
            };

            List<RequestHistoryEntry> entries = new List<RequestHistoryEntry>();
            while (entries.Count < filter.Limit)
            {
                RequestHistorySearchResult historyResult = await _Database.RequestHistory.SearchAsync(historyFilter, token).ConfigureAwait(false);
                if (historyResult == null || historyResult.Data == null || historyResult.Data.Count < 1)
                {
                    break;
                }

                entries.AddRange(historyResult.Data);
                if (historyResult.Data.Count < historyFilter.PageSize)
                {
                    break;
                }

                historyFilter.Page++;
            }

            if (entries.Count > filter.Limit)
            {
                entries = entries.Take(filter.Limit).ToList();
            }

            List<RequestAnalyticsEvent> events = _Database.RequestAnalytics != null
                ? await _Database.RequestAnalytics.SearchAsync(filter, token).ConfigureAwait(false)
                : new List<RequestAnalyticsEvent>();

            return RequestAnalyticsOverviewBuilder.BuildOverview(filter, entries, events);
        }

        /// <summary>
        /// Search request history entries.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result with pagination info.</returns>
        public async Task<RequestHistorySearchResult> SearchAsync(RequestHistorySearchFilter filter, CancellationToken token = default)
        {
            return await _Database.RequestHistory.SearchAsync(filter, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get aggregated request history summary with time-bucketed counts.
        /// </summary>
        /// <param name="filter">Summary filter specifying time range, interval, and optional VMR filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Summary result with time-bucketed success/failure counts.</returns>
        public async Task<RequestHistorySummaryResult> GetSummaryAsync(RequestHistorySummaryFilter filter, CancellationToken token = default)
        {
            return await _Database.RequestHistory.GetSummaryAsync(filter, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a request history entry and its associated file.
        /// </summary>
        /// <param name="id">Entry ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) return;

            RequestHistoryEntry entry = await _Database.RequestHistory.ReadByIdAsync(id, token).ConfigureAwait(false);
            if (entry == null) return;

            // Delete file
            DeleteFile(entry.ObjectKey);

            if (_Database.RequestAnalytics != null)
            {
                await _Database.RequestAnalytics.DeleteByRequestHistoryIdAsync(id, token).ConfigureAwait(false);
            }

            // Delete database entry
            await _Database.RequestHistory.DeleteByIdAsync(id, token).ConfigureAwait(false);

            _Logging.Debug(_Header + "deleted request history entry " + id);
        }

        /// <summary>
        /// Delete request history entries matching a filter.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of deleted entries.</returns>
        public async Task<long> DeleteBulkAsync(RequestHistorySearchFilter filter, CancellationToken token = default)
        {
            RequestHistorySearchFilter workingFilter = filter ?? new RequestHistorySearchFilter();
            workingFilter.Page = 1;
            workingFilter.PageSize = 100;

            long totalDeleted = 0;
            List<string> requestHistoryIds = new List<string>();
            RequestHistorySearchResult result = await _Database.RequestHistory.SearchAsync(workingFilter, token).ConfigureAwait(false);
            while (result.Data.Count > 0)
            {
                foreach (RequestHistoryEntry entry in result.Data)
                {
                    DeleteFile(entry.ObjectKey);
                    requestHistoryIds.Add(entry.Id);
                }
                totalDeleted += result.Data.Count;

                if (result.Data.Count < 100) break;

                workingFilter.Page++;
                result = await _Database.RequestHistory.SearchAsync(workingFilter, token).ConfigureAwait(false);
            }

            if (_Database.RequestAnalytics != null)
            {
                foreach (string requestHistoryId in requestHistoryIds)
                {
                    await _Database.RequestAnalytics.DeleteByRequestHistoryIdAsync(requestHistoryId, token).ConfigureAwait(false);
                }
            }

            // Delete database entries
            await _Database.RequestHistory.DeleteBulkAsync(filter, token).ConfigureAwait(false);

            _Logging.Info(_Header + "bulk deleted " + totalDeleted + " request history entries");
            return totalDeleted;
        }

        private string GenerateObjectKey(string id)
        {
            // Use a simple flat file structure with the ID as filename
            return id + ".json";
        }

        private string GetFilePath(string objectKey)
        {
            return Path.Combine(_Directory, objectKey);
        }

        private async Task PersistDetailAsync(RequestHistoryDetail detail, CancellationToken token = default)
        {
            try
            {
                string filePath = GetFilePath(detail.ObjectKey);
                string json = _Serializer.SerializeJson(detail, true);
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to persist detail file for " + detail.Id + ": " + ex.Message);
            }
        }

        private async Task<RequestHistoryDetail> LoadDetailAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            try
            {
                string filePath = GetFilePath(entry.ObjectKey);
                if (!File.Exists(filePath))
                {
                    // Return basic detail from entry if file doesn't exist
                    return ApplyBodyRetention(RequestHistoryDetail.FromEntry(entry));
                }

                string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, token).ConfigureAwait(false);
                return ApplyBodyRetention(_Serializer.DeserializeJson<RequestHistoryDetail>(json) ?? RequestHistoryDetail.FromEntry(entry));
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to load detail file for " + entry.Id + ": " + ex.Message);
                return ApplyBodyRetention(RequestHistoryDetail.FromEntry(entry));
            }
        }

        private void DeleteFile(string objectKey)
        {
            try
            {
                string filePath = GetFilePath(objectKey);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to delete file " + objectKey + ": " + ex.Message);
            }
        }

        private void ApplyRoutingDecision(RequestHistoryDetail detail, RoutingDecision routingDecision)
        {
            if (detail == null || routingDecision == null) return;

            detail.RoutingDecision = routingDecision;
            detail.LoadBalancingPolicyGuid = routingDecision.LoadBalancingPolicyId;
            detail.LoadBalancingPolicyName = routingDecision.LoadBalancingPolicyName;
            detail.ModelAccessPolicyGuid = routingDecision.ModelAccessPolicyId;
            detail.ModelAccessPolicyName = routingDecision.ModelAccessPolicyName;
            detail.ModelAccessRuleGuid = routingDecision.ModelAccessRuleId;
            detail.ModelAccessRuleName = routingDecision.ModelAccessRuleName;
            detail.ModelAccessDecision = routingDecision.ModelAccessDecision?.ToString();
            detail.ModelAccessWouldDeny = routingDecision.ModelAccessWouldDeny;
            detail.RequestedModel = routingDecision.RequestedModel;
            detail.EffectiveModel = routingDecision.EffectiveModel;
            detail.RequestType = routingDecision.RequestType.ToString();
            detail.RoutingOutcomeCode = routingDecision.OutcomeCode;
            detail.SelectionStrategy = routingDecision.SelectionStrategy;
            detail.EndpointGroupGuid = routingDecision.SelectedEndpointGroupId;
            detail.EndpointGroupName = routingDecision.SelectedEndpointGroupName;
            detail.BackoffReason = routingDecision.BackoffReason;
            detail.AdaptiveSelection = routingDecision.AdaptiveModeUsed;
            detail.PolicyFallbackUsed = routingDecision.PolicyFallbackUsed;
            detail.DenialReasonCode = routingDecision.DenialReasonCode;
            detail.DenialReason = routingDecision.DenialReason;
            detail.ReservationGuid = routingDecision.ReservationId;
            detail.ReservationName = routingDecision.ReservationName;
            detail.ReservationDecision = routingDecision.ReservationDecision?.ToString();
            detail.ReservationReasonCode = routingDecision.ReservationReasonCode;
            detail.ReservationWindowStartUtc = routingDecision.ReservationWindowStartUtc;
            detail.ReservationWindowEndUtc = routingDecision.ReservationWindowEndUtc;
            detail.SessionAffinityOutcome = routingDecision.SessionAffinityOutcome;
            detail.ExplanationSummary = routingDecision.Message;
            detail.MutationSummary = BuildMutationSummary(routingDecision.MutationSummary);
        }

        private string BuildMutationSummary(RequestMutationSummary mutationSummary)
        {
            if (mutationSummary == null || mutationSummary.Changes.Count < 1)
            {
                return null;
            }

            List<string> changes = new List<string>();
            foreach (RequestMutationDetail change in mutationSummary.Changes)
            {
                changes.Add(change.PropertyName + ": " + (change.RequestedValue ?? "(unset)") + " -> " + (change.EffectiveValue ?? "(unset)"));
            }

            return String.Join("; ", changes);
        }

        private Dictionary<string, string> RedactHeaders(Dictionary<string, string> headers, out bool redacted)
        {
            redacted = false;
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            if (headers == null) return result;

            HashSet<string> redactedHeaders = new HashSet<string>(_Settings.RedactedHeaders ?? new List<string>(), StringComparer.InvariantCultureIgnoreCase);
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (String.IsNullOrWhiteSpace(header.Key)) continue;

                if (redactedHeaders.Contains(header.Key))
                {
                    result[header.Key] = "[REDACTED]";
                    redacted = true;
                }
                else
                {
                    result[header.Key] = header.Value;
                }
            }

            return result;
        }

        private string PrepareBodyForPersistence(
            string body,
            bool captureEnabled,
            int maxLength,
            out bool retained,
            out bool redacted,
            out bool truncated)
        {
            retained = false;
            redacted = false;
            truncated = false;

            if (!captureEnabled || String.IsNullOrEmpty(body))
            {
                return null;
            }

            retained = true;
            string persisted = RedactBody(body, out redacted);
            if (!String.IsNullOrEmpty(persisted) && persisted.Length > maxLength)
            {
                persisted = persisted.Substring(0, maxLength);
                truncated = true;
            }

            return persisted;
        }

        private string RedactBody(string body, out bool redacted)
        {
            redacted = false;
            if (String.IsNullOrWhiteSpace(body))
            {
                return body;
            }

            if (_Settings.RedactedJsonFields == null || _Settings.RedactedJsonFields.Count < 1)
            {
                return body;
            }

            try
            {
                JsonNode node = JsonNode.Parse(body);
                if (node == null)
                {
                    return body;
                }

                HashSet<string> redactedFields = new HashSet<string>(_Settings.RedactedJsonFields, StringComparer.InvariantCultureIgnoreCase);
                RedactNode(node, redactedFields, ref redacted);
                return node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
            }
            catch
            {
                return body;
            }
        }

        private void RedactNode(JsonNode node, HashSet<string> fields, ref bool redacted)
        {
            if (node is JsonObject obj)
            {
                foreach (KeyValuePair<string, JsonNode> child in obj.ToList())
                {
                    if (fields.Contains(child.Key))
                    {
                        obj[child.Key] = "[REDACTED]";
                        redacted = true;
                    }
                    else if (child.Value != null)
                    {
                        RedactNode(child.Value, fields, ref redacted);
                    }
                }
            }
            else if (node is JsonArray array)
            {
                foreach (JsonNode child in array)
                {
                    if (child != null)
                    {
                        RedactNode(child, fields, ref redacted);
                    }
                }
            }
        }

        private RequestHistoryDetail ApplyBodyRetention(RequestHistoryDetail detail)
        {
            if (detail == null)
            {
                return null;
            }

            int bodyRetentionDays = _Settings.BodyRetentionDays > 0 ? _Settings.BodyRetentionDays : _Settings.RetentionDays;
            if (detail.CreatedUtc < DateTime.UtcNow.AddDays(-bodyRetentionDays))
            {
                detail.RequestBody = null;
                detail.ResponseBody = null;
                detail.RequestBodyRetained = false;
                detail.ResponseBodyRetained = false;
            }

            return detail;
        }

        private class ProviderMetrics
        {
            public string ProviderRequestId { get; set; } = null;
            public int? PromptTokens { get; set; } = null;
            public int? CompletionTokens { get; set; } = null;
            public int? TotalTokens { get; set; } = null;
            public int? ProviderLoadDurationMs { get; set; } = null;
            public int? ProviderPromptEvalDurationMs { get; set; } = null;
            public int? ProviderGenerationDurationMs { get; set; } = null;
            public int? ProviderTotalDurationMs { get; set; } = null;
            public string RawProviderMetrics { get; set; } = null;
        }
    }
}
