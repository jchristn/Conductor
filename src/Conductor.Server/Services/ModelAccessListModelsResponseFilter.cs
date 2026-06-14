namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Applies model access decisions to provider list-model responses.
    /// </summary>
    public class ModelAccessListModelsResponseFilter
    {
        private const string _Header = "[ModelAccessListModelsResponseFilter] ";

        private readonly DatabaseDriverBase _Database;
        private readonly IModelAccessControlService _ModelAccessControlService;
        private readonly ModelAccessControlSettings _Settings;
        private readonly LoggingModule _Logging;

        /// <summary>
        /// Instantiate the response filter.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="modelAccessControlService">Model access control service.</param>
        /// <param name="settings">Model access control settings.</param>
        /// <param name="logging">Logging module.</param>
        public ModelAccessListModelsResponseFilter(
            DatabaseDriverBase database,
            IModelAccessControlService modelAccessControlService,
            ModelAccessControlSettings settings,
            LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _ModelAccessControlService = modelAccessControlService;
            _Settings = settings ?? new ModelAccessControlSettings();
            _Logging = logging;
        }

        /// <summary>
        /// Filter or synthesize a provider list-model response according to model access settings.
        /// </summary>
        /// <param name="vmr">Virtual model runner.</param>
        /// <param name="routingResult">Routing result.</param>
        /// <param name="requestContext">Request context.</param>
        /// <param name="responseBody">Original upstream response body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Filtered or original response bytes.</returns>
        public async Task<byte[]> ApplyAsync(
            VirtualModelRunner vmr,
            RoutingExecutionResult routingResult,
            RequestContext requestContext,
            byte[] responseBody,
            CancellationToken token = default)
        {
            if (!ShouldApply(vmr, routingResult, requestContext, responseBody))
            {
                return responseBody;
            }

            List<ModelDefinition> attachedDefinitions = await ResolveActiveModelDefinitionsAsync(vmr, token).ConfigureAwait(false);
            Dictionary<string, ModelDefinition> definitionsByName = attachedDefinitions
                .GroupBy(item => item.Name, StringComparer.InvariantCultureIgnoreCase)
                .ToDictionary(item => item.Key, item => item.First(), StringComparer.InvariantCultureIgnoreCase);

            if (_Settings.ListModelsBehavior == ModelAccessListModelsBehaviorEnum.Synthesize)
            {
                return await SynthesizeAsync(vmr, requestContext, attachedDefinitions, token).ConfigureAwait(false);
            }

            return await FilterAsync(vmr, requestContext, responseBody, definitionsByName, token).ConfigureAwait(false);
        }

        private bool ShouldApply(VirtualModelRunner vmr, RoutingExecutionResult routingResult, RequestContext requestContext, byte[] responseBody)
        {
            if (vmr == null || routingResult?.Decision == null || requestContext == null || responseBody == null) return false;
            if (!_Settings.Enabled || _Settings.Mode != ModelAccessEnforcementModeEnum.Enforce) return false;
            if (_Settings.ListModelsBehavior == ModelAccessListModelsBehaviorEnum.RawPassThrough) return false;
            if (!routingResult.Decision.Success || routingResult.Decision.HttpStatusCode < 200 || routingResult.Decision.HttpStatusCode > 299) return false;
            return IsListModelsRequest(requestContext.RequestType);
        }

        private async Task<byte[]> FilterAsync(
            VirtualModelRunner vmr,
            RequestContext requestContext,
            byte[] responseBody,
            Dictionary<string, ModelDefinition> definitionsByName,
            CancellationToken token)
        {
            JsonNode root = TryParse(responseBody);
            if (root == null)
            {
                _Logging?.Warn(_Header + "could not parse list-model response; returning empty provider-shaped list");
                return CreateEmptyResponse(requestContext.RequestType);
            }

            switch (requestContext.RequestType)
            {
                case RequestTypeEnum.OpenAIListModels:
                    await FilterArrayAsync(root["data"] as JsonArray, item => ExtractString(item, "id"), vmr, requestContext, definitionsByName, token).ConfigureAwait(false);
                    return Serialize(root);
                case RequestTypeEnum.GeminiListModels:
                    await FilterArrayAsync(root["models"] as JsonArray, item => NormalizeGeminiModelName(ExtractString(item, "name")), vmr, requestContext, definitionsByName, token).ConfigureAwait(false);
                    return Serialize(root);
                case RequestTypeEnum.OllamaListTags:
                case RequestTypeEnum.OllamaListRunningModels:
                    await FilterArrayAsync(root["models"] as JsonArray, ExtractOllamaModelName, vmr, requestContext, definitionsByName, token).ConfigureAwait(false);
                    return Serialize(root);
                default:
                    return responseBody;
            }
        }

        private async Task FilterArrayAsync(
            JsonArray models,
            Func<JsonNode, string> getModelName,
            VirtualModelRunner vmr,
            RequestContext requestContext,
            Dictionary<string, ModelDefinition> definitionsByName,
            CancellationToken token)
        {
            if (models == null) return;

            for (int i = models.Count - 1; i >= 0; i--)
            {
                JsonNode item = models[i];
                string modelName = getModelName(item);
                if (String.IsNullOrWhiteSpace(modelName)
                    || !await IsModelAllowedAsync(vmr, requestContext, modelName, definitionsByName, token).ConfigureAwait(false))
                {
                    models.RemoveAt(i);
                }
            }
        }

        private async Task<byte[]> SynthesizeAsync(
            VirtualModelRunner vmr,
            RequestContext requestContext,
            List<ModelDefinition> attachedDefinitions,
            CancellationToken token)
        {
            List<ModelDefinition> allowedDefinitions = new List<ModelDefinition>();
            Dictionary<string, ModelDefinition> definitionsByName = attachedDefinitions
                .GroupBy(item => item.Name, StringComparer.InvariantCultureIgnoreCase)
                .ToDictionary(item => item.Key, item => item.First(), StringComparer.InvariantCultureIgnoreCase);

            foreach (ModelDefinition definition in attachedDefinitions.OrderBy(item => item.Name, StringComparer.InvariantCultureIgnoreCase))
            {
                if (await IsModelAllowedAsync(vmr, requestContext, definition.Name, definitionsByName, token).ConfigureAwait(false))
                {
                    allowedDefinitions.Add(definition);
                }
            }

            switch (requestContext.RequestType)
            {
                case RequestTypeEnum.OpenAIListModels:
                    return Serialize(CreateOpenAIResponse(allowedDefinitions));
                case RequestTypeEnum.GeminiListModels:
                    return Serialize(CreateGeminiResponse(allowedDefinitions));
                case RequestTypeEnum.OllamaListTags:
                case RequestTypeEnum.OllamaListRunningModels:
                    return Serialize(CreateOllamaResponse(allowedDefinitions));
                default:
                    return CreateEmptyResponse(requestContext.RequestType);
            }
        }

        private async Task<bool> IsModelAllowedAsync(
            VirtualModelRunner vmr,
            RequestContext requestContext,
            string modelName,
            Dictionary<string, ModelDefinition> definitionsByName,
            CancellationToken token)
        {
            if (_ModelAccessControlService == null) return true;

            definitionsByName.TryGetValue(modelName, out ModelDefinition definition);
            ModelAccessEvaluationResult result = await _ModelAccessControlService.EvaluateAsync(new ModelAccessEvaluationContext
            {
                TenantId = vmr.TenantId,
                CredentialId = requestContext.CredentialId,
                UserId = requestContext.UserId,
                VirtualModelRunnerId = vmr.Id,
                ModelAccessPolicyId = vmr.ModelAccessPolicyId,
                RequestedModel = modelName,
                EffectiveModel = modelName,
                ModelDefinitionId = definition?.Id,
                ModelDefinitionName = definition?.Name,
                ModelLabels = definition?.Labels,
                Action = ModelAccessActionEnum.ListModels,
                RequestType = requestContext.RequestType,
                ApiType = requestContext.ApiType
            }, token).ConfigureAwait(false);

            return result == null || result.Allowed;
        }

        private async Task<List<ModelDefinition>> ResolveActiveModelDefinitionsAsync(VirtualModelRunner vmr, CancellationToken token)
        {
            List<ModelDefinition> definitions = new List<ModelDefinition>();
            foreach (string definitionId in vmr.ModelDefinitionIds ?? new List<string>())
            {
                ModelDefinition definition = await _Database.ModelDefinition.ReadAsync(vmr.TenantId, definitionId, token).ConfigureAwait(false);
                if (definition != null && definition.Active)
                {
                    definitions.Add(definition);
                }
            }

            return definitions;
        }

        private static JsonObject CreateOpenAIResponse(List<ModelDefinition> definitions)
        {
            JsonArray data = new JsonArray();
            foreach (ModelDefinition definition in definitions)
            {
                data.Add(new JsonObject
                {
                    ["id"] = definition.Name,
                    ["object"] = "model",
                    ["created"] = 0,
                    ["owned_by"] = "conductor"
                });
            }

            return new JsonObject
            {
                ["object"] = "list",
                ["data"] = data
            };
        }

        private static JsonObject CreateGeminiResponse(List<ModelDefinition> definitions)
        {
            JsonArray models = new JsonArray();
            foreach (ModelDefinition definition in definitions)
            {
                JsonArray supportedMethods = new JsonArray();
                if (definition.SupportsCompletions) supportedMethods.Add("generateContent");
                if (definition.SupportsEmbeddings) supportedMethods.Add("embedContent");

                models.Add(new JsonObject
                {
                    ["name"] = "models/" + definition.Name,
                    ["displayName"] = definition.Name,
                    ["supportedGenerationMethods"] = supportedMethods
                });
            }

            return new JsonObject
            {
                ["models"] = models
            };
        }

        private static JsonObject CreateOllamaResponse(List<ModelDefinition> definitions)
        {
            JsonArray models = new JsonArray();
            foreach (ModelDefinition definition in definitions)
            {
                models.Add(new JsonObject
                {
                    ["name"] = definition.Name,
                    ["model"] = definition.Name
                });
            }

            return new JsonObject
            {
                ["models"] = models
            };
        }

        private static byte[] CreateEmptyResponse(RequestTypeEnum requestType)
        {
            switch (requestType)
            {
                case RequestTypeEnum.OpenAIListModels:
                    return Serialize(CreateOpenAIResponse(new List<ModelDefinition>()));
                case RequestTypeEnum.GeminiListModels:
                    return Serialize(CreateGeminiResponse(new List<ModelDefinition>()));
                case RequestTypeEnum.OllamaListTags:
                case RequestTypeEnum.OllamaListRunningModels:
                    return Serialize(CreateOllamaResponse(new List<ModelDefinition>()));
                default:
                    return Encoding.UTF8.GetBytes("{}");
            }
        }

        private static JsonNode TryParse(byte[] responseBody)
        {
            try
            {
                return JsonNode.Parse(responseBody);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] Serialize(JsonNode node)
        {
            return Encoding.UTF8.GetBytes(node?.ToJsonString() ?? "{}");
        }

        private static string ExtractString(JsonNode item, string propertyName)
        {
            if (item == null || String.IsNullOrWhiteSpace(propertyName)) return null;
            return item[propertyName]?.GetValue<string>();
        }

        private static string ExtractOllamaModelName(JsonNode item)
        {
            return ExtractString(item, "name") ?? ExtractString(item, "model");
        }

        private static string NormalizeGeminiModelName(string modelName)
        {
            const string prefix = "models/";
            if (String.IsNullOrWhiteSpace(modelName)) return modelName;
            return modelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? modelName.Substring(prefix.Length)
                : modelName;
        }

        private static bool IsListModelsRequest(RequestTypeEnum requestType)
        {
            return requestType == RequestTypeEnum.OpenAIListModels
                || requestType == RequestTypeEnum.GeminiListModels
                || requestType == RequestTypeEnum.OllamaListTags
                || requestType == RequestTypeEnum.OllamaListRunningModels;
        }
    }
}
