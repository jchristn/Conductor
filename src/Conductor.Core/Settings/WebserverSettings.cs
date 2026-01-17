namespace Conductor.Core.Settings
{
    using System;

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

        private string _Hostname = "localhost";
        private int _Port = 9000;
        private CorsSettings _Cors = new CorsSettings();

        /// <summary>
        /// Instantiate the webserver settings.
        /// </summary>
        public WebserverSettings()
        {
        }
    }
}
