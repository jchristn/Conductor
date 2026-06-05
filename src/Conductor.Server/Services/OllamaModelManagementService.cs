namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Requests;
    using Conductor.Core.Responses;
    using Conductor.Core.Serialization;
    using Conductor.Core.ThirdParty.Ollama.Models;
    using WatsonWebserver.Core;

    /// <summary>
    /// Service for managing models on Ollama model runner endpoints.
    /// </summary>
    public class OllamaModelManagementService : IDisposable
    {
        private readonly IModelLoadTransport _Transport;
        private readonly bool _OwnsTransport;
        private readonly Serializer _Serializer = new Serializer();
        private bool _Disposed;

        /// <summary>
        /// Instantiate the Ollama model management service.
        /// </summary>
        /// <param name="transport">Optional transport override for tests.</param>
        public OllamaModelManagementService(IModelLoadTransport transport = null)
        {
            _Transport = transport ?? new DefaultModelLoadTransport();
            _OwnsTransport = transport == null;
        }

        /// <summary>
        /// List locally available models on an Ollama endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model list response.</returns>
        public async Task<OllamaModelListResponse> ListModelsAsync(
            ModelRunnerEndpoint endpoint,
            CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            EnsureOllamaEndpoint(endpoint);

            ModelLoadProbePlan plan = new ModelLoadProbePlan
            {
                Method = "GET",
                Path = "/api/tags",
                Mechanism = "OllamaListTags",
                MetadataOnly = true
            };

            ModelLoadTransportResponse transportResponse = await _Transport.SendAsync(
                endpoint,
                plan,
                NormalizeTimeout(endpoint.TimeoutMs, 300000, 1800000),
                token).ConfigureAwait(false);

            OllamaModelListResponse response = new OllamaModelListResponse
            {
                TenantId = endpoint.TenantId,
                EndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                BaseUrl = endpoint.GetBaseUrl(),
                ProviderStatusCode = transportResponse?.StatusCode
            };

            if (transportResponse == null)
            {
                response.Message = "No upstream response was returned.";
                response.ErrorMessage = response.Message;
                return response;
            }

            if (!transportResponse.IsSuccessStatusCode)
            {
                response.Message = "Ollama model list request failed.";
                response.ErrorMessage = BuildUpstreamError("Ollama returned HTTP " + transportResponse.StatusCode + ".", transportResponse.Body);
                return response;
            }

            try
            {
                response.Models = ParseModels(transportResponse.Body);
                response.Success = true;
                response.Message = "Listed " + response.Models.Count + " Ollama model(s).";
                return response;
            }
            catch (JsonException ex)
            {
                response.Message = "Ollama model list response could not be parsed.";
                response.ErrorMessage = ex.Message;
                return response;
            }
        }

        /// <summary>
        /// Pull a model onto an Ollama endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="request">Pull request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Operation response.</returns>
        public async Task<OllamaModelOperationResponse> PullModelAsync(
            ModelRunnerEndpoint endpoint,
            OllamaModelPullRequest request,
            CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            EnsureOllamaEndpoint(endpoint);
            OllamaModelPullRequest normalizedRequest = request ?? new OllamaModelPullRequest();
            string model = NormalizeModel(normalizedRequest.Model);

            Dictionary<string, object> body = new Dictionary<string, object>
            {
                { "model", model },
                { "stream", false }
            };

            if (normalizedRequest.Insecure)
            {
                body.Add("insecure", true);
            }

            ModelLoadProbePlan plan = new ModelLoadProbePlan
            {
                Method = "POST",
                Path = "/api/pull",
                BodyJson = _Serializer.SerializeJson(body, false),
                Mechanism = "OllamaPullModel",
                ExplicitLoad = true,
                HostLocalLoadSupported = true
            };

            return await SendOperationAsync(
                endpoint,
                plan,
                "Pull",
                model,
                NormalizeTimeout(normalizedRequest.TimeoutMs, 1800000, 7200000),
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a model from an Ollama endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="request">Delete request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Operation response.</returns>
        public async Task<OllamaModelOperationResponse> DeleteModelAsync(
            ModelRunnerEndpoint endpoint,
            OllamaModelDeleteRequest request,
            CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            EnsureOllamaEndpoint(endpoint);
            OllamaModelDeleteRequest normalizedRequest = request ?? new OllamaModelDeleteRequest();
            string model = NormalizeModel(normalizedRequest.Model);

            ModelLoadProbePlan plan = new ModelLoadProbePlan
            {
                Method = "DELETE",
                Path = "/api/delete",
                BodyJson = _Serializer.SerializeJson(new Dictionary<string, object>
                {
                    { "model", model }
                }, false),
                Mechanism = "OllamaDeleteModel",
                HostLocalLoadSupported = true
            };

            return await SendOperationAsync(
                endpoint,
                plan,
                "Delete",
                model,
                NormalizeTimeout(normalizedRequest.TimeoutMs, 300000, 1800000),
                token).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">True when disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing && _OwnsTransport && _Transport is IDisposable disposableTransport)
            {
                disposableTransport.Dispose();
            }

            _Disposed = true;
        }

        private async Task<OllamaModelOperationResponse> SendOperationAsync(
            ModelRunnerEndpoint endpoint,
            ModelLoadProbePlan plan,
            string operation,
            string model,
            int timeoutMs,
            CancellationToken token)
        {
            ModelLoadTransportResponse transportResponse = await _Transport.SendAsync(
                endpoint,
                plan,
                timeoutMs,
                token).ConfigureAwait(false);

            OllamaModelOperationResponse response = new OllamaModelOperationResponse
            {
                TenantId = endpoint.TenantId,
                EndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                BaseUrl = endpoint.GetBaseUrl(),
                Operation = operation,
                Model = model,
                ProviderStatusCode = transportResponse?.StatusCode,
                ProviderBody = transportResponse?.Body
            };

            if (transportResponse == null)
            {
                response.Message = "No upstream response was returned.";
                response.ErrorMessage = response.Message;
                return response;
            }

            if (!transportResponse.IsSuccessStatusCode)
            {
                response.Message = "Ollama " + operation.ToLowerInvariant() + " request failed.";
                response.ErrorMessage = BuildUpstreamError("Ollama returned HTTP " + transportResponse.StatusCode + ".", transportResponse.Body);
                return response;
            }

            string providerError = TryReadProviderProperty(transportResponse.Body, "error");
            if (!String.IsNullOrWhiteSpace(providerError))
            {
                response.Message = "Ollama " + operation.ToLowerInvariant() + " request failed.";
                response.ErrorMessage = providerError;
                return response;
            }

            string providerStatus = TryReadProviderProperty(transportResponse.Body, "status");
            response.Success = true;
            response.Message = String.IsNullOrWhiteSpace(providerStatus)
                ? "Ollama " + operation.ToLowerInvariant() + " request completed."
                : "Ollama " + operation.ToLowerInvariant() + " request completed: " + providerStatus + ".";
            return response;
        }

        private static void EnsureOllamaEndpoint(ModelRunnerEndpoint endpoint)
        {
            if (endpoint.ApiType != ApiTypeEnum.Ollama)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Ollama model management is only supported for Ollama endpoints.");
            }
        }

        private static string NormalizeModel(string model)
        {
            if (String.IsNullOrWhiteSpace(model))
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Model is required.");
            }

            return model.Trim();
        }

        private static int NormalizeTimeout(int timeoutMs, int defaultTimeoutMs, int maximumTimeoutMs)
        {
            if (timeoutMs < 1000)
            {
                return defaultTimeoutMs;
            }

            if (timeoutMs > maximumTimeoutMs)
            {
                return maximumTimeoutMs;
            }

            return timeoutMs;
        }

        private List<OllamaLocalModel> ParseModels(string body)
        {
            if (String.IsNullOrWhiteSpace(body))
            {
                return new List<OllamaLocalModel>();
            }

            using (JsonDocument document = JsonDocument.Parse(body))
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    return _Serializer.DeserializeJson<List<OllamaLocalModel>>(root.GetRawText()) ?? new List<OllamaLocalModel>();
                }

                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("models", out JsonElement modelsElement)
                    && modelsElement.ValueKind == JsonValueKind.Array)
                {
                    return _Serializer.DeserializeJson<List<OllamaLocalModel>>(modelsElement.GetRawText()) ?? new List<OllamaLocalModel>();
                }
            }

            return new List<OllamaLocalModel>();
        }

        private static string BuildUpstreamError(string fallback, string body)
        {
            string providerError = TryReadProviderProperty(body, "error");
            if (!String.IsNullOrWhiteSpace(providerError))
            {
                return fallback + " " + providerError;
            }

            string providerMessage = TryReadProviderProperty(body, "message");
            if (!String.IsNullOrWhiteSpace(providerMessage))
            {
                return fallback + " " + providerMessage;
            }

            return fallback;
        }

        private static string TryReadProviderProperty(string body, string propertyName)
        {
            if (String.IsNullOrWhiteSpace(body) || String.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            try
            {
                using (JsonDocument document = JsonDocument.Parse(body))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object
                        || !document.RootElement.TryGetProperty(propertyName, out JsonElement property))
                    {
                        return null;
                    }

                    if (property.ValueKind == JsonValueKind.String)
                    {
                        return property.GetString();
                    }

                    if (property.ValueKind == JsonValueKind.Number
                        || property.ValueKind == JsonValueKind.True
                        || property.ValueKind == JsonValueKind.False)
                    {
                        return property.GetRawText();
                    }
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }
    }
}
