namespace Conductor.Core.ThirdParty.OpenAI.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI tool call function details.
    /// </summary>
    public class OpenAIToolCallFunction
    {
        /// <summary>
        /// Name of the function to call.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = null;

        /// <summary>
        /// Arguments to pass to the function as a JSON string.
        /// </summary>
        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = null;

        /// <summary>
        /// OpenAI tool call function.
        /// </summary>
        public OpenAIToolCallFunction()
        {
        }
    }
}
