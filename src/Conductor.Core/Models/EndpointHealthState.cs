namespace Conductor.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Runtime health state for a model runner endpoint (not persisted to database).
    /// </summary>
    public class EndpointHealthState
    {
        /// <summary>
        /// Endpoint identifier.
        /// </summary>
        public string EndpointId { get; set; }

        /// <summary>
        /// Endpoint name for display purposes.
        /// </summary>
        public string EndpointName { get; set; }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// Whether the endpoint is currently healthy.
        /// Endpoints start as unhealthy until proven healthy.
        /// </summary>
        public bool IsHealthy { get; set; } = false;

        /// <summary>
        /// UTC timestamp of the last health check.
        /// </summary>
        public DateTime? LastCheckUtc { get; set; }

        /// <summary>
        /// UTC timestamp when the endpoint last became healthy.
        /// </summary>
        public DateTime? LastHealthyUtc { get; set; }

        /// <summary>
        /// UTC timestamp when the endpoint last became unhealthy.
        /// </summary>
        public DateTime? LastUnhealthyUtc { get; set; }

        /// <summary>
        /// UTC timestamp when monitoring started for this endpoint.
        /// </summary>
        public DateTime FirstCheckUtc { get; set; }

        /// <summary>
        /// UTC timestamp of the last state transition.
        /// </summary>
        public DateTime? LastStateChangeUtc { get; set; }

        /// <summary>
        /// Total time in milliseconds the endpoint has been healthy.
        /// </summary>
        public long TotalUptimeMs { get; set; } = 0;

        /// <summary>
        /// Total time in milliseconds the endpoint has been unhealthy.
        /// </summary>
        public long TotalDowntimeMs { get; set; } = 0;

        /// <summary>
        /// Number of consecutive successful health checks.
        /// </summary>
        public int ConsecutiveSuccesses { get; set; } = 0;

        /// <summary>
        /// Number of consecutive failed health checks.
        /// </summary>
        public int ConsecutiveFailures { get; set; } = 0;

        /// <summary>
        /// Number of requests currently in flight to this endpoint.
        /// </summary>
        public int InFlightRequests { get; set; } = 0;

        /// <summary>
        /// Last error message from a failed health check.
        /// </summary>
        public string LastError { get; set; }

        /// <summary>
        /// Lock object for thread-safe operations.
        /// </summary>
        [JsonIgnore]
        public readonly object Lock = new object();

        /// <summary>
        /// Create a copy of this state for external use.
        /// </summary>
        /// <returns>A new instance with copied values.</returns>
        public EndpointHealthState Copy()
        {
            return new EndpointHealthState
            {
                EndpointId = this.EndpointId,
                EndpointName = this.EndpointName,
                TenantId = this.TenantId,
                IsHealthy = this.IsHealthy,
                LastCheckUtc = this.LastCheckUtc,
                LastHealthyUtc = this.LastHealthyUtc,
                LastUnhealthyUtc = this.LastUnhealthyUtc,
                FirstCheckUtc = this.FirstCheckUtc,
                LastStateChangeUtc = this.LastStateChangeUtc,
                TotalUptimeMs = this.TotalUptimeMs,
                TotalDowntimeMs = this.TotalDowntimeMs,
                ConsecutiveSuccesses = this.ConsecutiveSuccesses,
                ConsecutiveFailures = this.ConsecutiveFailures,
                InFlightRequests = this.InFlightRequests,
                LastError = this.LastError
            };
        }
    }
}
