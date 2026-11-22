namespace Conductor.Core.ThirdParty.Anthropic.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Anthropic error detail: the error member of an Anthropic error response.
    /// Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class AnthropicErrorDetail
    {
        /// <summary>
        /// Anthropic error type, for example authentication_error, permission_error, not_found_error, rate_limit_error, api_error or overloaded_error.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }

        /// <summary>
        /// Instantiate the AnthropicErrorDetail.
        /// </summary>
        public AnthropicErrorDetail()
        {
        }
    }
}
