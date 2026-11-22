namespace Conductor.Core.ThirdParty.Anthropic.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Anthropic model object returned by GET /v1/models and GET /v1/models/{id}.
    /// Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class AnthropicModelInfo
    {
        /// <summary>
        /// Object type, always "model".
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "model";

        /// <summary>
        /// Model identifier. Null when absent.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Human-readable model name. Null when absent.
        /// </summary>
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        /// <summary>
        /// RFC 3339 creation timestamp. Null when absent.
        /// </summary>
        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        /// <summary>
        /// Instantiate the AnthropicModelInfo.
        /// </summary>
        public AnthropicModelInfo()
        {
        }
    }
}
