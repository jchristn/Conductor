namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Conductor.Core.Enums;

    /// <summary>
    /// Request context for proxied API requests.
    /// </summary>
    public class RequestContext
    {
        /// <summary>
        /// Unique request identifier.
        /// </summary>
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp when the request was received.
        /// </summary>
        public DateTime ReceivedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tenant identifier from authentication.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// User identifier from authentication.
        /// </summary>
        public string UserId { get; set; } = null;

        /// <summary>
        /// Virtual model runner handling this request.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Selected backend model runner endpoint.
        /// </summary>
        public string SelectedEndpointId { get; set; } = null;

        /// <summary>
        /// Request type.
        /// </summary>
        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;

        /// <summary>
        /// API type (inferred from virtual model runner or request).
        /// </summary>
        public ApiTypeEnum ApiType { get; set; } = ApiTypeEnum.Ollama;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string HttpMethod { get; set; } = "GET";

        /// <summary>
        /// Original request URL.
        /// </summary>
        public string OriginalUrl { get; set; } = null;

        /// <summary>
        /// Request path (without query string).
        /// </summary>
        public string Path { get; set; } = null;

        /// <summary>
        /// Query string.
        /// </summary>
        public string QueryString { get; set; } = null;

        /// <summary>
        /// Request headers.
        /// </summary>
        public Dictionary<string, string> Headers
        {
            get => _Headers;
            set => _Headers = (value != null ? value : new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Content type of the request.
        /// </summary>
        public string ContentType { get; set; } = null;

        /// <summary>
        /// Content length of the request.
        /// </summary>
        public long ContentLength { get; set; } = 0;

        /// <summary>
        /// Client IP address.
        /// </summary>
        public string ClientIpAddress { get; set; } = null;

        /// <summary>
        /// Client identifier for sticky sessions.
        /// </summary>
        public string ClientIdentifier { get; set; } = null;

        /// <summary>
        /// Model name from the request (if available).
        /// </summary>
        public string ModelName { get; set; } = null;

        /// <summary>
        /// Boolean indicating if this is an embeddings request.
        /// </summary>
        [JsonIgnore]
        public bool IsEmbeddingsRequest
        {
            get
            {
                return RequestType == RequestTypeEnum.OllamaEmbeddings
                    || RequestType == RequestTypeEnum.OpenAIEmbeddings;
            }
        }

        /// <summary>
        /// Boolean indicating if this is a completions request.
        /// </summary>
        [JsonIgnore]
        public bool IsCompletionsRequest
        {
            get
            {
                return RequestType == RequestTypeEnum.OllamaGenerate
                    || RequestType == RequestTypeEnum.OllamaChat
                    || RequestType == RequestTypeEnum.OpenAICompletions
                    || RequestType == RequestTypeEnum.OpenAIChatCompletions;
            }
        }

        /// <summary>
        /// Boolean indicating if this is a model management request.
        /// </summary>
        [JsonIgnore]
        public bool IsModelManagementRequest
        {
            get
            {
                return RequestType == RequestTypeEnum.OllamaPullModel
                    || RequestType == RequestTypeEnum.OllamaDeleteModel
                    || RequestType == RequestTypeEnum.OllamaListTags
                    || RequestType == RequestTypeEnum.OllamaListRunningModels
                    || RequestType == RequestTypeEnum.OllamaShowModelInfo
                    || RequestType == RequestTypeEnum.OpenAIListModels;
            }
        }

        /// <summary>
        /// Boolean indicating if the request is streaming.
        /// </summary>
        public bool IsStreaming { get; set; } = false;

        /// <summary>
        /// Duration of the request in milliseconds.
        /// </summary>
        public long DurationMs { get; set; } = 0;

        /// <summary>
        /// Response status code.
        /// </summary>
        public int ResponseStatusCode { get; set; } = 0;

        /// <summary>
        /// Error message if request failed.
        /// </summary>
        public string ErrorMessage { get; set; } = null;

        private Dictionary<string, string> _Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Instantiate the request context.
        /// </summary>
        public RequestContext()
        {
        }

        /// <summary>
        /// Mark the request as complete.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="errorMessage">Error message if failed.</param>
        public void Complete(int statusCode, string errorMessage = null)
        {
            ResponseStatusCode = statusCode;
            ErrorMessage = errorMessage;
            DurationMs = (long)(DateTime.UtcNow - ReceivedUtc).TotalMilliseconds;
        }
    }
}
