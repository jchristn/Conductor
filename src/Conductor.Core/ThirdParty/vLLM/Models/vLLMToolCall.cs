namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM tool call.
    /// </summary>
    public class vLLMToolCall
    {
        /// <summary>
        /// Unique identifier for the tool call.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = null;

        /// <summary>
        /// Type of tool call (currently only "function").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        /// <summary>
        /// Function call details.
        /// </summary>
        [JsonPropertyName("function")]
        public vLLMToolCallFunction Function { get; set; } = null;

        /// <summary>
        /// vLLM tool call.
        /// </summary>
        public vLLMToolCall()
        {
        }
    }
}
