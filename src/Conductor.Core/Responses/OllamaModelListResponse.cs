namespace Conductor.Core.Responses
{
    using System.Collections.Generic;
    using Conductor.Core.ThirdParty.Ollama.Models;

    /// <summary>
    /// Response returned when listing locally available Ollama models on an endpoint.
    /// </summary>
    public class OllamaModelListResponse
    {
        /// <summary>
        /// Whether the upstream Ollama request succeeded and was parsed.
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
        /// Provider HTTP status code, when an upstream response was received.
        /// </summary>
        public int? ProviderStatusCode { get; set; } = null;

        /// <summary>
        /// Local Ollama models reported by /api/tags.
        /// </summary>
        public List<OllamaLocalModel> Models { get; set; } = new List<OllamaLocalModel>();

        /// <summary>
        /// Human-readable summary message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Error message when the operation failed.
        /// </summary>
        public string ErrorMessage { get; set; } = null;
    }
}
