namespace Conductor.Core.ThirdParty.TypeSafe.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// TypeSafe System One token usage block.
    /// Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class TypeSafeUsage
    {
        /// <summary>
        /// Input tokens. Null when absent.
        /// </summary>
        [JsonPropertyName("input_tokens")]
        public int? InputTokens { get; set; }

        /// <summary>
        /// Output tokens. Null when absent.
        /// </summary>
        [JsonPropertyName("output_tokens")]
        public int? OutputTokens { get; set; }

        /// <summary>
        /// Instantiate the TypeSafeUsage.
        /// </summary>
        public TypeSafeUsage()
        {
        }
    }
}
