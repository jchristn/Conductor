namespace Conductor.Core.Responses
{
    /// <summary>
    /// Response returned by Ollama model pull or delete operations.
    /// </summary>
    public class OllamaModelOperationResponse
    {
        /// <summary>
        /// Whether the upstream Ollama operation succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Model runner endpoint identifier.
        /// </summary>
        public string EndpointId { get; set; } = null;

        /// <summary>
        /// Model runner endpoint name.
        /// </summary>
        public string EndpointName { get; set; } = null;

        /// <summary>
        /// Endpoint base URL used for the upstream call.
        /// </summary>
        public string BaseUrl { get; set; } = null;

        /// <summary>
        /// Operation name, such as Pull or Delete.
        /// </summary>
        public string Operation { get; set; } = null;

        /// <summary>
        /// Ollama model name or tag.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Provider HTTP status code, when an upstream response was received.
        /// </summary>
        public int? ProviderStatusCode { get; set; } = null;

        /// <summary>
        /// Human-readable summary message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Error message when the operation failed.
        /// </summary>
        public string ErrorMessage { get; set; } = null;

        /// <summary>
        /// Raw provider response body.
        /// </summary>
        public string ProviderBody { get; set; } = null;
    }
}
