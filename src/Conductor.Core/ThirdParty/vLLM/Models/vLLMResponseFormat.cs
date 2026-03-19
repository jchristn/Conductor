namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM response format specification.
    /// </summary>
    public class vLLMResponseFormat
    {
        /// <summary>
        /// Type of response format.
        /// Valid values: "text", "json_object", "json_schema"
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = null;

        /// <summary>
        /// JSON schema for structured output (when type is "json_schema").
        /// </summary>
        [JsonPropertyName("json_schema")]
        public object JsonSchema { get; set; } = null;

        /// <summary>
        /// vLLM response format.
        /// </summary>
        public vLLMResponseFormat()
        {
        }
    }
}
