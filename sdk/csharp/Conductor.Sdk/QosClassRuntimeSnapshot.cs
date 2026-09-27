namespace Conductor.Sdk
{
    using System;

    /// <summary>
    /// Live QoS admission statistics for one traffic class of one virtual model runner, accumulated since the
    /// class was first seen after server start. Statistics are held in server memory and reset when the server
    /// restarts. Not thread-safe; do not mutate an instance from multiple threads.
    /// </summary>
    public class QosClassRuntimeSnapshot
    {
        /// <summary>
        /// Traffic class name. Nullable.
        /// </summary>
        public string ClassName { get; set; } = null;

        /// <summary>
        /// Requests of this class currently waiting in the QoS queue. Default 0.
        /// </summary>
        public int Waiting { get; set; } = 0;

        /// <summary>
        /// Requests admitted. Default 0.
        /// </summary>
        public long Admitted { get; set; } = 0;

        /// <summary>
        /// Requests rejected on arrival because the queue was full. Default 0.
        /// </summary>
        public long Rejected { get; set; } = 0;

        /// <summary>
        /// Requests rejected because they exceeded the queue wait deadline. Default 0.
        /// </summary>
        public long TimedOut { get; set; } = 0;

        /// <summary>
        /// Requests whose client disconnected while queued. Default 0.
        /// </summary>
        public long Aborted { get; set; } = 0;

        /// <summary>
        /// Admitted requests that then timed out waiting for a free endpoint slot. Default 0.
        /// </summary>
        public long EndpointSlotTimeouts { get; set; } = 0;

        /// <summary>
        /// Average queue wait of admitted requests, in milliseconds. Default 0.
        /// </summary>
        public double AverageWaitMs { get; set; } = 0;

        /// <summary>
        /// 95th percentile queue wait over the most recent admitted requests, in milliseconds. Default 0.
        /// </summary>
        public double P95WaitMs { get; set; } = 0;

        /// <summary>
        /// Longest queue wait of an admitted request, in milliseconds. Default 0.
        /// </summary>
        public double MaxWaitMs { get; set; } = 0;

        /// <summary>
        /// UTC timestamp of the most recent admission, or null when none.
        /// </summary>
        public DateTime? LastAdmittedUtc { get; set; } = null;

        /// <summary>
        /// UTC timestamp of the most recent rejection, timeout, or abort, or null when none.
        /// </summary>
        public DateTime? LastRejectedUtc { get; set; } = null;
    }
}
