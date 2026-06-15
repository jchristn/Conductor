namespace Test.Shared.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Server.Controllers;
    using FluentAssertions;
    using WatsonWebserver.Core;

    /// <summary>
    /// Unit tests for AnalyticsSavedReportController.
    /// </summary>
    public class AnalyticsSavedReportControllerTests : ControllerTestBase
    {
        private AnalyticsSavedReportController _Controller;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Controller = new AnalyticsSavedReportController(Database, AuthService, Serializer, Logging);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Verify valid saved reports are persisted after scope normalization.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Create_WithValidAnalyticsReport_PersistsTenantScope()
        {
            AnalyticsSavedReport report = new AnalyticsSavedReport
            {
                Name = "User cost last day",
                Query = new AnalyticsQueryRequest
                {
                    Range = "lastDay",
                    TokenUnitCost = 0.00001m,
                    GroupBy = new List<string> { "RequestorUserId" }
                },
                DisplayState = new Dictionary<string, string>
                {
                    ["workspace"] = "Analytics",
                    ["chart"] = "VolumeAndTtft"
                },
                Labels = new List<string> { "analytics" },
                Tags = new Dictionary<string, string> { ["range"] = "lastDay" }
            };

            AnalyticsSavedReport created = await _Controller.Create(TestTenantId, TestUserId, report).ConfigureAwait(false);

            created.Id.Should().StartWith("asr_");
            created.TenantId.Should().Be(TestTenantId);
            created.OwnerUserId.Should().Be(TestUserId);
            created.Scope.Should().Be("Tenant");
        }

        /// <summary>
        /// Verify saved reports cannot persist obvious raw payload or secret metadata fields.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Create_WithSensitiveDisplayStateMetadata_ThrowsBadRequest()
        {
            AnalyticsSavedReport report = new AnalyticsSavedReport
            {
                Name = "Unsafe report",
                DisplayState = new Dictionary<string, string>
                {
                    ["requestBody"] = "{\"prompt\":\"secret\"}"
                }
            };

            Func<Task> act = async () => await _Controller.Create(TestTenantId, TestUserId, report).ConfigureAwait(false);

            await act.Should()
                .ThrowAsync<WebserverException>()
                .WithMessage("*sensitive payload or secret metadata field*")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Verify saved reports cannot persist obvious secret-bearing metadata values.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Create_WithSensitiveTagValue_ThrowsBadRequest()
        {
            AnalyticsSavedReport report = new AnalyticsSavedReport
            {
                Name = "Unsafe tag",
                Tags = new Dictionary<string, string>
                {
                    ["note"] = "Authorization: Bearer secret"
                }
            };

            Func<Task> act = async () => await _Controller.Create(TestTenantId, TestUserId, report).ConfigureAwait(false);

            await act.Should()
                .ThrowAsync<WebserverException>()
                .WithMessage("*sensitive payload or secret metadata value*")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Verify saved report query definitions use the same Analytics query validation.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Create_WithInvalidQueryDefinition_ThrowsBadRequest()
        {
            AnalyticsSavedReport report = new AnalyticsSavedReport
            {
                Name = "Invalid query",
                Query = new AnalyticsQueryRequest
                {
                    Metrics = new List<string> { "tokens.invalid" }
                }
            };

            Func<Task> act = async () => await _Controller.Create(TestTenantId, TestUserId, report).ConfigureAwait(false);

            await act.Should()
                .ThrowAsync<WebserverException>()
                .WithMessage("*Unsupported analytics metric*")
                .ConfigureAwait(false);
        }
    }
}
