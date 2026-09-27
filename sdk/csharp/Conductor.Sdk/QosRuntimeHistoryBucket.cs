namespace Conductor.Sdk
{
    using System;

    /// <summary>
    /// QoS admission activity of one traffic class during one time bucket.
    /// Not thread-safe; do not mutate an instance from multiple threads.
    /// </summary>
    public class QosRuntimeHistoryBucket
    {
        /// <summary>
        /// UTC start of the bucket.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Traffic class name. Nullable.
        /// </summary>
        public string ClassName { get; set; } = null;

        /// <summary>
        /// Requests admitted during the bucket. Default 0.
        /// </summary>
        public long Admitted { get; set; } = 0;

        /// <summary>
        /// Requests rejected on arrival during the bucket. Default 0.
        /// </summary>
        public long Rejected { get; set; } = 0;

        /// <summary>
        /// Requests that exceeded the queue wait deadline during the bucket. Default 0.
        /// </summary>
        public long TimedOut { get; set; } = 0;

        /// <summary>
        /// Requests whose client disconnected while queued during the bucket. Default 0.
        /// </summary>
        public long Aborted { get; set; } = 0;

        /// <summary>
        /// Admitted requests that timed out waiting for an endpoint slot during the bucket. Default 0.
        /// </summary>
        public long EndpointSlotTimeouts { get; set; } = 0;

        /// <summary>
        /// Average queue wait of requests admitted during the bucket, in milliseconds. Default 0.
        /// </summary>
        public double AverageWaitMs { get; set; } = 0;

        /// <summary>
        /// Longest queue wait of a request admitted during the bucket, in milliseconds. Default 0.
        /// </summary>
        public double MaxWaitMs { get; set; } = 0;

        /// <summary>
        /// Highest number of requests of the class waiting at once during the bucket. Default 0.
        /// </summary>
        public int PeakWaiting { get; set; } = 0;
    }
}
