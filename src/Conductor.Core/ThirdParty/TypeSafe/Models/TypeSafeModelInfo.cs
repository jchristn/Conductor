namespace Conductor.Core.ThirdParty.TypeSafe.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A model entry in a TypeSafe System One model list (GET /v1/models, served by Ollaya), using the OpenAI-style
    /// {"id","object"} shape. Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class TypeSafeModelInfo
    {
        /// <summary>
        /// Model identifier. Null when absent.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Object type, always "model".
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; } = "model";

        /// <summary>
        /// Owner of the model. Null when absent.
        /// </summary>
        [JsonPropertyName("owned_by")]
        public string OwnedBy { get; set; }

        /// <summary>
        /// Instantiate the TypeSafeModelInfo.
        /// </summary>
        public TypeSafeModelInfo()
        {
        }
    }
}
