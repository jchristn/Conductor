namespace Conductor.Core.Models
{
    using System;
    using Conductor.Core.Enums;

    /// <summary>
    /// URL context for parsing and routing requests.
    /// </summary>
    public class UrlContext
    {
        /// <summary>
        /// Full URL path.
        /// </summary>
        public string FullPath { get; set; } = null;

        /// <summary>
        /// Base path of the virtual model runner.
        /// </summary>
        public string BasePath { get; set; } = null;

        /// <summary>
        /// Relative path after the base path.
        /// </summary>
        public string RelativePath { get; set; } = null;

        /// <summary>
        /// Virtual model runner identifier extracted from the path.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// API type determined from the URL.
        /// </summary>
        public ApiTypeEnum ApiType { get; set; } = ApiTypeEnum.Ollama;

        /// <summary>
        /// Request type determined from the URL and HTTP method.
        /// </summary>
        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;

        /// <summary>
        /// Boolean indicating if this is a valid virtual model runner request.
        /// </summary>
        public bool IsValidVmrRequest { get; set; } = false;

        /// <summary>
        /// Check if this is an embeddings request.
        /// </summary>
        public bool IsEmbeddingsRequest =>
            RequestType == RequestTypeEnum.OpenAIEmbeddings ||
            RequestType == RequestTypeEnum.OllamaEmbeddings;

        /// <summary>
        /// Check if this is a completions request.
        /// </summary>
        public bool IsCompletionsRequest =>
            RequestType == RequestTypeEnum.OpenAIChatCompletions ||
            RequestType == RequestTypeEnum.OpenAICompletions ||
            RequestType == RequestTypeEnum.OllamaGenerate ||
            RequestType == RequestTypeEnum.OllamaChat;

        /// <summary>
        /// Check if this is a model management request.
        /// </summary>
        public bool IsModelManagementRequest =>
            RequestType == RequestTypeEnum.OpenAIListModels ||
            RequestType == RequestTypeEnum.OllamaListTags ||
            RequestType == RequestTypeEnum.OllamaPullModel ||
            RequestType == RequestTypeEnum.OllamaDeleteModel ||
            RequestType == RequestTypeEnum.OllamaListRunningModels ||
            RequestType == RequestTypeEnum.OllamaShowModelInfo;

        /// <summary>
        /// Instantiate the URL context.
        /// </summary>
        public UrlContext()
        {
        }

        /// <summary>
        /// Parse a URL path and HTTP method into a URL context.
        /// </summary>
        /// <param name="path">URL path.</param>
        /// <param name="httpMethod">HTTP method.</param>
        /// <returns>URL context.</returns>
        public static UrlContext Parse(string path, string httpMethod)
        {
            UrlContext ctx = new UrlContext
            {
                FullPath = path
            };

            if (String.IsNullOrEmpty(path)) return ctx;

            // Normalize path
            path = path.ToLowerInvariant();
            if (!path.StartsWith("/")) path = "/" + path;

            // Check for virtual model runner API pattern: /v1.0/api/{vmr_id}/...
            if (path.StartsWith("/v1.0/api/"))
            {
                string remaining = path.Substring(10); // Remove "/v1.0/api/"
                int nextSlash = remaining.IndexOf('/');

                if (nextSlash > 0)
                {
                    ctx.VirtualModelRunnerId = remaining.Substring(0, nextSlash);
                    ctx.BasePath = "/v1.0/api/" + ctx.VirtualModelRunnerId + "/";
                    ctx.RelativePath = remaining.Substring(nextSlash);
                    ctx.IsValidVmrRequest = true;

                    // Determine API type and request type from relative path
                    DetermineRequestType(ctx, ctx.RelativePath, httpMethod);
                }
                else if (remaining.Length > 0)
                {
                    // Path like /v1.0/api/vmr_xxx without trailing slash
                    ctx.VirtualModelRunnerId = remaining.TrimEnd('/');
                    ctx.BasePath = "/v1.0/api/" + ctx.VirtualModelRunnerId + "/";
                    ctx.RelativePath = "/";
                    ctx.IsValidVmrRequest = true;
                }
            }

            return ctx;
        }

        private static void DetermineRequestType(UrlContext ctx, string relativePath, string httpMethod)
        {
            if (String.IsNullOrEmpty(relativePath)) return;

            httpMethod = (httpMethod ?? "GET").ToUpperInvariant();
            relativePath = relativePath.ToLowerInvariant();

            // OpenAI API patterns
            if (relativePath.StartsWith("/v1/"))
            {
                ctx.ApiType = ApiTypeEnum.OpenAI;

                if (relativePath.StartsWith("/v1/chat/completions"))
                {
                    ctx.RequestType = RequestTypeEnum.OpenAIChatCompletions;
                }
                else if (relativePath.StartsWith("/v1/completions"))
                {
                    ctx.RequestType = RequestTypeEnum.OpenAICompletions;
                }
                else if (relativePath.StartsWith("/v1/models"))
                {
                    ctx.RequestType = RequestTypeEnum.OpenAIListModels;
                }
                else if (relativePath.StartsWith("/v1/embeddings"))
                {
                    ctx.RequestType = RequestTypeEnum.OpenAIEmbeddings;
                }
            }
            // Ollama API patterns
            else if (relativePath.StartsWith("/api/"))
            {
                ctx.ApiType = ApiTypeEnum.Ollama;

                if (relativePath.StartsWith("/api/generate"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaGenerate;
                }
                else if (relativePath.StartsWith("/api/chat"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaChat;
                }
                else if (relativePath.StartsWith("/api/tags"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaListTags;
                }
                else if (relativePath.StartsWith("/api/embeddings") || relativePath.StartsWith("/api/embed"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaEmbeddings;
                }
                else if (relativePath.StartsWith("/api/pull"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaPullModel;
                }
                else if (relativePath.StartsWith("/api/delete"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaDeleteModel;
                }
                else if (relativePath.StartsWith("/api/ps"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaListRunningModels;
                }
                else if (relativePath.StartsWith("/api/show"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaShowModelInfo;
                }
            }
        }

        /// <summary>
        /// Build the target URL for a backend endpoint.
        /// </summary>
        /// <param name="baseUrl">Base URL of the backend endpoint.</param>
        /// <returns>Full target URL.</returns>
        public string BuildTargetUrl(string baseUrl)
        {
            if (String.IsNullOrEmpty(baseUrl)) return null;
            if (String.IsNullOrEmpty(RelativePath)) return baseUrl;

            baseUrl = baseUrl.TrimEnd('/');
            string path = RelativePath;
            if (!path.StartsWith("/")) path = "/" + path;

            return baseUrl + path;
        }
    }
}
