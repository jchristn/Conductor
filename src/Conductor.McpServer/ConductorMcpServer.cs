namespace Conductor.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;
    using Voltaic;

    /// <summary>
    /// Main MCP server for Conductor that exposes Conductor APIs as MCP tools.
    /// Supports both HTTP (with SSE) and TCP transports.
    /// </summary>
    public class ConductorMcpServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Event raised when a log message is generated.
        /// </summary>
        public event EventHandler<string> Log;

        /// <summary>
        /// Event raised when a client connects.
        /// </summary>
        public event EventHandler<ClientConnection> ClientConnected;

        /// <summary>
        /// Event raised when a client disconnects.
        /// </summary>
        public event EventHandler<ClientConnection> ClientDisconnected;

        /// <summary>
        /// Whether the server is currently running.
        /// </summary>
        public bool IsRunning
        {
            get => _IsRunning;
        }

        /// <summary>
        /// The HTTP server instance (null if HTTP is disabled).
        /// </summary>
        public McpHttpServer HttpServer
        {
            get => _HttpServer;
        }

        /// <summary>
        /// The TCP server instance (null if TCP is disabled).
        /// </summary>
        public McpTcpServer TcpServer
        {
            get => _TcpServer;
        }

        /// <summary>
        /// The tool registry containing all Conductor tool handlers.
        /// </summary>
        public ConductorToolRegistry ToolRegistry
        {
            get => _ToolRegistry;
        }

        /// <summary>
        /// The settings used to configure this server.
        /// </summary>
        public McpSettings Settings
        {
            get => _Settings;
        }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly McpSettings _Settings;
        private readonly ConductorToolRegistry _ToolRegistry;
        private McpHttpServer _HttpServer;
        private McpTcpServer _TcpServer;
        private CancellationTokenSource _TokenSource;
        private bool _IsRunning;
        private bool _Disposed;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the Conductor MCP server.
        /// </summary>
        /// <param name="database">Database driver for Conductor operations.</param>
        /// <param name="settings">MCP server settings. If null, default settings are used.</param>
        /// <exception cref="ArgumentNullException">Thrown if database is null.</exception>
        public ConductorMcpServer(DatabaseDriverBase database, McpSettings settings = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? new McpSettings();
            _ToolRegistry = new ConductorToolRegistry(database);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the MCP servers.
        /// </summary>
        /// <param name="token">Cancellation token for the operation.</param>
        /// <returns>A task representing the async operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the server is already running.</exception>
        public async Task StartAsync(CancellationToken token = default)
        {
            if (_IsRunning)
                throw new InvalidOperationException("Server is already running");

            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

            try
            {
                // Start HTTP server if enabled
                if (_Settings.EnableHttpServer)
                {
                    _HttpServer = new McpHttpServer(
                        _Settings.HttpHostname,
                        _Settings.HttpPort,
                        _Settings.HttpRpcPath,
                        _Settings.HttpEventsPath,
                        includeDefaultMethods: false);

                    _HttpServer.ServerName = _Settings.ServerName;
                    _HttpServer.ServerVersion = _Settings.ServerVersion;
                    _HttpServer.SessionTimeoutSeconds = _Settings.SessionTimeoutSeconds;
                    _HttpServer.EnableCors = _Settings.EnableCors;

                    // Register tools
                    _ToolRegistry.RegisterTools(_HttpServer);

                    // Wire up events
                    _HttpServer.Log += OnHttpLog;
                    _HttpServer.ClientConnected += OnHttpClientConnected;
                    _HttpServer.ClientDisconnected += OnHttpClientDisconnected;

                    // Start in background
                    _ = Task.Run(() => _HttpServer.StartAsync(_TokenSource.Token), _TokenSource.Token);

                    RaiseLog("HTTP MCP server started on " + _Settings.HttpHostname + ":" + _Settings.HttpPort);
                    RaiseLog("  RPC endpoint: " + _Settings.HttpRpcPath);
                    RaiseLog("  Events endpoint: " + _Settings.HttpEventsPath);
                }

                // Start TCP server if enabled
                if (_Settings.EnableTcpServer)
                {
                    IPAddress bindAddress = IPAddress.Parse(_Settings.TcpBindAddress);
                    _TcpServer = new McpTcpServer(bindAddress, _Settings.TcpPort, includeDefaultMethods: false);

                    _TcpServer.ServerName = _Settings.ServerName;
                    _TcpServer.ServerVersion = _Settings.ServerVersion;

                    // Register tools
                    _ToolRegistry.RegisterTools(_TcpServer);

                    // Wire up events
                    _TcpServer.Log += OnTcpLog;
                    _TcpServer.ClientConnected += OnTcpClientConnected;
                    _TcpServer.ClientDisconnected += OnTcpClientDisconnected;

                    // Start in background
                    _ = Task.Run(() => _TcpServer.StartAsync(_TokenSource.Token), _TokenSource.Token);

                    RaiseLog("TCP MCP server started on " + _Settings.TcpBindAddress + ":" + _Settings.TcpPort);
                }

                _IsRunning = true;

                // Allow servers to initialize
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RaiseLog("Failed to start server: " + ex.Message);
                Stop();
                throw;
            }
        }

        /// <summary>
        /// Stop the MCP servers.
        /// </summary>
        public void Stop()
        {
            if (!_IsRunning) return;

            _TokenSource?.Cancel();

            try
            {
                if (_HttpServer != null)
                {
                    _HttpServer.Log -= OnHttpLog;
                    _HttpServer.ClientConnected -= OnHttpClientConnected;
                    _HttpServer.ClientDisconnected -= OnHttpClientDisconnected;
                    _HttpServer.Stop();
                    RaiseLog("HTTP MCP server stopped");
                }
            }
            catch (Exception ex)
            {
                RaiseLog("Error stopping HTTP server: " + ex.Message);
            }

            try
            {
                if (_TcpServer != null)
                {
                    _TcpServer.Log -= OnTcpLog;
                    _TcpServer.ClientConnected -= OnTcpClientConnected;
                    _TcpServer.ClientDisconnected -= OnTcpClientDisconnected;
                    _TcpServer.Stop();
                    RaiseLog("TCP MCP server stopped");
                }
            }
            catch (Exception ex)
            {
                RaiseLog("Error stopping TCP server: " + ex.Message);
            }

            _IsRunning = false;
        }

        /// <summary>
        /// Configure health check functions from an external health service.
        /// </summary>
        /// <param name="getHealthState">Function to get health state for a specific endpoint.</param>
        /// <param name="getAllHealthStates">Function to get all health states for a tenant.</param>
        public void ConfigureHealthCheck(
            Func<string, EndpointHealthState> getHealthState,
            Func<string, List<EndpointHealthState>> getAllHealthStates)
        {
            _ToolRegistry.GetHealthStateFunc = getHealthState;
            _ToolRegistry.GetAllHealthStatesFunc = getAllHealthStates;
        }

        /// <summary>
        /// Broadcast a notification to all connected HTTP clients.
        /// </summary>
        /// <param name="method">The notification method name.</param>
        /// <param name="parameters">Optional parameters for the notification.</param>
        public void BroadcastNotification(string method, object parameters = null)
        {
            _HttpServer?.BroadcastNotification(method, parameters);
        }

        /// <summary>
        /// Get a list of all active HTTP session IDs.
        /// </summary>
        /// <returns>List of session IDs.</returns>
        public List<string> GetActiveHttpSessions()
        {
            return _HttpServer?.GetActiveSessions() ?? new List<string>();
        }

        /// <summary>
        /// Get a list of all connected TCP client IDs.
        /// </summary>
        /// <returns>List of client IDs.</returns>
        public List<string> GetConnectedTcpClients()
        {
            return _TcpServer?.GetConnectedClients() ?? new List<string>();
        }

        /// <summary>
        /// Releases all resources used by the ConductorMcpServer.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Releases resources used by the ConductorMcpServer.
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                Stop();
                _HttpServer?.Dispose();
                _TcpServer?.Dispose();
                _TokenSource?.Dispose();
            }

            _Disposed = true;
        }

        private void OnHttpLog(object sender, string message)
        {
            RaiseLog("[HTTP] " + message);
        }

        private void OnTcpLog(object sender, string message)
        {
            RaiseLog("[TCP] " + message);
        }

        private void OnHttpClientConnected(object sender, ClientConnection client)
        {
            RaiseLog("[HTTP] Client connected: " + client.SessionId);
            RaiseClientConnected(client);
        }

        private void OnHttpClientDisconnected(object sender, ClientConnection client)
        {
            RaiseLog("[HTTP] Client disconnected: " + client.SessionId);
            RaiseClientDisconnected(client);
        }

        private void OnTcpClientConnected(object sender, ClientConnection client)
        {
            RaiseLog("[TCP] Client connected: " + client.SessionId);
            RaiseClientConnected(client);
        }

        private void OnTcpClientDisconnected(object sender, ClientConnection client)
        {
            RaiseLog("[TCP] Client disconnected: " + client.SessionId);
            RaiseClientDisconnected(client);
        }

        private void RaiseLog(string message)
        {
            string formatted = "[" + DateTime.UtcNow.ToString("HH:mm:ss.fffZ") + "] [ConductorMcpServer] " + message;
            EventHandler<string> handler = Log;
            if (handler != null)
            {
                try
                {
                    handler(this, formatted);
                }
                catch
                {
                    // Swallow exceptions in event handlers
                }
            }
        }

        private void RaiseClientConnected(ClientConnection client)
        {
            EventHandler<ClientConnection> handler = ClientConnected;
            if (handler != null)
            {
                try
                {
                    handler(this, client);
                }
                catch
                {
                    // Swallow exceptions in event handlers
                }
            }
        }

        private void RaiseClientDisconnected(ClientConnection client)
        {
            EventHandler<ClientConnection> handler = ClientDisconnected;
            if (handler != null)
            {
                try
                {
                    handler(this, client);
                }
                catch
                {
                    // Swallow exceptions in event handlers
                }
            }
        }

        #endregion
    }
}
