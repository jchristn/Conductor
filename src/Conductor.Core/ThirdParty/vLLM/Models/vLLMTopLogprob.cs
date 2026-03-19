namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM top logprob entry.
    /// </summary>
    public class vLLMTopLogprob
    {
        /// <summary>
        /// The token.
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; }

        /// <summary>
        /// Log probability of this token.
        /// </summary>
        [JsonPropertyName("logprob")]
        public float Logprob { get; set; }

        /// <summary>
        /// UTF-8 byte representation of the token.
        /// </summary>
        [JsonPropertyName("bytes")]
        public List<int> Bytes { get; set; }

        /// <summary>
        /// vLLM top logprob.
        /// </summary>
        public vLLMTopLogprob()
        {
        }
    }
}
