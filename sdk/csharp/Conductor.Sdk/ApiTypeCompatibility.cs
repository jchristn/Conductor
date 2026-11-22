namespace Conductor.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Client-side copy of the server's virtual model runner to endpoint compatibility table.
    /// All members are static, stateless, and thread safe.
    /// </summary>
    public static class ApiTypeCompatibility
    {
        private static readonly IReadOnlyDictionary<ApiType, IReadOnlyList<ApiType>> _Accepted = new Dictionary<ApiType, IReadOnlyList<ApiType>>
        {
            [ApiType.Ollama] = new ApiType[] { ApiType.Ollama, ApiType.OllamaCloud },
            [ApiType.OpenAI] = new ApiType[] { ApiType.OpenAI, ApiType.OpenAICompatible, ApiType.vLLM },
            [ApiType.vLLM] = new ApiType[] { ApiType.vLLM },
            [ApiType.Gemini] = new ApiType[] { ApiType.Gemini },
            [ApiType.Anthropic] = new ApiType[] { ApiType.Anthropic },
            [ApiType.TypeSafe] = new ApiType[] { ApiType.TypeSafe, ApiType.Ollaya }
        };

        /// <summary>
        /// API types that may be used as a virtual model runner type, in enum order.
        /// Never null.
        /// </summary>
        public static IReadOnlyList<ApiType> ClientFacingTypes { get; } = _Accepted.Keys.OrderBy(t => (int)t).ToArray();

        /// <summary>
        /// Determine whether an API type may be used as a virtual model runner type.
        /// </summary>
        /// <param name="apiType">API type.</param>
        /// <returns>True for Ollama, OpenAI, Gemini, vLLM, Anthropic, and TypeSafe; false otherwise.</returns>
        public static bool IsClientFacing(ApiType apiType)
        {
            return _Accepted.ContainsKey(apiType);
        }

        /// <summary>
        /// Get the endpoint API types a virtual model runner of the given type accepts.
        /// </summary>
        /// <param name="vmrApiType">Virtual model runner API type.</param>
        /// <returns>Accepted endpoint types. Empty (never null) when the type is not client facing.</returns>
        public static IReadOnlyList<ApiType> GetAcceptedEndpointTypes(ApiType vmrApiType)
        {
            if (_Accepted.TryGetValue(vmrApiType, out IReadOnlyList<ApiType> accepted))
            {
                return accepted;
            }

            return Array.Empty<ApiType>();
        }

        /// <summary>
        /// Determine whether a virtual model runner of one type accepts an endpoint of another type.
        /// </summary>
        /// <param name="vmrApiType">Virtual model runner API type.</param>
        /// <param name="endpointApiType">Endpoint API type.</param>
        /// <returns>True when the server would accept the endpoint on the virtual model runner.</returns>
        public static bool AcceptsEndpoint(ApiType vmrApiType, ApiType endpointApiType)
        {
            return GetAcceptedEndpointTypes(vmrApiType).Contains(endpointApiType);
        }
    }
}
