namespace Conductor.Core.Enums
{
    using System;

    /// <summary>
    /// API type enumeration.
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
        vLLM = 3
    }
}
