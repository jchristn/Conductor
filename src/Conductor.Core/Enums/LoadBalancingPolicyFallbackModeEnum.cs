namespace Conductor.Core.Enums
{
    /// <summary>
    /// Fallback behavior when a load-balancing policy cannot select an endpoint.
    /// </summary>
    public enum LoadBalancingPolicyFallbackModeEnum
    {
        /// <summary>
        /// Fall back to the virtual model runner's legacy load-balancing mode.
        /// </summary>
        UseLegacyLoadBalancingMode = 0,

        /// <summary>
        /// Do not fall back; fail the request instead.
        /// </summary>
        FailClosed = 1
    }
}
