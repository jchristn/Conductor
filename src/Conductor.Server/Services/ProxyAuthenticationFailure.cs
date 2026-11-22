namespace Conductor.Server.Services
{
    using System;

    /// <summary>
    /// Marker placed in the HTTP context metadata when authentication failed for a request to an Anthropic virtual model
    /// runner. The route-level authentication lets such requests through so the proxy can answer with an Anthropic-shaped
    /// authentication_error instead of the webserver's generic 401. Immutable and thread safe.
    /// </summary>
    public sealed class ProxyAuthenticationFailure
    {
        /// <summary>
        /// The failure message. Never null.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Instantiate the marker.
        /// </summary>
        /// <param name="message">Failure message. Null uses a default message.</param>
        public ProxyAuthenticationFailure(string message)
        {
            Message = String.IsNullOrEmpty(message) ? "Authentication failed." : message;
        }
    }
}
