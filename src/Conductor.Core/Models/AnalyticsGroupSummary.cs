namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Grouped analytics summary row.
    /// </summary>
    public class AnalyticsGroupSummary
    {
        /// <summary>
        /// Grouping dimension.
        /// </summary>
        public string Dimension { get; set; } = null;

        /// <summary>
        /// Grouping value.
        /// </summary>
        public string Value { get; set; } = null;

        /// <summary>
        /// Display label.
        /// </summary>
        public string Label { get; set; } = null;

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
        /// P95 time to first token.
        /// </summary>
        public int? P95TimeToFirstTokenMs { get; set; }

        /// <summary>
        /// Total token count.
        /// </summary>
        public long TotalTokens { get; set; }

        /// <summary>
        /// Count of successful completions without usable token metrics.
        /// </summary>
        public long UnknownTokenUsageCount { get; set; }

        /// <summary>
        /// Percentage of successful completions with measured time to first token.
        /// </summary>
        public decimal? TimeToFirstTokenCoveragePercent { get; set; }

        /// <summary>
        /// Estimate-only cost.
        /// </summary>
        public decimal? EstimatedCost { get; set; }

        /// <summary>
        /// Most recent request timestamp in this group.
        /// </summary>
        public DateTime? LastSeenUtc { get; set; }
    }
}
