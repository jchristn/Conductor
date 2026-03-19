namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM prompt tokens details.
    /// </summary>
    public class vLLMPromptTokensDetails
    {
        /// <summary>
        /// Number of tokens from cached content.
        /// </summary>
        [JsonPropertyName("cached_tokens")]
        public int? CachedTokens { get; set; }

        /// <summary>
        /// Number of tokens from audio input.
        /// </summary>
        [JsonPropertyName("audio_tokens")]
        public int? AudioTokens { get; set; }

        /// <summary>
        /// vLLM prompt tokens details.
        /// </summary>
        public vLLMPromptTokensDetails()
        {
        }
    }
}
