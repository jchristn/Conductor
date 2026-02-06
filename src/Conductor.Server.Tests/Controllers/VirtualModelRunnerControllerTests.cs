namespace Conductor.Server.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Controllers;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for VirtualModelRunnerController.
    /// </summary>
    public class VirtualModelRunnerControllerTests : ControllerTestBase, IAsyncLifetime
    {
        private VirtualModelRunnerController _Controller;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Controller = new VirtualModelRunnerController(Database, AuthService, Serializer, Logging, null);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        #region Create-Tests

        [Fact]
        public async Task Create_WithNullBody_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Create(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Create_WithDefaultName_UsesDefault()
        {
            // VirtualModelRunner has a default Name value, so it doesn't throw
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                BasePath = "/test"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Should().NotBeNull();
            result.Name.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Create_WithoutBasePath_AutoGeneratesBasePath()
        {
            // VirtualModelRunner auto-generates BasePath when not provided
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Test VMR"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Should().NotBeNull();
            result.BasePath.Should().NotBeNullOrEmpty();
            result.BasePath.Should().StartWith("/v1.0/api/");
        }

        [Fact]
        public async Task Create_WithValidInput_ReturnsCreatedVmr()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Should().NotBeNull();
            result.Id.Should().StartWith("vmr_");
            result.Name.Should().Be("Test VMR");
            result.BasePath.Should().Be("/test");
            result.TenantId.Should().Be(TestTenantId);
        }

        [Fact]
        public async Task Create_SetsDefaultValues()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Active.Should().BeTrue();
            result.StrictMode.Should().BeFalse();
            result.LoadBalancingMode.Should().Be(LoadBalancingModeEnum.RoundRobin);
            result.AllowEmbeddings.Should().BeTrue();
            result.AllowCompletions.Should().BeTrue();
            result.AllowModelManagement.Should().BeFalse(); // Default is false
        }

        [Fact]
        public async Task Create_OverridesTenantId()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = "wrong_tenant",
                Name = "Test VMR",
                BasePath = "/test"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.TenantId.Should().Be(TestTenantId);
        }

        [Fact]
        public async Task Create_GeneratesNewId()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Id = "existing_id",
                Name = "Test VMR",
                BasePath = "/test"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Id.Should().NotBe("existing_id");
            result.Id.Should().StartWith("vmr_");
        }

        #endregion

        #region Read-Tests

        [Fact]
        public async Task Read_WithNullId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Read_WithEmptyId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, "");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Read_WithNonExistentId_ThrowsNotFound()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, "vmr_nonexistent");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Read_WithValidId_ReturnsVmr()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            });

            VirtualModelRunner result = await _Controller.Read(TestTenantId, created.Id);

            result.Should().NotBeNull();
            result.Id.Should().Be(created.Id);
            result.Name.Should().Be("Test VMR");
        }

        [Fact]
        public async Task Read_WrongTenant_ThrowsNotFound()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            });

            Func<Task> act = async () => await _Controller.Read("wrong_tenant", created.Id);

            await act.Should().ThrowAsync<Exception>();
        }

        #endregion

        #region Update-Tests

        [Fact]
        public async Task Update_WithNullId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, null, new VirtualModelRunner());

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_WithEmptyId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, "", new VirtualModelRunner());

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_WithNonExistentId_ThrowsNotFound()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, "vmr_nonexistent", new VirtualModelRunner
            {
                Name = "Updated",
                BasePath = "/updated"
            });

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_WithNullBody_ThrowsBadRequest()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            });

            Func<Task> act = async () => await _Controller.Update(TestTenantId, created.Id, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_PreservesCreatedUtc()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                Name = "Updated VMR",
                BasePath = "/updated",
                CreatedUtc = DateTime.UtcNow.AddDays(-10) // Try to override
            };

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.CreatedUtc.Should().BeCloseTo(created.CreatedUtc, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Update_UpdatesFields()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test",
                Active = true
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                Name = "Updated VMR",
                BasePath = "/updated",
                Active = false,
                StrictMode = true,
                TimeoutMs = 120000
            };

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.Name.Should().Be("Updated VMR");
            result.BasePath.Should().Be("/updated");
            result.Active.Should().BeFalse();
            result.StrictMode.Should().BeTrue();
            result.TimeoutMs.Should().Be(120000);
        }

        [Fact]
        public async Task Update_PreservesId()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                Id = "different_id",
                Name = "Updated VMR",
                BasePath = "/updated"
            };

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.Id.Should().Be(created.Id);
        }

        [Fact]
        public async Task Update_PreservesTenantId()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                TenantId = "different_tenant",
                Name = "Updated VMR",
                BasePath = "/updated"
            };

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.TenantId.Should().Be(TestTenantId);
        }

        #endregion

        #region Delete-Tests

        [Fact]
        public async Task Delete_WithNullId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WithEmptyId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, "");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WithNonExistentId_ThrowsNotFound()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, "vmr_nonexistent");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WithValidId_DeletesSuccessfully()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            });

            await _Controller.Delete(TestTenantId, created.Id);

            Func<Task> act = async () => await _Controller.Read(TestTenantId, created.Id);
            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WrongTenant_ThrowsNotFound()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            });

            Func<Task> act = async () => await _Controller.Delete("wrong_tenant", created.Id);

            await act.Should().ThrowAsync<Exception>();
        }

        #endregion

        #region Enumerate-Tests

        [Fact]
        public async Task Enumerate_WithNoData_ReturnsEmptyResult()
        {
            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate(TestTenantId);

            result.Should().NotBeNull();
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task Enumerate_ReturnsAllVmrs()
        {
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 1", BasePath = "/vmr1" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 2", BasePath = "/vmr2" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 3", BasePath = "/vmr3" });

            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate(TestTenantId);

            result.Data.Should().HaveCount(3);
        }

        [Fact]
        public async Task Enumerate_WithMaxResults_LimitsResults()
        {
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 1", BasePath = "/vmr1" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 2", BasePath = "/vmr2" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 3", BasePath = "/vmr3" });

            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate(TestTenantId, maxResults: 2);

            result.Data.Should().HaveCount(2);
        }

        [Fact]
        public async Task Enumerate_WithNameFilter_FiltersResults()
        {
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "Alpha VMR", BasePath = "/alpha" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "Beta VMR", BasePath = "/beta" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "Alpha Runner", BasePath = "/runner" });

            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate(TestTenantId, nameFilter: "Alpha");

            result.Data.Should().HaveCount(2);
            result.Data.Should().OnlyContain(v => v.Name.Contains("Alpha"));
        }

        [Fact]
        public async Task Enumerate_WithActiveFilter_FiltersResults()
        {
            VirtualModelRunner activeVmr = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Active VMR",
                BasePath = "/active",
                Active = true
            });

            VirtualModelRunner vmrToDeactivate = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Inactive VMR",
                BasePath = "/inactive"
            });

            // Deactivate one VMR
            await _Controller.Update(TestTenantId, vmrToDeactivate.Id, new VirtualModelRunner
            {
                Name = "Inactive VMR",
                BasePath = "/inactive",
                Active = false
            });

            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate(TestTenantId, activeFilter: true);

            result.Data.Should().HaveCount(1);
            result.Data.Should().OnlyContain(v => v.Active);
        }

        [Fact]
        public async Task Enumerate_WrongTenant_ReturnsEmpty()
        {
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 1", BasePath = "/vmr1" });

            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate("wrong_tenant");

            result.Data.Should().BeEmpty();
        }

        #endregion

        #region SessionAffinity-Tests

        [Fact]
        public async Task Create_WithSessionAffinity_ReturnsCreatedVmr()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Session VMR",
                BasePath = "/session",
                SessionAffinityMode = SessionAffinityModeEnum.SourceIP,
                SessionTimeoutMs = 300000,
                SessionMaxEntries = 5000
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Should().NotBeNull();
            result.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.SourceIP);
            result.SessionTimeoutMs.Should().Be(300000);
            result.SessionMaxEntries.Should().Be(5000);
        }

        [Fact]
        public async Task Create_WithSessionAffinityHeader_ReturnsCreatedVmr()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Header Session VMR",
                BasePath = "/header-session",
                SessionAffinityMode = SessionAffinityModeEnum.Header,
                SessionAffinityHeader = "X-Session-Id"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Should().NotBeNull();
            result.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.Header);
            result.SessionAffinityHeader.Should().Be("X-Session-Id");
        }

        [Fact]
        public async Task Update_SessionAffinityMode_UpdatesSuccessfully()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "VMR to Update Session",
                BasePath = "/update-session",
                SessionAffinityMode = SessionAffinityModeEnum.None
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                Name = "VMR to Update Session",
                BasePath = "/update-session",
                SessionAffinityMode = SessionAffinityModeEnum.ApiKey,
                SessionTimeoutMs = 120000,
                SessionMaxEntries = 2000
            };

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.ApiKey);
            result.SessionTimeoutMs.Should().Be(120000);
            result.SessionMaxEntries.Should().Be(2000);
        }

        [Fact]
        public async Task Create_SetsDefaultSessionAffinityValues()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Default Session VMR",
                BasePath = "/default-session"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.None);
            result.SessionAffinityHeader.Should().BeNull();
            result.SessionTimeoutMs.Should().Be(600000);
            result.SessionMaxEntries.Should().Be(10000);
        }

        [Fact]
        public async Task Update_SessionAffinityFields_ReadBackCorrectly()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Read Back VMR",
                BasePath = "/readback"
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                Name = "Read Back VMR",
                BasePath = "/readback",
                SessionAffinityMode = SessionAffinityModeEnum.Header,
                SessionAffinityHeader = "X-Custom",
                SessionTimeoutMs = 86400000,
                SessionMaxEntries = 100000
            };

            await _Controller.Update(TestTenantId, created.Id, updateRequest);
            VirtualModelRunner read = await _Controller.Read(TestTenantId, created.Id);

            read.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.Header);
            read.SessionAffinityHeader.Should().Be("X-Custom");
            read.SessionTimeoutMs.Should().Be(86400000);
            read.SessionMaxEntries.Should().Be(100000);
        }

        #endregion

        #region GetHealth-Tests

        [Fact]
        public async Task GetHealth_WithNullId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.GetHealth(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task GetHealth_WithEmptyId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.GetHealth(TestTenantId, "");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task GetHealth_WithNonExistentId_ThrowsNotFound()
        {
            Func<Task> act = async () => await _Controller.GetHealth(TestTenantId, "vmr_nonexistent");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task GetHealth_WithNoEndpoints_ReturnsUnhealthyStatus()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/test"
            });

            VirtualModelRunnerHealthStatus result = await _Controller.GetHealth(TestTenantId, created.Id);

            result.Should().NotBeNull();
            result.VirtualModelRunnerId.Should().Be(created.Id);
            result.TotalEndpointCount.Should().Be(0);
            result.HealthyEndpointCount.Should().Be(0);
            result.OverallHealthy.Should().BeFalse();
        }

        [Fact]
        public async Task GetHealth_ReturnsCorrectVmrInfo()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "My Test VMR",
                BasePath = "/test"
            });

            VirtualModelRunnerHealthStatus result = await _Controller.GetHealth(TestTenantId, created.Id);

            result.VirtualModelRunnerId.Should().Be(created.Id);
            result.VirtualModelRunnerName.Should().Be("My Test VMR");
            result.CheckedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        #endregion
    }
}
