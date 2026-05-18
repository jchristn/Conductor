namespace Conductor.Core.Enums
{
    /// <summary>
    /// Comparison operators supported by load-balancing policy filters.
    /// </summary>
    public enum LoadBalancingPolicyOperatorEnum
    {
        /// <summary>
        /// Equality.
        /// </summary>
        Equal = 0,

        /// <summary>
        /// Inequality.
        /// </summary>
        NotEqual = 1,

        /// <summary>
        /// Less than.
        /// </summary>
        LessThan = 2,

        /// <summary>
        /// Less than or equal.
        /// </summary>
        LessThanOrEqual = 3,

        /// <summary>
        /// Greater than.
        /// </summary>
        GreaterThan = 4,

        /// <summary>
        /// Greater than or equal.
        /// </summary>
        GreaterThanOrEqual = 5
    }
}
