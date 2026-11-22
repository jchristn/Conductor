namespace Conductor.Core.ThirdParty.TypeSafe.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// TypeSafe System One decision response body: {"model","answers":{...},"usage":{"input_tokens","output_tokens"}}.
    /// Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class TypeSafeDecisionResponse
    {
        /// <summary>
        /// Model that answered. Null when absent.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; }

        /// <summary>
        /// Answers keyed by question id. Null when absent.
        /// </summary>
        [JsonPropertyName("answers")]
        public Dictionary<string, JsonElement> Answers { get; set; }

        /// <summary>
        /// Token usage. Null when absent.
        /// </summary>
        [JsonPropertyName("usage")]
        public TypeSafeUsage Usage { get; set; }

        /// <summary>
        /// Instantiate the TypeSafeDecisionResponse.
        /// </summary>
        public TypeSafeDecisionResponse()
        {
        }
    }
}
