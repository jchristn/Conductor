namespace Conductor.Core.Enums
{
    using System;

    /// <summary>
    /// Health check HTTP method enumeration.
    /// </summary>
    public enum HealthCheckMethodEnum
    {
        /// <summary>
        /// HTTP GET method.
        /// </summary>
        GET = 0,

        /// <summary>
        /// HTTP HEAD method.
        /// </summary>
        HEAD = 1,

        /// <summary>
        /// HTTP POST method. The endpoint's HealthCheckRequestBody is sent with Content-Type application/json.
        /// </summary>
        POST = 2
    }
}
