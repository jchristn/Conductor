namespace Conductor.Core.Models
{
    using Conductor.Core.Enums;

    /// <summary>
    /// Ranking rule used to score endpoints within a load-balancing policy.
    /// </summary>
    public class LoadBalancingPolicyRankingRule
    {
        public string Metric { get; set; } = null;
        public LoadBalancingPolicyRankingDirectionEnum Direction { get; set; } = LoadBalancingPolicyRankingDirectionEnum.Ascending;
        public double Weight { get; set; } = 1.0;
    }
}
