namespace Test.Shared.Server.Fakes
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A response a fake upstream server sends. When StreamChunks is set, the body is sent with chunked transfer encoding,
    /// one write and flush per chunk. Not thread safe.
    /// </summary>
    public class FakeUpstreamResponse
    {
        /// <summary>
        /// HTTP status code. Default: 200.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Content type. Default: application/json.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Extra response headers. Never null.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Non-streamed body. Null sends an empty body.
        /// </summary>
        public string Body { get; set; } = null;

        /// <summary>
        /// Streamed body chunks. Null for a non-streamed response.
        /// </summary>
        public List<string> StreamChunks { get; set; } = null;
    }
}
