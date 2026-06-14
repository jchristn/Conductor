namespace Test.Shared.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;
    using Conductor.Core.Settings;
    using FluentAssertions;
    
    /// <summary>
    /// Unit tests for ServerSettings.
    /// </summary>
    public class ServerSettingsTests
    {
        #region Required-Property-Tests
        public void Webserver_WhenNull_ThrowsArgumentNullException()
        {
            ServerSettings settings = new ServerSettings();
            Action act = () => settings.Webserver = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Webserver");
        }
        public void Database_WhenNull_ThrowsArgumentNullException()
        {
            ServerSettings settings = new ServerSettings();
            Action act = () => settings.Database = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Database");
        }
        public void Logging_WhenNull_ThrowsArgumentNullException()
        {
            ServerSettings settings = new ServerSettings();
            Action act = () => settings.Logging = null;
            act.Should().Throw<ArgumentNullException>().WithParameterName("Logging");
        }

        #endregion

        #region Default-Value-Tests
        public void Webserver_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.Webserver.Should().NotBeNull();
        }
        public void Webserver_DefaultPort_Is9000()
        {
            ServerSettings settings = new ServerSettings();
            settings.Webserver.Port.Should().Be(9000);
        }
        public void Database_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.Database.Should().NotBeNull();
        }
        public void Logging_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.Logging.Should().NotBeNull();
        }
        public void RequestHistory_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.RequestHistory.Should().NotBeNull();
            settings.RequestHistory.Enabled.Should().BeTrue();
        }
        public void ModelAccessControl_HasCompatibleDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.ModelAccessControl.Should().NotBeNull();
            settings.ModelAccessControl.Enabled.Should().BeFalse();
            settings.ModelAccessControl.Mode.Should().Be(ModelAccessEnforcementModeEnum.Disabled);
            settings.ModelAccessControl.DefaultDecision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            settings.ModelAccessControl.RequireCredentialForProxy.Should().BeFalse();
            settings.ModelAccessControl.UnknownModelBehavior.Should().Be(ModelAccessUnknownModelBehaviorEnum.Deny);
            settings.ModelAccessControl.ListModelsBehavior.Should().Be(ModelAccessListModelsBehaviorEnum.Filter);
            settings.ModelAccessControl.CacheTtlMs.Should().Be(30000);
            settings.ModelAccessControl.AllowAdministratorBypass.Should().BeFalse();
            settings.ModelAccessControl.AllowGlobalAdministratorBypass.Should().BeFalse();
        }
        public void AdminApiKeys_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.AdminApiKeys.Should().NotBeNull();
            settings.AdminApiKeys.Should().Contain("conductoradmin");
        }
        public void Webserver_Cors_HasDefaultValue()
        {
            ServerSettings settings = new ServerSettings();
            settings.Webserver.Cors.Should().NotBeNull();
            settings.Webserver.Cors.Enabled.Should().BeFalse();
        }

        #endregion

        #region Null-Coalescing-Tests
        public void AdminApiKeys_WhenSetToNull_BecomesEmptyList()
        {
            ServerSettings settings = new ServerSettings();
            settings.AdminApiKeys = null;
            settings.AdminApiKeys.Should().NotBeNull();
            settings.AdminApiKeys.Should().BeEmpty();
        }
        public void Webserver_Cors_WhenSetToNull_BecomesNewCorsSettings()
        {
            ServerSettings settings = new ServerSettings();
            settings.Webserver.Cors = null;
            settings.Webserver.Cors.Should().NotBeNull();
        }
        public void ModelAccessControl_WhenSetToNull_BecomesNewSettings()
        {
            ServerSettings settings = new ServerSettings();
            settings.ModelAccessControl = null;
            settings.ModelAccessControl.Should().NotBeNull();
            settings.ModelAccessControl.Mode.Should().Be(ModelAccessEnforcementModeEnum.Disabled);
        }

        #endregion

        #region Assignment-Tests
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
        public void InvalidWebserverPort_RevertsToDefault()
        {
            WebserverSettings webserver = new WebserverSettings
            {
                Port = 0
            };
            webserver.Port.Should().Be(9000);
        }
        public void CanAssignDatabaseSettings()
        {
            ServerSettings settings = new ServerSettings();
            DatabaseSettings database = new DatabaseSettings
            {
                Type = Conductor.Core.Enums.DatabaseTypeEnum.PostgreSql,
                Hostname = "db.example.com"
            };
            settings.Database = database;
            settings.Database.Type.Should().Be(Conductor.Core.Enums.DatabaseTypeEnum.PostgreSql);
        }
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
        public void CanAssignModelAccessControlSettings()
        {
            ServerSettings settings = new ServerSettings();
            ModelAccessControlSettings modelAccessControl = new ModelAccessControlSettings
            {
                Enabled = true,
                Mode = ModelAccessEnforcementModeEnum.Monitor,
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny,
                CacheTtlMs = 1000
            };

            settings.ModelAccessControl = modelAccessControl;

            settings.ModelAccessControl.Enabled.Should().BeTrue();
            settings.ModelAccessControl.Mode.Should().Be(ModelAccessEnforcementModeEnum.Monitor);
            settings.ModelAccessControl.DefaultDecision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            settings.ModelAccessControl.CacheTtlMs.Should().Be(1000);
        }
        public void ModelAccessControlSettings_NegativeCacheTtl_RevertsToDefault()
        {
            ModelAccessControlSettings modelAccessControl = new ModelAccessControlSettings
            {
                CacheTtlMs = -1
            };

            modelAccessControl.CacheTtlMs.Should().Be(30000);
        }

        #endregion
    }
}
