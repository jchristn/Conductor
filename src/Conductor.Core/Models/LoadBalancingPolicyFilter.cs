namespace Conductor.Core.Models
{
    using Conductor.Core.Enums;

    /// <summary>
    /// Single filter clause in a load-balancing policy.
    /// </summary>
    public class LoadBalancingPolicyFilter
    {
        /// <summary>
        /// Metric identifier to evaluate.
        /// </summary>
        public string Metric { get; set; } = null;

        /// <summary>
        /// Comparison operator applied to the metric.
        /// </summary>
        public LoadBalancingPolicyOperatorEnum Operator { get; set; } = LoadBalancingPolicyOperatorEnum.Equal;

        /// <summary>
        /// Value type expected for the filter literal.
        /// </summary>
        public LoadBalancingMetricValueTypeEnum ValueType { get; set; } = LoadBalancingMetricValueTypeEnum.Number;

        /// <summary>
        /// Filter literal encoded as text.
        /// </summary>
        public string Value { get; set; } = null;
    }
}
