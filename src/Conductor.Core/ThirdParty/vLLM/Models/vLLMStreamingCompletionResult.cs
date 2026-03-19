namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM streaming completion result.
    /// </summary>
    public class vLLMStreamingCompletionResult
    {
        /// <summary>
        /// Unique identifier for the completion.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Object type (always "text_completion").
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; }

        /// <summary>
        /// Unix timestamp when the completion was created.
        /// </summary>
        [JsonPropertyName("created")]
        public long? Created { get; set; }

        /// <summary>
        /// Model used for the completion.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; }

        /// <summary>
        /// List of completion choices.
        /// </summary>
        [JsonPropertyName("choices")]
        public List<vLLMCompletionChoice> Choices { get; set; }

        /// <summary>
        /// vLLM streaming completion result.
        /// </summary>
        public vLLMStreamingCompletionResult()
        {
        }
    }
}
