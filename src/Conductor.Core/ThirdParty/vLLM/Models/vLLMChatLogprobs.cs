namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM chat logprobs data.
    /// </summary>
    public class vLLMChatLogprobs
    {
        /// <summary>
        /// Log probability information for each content token.
        /// </summary>
        [JsonPropertyName("content")]
        public List<vLLMChatLogprobContent> Content { get; set; }

        /// <summary>
        /// vLLM chat logprobs.
        /// </summary>
        public vLLMChatLogprobs()
        {
        }
    }
}
