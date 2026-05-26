namespace Conductor.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Request history settings.
    /// </summary>
    public class RequestHistorySettings
    {
        /// <summary>
        /// Enable or disable request history globally.
        /// Default is true.
        /// </summary>
        public bool Enabled
        {
            get => _Enabled;
            set => _Enabled = value;
        }

        /// <summary>
        /// Number of days to retain request history records.
        /// Minimum: 1. Maximum: 365. Default: 30.
        /// </summary>
        public int RetentionDays
        {
            get => _RetentionDays;
            set => _RetentionDays = (value < 1 ? 30 : (value > 365 ? 365 : value));
        }

        /// <summary>
        /// Number of days to retain request-history metadata rows.
        /// Minimum: 1. Maximum: 365. Default: 30.
        /// </summary>
        public int MetadataRetentionDays
        {
            get => _MetadataRetentionDays;
            set => _MetadataRetentionDays = (value < 1 ? 30 : (value > 365 ? 365 : value));
        }

        /// <summary>
        /// Number of days to retain request and response bodies in detail files.
        /// Minimum: 1. Maximum: 365. Default: 30.
        /// </summary>
        public int BodyRetentionDays
        {
            get => _BodyRetentionDays;
            set => _BodyRetentionDays = (value < 1 ? 30 : (value > 365 ? 365 : value));
        }

        /// <summary>
        /// Directory for storing request/response files.
        /// Default is "./request-history".
        /// </summary>
        public string Directory
        {
            get => _Directory;
            set => _Directory = (String.IsNullOrEmpty(value) ? "./request-history" : value);
        }

        /// <summary>
        /// Interval in minutes between cleanup runs.
        /// Minimum: 1. Maximum: 1440. Default: 5.
        /// </summary>
        public int CleanupIntervalMinutes
        {
            get => _CleanupIntervalMinutes;
            set => _CleanupIntervalMinutes = (value < 1 ? 5 : (value > 1440 ? 1440 : value));
        }

        /// <summary>
        /// Whether request bodies should be captured when request history is enabled.
        /// </summary>
        public bool CaptureRequestBody { get; set; } = true;

        /// <summary>
        /// Whether response bodies should be captured when request history is enabled.
        /// </summary>
        public bool CaptureResponseBody { get; set; } = true;

        /// <summary>
        /// Header names that should be redacted when persisted.
        /// </summary>
        public List<string> RedactedHeaders
        {
            get => _RedactedHeaders;
            set => _RedactedHeaders = (value != null ? value : new List<string>());
        }

        /// <summary>
        /// JSON field names that should be redacted recursively when persisted.
        /// </summary>
        public List<string> RedactedJsonFields
        {
            get => _RedactedJsonFields;
            set => _RedactedJsonFields = (value != null ? value : new List<string>());
        }

        /// <summary>
        /// Maximum request body size to capture in bytes.
        /// Minimum: 1024. Maximum: 1048576. Default: 65536.
        /// </summary>
        public int MaxRequestBodyBytes
        {
            get => _MaxRequestBodyBytes;
            set => _MaxRequestBodyBytes = (value < 1024 ? 65536 : (value > 1048576 ? 1048576 : value));
        }

        /// <summary>
        /// Maximum response body size to capture in bytes.
        /// Minimum: 1024. Maximum: 1048576. Default: 65536.
        /// </summary>
        public int MaxResponseBodyBytes
        {
            get => _MaxResponseBodyBytes;
            set => _MaxResponseBodyBytes = (value < 1024 ? 65536 : (value > 1048576 ? 1048576 : value));
        }

        private bool _Enabled = true;
        private int _RetentionDays = 30;
        private int _MetadataRetentionDays = 30;
        private int _BodyRetentionDays = 30;
        private string _Directory = "./request-history";
        private int _CleanupIntervalMinutes = 5;
        private int _MaxRequestBodyBytes = 65536;
        private int _MaxResponseBodyBytes = 65536;
        private List<string> _RedactedHeaders = new List<string>
        {
            "authorization",
            "x-password",
            "x-admin-password",
            "x-goog-api-key"
        };
        private List<string> _RedactedJsonFields = new List<string>
        {
            "authorization",
            "api_key",
            "apikey",
            "password",
            "token",
            "bearertoken"
        };

        /// <summary>
        /// Instantiate request history settings with defaults.
        /// </summary>
        public RequestHistorySettings()
        {
        }
    }
}
