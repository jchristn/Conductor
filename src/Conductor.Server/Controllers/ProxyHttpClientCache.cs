namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Conductor.Core.Models;

    internal static class ProxyHttpClientCache
    {
        private static readonly Dictionary<string, ProxyHttpClientCacheEntry> _HttpClients = new Dictionary<string, ProxyHttpClientCacheEntry>();
        private static readonly object _HttpClientLock = new object();

        internal static HttpClient GetHttpClient(ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            lock (_HttpClientLock)
            {
                if (_HttpClients.TryGetValue(endpoint.Id, out ProxyHttpClientCacheEntry existing))
                {
                    if (existing.TimeoutMs == endpoint.TimeoutMs)
                    {
                        return existing.Client;
                    }

                    existing.Client.Dispose();
                    _HttpClients.Remove(endpoint.Id);
                }

                HttpClient client = new HttpClient
                {
                    Timeout = TimeSpan.FromMilliseconds(endpoint.TimeoutMs)
                };

                _HttpClients[endpoint.Id] = new ProxyHttpClientCacheEntry
                {
                    Client = client,
                    TimeoutMs = endpoint.TimeoutMs
                };

                return client;
            }
        }

        private sealed class ProxyHttpClientCacheEntry
        {
            internal HttpClient Client { get; set; }
            internal int TimeoutMs { get; set; }
        }
    }
}
