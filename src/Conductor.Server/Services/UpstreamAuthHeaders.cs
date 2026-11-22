namespace Conductor.Server.Services
{
    using System;
    using System.Net.Http;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;

    /// <summary>
    /// Applies the upstream authentication headers each endpoint API type needs. Shared by the proxy, health checks and
    /// model load transport so every outbound call to an endpoint authenticates the same way. All members are static and
    /// thread safe; they mutate only the request passed in.
    /// </summary>
    public static class UpstreamAuthHeaders
    {
        /// <summary>
        /// Header carrying the Anthropic API key.
        /// </summary>
        public const string AnthropicApiKeyHeader = "x-api-key";

        /// <summary>
        /// Header carrying the Anthropic API version.
        /// </summary>
        public const string AnthropicVersionHeader = "anthropic-version";

        /// <summary>
        /// Header carrying the Gemini API key.
        /// </summary>
        public const string GeminiApiKeyHeader = "x-goog-api-key";

        /// <summary>
        /// Get the name of the header that carries the endpoint's upstream key.
        /// </summary>
        /// <param name="apiType">Endpoint API type.</param>
        /// <returns>x-goog-api-key for Gemini, x-api-key for Anthropic, Authorization otherwise. Never null.</returns>
        public static string GetKeyHeaderName(ApiTypeEnum apiType)
        {
            switch (apiType)
            {
                case ApiTypeEnum.Gemini:
                    return GeminiApiKeyHeader;
                case ApiTypeEnum.Anthropic:
                    return AnthropicApiKeyHeader;
                default:
                    return "Authorization";
            }
        }

        /// <summary>
        /// Resolve the effective anthropic-version: the client's value when it sent one, then the endpoint's
        /// AnthropicVersion, then the settings default.
        /// </summary>
        /// <param name="clientVersion">The client's anthropic-version header value. Null or empty when absent.</param>
        /// <param name="endpoint">Endpoint. Null skips the endpoint value.</param>
        /// <param name="settings">Anthropic settings. Null uses defaults.</param>
        /// <returns>The effective version. Never null.</returns>
        public static string ResolveAnthropicVersion(string clientVersion, ModelRunnerEndpoint endpoint, AnthropicSettings settings)
        {
            if (!String.IsNullOrWhiteSpace(clientVersion)) return clientVersion.Trim();
            if (endpoint != null && !String.IsNullOrWhiteSpace(endpoint.AnthropicVersion)) return endpoint.AnthropicVersion.Trim();
            return (settings ?? new AnthropicSettings()).DefaultVersion;
        }

        /// <summary>
        /// Add the endpoint's upstream credential and required per-family headers to a request.
        /// Gemini: x-goog-api-key. Anthropic: x-api-key plus anthropic-version (client value, endpoint value, then setting).
        /// All other types: Authorization Bearer when a key is set. Inbound client credentials are never copied.
        /// </summary>
        /// <param name="request">Outbound request. Must not be null.</param>
        /// <param name="endpoint">Endpoint. Must not be null.</param>
        /// <param name="settings">Anthropic settings. Null uses defaults.</param>
        /// <param name="clientHeader">Lookup for inbound client header values by name, used only for anthropic-version.
        /// Null when there is no client request (health checks, model load).</param>
        /// <returns>The anthropic-version sent for Anthropic endpoints; null for other types.</returns>
        /// <exception cref="ArgumentNullException">Thrown when request or endpoint is null.</exception>
        public static string Apply(HttpRequestMessage request, ModelRunnerEndpoint endpoint, AnthropicSettings settings, Func<string, string> clientHeader = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(endpoint);

            bool hasKey = !String.IsNullOrEmpty(endpoint.ApiKey);
            switch (endpoint.ApiType)
            {
                case ApiTypeEnum.Gemini:
                    if (hasKey) request.Headers.TryAddWithoutValidation(GeminiApiKeyHeader, endpoint.ApiKey);
                    return null;
                case ApiTypeEnum.Anthropic:
                    if (hasKey) request.Headers.TryAddWithoutValidation(AnthropicApiKeyHeader, endpoint.ApiKey);
                    string clientVersion = clientHeader != null ? clientHeader(AnthropicVersionHeader) : null;
                    string version = ResolveAnthropicVersion(clientVersion, endpoint, settings);
                    request.Headers.TryAddWithoutValidation(AnthropicVersionHeader, version);
                    return version;
                default:
                    if (hasKey) request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + endpoint.ApiKey);
                    return null;
            }
        }
    }
}
