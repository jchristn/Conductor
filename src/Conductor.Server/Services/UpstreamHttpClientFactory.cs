namespace Conductor.Server.Services
{
    using System;
    using System.Net.Http;

    /// <summary>
    /// Builds the HTTP clients and handlers Conductor uses for calls to model runner endpoints (proxying, health checks,
    /// model load probes and RigMonitor). Every handler has cookies disabled: Conductor is a multi-tenant proxy and never
    /// stores a Set-Cookie value from an upstream response or replays it on a later request, so one caller's upstream
    /// session cannot leak into another caller's requests. All members are static and thread safe.
    /// </summary>
    public static class UpstreamHttpClientFactory
    {
        /// <summary>
        /// Create a new handler for upstream calls with cookie handling disabled (UseCookies is false), so no cookie
        /// container exists and no Cookie header is added unless a caller sets one explicitly on a request.
        /// </summary>
        /// <returns>A new handler owned by the caller. Never null.</returns>
        public static SocketsHttpHandler CreateHandler()
        {
            return new SocketsHttpHandler
            {
                UseCookies = false
            };
        }

        /// <summary>
        /// Create a new HTTP client for upstream calls that owns a cookie-free handler from CreateHandler.
        /// </summary>
        /// <param name="timeout">Client timeout. Null keeps the HttpClient default of 100 seconds.</param>
        /// <returns>A new client owned by the caller. Never null.</returns>
        public static HttpClient CreateClient(TimeSpan? timeout = null)
        {
            HttpClient client = new HttpClient(CreateHandler(), true);
            if (timeout.HasValue) client.Timeout = timeout.Value;
            return client;
        }
    }
}
