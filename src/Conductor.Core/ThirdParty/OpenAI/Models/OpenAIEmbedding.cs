namespace Conductor.Core.ThirdParty.OpenAI.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI embedding data.
    /// </summary>
    public class OpenAIEmbedding
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
        /// OpenAI embedding.
        /// </summary>
        public OpenAIEmbedding()
        {
        }
    }
}
