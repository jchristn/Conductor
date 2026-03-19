namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM logprobs data.
    /// </summary>
    public class vLLMLogprobs
    {
        /// <summary>
        /// List of token strings.
        /// </summary>
        [JsonPropertyName("tokens")]
        public List<string> Tokens { get; set; }

        /// <summary>
        /// Log probabilities for each token.
        /// </summary>
        [JsonPropertyName("token_logprobs")]
        public List<float?> TokenLogprobs { get; set; }

        /// <summary>
        /// Top log probabilities for each token position.
        /// </summary>
        [JsonPropertyName("top_logprobs")]
        public List<Dictionary<string, float>> TopLogprobs { get; set; }

        /// <summary>
        /// Text offset for each token.
        /// </summary>
        [JsonPropertyName("text_offset")]
        public List<int> TextOffset { get; set; }

        /// <summary>
        /// vLLM logprobs.
        /// </summary>
        public vLLMLogprobs()
        {
        }
    }
}
