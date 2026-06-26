namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Runtime statistics for one endpoint as used by adaptive load balancing.
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
        /// Current in-flight request count recorded by the runtime stats service.
        /// </summary>
        public int InFlight { get; set; } = 0;

        /// <summary>
        /// Requests admitted but not yet completed.
        /// </summary>
        public int Pending { get; set; } = 0;

        /// <summary>
        /// Completed request count.
        /// </summary>
        public long CompletedCount { get; set; } = 0;

        /// <summary>
        /// Success EWMA from 0 to 1.
        /// </summary>
        public double? SuccessEwma { get; set; } = null;

        /// <summary>
        /// Error EWMA from 0 to 1.
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
        /// Most recent upstream HTTP status code.
        /// </summary>
        public int? LastStatusCode { get; set; } = null;

        /// <summary>
        /// Most recent runtime error code.
        /// </summary>
        public string LastErrorCode { get; set; } = null;

        /// <summary>
        /// Consecutive runtime failures.
        /// </summary>
        public int ConsecutiveFailures { get; set; } = 0;

        /// <summary>
        /// Whether transient backoff is active.
        /// </summary>
        public bool BackoffActive { get; set; } = false;

        /// <summary>
        /// Transient backoff reason.
        /// </summary>
        public string BackoffReason { get; set; } = null;

        /// <summary>
        /// UTC time when transient backoff expires.
        /// </summary>
        public DateTime? BackoffUntilUtc { get; set; } = null;

        /// <summary>
        /// Last endpoint selection UTC.
        /// </summary>
        public DateTime? LastSelectedUtc { get; set; } = null;

        /// <summary>
        /// Last request admission UTC.
        /// </summary>
        public DateTime? LastAdmittedUtc { get; set; } = null;

        /// <summary>
        /// Monotonic selection sequence.
        /// </summary>
        public long SelectionSequence { get; set; } = 0;

        /// <summary>
        /// Last update UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }
}
