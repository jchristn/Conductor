namespace Conductor.Server.Services
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// Verifies whether a model is available or resident after a model load probe.
    /// </summary>
    public class ModelLoadVerificationService
    {
        private readonly IModelLoadTransport _Transport;
        private readonly ModelLoadProbeBuilder _ProbeBuilder;

        /// <summary>
        /// Instantiate the model load verification service.
        /// </summary>
        /// <param name="transport">Model load transport.</param>
        /// <param name="probeBuilder">Probe builder.</param>
        public ModelLoadVerificationService(IModelLoadTransport transport, ModelLoadProbeBuilder probeBuilder)
        {
            _Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _ProbeBuilder = probeBuilder ?? throw new ArgumentNullException(nameof(probeBuilder));
        }

        /// <summary>
        /// Verify model availability or residency using a provider-specific metadata endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="model">Model name.</param>
        /// <param name="timeoutMs">Request timeout in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the model was found in provider metadata.</returns>
        public async Task<bool> VerifyAsync(ModelRunnerEndpoint endpoint, string model, int timeoutMs, CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (String.IsNullOrWhiteSpace(model)) return false;

            ModelLoadProbePlan plan = endpoint.ApiType == ApiTypeEnum.Ollama
                ? _ProbeBuilder.BuildOllamaRunningModelsPlan()
                : _ProbeBuilder.BuildMetadataPlan(endpoint);

            ModelLoadTransportResponse response = await _Transport.SendAsync(endpoint, plan, timeoutMs, token).ConfigureAwait(false);
            if (response == null || !response.IsSuccessStatusCode)
            {
                return false;
            }

            return ContainsModel(endpoint.ApiType, response.Body, model);
        }

        /// <summary>
        /// Check whether an already available upstream response body references the requested model.
        /// </summary>
        /// <param name="apiType">Endpoint API type.</param>
        /// <param name="body">Provider response body.</param>
        /// <param name="model">Model name.</param>
        /// <returns>True if the model was found.</returns>
        public bool ContainsModel(ApiTypeEnum apiType, string body, string model)
        {
            if (String.IsNullOrWhiteSpace(body) || String.IsNullOrWhiteSpace(model))
            {
                return false;
            }

            try
            {
                using (JsonDocument document = JsonDocument.Parse(body))
                {
                    return ContainsModel(document.RootElement, model);
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool ContainsModel(JsonElement element, string model)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                string name = TryGetStringProperty(element, "model")
                    ?? TryGetStringProperty(element, "name")
                    ?? TryGetStringProperty(element, "id")
                    ?? TryGetStringProperty(element, "Model")
                    ?? TryGetStringProperty(element, "Name")
                    ?? TryGetStringProperty(element, "Id");

                if (ModelNamesMatch(name, model))
                {
                    return true;
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array || property.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (ContainsModel(property.Value, model))
                        {
                            return true;
                        }
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (ContainsModel(item, model))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string TryGetStringProperty(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;

            if (element.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            return null;
        }

        private static bool ModelNamesMatch(string candidate, string model)
        {
            if (String.IsNullOrWhiteSpace(candidate) || String.IsNullOrWhiteSpace(model))
            {
                return false;
            }

            if (String.Equals(candidate, model, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string normalizedCandidate = candidate.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                ? candidate.Substring("models/".Length)
                : candidate;
            string normalizedModel = model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                ? model.Substring("models/".Length)
                : model;

            return String.Equals(normalizedCandidate, normalizedModel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
