namespace Conductor.Core.ThirdParty.Anthropic.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Anthropic-shaped error response: {"type":"error","error":{"type":"...","message":"..."}}. Conductor uses this shape for errors it generates on Anthropic virtual model runners.
    /// Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class AnthropicErrorResponse
    {
        /// <summary>
        /// Always "error".
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "error";

        /// <summary>
        /// Error detail. Never null when built by Conductor.
        /// </summary>
        [JsonPropertyName("error")]
        public AnthropicErrorDetail Error { get; set; } = new AnthropicErrorDetail();

        /// <summary>
        /// Instantiate the AnthropicErrorResponse.
        /// </summary>
        public AnthropicErrorResponse()
        {
        }
    }
}
