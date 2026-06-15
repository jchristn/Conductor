namespace Conductor.Sdk
{
    using System;
    using System.Net;

    /// <summary>
    /// Exception thrown when the Conductor API returns an unsuccessful response.
    /// </summary>
    public sealed class ConductorApiException : Exception
    {
        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="endpoint">Endpoint path.</param>
        /// <param name="responseBody">Response body.</param>
        public ConductorApiException(string message, HttpStatusCode statusCode, string endpoint, string responseBody)
            : base(message)
        {
            StatusCode = statusCode;
            Endpoint = endpoint;
            ResponseBody = responseBody;
        }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Endpoint path.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Response body.
        /// </summary>
        public string ResponseBody { get; }
    }
}
