namespace Conductor.Core.Enums
{
    using System;

    /// <summary>
    /// Load balancing mode enumeration.
    /// </summary>
    public enum LoadBalancingModeEnum
    {
        /// <summary>
        /// Round-robin load balancing.
        /// </summary>
        RoundRobin = 0,

        /// <summary>
        /// Random load balancing.
        /// </summary>
        Random = 1,

        /// <summary>
        /// First available (always use the first healthy endpoint).
        /// </summary>
        FirstAvailable = 2
    }
}
