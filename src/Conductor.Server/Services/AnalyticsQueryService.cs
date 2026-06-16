namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using WatsonWebserver.Core;

    /// <summary>
    /// Query service for the Analytics workspace.
    /// </summary>
    public sealed class AnalyticsQueryService
    {
        private const int RetentionDays = 30;
        private const int MaxBucketCount = 720;
        private const int MaxLimit = 50000;
        private const int SearchPageSize = 100;

        private static readonly HashSet<string> SupportedMetricIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "request.count",
            "request.successful_completions",
            "request.failed",
            "request.denied",
            "request.reservation_denied",
            "request.rate_limited",
            "latency.ttft.avg",
            "latency.ttft.p50",
            "latency.ttft.p90",
            "latency.ttft.p95",
            "latency.ttft.p99",
            "tokens.prompt",
            "tokens.completion",
            "tokens.total",
            "tokens.cached",
            "tokens.multimodal",
            "tokens.per_second",
            "cost.estimated",
            "analytics.coverage"
        };

        private static readonly HashSet<string> SupportedDimensionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TenantId",
            "RequestedModel",
            "EffectiveModel",
            "Model",
            "ModelDefinitionId",
            "ModelRunnerEndpointId",
            "EndpointId",
            "VirtualModelRunnerId",
            "VmrId",
            "RequestorUserId",
            "UserId",
            "CredentialId",
            "ProviderName",
            "Provider",
            "ReservationId",
            "ReservationGuid",
            "ReservationName",
            "ReservationDecision",
            "ReservationReasonCode"
        };

        private static readonly HashSet<string> SupportedRangeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "lastHour",
            "lastDay",
            "lastWeek",
            "lastMonth",
            "custom"
        };

        private static readonly HashSet<string> SupportedStatusClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "1",
            "1xx",
            "2",
            "2xx",
            "3",
            "3xx",
            "4",
            "4xx",
            "5",
            "5xx",
            "nostatus"
        };

        private static readonly HashSet<string> SupportedStageKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "routing",
            "capacity_wait",
            "upstream_headers",
            "first_token_wait",
            "generation",
            "completion",
            "denial",
            "provider_load",
            "provider_prompt_eval",
            "provider_generation"
        };

        private readonly RequestHistoryService _RequestHistoryService;

        /// <summary>
        /// Instantiate the analytics query service.
        /// </summary>
        /// <param name="requestHistoryService">Request history service.</param>
        /// <exception cref="ArgumentNullException">Thrown when requestHistoryService is null.</exception>
        public AnalyticsQueryService(RequestHistoryService requestHistoryService)
        {
            _RequestHistoryService = requestHistoryService ?? throw new ArgumentNullException(nameof(requestHistoryService));
        }

        /// <summary>
        /// Get the analytics catalog.
        /// </summary>
        /// <returns>Analytics catalog.</returns>
        public AnalyticsCatalogResult GetCatalog()
        {
            AnalyticsCatalogResult result = new AnalyticsCatalogResult
            {
                RetentionDays = RetentionDays
            };

            result.Metrics.Add(CreateCatalogItem("request.count", "Requests", "Total matching requests.", "count", true));
            result.Metrics.Add(CreateCatalogItem("request.successful_completions", "Successful completions", "Requests with a successful completion outcome.", "count", true));
            result.Metrics.Add(CreateCatalogItem("request.failed", "Failed requests", "Requests with failed outcomes.", "count", true));
            result.Metrics.Add(CreateCatalogItem("request.denied", "Denied requests", "Requests denied by policy or routing.", "count", true));
            result.Metrics.Add(CreateCatalogItem("request.reservation_denied", "Reservation denials", "Requests explicitly denied by an active VMR reservation gate.", "count", true));
            result.Metrics.Add(CreateCatalogItem("request.rate_limited", "Rate-limited requests", "Requests that returned HTTP 429.", "count", true));
            result.Metrics.Add(CreateCatalogItem("latency.ttft.avg", "Average TTFT", "Average time from Conductor request received to first token received.", "milliseconds", true));
            result.Metrics.Add(CreateCatalogItem("latency.ttft.p50", "P50 TTFT", "P50 time from Conductor request received to first token received.", "milliseconds", true));
            result.Metrics.Add(CreateCatalogItem("latency.ttft.p95", "P95 TTFT", "P95 time from Conductor request received to first token received.", "milliseconds", true));
            result.Metrics.Add(CreateCatalogItem("latency.ttft.p99", "P99 TTFT", "P99 time from Conductor request received to first token received.", "milliseconds", true));
            result.Metrics.Add(CreateCatalogItem("tokens.prompt", "Prompt tokens", "Reported prompt/input tokens.", "tokens", true));
            result.Metrics.Add(CreateCatalogItem("tokens.completion", "Completion tokens", "Reported completion/output tokens.", "tokens", true));
            result.Metrics.Add(CreateCatalogItem("tokens.total", "Total tokens", "Reported total tokens.", "tokens", true));
            result.Metrics.Add(CreateCatalogItem("tokens.cached", "Cached tokens", "Reported cached tokens where provider parsers persist them.", "tokens", false));
            result.Metrics.Add(CreateCatalogItem("tokens.multimodal", "Multimodal tokens", "Reported multimodal tokens where provider parsers persist them.", "tokens", false));
            result.Metrics.Add(CreateCatalogItem("cost.estimated", "Estimated cost", "Estimate-only cost from successful reported tokens and supplied token unit cost.", "currency", true));
            result.Metrics.Add(CreateCatalogItem("analytics.coverage", "Analytics coverage", "Requests with detailed analytics capture.", "percent", true));

            result.Dimensions.Add(CreateCatalogItem("TenantId", "Tenant", "Tenant ID.", null, true));
            result.Dimensions.Add(CreateCatalogItem("RequestedModel", "Requested model", "Model requested by the caller.", null, true));
            result.Dimensions.Add(CreateCatalogItem("EffectiveModel", "Effective model", "Model used after Conductor mutation.", null, true));
            result.Dimensions.Add(CreateCatalogItem("ModelDefinitionId", "Model definition", "Model definition ID.", null, true));
            result.Dimensions.Add(CreateCatalogItem("ModelRunnerEndpointId", "Model endpoint", "Model runner endpoint ID.", null, true));
            result.Dimensions.Add(CreateCatalogItem("VirtualModelRunnerId", "VMR", "Virtual Model Runner ID.", null, true));
            result.Dimensions.Add(CreateCatalogItem("RequestorUserId", "User", "Requestor user ID.", null, true));
            result.Dimensions.Add(CreateCatalogItem("CredentialId", "Credential", "Credential ID.", null, true));
            result.Dimensions.Add(CreateCatalogItem("ProviderName", "Provider", "Provider family.", null, true));
            result.Dimensions.Add(CreateCatalogItem("ReservationGuid", "Reservation", "VMR reservation ID.", null, true));
            result.Dimensions.Add(CreateCatalogItem("ReservationName", "Reservation name", "VMR reservation display name.", null, true));
            result.Dimensions.Add(CreateCatalogItem("ReservationDecision", "Reservation decision", "Reservation gate decision.", null, true));
            result.Dimensions.Add(CreateCatalogItem("ReservationReasonCode", "Reservation reason", "Reservation gate reason code.", null, true));

            result.Ranges.Add(CreateCatalogItem("lastHour", "Last hour", "Last one hour.", null, true));
            result.Ranges.Add(CreateCatalogItem("lastDay", "Last day", "Last 24 hours.", null, true));
            result.Ranges.Add(CreateCatalogItem("lastWeek", "Last week", "Last seven days.", null, true));
            result.Ranges.Add(CreateCatalogItem("lastMonth", "Last month", "Last 30 days.", null, true));
            result.Ranges.Add(CreateCatalogItem("custom", "Custom", "Custom start and end within 30-day retention.", null, true));

            result.Granularities.Add(CreateCatalogItem("60", "1 minute", "One-minute buckets.", "seconds", true));
            result.Granularities.Add(CreateCatalogItem("900", "15 minutes", "Fifteen-minute buckets.", "seconds", true));
            result.Granularities.Add(CreateCatalogItem("3600", "1 hour", "One-hour buckets.", "seconds", true));
            result.Granularities.Add(CreateCatalogItem("21600", "6 hours", "Six-hour buckets.", "seconds", true));
            result.Granularities.Add(CreateCatalogItem("86400", "1 day", "One-day buckets.", "seconds", true));

            result.ExportFormats.Add(CreateCatalogItem("csv", "CSV", "Comma-separated values export.", null, false));
            result.ExportFormats.Add(CreateCatalogItem("json", "JSON", "JSON export.", null, false));
            result.ExportFormats.Add(CreateCatalogItem("parquet", "Parquet", "Parquet export.", null, false));
            result.ExportFormats.Add(CreateCatalogItem("pdf", "PDF", "PDF export.", null, false));
            result.ExportFormats.Add(CreateCatalogItem("dashboardLink", "Dashboard link", "Shareable dashboard link.", null, false));

            return result;
        }

        /// <summary>
        /// Execute an analytics query.
        /// </summary>
        /// <param name="tenantId">Tenant ID, or null for global scope.</param>
        /// <param name="request">Query request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Analytics query result.</returns>
        public async Task<AnalyticsQueryResult> QueryAsync(string tenantId, AnalyticsQueryRequest request, CancellationToken token = default)
        {
            AnalyticsQueryRequest normalizedRequest = NormalizeRequest(request);
            DateTime endUtc = normalizedRequest.EndUtc ?? DateTime.UtcNow;
            DateTime startUtc = normalizedRequest.StartUtc ?? ResolveRangeStart(normalizedRequest.Range, endUtc);
            if (startUtc >= endUtc)
            {
                startUtc = endUtc.AddDays(-1);
            }

            DateTime earliestAllowedStart = endUtc.AddDays(-RetentionDays);
            if (startUtc < earliestAllowedStart)
            {
                startUtc = earliestAllowedStart;
            }

            int bucketSeconds = NormalizeBucketSeconds(startUtc, endUtc, normalizedRequest.BucketSeconds);
            int limit = NormalizeLimit(normalizedRequest.Limit);
            List<RequestHistoryEntry> entries = await SearchEntriesAsync(tenantId, normalizedRequest, startUtc, endUtc, limit, token).ConfigureAwait(false);
            List<RequestHistoryEntry> successfulEntries = entries.Where(IsSuccessfulCompletion).ToList();

            AnalyticsQueryResult result = BuildResult(
                tenantId,
                normalizedRequest,
                entries,
                successfulEntries,
                startUtc,
                endUtc,
                bucketSeconds);

            return result;
        }

        /// <summary>
        /// Validate an analytics query definition without executing it.
        /// </summary>
        /// <param name="request">Analytics query request.</param>
        public static void ValidateRequestDefinition(AnalyticsQueryRequest request)
        {
            if (request == null)
            {
                return;
            }

            if (request.TokenUnitCost.HasValue && request.TokenUnitCost.Value < 0m)
            {
                throw BadRequest("TokenUnitCost cannot be negative.");
            }

            if (request.Limit < 1)
            {
                throw BadRequest("Limit must be greater than zero.");
            }

            if (request.BucketSeconds.HasValue && request.BucketSeconds.Value < 1)
            {
                throw BadRequest("BucketSeconds must be greater than zero.");
            }

            if (!String.IsNullOrWhiteSpace(request.Range) && !SupportedRangeIds.Contains(request.Range))
            {
                throw BadRequest("Unsupported analytics range: " + request.Range + ".");
            }

            if (String.Equals(request.Range, "custom", StringComparison.OrdinalIgnoreCase)
                && (!request.StartUtc.HasValue || !request.EndUtc.HasValue))
            {
                throw BadRequest("Custom analytics range requires StartUtc and EndUtc.");
            }

            if (request.StartUtc.HasValue && request.EndUtc.HasValue && request.StartUtc.Value >= request.EndUtc.Value)
            {
                throw BadRequest("StartUtc must be before EndUtc.");
            }

            ValidateRequestedValues("analytics metric", request.Metrics, SupportedMetricIds);

            if (request.GroupBy != null && request.GroupBy.Count > 1)
            {
                throw BadRequest("Only one GroupBy dimension is supported in this release.");
            }

            ValidateRequestedValues("analytics dimension", request.GroupBy, SupportedDimensionIds);

            if (request.Filters != null)
            {
                ValidateRequestedValues("analytics status class", request.Filters.StatusClasses, SupportedStatusClasses);
                ValidateRequestedValues("analytics stage kind", request.Filters.StageKinds, SupportedStageKinds);
            }
        }

        private static AnalyticsCatalogItem CreateCatalogItem(string id, string name, string description, string unit, bool available)
        {
            return new AnalyticsCatalogItem
            {
                Id = id,
                Name = name,
                Description = description,
                Unit = unit,
                Available = available
            };
        }

        private static AnalyticsQueryRequest NormalizeRequest(AnalyticsQueryRequest request)
        {
            AnalyticsQueryRequest normalized = request ?? new AnalyticsQueryRequest();
            if (normalized.Filters == null)
            {
                normalized.Filters = new AnalyticsQueryFilters();
            }

            if (String.IsNullOrWhiteSpace(normalized.Range))
            {
                normalized.Range = "lastDay";
            }

            if (String.IsNullOrWhiteSpace(normalized.Timezone))
            {
                normalized.Timezone = "UTC";
            }

            normalized.Range = normalized.Range.Trim();
            normalized.Timezone = normalized.Timezone.Trim();
            normalized.CostCurrency = String.IsNullOrWhiteSpace(normalized.CostCurrency) ? null : normalized.CostCurrency.Trim();
            normalized.Metrics = NormalizeStringList(normalized.Metrics);
            normalized.GroupBy = NormalizeStringList(normalized.GroupBy);
            normalized.Filters.VirtualModelRunnerIds = NormalizeStringList(normalized.Filters.VirtualModelRunnerIds);
            normalized.Filters.ModelRunnerEndpointIds = NormalizeStringList(normalized.Filters.ModelRunnerEndpointIds);
            normalized.Filters.ModelNames = NormalizeStringList(normalized.Filters.ModelNames);
            normalized.Filters.RequestorUserIds = NormalizeStringList(normalized.Filters.RequestorUserIds);
            normalized.Filters.CredentialIds = NormalizeStringList(normalized.Filters.CredentialIds);
            normalized.Filters.ProviderNames = NormalizeStringList(normalized.Filters.ProviderNames);
            normalized.Filters.ReservationIds = NormalizeStringList(normalized.Filters.ReservationIds);
            normalized.Filters.ReservationDecisions = NormalizeStringList(normalized.Filters.ReservationDecisions);
            normalized.Filters.ReservationReasonCodes = NormalizeStringList(normalized.Filters.ReservationReasonCodes);
            normalized.Filters.StatusClasses = NormalizeStringList(normalized.Filters.StatusClasses);
            normalized.Filters.StageKinds = NormalizeStringList(normalized.Filters.StageKinds);

            ValidateRequestDefinition(normalized);

            return normalized;
        }

        private async Task<List<RequestHistoryEntry>> SearchEntriesAsync(
            string tenantId,
            AnalyticsQueryRequest request,
            DateTime startUtc,
            DateTime endUtc,
            int limit,
            CancellationToken token)
        {
            RequestHistorySearchFilter filter = new RequestHistorySearchFilter
            {
                TenantGuid = tenantId,
                VirtualModelRunnerGuid = GetSingleFilterValue(request.Filters.VirtualModelRunnerIds),
                ModelEndpointGuid = GetSingleFilterValue(request.Filters.ModelRunnerEndpointIds),
                RequestorUserGuid = GetSingleFilterValue(request.Filters.RequestorUserIds),
                CredentialGuid = GetSingleFilterValue(request.Filters.CredentialIds),
                ModelName = GetSingleFilterValue(request.Filters.ModelNames),
                ReservationGuid = GetSingleFilterValue(request.Filters.ReservationIds),
                ReservationDecision = GetSingleFilterValue(request.Filters.ReservationDecisions),
                ReservationReasonCode = GetSingleFilterValue(request.Filters.ReservationReasonCodes),
                StatusClass = GetSingleFilterValue(request.Filters.StatusClasses),
                ModelAccessWouldDeny = request.Filters.ModelAccessWouldDeny,
                CreatedAfterUtc = startUtc,
                CreatedBeforeUtc = endUtc,
                Page = 1,
                PageSize = SearchPageSize
            };

            List<RequestHistoryEntry> entries = new List<RequestHistoryEntry>();
            while (entries.Count < limit)
            {
                token.ThrowIfCancellationRequested();
                filter.PageSize = Math.Min(SearchPageSize, limit - entries.Count);
                RequestHistorySearchResult page = await _RequestHistoryService.SearchAsync(filter, token).ConfigureAwait(false);
                if (page == null || page.Data == null || page.Data.Count < 1)
                {
                    break;
                }

                entries.AddRange(page.Data);
                if (page.Data.Count < filter.PageSize)
                {
                    break;
                }

                filter.Page++;
            }

            return entries
                .Where(item => MatchesList(item.VirtualModelRunnerGuid, request.Filters.VirtualModelRunnerIds))
                .Where(item => MatchesList(item.ModelEndpointGuid, request.Filters.ModelRunnerEndpointIds))
                .Where(item => MatchesList(item.RequestorUserGuid, request.Filters.RequestorUserIds))
                .Where(item => MatchesList(item.CredentialGuid, request.Filters.CredentialIds))
                .Where(item => MatchesModelName(item, request.Filters.ModelNames))
                .Where(item => MatchesList(item.ProviderName, request.Filters.ProviderNames))
                .Where(item => MatchesList(item.ReservationGuid, request.Filters.ReservationIds))
                .Where(item => MatchesList(item.ReservationDecision, request.Filters.ReservationDecisions))
                .Where(item => MatchesList(item.ReservationReasonCode, request.Filters.ReservationReasonCodes))
                .Where(item => MatchesStatusClassList(item, request.Filters.StatusClasses))
                .Where(item => MatchesList(item.DominantStageKind, request.Filters.StageKinds))
                .ToList();
        }

        private static List<string> NormalizeStringList(List<string> values)
        {
            if (values == null || values.Count < 1)
            {
                return new List<string>();
            }

            return values
                .Where(item => !String.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ValidateRequestedValues(string fieldName, List<string> values, HashSet<string> supportedValues)
        {
            if (values == null || values.Count < 1)
            {
                return;
            }

            foreach (string value in values)
            {
                if (!supportedValues.Contains(value))
                {
                    throw BadRequest("Unsupported " + fieldName + ": " + value + ".");
                }
            }
        }

        private static WebserverException BadRequest(string message)
        {
            return new WebserverException(ApiResultEnum.BadRequest, message);
        }

        private static AnalyticsQueryResult BuildResult(
            string tenantId,
            AnalyticsQueryRequest request,
            List<RequestHistoryEntry> entries,
            List<RequestHistoryEntry> successfulEntries,
            DateTime startUtc,
            DateTime endUtc,
            int bucketSeconds)
        {
            List<int> ttftValues = successfulEntries
                .Where(item => item.FirstTokenTimeMs.HasValue)
                .Select(item => item.FirstTokenTimeMs.Value)
                .OrderBy(item => item)
                .ToList();

            long totalTokens = SumTokens(successfulEntries, item => item.TotalTokens);

            AnalyticsQueryResult result = new AnalyticsQueryResult
            {
                TenantId = tenantId,
                IsGlobalScope = String.IsNullOrEmpty(tenantId),
                StartUtc = startUtc,
                EndUtc = endUtc,
                BucketSeconds = bucketSeconds,
                RetentionDays = RetentionDays,
                TotalRequests = entries.Count,
                SuccessfulCompletionCount = successfulEntries.Count,
                FailedRequestCount = entries.Count(item => !IsSuccessfulCompletion(item)),
                DeniedRequestCount = entries.Count(IsDenied),
                ReservationDeniedCount = entries.Count(IsReservationDenied),
                ReservationDenialCounts = BuildReservationDenialCounts(entries),
                RateLimitedRequestCount = entries.Count(IsRateLimited),
                AverageTimeToFirstTokenMs = AverageInt(ttftValues),
                P50TimeToFirstTokenMs = Percentile(ttftValues, 0.50m),
                P95TimeToFirstTokenMs = Percentile(ttftValues, 0.95m),
                P99TimeToFirstTokenMs = Percentile(ttftValues, 0.99m),
                PromptTokens = SumTokens(successfulEntries, item => item.PromptTokens),
                CompletionTokens = SumTokens(successfulEntries, item => item.CompletionTokens),
                TotalTokens = totalTokens,
                CachedTokens = null,
                MultimodalTokens = null,
                UnknownTokenUsageCount = successfulEntries.Count(item => !HasUsableTokenMetric(item)),
                TokenUnitCost = request.TokenUnitCost,
                CostCurrency = request.CostCurrency,
                EstimatedCost = CalculateEstimatedCost(totalTokens, request.TokenUnitCost)
            };

            result.TimeSeries = BuildTimeSeries(entries, successfulEntries, startUtc, endUtc, bucketSeconds, request.TokenUnitCost);
            result.Groups = BuildGroups(entries, successfulEntries, request.GroupBy, request.TokenUnitCost);
            return result;
        }

        private static List<AnalyticsTimeSeriesBucket> BuildTimeSeries(
            List<RequestHistoryEntry> entries,
            List<RequestHistoryEntry> successfulEntries,
            DateTime startUtc,
            DateTime endUtc,
            int bucketSeconds,
            decimal? tokenUnitCost)
        {
            List<AnalyticsTimeSeriesBucket> buckets = new List<AnalyticsTimeSeriesBucket>();
            DateTime cursor = FloorToBucket(startUtc, bucketSeconds);
            TimeSpan step = TimeSpan.FromSeconds(bucketSeconds);

            while (cursor < endUtc)
            {
                DateTime bucketStart = cursor;
                DateTime bucketEnd = cursor.Add(step);
                List<RequestHistoryEntry> bucketEntries = entries
                    .Where(item => item.CreatedUtc >= bucketStart && item.CreatedUtc < bucketEnd)
                    .ToList();
                List<RequestHistoryEntry> bucketSuccessfulEntries = successfulEntries
                    .Where(item => item.CreatedUtc >= bucketStart && item.CreatedUtc < bucketEnd)
                    .ToList();
                List<int> ttftValues = bucketSuccessfulEntries
                    .Where(item => item.FirstTokenTimeMs.HasValue)
                    .Select(item => item.FirstTokenTimeMs.Value)
                    .OrderBy(item => item)
                    .ToList();
                long totalTokens = SumTokens(bucketSuccessfulEntries, item => item.TotalTokens);

                buckets.Add(new AnalyticsTimeSeriesBucket
                {
                    TimestampUtc = bucketStart,
                    RequestCount = bucketEntries.Count,
                    SuccessfulCompletionCount = bucketSuccessfulEntries.Count,
                    FailedRequestCount = bucketEntries.Count(item => !IsSuccessfulCompletion(item)),
                    DeniedRequestCount = bucketEntries.Count(IsDenied),
                    RateLimitedRequestCount = bucketEntries.Count(IsRateLimited),
                    AverageTimeToFirstTokenMs = AverageInt(ttftValues),
                    PromptTokens = SumTokens(bucketSuccessfulEntries, item => item.PromptTokens),
                    CompletionTokens = SumTokens(bucketSuccessfulEntries, item => item.CompletionTokens),
                    TotalTokens = totalTokens,
                    CachedTokens = null,
                    MultimodalTokens = null,
                    UnknownTokenUsageCount = bucketSuccessfulEntries.Count(item => !HasUsableTokenMetric(item)),
                    EstimatedCost = CalculateEstimatedCost(totalTokens, tokenUnitCost)
                });

                cursor = bucketEnd;
            }

            return buckets;
        }

        private static List<AnalyticsGroupSummary> BuildGroups(
            List<RequestHistoryEntry> entries,
            List<RequestHistoryEntry> successfulEntries,
            List<string> groupBy,
            decimal? tokenUnitCost)
        {
            string dimension = groupBy != null && groupBy.Count > 0 ? groupBy[0] : "RequestorUserId";
            return entries
                .GroupBy(item => GetDimensionValue(item, dimension))
                .Select(group =>
                {
                    List<RequestHistoryEntry> groupedEntries = group.ToList();
                    List<RequestHistoryEntry> groupedSuccessfulEntries = successfulEntries
                        .Where(item => GetDimensionValue(item, dimension) == group.Key)
                        .ToList();
                    List<int> ttftValues = groupedSuccessfulEntries
                        .Where(item => item.FirstTokenTimeMs.HasValue)
                        .Select(item => item.FirstTokenTimeMs.Value)
                        .OrderBy(item => item)
                        .ToList();
                    long totalTokens = SumTokens(groupedSuccessfulEntries, item => item.TotalTokens);

                    return new AnalyticsGroupSummary
                    {
                        Dimension = dimension,
                        Value = group.Key,
                        Label = GetDimensionLabel(groupedEntries.FirstOrDefault(), dimension),
                        RequestCount = groupedEntries.Count,
                        SuccessfulCompletionCount = groupedSuccessfulEntries.Count,
                        FailedRequestCount = groupedEntries.Count(item => !IsSuccessfulCompletion(item)),
                        DeniedRequestCount = groupedEntries.Count(IsDenied),
                        RateLimitedRequestCount = groupedEntries.Count(IsRateLimited),
                        AverageTimeToFirstTokenMs = AverageInt(ttftValues),
                        P95TimeToFirstTokenMs = Percentile(ttftValues, 0.95m),
                        TotalTokens = totalTokens,
                        UnknownTokenUsageCount = groupedSuccessfulEntries.Count(item => !HasUsableTokenMetric(item)),
                        TimeToFirstTokenCoveragePercent = CalculateCoveragePercent(ttftValues.Count, groupedSuccessfulEntries.Count),
                        EstimatedCost = CalculateEstimatedCost(totalTokens, tokenUnitCost),
                        LastSeenUtc = groupedEntries.Max(item => item.CreatedUtc)
                    };
                })
                .OrderByDescending(item => item.TotalTokens)
                .ThenByDescending(item => item.RequestCount)
                .ToList();
        }

        private static string GetSingleFilterValue(List<string> values)
        {
            if (values == null || values.Count != 1)
            {
                return null;
            }

            return String.IsNullOrWhiteSpace(values[0]) ? null : values[0];
        }

        private static bool MatchesList(string value, List<string> filters)
        {
            if (filters == null || filters.Count < 1)
            {
                return true;
            }

            foreach (string filter in filters)
            {
                if (String.Equals(value ?? "", filter ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesModelName(RequestHistoryEntry entry, List<string> filters)
        {
            if (filters == null || filters.Count < 1)
            {
                return true;
            }

            foreach (string filter in filters)
            {
                if (String.IsNullOrWhiteSpace(filter))
                {
                    continue;
                }

                if ((entry.RequestedModel != null && entry.RequestedModel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (entry.EffectiveModel != null && entry.EffectiveModel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (entry.ModelDefinitionName != null && entry.ModelDefinitionName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesStatusClassList(RequestHistoryEntry entry, List<string> filters)
        {
            if (filters == null || filters.Count < 1)
            {
                return true;
            }

            foreach (string filter in filters)
            {
                if (MatchesStatusClass(entry, filter))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesStatusClass(RequestHistoryEntry entry, string statusClass)
        {
            if (String.IsNullOrWhiteSpace(statusClass))
            {
                return true;
            }

            if (!entry.HttpStatus.HasValue)
            {
                return String.Equals(statusClass, "nostatus", StringComparison.OrdinalIgnoreCase);
            }

            int status = entry.HttpStatus.Value;
            switch (statusClass.Trim().ToLowerInvariant())
            {
                case "1":
                case "1xx":
                    return status >= 100 && status < 200;
                case "2":
                case "2xx":
                    return status >= 200 && status < 300;
                case "3":
                case "3xx":
                    return status >= 300 && status < 400;
                case "4":
                case "4xx":
                    return status >= 400 && status < 500;
                case "5":
                case "5xx":
                    return status >= 500 && status < 600;
                default:
                    return true;
            }
        }

        private static bool IsSuccessfulCompletion(RequestHistoryEntry entry)
        {
            return entry != null
                && entry.HttpStatus.HasValue
                && entry.HttpStatus.Value >= 100
                && entry.HttpStatus.Value < 400;
        }

        private static bool IsDenied(RequestHistoryEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            return String.Equals(entry.ModelAccessDecision, "Deny", StringComparison.OrdinalIgnoreCase)
                || !String.IsNullOrEmpty(entry.DenialReasonCode);
        }

        private static bool IsReservationDenied(RequestHistoryEntry entry)
        {
            return String.Equals(entry?.ReservationReasonCode, "ReservationDenied", StringComparison.OrdinalIgnoreCase)
                || String.Equals(entry?.ReservationReasonCode, "ReservationDrainDenied", StringComparison.OrdinalIgnoreCase)
                || String.Equals(entry?.ReservationReasonCode, "ReservationAuthenticationRequired", StringComparison.OrdinalIgnoreCase)
                || String.Equals(entry?.ReservationReasonCode, "ReservationConflict", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, long> BuildReservationDenialCounts(List<RequestHistoryEntry> entries)
        {
            return entries
                .Where(IsReservationDenied)
                .GroupBy(item => String.IsNullOrEmpty(item.ReservationGuid) ? "Unknown" : item.ReservationGuid)
                .ToDictionary(group => group.Key, group => (long)group.Count(), StringComparer.InvariantCultureIgnoreCase);
        }

        private static bool IsRateLimited(RequestHistoryEntry entry)
        {
            return entry != null && entry.HttpStatus.HasValue && entry.HttpStatus.Value == 429;
        }

        private static bool HasUsableTokenMetric(RequestHistoryEntry entry)
        {
            return entry != null
                && (entry.TotalTokens.HasValue || entry.PromptTokens.HasValue || entry.CompletionTokens.HasValue);
        }

        private static long SumTokens(List<RequestHistoryEntry> entries, Func<RequestHistoryEntry, int?> selector)
        {
            return entries
                .Where(item => selector(item).HasValue)
                .Sum(item => (long)selector(item).Value);
        }

        private static decimal? CalculateEstimatedCost(long totalTokens, decimal? tokenUnitCost)
        {
            if (!tokenUnitCost.HasValue)
            {
                return null;
            }

            return Decimal.Round(totalTokens * tokenUnitCost.Value, 8);
        }

        private static decimal? CalculateCoveragePercent(int coveredCount, int totalCount)
        {
            if (totalCount < 1)
            {
                return null;
            }

            return Decimal.Round((decimal)coveredCount * 100m / totalCount, 2);
        }

        private static decimal? AverageInt(List<int> values)
        {
            if (values == null || values.Count < 1)
            {
                return null;
            }

            return Decimal.Round((decimal)values.Average(), 2);
        }

        private static int? Percentile(List<int> sortedValues, decimal percentile)
        {
            if (sortedValues == null || sortedValues.Count < 1)
            {
                return null;
            }

            int index = (int)Math.Ceiling(sortedValues.Count * percentile) - 1;
            index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));
            return sortedValues[index];
        }

        private static string GetDimensionValue(RequestHistoryEntry entry, string dimension)
        {
            if (entry == null)
            {
                return "(unknown)";
            }

            switch ((dimension ?? "").Trim().ToLowerInvariant())
            {
                case "tenantid":
                    return entry.TenantGuid ?? "(unknown)";
                case "requestedmodel":
                    return entry.RequestedModel ?? "(unknown)";
                case "effectivemodel":
                case "model":
                    return entry.EffectiveModel ?? "(unknown)";
                case "modeldefinitionid":
                    return entry.ModelDefinitionGuid ?? "(unknown)";
                case "modelrunnerendpointid":
                case "endpointid":
                    return entry.ModelEndpointGuid ?? "(unrouted)";
                case "virtualmodelrunnerid":
                case "vmrid":
                    return entry.VirtualModelRunnerGuid ?? "(unknown)";
                case "requestoruserid":
                case "userid":
                    return entry.RequestorUserGuid ?? "(anonymous)";
                case "credentialid":
                    return entry.CredentialGuid ?? "(none)";
                case "providername":
                case "provider":
                    return entry.ProviderName ?? "(unknown)";
                case "reservationid":
                case "reservationguid":
                    return entry.ReservationGuid ?? "(none)";
                case "reservationname":
                    return entry.ReservationName ?? "(none)";
                case "reservationdecision":
                    return entry.ReservationDecision ?? "(none)";
                case "reservationreasoncode":
                    return entry.ReservationReasonCode ?? "(none)";
                default:
                    return entry.RequestorUserGuid ?? "(anonymous)";
            }
        }

        private static string GetDimensionLabel(RequestHistoryEntry entry, string dimension)
        {
            if (entry == null)
            {
                return "(unknown)";
            }

            switch ((dimension ?? "").Trim().ToLowerInvariant())
            {
                case "requestedmodel":
                    return entry.RequestedModel ?? "(unknown)";
                case "effectivemodel":
                case "model":
                    return entry.EffectiveModel ?? "(unknown)";
                case "modeldefinitionid":
                    return entry.ModelDefinitionName ?? entry.ModelDefinitionGuid ?? "(unknown)";
                case "modelrunnerendpointid":
                case "endpointid":
                    return entry.ModelEndpointName ?? entry.ModelEndpointGuid ?? "(unrouted)";
                case "virtualmodelrunnerid":
                case "vmrid":
                    return entry.VirtualModelRunnerName ?? entry.VirtualModelRunnerGuid ?? "(unknown)";
                case "requestoruserid":
                case "userid":
                    return entry.RequestorUserEmail ?? entry.RequestorUserGuid ?? "(anonymous)";
                case "credentialid":
                    return entry.CredentialName ?? entry.CredentialGuid ?? "(none)";
                case "providername":
                case "provider":
                    return entry.ProviderName ?? "(unknown)";
                case "reservationid":
                case "reservationguid":
                    return entry.ReservationName ?? entry.ReservationGuid ?? "(none)";
                case "reservationname":
                    return entry.ReservationName ?? "(none)";
                case "reservationdecision":
                    return entry.ReservationDecision ?? "(none)";
                case "reservationreasoncode":
                    return entry.ReservationReasonCode ?? "(none)";
                case "tenantid":
                default:
                    return GetDimensionValue(entry, dimension);
            }
        }

        private static DateTime ResolveRangeStart(string range, DateTime endUtc)
        {
            switch ((range ?? "").Trim().ToLowerInvariant())
            {
                case "lasthour":
                    return endUtc.AddHours(-1);
                case "lastweek":
                    return endUtc.AddDays(-7);
                case "lastmonth":
                    return endUtc.AddDays(-30);
                case "lastday":
                default:
                    return endUtc.AddDays(-1);
            }
        }

        private static int NormalizeLimit(int limit)
        {
            if (limit < 1)
            {
                return 10000;
            }

            if (limit > MaxLimit)
            {
                return MaxLimit;
            }

            return limit;
        }

        private static int NormalizeBucketSeconds(DateTime startUtc, DateTime endUtc, int? requestedBucketSeconds)
        {
            int bucketSeconds = requestedBucketSeconds.HasValue && requestedBucketSeconds.Value > 0
                ? Math.Max(60, requestedBucketSeconds.Value)
                : ResolveBucketSeconds(startUtc, endUtc);
            int capSeconds = CalculateBucketSecondsForCap(startUtc, endUtc);
            return Math.Max(bucketSeconds, capSeconds);
        }

        private static int ResolveBucketSeconds(DateTime startUtc, DateTime endUtc)
        {
            double totalHours = (endUtc - startUtc).TotalHours;
            if (totalHours <= 2d) return 60;
            if (totalHours <= 36d) return 900;
            if (totalHours <= 240d) return 3600;
            return 21600;
        }

        private static int CalculateBucketSecondsForCap(DateTime startUtc, DateTime endUtc)
        {
            double totalSeconds = Math.Max(60d, (endUtc - startUtc).TotalSeconds);
            int minimumSeconds = (int)Math.Ceiling(totalSeconds / (MaxBucketCount - 1d));
            return Math.Max(60, minimumSeconds);
        }

        private static DateTime FloorToBucket(DateTime value, int bucketSeconds)
        {
            long ticks = TimeSpan.FromSeconds(bucketSeconds).Ticks;
            return new DateTime(value.Ticks - (value.Ticks % ticks), DateTimeKind.Utc);
        }
    }
}
