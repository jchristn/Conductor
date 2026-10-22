namespace Conductor.Server.Services
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// Default HTTP transport for model load requests.
    /// </summary>
    public class DefaultModelLoadTransport : IModelLoadTransport, IDisposable
    {
        private readonly HttpClient _HttpClient;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the default model load transport.
        /// The underlying HTTP client has no timeout of its own; each call is bounded by the timeoutMs passed to
        /// <see cref="SendAsync"/>, so model loads and pulls can run longer than HttpClient's 100-second default.
        /// </summary>
        public DefaultModelLoadTransport() : this(null)
        {
        }

        /// <summary>
        /// Instantiate the default model load transport with a custom message handler.
        /// </summary>
        /// <param name="handler">HTTP message handler, or null to use the default handler. Disposed with this transport.</param>
        public DefaultModelLoadTransport(HttpMessageHandler handler)
        {
            _HttpClient = handler == null ? new HttpClient() : new HttpClient(handler, true);
            _HttpClient.Timeout = Timeout.InfiniteTimeSpan;
        }

        /// <summary>
        /// Timeout applied by the underlying HTTP client. Always <see cref="Timeout.InfiniteTimeSpan"/>; per-call
        /// limits come from the timeoutMs argument of <see cref="SendAsync"/>.
        /// </summary>
        public TimeSpan ClientTimeout => _HttpClient.Timeout;

        /// <summary>
        /// Send a model load probe to an upstream endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="plan">Provider request plan.</param>
        /// <param name="timeoutMs">Request timeout in milliseconds. Bounds the whole call, including reading the body.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Transport response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpoint"/> or <paramref name="plan"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="timeoutMs"/> elapses or <paramref name="token"/> is cancelled.</exception>
        public async Task<ModelLoadTransportResponse> SendAsync(
            ModelRunnerEndpoint endpoint,
            ModelLoadProbePlan plan,
            int timeoutMs,
            CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            string url = endpoint.GetBaseUrl().TrimEnd('/') + NormalizePath(plan.Path);

            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                request.RequestUri = new Uri(url);
                request.Method = ResolveHttpMethod(plan.Method);

                if (!String.IsNullOrEmpty(plan.BodyJson))
                {
                    request.Content = new StringContent(plan.BodyJson, Encoding.UTF8, "application/json");
                }

                if (!String.IsNullOrEmpty(endpoint.ApiKey))
                {
                    if (endpoint.ApiType == ApiTypeEnum.Gemini)
                    {
                        request.Headers.TryAddWithoutValidation("x-goog-api-key", endpoint.ApiKey);
                    }
                    else
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + endpoint.ApiKey);
                    }
                }

                using (CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeoutSource.CancelAfter(timeoutMs);
                    using (HttpResponseMessage response = await _HttpClient.SendAsync(request, timeoutSource.Token).ConfigureAwait(false))
                    {
                        string body = null;
                        if (response.Content != null)
                        {
                            using (Stream stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token).ConfigureAwait(false))
                            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                            {
                                body = await reader.ReadToEndAsync().ConfigureAwait(false);
                            }
                        }

                        return new ModelLoadTransportResponse
                        {
                            StatusCode = (int)response.StatusCode,
                            Body = body
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">True when disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                _HttpClient.Dispose();
            }

            _Disposed = true;
        }

        private static string NormalizePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "/";
            return path.StartsWith("/") ? path : "/" + path;
        }

        private static HttpMethod ResolveHttpMethod(string method)
        {
            if (String.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)) return HttpMethod.Post;
            if (String.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)) return HttpMethod.Put;
            if (String.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase)) return HttpMethod.Delete;
            return HttpMethod.Get;
        }
    }
}
