namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Requests;
    using Conductor.Core.Serialization;

    /// <summary>
    /// Builds provider-specific model load probe plans.
    /// </summary>
    public class ModelLoadProbeBuilder
    {
        private readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Build a provider-specific model load probe plan.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="request">Model load request.</param>
        /// <param name="model">Effective model name.</param>
        /// <returns>Provider request plan.</returns>
        public ModelLoadProbePlan Build(ModelRunnerEndpoint endpoint, ModelLoadRequest request, string model)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (String.IsNullOrWhiteSpace(model)) throw new ArgumentNullException(nameof(model));

            switch (endpoint.ApiType)
            {
                case ApiTypeEnum.Ollama:
                    return BuildOllama(request, model);
                case ApiTypeEnum.OpenAI:
                    return BuildOpenAICompatible(request, model, false);
                case ApiTypeEnum.vLLM:
                    return BuildOpenAICompatible(request, model, true);
                case ApiTypeEnum.Gemini:
                    return BuildGemini(request, model);
                default:
                    throw new ArgumentException("Unsupported API type: " + endpoint.ApiType, nameof(endpoint));
            }
        }

        /// <summary>
        /// Build a metadata verification plan for the endpoint API type.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <returns>Provider request plan.</returns>
        public ModelLoadProbePlan BuildMetadataPlan(ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            switch (endpoint.ApiType)
            {
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

        private ModelLoadProbePlan BuildOllama(ModelLoadRequest request, string model)
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

        private ModelLoadProbePlan BuildOpenAICompatible(ModelLoadRequest request, string model, bool isVllm)
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
                    Mechanism = isVllm ? "vLLMListModels" : "OpenAIListModels",
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
                    Mechanism = isVllm ? "vLLMEmbeddings" : "OpenAIEmbeddings",
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
                    Mechanism = isVllm ? "vLLMCompletion" : "OpenAICompletion",
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
                Mechanism = isVllm ? "vLLMChatCompletion" : "OpenAIChatCompletion",
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
