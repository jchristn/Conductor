namespace Test.Shared.Server.Fakes
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A request received by a fake upstream server. Not thread safe; instances are recorded once and then read.
    /// </summary>
    public class FakeUpstreamRequest
    {
        /// <summary>
        /// HTTP method, for example POST.
        /// </summary>
        public string Method { get; set; } = null;

        /// <summary>
        /// Path without the query string.
        /// </summary>
        public string Path { get; set; } = null;

        /// <summary>
        /// Query string including the leading question mark, or empty.
        /// </summary>
        public string Query { get; set; } = String.Empty;

        /// <summary>
        /// Request headers keyed case-insensitively. Never null.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Request body as UTF-8 text. Empty when there is no body.
        /// </summary>
        public string Body { get; set; } = String.Empty;

        /// <summary>
        /// Get a header value.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <returns>The value, or null when absent.</returns>
        public string GetHeader(string name)
        {
            return Headers.TryGetValue(name, out string value) ? value : null;
        }
    }
}
