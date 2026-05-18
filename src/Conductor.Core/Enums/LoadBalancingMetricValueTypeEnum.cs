namespace Conductor.Core.Enums
{
    /// <summary>
    /// Supported scalar value types for load-balancing policy metrics.
    /// </summary>
    public enum LoadBalancingMetricValueTypeEnum
    {
        /// <summary>
        /// Numeric scalar.
        /// </summary>
        Number = 0,

        /// <summary>
        /// Boolean scalar.
        /// </summary>
        Boolean = 1,

        /// <summary>
        /// String scalar.
        /// </summary>
        String = 2
    }
}
