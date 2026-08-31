namespace Conductor.Core.Enums
{
    /// <summary>
    /// The comparison a QoS classifier rule applies between the extracted source value and its match value.
    /// </summary>
    public enum QosClassifierOperatorEnum
    {
        /// <summary>
        /// Case-insensitive string equality.
        /// </summary>
        Equals = 0,

        /// <summary>
        /// Case-insensitive substring containment.
        /// </summary>
        Contains = 1,

        /// <summary>
        /// Regular-expression match.
        /// </summary>
        Regex = 2,

        /// <summary>
        /// The source value is present and non-empty (match value ignored).
        /// </summary>
        Exists = 3,

        /// <summary>
        /// Numeric greater-than comparison (for numeric body attributes).
        /// </summary>
        GreaterThan = 4,

        /// <summary>
        /// Numeric less-than comparison (for numeric body attributes).
        /// </summary>
        LessThan = 5
    }
}
