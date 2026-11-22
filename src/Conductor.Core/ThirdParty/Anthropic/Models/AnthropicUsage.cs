namespace Conductor.Core.ThirdParty.Anthropic.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Anthropic Messages API token usage block.
    /// Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class AnthropicUsage
    {
        /// <summary>
        /// Input tokens billed for the request. Null when absent.
        /// </summary>
        [JsonPropertyName("input_tokens")]
        public int? InputTokens { get; set; }

        /// <summary>
        /// Output tokens generated. In a streaming message_delta event this is the cumulative count so far. Null when absent.
        /// </summary>
        [JsonPropertyName("output_tokens")]
        public int? OutputTokens { get; set; }

        /// <summary>
        /// Input tokens written to the prompt cache. Null when absent.
        /// </summary>
        [JsonPropertyName("cache_creation_input_tokens")]
        public int? CacheCreationInputTokens { get; set; }

        /// <summary>
        /// Input tokens read from the prompt cache. Null when absent.
        /// </summary>
        [JsonPropertyName("cache_read_input_tokens")]
        public int? CacheReadInputTokens { get; set; }

        /// <summary>
        /// Instantiate the AnthropicUsage.
        /// </summary>
        public AnthropicUsage()
        {
        }
    }
}
