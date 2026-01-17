namespace Conductor.Core.ThirdParty.Ollama.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Pull model response message.
    /// </summary>
    public class PullModelResponse
    {
        /// <summary>
        /// Current status of the pull operation.
        /// Examples: "pulling manifest", "pulling model", "writing manifest", "success"
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Number of bytes downloaded.
        /// </summary>
        [JsonPropertyName("downloaded")]
        public long? Downloaded { get; set; }

        /// <summary>
        /// Download progress as a decimal (0.0 to 1.0).
        /// </summary>
        [JsonPropertyName("percent")]
        public decimal? Percent { get; set; }

        /// <summary>
        /// Error message if the pull operation failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Gets the download progress as a percentage (0-100).
        /// </summary>
        public double? GetProgressPercentage()
        {
            if (Percent.HasValue)
            {
                return (double)(Percent.Value * 100.0m);
            }
            return null;
        }

        /// <summary>
        /// Gets a formatted progress string.
        /// </summary>
        public string GetFormattedProgress()
        {
            if (Downloaded.HasValue && Percent.HasValue)
            {
                string downloadedStr = FormatBytes(Downloaded.Value);
                string percentStr = GetProgressPercentage()?.ToString("F1") ?? "0.0";
                return $"{downloadedStr} ({percentStr}%)";
            }
            return Status ?? "Unknown";
        }

        /// <summary>
        /// Checks if the operation is complete.
        /// </summary>
        public bool IsComplete()
        {
            return Status?.Equals("success", StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Checks if the operation has failed.
        /// </summary>
        public bool HasError()
        {
            return !string.IsNullOrEmpty(Error);
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:F2} {sizes[order]}";
        }
    }
}
