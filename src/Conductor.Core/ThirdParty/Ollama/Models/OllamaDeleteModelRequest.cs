namespace Conductor.Core.ThirdParty.Ollama.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama delete model request.
    /// </summary>
    public class OllamaDeleteModelRequest
    {
        /// <summary>
        /// Name of the model to delete (required).
        /// </summary>
        [JsonPropertyName("name")]
        public string Model
        {
            get => _Model;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Model name cannot be null or empty", nameof(Model));

                if (value.Contains(':'))
                {
                    string[] parts = value.Split(':');
                    if (parts.Length != 2)
                        throw new ArgumentException("Model name format should be 'name' or 'name:tag'", nameof(Model));

                    if (string.IsNullOrWhiteSpace(parts[0]))
                        throw new ArgumentException("Model base name cannot be empty", nameof(Model));

                    if (string.IsNullOrWhiteSpace(parts[1]))
                        throw new ArgumentException("Model tag cannot be empty when colon is present", nameof(Model));
                }

                _Model = value;
            }
        }

        private string _Model;

        /// <summary>
        /// Ollama delete model request.
        /// </summary>
        public OllamaDeleteModelRequest()
        {
        }
    }
}
