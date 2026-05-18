namespace Conductor.Core.Models
{
    using Conductor.Core.Enums;

    /// <summary>
    /// Single filter clause in a load-balancing policy.
    /// </summary>
    public class LoadBalancingPolicyFilter
    {
        public string Metric { get; set; } = null;
        public LoadBalancingPolicyOperatorEnum Operator { get; set; } = LoadBalancingPolicyOperatorEnum.Equal;
        public LoadBalancingMetricValueTypeEnum ValueType { get; set; } = LoadBalancingMetricValueTypeEnum.Number;
        public string Value { get; set; } = null;
    }
}
