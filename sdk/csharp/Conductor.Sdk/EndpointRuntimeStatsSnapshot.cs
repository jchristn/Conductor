namespace Conductor.Sdk
{
    using System;

    /// <summary>
    /// Runtime statistics for one virtual model runner endpoint.
    /// </summary>
    public class EndpointRuntimeStatsSnapshot
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Virtual model runner identifier.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Endpoint identifier.
        /// </summary>
        public string EndpointId { get; set; } = null;

        /// <summary>
        /// Endpoint display name.
        /// </summary>
        public string EndpointName { get; set; } = null;

        /// <summary>
        /// In-flight request count.
        /// </summary>
        public int InFlight { get; set; } = 0;

        /// <summary>
        /// Pending request count.
        /// </summary>
        public int Pending { get; set; } = 0;

        /// <summary>
        /// Completed request count.
        /// </summary>
        public long CompletedCount { get; set; } = 0;

        /// <summary>
        /// Success-rate EWMA.
        /// </summary>
        public double? SuccessEwma { get; set; } = null;

        /// <summary>
        /// Error-rate EWMA.
        /// </summary>
        public double? ErrorEwma { get; set; } = null;

        /// <summary>
        /// Latency EWMA in milliseconds.
        /// </summary>
        public double? LatencyEwmaMs { get; set; } = null;

        /// <summary>
        /// Time-to-first-token EWMA in milliseconds.
        /// </summary>
        public double? TimeToFirstTokenEwmaMs { get; set; } = null;

        /// <summary>
        /// Last upstream status code.
        /// </summary>
        public int? LastStatusCode { get; set; } = null;

        /// <summary>
        /// Last stable upstream error code.
        /// </summary>
        public string LastErrorCode { get; set; } = null;

        /// <summary>
        /// Consecutive failure count.
        /// </summary>
        public int ConsecutiveFailures { get; set; } = 0;

        /// <summary>
        /// Whether transient backoff is active.
        /// </summary>
        public bool BackoffActive { get; set; } = false;

        /// <summary>
        /// Stable transient backoff reason code.
        /// </summary>
        public string BackoffReason { get; set; } = null;

        /// <summary>
        /// UTC timestamp when transient backoff ends.
        /// </summary>
        public DateTime? BackoffUntilUtc { get; set; } = null;
    }
}
