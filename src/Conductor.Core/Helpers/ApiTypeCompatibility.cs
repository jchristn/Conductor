namespace Conductor.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Conductor.Core.Enums;

    /// <summary>
    /// The explicit virtual model runner to endpoint API type compatibility table. A virtual model runner's API type is the
    /// API that clients call; each client-facing type accepts a fixed set of endpoint types. Nothing is inferred from
    /// hostnames or URLs. All members are static, side-effect free and thread safe.
    /// </summary>
    public static class ApiTypeCompatibility
    {
        private static readonly Dictionary<ApiTypeEnum, ApiTypeEnum[]> _Table = new Dictionary<ApiTypeEnum, ApiTypeEnum[]>
        {
            { ApiTypeEnum.Ollama, new[] { ApiTypeEnum.Ollama, ApiTypeEnum.OllamaCloud } },
            { ApiTypeEnum.OpenAI, new[] { ApiTypeEnum.OpenAI, ApiTypeEnum.OpenAICompatible, ApiTypeEnum.vLLM } },
            { ApiTypeEnum.vLLM, new[] { ApiTypeEnum.vLLM } },
            { ApiTypeEnum.Gemini, new[] { ApiTypeEnum.Gemini } },
            { ApiTypeEnum.Anthropic, new[] { ApiTypeEnum.Anthropic } },
            { ApiTypeEnum.TypeSafe, new[] { ApiTypeEnum.TypeSafe, ApiTypeEnum.Ollaya } }
        };

        /// <summary>
        /// Determine whether an API type can be used as a virtual model runner type.
        /// Ollaya, OllamaCloud and OpenAICompatible are endpoint-only types and return false.
        /// </summary>
        /// <param name="vmrApiType">Candidate virtual model runner API type.</param>
        /// <returns>True when the type is client-facing.</returns>
        public static bool IsClientFacing(ApiTypeEnum vmrApiType)
        {
            return _Table.ContainsKey(vmrApiType);
        }

        /// <summary>
        /// Determine whether a virtual model runner of the given type accepts an endpoint of the given type.
        /// </summary>
        /// <param name="vmrApiType">Virtual model runner API type. A non-client-facing type accepts nothing.</param>
        /// <param name="endpointApiType">Endpoint API type.</param>
        /// <returns>True when the compatibility table lists the endpoint type for the virtual model runner type.</returns>
        public static bool AcceptsEndpoint(ApiTypeEnum vmrApiType, ApiTypeEnum endpointApiType)
        {
            if (!_Table.TryGetValue(vmrApiType, out ApiTypeEnum[] accepted)) return false;
            return accepted.Contains(endpointApiType);
        }

        /// <summary>
        /// Get the endpoint API types accepted by a virtual model runner type, in table order.
        /// </summary>
        /// <param name="vmrApiType">Virtual model runner API type.</param>
        /// <returns>A new list; empty when the type is not client-facing. Never null.</returns>
        public static List<ApiTypeEnum> GetAcceptedEndpointTypes(ApiTypeEnum vmrApiType)
        {
            if (!_Table.TryGetValue(vmrApiType, out ApiTypeEnum[] accepted)) return new List<ApiTypeEnum>();
            return accepted.ToList();
        }

        /// <summary>
        /// Get all client-facing API types, in enum order.
        /// </summary>
        /// <returns>A new list. Never null.</returns>
        public static List<ApiTypeEnum> GetClientFacingTypes()
        {
            return Enum.GetValues(typeof(ApiTypeEnum)).Cast<ApiTypeEnum>().Where(IsClientFacing).ToList();
        }

        /// <summary>
        /// Determine whether an endpoint API type speaks the Ollama wire API (/api/*).
        /// </summary>
        /// <param name="apiType">Endpoint API type.</param>
        /// <returns>True for Ollama and OllamaCloud.</returns>
        public static bool IsOllamaWire(ApiTypeEnum apiType)
        {
            return apiType == ApiTypeEnum.Ollama || apiType == ApiTypeEnum.OllamaCloud;
        }

        /// <summary>
        /// Determine whether an endpoint API type speaks the OpenAI wire API (/v1/*).
        /// </summary>
        /// <param name="apiType">Endpoint API type.</param>
        /// <returns>True for OpenAI, OpenAICompatible and vLLM.</returns>
        public static bool IsOpenAIWire(ApiTypeEnum apiType)
        {
            return apiType == ApiTypeEnum.OpenAI || apiType == ApiTypeEnum.OpenAICompatible || apiType == ApiTypeEnum.vLLM;
        }

        /// <summary>
        /// Determine whether an endpoint API type speaks the TypeSafe System One wire API (/v1/systemone).
        /// </summary>
        /// <param name="apiType">Endpoint API type.</param>
        /// <returns>True for TypeSafe and Ollaya.</returns>
        public static bool IsSystemOneWire(ApiTypeEnum apiType)
        {
            return apiType == ApiTypeEnum.TypeSafe || apiType == ApiTypeEnum.Ollaya;
        }

        /// <summary>
        /// Determine whether an endpoint API type is a hosted (remote, usually billable) provider for which model load
        /// probes report VerifiedRemote rather than Verified or Loaded.
        /// </summary>
        /// <param name="apiType">Endpoint API type.</param>
        /// <returns>True for OpenAI, Gemini, Anthropic, TypeSafe and OllamaCloud.</returns>
        public static bool IsHostedProvider(ApiTypeEnum apiType)
        {
            return apiType == ApiTypeEnum.OpenAI
                || apiType == ApiTypeEnum.Gemini
                || apiType == ApiTypeEnum.Anthropic
                || apiType == ApiTypeEnum.TypeSafe
                || apiType == ApiTypeEnum.OllamaCloud;
        }
    }
}
