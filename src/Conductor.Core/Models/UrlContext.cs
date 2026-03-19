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
        /// Query string after the path, including the leading question mark when present.
        /// </summary>
        public string QueryString { get; set; } = null;

        /// <summary>
        /// Virtual model runner identifier extracted from the path.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Requested model extracted from the URL path when the provider embeds the model in the route.
        /// </summary>
        public string RequestedModel { get; set; } = null;

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
            RequestType == RequestTypeEnum.GeminiEmbedContent ||
            RequestType == RequestTypeEnum.OllamaEmbeddings;

        /// <summary>
        /// Check if this is a completions request.
        /// </summary>
        public bool IsCompletionsRequest =>
            RequestType == RequestTypeEnum.OpenAIChatCompletions ||
            RequestType == RequestTypeEnum.OpenAICompletions ||
            RequestType == RequestTypeEnum.GeminiGenerateContent ||
            RequestType == RequestTypeEnum.GeminiStreamGenerateContent ||
            RequestType == RequestTypeEnum.OllamaGenerate ||
            RequestType == RequestTypeEnum.OllamaChat;

        /// <summary>
        /// Check if this is a model management request (pulling or deleting models).
        /// Read-only model listing and info requests are not considered model management.
        /// </summary>
        public bool IsModelManagementRequest =>
            RequestType == RequestTypeEnum.OllamaPullModel ||
            RequestType == RequestTypeEnum.OllamaDeleteModel;

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

            int queryIdx = path.IndexOf('?');
            if (queryIdx >= 0)
            {
                ctx.QueryString = path.Substring(queryIdx);
                path = path.Substring(0, queryIdx);
            }

            // Normalize path shape without altering route casing, which matters for Gemini.
            if (!path.StartsWith("/")) path = "/" + path;
            string normalizedPath = path.ToLowerInvariant();

            // Check for virtual model runner API pattern: /v1.0/api/{vmr_id}/...
            if (normalizedPath.StartsWith("/v1.0/api/"))
            {
                string remaining = path.Substring(10); // Remove "/v1.0/api/"
                string normalizedRemaining = normalizedPath.Substring(10);
                int nextSlash = remaining.IndexOf('/');

                if (nextSlash > 0)
                {
                    ctx.VirtualModelRunnerId = normalizedRemaining.Substring(0, nextSlash);
                    ctx.BasePath = "/v1.0/api/" + ctx.VirtualModelRunnerId + "/";
                    ctx.RelativePath = remaining.Substring(nextSlash);
                    ctx.IsValidVmrRequest = true;

                    // Determine API type and request type from relative path
                    DetermineRequestType(ctx, ctx.RelativePath, httpMethod);
                }
                else if (remaining.Length > 0)
                {
                    // Path like /v1.0/api/vmr_xxx without trailing slash
                    ctx.VirtualModelRunnerId = normalizedRemaining.TrimEnd('/');
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
            string normalizedRelativePath = relativePath.ToLowerInvariant();

            // OpenAI-compatible API patterns
            if (normalizedRelativePath.StartsWith("/v1/"))
            {
                ctx.ApiType = ApiTypeEnum.OpenAI;

                if (normalizedRelativePath.StartsWith("/v1/chat/completions"))
                {
                    ctx.RequestType = RequestTypeEnum.OpenAIChatCompletions;
                }
                else if (normalizedRelativePath.StartsWith("/v1/completions"))
                {
                    ctx.RequestType = RequestTypeEnum.OpenAICompletions;
                }
                else if (normalizedRelativePath.StartsWith("/v1/models"))
                {
                    ctx.RequestType = RequestTypeEnum.OpenAIListModels;
                }
                else if (normalizedRelativePath.StartsWith("/v1/embeddings"))
                {
                    ctx.RequestType = RequestTypeEnum.OpenAIEmbeddings;
                }
            }
            // Gemini API patterns
            else if (normalizedRelativePath.StartsWith("/v1beta/models"))
            {
                ctx.ApiType = ApiTypeEnum.Gemini;

                if (normalizedRelativePath.Equals("/v1beta/models") || normalizedRelativePath.StartsWith("/v1beta/models?"))
                {
                    ctx.RequestType = RequestTypeEnum.GeminiListModels;
                }
                else if (normalizedRelativePath.StartsWith("/v1beta/models/"))
                {
                    ctx.RequestedModel = ExtractGeminiModelSegment(relativePath);

                    if (normalizedRelativePath.Contains(":streamgeneratecontent"))
                    {
                        ctx.RequestType = RequestTypeEnum.GeminiStreamGenerateContent;
                    }
                    else if (normalizedRelativePath.Contains(":generatecontent"))
                    {
                        ctx.RequestType = RequestTypeEnum.GeminiGenerateContent;
                    }
                    else if (normalizedRelativePath.Contains(":embedcontent"))
                    {
                        ctx.RequestType = RequestTypeEnum.GeminiEmbedContent;
                    }
                }
            }
            // Ollama API patterns
            else if (normalizedRelativePath.StartsWith("/api/"))
            {
                ctx.ApiType = ApiTypeEnum.Ollama;

                if (normalizedRelativePath.StartsWith("/api/generate"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaGenerate;
                }
                else if (normalizedRelativePath.StartsWith("/api/chat"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaChat;
                }
                else if (normalizedRelativePath.StartsWith("/api/tags"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaListTags;
                }
                else if (normalizedRelativePath.StartsWith("/api/embeddings") || normalizedRelativePath.StartsWith("/api/embed"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaEmbeddings;
                }
                else if (normalizedRelativePath.StartsWith("/api/pull"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaPullModel;
                }
                else if (normalizedRelativePath.StartsWith("/api/delete"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaDeleteModel;
                }
                else if (normalizedRelativePath.StartsWith("/api/ps"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaListRunningModels;
                }
                else if (normalizedRelativePath.StartsWith("/api/show"))
                {
                    ctx.RequestType = RequestTypeEnum.OllamaShowModelInfo;
                }
            }
        }

        private static string ExtractGeminiModelSegment(string relativePath)
        {
            if (String.IsNullOrEmpty(relativePath)) return null;

            const string prefix = "/v1beta/models/";
            if (!relativePath.StartsWith(prefix)) return null;

            string remaining = relativePath.Substring(prefix.Length);
            int separator = remaining.IndexOfAny(new[] { ':', '/', '?' });
            if (separator >= 0)
            {
                return remaining.Substring(0, separator);
            }

            return remaining;
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

            return baseUrl + path + (QueryString ?? String.Empty);
        }
    }
}
