namespace Conductor.Core.Enums
{
    /// <summary>
    /// A suggested scheduling tier for a QoS traffic class, used as a hint when a profile adopts a
    /// class from the tenant catalog.
    /// </summary>
    public enum QosClassTierEnum
    {
        /// <summary>
        /// Live or streaming work (voice, token streaming); ultra-low latency, strict priority.
        /// </summary>
        Realtime = 0,

        /// <summary>
        /// A person actively waiting on a response; latency-critical, strict priority.
        /// </summary>
        Interactive = 1,

        /// <summary>
        /// An autonomous agent in a live loop; latency-sensitive, top weighted-fair tier.
        /// </summary>
        AgentInteractive = 2,

        /// <summary>
        /// Bulk work with a soft deadline; mid weighted-fair with aging.
        /// </summary>
        BatchTimebound = 3,

        /// <summary>
        /// Best-effort bulk work; lowest weighted-fair tier.
        /// </summary>
        BatchBackground = 4,

        /// <summary>
        /// Fallback tier for unclassified traffic.
        /// </summary>
        Default = 5
    }
}
