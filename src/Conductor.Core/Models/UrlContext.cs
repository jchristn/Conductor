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
        /// API type of the request. Set from the virtual model runner's configured API type by Classify; never inferred from
        /// the URL. Default: Ollama until classified.
        /// </summary>
        public ApiTypeEnum ApiType { get; set; } = ApiTypeEnum.Ollama;

        /// <summary>
        /// Request type determined by Classify from the virtual model runner type, relative path and HTTP method.
        /// Unknown for paths that are not classified (including before Classify is called).
        /// </summary>
        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;

        /// <summary>
        /// Boolean indicating if this is a valid virtual model runner request.
        /// </summary>
        public bool IsValidVmrRequest { get; set; } = false;

        /// <summary>
        /// Boolean indicating whether Classify has been applied with a virtual model runner type.
        /// </summary>
        public bool IsClassified { get; set; } = false;

        /// <summary>
        /// Boolean indicating whether the relative path is inside the virtual model runner type's path namespace
        /// (/api/ for Ollama, /v1/ for OpenAI, vLLM, Anthropic and TypeSafe, /v1beta/ for Gemini). Requests outside the
        /// namespace are answered with 404 and never forwarded. False until classified.
        /// </summary>
        public bool IsInNamespace { get; set; } = false;

        /// <summary>
        /// Boolean indicating whether the path is inside the namespace but names a route Conductor deliberately does not
        /// support (Anthropic /v1/messages/batches and /v1/files, which are stateful). Such requests are answered with 404.
        /// </summary>
        public bool IsUnsupportedRoute { get; set; } = false;

        /// <summary>
        /// Boolean indicating whether the relative path contains a dot segment ("." or ".."), including percent-encoded
        /// forms such as %2e%2e, or an encoded or literal path separator inside a segment (%2f, %5c, backslash). Such paths
        /// are never classified or forwarded, because normalizing them later could move the request outside the namespace
        /// or onto an unsupported route. Set by Classify.
        /// </summary>
        public bool HasUnsafePathSegments { get; set; } = false;

        /// <summary>
        /// Boolean indicating whether the request must be answered with 404 without forwarding: the path is outside the
        /// namespace, names an unsupported route, or contains unsafe path segments. Only meaningful after Classify.
        /// </summary>
        public bool IsNotFound => IsClassified && (!IsInNamespace || IsUnsupportedRoute || HasUnsafePathSegments);

        /// <summary>
        /// Check if this is an embeddings request.
        /// </summary>
        public bool IsEmbeddingsRequest => IsEmbeddingsRequestType(RequestType);

        /// <summary>
        /// Check if this is a completions request. Includes Anthropic messages.
        /// </summary>
        public bool IsCompletionsRequest => IsCompletionsRequestType(RequestType);

        /// <summary>
        /// Check if this is a decision request (TypeSafe System One).
        /// </summary>
        public bool IsDecisionRequest => IsDecisionRequestType(RequestType);

        /// <summary>
        /// Check if this is a token counting request (Anthropic count_tokens).
        /// </summary>
        public bool IsTokenCountRequest => RequestType == RequestTypeEnum.AnthropicCountTokens;

        /// <summary>
        /// Check if this is a model listing or single-model inspection request that returns model metadata
        /// (OpenAI, Gemini, Ollama tags and running models, Anthropic list and get, TypeSafe list).
        /// </summary>
        public bool IsModelListingRequest => IsModelListingRequestType(RequestType);

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
        /// Determine whether a request type is a completions request.
        /// </summary>
        /// <param name="requestType">Request type.</param>
        /// <returns>True for OpenAI chat and text completions, Gemini generate and stream generate, Ollama generate and
        /// chat, and Anthropic messages.</returns>
        public static bool IsCompletionsRequestType(RequestTypeEnum requestType)
        {
            return requestType == RequestTypeEnum.OpenAIChatCompletions ||
                requestType == RequestTypeEnum.OpenAICompletions ||
                requestType == RequestTypeEnum.GeminiGenerateContent ||
                requestType == RequestTypeEnum.GeminiStreamGenerateContent ||
                requestType == RequestTypeEnum.OllamaGenerate ||
                requestType == RequestTypeEnum.OllamaChat ||
                requestType == RequestTypeEnum.AnthropicMessages;
        }

        /// <summary>
        /// Determine whether a request type is an embeddings request.
        /// </summary>
        /// <param name="requestType">Request type.</param>
        /// <returns>True for OpenAI, Gemini and Ollama embeddings.</returns>
        public static bool IsEmbeddingsRequestType(RequestTypeEnum requestType)
        {
            return requestType == RequestTypeEnum.OpenAIEmbeddings ||
                requestType == RequestTypeEnum.GeminiEmbedContent ||
                requestType == RequestTypeEnum.OllamaEmbeddings;
        }

        /// <summary>
        /// Determine whether a request type is a decision request.
        /// </summary>
        /// <param name="requestType">Request type.</param>
        /// <returns>True for TypeSafeDecide.</returns>
        public static bool IsDecisionRequestType(RequestTypeEnum requestType)
        {
            return requestType == RequestTypeEnum.TypeSafeDecide;
        }

        /// <summary>
        /// Determine whether a request type lists or inspects models.
        /// </summary>
        /// <param name="requestType">Request type.</param>
        /// <returns>True for model listing and Anthropic get-model request types.</returns>
        public static bool IsModelListingRequestType(RequestTypeEnum requestType)
        {
            return requestType == RequestTypeEnum.OpenAIListModels ||
                requestType == RequestTypeEnum.GeminiListModels ||
                requestType == RequestTypeEnum.OllamaListTags ||
                requestType == RequestTypeEnum.OllamaListRunningModels ||
                requestType == RequestTypeEnum.AnthropicListModels ||
                requestType == RequestTypeEnum.AnthropicGetModel ||
                requestType == RequestTypeEnum.TypeSafeListModels;
        }

        /// <summary>
        /// Get the relative path namespace served by a virtual model runner type.
        /// Endpoint-only types map to the namespace of their wire API.
        /// </summary>
        /// <param name="vmrApiType">Virtual model runner API type.</param>
        /// <returns>The namespace, including leading and trailing slashes, for example /v1/. Never null.</returns>
        public static string GetApiNamespace(ApiTypeEnum vmrApiType)
        {
            switch (vmrApiType)
            {
                case ApiTypeEnum.Ollama:
                case ApiTypeEnum.OllamaCloud:
                    return "/api/";
                case ApiTypeEnum.Gemini:
                    return "/v1beta/";
                case ApiTypeEnum.OpenAI:
                case ApiTypeEnum.OpenAICompatible:
                case ApiTypeEnum.vLLM:
                case ApiTypeEnum.Anthropic:
                case ApiTypeEnum.TypeSafe:
                case ApiTypeEnum.Ollaya:
                default:
                    return "/v1/";
            }
        }

        /// <summary>
        /// Parse a URL path into a URL context without classifying the request. Extracts the virtual model runner base path,
        /// relative path and query string only; RequestType stays Unknown until Classify is called with the runner's
        /// configured API type.
        /// </summary>
        /// <param name="path">URL path, optionally including a query string. Null or empty returns an empty context.</param>
        /// <param name="httpMethod">HTTP method. Unused for structural parsing; kept for call-site symmetry.</param>
        /// <returns>URL context. Never null.</returns>
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

        /// <summary>
        /// Parse a URL path and classify it by a virtual model runner's configured API type.
        /// </summary>
        /// <param name="path">URL path, optionally including a query string. Null or empty returns an empty context.</param>
        /// <param name="httpMethod">HTTP method. Null is treated as GET.</param>
        /// <param name="vmrApiType">The virtual model runner's configured API type.</param>
        /// <returns>Classified URL context. Never null.</returns>
        public static UrlContext Parse(string path, string httpMethod, ApiTypeEnum vmrApiType)
        {
            UrlContext ctx = Parse(path, httpMethod);
            if (ctx.IsValidVmrRequest) ctx.Classify(vmrApiType, httpMethod);
            return ctx;
        }

        /// <summary>
        /// Classify the request by the virtual model runner's configured API type, using the namespace and route table for
        /// that type. Sets ApiType, RequestType, RequestedModel (for routes that embed the model), IsInNamespace and
        /// IsUnsupportedRoute. Safe to call more than once; each call resets the classification first. Not thread safe.
        /// </summary>
        /// <param name="vmrApiType">The virtual model runner's configured API type.</param>
        /// <param name="httpMethod">HTTP method. Null is treated as GET.</param>
        public void Classify(ApiTypeEnum vmrApiType, string httpMethod)
        {
            ApiType = vmrApiType;
            RequestType = RequestTypeEnum.Unknown;
            RequestedModel = null;
            IsInNamespace = false;
            IsUnsupportedRoute = false;
            HasUnsafePathSegments = false;
            IsClassified = true;

            if (String.IsNullOrEmpty(RelativePath)) return;

            if (ContainsUnsafePathSegments(RelativePath))
            {
                HasUnsafePathSegments = true;
                return;
            }

            string method = (httpMethod ?? "GET").ToUpperInvariant();
            string normalizedRelativePath = RelativePath.ToLowerInvariant();
            string apiNamespace = GetApiNamespace(vmrApiType);
            if (!normalizedRelativePath.StartsWith(apiNamespace, StringComparison.Ordinal)) return;
            IsInNamespace = true;

            switch (vmrApiType)
            {
                case ApiTypeEnum.Ollama:
                case ApiTypeEnum.OllamaCloud:
                    ClassifyOllama(normalizedRelativePath);
                    break;
                case ApiTypeEnum.Gemini:
                    ClassifyGemini(normalizedRelativePath);
                    break;
                case ApiTypeEnum.Anthropic:
                    ClassifyAnthropic(normalizedRelativePath, method);
                    break;
                case ApiTypeEnum.TypeSafe:
                case ApiTypeEnum.Ollaya:
                    ClassifyTypeSafe(normalizedRelativePath, method);
                    break;
                case ApiTypeEnum.OpenAI:
                case ApiTypeEnum.OpenAICompatible:
                case ApiTypeEnum.vLLM:
                default:
                    ClassifyOpenAI(normalizedRelativePath);
                    break;
            }
        }

        private void ClassifyOpenAI(string normalizedRelativePath)
        {
            if (normalizedRelativePath.StartsWith("/v1/chat/completions"))
            {
                RequestType = RequestTypeEnum.OpenAIChatCompletions;
            }
            else if (normalizedRelativePath.StartsWith("/v1/completions"))
            {
                RequestType = RequestTypeEnum.OpenAICompletions;
            }
            else if (normalizedRelativePath.StartsWith("/v1/models"))
            {
                RequestType = RequestTypeEnum.OpenAIListModels;
            }
            else if (normalizedRelativePath.StartsWith("/v1/embeddings"))
            {
                RequestType = RequestTypeEnum.OpenAIEmbeddings;
            }
        }

        private void ClassifyGemini(string normalizedRelativePath)
        {
            if (!normalizedRelativePath.StartsWith("/v1beta/models")) return;

            if (normalizedRelativePath.Equals("/v1beta/models"))
            {
                RequestType = RequestTypeEnum.GeminiListModels;
            }
            else if (normalizedRelativePath.StartsWith("/v1beta/models/"))
            {
                RequestedModel = ExtractGeminiModelSegment(RelativePath);

                if (normalizedRelativePath.Contains(":streamgeneratecontent"))
                {
                    RequestType = RequestTypeEnum.GeminiStreamGenerateContent;
                }
                else if (normalizedRelativePath.Contains(":generatecontent"))
                {
                    RequestType = RequestTypeEnum.GeminiGenerateContent;
                }
                else if (normalizedRelativePath.Contains(":embedcontent"))
                {
                    RequestType = RequestTypeEnum.GeminiEmbedContent;
                }
            }
        }

        private void ClassifyOllama(string normalizedRelativePath)
        {
            if (normalizedRelativePath.StartsWith("/api/generate"))
            {
                RequestType = RequestTypeEnum.OllamaGenerate;
            }
            else if (normalizedRelativePath.StartsWith("/api/chat"))
            {
                RequestType = RequestTypeEnum.OllamaChat;
            }
            else if (normalizedRelativePath.StartsWith("/api/tags"))
            {
                RequestType = RequestTypeEnum.OllamaListTags;
            }
            else if (normalizedRelativePath.StartsWith("/api/embeddings") || normalizedRelativePath.StartsWith("/api/embed"))
            {
                RequestType = RequestTypeEnum.OllamaEmbeddings;
            }
            else if (normalizedRelativePath.StartsWith("/api/pull"))
            {
                RequestType = RequestTypeEnum.OllamaPullModel;
            }
            else if (normalizedRelativePath.StartsWith("/api/delete"))
            {
                RequestType = RequestTypeEnum.OllamaDeleteModel;
            }
            else if (normalizedRelativePath.StartsWith("/api/ps"))
            {
                RequestType = RequestTypeEnum.OllamaListRunningModels;
            }
            else if (normalizedRelativePath.StartsWith("/api/show"))
            {
                RequestType = RequestTypeEnum.OllamaShowModelInfo;
            }
        }

        private void ClassifyAnthropic(string normalizedRelativePath, string method)
        {
            string trimmed = normalizedRelativePath.Length > 1 ? normalizedRelativePath.TrimEnd('/') : normalizedRelativePath;

            if (trimmed.Equals("/v1/messages/batches") || trimmed.StartsWith("/v1/messages/batches/")
                || trimmed.Equals("/v1/files") || trimmed.StartsWith("/v1/files/"))
            {
                IsUnsupportedRoute = true;
                return;
            }

            if (method == "POST" && trimmed.Equals("/v1/messages"))
            {
                RequestType = RequestTypeEnum.AnthropicMessages;
            }
            else if (method == "POST" && trimmed.Equals("/v1/messages/count_tokens"))
            {
                RequestType = RequestTypeEnum.AnthropicCountTokens;
            }
            else if (method == "GET" && trimmed.Equals("/v1/models"))
            {
                RequestType = RequestTypeEnum.AnthropicListModels;
            }
            else if (method == "GET" && trimmed.StartsWith("/v1/models/"))
            {
                string original = RelativePath.TrimEnd('/');
                string modelId = original.Substring("/v1/models/".Length);
                if (!String.IsNullOrEmpty(modelId) && modelId.IndexOf('/') < 0)
                {
                    RequestType = RequestTypeEnum.AnthropicGetModel;
                    RequestedModel = Uri.UnescapeDataString(modelId);
                }
            }
        }

        private void ClassifyTypeSafe(string normalizedRelativePath, string method)
        {
            string trimmed = normalizedRelativePath.Length > 1 ? normalizedRelativePath.TrimEnd('/') : normalizedRelativePath;

            if (method == "POST" && trimmed.Equals("/v1/systemone"))
            {
                RequestType = RequestTypeEnum.TypeSafeDecide;
            }
            else if (method == "GET" && trimmed.Equals("/v1/models"))
            {
                RequestType = RequestTypeEnum.TypeSafeListModels;
            }
        }

        /// <summary>
        /// Determine whether a path contains a dot segment ("." or ".."), a backslash, or a percent-encoded dot, slash or
        /// backslash. Percent-encoding is decoded repeatedly (up to MaxPathDecodePasses times) so double-encoded forms such as
        /// %252e%252e are caught as well. The query string, when present, is ignored.
        /// </summary>
        /// <param name="path">Path to inspect. Null or empty returns false.</param>
        /// <returns>True when the path must not be forwarded.</returns>
        public static bool ContainsUnsafePathSegments(string path)
        {
            if (String.IsNullOrEmpty(path)) return false;

            int queryIdx = path.IndexOf('?');
            string candidate = queryIdx >= 0 ? path.Substring(0, queryIdx) : path;

            for (int pass = 0; pass <= MaxPathDecodePasses; pass++)
            {
                if (candidate.IndexOf('\\') >= 0) return true;

                string lowered = candidate.ToLowerInvariant();
                if (lowered.Contains("%2e") || lowered.Contains("%2f") || lowered.Contains("%5c")) return true;

                foreach (string segment in candidate.Split('/'))
                {
                    if (segment == "." || segment == "..") return true;
                }

                if (candidate.IndexOf('%') < 0) return false;

                string decoded;
                try
                {
                    decoded = Uri.UnescapeDataString(candidate);
                }
                catch (UriFormatException)
                {
                    return true;
                }

                if (String.Equals(decoded, candidate, StringComparison.Ordinal)) return false;
                candidate = decoded;
            }

            return true;
        }

        /// <summary>
        /// Maximum number of percent-decoding passes ContainsUnsafePathSegments applies when looking for encoded dot
        /// segments and separators. A path that is still changing after this many passes is treated as unsafe.
        /// </summary>
        public const int MaxPathDecodePasses = 3;

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
        /// <param name="endpointApiKey">
        /// When not null, replaces the value of the <c>key</c> query parameter with
        /// the endpoint's own API key.  This is used for Gemini-compatible clients that
        /// authenticate to Conductor via <c>?key=</c>; the Conductor credential must be
        /// swapped for the real upstream key before forwarding.
        /// When null the query string is forwarded unchanged.
        /// </param>
        /// <returns>Full target URL.</returns>
        public string BuildTargetUrl(string baseUrl, string endpointApiKey = null)
        {
            if (String.IsNullOrEmpty(baseUrl)) return null;
            if (String.IsNullOrEmpty(RelativePath)) return baseUrl;

            baseUrl = baseUrl.TrimEnd('/');
            string path = RelativePath;
            if (!path.StartsWith("/")) path = "/" + path;

            string queryString = QueryString ?? String.Empty;

            // Replace the ?key= value with the endpoint's API key for Gemini forwarding
            if (!String.IsNullOrEmpty(endpointApiKey) && !String.IsNullOrEmpty(queryString))
            {
                queryString = ReplaceQueryParameter(queryString, "key", endpointApiKey);
            }

            return baseUrl + path + queryString;
        }

        /// <summary>
        /// Replace the value of a query string parameter, preserving all other parameters.
        /// </summary>
        /// <param name="queryString">Query string including the leading '?'.</param>
        /// <param name="paramName">Parameter name to replace.</param>
        /// <param name="newValue">New value for the parameter.</param>
        /// <returns>Updated query string.</returns>
        private static string ReplaceQueryParameter(string queryString, string paramName, string newValue)
        {
            if (String.IsNullOrEmpty(queryString)) return queryString;

            // Remove leading '?'
            string qs = queryString.StartsWith("?") ? queryString.Substring(1) : queryString;
            string[] pairs = qs.Split('&');
            bool found = false;

            for (int i = 0; i < pairs.Length; i++)
            {
                int eqIdx = pairs[i].IndexOf('=');
                string name = eqIdx >= 0 ? pairs[i].Substring(0, eqIdx) : pairs[i];
                if (String.Equals(name, paramName, StringComparison.OrdinalIgnoreCase))
                {
                    pairs[i] = name + "=" + Uri.EscapeDataString(newValue);
                    found = true;
                    break;
                }
            }

            if (!found) return queryString;
            return "?" + String.Join("&", pairs);
        }
    }
}
