namespace Conductor.Core.Models
{
    /// <summary>
    /// Represents the availability status of a model runner endpoint for load balancing.
    /// </summary>
    public class EndpointAvailability
    {
        /// <summary>
        /// The model runner endpoint.
        /// </summary>
        public ModelRunnerEndpoint Endpoint { get; set; }

        /// <summary>
        /// Whether the endpoint is currently healthy.
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Whether the endpoint has capacity for additional requests.
        /// </summary>
        public bool HasCapacity { get; set; }

        /// <summary>
        /// Instantiate the endpoint availability.
        /// </summary>
        public EndpointAvailability()
        {
        }

        /// <summary>
        /// Instantiate the endpoint availability with values.
        /// </summary>
        /// <param name="endpoint">The model runner endpoint.</param>
        /// <param name="isHealthy">Whether the endpoint is healthy.</param>
        /// <param name="hasCapacity">Whether the endpoint has capacity.</param>
        public EndpointAvailability(ModelRunnerEndpoint endpoint, bool isHealthy, bool hasCapacity)
        {
            Endpoint = endpoint;
            IsHealthy = isHealthy;
            HasCapacity = hasCapacity;
        }
    }
}
