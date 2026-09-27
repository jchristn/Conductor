namespace Conductor.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Time-bucketed QoS admission activity for one virtual model runner. History is held in server memory, covers
    /// at most the configured retention window (24 hours by default), and resets when the server restarts.
    /// Not thread-safe; do not mutate an instance from multiple threads.
    /// </summary>
    public class QosRuntimeHistory
    {
        /// <summary>
        /// Virtual model runner identifier. Nullable.
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
        /// Bucket interval: minute, 5minute, 15minute, or hour. Default minute.
        /// </summary>
        public string Interval { get; set; } = "minute";

        /// <summary>
        /// Class names that appear in the buckets, ordered by name. Never null; setting null stores an empty list.
        /// </summary>
        public List<string> Classes
        {
            get
            {
                return _Classes;
            }
            set
            {
                _Classes = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Buckets with activity, one per class per interval, ordered by timestamp then class name. Buckets
        /// without activity are omitted. Never null; setting null stores an empty list.
        /// </summary>
        public List<QosRuntimeHistoryBucket> Buckets
        {
            get
            {
                return _Buckets;
            }
            set
            {
                _Buckets = value ?? new List<QosRuntimeHistoryBucket>();
            }
        }

        private List<string> _Classes = new List<string>();
        private List<QosRuntimeHistoryBucket> _Buckets = new List<QosRuntimeHistoryBucket>();
    }
}
