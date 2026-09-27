namespace Conductor.Sdk
{
    /// <summary>
    /// Live concurrency usage of one model runner endpoint referenced by a virtual model runner.
    /// Not thread-safe; do not mutate an instance from multiple threads.
    /// </summary>
    public class QosEndpointSlotSnapshot
    {
        /// <summary>
        /// Endpoint identifier. Nullable.
        /// </summary>
        public string EndpointId { get; set; } = null;

        /// <summary>
        /// Endpoint display name. Nullable.
        /// </summary>
        public string EndpointName { get; set; } = null;

        /// <summary>
        /// Requests currently in flight to the endpoint, across every virtual model runner that uses it. Default 0.
        /// </summary>
        public int InFlight { get; set; } = 0;

        /// <summary>
        /// Configured maximum parallel requests. Zero means unlimited. Default 0.
        /// </summary>
        public int MaxParallelRequests { get; set; } = 0;

        /// <summary>
        /// True when the endpoint's last health check passed, or it has no recorded health state yet. Default false.
        /// </summary>
        public bool IsHealthy { get; set; } = false;

        /// <summary>
        /// True when the endpoint is active. Default false.
        /// </summary>
        public bool Active { get; set; } = false;
    }
}
