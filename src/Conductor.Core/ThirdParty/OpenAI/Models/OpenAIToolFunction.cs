namespace Conductor.Core.ThirdParty.OpenAI.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI tool function definition.
    /// </summary>
    public class OpenAIToolFunction
    {
        /// <summary>
        /// Name of the function.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = null;

        /// <summary>
        /// Description of what the function does.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = null;

        /// <summary>
        /// Parameters the function accepts, described as a JSON Schema object.
        /// </summary>
        [JsonPropertyName("parameters")]
        public object Parameters { get; set; } = null;

        /// <summary>
        /// Whether to enable strict schema adherence.
        /// </summary>
        [JsonPropertyName("strict")]
        public bool? Strict { get; set; } = null;

        /// <summary>
        /// OpenAI tool function.
        /// </summary>
        public OpenAIToolFunction()
        {
        }
    }
}
