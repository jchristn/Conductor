namespace Conductor.Core.Models
{
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Describes a supported metric for load-balancing policies.
    /// </summary>
    public class LoadBalancingMetricDefinition
    {
        /// <summary>
        /// Stable metric identifier used in policy filters and ranking rules.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Human-readable metric name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Description of the metric and its semantics.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Source category for the metric, such as endpoint health or RigMonitor.
        /// </summary>
        public string Source { get; set; } = null;

        /// <summary>
        /// Metric value type expected by filters and evaluators.
        /// </summary>
        public LoadBalancingMetricValueTypeEnum ValueType { get; set; } = LoadBalancingMetricValueTypeEnum.Number;

        /// <summary>
        /// Whether the metric can be used in policy filters.
        /// </summary>
        public bool SupportsFiltering { get; set; } = true;

        /// <summary>
        /// Whether the metric can be used in policy ranking rules.
        /// </summary>
        public bool SupportsRanking { get; set; } = true;

        /// <summary>
        /// Recommended ranking direction for the metric, if any.
        /// </summary>
        public string RecommendedDirection { get; set; } = null;

        /// <summary>
        /// Comparison operators supported when filtering on the metric.
        /// </summary>
        public List<LoadBalancingPolicyOperatorEnum> SupportedOperators { get; set; } = new List<LoadBalancingPolicyOperatorEnum>();
    }
}
