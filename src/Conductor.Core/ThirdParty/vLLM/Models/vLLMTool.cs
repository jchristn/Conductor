namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM tool definition.
    /// </summary>
    public class vLLMTool
    {
        /// <summary>
        /// Type of tool (currently only "function" is supported).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        /// <summary>
        /// Function definition.
        /// </summary>
        [JsonPropertyName("function")]
        public vLLMToolFunction Function { get; set; } = null;

        /// <summary>
        /// vLLM tool.
        /// </summary>
        public vLLMTool()
        {
        }
    }
}
