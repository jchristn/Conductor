namespace Conductor.Core.ThirdParty.OpenAI
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.ThirdParty.OpenAI.Models;
    using System.Net.Http;
    using RestWrapper;

    /// <summary>
    /// Client for interacting with OpenAI-compatible API endpoints.
    /// </summary>
    public class OpenAIClient
    {
        /// <summary>
        /// OpenAI API endpoint URL.
        /// </summary>
        public string Endpoint
        {
            get => _Endpoint;
            set => _Endpoint = value?.TrimEnd('/');
        }

        /// <summary>
        /// API key for authentication.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// HTTP request timeout in milliseconds (default: 300000ms / 5 minutes).
        /// </summary>
        public int TimeoutMs { get; set; } = 300000;

        /// <summary>
        /// Enable logging of request bodies.
        /// </summary>
        public bool LogRequests { get; set; } = false;

        /// <summary>
        /// Enable logging of response bodies.
        /// </summary>
        public bool LogResponses { get; set; } = false;

        /// <summary>
        /// Logger callback (level, message).
        /// </summary>
        public Action<string, string> Logger { get; set; } = null;

        private string _Endpoint;
        private JsonSerializerOptions _JsonOptions;

        /// <summary>
        /// Instantiate the OpenAI client.
        /// </summary>
        /// <param name="endpoint">OpenAI API endpoint URL.</param>
        /// <param name="apiKey">API key for authentication (optional for some backends).</param>
        public OpenAIClient(string endpoint, string apiKey = null)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentNullException(nameof(endpoint));

            Endpoint = endpoint;
            ApiKey = apiKey;

            _JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            _JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        /// <summary>
        /// Generate embeddings.
        /// </summary>
        /// <param name="request">Embeddings request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Embeddings result.</returns>
        public async Task<OpenAIGenerateEmbeddingsResult> GenerateEmbeddingsAsync(
            OpenAIGenerateEmbeddingsRequest request,
            CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string url = $"{_Endpoint}/v1/embeddings";
            return await PostAsync<OpenAIGenerateEmbeddingsResult>(url, request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Generate completion (legacy endpoint, non-streaming).
        /// </summary>
        /// <param name="request">Completion request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Completion result.</returns>
        public async Task<OpenAIGenerateCompletionResult> GenerateCompletionAsync(
            OpenAIGenerateCompletionRequest request,
            CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Stream = false;
            string url = $"{_Endpoint}/v1/completions";
            return await PostAsync<OpenAIGenerateCompletionResult>(url, request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Generate completion (legacy endpoint, streaming).
        /// </summary>
        /// <param name="request">Completion request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Async enumerable of streaming results.</returns>
        public async IAsyncEnumerable<OpenAIStreamingCompletionResult> GenerateCompletionStreamAsync(
            OpenAIGenerateCompletionRequest request,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Stream = true;
            string url = $"{_Endpoint}/v1/completions";

            await foreach (OpenAIStreamingCompletionResult result in PostStreamAsync<OpenAIStreamingCompletionResult>(url, request, token))
            {
                yield return result;
            }
        }

        /// <summary>
        /// Generate chat completion (non-streaming).
        /// </summary>
        /// <param name="request">Chat completion request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Chat completion result.</returns>
        public async Task<OpenAIGenerateChatCompletionResult> GenerateChatCompletionAsync(
            OpenAIGenerateChatCompletionRequest request,
            CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Stream = false;
            string url = $"{_Endpoint}/v1/chat/completions";
            return await PostAsync<OpenAIGenerateChatCompletionResult>(url, request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Generate chat completion (streaming).
        /// </summary>
        /// <param name="request">Chat completion request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Async enumerable of streaming results.</returns>
        public async IAsyncEnumerable<OpenAIGenerateChatCompletionResult> GenerateChatCompletionStreamAsync(
            OpenAIGenerateChatCompletionRequest request,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Stream = true;
            string url = $"{_Endpoint}/v1/chat/completions";

            await foreach (OpenAIGenerateChatCompletionResult result in PostStreamAsync<OpenAIGenerateChatCompletionResult>(url, request, token))
            {
                yield return result;
            }
        }

        private async Task<T> PostAsync<T>(string url, object data, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(data, _JsonOptions);

            Log("DEBUG", $"POST {url}");
            if (LogRequests) Log("DEBUG", json);

            using RestRequest req = new RestRequest(url, HttpMethod.Post, "application/json");
            req.TimeoutMilliseconds = TimeoutMs;

            if (!string.IsNullOrEmpty(ApiKey))
            {
                req.Authorization.BearerToken = ApiKey;
            }

            using RestResponse resp = await req.SendAsync(json, token).ConfigureAwait(false);

            if (resp.StatusCode < 200 || resp.StatusCode > 299)
            {
                string errorContent = await ReadResponseAsync(resp, token).ConfigureAwait(false);
                Log("ERROR", $"Request failed with status {resp.StatusCode}: {errorContent}");
                return default;
            }

            string content = await ReadResponseAsync(resp, token).ConfigureAwait(false);
            if (LogResponses) Log("DEBUG", content);

            return JsonSerializer.Deserialize<T>(content, _JsonOptions);
        }

        private async IAsyncEnumerable<T> PostStreamAsync<T>(
            string url,
            object data,
            [EnumeratorCancellation] CancellationToken token)
        {
            string json = JsonSerializer.Serialize(data, _JsonOptions);

            Log("DEBUG", $"POST (streaming) {url}");
            if (LogRequests) Log("DEBUG", json);

            using RestRequest req = new RestRequest(url, HttpMethod.Post, "application/json");
            req.TimeoutMilliseconds = TimeoutMs;

            if (!string.IsNullOrEmpty(ApiKey))
            {
                req.Authorization.BearerToken = ApiKey;
            }

            using RestResponse resp = await req.SendAsync(json, token).ConfigureAwait(false);

            if (resp.StatusCode < 200 || resp.StatusCode > 299)
            {
                string errorContent = await ReadResponseAsync(resp, token).ConfigureAwait(false);
                Log("ERROR", $"Request failed with status {resp.StatusCode}: {errorContent}");
                yield break;
            }

            await foreach (string line in ReadLinesAsync(resp, token))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // OpenAI streaming responses are prefixed with "data: "
                string processedLine = line;
                if (processedLine.StartsWith("data: "))
                {
                    processedLine = processedLine.Substring(6);
                }

                // "[DONE]" signals end of stream
                if (processedLine == "[DONE]") yield break;

                T result = default;
                try
                {
                    result = JsonSerializer.Deserialize<T>(processedLine, _JsonOptions);
                }
                catch (JsonException ex)
                {
                    Log("WARN", $"Failed to parse streaming response: {ex.Message}");
                    continue;
                }

                if (result != null)
                {
                    yield return result;
                }
            }
        }

        private async Task<string> ReadResponseAsync(RestResponse resp, CancellationToken token)
        {
            if (resp.Data != null)
            {
                using StreamReader reader = new StreamReader(resp.Data, Encoding.UTF8);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
            return resp.DataAsString ?? string.Empty;
        }

        private async IAsyncEnumerable<string> ReadLinesAsync(
            RestResponse resp,
            [EnumeratorCancellation] CancellationToken token)
        {
            if (resp.Data == null) yield break;

            using StreamReader reader = new StreamReader(resp.Data, Encoding.UTF8);
            string line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (token.IsCancellationRequested) yield break;
                yield return line;
            }
        }

        private void Log(string level, string message)
        {
            Logger?.Invoke(level, message);
        }
    }
}
