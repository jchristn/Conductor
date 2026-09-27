namespace Conductor.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Webserver settings.
    /// </summary>
    public class WebserverSettings
    {
        /// <summary>
        /// Hostname to bind to.
        /// </summary>
        public string Hostname
        {
            get => _Hostname;
            set => _Hostname = (String.IsNullOrEmpty(value) ? "localhost" : value);
        }

        /// <summary>
        /// Port number to listen on.
        /// </summary>
        public int Port
        {
            get => _Port;
            set => _Port = (value < 1 || value > 65535 ? 9000 : value);
        }

        /// <summary>
        /// Enable SSL/TLS.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// CORS configuration settings.
        /// Default is disabled.
        /// </summary>
        public CorsSettings Cors
        {
            get => _Cors;
            set => _Cors = value ?? new CorsSettings();
        }

        /// <summary>
        /// Reverse proxies whose forwarded-for header is trusted when determining the client IP address
        /// recorded in request history, logs, and source-IP session affinity.
        /// Each entry is an IP address (e.g. "10.0.0.5") or a CIDR range (e.g. "172.16.0.0/12").
        /// When the connecting peer matches an entry, the forwarded-for header is walked from right to left,
        /// skipping trusted entries, and the first untrusted address is used as the client IP.
        /// Only list addresses that cannot be reached directly by untrusted clients, since any client
        /// connecting from a trusted address can supply an arbitrary forwarded-for value.
        /// Default is empty (forwarded-for headers are ignored and the connecting peer address is used).
        /// Setting to null resets to an empty list.
        /// </summary>
        public List<string> TrustedProxies
        {
            get => _TrustedProxies;
            set => _TrustedProxies = value ?? new List<string>();
        }

        /// <summary>
        /// Name of the request header carrying the forwarded client address chain.
        /// Only consulted when the connecting peer is listed in TrustedProxies.
        /// Default is "X-Forwarded-For". Setting to null or empty resets to the default.
        /// </summary>
        public string ForwardedForHeader
        {
            get => _ForwardedForHeader;
            set => _ForwardedForHeader = (String.IsNullOrWhiteSpace(value) ? "X-Forwarded-For" : value);
        }

        private string _Hostname = "localhost";
        private int _Port = 9000;
        private CorsSettings _Cors = new CorsSettings();
        private List<string> _TrustedProxies = new List<string>();
        private string _ForwardedForHeader = "X-Forwarded-For";

        /// <summary>
        /// Instantiate the webserver settings.
        /// </summary>
        public WebserverSettings()
        {
        }
    }
}
