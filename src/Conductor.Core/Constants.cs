namespace Conductor.Core
{
    /// <summary>
    /// Application constants.
    /// </summary>
    public static class Constants
    {
        #region Public-Members

        /// <summary>
        /// ASCII art logo for the application.
        /// </summary>
        public static string Logo =
"""

                     _            _
  ___ ___  _ __   __| |_   _  ___| |_ ___  _ __
 / __/ _ \| '_ \ / _` | | | |/ __| __/ _ \| '__|
| (_| (_) | | | | (_| | |_| | (__| || (_) | |
 \___\___/|_| |_|\__,_|\__,_|\___|\__\___/|_|

""";

        /// <summary>
        /// Default time to first byte, in milliseconds, for virtual model runners and model runner endpoints.
        /// 300000 (5 minutes) matches Ollama's default model load timeout, so a cold model load can finish.
        /// </summary>
        public const int DefaultRequestTimeoutMs = 300000;

        /// <summary>
        /// Default request timeout, in milliseconds, used by Conductor releases before v0.6.1.
        /// Records still set to this value are reported at startup because cold loads of large models often exceed it.
        /// </summary>
        public const int LegacyDefaultRequestTimeoutMs = 60000;

        #endregion

        #region Response-Headers

        /// <summary>
        /// Response header key for the virtual model runner ID.
        /// </summary>
        public static string HeaderVmrId = "X-Conductor-Vmr-Id";

        /// <summary>
        /// Response header key for the model endpoint ID.
        /// </summary>
        public static string HeaderEndpointId = "X-Conductor-Endpoint-Id";

        /// <summary>
        /// Response header key for the model name.
        /// </summary>
        public static string HeaderModelName = "X-Conductor-Model-Name";

        /// <summary>
        /// Response header indicating whether session affinity was applied to route the request.
        /// Value is "true" when a pinned endpoint was used, "false" when load balancing was used.
        /// </summary>
        public static string HeaderSessionPinned = "X-Conductor-Session-Pinned";

        /// <summary>
        /// Response header carrying a Conductor error code when Conductor, rather than the upstream endpoint,
        /// classified the failure. Values include "UpstreamTimeout" and "RunnerLoadTimeout".
        /// </summary>
        public static string HeaderErrorCode = "X-Conductor-Error-Code";

        #endregion

        #region Proxy-Request-Headers

        /// <summary>
        /// Header key for forwarded client IP address.
        /// </summary>
        public static string HeaderXForwardedFor = "X-Forwarded-For";

        /// <summary>
        /// Header key for forwarded host.
        /// </summary>
        public static string HeaderXForwardedHost = "X-Forwarded-Host";

        /// <summary>
        /// Header key for forwarded protocol.
        /// </summary>
        public static string HeaderXForwardedProto = "X-Forwarded-Proto";

        #endregion
    }
}
