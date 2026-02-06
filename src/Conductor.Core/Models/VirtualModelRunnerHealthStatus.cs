namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Health status for a virtual model runner (API response model).
    /// </summary>
    public class VirtualModelRunnerHealthStatus
    {
        /// <summary>
        /// Virtual model runner identifier.
        /// </summary>
        public string VirtualModelRunnerId { get; set; }

        /// <summary>
        /// Virtual model runner name.
        /// </summary>
        public string VirtualModelRunnerName { get; set; }

        /// <summary>
        /// UTC timestamp when this status was generated.
        /// </summary>
        public DateTime CheckedUtc { get; set; }

        /// <summary>
        /// Whether all endpoints are healthy.
        /// </summary>
        public bool OverallHealthy { get; set; }

        /// <summary>
        /// Number of healthy endpoints.
        /// </summary>
        public int HealthyEndpointCount { get; set; }

        /// <summary>
        /// Total number of endpoints.
        /// </summary>
        public int TotalEndpointCount { get; set; }

        /// <summary>
        /// Number of active session affinity entries for this VMR.
        /// Only populated when session affinity is enabled. Null when session affinity is disabled.
        /// </summary>
        public int? ActiveSessionCount { get; set; } = null;

        /// <summary>
        /// Health status for each endpoint.
        /// </summary>
        public List<EndpointHealthStatus> Endpoints { get; set; } = new List<EndpointHealthStatus>();
    }
}
