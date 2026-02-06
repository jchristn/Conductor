namespace Conductor.Core.Settings
{
    using System;

    /// <summary>
    /// Request history settings.
    /// </summary>
    public class RequestHistorySettings
    {
        /// <summary>
        /// Enable or disable request history globally.
        /// Default is false.
        /// </summary>
        public bool Enabled
        {
            get => _Enabled;
            set => _Enabled = value;
        }

        /// <summary>
        /// Number of days to retain request history records.
        /// Minimum: 1. Maximum: 365. Default: 7.
        /// </summary>
        public int RetentionDays
        {
            get => _RetentionDays;
            set => _RetentionDays = (value < 1 ? 7 : (value > 365 ? 365 : value));
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

        private bool _Enabled = false;
        private int _RetentionDays = 7;
        private string _Directory = "./request-history";
        private int _CleanupIntervalMinutes = 5;
        private int _MaxRequestBodyBytes = 65536;
        private int _MaxResponseBodyBytes = 65536;

        /// <summary>
        /// Instantiate request history settings with defaults.
        /// </summary>
        public RequestHistorySettings()
        {
        }
    }
}
