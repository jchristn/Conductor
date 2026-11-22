namespace Conductor.Server.Services
{
    using System;
    using Conductor.Core.Enums;

    /// <summary>
    /// Resolves the telemetry and metrics api_family label for a request type. One shared implementation is used by the
    /// proxy, routing, QoS and metrics paths. All members are static and thread safe.
    /// </summary>
    public static class ApiFamilyResolver
    {
        /// <summary>
        /// api_family value for OpenAI wire requests.
        /// </summary>
        public const string OpenAI = "OpenAI";

        /// <summary>
        /// api_family value for Gemini requests.
        /// </summary>
        public const string Gemini = "Gemini";

        /// <summary>
        /// api_family value for Ollama wire requests.
        /// </summary>
        public const string Ollama = "Ollama";

        /// <summary>
        /// api_family value for Anthropic Messages API requests.
        /// </summary>
        public const string Anthropic = "Anthropic";

        /// <summary>
        /// api_family value for TypeSafe System One requests.
        /// </summary>
        public const string TypeSafe = "TypeSafe";

        /// <summary>
        /// api_family value for unclassified and management requests.
        /// </summary>
        public const string Management = "Management";

        /// <summary>
        /// Resolve the api_family label for a request type.
        /// </summary>
        /// <param name="requestType">Request type.</param>
        /// <returns>OpenAI, Gemini, Ollama, Anthropic, TypeSafe or Management. Never null.</returns>
        public static string Resolve(RequestTypeEnum requestType)
        {
            switch (requestType)
            {
                case RequestTypeEnum.OpenAIChatCompletions:
                case RequestTypeEnum.OpenAICompletions:
                case RequestTypeEnum.OpenAIEmbeddings:
                case RequestTypeEnum.OpenAIListModels:
                    return OpenAI;
                case RequestTypeEnum.GeminiGenerateContent:
                case RequestTypeEnum.GeminiStreamGenerateContent:
                case RequestTypeEnum.GeminiEmbedContent:
                case RequestTypeEnum.GeminiListModels:
                    return Gemini;
                case RequestTypeEnum.OllamaGenerate:
                case RequestTypeEnum.OllamaChat:
                case RequestTypeEnum.OllamaEmbeddings:
                case RequestTypeEnum.OllamaListTags:
                case RequestTypeEnum.OllamaListRunningModels:
                case RequestTypeEnum.OllamaPullModel:
                case RequestTypeEnum.OllamaDeleteModel:
                case RequestTypeEnum.OllamaShowModelInfo:
                    return Ollama;
                case RequestTypeEnum.AnthropicMessages:
                case RequestTypeEnum.AnthropicCountTokens:
                case RequestTypeEnum.AnthropicListModels:
                case RequestTypeEnum.AnthropicGetModel:
                    return Anthropic;
                case RequestTypeEnum.TypeSafeDecide:
                case RequestTypeEnum.TypeSafeListModels:
                    return TypeSafe;
                default:
                    return Management;
            }
        }
    }
}
