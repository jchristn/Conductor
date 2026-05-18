namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Response payload from RigMonitor /readyz.
    /// </summary>
    public class RigMonitorReadyStatus
    {
        /// <summary>
        /// Human-readable status.
        /// </summary>
        public string Status { get; set; } = null;

        /// <summary>
        /// Whether RigMonitor reports itself as ready.
        /// </summary>
        public bool Ready { get; set; } = false;

        /// <summary>
        /// Optional readiness message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Timestamp supplied by RigMonitor.
        /// </summary>
        public DateTime? TimestampUtc { get; set; } = null;
    }
}
