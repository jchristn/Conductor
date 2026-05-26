namespace Test.Shared.Server.Controllers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for backup validation behavior.
    /// </summary>
    public class BackupControllerTests : ControllerTestBase
    {
        private BackupController _Controller;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            RoutingDecisionService routingDecisionService = new RoutingDecisionService(Database, Logging);
            ConfigurationValidationService validationService = new ConfigurationValidationService(Database, Logging, routingDecisionService);
            _Controller = new BackupController(Database, AuthService, Serializer, Logging, validationService);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ValidateBackup_WithInvalidVirtualModelRunner_ReturnsValidationError()
        {
            BackupPackage package = new BackupPackage
            {
                SchemaVersion = "1.0",
                VirtualModelRunners = new List<VirtualModelRunner>
                {
                    new VirtualModelRunner
                    {
                        TenantId = TestTenantId,
                        Name = "Broken Strict VMR",
                        BasePath = "/v1.0/api/broken-strict/",
                        StrictMode = true
                    }
                }
            };

            ValidationResult result = await _Controller.ValidateBackup(package).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(item => item.Contains("Strict mode requires at least one attached model definition."));
        }
    }
}
