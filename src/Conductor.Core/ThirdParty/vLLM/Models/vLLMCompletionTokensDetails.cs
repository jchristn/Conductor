namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM completion tokens details.
    /// </summary>
    public class vLLMCompletionTokensDetails
    {
        /// <summary>
        /// Number of reasoning tokens generated.
        /// </summary>
        [JsonPropertyName("reasoning_tokens")]
        public int? ReasoningTokens { get; set; }

        /// <summary>
        /// Number of audio tokens generated.
        /// </summary>
        [JsonPropertyName("audio_tokens")]
        public int? AudioTokens { get; set; }

        /// <summary>
        /// Number of tokens that were accepted.
        /// </summary>
        [JsonPropertyName("accepted_prediction_tokens")]
        public int? AcceptedPredictionTokens { get; set; }

        /// <summary>
        /// Number of tokens that were rejected.
        /// </summary>
        [JsonPropertyName("rejected_prediction_tokens")]
        public int? RejectedPredictionTokens { get; set; }

        /// <summary>
        /// vLLM completion tokens details.
        /// </summary>
        public vLLMCompletionTokensDetails()
        {
        }
    }
}
