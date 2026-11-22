namespace Conductor.Core.ThirdParty.Anthropic.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Anthropic Messages API message object (non-streaming response body, or the message in a message_start event). Only the fields Conductor reads are modeled.
    /// Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class AnthropicMessageResponse
    {
        /// <summary>
        /// Message identifier, for example msg_01. Null when absent.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Object type, normally "message". Null when absent.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Message role, normally "assistant". Null when absent.
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; }

        /// <summary>
        /// Model that produced the message. Null when absent.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; }

        /// <summary>
        /// Reason generation stopped. Null when absent or not yet known.
        /// </summary>
        [JsonPropertyName("stop_reason")]
        public string StopReason { get; set; }

        /// <summary>
        /// Token usage. Null when absent.
        /// </summary>
        [JsonPropertyName("usage")]
        public AnthropicUsage Usage { get; set; }

        /// <summary>
        /// Instantiate the AnthropicMessageResponse.
        /// </summary>
        public AnthropicMessageResponse()
        {
        }
    }
}
