namespace Conductor.Core.Settings
{
    using System;

    /// <summary>
    /// Syslog server settings.
    /// </summary>
    public class SyslogServer
    {
        /// <summary>
        /// Hostname or IP address of the syslog server.
        /// </summary>
        public string Hostname
        {
            get => _Hostname;
            set => _Hostname = (String.IsNullOrEmpty(value) ? "127.0.0.1" : value);
        }

        /// <summary>
        /// Port number for the syslog server.
        /// </summary>
        public int Port
        {
            get => _Port;
            set => _Port = (value < 1 || value > 65535 ? 514 : value);
        }

        /// <summary>
        /// Enable randomizing ports for syslog connections.
        /// </summary>
        public bool RandomizePorts { get; set; } = false;

        /// <summary>
        /// Minimum port number when randomizing.
        /// </summary>
        public int MinimumPort
        {
            get => _MinimumPort;
            set => _MinimumPort = (value < 1 || value > 65535 ? 65000 : value);
        }

        /// <summary>
        /// Maximum port number when randomizing.
        /// </summary>
        public int MaximumPort
        {
            get => _MaximumPort;
            set => _MaximumPort = (value < 1 || value > 65535 ? 65535 : value);
        }

        private string _Hostname = "127.0.0.1";
        private int _Port = 514;
        private int _MinimumPort = 65000;
        private int _MaximumPort = 65535;

        /// <summary>
        /// Instantiate the syslog server settings.
        /// </summary>
        public SyslogServer()
        {
        }
    }
}
