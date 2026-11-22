namespace Conductor.Server.Services
{
    using System;
    using Conductor.Core.Models;
    using Conductor.Core.ThirdParty.Anthropic.Models;

    /// <summary>
    /// Maps errors that Conductor itself generates on Anthropic virtual model runners to the Anthropic error shape
    /// {"type":"error","error":{"type","message"}}. Upstream errors are never mapped; they pass through unchanged.
    /// All members are static and thread safe.
    /// </summary>
    public static class AnthropicErrorMapper
    {
        /// <summary>Anthropic error type for authentication failures (HTTP 401).</summary>
        public const string AuthenticationError = "authentication_error";

        /// <summary>Anthropic error type for access denials (HTTP 403).</summary>
        public const string PermissionError = "permission_error";

        /// <summary>Anthropic error type for missing or unsupported resources (HTTP 404).</summary>
        public const string NotFoundError = "not_found_error";

        /// <summary>Anthropic error type for QoS and capacity rejections (HTTP 429).</summary>
        public const string RateLimitError = "rate_limit_error";

        /// <summary>Anthropic error type for upstream failures and timeouts.</summary>
        public const string ApiError = "api_error";

        /// <summary>Anthropic error type when no healthy endpoint can serve the request.</summary>
        public const string OverloadedError = "overloaded_error";

        /// <summary>Anthropic error type for malformed requests (HTTP 400 and other 4xx).</summary>
        public const string InvalidRequestError = "invalid_request_error";

        /// <summary>
        /// Map an HTTP status code and optional Conductor reason code to an Anthropic error type.
        /// Reason codes that mean no healthy endpoint is available (NoHealthyEndpointsAvailable, AllEndpointsQuarantined,
        /// AllEndpointsDraining, AllEndpointsInTransientBackoff, NoEndpointGroupAvailable, NoHealthyEndpoint) map to
        /// overloaded_error regardless of status code.
        /// </summary>
        /// <param name="statusCode">HTTP status code Conductor returns.</param>
        /// <param name="reasonCode">Conductor reason or denial code. Null is allowed.</param>
        /// <returns>The Anthropic error type. Never null.</returns>
        public static string MapErrorType(int statusCode, string reasonCode)
        {
            if (IsNoHealthyEndpointReason(reasonCode)) return OverloadedError;

            switch (statusCode)
            {
                case 401:
                    return AuthenticationError;
                case 403:
                    return PermissionError;
                case 404:
                    return NotFoundError;
                case 429:
                    return RateLimitError;
                case 503:
                    return OverloadedError;
                default:
                    if (statusCode >= 500) return ApiError;
                    if (statusCode >= 400) return InvalidRequestError;
                    return ApiError;
            }
        }

        /// <summary>
        /// Build an Anthropic-shaped error from a Conductor error response.
        /// </summary>
        /// <param name="error">Conductor error. Must not be null.</param>
        /// <param name="reasonCode">Conductor reason or denial code used to refine the mapping. Null falls back to error.Code.</param>
        /// <returns>The Anthropic error response. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when error is null.</exception>
        public static AnthropicErrorResponse Build(ApiErrorResponse error, string reasonCode = null)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new AnthropicErrorResponse
            {
                Type = "error",
                Error = new AnthropicErrorDetail
                {
                    Type = MapErrorType(error.StatusCode, reasonCode ?? error.Code),
                    Message = String.IsNullOrEmpty(error.Error) ? "Request failed." : error.Error
                }
            };
        }

        private static bool IsNoHealthyEndpointReason(string reasonCode)
        {
            if (String.IsNullOrEmpty(reasonCode)) return false;
            return String.Equals(reasonCode, "NoHealthyEndpointsAvailable", StringComparison.Ordinal)
                || String.Equals(reasonCode, "AllEndpointsQuarantined", StringComparison.Ordinal)
                || String.Equals(reasonCode, "AllEndpointsDraining", StringComparison.Ordinal)
                || String.Equals(reasonCode, "AllEndpointsInTransientBackoff", StringComparison.Ordinal)
                || String.Equals(reasonCode, "NoEndpointGroupAvailable", StringComparison.Ordinal)
                || String.Equals(reasonCode, "NoHealthyEndpoint", StringComparison.Ordinal);
        }
    }
}
