namespace Conductor.Core.Models
{
    /// <summary>
    /// In-flight analytics measurements collected while proxying a request.
    /// </summary>
    public class RequestAnalyticsCapture
    {
        /// <summary>
        /// Duration from request receipt to routing decision completion.
        /// </summary>
        public int? RoutingDurationMs { get; set; } = null;

        /// <summary>
        /// Endpoint limiter wait duration.
        /// </summary>
        public int? EndpointLimiterWaitMs { get; set; } = null;

        /// <summary>
        /// Duration from request receipt to upstream dispatch.
        /// </summary>
        public int? UpstreamStartOffsetMs { get; set; } = null;

        /// <summary>
        /// Duration from request receipt to upstream response headers.
        /// </summary>
        public int? UpstreamHeadersOffsetMs { get; set; } = null;

        /// <summary>
        /// Number of streaming chunks sent to the client.
        /// </summary>
        public int StreamingChunkCount { get; set; } = 0;

        /// <summary>
        /// Whether the response used a streaming transfer.
        /// </summary>
        public bool IsStreaming { get; set; } = false;

        /// <summary>
        /// Request body bytes.
        /// </summary>
        public long? RequestBytes { get; set; } = null;

        /// <summary>
        /// Response body bytes.
        /// </summary>
        public long? ResponseBytes { get; set; } = null;

        /// <summary>
        /// Error category.
        /// </summary>
        public string ErrorType { get; set; } = null;

        /// <summary>
        /// Redacted error message.
        /// </summary>
        public string ErrorMessage { get; set; } = null;
    }
}
