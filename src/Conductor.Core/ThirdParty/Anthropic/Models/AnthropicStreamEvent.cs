namespace Conductor.Core.ThirdParty.Anthropic.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One Anthropic Messages API server-sent event payload (the JSON in a data: line). Only the fields Conductor reads are modeled.
    /// Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class AnthropicStreamEvent
    {
        /// <summary>
        /// Event type: message_start, content_block_start, content_block_delta, content_block_stop, message_delta, message_stop, ping or error. Null when absent.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Message object carried by message_start. Null for other events.
        /// </summary>
        [JsonPropertyName("message")]
        public AnthropicMessageResponse Message { get; set; }

        /// <summary>
        /// Usage carried by message_delta (cumulative output tokens). Null for other events.
        /// </summary>
        [JsonPropertyName("usage")]
        public AnthropicUsage Usage { get; set; }

        /// <summary>
        /// Error detail carried by an error event. Null for other events.
        /// </summary>
        [JsonPropertyName("error")]
        public AnthropicErrorDetail Error { get; set; }

        /// <summary>
        /// Instantiate the AnthropicStreamEvent.
        /// </summary>
        public AnthropicStreamEvent()
        {
        }
    }
}
