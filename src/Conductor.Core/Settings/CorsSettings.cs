namespace Conductor.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Configuration settings for Cross-Origin Resource Sharing (CORS).
    /// </summary>
    public class CorsSettings
    {
        private bool _Enabled = false;
        private List<string> _AllowedOrigins = new List<string>();
        private List<string> _AllowedMethods = new List<string> { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
        private List<string> _AllowedHeaders = new List<string> { "Content-Type", "Authorization", "x-tenant-id", "x-email", "x-password", "x-admin-apikey", "x-admin-email", "x-admin-password" };
        private List<string> _ExposedHeaders = new List<string>();
        private bool _AllowCredentials = false;
        private int _MaxAgeSeconds = 86400;

        /// <summary>
        /// Enable or disable CORS support.
        /// Default is false.
        /// </summary>
        public bool Enabled
        {
            get => _Enabled;
            set => _Enabled = value;
        }

        /// <summary>
        /// List of allowed origins. Use "*" to allow all origins.
        /// When AllowCredentials is true, "*" is not permitted.
        /// Default is empty (no origins allowed).
        /// </summary>
        public List<string> AllowedOrigins
        {
            get => _AllowedOrigins;
            set => _AllowedOrigins = value ?? new List<string>();
        }

        /// <summary>
        /// List of allowed HTTP methods.
        /// Default: GET, POST, PUT, DELETE, OPTIONS.
        /// </summary>
        public List<string> AllowedMethods
        {
            get => _AllowedMethods;
            set => _AllowedMethods = value ?? new List<string>();
        }

        /// <summary>
        /// List of allowed request headers.
        /// Default includes Content-Type, Authorization, and Conductor auth headers.
        /// </summary>
        public List<string> AllowedHeaders
        {
            get => _AllowedHeaders;
            set => _AllowedHeaders = value ?? new List<string>();
        }

        /// <summary>
        /// List of headers to expose to the browser.
        /// Default is empty.
        /// </summary>
        public List<string> ExposedHeaders
        {
            get => _ExposedHeaders;
            set => _ExposedHeaders = value ?? new List<string>();
        }

        /// <summary>
        /// Whether to allow credentials (cookies, authorization headers).
        /// When true, AllowedOrigins cannot contain "*".
        /// Default is false.
        /// </summary>
        public bool AllowCredentials
        {
            get => _AllowCredentials;
            set => _AllowCredentials = value;
        }

        /// <summary>
        /// How long (in seconds) browsers should cache preflight results.
        /// Default is 86400 (24 hours). Minimum is 0, maximum is 86400.
        /// </summary>
        public int MaxAgeSeconds
        {
            get => _MaxAgeSeconds;
            set => _MaxAgeSeconds = Math.Clamp(value, 0, 86400);
        }

        /// <summary>
        /// Instantiate the CORS settings.
        /// </summary>
        public CorsSettings()
        {
        }
    }
}
