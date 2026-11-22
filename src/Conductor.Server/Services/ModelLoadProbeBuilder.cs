namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Requests;
    using Conductor.Core.Serialization;
    using Conductor.Core.Settings;

    /// <summary>
    /// Builds provider-specific model load probe plans.
    /// </summary>
    public class ModelLoadProbeBuilder
    {
        private readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// TypeSafe settings used for the decision probe. Null resets to defaults. Default: a new TypeSafeSettings instance.
        /// </summary>
        public TypeSafeSettings TypeSafeSettings
        {
            get => _TypeSafeSettings;
            set => _TypeSafeSettings = value ?? new TypeSafeSettings();
        }

        private TypeSafeSettings _TypeSafeSettings = new TypeSafeSettings();

        /// <summary>
        /// Build a provider-specific model load probe plan.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="request">Model load request.</param>
        /// <param name="model">Effective model name.</param>
        /// <returns>Provider request plan.</returns>
        /// <exception cref="ArgumentNullException">Thrown when endpoint, request or model is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the endpoint API type is not supported by model loading.</exception>
        /// <exception cref="NotSupportedException">Thrown when the requested probe kind is not supported by the endpoint API
        /// type (for example Embeddings on Anthropic); model loading reports UnsupportedApiType.</exception>
        public ModelLoadProbePlan Build(ModelRunnerEndpoint endpoint, ModelLoadRequest request, string model)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (String.IsNullOrWhiteSpace(model)) throw new ArgumentNullException(nameof(model));

            if (request.ProbeKind == ModelLoadProbeKindEnum.Decision
                && endpoint.ApiType != ApiTypeEnum.TypeSafe
                && endpoint.ApiType != ApiTypeEnum.Ollaya)
            {
                throw new NotSupportedException("The Decision probe applies only to TypeSafe and Ollaya endpoints.");
            }

            switch (endpoint.ApiType)
            {
                case ApiTypeEnum.Ollama:
                    return BuildOllama(request, model, true);
                case ApiTypeEnum.OllamaCloud:
                    return BuildOllama(request, model, false);
                case ApiTypeEnum.OpenAI:
                    return BuildOpenAICompatible(request, model, "OpenAI");
                case ApiTypeEnum.OpenAICompatible:
                    return BuildOpenAICompatible(request, model, "OpenAICompatible");
                case ApiTypeEnum.vLLM:
                    return BuildOpenAICompatible(request, model, "vLLM");
                case ApiTypeEnum.Gemini:
                    return BuildGemini(request, model);
                case ApiTypeEnum.Anthropic:
                    return BuildAnthropic(request, model);
                case ApiTypeEnum.TypeSafe:
                case ApiTypeEnum.Ollaya:
                    return BuildSystemOne(endpoint.ApiType, request, model);
                default:
                    throw new ArgumentException("Unsupported API type: " + endpoint.ApiType, nameof(endpoint));
            }
        }

        /// <summary>
        /// Build the one-question decision probe body for a model, matching PolyPrompt's TypeSafe connectivity probe.
        /// </summary>
        /// <param name="model">Decision model. Null or empty uses TypeSafeSettings.DefaultModel.</param>
        /// <returns>JSON body. Never null.</returns>
        public string BuildDecisionProbeBody(string model)
        {
            Dictionary<string, object> question = new Dictionary<string, object>
            {
                { "type", "noul" },
                { "instructions", "Is this a test message?" }
            };

            Dictionary<string, object> body = new Dictionary<string, object>
            {
                { "model", String.IsNullOrWhiteSpace(model) ? _TypeSafeSettings.DefaultModel : model },
                { "state", "ping" },
                { "questions", new Dictionary<string, object> { { "reachable", question } } }
            };

            return _Serializer.SerializeJson(body, false);
        }

        /// <summary>
        /// Build a metadata verification plan for the endpoint API type.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <returns>Provider request plan.</returns>
        /// <exception cref="ArgumentNullException">Thrown when endpoint is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the endpoint API type is not supported by model loading.</exception>
        /// <exception cref="NotSupportedException">Thrown for hosted TypeSafe, which has no model metadata API.</exception>
        public ModelLoadProbePlan BuildMetadataPlan(ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            switch (endpoint.ApiType)
            {
                case ApiTypeEnum.OllamaCloud:
                    return new ModelLoadProbePlan
                    {
                        Method = "GET",
                        Path = "/api/tags",
                        Mechanism = "OllamaCloudListTags",
                        EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                        MetadataOnly = true
                    };
                case ApiTypeEnum.OpenAICompatible:
                    return new ModelLoadProbePlan
                    {
                        Method = "GET",
                        Path = "/v1/models",
                        Mechanism = "OpenAICompatibleListModels",
                        EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                        MetadataOnly = true
                    };
                case ApiTypeEnum.Anthropic:
                    return new ModelLoadProbePlan
                    {
                        Method = "GET",
                        Path = "/v1/models",
                        Mechanism = "AnthropicListModels",
                        EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                        MetadataOnly = true
                    };
                case ApiTypeEnum.Ollaya:
                    return new ModelLoadProbePlan
                    {
                        Method = "GET",
                        Path = "/v1/models",
                        Mechanism = "OllayaListModels",
                        EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                        MetadataOnly = true
                    };
                case ApiTypeEnum.TypeSafe:
                    throw new NotSupportedException("Hosted TypeSafe has no model metadata API; use the Decision probe.");
                case ApiTypeEnum.Ollama:
                    return new ModelLoadProbePlan
                    {
                        Method = "GET",
                        Path = "/api/tags",
                        Mechanism = "OllamaListTags",
                        EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                        MetadataOnly = true
                    };
                case ApiTypeEnum.OpenAI:
                    return new ModelLoadProbePlan
                    {
                        Method = "GET",
                        Path = "/v1/models",
                        Mechanism = "OpenAIListModels",
                        EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                        MetadataOnly = true
                    };
                case ApiTypeEnum.vLLM:
                    return new ModelLoadProbePlan
                    {
                        Method = "GET",
                        Path = "/v1/models",
                        Mechanism = "vLLMListModels",
                        EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                        MetadataOnly = true
                    };
                case ApiTypeEnum.Gemini:
                    return new ModelLoadProbePlan
                    {
                        Method = "GET",
                        Path = "/v1beta/models",
                        Mechanism = "GeminiListModels",
                        EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                        MetadataOnly = true
                    };
                default:
                    throw new ArgumentException("Unsupported API type: " + endpoint.ApiType, nameof(endpoint));
            }
        }

        /// <summary>
        /// Build an Ollama resident-model verification plan.
        /// </summary>
        /// <returns>Provider request plan.</returns>
        public ModelLoadProbePlan BuildOllamaRunningModelsPlan()
        {
            return new ModelLoadProbePlan
            {
                Method = "GET",
                Path = "/api/ps",
                Mechanism = "OllamaRunningModels",
                EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                MetadataOnly = true
            };
        }

        private ModelLoadProbePlan BuildOllama(ModelLoadRequest request, string model, bool hostLocal)
        {
            ModelLoadProbePlan plan = BuildOllamaCore(request, model);
            if (!hostLocal)
            {
                plan.HostLocalLoadSupported = false;
                plan.Mechanism = plan.Mechanism.Replace("Ollama", "OllamaCloud");
                if (plan.IgnoredFields == null) plan.IgnoredFields = new List<string>();
                if (!String.IsNullOrWhiteSpace(request.KeepAlive) && !plan.IgnoredFields.Contains("KeepAlive"))
                {
                    plan.IgnoredFields.Add("KeepAlive");
                }
            }

            return plan;
        }

        private ModelLoadProbePlan BuildAnthropic(ModelLoadRequest request, string model)
        {
            List<string> ignoredFields = new List<string>();
            if (!String.IsNullOrWhiteSpace(request.KeepAlive)) ignoredFields.Add("KeepAlive");

            if (request.ProbeKind == ModelLoadProbeKindEnum.Auto || request.ProbeKind == ModelLoadProbeKindEnum.MetadataOnly)
            {
                return new ModelLoadProbePlan
                {
                    Method = "GET",
                    Path = "/v1/models/" + Uri.EscapeDataString(model),
                    Mechanism = "AnthropicGetModel",
                    EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                    MetadataOnly = true,
                    IgnoredFields = ignoredFields
                };
            }

            if (request.ProbeKind == ModelLoadProbeKindEnum.ChatCompletion)
            {
                Dictionary<string, object> body = new Dictionary<string, object>
                {
                    { "model", model },
                    { "max_tokens", 1 },
                    { "messages", new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                { "role", "user" },
                                { "content", request.InputText }
                            }
                        }
                    }
                };

                return new ModelLoadProbePlan
                {
                    Method = "POST",
                    Path = "/v1/messages",
                    BodyJson = _Serializer.SerializeJson(body, false),
                    Mechanism = "AnthropicMessages",
                    EffectiveProbeKind = ModelLoadProbeKindEnum.ChatCompletion,
                    ExplicitLoad = true,
                    IgnoredFields = ignoredFields
                };
            }

            throw new NotSupportedException("Probe kind " + request.ProbeKind + " is not supported for Anthropic endpoints; use Auto, MetadataOnly or ChatCompletion.");
        }

        private ModelLoadProbePlan BuildSystemOne(ApiTypeEnum apiType, ModelLoadRequest request, string model)
        {
            List<string> ignoredFields = new List<string>();
            if (!String.IsNullOrWhiteSpace(request.KeepAlive)) ignoredFields.Add("KeepAlive");

            bool isOllaya = apiType == ApiTypeEnum.Ollaya;
            if (isOllaya && (request.ProbeKind == ModelLoadProbeKindEnum.Auto || request.ProbeKind == ModelLoadProbeKindEnum.MetadataOnly))
            {
                return new ModelLoadProbePlan
                {
                    Method = "GET",
                    Path = "/v1/models",
                    Mechanism = "OllayaListModels",
                    EffectiveProbeKind = ModelLoadProbeKindEnum.MetadataOnly,
                    MetadataOnly = true,
                    IgnoredFields = ignoredFields
                };
            }

            if (request.ProbeKind == ModelLoadProbeKindEnum.Auto || request.ProbeKind == ModelLoadProbeKindEnum.Decision)
            {
                return new ModelLoadProbePlan
                {
                    Method = "POST",
                    Path = "/v1/systemone",
                    BodyJson = BuildDecisionProbeBody(model),
                    Mechanism = isOllaya ? "OllayaDecision" : "TypeSafeDecision",
                    EffectiveProbeKind = ModelLoadProbeKindEnum.Decision,
                    ExplicitLoad = true,
                    IgnoredFields = ignoredFields
                };
            }

            throw new NotSupportedException("Probe kind " + request.ProbeKind + " is not supported for " + apiType + " endpoints; use "
                + (isOllaya ? "Auto, MetadataOnly or Decision." : "Auto or Decision."));
        }

        private ModelLoadProbePlan BuildOllamaCore(ModelLoadRequest request, string model)
        {
            ModelLoadProbeKindEnum effectiveProbe = request.ProbeKind == ModelLoadProbeKindEnum.Auto
                ? ModelLoadProbeKindEnum.NativeGenerate
                : request.ProbeKind;

            if (effectiveProbe == ModelLoadProbeKindEnum.MetadataOnly)
            {
                return new ModelLoadProbePlan
                {
                    Method = "GET",
                    Path = "/api/tags",
                    Mechanism = "OllamaListTags",
                    EffectiveProbeKind = effectiveProbe,
                    MetadataOnly = true,
                    HostLocalLoadSupported = true
                };
            }

            if (effectiveProbe == ModelLoadProbeKindEnum.Embeddings)
            {
                Dictionary<string, object> body = new Dictionary<string, object>
                {
                    { "model", model },
                    { "input", request.InputText }
                };
                if (!String.IsNullOrWhiteSpace(request.KeepAlive)) body["keep_alive"] = request.KeepAlive;

                return new ModelLoadProbePlan
                {
                    Method = "POST",
                    Path = "/api/embed",
                    BodyJson = _Serializer.SerializeJson(body, false),
                    Mechanism = "OllamaEmbeddings",
                    EffectiveProbeKind = effectiveProbe,
                    ExplicitLoad = true,
                    HostLocalLoadSupported = true
                };
            }

            if (effectiveProbe == ModelLoadProbeKindEnum.ChatCompletion)
            {
                Dictionary<string, object> body = new Dictionary<string, object>
                {
                    { "model", model },
                    { "messages", new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                { "role", "user" },
                                { "content", request.InputText }
                            }
                        }
                    },
                    { "stream", false },
                    { "options", new Dictionary<string, object> { { "num_predict", 1 } } }
                };
                if (!String.IsNullOrWhiteSpace(request.KeepAlive)) body["keep_alive"] = request.KeepAlive;

                return new ModelLoadProbePlan
                {
                    Method = "POST",
                    Path = "/api/chat",
                    BodyJson = _Serializer.SerializeJson(body, false),
                    Mechanism = "OllamaChat",
                    EffectiveProbeKind = effectiveProbe,
                    ExplicitLoad = true,
                    HostLocalLoadSupported = true
                };
            }

            Dictionary<string, object> generateBody = new Dictionary<string, object>
            {
                { "model", model },
                { "prompt", request.InputText },
                { "stream", false },
                { "options", new Dictionary<string, object> { { "num_predict", 1 } } }
            };
            if (!String.IsNullOrWhiteSpace(request.KeepAlive)) generateBody["keep_alive"] = request.KeepAlive;

            return new ModelLoadProbePlan
            {
                Method = "POST",
                Path = "/api/generate",
                BodyJson = _Serializer.SerializeJson(generateBody, false),
                Mechanism = "OllamaGenerate",
                EffectiveProbeKind = ModelLoadProbeKindEnum.NativeGenerate,
                ExplicitLoad = true,
                HostLocalLoadSupported = true
            };
        }

        private ModelLoadProbePlan BuildOpenAICompatible(ModelLoadRequest request, string model, string mechanismPrefix)
        {
            ModelLoadProbeKindEnum effectiveProbe = request.ProbeKind == ModelLoadProbeKindEnum.Auto
                ? ModelLoadProbeKindEnum.MetadataOnly
                : request.ProbeKind;

            List<string> ignoredFields = new List<string>();
            if (!String.IsNullOrWhiteSpace(request.KeepAlive)) ignoredFields.Add("KeepAlive");

            if (effectiveProbe == ModelLoadProbeKindEnum.MetadataOnly)
            {
                return new ModelLoadProbePlan
                {
                    Method = "GET",
                    Path = "/v1/models",
                    Mechanism = mechanismPrefix + "ListModels",
                    EffectiveProbeKind = effectiveProbe,
                    MetadataOnly = true,
                    IgnoredFields = ignoredFields
                };
            }

            if (effectiveProbe == ModelLoadProbeKindEnum.Embeddings)
            {
                Dictionary<string, object> body = new Dictionary<string, object>
                {
                    { "model", model },
                    { "input", request.InputText }
                };

                return new ModelLoadProbePlan
                {
                    Method = "POST",
                    Path = "/v1/embeddings",
                    BodyJson = _Serializer.SerializeJson(body, false),
                    Mechanism = mechanismPrefix + "Embeddings",
                    EffectiveProbeKind = effectiveProbe,
                    ExplicitLoad = true,
                    IgnoredFields = ignoredFields
                };
            }

            if (effectiveProbe == ModelLoadProbeKindEnum.Completion)
            {
                Dictionary<string, object> body = new Dictionary<string, object>
                {
                    { "model", model },
                    { "prompt", request.InputText },
                    { "max_tokens", 1 },
                    { "stream", false }
                };

                return new ModelLoadProbePlan
                {
                    Method = "POST",
                    Path = "/v1/completions",
                    BodyJson = _Serializer.SerializeJson(body, false),
                    Mechanism = mechanismPrefix + "Completion",
                    EffectiveProbeKind = effectiveProbe,
                    ExplicitLoad = true,
                    IgnoredFields = ignoredFields
                };
            }

            Dictionary<string, object> chatBody = new Dictionary<string, object>
            {
                { "model", model },
                { "messages", new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            { "role", "user" },
                            { "content", request.InputText }
                        }
                    }
                },
                { "max_tokens", 1 },
                { "stream", false }
            };

            return new ModelLoadProbePlan
            {
                Method = "POST",
                Path = "/v1/chat/completions",
                BodyJson = _Serializer.SerializeJson(chatBody, false),
                Mechanism = mechanismPrefix + "ChatCompletion",
                EffectiveProbeKind = ModelLoadProbeKindEnum.ChatCompletion,
                ExplicitLoad = true,
                IgnoredFields = ignoredFields
            };
        }

        private ModelLoadProbePlan BuildGemini(ModelLoadRequest request, string model)
        {
            ModelLoadProbeKindEnum effectiveProbe = request.ProbeKind == ModelLoadProbeKindEnum.Auto
                ? ModelLoadProbeKindEnum.MetadataOnly
                : request.ProbeKind;

            List<string> ignoredFields = new List<string>();
            if (!String.IsNullOrWhiteSpace(request.KeepAlive)) ignoredFields.Add("KeepAlive");

            if (effectiveProbe == ModelLoadProbeKindEnum.MetadataOnly)
            {
                return new ModelLoadProbePlan
                {
                    Method = "GET",
                    Path = "/v1beta/models",
                    Mechanism = "GeminiListModels",
                    EffectiveProbeKind = effectiveProbe,
                    MetadataOnly = true,
                    IgnoredFields = ignoredFields
                };
            }

            string normalizedModel = NormalizeGeminiModel(model);
            if (effectiveProbe == ModelLoadProbeKindEnum.Embeddings)
            {
                Dictionary<string, object> body = new Dictionary<string, object>
                {
                    { "content", new Dictionary<string, object>
                        {
                            { "parts", new List<Dictionary<string, object>>
                                {
                                    new Dictionary<string, object> { { "text", request.InputText } }
                                }
                            }
                        }
                    }
                };

                return new ModelLoadProbePlan
                {
                    Method = "POST",
                    Path = "/v1beta/" + normalizedModel + ":embedContent",
                    BodyJson = _Serializer.SerializeJson(body, false),
                    Mechanism = "GeminiEmbedContent",
                    EffectiveProbeKind = effectiveProbe,
                    ExplicitLoad = true,
                    IgnoredFields = ignoredFields
                };
            }

            Dictionary<string, object> generateBody = new Dictionary<string, object>
            {
                { "contents", new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            { "parts", new List<Dictionary<string, object>>
                                {
                                    new Dictionary<string, object> { { "text", request.InputText } }
                                }
                            }
                        }
                    }
                },
                { "generationConfig", new Dictionary<string, object> { { "maxOutputTokens", 1 } } }
            };

            return new ModelLoadProbePlan
            {
                Method = "POST",
                Path = "/v1beta/" + normalizedModel + ":generateContent",
                BodyJson = _Serializer.SerializeJson(generateBody, false),
                Mechanism = "GeminiGenerateContent",
                EffectiveProbeKind = ModelLoadProbeKindEnum.ChatCompletion,
                ExplicitLoad = true,
                IgnoredFields = ignoredFields
            };
        }

        private static string NormalizeGeminiModel(string model)
        {
            if (String.IsNullOrWhiteSpace(model)) return "models/";
            if (model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)) return model;
            return "models/" + Uri.EscapeDataString(model);
        }
    }
}
