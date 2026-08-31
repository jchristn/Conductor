namespace Conductor.Core.Enums
{
    /// <summary>
    /// Queue scheduling discipline for a QoS queue node, mapped one-to-one onto a QoSKit queue type.
    /// </summary>
    public enum QosDisciplineEnum
    {
        /// <summary>
        /// First-in-first-out.
        /// </summary>
        Fifo = 0,

        /// <summary>
        /// Last-in-first-out.
        /// </summary>
        Lifo = 1,

        /// <summary>
        /// Strict priority across a fixed number of bands.
        /// </summary>
        Priority = 2,

        /// <summary>
        /// Weighted fair queuing across flows.
        /// </summary>
        Wfq = 3,

        /// <summary>
        /// Class-based weighted fair queuing.
        /// </summary>
        Cbwfq = 4,

        /// <summary>
        /// Low-latency queuing (strict-priority classes ahead of weighted-fair classes).
        /// </summary>
        Llq = 5,

        /// <summary>
        /// Weighted round robin (deficit round robin).
        /// </summary>
        Wrr = 6
    }
}
