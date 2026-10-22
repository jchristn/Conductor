namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using Conductor.Core.Models;

    internal static class ProxyHttpClientCache
    {
        private static readonly Dictionary<string, HttpClient> _HttpClients = new Dictionary<string, HttpClient>();
        private static readonly object _HttpClientLock = new object();

        /// <summary>
        /// Get the shared HTTP client for an endpoint. The client has no timeout of its own: the proxy applies the
        /// effective time-to-first-byte limit per request, so it can tell its own deadline apart from other failures.
        /// Because the client does not depend on the endpoint's TimeoutMs, editing that value never disposes a client
        /// that in-flight requests are still using.
        /// </summary>
        internal static HttpClient GetHttpClient(ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            lock (_HttpClientLock)
            {
                if (_HttpClients.TryGetValue(endpoint.Id, out HttpClient existing))
                {
                    return existing;
                }

                HttpClient client = new HttpClient
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };

                _HttpClients[endpoint.Id] = client;
                return client;
            }
        }
    }
}
