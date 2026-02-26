namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Record of an individual health check result.
    /// </summary>
    public class HealthCheckRecord
    {
        /// <summary>
        /// UTC timestamp when the health check was performed.
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Whether the health check was successful.
        /// </summary>
        public bool Success { get; set; }
    }
}
