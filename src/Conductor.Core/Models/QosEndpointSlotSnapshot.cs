namespace Conductor.Core.Models
{
    /// <summary>
    /// Live concurrency usage of one model runner endpoint referenced by a virtual model runner.
    /// </summary>
    public class QosEndpointSlotSnapshot
    {
        /// <summary>
        /// Endpoint identifier.
        /// </summary>
        public string EndpointId { get; set; } = null;

        /// <summary>
        /// Endpoint display name.
        /// </summary>
        public string EndpointName { get; set; } = null;

        /// <summary>
        /// Requests currently in flight to the endpoint, across every virtual model runner that uses it.
        /// </summary>
        public int InFlight { get; set; } = 0;

        /// <summary>
        /// Configured maximum parallel requests. Zero means unlimited.
        /// </summary>
        public int MaxParallelRequests { get; set; } = 0;

        /// <summary>
        /// True when the endpoint's last health check passed, or it has no recorded health state yet.
        /// </summary>
        public bool IsHealthy { get; set; } = false;

        /// <summary>
        /// True when the endpoint is active.
        /// </summary>
        public bool Active { get; set; } = false;
    }
}
