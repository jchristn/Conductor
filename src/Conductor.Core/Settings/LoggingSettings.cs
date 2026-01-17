namespace Conductor.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// List of syslog servers.
        /// </summary>
        public List<SyslogServer> Servers
        {
            get => _Servers;
            set => _Servers = (value != null ? value : new List<SyslogServer>());
        }

        /// <summary>
        /// Directory for log files.
        /// </summary>
        public string LogDirectory
        {
            get => _LogDirectory;
            set => _LogDirectory = (String.IsNullOrEmpty(value) ? "./logs/" : value);
        }

        /// <summary>
        /// Log filename.
        /// </summary>
        public string LogFilename
        {
            get => _LogFilename;
            set => _LogFilename = (String.IsNullOrEmpty(value) ? "./conductor.log" : value);
        }

        /// <summary>
        /// Enable console logging.
        /// </summary>
        public bool ConsoleLogging { get; set; } = true;

        /// <summary>
        /// Enable colors in console output.
        /// </summary>
        public bool EnableColors { get; set; } = false;

        /// <summary>
        /// Minimum severity level for logging (0 = Debug, 7 = Emergency).
        /// </summary>
        public int MinimumSeverity
        {
            get => _MinimumSeverity;
            set => _MinimumSeverity = (value < 0 ? 0 : (value > 7 ? 7 : value));
        }

        private List<SyslogServer> _Servers = new List<SyslogServer>();
        private string _LogDirectory = "./logs/";
        private string _LogFilename = "./conductor.log";
        private int _MinimumSeverity = 0;

        /// <summary>
        /// Instantiate the logging settings.
        /// </summary>
        public LoggingSettings()
        {
        }
    }
}
