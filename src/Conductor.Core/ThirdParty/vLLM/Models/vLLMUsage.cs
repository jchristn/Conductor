namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM usage statistics.
    /// </summary>
    public class vLLMUsage
    {
        /// <summary>
        /// Number of tokens in the prompt.
        /// </summary>
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        /// <summary>
        /// Number of tokens in the completion.
        /// </summary>
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        /// <summary>
        /// Total number of tokens used.
        /// </summary>
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        /// <summary>
        /// Detailed breakdown of prompt tokens (for chat completions with images).
        /// </summary>
        [JsonPropertyName("prompt_tokens_details")]
        public vLLMPromptTokensDetails PromptTokensDetails { get; set; }

        /// <summary>
        /// Detailed breakdown of completion tokens.
        /// </summary>
        [JsonPropertyName("completion_tokens_details")]
        public vLLMCompletionTokensDetails CompletionTokensDetails { get; set; }

        /// <summary>
        /// vLLM usage.
        /// </summary>
        public vLLMUsage()
        {
        }
    }
}
