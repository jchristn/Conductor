namespace Conductor.Core.ThirdParty.Anthropic.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Anthropic model list returned by GET /v1/models.
    /// Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class AnthropicModelList
    {
        /// <summary>
        /// Models on this page. Never null when built by Conductor.
        /// </summary>
        [JsonPropertyName("data")]
        public List<AnthropicModelInfo> Data { get; set; } = new List<AnthropicModelInfo>();

        /// <summary>
        /// Whether more pages exist.
        /// </summary>
        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }

        /// <summary>
        /// First model id on this page. Null when the page is empty.
        /// </summary>
        [JsonPropertyName("first_id")]
        public string FirstId { get; set; }

        /// <summary>
        /// Last model id on this page. Null when the page is empty.
        /// </summary>
        [JsonPropertyName("last_id")]
        public string LastId { get; set; }

        /// <summary>
        /// Instantiate the AnthropicModelList.
        /// </summary>
        public AnthropicModelList()
        {
        }
    }
}
