namespace Conductor.Core.ThirdParty.TypeSafe.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// TypeSafe System One model list (GET /v1/models, served by Ollaya), using the OpenAI-style {"object":"list","data":[]}
    /// shape. Not thread safe; instances are plain data transfer objects.
    /// </summary>
    public class TypeSafeModelList
    {
        /// <summary>
        /// Object type, always "list".
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; } = "list";

        /// <summary>
        /// Models. Never null when built by Conductor.
        /// </summary>
        [JsonPropertyName("data")]
        public List<TypeSafeModelInfo> Data { get; set; } = new List<TypeSafeModelInfo>();

        /// <summary>
        /// Instantiate the TypeSafeModelList.
        /// </summary>
        public TypeSafeModelList()
        {
        }
    }
}
