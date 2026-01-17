namespace Conductor.McpServer
{
    using System;

    /// <summary>
    /// Configuration settings for the Conductor MCP server.
    /// </summary>
    public class McpSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable the HTTP-based MCP server with SSE support.
        /// Default is true.
        /// </summary>
        public bool EnableHttpServer
        {
            get => _EnableHttpServer;
            set => _EnableHttpServer = value;
        }

        /// <summary>
        /// HTTP server hostname.
        /// Use "localhost" for local-only access or "*" for all interfaces (requires admin privileges).
        /// Default is "localhost".
        /// </summary>
        public string HttpHostname
        {
            get => _HttpHostname;
            set => _HttpHostname = (String.IsNullOrEmpty(value) ? "localhost" : value);
        }

        /// <summary>
        /// HTTP server port for MCP.
        /// Default is 9001. Must be between 1 and 65535.
        /// </summary>
        public int HttpPort
        {
            get => _HttpPort;
            set
            {
                if (value < 1 || value > 65535)
                    throw new ArgumentOutOfRangeException(nameof(HttpPort), "Port must be between 1 and 65535");
                _HttpPort = value;
            }
        }

        /// <summary>
        /// URL path for JSON-RPC requests on the HTTP server.
        /// Default is "/mcp/rpc".
        /// </summary>
        public string HttpRpcPath
        {
            get => _HttpRpcPath;
            set => _HttpRpcPath = (String.IsNullOrEmpty(value) ? "/mcp/rpc" : value);
        }

        /// <summary>
        /// URL path for Server-Sent Events on the HTTP server.
        /// Default is "/mcp/events".
        /// </summary>
        public string HttpEventsPath
        {
            get => _HttpEventsPath;
            set => _HttpEventsPath = (String.IsNullOrEmpty(value) ? "/mcp/events" : value);
        }

        /// <summary>
        /// Enable the TCP-based MCP server.
        /// Default is true.
        /// </summary>
        public bool EnableTcpServer
        {
            get => _EnableTcpServer;
            set => _EnableTcpServer = value;
        }

        /// <summary>
        /// TCP server bind address.
        /// Use "0.0.0.0" to listen on all interfaces or "127.0.0.1" for localhost only.
        /// Default is "127.0.0.1".
        /// </summary>
        public string TcpBindAddress
        {
            get => _TcpBindAddress;
            set => _TcpBindAddress = (String.IsNullOrEmpty(value) ? "127.0.0.1" : value);
        }

        /// <summary>
        /// TCP server port for MCP.
        /// Default is 9002. Must be between 1 and 65535.
        /// </summary>
        public int TcpPort
        {
            get => _TcpPort;
            set
            {
                if (value < 1 || value > 65535)
                    throw new ArgumentOutOfRangeException(nameof(TcpPort), "Port must be between 1 and 65535");
                _TcpPort = value;
            }
        }

        /// <summary>
        /// Session timeout in seconds for HTTP server sessions.
        /// Default is 300 seconds (5 minutes). Minimum is 10 seconds.
        /// </summary>
        public int SessionTimeoutSeconds
        {
            get => _SessionTimeoutSeconds;
            set
            {
                if (value < 10)
                    throw new ArgumentOutOfRangeException(nameof(SessionTimeoutSeconds), "Session timeout must be at least 10 seconds");
                _SessionTimeoutSeconds = value;
            }
        }

        /// <summary>
        /// Enable CORS support on the HTTP server.
        /// Default is true for browser-based clients.
        /// </summary>
        public bool EnableCors
        {
            get => _EnableCors;
            set => _EnableCors = value;
        }

        /// <summary>
        /// Server name advertised in MCP initialize response.
        /// Default is "Conductor.McpServer".
        /// </summary>
        public string ServerName
        {
            get => _ServerName;
            set => _ServerName = (String.IsNullOrEmpty(value) ? "Conductor.McpServer" : value);
        }

        /// <summary>
        /// Server version advertised in MCP initialize response.
        /// Default is "1.0.0".
        /// </summary>
        public string ServerVersion
        {
            get => _ServerVersion;
            set => _ServerVersion = (String.IsNullOrEmpty(value) ? "1.0.0" : value);
        }

        #endregion

        #region Private-Members

        private bool _EnableHttpServer = true;
        private string _HttpHostname = "localhost";
        private int _HttpPort = 9001;
        private string _HttpRpcPath = "/mcp/rpc";
        private string _HttpEventsPath = "/mcp/events";
        private bool _EnableTcpServer = true;
        private string _TcpBindAddress = "127.0.0.1";
        private int _TcpPort = 9002;
        private int _SessionTimeoutSeconds = 300;
        private bool _EnableCors = true;
        private string _ServerName = "Conductor.McpServer";
        private string _ServerVersion = "1.0.0";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the MCP settings with default values.
        /// </summary>
        public McpSettings()
        {
        }

        #endregion
    }
}
