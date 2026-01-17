namespace Conductor.Core.ThirdParty.Ollama.Models
{
    using System;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Ollama local model information.
    /// </summary>
    public class OllamaLocalModel
    {
        /// <summary>
        /// Model name including tag (e.g., "llama2:latest").
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Model digest/hash.
        /// </summary>
        [JsonPropertyName("digest")]
        public string Digest { get; set; }

        /// <summary>
        /// Model size in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public long? Size { get; set; }

        /// <summary>
        /// When the model was last modified.
        /// </summary>
        [JsonPropertyName("modified_at")]
        public DateTime? ModifiedAt { get; set; }

        /// <summary>
        /// Model details including format, family, parameter size, and quantization.
        /// </summary>
        [JsonPropertyName("details")]
        public OllamaModelDetails Details { get; set; }

        /// <summary>
        /// Gets the model's base name without tag.
        /// </summary>
        public string GetBaseName()
        {
            if (string.IsNullOrEmpty(Name))
                return string.Empty;

            int colonIndex = Name.IndexOf(':');
            return colonIndex > 0 ? Name.Substring(0, colonIndex) : Name;
        }

        /// <summary>
        /// Gets the model's tag.
        /// </summary>
        public string GetTag()
        {
            if (string.IsNullOrEmpty(Name))
                return "latest";

            int colonIndex = Name.IndexOf(':');
            return colonIndex > 0 ? Name.Substring(colonIndex + 1) : "latest";
        }

        /// <summary>
        /// Gets the formatted size of the model.
        /// </summary>
        public string GetFormattedSize()
        {
            if (!Size.HasValue)
                return "Unknown";

            return FormatBytes(Size.Value);
        }

        /// <summary>
        /// Gets a short digest identifier.
        /// </summary>
        public string GetShortDigest()
        {
            if (string.IsNullOrEmpty(Digest))
                return string.Empty;

            string[] parts = Digest.Split(':');
            string hash = parts.Length == 2 ? parts[1] : Digest;

            return hash.Length > 12 ? hash.Substring(0, 12) : hash;
        }

        /// <summary>
        /// Gets the age of the model since last modification.
        /// </summary>
        public TimeSpan? GetAge()
        {
            if (!ModifiedAt.HasValue)
                return null;

            return DateTime.UtcNow - ModifiedAt.Value;
        }

        /// <summary>
        /// Gets a formatted age string.
        /// </summary>
        public string GetFormattedAge()
        {
            TimeSpan? age = GetAge();
            if (!age.HasValue)
                return "Unknown";

            if (age.Value.TotalDays >= 365)
                return $"{(int)(age.Value.TotalDays / 365)} year(s) ago";
            if (age.Value.TotalDays >= 30)
                return $"{(int)(age.Value.TotalDays / 30)} month(s) ago";
            if (age.Value.TotalDays >= 1)
                return $"{(int)age.Value.TotalDays} day(s) ago";
            if (age.Value.TotalHours >= 1)
                return $"{(int)age.Value.TotalHours} hour(s) ago";
            if (age.Value.TotalMinutes >= 1)
                return $"{(int)age.Value.TotalMinutes} minute(s) ago";

            return "Just now";
        }

        /// <summary>
        /// Checks if this model matches a given pattern.
        /// </summary>
        /// <param name="pattern">Pattern to match (supports wildcards).</param>
        public bool MatchesPattern(string pattern)
        {
            if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(pattern))
                return false;

            if (pattern.Contains("*"))
            {
                string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                return Regex.IsMatch(Name, regexPattern, RegexOptions.IgnoreCase);
            }

            return Name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
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

        /// <summary>
        /// Ollama local model.
        /// </summary>
        public OllamaLocalModel()
        {
        }
    }
}
