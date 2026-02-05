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
