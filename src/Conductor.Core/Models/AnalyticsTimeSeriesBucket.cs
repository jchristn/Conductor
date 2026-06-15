namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Time-series bucket for analytics workspace results.
    /// </summary>
    public class AnalyticsTimeSeriesBucket
    {
        /// <summary>
        /// Bucket start timestamp.
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Total matching requests.
        /// </summary>
        public long RequestCount { get; set; }

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
        /// Estimate-only cost for this bucket.
        /// </summary>
        public decimal? EstimatedCost { get; set; }
    }
}
