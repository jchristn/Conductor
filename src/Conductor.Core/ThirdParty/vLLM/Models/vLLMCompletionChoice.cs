namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM completion choice.
    /// </summary>
    public class vLLMCompletionChoice
    {
        /// <summary>
        /// Generated text.
        /// </summary>
        [JsonPropertyName("text")]
        public string Text { get; set; }

        /// <summary>
        /// Index of this choice.
        /// </summary>
        [JsonPropertyName("index")]
        public int Index { get; set; }

        /// <summary>
        /// Log probabilities information.
        /// </summary>
        [JsonPropertyName("logprobs")]
        public vLLMLogprobs Logprobs { get; set; }

        /// <summary>
        /// Reason the generation stopped.
        /// Possible values: "stop", "length"
        /// </summary>
        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; }

        /// <summary>
        /// vLLM completion choice.
        /// </summary>
        public vLLMCompletionChoice()
        {
        }
    }
}
