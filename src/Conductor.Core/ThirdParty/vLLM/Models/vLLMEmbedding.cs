namespace Conductor.Core.ThirdParty.vLLM.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// vLLM embedding data.
    /// </summary>
    public class vLLMEmbedding
    {
        /// <summary>
        /// Object type (always "embedding").
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; }

        /// <summary>
        /// Index of this embedding in the input array.
        /// </summary>
        [JsonPropertyName("index")]
        public int Index { get; set; }

        /// <summary>
        /// The embedding vector.
        /// </summary>
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; }

        /// <summary>
        /// vLLM embedding.
        /// </summary>
        public vLLMEmbedding()
        {
        }
    }
}
