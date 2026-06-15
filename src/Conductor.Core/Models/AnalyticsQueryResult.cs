namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Analytics workspace query result.
    /// </summary>
    public class AnalyticsQueryResult
    {
        /// <summary>
        /// Applied tenant ID, or null for global system-admin scope.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Whether this result used global scope.
        /// </summary>
        public bool IsGlobalScope { get; set; }

        /// <summary>
        /// Start timestamp.
        /// </summary>
        public DateTime StartUtc { get; set; }

        /// <summary>
        /// End timestamp.
        /// </summary>
        public DateTime EndUtc { get; set; }

        /// <summary>
        /// Bucket size in seconds.
        /// </summary>
        public int BucketSeconds { get; set; }

        /// <summary>
        /// Retained analytics data window in days.
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Total matching requests.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Successful completion count.
        /// </summary>
        public long SuccessfulCompletionCount { get; set; }

        /// <summary>
        /// Failed request count.
        /// </summary>
        public long FailedRequestCount { get; set; }

        /// <summary>
        /// Denied request count.
        /// </summary>
        public long DeniedRequestCount { get; set; }

        /// <summary>
        /// Rate-limited request count.
        /// </summary>
        public long RateLimitedRequestCount { get; set; }

        /// <summary>
        /// Average time to first token.
        /// </summary>
        public decimal? AverageTimeToFirstTokenMs { get; set; }

        /// <summary>
        /// P50 time to first token.
        /// </summary>
        public int? P50TimeToFirstTokenMs { get; set; }

        /// <summary>
        /// P95 time to first token.
        /// </summary>
        public int? P95TimeToFirstTokenMs { get; set; }

        /// <summary>
        /// P99 time to first token.
        /// </summary>
        public int? P99TimeToFirstTokenMs { get; set; }

        /// <summary>
        /// Prompt token total.
        /// </summary>
        public long PromptTokens { get; set; }

        /// <summary>
        /// Completion token total.
        /// </summary>
        public long CompletionTokens { get; set; }

        /// <summary>
        /// Total token count.
        /// </summary>
        public long TotalTokens { get; set; }

        /// <summary>
        /// Cached token count, if persisted by a provider parser.
        /// </summary>
        public long? CachedTokens { get; set; } = null;

        /// <summary>
        /// Multimodal token count, if persisted by a provider parser.
        /// </summary>
        public long? MultimodalTokens { get; set; } = null;

        /// <summary>
        /// Count of successful completions without usable token metrics.
        /// </summary>
        public long UnknownTokenUsageCount { get; set; }

        /// <summary>
        /// Token unit cost supplied by the caller.
        /// </summary>
        public decimal? TokenUnitCost { get; set; }

        /// <summary>
        /// Currency or display label supplied by the caller.
        /// </summary>
        public string CostCurrency { get; set; } = null;

        /// <summary>
        /// Estimate-only cost.
        /// </summary>
        public decimal? EstimatedCost { get; set; }

        /// <summary>
        /// Time-series buckets.
        /// </summary>
        public List<AnalyticsTimeSeriesBucket> TimeSeries { get; set; } = new List<AnalyticsTimeSeriesBucket>();

        /// <summary>
        /// Grouped summaries.
        /// </summary>
        public List<AnalyticsGroupSummary> Groups { get; set; } = new List<AnalyticsGroupSummary>();
    }
}
