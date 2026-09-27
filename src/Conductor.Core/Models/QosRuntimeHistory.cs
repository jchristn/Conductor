namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Time-bucketed QoS admission activity for one virtual model runner. History is held in memory, covers at
    /// most the configured retention window, and resets when the server restarts.
    /// </summary>
    public class QosRuntimeHistory
    {
        /// <summary>
        /// Virtual model runner identifier.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// UTC start of the window, aligned to the interval.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC end of the window (exclusive).
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket interval: minute, 5minute, 15minute, or hour.
        /// </summary>
        public string Interval { get; set; } = "minute";

        /// <summary>
        /// Class names that appear in the buckets, ordered by name.
        /// </summary>
        public List<string> Classes { get; set; } = new List<string>();

        /// <summary>
        /// Buckets with activity, one per class per interval, ordered by timestamp then class name. Buckets
        /// without activity are omitted.
        /// </summary>
        public List<QosRuntimeHistoryBucket> Buckets { get; set; } = new List<QosRuntimeHistoryBucket>();
    }
}
