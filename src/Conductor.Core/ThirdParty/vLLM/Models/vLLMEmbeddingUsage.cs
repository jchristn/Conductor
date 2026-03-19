namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM embedding usage statistics.
    /// </summary>
    public class vLLMEmbeddingUsage
    {
        /// <summary>
        /// Number of tokens in the input.
        /// </summary>
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        /// <summary>
        /// Total number of tokens used.
        /// </summary>
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        /// <summary>
        /// vLLM embedding usage.
        /// </summary>
        public vLLMEmbeddingUsage()
        {
        }
    }
}
