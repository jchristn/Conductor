namespace Conductor.Sdk
{
    /// <summary>
    /// Virtual model runner endpoint selection mode.
    /// </summary>
    public enum LoadBalancingMode
    {
        /// <summary>
        /// Select endpoints using weighted round-robin order.
        /// </summary>
        RoundRobin = 0,

        /// <summary>
        /// Select endpoints using configured weights and random choice.
        /// </summary>
        Random = 1,

        /// <summary>
        /// Select the first eligible configured endpoint.
        /// </summary>
        FirstAvailable = 2,

        /// <summary>
        /// Select the eligible endpoint that has gone longest without a route assignment.
        /// </summary>
        LeastRecentlyUsed = 3,

        /// <summary>
        /// Select from sampled eligible endpoints using runtime health and latency scoring.
        /// </summary>
        Adaptive = 4
    }
}
