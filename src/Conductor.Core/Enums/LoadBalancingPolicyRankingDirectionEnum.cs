namespace Conductor.Core.Enums
{
    /// <summary>
    /// Ranking direction for policy scoring.
    /// </summary>
    public enum LoadBalancingPolicyRankingDirectionEnum
    {
        /// <summary>
        /// Lower values are better.
        /// </summary>
        Ascending = 0,

        /// <summary>
        /// Higher values are better.
        /// </summary>
        Descending = 1
    }
}
