namespace Conductor.Core.Enums
{
    using System;

    /// <summary>
    /// API type enumeration. Values are persisted as integers, so new values are appended and existing values are never
    /// renumbered. Client-facing types can be used as a virtual model runner type; the others are endpoint-only types.
    /// </summary>
    public enum ApiTypeEnum
    {
        /// <summary>
        /// Ollama API format.
        /// </summary>
        Ollama = 0,

        /// <summary>
        /// OpenAI API format.
        /// </summary>
        OpenAI = 1,

        /// <summary>
        /// Gemini API format.
        /// </summary>
        Gemini = 2,

        /// <summary>
        /// vLLM API format (OpenAI-compatible).
        /// </summary>
        vLLM = 3,

        /// <summary>
        /// Anthropic Messages API (/v1/messages). Upstream authentication uses the x-api-key and anthropic-version headers.
        /// Client-facing.
        /// </summary>
        Anthropic = 4,

        /// <summary>
        /// Hosted TypeSafe System One decision API (/v1/systemone). Upstream authentication uses a required Bearer key.
        /// Client-facing.
        /// </summary>
        TypeSafe = 5,

        /// <summary>
        /// Ollaya, the local runtime for the TypeSafe System One decision API. Upstream authentication uses an optional
        /// Bearer key. Endpoint-only: accepted by TypeSafe virtual model runners.
        /// </summary>
        Ollaya = 6,

        /// <summary>
        /// Ollama Cloud: the Ollama wire API (/api/*) with a required Bearer key. Model management, keep-warm and
        /// RigMonitor are not supported. Endpoint-only: accepted by Ollama virtual model runners.
        /// </summary>
        OllamaCloud = 7,

        /// <summary>
        /// A server that implements the OpenAI wire API (/v1/*) but is not OpenAI, for example LM Studio or an
        /// Ollama host's OpenAI-compatible API. Upstream authentication uses an optional Bearer key. Endpoint-only:
        /// accepted by OpenAI virtual model runners.
        /// </summary>
        OpenAICompatible = 8
    }
}
