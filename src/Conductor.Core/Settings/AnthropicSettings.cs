namespace Conductor.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Settings for Anthropic Messages API endpoints and virtual model runners.
    /// Instances are not thread safe for concurrent mutation; treat a loaded instance as read-only.
    /// </summary>
    public class AnthropicSettings
    {
        /// <summary>
        /// The date format used by anthropic-version values.
        /// </summary>
        public const string VersionFormat = "yyyy-MM-dd";

        /// <summary>
        /// The anthropic-version value sent upstream when neither the client nor the endpoint supplies one.
        /// Must use the format yyyy-MM-dd. Null, empty or invalid values reset to the default. Default: 2023-06-01.
        /// </summary>
        public string DefaultVersion
        {
            get => _DefaultVersion;
            set => _DefaultVersion = (IsValidVersion(value) ? value.Trim() : _DefaultVersionValue);
        }

        /// <summary>
        /// Client request headers that are copied upstream unchanged on Anthropic endpoints.
        /// The authorization, x-api-key and anthropic-api-key headers are never forwarded even when listed.
        /// Null resets to an empty list. Default: anthropic-version, anthropic-beta.
        /// </summary>
        public List<string> ForwardedRequestHeaders
        {
            get => _ForwardedRequestHeaders;
            set => _ForwardedRequestHeaders = (value ?? new List<string>());
        }

        /// <summary>
        /// Upstream response headers that are copied to the client on Anthropic endpoints.
        /// Null resets to an empty list. Default: request-id, retry-after, x-should-retry.
        /// </summary>
        public List<string> ForwardedResponseHeaders
        {
            get => _ForwardedResponseHeaders;
            set => _ForwardedResponseHeaders = (value ?? new List<string>());
        }

        /// <summary>
        /// Upstream response header name prefixes; headers starting with any of these are copied to the client on
        /// Anthropic endpoints. Null resets to an empty list. Default: anthropic-ratelimit-.
        /// </summary>
        public List<string> ForwardedResponseHeaderPrefixes
        {
            get => _ForwardedResponseHeaderPrefixes;
            set => _ForwardedResponseHeaderPrefixes = (value ?? new List<string>());
        }

        /// <summary>
        /// The first anthropic-version at which model configuration sampling parameters (Temperature, TopP, TopK) are no
        /// longer injected into Anthropic requests. Must use the format yyyy-MM-dd. Null, empty or invalid values reset to
        /// the default. Default: 2026-10-21.
        /// </summary>
        public string SamplingParametersRemovedFromVersion
        {
            get => _SamplingParametersRemovedFromVersion;
            set => _SamplingParametersRemovedFromVersion = (IsValidVersion(value) ? value.Trim() : _SamplingRemovedValue);
        }

        private const string _DefaultVersionValue = "2023-06-01";
        private const string _SamplingRemovedValue = "2026-10-21";

        private string _DefaultVersion = _DefaultVersionValue;
        private List<string> _ForwardedRequestHeaders = new List<string> { "anthropic-version", "anthropic-beta" };
        private List<string> _ForwardedResponseHeaders = new List<string> { "request-id", "retry-after", "x-should-retry" };
        private List<string> _ForwardedResponseHeaderPrefixes = new List<string> { "anthropic-ratelimit-" };
        private string _SamplingParametersRemovedFromVersion = _SamplingRemovedValue;

        /// <summary>
        /// Instantiate Anthropic settings with defaults.
        /// </summary>
        public AnthropicSettings()
        {
        }

        /// <summary>
        /// Determine whether a value is a valid anthropic-version string in the format yyyy-MM-dd.
        /// </summary>
        /// <param name="value">Candidate value. Null returns false.</param>
        /// <returns>True when the value parses as a yyyy-MM-dd date.</returns>
        public static bool IsValidVersion(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            return DateTime.TryParseExact(value.Trim(), VersionFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime _);
        }

        /// <summary>
        /// Compare two anthropic-version strings by date.
        /// </summary>
        /// <param name="left">First version. Invalid or null values sort before valid values.</param>
        /// <param name="right">Second version. Invalid or null values sort before valid values.</param>
        /// <returns>Negative when left is earlier, zero when equal, positive when left is later.</returns>
        public static int CompareVersions(string left, string right)
        {
            bool leftValid = DateTime.TryParseExact(left?.Trim(), VersionFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime leftDate);
            bool rightValid = DateTime.TryParseExact(right?.Trim(), VersionFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime rightDate);
            if (!leftValid && !rightValid) return 0;
            if (!leftValid) return -1;
            if (!rightValid) return 1;
            return leftDate.CompareTo(rightDate);
        }

        /// <summary>
        /// Determine whether model configuration sampling parameters may be injected for an effective anthropic-version.
        /// </summary>
        /// <param name="effectiveVersion">The effective anthropic-version. Null or invalid values are treated as the default version.</param>
        /// <returns>True when the version is earlier than SamplingParametersRemovedFromVersion.</returns>
        public bool AllowsSamplingParameters(string effectiveVersion)
        {
            string version = IsValidVersion(effectiveVersion) ? effectiveVersion.Trim() : DefaultVersion;
            return CompareVersions(version, SamplingParametersRemovedFromVersion) < 0;
        }
    }
}
