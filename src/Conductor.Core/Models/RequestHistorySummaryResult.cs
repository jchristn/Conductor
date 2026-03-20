namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of a request history summary aggregation.
    /// </summary>
    public class RequestHistorySummaryResult
    {
        /// <summary>
        /// List of time-bucketed summary entries.
        /// </summary>
        public List<RequestHistorySummaryBucket> Data
        {
            get => _Data;
            set => _Data = (value != null ? value : new List<RequestHistorySummaryBucket>());
        }

        /// <summary>
        /// Start of the queried time range (UTC).
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End of the queried time range (UTC).
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The interval used for bucketing ("hour" or "day").
        /// </summary>
        public string Interval { get; set; } = "hour";

        /// <summary>
        /// Total successful requests across all buckets.
        /// </summary>
        public long TotalSuccess { get; set; } = 0;

        /// <summary>
        /// Total failed requests across all buckets.
        /// </summary>
        public long TotalFailure { get; set; } = 0;

        /// <summary>
        /// Total requests across all buckets.
        /// </summary>
        public long TotalRequests => TotalSuccess + TotalFailure;

        private List<RequestHistorySummaryBucket> _Data = new List<RequestHistorySummaryBucket>();

        /// <summary>
        /// Instantiate the summary result.
        /// </summary>
        public RequestHistorySummaryResult()
        {
        }
    }
}
