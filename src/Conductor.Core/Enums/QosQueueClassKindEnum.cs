namespace Conductor.Core.Enums
{
    /// <summary>
    /// The role a class row plays within a QoS queue node, disambiguating the polymorphic
    /// per-discipline class definitions stored in a single table.
    /// </summary>
    public enum QosQueueClassKindEnum
    {
        /// <summary>
        /// A priority band (priority discipline).
        /// </summary>
        Band = 0,

        /// <summary>
        /// A weighted-fair flow (weighted-fair discipline).
        /// </summary>
        Flow = 1,

        /// <summary>
        /// A named class (class-based weighted-fair discipline).
        /// </summary>
        Class = 2,

        /// <summary>
        /// A strict-priority class (low-latency discipline).
        /// </summary>
        PriorityClass = 3,

        /// <summary>
        /// A weighted-fair class served after priority classes (low-latency discipline).
        /// </summary>
        FairClass = 4,

        /// <summary>
        /// A weighted sub-queue (weighted round robin discipline).
        /// </summary>
        SubQueue = 5
    }
}
