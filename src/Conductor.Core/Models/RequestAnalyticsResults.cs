namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Per-request analytics details.
    /// </summary>
    public class RequestAnalyticsDetailResult
    {
        /// <summary>
        /// Request history ID.
        /// </summary>
        public string RequestHistoryId { get; set; } = null;

        /// <summary>
        /// Trace ID.
        /// </summary>
        public string TraceId { get; set; } = null;

        /// <summary>
        /// Whether detailed analytics were captured.
        /// </summary>
        public bool AnalyticsCaptured { get; set; } = false;

        /// <summary>
        /// Analytics capture failure code.
        /// </summary>
        public string AnalyticsFailureCode { get; set; } = null;

        /// <summary>
        /// Analytics events.
        /// </summary>
        public List<RequestAnalyticsEvent> Events { get; set; } = new List<RequestAnalyticsEvent>();
    }

    /// <summary>
    /// Aggregate request analytics overview.
    /// </summary>
    public class RequestAnalyticsOverviewResult
    {
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
        /// Total requests represented.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Successful request count.
        /// </summary>
        public long SuccessCount { get; set; }

        /// <summary>
        /// Failed request count.
        /// </summary>
        public long FailureCount { get; set; }

        /// <summary>
        /// Requests with analytics events.
        /// </summary>
        public long AnalyticsCapturedCount { get; set; }

        /// <summary>
        /// Analytics coverage percentage.
        /// </summary>
        public decimal AnalyticsCoveragePercent { get; set; }

        /// <summary>
        /// Requests with a model access permit decision.
        /// </summary>
        public long ModelAccessAllowedCount { get; set; }

        /// <summary>
        /// Requests denied by model access enforcement.
        /// </summary>
        public long ModelAccessDeniedCount { get; set; }

        /// <summary>
        /// Requests that monitor mode would have denied.
        /// </summary>
        public long ModelAccessWouldDenyCount { get; set; }

        /// <summary>
        /// Requests allowed by a model access default decision rather than a matched rule.
        /// </summary>
        public long ModelAccessDefaultAllowedCount { get; set; }

        /// <summary>
        /// Requests denied by a model access default decision rather than a matched rule.
        /// </summary>
        public long ModelAccessDefaultDeniedCount { get; set; }

        /// <summary>
        /// Requests where model access evaluation reported an error.
        /// </summary>
        public long ModelAccessEvaluatorErrorCount { get; set; }

        /// <summary>
        /// Requests denied by an active reservation gate.
        /// </summary>
        public long ReservationDeniedCount { get; set; }

        /// <summary>
        /// Reservation-denial counts keyed by reservation GUID or "Unknown".
        /// </summary>
        public Dictionary<string, long> ReservationDenialCounts { get; set; } = new Dictionary<string, long>();

        /// <summary>
        /// Average response duration.
        /// </summary>
        public decimal? AverageDurationMs { get; set; }

        /// <summary>
        /// P50 response duration.
        /// </summary>
        public int? P50DurationMs { get; set; }

        /// <summary>
        /// P95 response duration.
        /// </summary>
        public int? P95DurationMs { get; set; }

        /// <summary>
        /// P99 response duration.
        /// </summary>
        public int? P99DurationMs { get; set; }

        /// <summary>
        /// Total token count.
        /// </summary>
        public long TotalTokens { get; set; }

        /// <summary>
        /// Average overall token throughput.
        /// </summary>
        public decimal? AverageTokensPerSecond { get; set; }

        /// <summary>
        /// Time buckets.
        /// </summary>
        public List<RequestAnalyticsTimeSeriesBucket> TimeSeries { get; set; } = new List<RequestAnalyticsTimeSeriesBucket>();

        /// <summary>
        /// Stage summaries.
        /// </summary>
        public List<RequestAnalyticsStageSummary> StageBreakdown { get; set; } = new List<RequestAnalyticsStageSummary>();

        /// <summary>
        /// Endpoint summaries.
        /// </summary>
        public List<RequestAnalyticsEndpointSummary> EndpointSummaries { get; set; } = new List<RequestAnalyticsEndpointSummary>();

        /// <summary>
        /// Slowest requests.
        /// </summary>
        public List<RequestAnalyticsSlowRequest> SlowestRequests { get; set; } = new List<RequestAnalyticsSlowRequest>();
    }

    /// <summary>
    /// Time-series bucket for request analytics.
    /// </summary>
    public class RequestAnalyticsTimeSeriesBucket
    {
        /// <summary>
        /// Bucket timestamp.
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Request count.
        /// </summary>
        public long RequestCount { get; set; }

        /// <summary>
        /// Success count.
        /// </summary>
        public long SuccessCount { get; set; }

        /// <summary>
        /// Failure count.
        /// </summary>
        public long FailureCount { get; set; }

        /// <summary>
        /// Average duration in milliseconds.
        /// </summary>
        public decimal? AverageDurationMs { get; set; }

        /// <summary>
        /// P95 duration in milliseconds.
        /// </summary>
        public int? P95DurationMs { get; set; }

        /// <summary>
        /// Total token count.
        /// </summary>
        public long TotalTokens { get; set; }
    }

    /// <summary>
    /// Stage-level aggregate.
    /// </summary>
    public class RequestAnalyticsStageSummary
    {
        /// <summary>
        /// Stage kind.
        /// </summary>
        public string StageKind { get; set; } = null;

        /// <summary>
        /// Stage name.
        /// </summary>
        public string StageName { get; set; } = null;

        /// <summary>
        /// Stage count.
        /// </summary>
        public long Count { get; set; }

        /// <summary>
        /// Total duration.
        /// </summary>
        public long TotalDurationMs { get; set; }

        /// <summary>
        /// Average duration.
        /// </summary>
        public decimal? AverageDurationMs { get; set; }

        /// <summary>
        /// P95 duration.
        /// </summary>
        public int? P95DurationMs { get; set; }

        /// <summary>
        /// Percentage of total stage duration.
        /// </summary>
        public decimal PercentOfTotalDuration { get; set; }
    }

    /// <summary>
    /// Endpoint-level aggregate.
    /// </summary>
    public class RequestAnalyticsEndpointSummary
    {
        /// <summary>
        /// Model endpoint GUID.
        /// </summary>
        public string ModelEndpointGuid { get; set; } = null;

        /// <summary>
        /// Model endpoint name.
        /// </summary>
        public string ModelEndpointName { get; set; } = null;

        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = null;

        /// <summary>
        /// Request count.
        /// </summary>
        public long RequestCount { get; set; }

        /// <summary>
        /// Success count.
        /// </summary>
        public long SuccessCount { get; set; }

        /// <summary>
        /// Failure count.
        /// </summary>
        public long FailureCount { get; set; }

        /// <summary>
        /// Average duration.
        /// </summary>
        public decimal? AverageDurationMs { get; set; }

        /// <summary>
        /// P95 duration.
        /// </summary>
        public int? P95DurationMs { get; set; }

        /// <summary>
        /// Total token count.
        /// </summary>
        public long TotalTokens { get; set; }

        /// <summary>
        /// Average token throughput.
        /// </summary>
        public decimal? AverageTokensPerSecond { get; set; }
    }

    /// <summary>
    /// Slow request row.
    /// </summary>
    public class RequestAnalyticsSlowRequest
    {
        /// <summary>
        /// Request history ID.
        /// </summary>
        public string RequestHistoryId { get; set; } = null;

        /// <summary>
        /// Trace ID.
        /// </summary>
        public string TraceId { get; set; } = null;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Virtual Model Runner name.
        /// </summary>
        public string VirtualModelRunnerName { get; set; } = null;

        /// <summary>
        /// Model endpoint name.
        /// </summary>
        public string ModelEndpointName { get; set; } = null;

        /// <summary>
        /// Effective model.
        /// </summary>
        public string EffectiveModel { get; set; } = null;

        /// <summary>
        /// HTTP status.
        /// </summary>
        public int? HttpStatus { get; set; } = null;

        /// <summary>
        /// Response time.
        /// </summary>
        public int? ResponseTimeMs { get; set; } = null;

        /// <summary>
        /// Time to first token.
        /// </summary>
        public int? FirstTokenTimeMs { get; set; } = null;

        /// <summary>
        /// Dominant stage kind.
        /// </summary>
        public string DominantStageKind { get; set; } = null;

        /// <summary>
        /// Dominant stage duration.
        /// </summary>
        public int? DominantStageDurationMs { get; set; } = null;
    }
}
