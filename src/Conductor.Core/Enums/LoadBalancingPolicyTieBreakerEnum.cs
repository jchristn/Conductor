namespace Conductor.Core.Enums
{
    /// <summary>
    /// Tie-breaker strategies for policy-selected endpoints with equal scores.
    /// </summary>
    public enum LoadBalancingPolicyTieBreakerEnum
    {
        /// <summary>
        /// Use round-robin selection.
        /// </summary>
        RoundRobin = 0,

        /// <summary>
        /// Use random selection.
        /// </summary>
        Random = 1,

        /// <summary>
        /// Select the first available endpoint.
        /// </summary>
        FirstAvailable = 2
    }
}
