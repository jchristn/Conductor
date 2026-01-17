namespace Conductor.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Server settings - root configuration object.
    /// </summary>
    public class ServerSettings
    {
        /// <summary>
        /// Webserver settings.
        /// </summary>
        public WebserverSettings Webserver
        {
            get => _Webserver;
            set => _Webserver = (value != null ? value : throw new ArgumentNullException(nameof(Webserver)));
        }

        /// <summary>
        /// Database settings.
        /// </summary>
        public DatabaseSettings Database
        {
            get => _Database;
            set => _Database = (value != null ? value : throw new ArgumentNullException(nameof(Database)));
        }

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get => _Logging;
            set => _Logging = (value != null ? value : throw new ArgumentNullException(nameof(Logging)));
        }

        /// <summary>
        /// Administrator API keys for initial access.
        /// </summary>
        public List<string> AdminApiKeys
        {
            get => _AdminApiKeys;
            set => _AdminApiKeys = (value != null ? value : new List<string>());
        }

        private WebserverSettings _Webserver = new WebserverSettings();
        private DatabaseSettings _Database = new DatabaseSettings();
        private LoggingSettings _Logging = new LoggingSettings();
        private List<string> _AdminApiKeys = new List<string> { "conductoradmin" };

        /// <summary>
        /// Instantiate the server settings.
        /// </summary>
        public ServerSettings()
        {
        }
    }
}
