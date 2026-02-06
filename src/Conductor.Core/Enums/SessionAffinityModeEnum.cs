namespace Conductor.Core.Enums
{
    using System;

    /// <summary>
    /// Session affinity mode enumeration.
    /// Controls how client identity is derived for sticky session routing.
    /// </summary>
    public enum SessionAffinityModeEnum
    {
        /// <summary>
        /// No session affinity (default). Requests are routed via normal load balancing.
        /// </summary>
        None = 0,

        /// <summary>
        /// Pin by client IP address (from X-Forwarded-For or direct connection).
        /// </summary>
        SourceIP = 1,

        /// <summary>
        /// Pin by the Authorization/Bearer token or API key presented.
        /// </summary>
        ApiKey = 2,

        /// <summary>
        /// Pin by a custom request header value specified by SessionAffinityHeader.
        /// </summary>
        Header = 3
    }
}
