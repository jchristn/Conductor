namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Health status for a model runner endpoint (API response model).
    /// </summary>
    public class EndpointHealthStatus
    {
        /// <summary>
        /// Endpoint identifier.
        /// </summary>
        public string EndpointId { get; set; }

        /// <summary>
        /// Endpoint name.
        /// </summary>
        public string EndpointName { get; set; }

        /// <summary>
        /// Whether the endpoint is currently healthy.
        /// </summary>
        public bool IsHealthy { get; set; }

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
        /// UTC timestamp when monitoring started.
        /// </summary>
        public DateTime? FirstCheckUtc { get; set; }

        /// <summary>
        /// Total time in milliseconds the endpoint has been healthy.
        /// </summary>
        public long TotalUptimeMs { get; set; }

        /// <summary>
        /// Total time in milliseconds the endpoint has been unhealthy.
        /// </summary>
        public long TotalDowntimeMs { get; set; }

        /// <summary>
        /// Uptime percentage (0-100).
        /// </summary>
        public double UptimePercentage
        {
            get
            {
                long total = TotalUptimeMs + TotalDowntimeMs;
                if (total == 0) return 0;
                return Math.Round((double)TotalUptimeMs / total * 100, 2);
            }
        }

        /// <summary>
        /// Number of consecutive successful health checks.
        /// </summary>
        public int ConsecutiveSuccesses { get; set; }

        /// <summary>
        /// Number of consecutive failed health checks.
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// Number of requests currently in flight.
        /// </summary>
        public int InFlightRequests { get; set; }

        /// <summary>
        /// Maximum parallel requests allowed (0 = unlimited).
        /// </summary>
        public int MaxParallelRequests { get; set; }

        /// <summary>
        /// Weight for load balancing.
        /// </summary>
        public int Weight { get; set; }

        /// <summary>
        /// Last error message from health check.
        /// </summary>
        public string LastError { get; set; }

        /// <summary>
        /// Create an EndpointHealthStatus from an EndpointHealthState.
        /// </summary>
        public static EndpointHealthStatus FromState(EndpointHealthState state, ModelRunnerEndpoint endpoint)
        {
            return new EndpointHealthStatus
            {
                EndpointId = state.EndpointId,
                EndpointName = state.EndpointName ?? endpoint?.Name,
                IsHealthy = state.IsHealthy,
                LastCheckUtc = state.LastCheckUtc,
                LastHealthyUtc = state.LastHealthyUtc,
                LastUnhealthyUtc = state.LastUnhealthyUtc,
                FirstCheckUtc = state.FirstCheckUtc,
                TotalUptimeMs = state.TotalUptimeMs,
                TotalDowntimeMs = state.TotalDowntimeMs,
                ConsecutiveSuccesses = state.ConsecutiveSuccesses,
                ConsecutiveFailures = state.ConsecutiveFailures,
                InFlightRequests = state.InFlightRequests,
                MaxParallelRequests = endpoint?.MaxParallelRequests ?? 0,
                Weight = endpoint?.Weight ?? 1,
                LastError = state.LastError
            };
        }
    }
}
