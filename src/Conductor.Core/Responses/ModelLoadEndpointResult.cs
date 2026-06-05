namespace Conductor.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Per-endpoint result for a model load or verification attempt.
    /// </summary>
    public class ModelLoadEndpointResult
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
        /// Endpoint API type.
        /// </summary>
        public ApiTypeEnum ApiType { get; set; } = ApiTypeEnum.Ollama;

        /// <summary>
        /// Endpoint base URL.
        /// </summary>
        public string BaseUrl { get; set; } = null;

        /// <summary>
        /// Whether this endpoint attempt succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Stable endpoint outcome code.
        /// </summary>
        public ModelLoadOutcomeEnum OutcomeCode { get; set; } = ModelLoadOutcomeEnum.Failed;

        /// <summary>
        /// Upstream provider HTTP status code, when an upstream request was sent.
        /// </summary>
        public int? ProviderStatusCode { get; set; } = null;

        /// <summary>
        /// Provider mechanism used for the attempt.
        /// </summary>
        public string Mechanism { get; set; } = null;

        /// <summary>
        /// Provider request path used for the attempt.
        /// </summary>
        public string RequestPath { get; set; } = null;

        /// <summary>
        /// Endpoint attempt duration in milliseconds.
        /// </summary>
        public int DurationMs { get; set; } = 0;

        /// <summary>
        /// UTC timestamp when this endpoint attempt started.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when this endpoint attempt completed.
        /// </summary>
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether provider-specific verification confirmed the loaded or available model.
        /// </summary>
        public bool VerifiedLoaded { get; set; } = false;

        /// <summary>
        /// Request fields ignored because the provider does not support them.
        /// </summary>
        public List<string> IgnoredFields { get; set; } = new List<string>();

        /// <summary>
        /// Error message, if the endpoint attempt failed.
        /// </summary>
        public string ErrorMessage { get; set; } = null;
    }
}
