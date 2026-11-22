namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A response read by RawHttpClient. Not thread safe.
    /// </summary>
    public class RawHttpResponse
    {
        /// <summary>
        /// HTTP status code. Zero when no status line was received.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Response headers, case-insensitive. Never null.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Raw response body (chunk framing, if any, is left in place). Never null.
        /// </summary>
        public string Body { get; set; } = String.Empty;
    }
}
