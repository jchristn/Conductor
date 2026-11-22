namespace Conductor.Sdk.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    /// <summary>
    /// A request recorded by <see cref="CapturingHandler"/>.
    /// </summary>
    internal sealed class CapturedRequest
    {
        /// <summary>
        /// HTTP method.
        /// </summary>
        public HttpMethod Method { get; set; }

        /// <summary>
        /// Absolute request URI.
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// Request body, or an empty string when there is none.
        /// </summary>
        public string Body { get; set; } = "";

        /// <summary>
        /// Request headers, keyed case-insensitively.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
