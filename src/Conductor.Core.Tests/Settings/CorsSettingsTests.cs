namespace Conductor.Core.Tests.Settings
{
    using System.Collections.Generic;
    using Conductor.Core.Settings;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for CorsSettings.
    /// </summary>
    public class CorsSettingsTests
    {
        #region Default-Value-Tests

        [Fact]
        public void Enabled_DefaultsToFalse()
        {
            CorsSettings cors = new CorsSettings();
            cors.Enabled.Should().BeFalse();
        }

        [Fact]
        public void AllowedOrigins_DefaultsToEmptyList()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowedOrigins.Should().NotBeNull();
            cors.AllowedOrigins.Should().BeEmpty();
        }

        [Fact]
        public void AllowedMethods_HasDefaultValues()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowedMethods.Should().NotBeNull();
            cors.AllowedMethods.Should().Contain("GET");
            cors.AllowedMethods.Should().Contain("POST");
            cors.AllowedMethods.Should().Contain("PUT");
            cors.AllowedMethods.Should().Contain("DELETE");
            cors.AllowedMethods.Should().Contain("OPTIONS");
        }

        [Fact]
        public void AllowedHeaders_HasDefaultValues()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowedHeaders.Should().NotBeNull();
            cors.AllowedHeaders.Should().Contain("Content-Type");
            cors.AllowedHeaders.Should().Contain("Authorization");
        }

        [Fact]
        public void ExposedHeaders_DefaultsToEmptyList()
        {
            CorsSettings cors = new CorsSettings();
            cors.ExposedHeaders.Should().NotBeNull();
            cors.ExposedHeaders.Should().BeEmpty();
        }

        [Fact]
        public void AllowCredentials_DefaultsToFalse()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowCredentials.Should().BeFalse();
        }

        [Fact]
        public void MaxAgeSeconds_DefaultsTo86400()
        {
            CorsSettings cors = new CorsSettings();
            cors.MaxAgeSeconds.Should().Be(86400);
        }

        #endregion

        #region MaxAgeSeconds-Tests

        [Fact]
        public void MaxAgeSeconds_WhenNegative_ClampsToZero()
        {
            CorsSettings cors = new CorsSettings();
            cors.MaxAgeSeconds = -100;
            cors.MaxAgeSeconds.Should().Be(0);
        }

        [Fact]
        public void MaxAgeSeconds_WhenOverMax_ClampsTo86400()
        {
            CorsSettings cors = new CorsSettings();
            cors.MaxAgeSeconds = 100000;
            cors.MaxAgeSeconds.Should().Be(86400);
        }

        [Fact]
        public void MaxAgeSeconds_WhenValid_UsesProvidedValue()
        {
            CorsSettings cors = new CorsSettings();
            cors.MaxAgeSeconds = 3600;
            cors.MaxAgeSeconds.Should().Be(3600);
        }

        #endregion

        #region Null-Coalescing-Tests

        [Fact]
        public void AllowedOrigins_WhenSetToNull_BecomesEmptyList()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowedOrigins = null;
            cors.AllowedOrigins.Should().NotBeNull();
            cors.AllowedOrigins.Should().BeEmpty();
        }

        [Fact]
        public void AllowedMethods_WhenSetToNull_BecomesEmptyList()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowedMethods = null;
            cors.AllowedMethods.Should().NotBeNull();
            cors.AllowedMethods.Should().BeEmpty();
        }

        [Fact]
        public void AllowedHeaders_WhenSetToNull_BecomesEmptyList()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowedHeaders = null;
            cors.AllowedHeaders.Should().NotBeNull();
            cors.AllowedHeaders.Should().BeEmpty();
        }

        [Fact]
        public void ExposedHeaders_WhenSetToNull_BecomesEmptyList()
        {
            CorsSettings cors = new CorsSettings();
            cors.ExposedHeaders = null;
            cors.ExposedHeaders.Should().NotBeNull();
            cors.ExposedHeaders.Should().BeEmpty();
        }

        #endregion

        #region Configuration-Tests

        [Fact]
        public void CanSetAllowAllOrigins()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowedOrigins = new List<string> { "*" };
            cors.AllowedOrigins.Should().Contain("*");
        }

        [Fact]
        public void CanSetSpecificOrigins()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowedOrigins = new List<string>
            {
                "https://example.com",
                "https://app.example.com"
            };
            cors.AllowedOrigins.Should().HaveCount(2);
            cors.AllowedOrigins.Should().Contain("https://example.com");
        }

        [Fact]
        public void CanSetCustomHeaders()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowedHeaders = new List<string>
            {
                "Content-Type",
                "Authorization",
                "X-Custom-Header"
            };
            cors.AllowedHeaders.Should().Contain("X-Custom-Header");
        }

        [Fact]
        public void CanSetExposedHeaders()
        {
            CorsSettings cors = new CorsSettings();
            cors.ExposedHeaders = new List<string>
            {
                "X-Total-Count",
                "X-Page-Number"
            };
            cors.ExposedHeaders.Should().HaveCount(2);
        }

        [Fact]
        public void CanEnableCredentials()
        {
            CorsSettings cors = new CorsSettings();
            cors.AllowCredentials = true;
            cors.AllowCredentials.Should().BeTrue();
        }

        #endregion
    }
}
