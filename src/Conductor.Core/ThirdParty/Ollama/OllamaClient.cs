namespace Conductor.Core.ThirdParty.Ollama
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
    using Conductor.Core.ThirdParty.Ollama.Models;
    using System.Net.Http;
    using RestWrapper;

    /// <summary>
    /// Client for interacting with Ollama API endpoints.
    /// </summary>
    public class OllamaClient
    {
        /// <summary>
        /// Ollama API endpoint URL.
        /// </summary>
        public string Endpoint
        {
            get => _Endpoint;
            set => _Endpoint = value?.TrimEnd('/');
        }

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
        /// Instantiate the Ollama client.
        /// </summary>
        /// <param name="endpoint">Ollama API endpoint URL.</param>
        public OllamaClient(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentNullException(nameof(endpoint));

            Endpoint = endpoint;

            _JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            _JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        /// <summary>
        /// List locally available models.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of local models.</returns>
        public async Task<List<OllamaLocalModel>> ListLocalModelsAsync(CancellationToken token = default)
        {
            string url = $"{_Endpoint}/api/tags";
            JsonElement response = await GetAsync<JsonElement>(url, token).ConfigureAwait(false);

            if (response.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<OllamaLocalModel>>(response.GetRawText(), _JsonOptions);
            }
            else if (response.TryGetProperty("models", out JsonElement modelsElement))
            {
                return JsonSerializer.Deserialize<List<OllamaLocalModel>>(modelsElement.GetRawText(), _JsonOptions);
            }

            return new List<OllamaLocalModel>();
        }

        /// <summary>
        /// Pull a model from the registry.
        /// </summary>
        /// <param name="request">Pull model request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Async enumerable of pull progress responses.</returns>
        public async IAsyncEnumerable<PullModelResponse> PullModelAsync(
            OllamaPullModelRequest request,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Stream = true;
            string url = $"{_Endpoint}/api/pull";
            string json = JsonSerializer.Serialize(request, _JsonOptions);

            Log("DEBUG", $"POST {url}");
            if (LogRequests) Log("DEBUG", json);

            using RestRequest req = new RestRequest(url, HttpMethod.Post, "application/json");
            req.TimeoutMilliseconds = TimeoutMs;

            using RestResponse resp = await req.SendAsync(json, token).ConfigureAwait(false);

            if (resp.StatusCode < 200 || resp.StatusCode > 299)
            {
                Log("ERROR", $"Request failed with status {resp.StatusCode}");
                yield break;
            }

            await foreach (string line in ReadLinesAsync(resp, token))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                PullModelResponse result = null;
                try
                {
                    result = JsonSerializer.Deserialize<PullModelResponse>(line, _JsonOptions);
                }
                catch (JsonException)
                {
                    Log("WARN", $"Failed to parse pull response: {line}");
                    continue;
                }

                if (result != null)
                {
                    yield return result;
                    if (result.IsComplete()) yield break;
                }
            }
        }

        /// <summary>
        /// Delete a model.
        /// </summary>
        /// <param name="request">Delete model request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> DeleteModelAsync(OllamaDeleteModelRequest request, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string url = $"{_Endpoint}/api/delete";
            string json = JsonSerializer.Serialize(request, _JsonOptions);

            Log("DEBUG", $"DELETE {url}");
            if (LogRequests) Log("DEBUG", json);

            using RestRequest req = new RestRequest(url, HttpMethod.Delete, "application/json");
            req.TimeoutMilliseconds = TimeoutMs;

            using RestResponse resp = await req.SendAsync(json, token).ConfigureAwait(false);

            return resp.StatusCode >= 200 && resp.StatusCode <= 299;
        }

        /// <summary>
        /// Generate embeddings.
        /// </summary>
        /// <param name="request">Embeddings request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Embeddings result.</returns>
        public async Task<OllamaGenerateEmbeddingsResult> GenerateEmbeddingsAsync(
            OllamaGenerateEmbeddingsRequest request,
            CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string url = $"{_Endpoint}/api/embed";
            return await PostAsync<OllamaGenerateEmbeddingsResult>(url, request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Generate completion (non-streaming).
        /// </summary>
        /// <param name="request">Completion request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Completion result.</returns>
        public async Task<OllamaGenerateCompletionResult> GenerateCompletionAsync(
            OllamaGenerateCompletionRequest request,
            CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Stream = false;
            string url = $"{_Endpoint}/api/generate";
            return await PostAsync<OllamaGenerateCompletionResult>(url, request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Generate completion (streaming).
        /// </summary>
        /// <param name="request">Completion request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Async enumerable of streaming results.</returns>
        public async IAsyncEnumerable<OllamaStreamingCompletionResult> GenerateCompletionStreamAsync(
            OllamaGenerateCompletionRequest request,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Stream = true;
            string url = $"{_Endpoint}/api/generate";

            await foreach (OllamaStreamingCompletionResult result in PostStreamAsync<OllamaStreamingCompletionResult>(url, request, token))
            {
                yield return result;
                if (result.Done) yield break;
            }
        }

        /// <summary>
        /// Generate chat completion (non-streaming).
        /// </summary>
        /// <param name="request">Chat completion request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Completion result.</returns>
        public async Task<OllamaGenerateCompletionResult> GenerateChatCompletionAsync(
            OllamaGenerateChatCompletionRequest request,
            CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Stream = false;
            string url = $"{_Endpoint}/api/chat";
            return await PostAsync<OllamaGenerateCompletionResult>(url, request, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Generate chat completion (streaming).
        /// </summary>
        /// <param name="request">Chat completion request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Async enumerable of streaming chunks.</returns>
        public async IAsyncEnumerable<OllamaGenerateChatCompletionChunk> GenerateChatCompletionStreamAsync(
            OllamaGenerateChatCompletionRequest request,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Stream = true;
            string url = $"{_Endpoint}/api/chat";

            await foreach (OllamaGenerateChatCompletionChunk result in PostStreamAsync<OllamaGenerateChatCompletionChunk>(url, request, token))
            {
                yield return result;
                if (result.Done) yield break;
            }
        }

        private async Task<T> GetAsync<T>(string url, CancellationToken token)
        {
            Log("DEBUG", $"GET {url}");

            using RestRequest req = new RestRequest(url);
            req.TimeoutMilliseconds = TimeoutMs;

            using RestResponse resp = await req.SendAsync(token).ConfigureAwait(false);

            if (resp.StatusCode < 200 || resp.StatusCode > 299)
            {
                Log("ERROR", $"Request failed with status {resp.StatusCode}");
                return default;
            }

            string content = await ReadResponseAsync(resp, token).ConfigureAwait(false);
            if (LogResponses) Log("DEBUG", content);

            return JsonSerializer.Deserialize<T>(content, _JsonOptions);
        }

        private async Task<T> PostAsync<T>(string url, object data, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(data, _JsonOptions);

            Log("DEBUG", $"POST {url}");
            if (LogRequests) Log("DEBUG", json);

            using RestRequest req = new RestRequest(url, HttpMethod.Post, "application/json");
            req.TimeoutMilliseconds = TimeoutMs;

            using RestResponse resp = await req.SendAsync(json, token).ConfigureAwait(false);

            if (resp.StatusCode < 200 || resp.StatusCode > 299)
            {
                Log("ERROR", $"Request failed with status {resp.StatusCode}");
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

            using RestResponse resp = await req.SendAsync(json, token).ConfigureAwait(false);

            if (resp.StatusCode < 200 || resp.StatusCode > 299)
            {
                Log("ERROR", $"Request failed with status {resp.StatusCode}");
                yield break;
            }

            await foreach (string line in ReadLinesAsync(resp, token))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                T result = default;
                try
                {
                    result = JsonSerializer.Deserialize<T>(line, _JsonOptions);
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
