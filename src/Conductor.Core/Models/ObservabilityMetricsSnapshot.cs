namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// JSON-friendly observability snapshot derived from in-memory metrics.
    /// </summary>
    public class ObservabilityMetricsSnapshot
    {
        /// <summary>
        /// UTC timestamp when the snapshot was generated.
        /// </summary>
        public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Aggregate metrics across the current process.
        /// </summary>
        public ObservabilityAggregateMetrics Overall { get; set; } = new ObservabilityAggregateMetrics();

        /// <summary>
        /// Aggregate metrics grouped by virtual model runner.
        /// </summary>
        public List<ObservabilityScopedMetrics> VirtualModelRunners { get; set; } = new List<ObservabilityScopedMetrics>();
    }

    /// <summary>
    /// Aggregate observability metrics.
    /// </summary>
    public class ObservabilityAggregateMetrics
    {
        /// <summary>
        /// Total observed requests.
        /// </summary>
        public long TotalRequests { get; set; } = 0;

        /// <summary>
        /// Successfully routed requests.
        /// </summary>
        public long RoutedRequests { get; set; } = 0;

        /// <summary>
        /// Denied requests.
        /// </summary>
        public long DeniedRequests { get; set; } = 0;

        /// <summary>
        /// Policy fallback count.
        /// </summary>
        public long PolicyFallbacks { get; set; } = 0;

        /// <summary>
        /// Session-affinity hits.
        /// </summary>
        public long SessionAffinityHits { get; set; } = 0;

        /// <summary>
        /// Session-affinity misses.
        /// </summary>
        public long SessionAffinityMisses { get; set; } = 0;

        /// <summary>
        /// Session-affinity hit rate percentage.
        /// </summary>
        public double SessionAffinityHitRate { get; set; } = 0;

        /// <summary>
        /// Capacity/saturation denial count.
        /// </summary>
        public long SaturationDenials { get; set; } = 0;

        /// <summary>
        /// Telemetry freshness failure count.
        /// </summary>
        public long TelemetryFreshnessFailures { get; set; } = 0;

        /// <summary>
        /// Adaptive endpoint selections.
        /// </summary>
        public long AdaptiveSelections { get; set; } = 0;

        /// <summary>
        /// Runtime backoff selections or denials.
        /// </summary>
        public long RuntimeBackoffSelections { get; set; } = 0;

        /// <summary>
        /// Denial counts by reason code.
        /// </summary>
        public Dictionary<string, long> DenialReasons { get; set; } = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Selection counts grouped by stable strategy name.
        /// </summary>
        public Dictionary<string, long> SelectionStrategies { get; set; } = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Selection counts grouped by endpoint group identifier.
        /// </summary>
        public Dictionary<string, long> EndpointGroupSelections { get; set; } = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Backoff counts grouped by stable reason code.
        /// </summary>
        public Dictionary<string, long> BackoffReasons { get; set; } = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Route-decision latency summary.
        /// </summary>
        public ObservabilityPercentileSummary RouteDecisionDurationMs { get; set; } = new ObservabilityPercentileSummary();

        /// <summary>
        /// End-to-end duration summary.
        /// </summary>
        public ObservabilityPercentileSummary TotalDurationMs { get; set; } = new ObservabilityPercentileSummary();

        /// <summary>
        /// Time-to-first-token summary.
        /// </summary>
        public ObservabilityPercentileSummary FirstTokenTimeMs { get; set; } = new ObservabilityPercentileSummary();

        /// <summary>
        /// Adaptive sampled candidate count summary.
        /// </summary>
        public ObservabilityPercentileSummary AdaptiveSampledCandidates { get; set; } = new ObservabilityPercentileSummary();

        /// <summary>
        /// Selected adaptive score summary.
        /// </summary>
        public ObservabilityPercentileSummary AdaptiveSelectedScore { get; set; } = new ObservabilityPercentileSummary();
    }

    /// <summary>
    /// Metrics scoped to a tenant or virtual model runner.
    /// </summary>
    public class ObservabilityScopedMetrics : ObservabilityAggregateMetrics
    {
        /// <summary>
        /// Tenant identifier for the scope.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Virtual model runner identifier for the scope.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Virtual model runner display name for the scope.
        /// </summary>
        public string VirtualModelRunnerName { get; set; } = null;
    }

    /// <summary>
    /// Percentile-oriented metric summary.
    /// </summary>
    public class ObservabilityPercentileSummary
    {
        /// <summary>
        /// Number of observations contributing to the summary.
        /// </summary>
        public long Count { get; set; } = 0;

        /// <summary>
        /// Minimum observed value.
        /// </summary>
        public double Min { get; set; } = 0;

        /// <summary>
        /// Median approximation.
        /// </summary>
        public double P50 { get; set; } = 0;

        /// <summary>
        /// 95th percentile approximation.
        /// </summary>
        public double P95 { get; set; } = 0;

        /// <summary>
        /// Maximum observed value.
        /// </summary>
        public double Max { get; set; } = 0;
    }
}
