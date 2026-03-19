namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM tool call function details.
    /// </summary>
    public class vLLMToolCallFunction
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
        /// vLLM tool call function.
        /// </summary>
        public vLLMToolCallFunction()
        {
        }
    }
}
