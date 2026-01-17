namespace Conductor.Core.ThirdParty.OpenAI.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI chat logprobs data.
    /// </summary>
    public class OpenAIChatLogprobs
    {
        /// <summary>
        /// Log probability information for each content token.
        /// </summary>
        [JsonPropertyName("content")]
        public List<OpenAIChatLogprobContent> Content { get; set; }

        /// <summary>
        /// OpenAI chat logprobs.
        /// </summary>
        public OpenAIChatLogprobs()
        {
        }
    }
}
