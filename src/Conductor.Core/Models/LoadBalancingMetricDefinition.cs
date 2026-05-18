namespace Conductor.Core.Models
{
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Describes a supported metric for load-balancing policies.
    /// </summary>
    public class LoadBalancingMetricDefinition
    {
        public string Id { get; set; } = null;
        public string Name { get; set; } = null;
        public string Description { get; set; } = null;
        public string Source { get; set; } = null;
        public LoadBalancingMetricValueTypeEnum ValueType { get; set; } = LoadBalancingMetricValueTypeEnum.Number;
        public bool SupportsFiltering { get; set; } = true;
        public bool SupportsRanking { get; set; } = true;
        public string RecommendedDirection { get; set; } = null;
        public List<LoadBalancingPolicyOperatorEnum> SupportedOperators { get; set; } = new List<LoadBalancingPolicyOperatorEnum>();
    }
}
