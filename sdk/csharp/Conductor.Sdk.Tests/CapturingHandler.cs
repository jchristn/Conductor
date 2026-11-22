namespace Conductor.Sdk.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP handler that records every request and returns a canned response. Not thread safe.
    /// </summary>
    internal sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _Responder;

        /// <summary>
        /// Instantiate the handler.
        /// </summary>
        /// <param name="responder">Function that produces the response for a request.</param>
        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            ArgumentNullException.ThrowIfNull(responder);
            _Responder = responder;
        }

        /// <summary>
        /// Recorded requests, in send order.
        /// </summary>
        public List<CapturedRequest> Requests { get; } = new List<CapturedRequest>();

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            CapturedRequest captured = new CapturedRequest
            {
                Method = request.Method,
                Uri = request.RequestUri,
                Body = body
            };

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                captured.Headers[header.Key] = String.Join(",", header.Value);
            }

            Requests.Add(captured);
            return _Responder(request);
        }
    }
}
