namespace Conductor.Core.Models
{
    using System;

    /// <summary>
    /// Standard API error response.
    /// </summary>
    public class ApiErrorResponse
    {
        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Error code for programmatic handling.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// UTC timestamp of the error.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Create a BadGateway error response.
        /// </summary>
        public static ApiErrorResponse BadGateway(string message = "No model runners available")
        {
            return new ApiErrorResponse
            {
                Error = message,
                StatusCode = 502,
                Code = "BadGateway"
            };
        }

        /// <summary>
        /// Create a NotFound error response.
        /// </summary>
        public static ApiErrorResponse NotFound(string message = "Not found")
        {
            return new ApiErrorResponse
            {
                Error = message,
                StatusCode = 404,
                Code = "NotFound"
            };
        }

        /// <summary>
        /// Create a Forbidden error response.
        /// </summary>
        public static ApiErrorResponse Forbidden(string message = "Forbidden")
        {
            return new ApiErrorResponse
            {
                Error = message,
                StatusCode = 403,
                Code = "Forbidden"
            };
        }

        /// <summary>
        /// Create a ServiceUnavailable error response.
        /// </summary>
        public static ApiErrorResponse ServiceUnavailable(string message = "Service unavailable")
        {
            return new ApiErrorResponse
            {
                Error = message,
                StatusCode = 503,
                Code = "ServiceUnavailable"
            };
        }

        /// <summary>
        /// Create a TooManyRequests error response.
        /// </summary>
        public static ApiErrorResponse TooManyRequests(string message = "All endpoints at capacity")
        {
            return new ApiErrorResponse
            {
                Error = message,
                StatusCode = 429,
                Code = "TooManyRequests"
            };
        }

        /// <summary>
        /// Create an Unauthorized error response.
        /// </summary>
        public static ApiErrorResponse Unauthorized(string message = "Not authorized")
        {
            return new ApiErrorResponse
            {
                Error = message,
                StatusCode = 401,
                Code = "NotAuthorized"
            };
        }
    }
}
