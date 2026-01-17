namespace Conductor.Core.ThirdParty.Ollama.Models
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama model details.
    /// </summary>
    public class OllamaModelDetails
    {
        /// <summary>
        /// Parent model this was created from.
        /// </summary>
        [JsonPropertyName("parent_model")]
        public string ParentModel { get; set; }

        /// <summary>
        /// Model format (e.g., "gguf").
        /// </summary>
        [JsonPropertyName("format")]
        public string Format { get; set; }

        /// <summary>
        /// Model family (e.g., "llama", "mistral").
        /// </summary>
        [JsonPropertyName("family")]
        public string Family { get; set; }

        /// <summary>
        /// Model families/architectures.
        /// </summary>
        [JsonPropertyName("families")]
        public List<string> Families { get; set; }

        /// <summary>
        /// Parameter size (e.g., "7B", "13B", "70B").
        /// </summary>
        [JsonPropertyName("parameter_size")]
        public string ParameterSize { get; set; }

        /// <summary>
        /// Quantization level (e.g., "Q4_0", "Q4_K_M", "Q8_0").
        /// </summary>
        [JsonPropertyName("quantization_level")]
        public string QuantizationLevel { get; set; }

        /// <summary>
        /// Gets the estimated model size in billions of parameters.
        /// </summary>
        public double? GetParameterSizeInBillions()
        {
            if (string.IsNullOrEmpty(ParameterSize))
                return null;

            string sizeStr = ParameterSize.TrimEnd('B', 'b').Trim();

            if (double.TryParse(sizeStr, out double size))
                return size;

            if (sizeStr.Contains('.') && double.TryParse(sizeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatSize))
                return floatSize;

            return null;
        }

        /// <summary>
        /// Gets the quantization bits from the quantization level.
        /// </summary>
        public int? GetQuantizationBits()
        {
            if (string.IsNullOrEmpty(QuantizationLevel))
                return null;

            if (QuantizationLevel.StartsWith("Q", StringComparison.OrdinalIgnoreCase))
            {
                string numberPart = QuantizationLevel.Substring(1);
                int underscoreIndex = numberPart.IndexOf('_');

                if (underscoreIndex > 0)
                    numberPart = numberPart.Substring(0, underscoreIndex);

                if (int.TryParse(numberPart, out int bits))
                    return bits;
            }

            return null;
        }

        /// <summary>
        /// Checks if this is a quantized model.
        /// </summary>
        public bool IsQuantized()
        {
            return !string.IsNullOrEmpty(QuantizationLevel);
        }

        /// <summary>
        /// Checks if this model belongs to a specific family.
        /// </summary>
        /// <param name="familyName">The family name to check.</param>
        public bool BelongsToFamily(string familyName)
        {
            if (string.IsNullOrEmpty(familyName))
                return false;

            if (!string.IsNullOrEmpty(Family) && Family.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (Families != null && Families.Any(f => f.Equals(familyName, StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        /// <summary>
        /// Ollama model details.
        /// </summary>
        public OllamaModelDetails()
        {
        }
    }
}
