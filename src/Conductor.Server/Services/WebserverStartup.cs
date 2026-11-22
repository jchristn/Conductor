namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Startup helpers for the Conductor webserver: response default header cleanup, a listener port pre-check, a start
    /// that fails fast when the listener cannot bind, and a start that runs background work only once the listener is up.
    /// All members are static and thread safe.
    /// </summary>
    public static class WebserverStartup
    {
        /// <summary>
        /// Request header names that Watson adds to every response by default. They describe requests, not responses, so
        /// Conductor removes them from the default response headers.
        /// </summary>
        public static readonly IReadOnlyList<string> RequestOnlyHeaderNames = new List<string>
        {
            "Accept",
            "Accept-Language",
            "Accept-Charset",
            "Host"
        };

        /// <summary>
        /// Remove request-only header names (RequestOnlyHeaderNames) from the default response headers, so responses no
        /// longer echo Host, Accept, Accept-Language or Accept-Charset. Other defaults (including CORS and Cache-Control)
        /// are kept. Call it with the settings of a constructed Webserver (Webserver.Settings), because the Webserver
        /// constructor sets the Host default.
        /// </summary>
        /// <param name="settings">Webserver settings to update. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public static void RemoveRequestOnlyDefaultHeaders(WebserverSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Dictionary<string, string> defaults = settings.Headers?.DefaultHeaders;
            if (defaults == null) return;

            List<string> keys = defaults.Keys
                .Where(key => RequestOnlyHeaderNames.Any(name => String.Equals(name, key, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            foreach (string key in keys) defaults.Remove(key);
        }

        /// <summary>
        /// Check whether a TCP listener can bind to a hostname and port right now. Wildcard hostnames (*, +, 0.0.0.0) are
        /// checked on all interfaces, localhost on the loopback interface. A hostname that cannot be resolved is reported
        /// as available, so the webserver itself reports the problem.
        /// </summary>
        /// <param name="hostname">Hostname or IP address. Null or empty is treated as all interfaces.</param>
        /// <param name="port">Port, 1 to 65535.</param>
        /// <returns>True when the port can be bound.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when port is outside 1 to 65535.</exception>
        public static bool IsPortAvailable(string hostname, int port)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            IPAddress address = ResolveBindAddress(hostname);
            if (address == null) return true;

            TcpListener listener = new TcpListener(address, port);
            try
            {
                listener.ExclusiveAddressUse = true;
            }
            catch (SocketException)
            {
                // Not supported on every platform; the bind below still detects an active listener.
            }

            try
            {
                listener.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                try { listener.Stop(); } catch (SocketException) { }
            }
        }

        /// <summary>
        /// Start the webserver and wait until it is listening. Fails fast when the listener cannot bind (for example because
        /// another process already uses the port) instead of leaving a process that runs without serving requests.
        /// </summary>
        /// <param name="app">Webserver to start. Must not be null.</param>
        /// <param name="timeout">How long to wait for the listener. Minimum 100 ms.</param>
        /// <param name="token">Cancellation token passed to the webserver; cancelling it stops the server.</param>
        /// <returns>The task running the webserver, which completes when the server stops.</returns>
        /// <exception cref="ArgumentNullException">Thrown when app is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the webserver fails to start or is not listening within the timeout.</exception>
        public static async Task<Task> StartAndVerifyAsync(Webserver app, TimeSpan timeout, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(app);
            if (timeout < TimeSpan.FromMilliseconds(100)) timeout = TimeSpan.FromMilliseconds(100);

            Task serverTask = Task.Run(() => app.StartAsync(token), token);
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (serverTask.IsFaulted)
                {
                    Exception inner = serverTask.Exception?.GetBaseException();
                    throw new InvalidOperationException("The webserver failed to start: " + (inner?.Message ?? "unknown error"), inner);
                }

                if (app.IsListening) return serverTask;
                if (serverTask.IsCompleted) break;
                await Task.Delay(25, token).ConfigureAwait(false);
            }

            if (serverTask.IsFaulted)
            {
                Exception inner = serverTask.Exception?.GetBaseException();
                throw new InvalidOperationException("The webserver failed to start: " + (inner?.Message ?? "unknown error"), inner);
            }

            if (app.IsListening) return serverTask;
            throw new InvalidOperationException("The webserver did not start listening within " + (int)timeout.TotalMilliseconds + " ms.");
        }

        /// <summary>
        /// Start the webserver, wait until it is listening, and only then run the onListening callback, which starts
        /// background work such as health checks and keep-warm. When the listener does not start (port in use, bind failure
        /// or StartupTimeoutMs elapsed), the callback never runs, so no background work begins in a process that is about
        /// to exit.
        /// </summary>
        /// <param name="app">Webserver to start. Must not be null.</param>
        /// <param name="timeout">How long to wait for the listener. Minimum 100 ms.</param>
        /// <param name="onListening">Callback run once after the listener is verified. Must not be null. It receives the
        /// cancellation token passed to this method.</param>
        /// <param name="token">Cancellation token passed to the webserver and the callback; cancelling it stops the server.</param>
        /// <returns>The task running the webserver, which completes when the server stops.</returns>
        /// <exception cref="ArgumentNullException">Thrown when app or onListening is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the webserver fails to start or is not listening within
        /// the timeout. The callback has not run in that case.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the token is cancelled while waiting.</exception>
        public static async Task<Task> StartAndVerifyAsync(Webserver app, TimeSpan timeout, Func<CancellationToken, Task> onListening, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(onListening);

            Task serverTask = await StartAndVerifyAsync(app, timeout, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await onListening(token).ConfigureAwait(false);
            return serverTask;
        }

        private static IPAddress ResolveBindAddress(string hostname)
        {
            if (String.IsNullOrWhiteSpace(hostname) || hostname == "*" || hostname == "+" || hostname == "0.0.0.0") return IPAddress.Any;
            if (String.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase)) return IPAddress.Loopback;
            if (IPAddress.TryParse(hostname, out IPAddress parsed)) return parsed;

            try
            {
                IPAddress[] addresses = Dns.GetHostAddresses(hostname);
                return addresses.FirstOrDefault(item => item.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
            }
            catch (SocketException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
