namespace Conductor.Core.Tests.Settings
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Settings;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for ServerSettings.
    /// </summary>
    public class ServerSettingsTests
    {
        #region Required-Property-Tests

        [Fact]
        public void Webserver_WhenNull_ThrowsArgumentNullException()
        {
            ServerSettings settings = new ServerSettings();
            Action act = () => settings.Webserver = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Webserver");
        }

        [Fact]
        public void Database_WhenNull_ThrowsArgumentNullException()
        {
            ServerSettings settings = new ServerSettings();
            Action act = () => settings.Database = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Database");
        }

        [Fact]
        public void Logging_WhenNull_ThrowsArgumentNullException()
        {
            ServerSettings settings = new ServerSettings();
            Action act = () => settings.Logging = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Logging");
        }

        #endregion

        #region Default-Value-Tests

        [Fact]
        public void Webserver_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.Webserver.Should().NotBeNull();
        }

        [Fact]
        public void Database_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.Database.Should().NotBeNull();
        }

        [Fact]
        public void Logging_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.Logging.Should().NotBeNull();
        }

        [Fact]
        public void AdminApiKeys_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.AdminApiKeys.Should().NotBeNull();
            settings.AdminApiKeys.Should().Contain("conductoradmin");
        }

        [Fact]
        public void Webserver_Cors_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.Webserver.Cors.Should().NotBeNull();
            settings.Webserver.Cors.Enabled.Should().BeFalse();
        }

        #endregion

        #region Null-Coalescing-Tests

        [Fact]
        public void AdminApiKeys_WhenSetToNull_BecomesEmptyList()
        {
            ServerSettings settings = new ServerSettings();
            settings.AdminApiKeys = null;
            settings.AdminApiKeys.Should().NotBeNull();
            settings.AdminApiKeys.Should().BeEmpty();
        }

        [Fact]
        public void Webserver_Cors_WhenSetToNull_BecomesNewCorsSettings()
        {
            ServerSettings settings = new ServerSettings();
            settings.Webserver.Cors = null;
            settings.Webserver.Cors.Should().NotBeNull();
        }

        #endregion

        #region Assignment-Tests

        [Fact]
        public void CanAssignWebserverSettings()
        {
            ServerSettings settings = new ServerSettings();
            WebserverSettings webserver = new WebserverSettings
            {
                Hostname = "0.0.0.0",
                Port = 8080
            };
            settings.Webserver = webserver;
            settings.Webserver.Hostname.Should().Be("0.0.0.0");
            settings.Webserver.Port.Should().Be(8080);
        }

        [Fact]
        public void CanAssignDatabaseSettings()
        {
            ServerSettings settings = new ServerSettings();
            DatabaseSettings database = new DatabaseSettings
            {
                Type = Core.Enums.DatabaseTypeEnum.PostgreSql,
                Hostname = "db.example.com"
            };
            settings.Database = database;
            settings.Database.Type.Should().Be(Core.Enums.DatabaseTypeEnum.PostgreSql);
        }

        [Fact]
        public void CanAssignCorsSettingsToWebserver()
        {
            ServerSettings settings = new ServerSettings();
            CorsSettings cors = new CorsSettings
            {
                Enabled = true,
                AllowedOrigins = new List<string> { "*" }
            };
            settings.Webserver.Cors = cors;
            settings.Webserver.Cors.Enabled.Should().BeTrue();
            settings.Webserver.Cors.AllowedOrigins.Should().Contain("*");
        }

        #endregion
    }
}
