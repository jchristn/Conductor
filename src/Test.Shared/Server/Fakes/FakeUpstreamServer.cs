namespace Test.Shared.Server.Fakes
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A minimal HTTP server bound to 127.0.0.1 on a free port, used as a fake model provider in tests. Every request is
    /// recorded and answered by Handler. Safe for concurrent requests; Handler must be thread safe.
    /// </summary>
    public sealed class FakeUpstreamServer : IDisposable
    {
        /// <summary>
        /// The loopback hostname the server binds to.
        /// </summary>
        public string Hostname { get; } = "127.0.0.1";

        /// <summary>
        /// The bound port.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Requests received, in arrival order. Never null.
        /// </summary>
        public ConcurrentQueue<FakeUpstreamRequest> Requests { get; } = new ConcurrentQueue<FakeUpstreamRequest>();

        /// <summary>
        /// Produces the response for a request. Default: 404 with an empty JSON object.
        /// </summary>
        public Func<FakeUpstreamRequest, FakeUpstreamResponse> Handler { get; set; } = request => new FakeUpstreamResponse { StatusCode = 404, Body = "{}" };

        private readonly HttpListener _Listener;
        private readonly CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private Task _AcceptTask;
        private bool _Disposed;

        /// <summary>
        /// Instantiate and start the server on a free loopback port.
        /// </summary>
        public FakeUpstreamServer()
        {
            Port = GetFreePort();
            _Listener = new HttpListener();
            _Listener.Prefixes.Add("http://" + Hostname + ":" + Port + "/");
            _Listener.Start();
            _AcceptTask = Task.Run(() => AcceptLoopAsync(_TokenSource.Token));
        }

        /// <summary>
        /// Get a free TCP port on the loopback interface.
        /// </summary>
        /// <returns>Port number.</returns>
        public static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        /// <summary>
        /// Get the requests received so far as a list.
        /// </summary>
        /// <returns>A new list. Never null.</returns>
        public List<FakeUpstreamRequest> GetRequests()
        {
            return new List<FakeUpstreamRequest>(Requests);
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _TokenSource.Cancel();
            try { _Listener.Stop(); } catch (ObjectDisposedException) { }
            try { _Listener.Close(); } catch (ObjectDisposedException) { }
            try { _AcceptTask?.Wait(2000); } catch (AggregateException) { }
            _TokenSource.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (token.IsCancellationRequested || !_Listener.IsListening)
                {
                    return;
                }
                catch (HttpListenerException)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(context, token));
            }
        }

        private async Task HandleAsync(HttpListenerContext context, CancellationToken token)
        {
            try
            {
                FakeUpstreamRequest request = new FakeUpstreamRequest
                {
                    Method = context.Request.HttpMethod,
                    Path = context.Request.Url.AbsolutePath,
                    Query = context.Request.Url.Query ?? String.Empty
                };

                foreach (string key in context.Request.Headers.AllKeys)
                {
                    if (key != null) request.Headers[key] = context.Request.Headers[key];
                }

                if (context.Request.HasEntityBody)
                {
                    using (StreamReader reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        request.Body = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                    }
                }

                Requests.Enqueue(request);
                FakeUpstreamResponse response = Handler(request) ?? new FakeUpstreamResponse { StatusCode = 500, Body = "{}" };

                context.Response.StatusCode = response.StatusCode;
                context.Response.ContentType = response.ContentType;
                foreach (KeyValuePair<string, string> header in response.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }

                if (response.StreamChunks != null)
                {
                    context.Response.SendChunked = true;
                    foreach (string chunk in response.StreamChunks)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(chunk);
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
                        await context.Response.OutputStream.FlushAsync(token).ConfigureAwait(false);
                        await Task.Delay(5, token).ConfigureAwait(false);
                    }
                }
                else
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(response.Body ?? String.Empty);
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
                }

                context.Response.Close();
            }
            catch (Exception)
            {
                try { context.Response.Abort(); } catch (Exception) { }
            }
        }
    }
}
