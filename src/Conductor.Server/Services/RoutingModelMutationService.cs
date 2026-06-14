namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using JsonMerge;

    internal sealed class RoutingModelMutationService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly Serializer _Serializer = new Serializer();

        internal RoutingModelMutationService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        internal async Task<RoutingModelMutationResult> ApplyAsync(
            VirtualModelRunner vmr,
            UrlContext urlContext,
            byte[] requestBody,
            CancellationToken token)
        {
            RoutingModelMutationResult result = await ResolveAsync(vmr, urlContext, requestBody, token).ConfigureAwait(false);
            if (!result.Success) return result;
            if (!urlContext.IsCompletionsRequest && !urlContext.IsEmbeddingsRequest) return result;

            if (requestBody == null || requestBody.Length < 1)
            {
                return result;
            }

            string bodyJson = Encoding.UTF8.GetString(requestBody);
            Dictionary<string, object> bodyDict = _Serializer.DeserializeJson<Dictionary<string, object>>(bodyJson);
            if (bodyDict == null)
            {
                return result;
            }

            if (result.ModelDefinition != null && !String.IsNullOrWhiteSpace(result.EffectiveModel))
            {
                ApplyModelName(bodyDict, urlContext, result.EffectiveModel);
                AddMutation(result.MutationSummary, "model", result.RequestedModel, result.EffectiveModel, "ModelDefinition:" + result.ModelDefinition.Name);
            }

            result.MutationSummary.EffectiveModel = result.EffectiveModel;
            result.MutationSummary.ModelDefinitionId = result.ModelDefinition?.Id;

            if (urlContext.IsCompletionsRequest && result.ModelConfiguration != null)
            {
                ApplyConfigurationToBody(bodyDict, result.ModelConfiguration, urlContext, result.MutationSummary);
            }

            if (result.ModelConfiguration != null)
            {
                ApplyPinnedProperties(bodyDict, result.ModelConfiguration, urlContext, result.MutationSummary);
            }

            string mutatedJson = _Serializer.SerializeJson(bodyDict, false);
            if (urlContext.ApiType == ApiTypeEnum.Gemini)
            {
                mutatedJson = SanitizeGeminiJson(mutatedJson);
            }

            result.RequestBody = Encoding.UTF8.GetBytes(mutatedJson);
            return result;
        }

        internal async Task<RoutingModelMutationResult> ResolveAsync(
            VirtualModelRunner vmr,
            UrlContext urlContext,
            byte[] requestBody,
            CancellationToken token)
        {
            RoutingModelMutationResult result = new RoutingModelMutationResult
            {
                Success = true,
                HttpStatusCode = 200,
                OutcomeCode = "Routed",
                RequestBody = requestBody
            };

            if (!CanResolveRequestModel(urlContext))
            {
                return result;
            }

            Dictionary<string, object> bodyDict = null;
            if (requestBody != null && requestBody.Length > 0)
            {
                string bodyJson = Encoding.UTF8.GetString(requestBody);
                bodyDict = _Serializer.DeserializeJson<Dictionary<string, object>>(bodyJson);
            }

            result.RequestedModel = ExtractRequestedModel(urlContext, bodyDict);
            result.MutationSummary.RequestedModel = result.RequestedModel;
            result.EffectiveModel = result.RequestedModel;
            result.MutationSummary.EffectiveModel = result.EffectiveModel;

            bool routeModelDefinitionEnforced = urlContext.IsCompletionsRequest || urlContext.IsEmbeddingsRequest;
            List<ModelDefinition> modelDefinitions = await ResolveActiveModelDefinitionsAsync(vmr, token).ConfigureAwait(false);

            if (routeModelDefinitionEnforced)
            {
                if (vmr.StrictMode)
                {
                    if (modelDefinitions.Count < 1)
                    {
                        return DenyMutation(result, 401, "StrictModeNoModels", "Strict mode is enabled but no active model definitions are attached.");
                    }

                    if (String.IsNullOrWhiteSpace(result.RequestedModel))
                    {
                        return DenyMutation(result, 401, "StrictModeModelRequired", "Strict mode requires the request to specify a model.");
                    }

                    if (!modelDefinitions.Exists(item => String.Equals(item.Name, result.RequestedModel, StringComparison.OrdinalIgnoreCase)))
                    {
                        return DenyMutation(result, 401, "StrictModeModelRejected", "The requested model is not attached to this virtual model runner.");
                    }
                }

                if (modelDefinitions.Count == 1)
                {
                    result.ModelDefinition = modelDefinitions[0];
                    result.EffectiveModel = modelDefinitions[0].Name;
                }
                else if (modelDefinitions.Count > 1 && !String.IsNullOrWhiteSpace(result.RequestedModel))
                {
                    ModelDefinition matchedDefinition = modelDefinitions.Find(item => String.Equals(item.Name, result.RequestedModel, StringComparison.OrdinalIgnoreCase));
                    if (matchedDefinition == null)
                    {
                        return DenyMutation(result, 401, "ModelNotAttached", "The requested model is not attached to this virtual model runner.");
                    }

                    result.ModelDefinition = matchedDefinition;
                    result.EffectiveModel = matchedDefinition.Name;
                }

                List<ModelConfiguration> configurations = await ResolveActiveModelConfigurationsAsync(vmr, token).ConfigureAwait(false);
                result.ModelConfiguration = SelectConfiguration(vmr, configurations, result.EffectiveModel);
                result.MutationSummary.ModelConfigurationId = result.ModelConfiguration?.Id;
            }
            else if (!String.IsNullOrWhiteSpace(result.RequestedModel))
            {
                ModelDefinition matchedDefinition = modelDefinitions.Find(item => String.Equals(item.Name, result.RequestedModel, StringComparison.OrdinalIgnoreCase));
                if (matchedDefinition != null)
                {
                    result.ModelDefinition = matchedDefinition;
                    result.EffectiveModel = matchedDefinition.Name;
                }
            }

            result.MutationSummary.EffectiveModel = result.EffectiveModel;
            result.MutationSummary.ModelDefinitionId = result.ModelDefinition?.Id;
            return result;
        }

        private async Task<List<ModelDefinition>> ResolveActiveModelDefinitionsAsync(VirtualModelRunner vmr, CancellationToken token)
        {
            List<ModelDefinition> modelDefinitions = new List<ModelDefinition>();
            foreach (string definitionId in vmr.ModelDefinitionIds ?? new List<string>())
            {
                ModelDefinition definition = await _Database.ModelDefinition.ReadAsync(vmr.TenantId, definitionId, token).ConfigureAwait(false);
                if (definition != null && definition.Active)
                {
                    modelDefinitions.Add(definition);
                }
            }

            return modelDefinitions;
        }

        private async Task<List<ModelConfiguration>> ResolveActiveModelConfigurationsAsync(VirtualModelRunner vmr, CancellationToken token)
        {
            List<ModelConfiguration> configurations = new List<ModelConfiguration>();
            foreach (string configurationId in vmr.ModelConfigurationIds ?? new List<string>())
            {
                ModelConfiguration configuration = await _Database.ModelConfiguration.ReadAsync(vmr.TenantId, configurationId, token).ConfigureAwait(false);
                if (configuration != null && configuration.Active)
                {
                    configurations.Add(configuration);
                }
            }

            return configurations;
        }

        private static ModelConfiguration SelectConfiguration(VirtualModelRunner vmr, List<ModelConfiguration> configurations, string effectiveModel)
        {
            if (configurations == null || configurations.Count < 1) return null;

            if (!String.IsNullOrWhiteSpace(effectiveModel)
                && vmr.ModelConfigurationMappings != null
                && vmr.ModelConfigurationMappings.TryGetValue(effectiveModel, out string mappedConfigurationId))
            {
                ModelConfiguration mappedConfiguration = configurations.Find(item => String.Equals(item.Id, mappedConfigurationId, StringComparison.Ordinal));
                if (mappedConfiguration != null)
                {
                    return mappedConfiguration;
                }
            }

            if (configurations.Count == 1)
            {
                ModelConfiguration singleConfiguration = configurations[0];
                if (String.IsNullOrWhiteSpace(singleConfiguration.Model)
                    || String.Equals(singleConfiguration.Model, effectiveModel, StringComparison.OrdinalIgnoreCase))
                {
                    return singleConfiguration;
                }
            }

            return configurations.Find(item =>
                String.IsNullOrWhiteSpace(item.Model)
                || String.Equals(item.Model, effectiveModel, StringComparison.OrdinalIgnoreCase));
        }

        private static bool CanResolveRequestModel(UrlContext urlContext)
        {
            if (urlContext == null) return false;
            if (urlContext.IsCompletionsRequest || urlContext.IsEmbeddingsRequest) return true;

            switch (urlContext.RequestType)
            {
                case RequestTypeEnum.OllamaPullModel:
                case RequestTypeEnum.OllamaDeleteModel:
                case RequestTypeEnum.OllamaShowModelInfo:
                    return true;
                default:
                    return false;
            }
        }

        private static string ExtractRequestedModel(UrlContext urlContext, Dictionary<string, object> bodyDict)
        {
            if (urlContext != null && urlContext.ApiType == ApiTypeEnum.Gemini && !String.IsNullOrWhiteSpace(urlContext.RequestedModel))
            {
                return urlContext.RequestedModel;
            }

            if (bodyDict == null) return null;
            if (bodyDict.TryGetValue("model", out object modelValue) && modelValue != null)
            {
                return modelValue.ToString();
            }

            if (bodyDict.TryGetValue("name", out object nameValue) && nameValue != null)
            {
                return nameValue.ToString();
            }

            return null;
        }

        private static void ApplyModelName(Dictionary<string, object> bodyDict, UrlContext urlContext, string effectiveModel)
        {
            if (String.IsNullOrWhiteSpace(effectiveModel) || bodyDict == null) return;

            if (urlContext.ApiType == ApiTypeEnum.Gemini)
            {
                urlContext.RelativePath = ReplaceGeminiModelInPath(urlContext.RelativePath, effectiveModel);
                urlContext.RequestedModel = effectiveModel;
            }
            else
            {
                bodyDict["model"] = effectiveModel;
            }
        }

        private static void ApplyConfigurationToBody(
            Dictionary<string, object> bodyDict,
            ModelConfiguration configuration,
            UrlContext urlContext,
            RequestMutationSummary mutationSummary)
        {
            if (configuration.Temperature.HasValue)
            {
                ApplyMutationValue(bodyDict, urlContext, mutationSummary, "temperature", configuration.Temperature.Value, "ModelConfiguration:" + configuration.Name);
            }
            if (configuration.TopP.HasValue)
            {
                ApplyMutationValue(bodyDict, urlContext, mutationSummary, "top_p", configuration.TopP.Value, "ModelConfiguration:" + configuration.Name);
            }
            if (configuration.TopK.HasValue)
            {
                ApplyMutationValue(bodyDict, urlContext, mutationSummary, "top_k", configuration.TopK.Value, "ModelConfiguration:" + configuration.Name);
            }
            if (configuration.MaxTokens.HasValue)
            {
                ApplyMutationValue(bodyDict, urlContext, mutationSummary, "max_tokens", configuration.MaxTokens.Value, "ModelConfiguration:" + configuration.Name);
            }
            if (configuration.RepeatPenalty.HasValue && urlContext.ApiType != ApiTypeEnum.Gemini)
            {
                ApplyMutationValue(bodyDict, urlContext, mutationSummary, "repeat_penalty", configuration.RepeatPenalty.Value, "ModelConfiguration:" + configuration.Name);
            }
            if (configuration.ContextWindowSize.HasValue && urlContext.ApiType != ApiTypeEnum.Gemini)
            {
                ApplyMutationValue(bodyDict, urlContext, mutationSummary, "num_ctx", configuration.ContextWindowSize.Value, "ModelConfiguration:" + configuration.Name);
            }
        }

        private static void ApplyPinnedProperties(
            Dictionary<string, object> bodyDict,
            ModelConfiguration configuration,
            UrlContext urlContext,
            RequestMutationSummary mutationSummary)
        {
            Dictionary<string, object> pinnedProperties = null;

            if (urlContext.IsEmbeddingsRequest && configuration.PinnedEmbeddingsProperties != null && configuration.PinnedEmbeddingsProperties.Count > 0)
            {
                pinnedProperties = configuration.PinnedEmbeddingsProperties;
            }
            else if (urlContext.IsCompletionsRequest && configuration.PinnedCompletionsProperties != null && configuration.PinnedCompletionsProperties.Count > 0)
            {
                pinnedProperties = configuration.PinnedCompletionsProperties;
            }

            if (pinnedProperties == null || pinnedProperties.Count < 1) return;

            string requestJson = new Serializer().SerializeJson(bodyDict, false);
            string pinnedJson = new Serializer().SerializeJson(pinnedProperties, false);
            string mergedJson = JsonMerger.MergeJson(requestJson, pinnedJson);
            Dictionary<string, object> mergedBody = new Serializer().DeserializeJson<Dictionary<string, object>>(mergedJson);
            if (mergedBody == null) return;

            foreach (KeyValuePair<string, object> kvp in pinnedProperties)
            {
                string requestedValue = TryGetExistingValue(bodyDict, kvp.Key);
                string effectiveValue = TryGetExistingValue(mergedBody, kvp.Key);
                AddMutation(mutationSummary, kvp.Key, requestedValue, effectiveValue, "PinnedProperties:" + configuration.Name);
            }

            bodyDict.Clear();
            foreach (KeyValuePair<string, object> kvp in mergedBody)
            {
                bodyDict[kvp.Key] = kvp.Value;
            }
        }

        private static void ApplyMutationValue(
            Dictionary<string, object> bodyDict,
            UrlContext urlContext,
            RequestMutationSummary mutationSummary,
            string propertyName,
            object value,
            string source)
        {
            string existingValue = TryGetExistingValue(bodyDict, propertyName);
            if (urlContext.ApiType == ApiTypeEnum.Gemini)
            {
                string geminiKey = propertyName;
                if (String.Equals(propertyName, "top_p", StringComparison.OrdinalIgnoreCase)) geminiKey = "topP";
                if (String.Equals(propertyName, "top_k", StringComparison.OrdinalIgnoreCase)) geminiKey = "topK";
                if (String.Equals(propertyName, "max_tokens", StringComparison.OrdinalIgnoreCase)) geminiKey = "maxOutputTokens";
                SetGeminiGenerationConfigValue(bodyDict, geminiKey, value);
            }
            else
            {
                bodyDict[propertyName] = value;
            }

            AddMutation(mutationSummary, propertyName, existingValue, value?.ToString(), source);
        }

        private static RoutingModelMutationResult DenyMutation(RoutingModelMutationResult result, int statusCode, string outcomeCode, string message)
        {
            result.Success = false;
            result.HttpStatusCode = statusCode;
            result.OutcomeCode = outcomeCode;
            result.Message = message;
            return result;
        }

        private static void AddMutation(RequestMutationSummary summary, string propertyName, string requestedValue, string effectiveValue, string source)
        {
            if (summary == null) return;

            if (String.Equals(requestedValue, effectiveValue, StringComparison.Ordinal))
            {
                return;
            }

            summary.HasMutations = true;
            summary.Changes.Add(new RequestMutationDetail
            {
                PropertyName = propertyName,
                RequestedValue = requestedValue,
                EffectiveValue = effectiveValue,
                Source = source
            });
        }

        private static string TryGetExistingValue(Dictionary<string, object> bodyDict, string propertyName)
        {
            if (bodyDict == null || String.IsNullOrWhiteSpace(propertyName)) return null;

            if (bodyDict.TryGetValue(propertyName, out object value) && value != null)
            {
                return value.ToString();
            }

            return null;
        }

        private static void SetGeminiGenerationConfigValue(Dictionary<string, object> bodyDict, string key, object value)
        {
            if (bodyDict == null || String.IsNullOrEmpty(key)) return;

            Dictionary<string, object> generationConfig = null;
            if (bodyDict.TryGetValue("generationConfig", out object existing))
            {
                if (existing is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                {
                    generationConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                }
                else if (existing is Dictionary<string, object> dictionary)
                {
                    generationConfig = dictionary;
                }
            }

            generationConfig ??= new Dictionary<string, object>();
            generationConfig[key] = value;
            bodyDict["generationConfig"] = generationConfig;
        }

        private string SanitizeGeminiJson(string bodyJson)
        {
            if (String.IsNullOrEmpty(bodyJson)) return bodyJson;

            try
            {
                Dictionary<string, object> bodyDict = _Serializer.DeserializeJson<Dictionary<string, object>>(bodyJson);
                if (bodyDict == null) return bodyJson;

                bodyDict.Remove("repeat_penalty");
                bodyDict.Remove("num_ctx");
                return _Serializer.SerializeJson(bodyDict, false);
            }
            catch
            {
                return bodyJson;
            }
        }

        private static string ReplaceGeminiModelInPath(string relativePath, string modelName)
        {
            if (String.IsNullOrEmpty(relativePath) || String.IsNullOrEmpty(modelName)) return relativePath;

            const string prefix = "/v1beta/models/";
            if (!relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return relativePath;
            }

            string remainder = relativePath.Substring(prefix.Length);
            int separator = remainder.IndexOfAny(new[] { ':', '/', '?' });
            if (separator < 0)
            {
                return prefix + modelName;
            }

            return prefix + modelName + remainder.Substring(separator);
        }
    }

    internal sealed class RoutingModelMutationResult
    {
        internal bool Success { get; set; }
        internal int HttpStatusCode { get; set; }
        internal string OutcomeCode { get; set; }
        internal string Message { get; set; }
        internal string RequestedModel { get; set; }
        internal string EffectiveModel { get; set; }
        internal ModelDefinition ModelDefinition { get; set; }
        internal ModelConfiguration ModelConfiguration { get; set; }
        internal byte[] RequestBody { get; set; }
        internal RequestMutationSummary MutationSummary { get; set; } = new RequestMutationSummary();
    }
}
