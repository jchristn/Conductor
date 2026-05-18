namespace Test.Shared.Core.Models
{
    using System;
    using Conductor.Core.Models;
    using FluentAssertions;
    
    /// <summary>
    /// Unit tests for ApiErrorResponse.
    /// </summary>
    public class ApiErrorResponseTests
    {
        #region Default-Value-Tests
        public void Timestamp_HasDefaultValue()
        {
            DateTime before = DateTime.UtcNow;
            ApiErrorResponse response = new ApiErrorResponse();
            DateTime after = DateTime.UtcNow;

            response.Timestamp.Should().BeOnOrAfter(before);
            response.Timestamp.Should().BeOnOrBefore(after);
        }

        #endregion

        #region Property-Setting-Tests
        public void CanSetError()
        {
            ApiErrorResponse response = new ApiErrorResponse { Error = "Test error" };
            response.Error.Should().Be("Test error");
        }
        public void CanSetStatusCode()
        {
            ApiErrorResponse response = new ApiErrorResponse { StatusCode = 500 };
            response.StatusCode.Should().Be(500);
        }
        public void CanSetCode()
        {
            ApiErrorResponse response = new ApiErrorResponse { Code = "InternalError" };
            response.Code.Should().Be("InternalError");
        }
        public void CanSetTimestamp()
        {
            DateTime timestamp = DateTime.UtcNow.AddHours(-1);
            ApiErrorResponse response = new ApiErrorResponse { Timestamp = timestamp };
            response.Timestamp.Should().Be(timestamp);
        }

        #endregion

        #region BadGateway-Tests
        public void BadGateway_WithDefaultMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.BadGateway();
            response.StatusCode.Should().Be(502);
            response.Code.Should().Be("BadGateway");
            response.Error.Should().Be("No model runners available");
        }
        public void BadGateway_WithCustomMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.BadGateway("Custom error");
            response.StatusCode.Should().Be(502);
            response.Code.Should().Be("BadGateway");
            response.Error.Should().Be("Custom error");
        }

        #endregion

        #region NotFound-Tests
        public void NotFound_WithDefaultMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.NotFound();
            response.StatusCode.Should().Be(404);
            response.Code.Should().Be("NotFound");
            response.Error.Should().Be("Not found");
        }
        public void NotFound_WithCustomMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.NotFound("Resource not found");
            response.StatusCode.Should().Be(404);
            response.Error.Should().Be("Resource not found");
        }

        #endregion

        #region Forbidden-Tests
        public void Forbidden_WithDefaultMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.Forbidden();
            response.StatusCode.Should().Be(403);
            response.Code.Should().Be("Forbidden");
            response.Error.Should().Be("Forbidden");
        }
        public void Forbidden_WithCustomMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.Forbidden("Access denied");
            response.StatusCode.Should().Be(403);
            response.Error.Should().Be("Access denied");
        }

        #endregion

        #region ServiceUnavailable-Tests
        public void ServiceUnavailable_WithDefaultMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.ServiceUnavailable();
            response.StatusCode.Should().Be(503);
            response.Code.Should().Be("ServiceUnavailable");
            response.Error.Should().Be("Service unavailable");
        }
        public void ServiceUnavailable_WithCustomMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.ServiceUnavailable("Server overloaded");
            response.StatusCode.Should().Be(503);
            response.Error.Should().Be("Server overloaded");
        }

        #endregion

        #region TooManyRequests-Tests
        public void TooManyRequests_WithDefaultMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.TooManyRequests();
            response.StatusCode.Should().Be(429);
            response.Code.Should().Be("TooManyRequests");
            response.Error.Should().Be("All endpoints at capacity");
        }
        public void TooManyRequests_WithCustomMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.TooManyRequests("Rate limit exceeded");
            response.StatusCode.Should().Be(429);
            response.Error.Should().Be("Rate limit exceeded");
        }

        #endregion

        #region Unauthorized-Tests
        public void Unauthorized_WithDefaultMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.Unauthorized();
            response.StatusCode.Should().Be(401);
            response.Code.Should().Be("NotAuthorized");
            response.Error.Should().Be("Not authorized");
        }
        public void Unauthorized_WithCustomMessage_ReturnsCorrectResponse()
        {
            ApiErrorResponse response = ApiErrorResponse.Unauthorized("Invalid credentials");
            response.StatusCode.Should().Be(401);
            response.Error.Should().Be("Invalid credentials");
        }

        #endregion
    }
}
