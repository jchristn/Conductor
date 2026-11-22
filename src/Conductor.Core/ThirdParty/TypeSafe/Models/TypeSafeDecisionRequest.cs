namespace Conductor.Core.ThirdParty.TypeSafe.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// TypeSafe System One decision request body (POST /v1/systemone): {"model","state","questions":{"id":{"type","instructions","criteria"}}}. Question bodies are kept as raw JSON because Conductor forwards them unchanged and only counts them.
    /// Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class TypeSafeDecisionRequest
    {
        /// <summary>
        /// Decision model, for example jev-latest. Null when absent.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; }

        /// <summary>
        /// The state the questions are asked about (usually a string).
        /// </summary>
        [JsonPropertyName("state")]
        public JsonElement State { get; set; }

        /// <summary>
        /// Questions keyed by question id. Null when absent.
        /// </summary>
        [JsonPropertyName("questions")]
        public Dictionary<string, JsonElement> Questions { get; set; }

        /// <summary>
        /// Instantiate the TypeSafeDecisionRequest.
        /// </summary>
        public TypeSafeDecisionRequest()
        {
        }
    }
}
