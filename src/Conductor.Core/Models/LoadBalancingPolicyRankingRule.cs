namespace Conductor.Core.Models
{
    using Conductor.Core.Enums;

    /// <summary>
    /// Ranking rule used to score endpoints within a load-balancing policy.
    /// </summary>
    public class LoadBalancingPolicyRankingRule
    {
        /// <summary>
        /// Metric identifier used for scoring.
        /// </summary>
        public string Metric { get; set; } = null;

        /// <summary>
        /// Desired sort direction for the metric.
        /// </summary>
        public LoadBalancingPolicyRankingDirectionEnum Direction { get; set; } = LoadBalancingPolicyRankingDirectionEnum.Ascending;

        /// <summary>
        /// Relative weight applied when combining this rule into a final score.
        /// </summary>
        public double Weight { get; set; } = 1.0;
    }
}
