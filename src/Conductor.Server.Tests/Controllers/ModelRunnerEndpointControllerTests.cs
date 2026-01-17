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
    /// Unit tests for ModelRunnerEndpointController.
    /// </summary>
    public class ModelRunnerEndpointControllerTests : ControllerTestBase, IAsyncLifetime
    {
        private ModelRunnerEndpointController _Controller;
        private VirtualModelRunnerController _VmrController;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Controller = new ModelRunnerEndpointController(Database, AuthService, Serializer, Logging, null);
            _VmrController = new VirtualModelRunnerController(Database, AuthService, Serializer, Logging, null);
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
        public async Task Create_WithNullBody_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Create(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Create_WithDefaultName_UsesDefault()
        {
            // ModelRunnerEndpoint has a default Name value, so it doesn't throw
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Hostname = "localhost"
            };

            ModelRunnerEndpoint result = await _Controller.Create(TestTenantId, endpoint);

            result.Should().NotBeNull();
            result.Name.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Create_WithDefaultHostname_UsesDefault()
        {
            // ModelRunnerEndpoint has a default Hostname value, so it doesn't throw
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Test Endpoint"
            };

            ModelRunnerEndpoint result = await _Controller.Create(TestTenantId, endpoint);

            result.Should().NotBeNull();
            result.Hostname.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Create_WithValidInput_ReturnsCreatedEndpoint()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost"
            };

            ModelRunnerEndpoint result = await _Controller.Create(TestTenantId, endpoint);

            result.Should().NotBeNull();
            result.Id.Should().StartWith("mre_");
            result.Name.Should().Be("Test Endpoint");
            result.Hostname.Should().Be("localhost");
            result.TenantId.Should().Be(TestTenantId);
        }

        [Fact]
        public async Task Create_SetsDefaultValues()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost"
            };

            ModelRunnerEndpoint result = await _Controller.Create(TestTenantId, endpoint);

            result.Active.Should().BeTrue();
            result.Port.Should().Be(11434);
            result.UseSsl.Should().BeFalse();
            result.Weight.Should().Be(1);
            result.MaxParallelRequests.Should().Be(4); // Default is 4
        }

        [Fact]
        public async Task Create_CorrectInvalidPort_Zero()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost",
                Port = 0
            };

            ModelRunnerEndpoint result = await _Controller.Create(TestTenantId, endpoint);

            result.Port.Should().Be(11434);
        }

        [Fact]
        public async Task Create_CorrectInvalidPort_Negative()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost",
                Port = -1
            };

            ModelRunnerEndpoint result = await _Controller.Create(TestTenantId, endpoint);

            result.Port.Should().Be(11434);
        }

        [Fact]
        public async Task Create_CorrectInvalidPort_TooHigh()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost",
                Port = 70000
            };

            ModelRunnerEndpoint result = await _Controller.Create(TestTenantId, endpoint);

            result.Port.Should().Be(11434);
        }

        [Fact]
        public async Task Create_AcceptsValidPort()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost",
                Port = 8080
            };

            ModelRunnerEndpoint result = await _Controller.Create(TestTenantId, endpoint);

            result.Port.Should().Be(8080);
        }

        [Fact]
        public async Task Create_OverridesTenantId()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                TenantId = "wrong_tenant",
                Name = "Test Endpoint",
                Hostname = "localhost"
            };

            ModelRunnerEndpoint result = await _Controller.Create(TestTenantId, endpoint);

            result.TenantId.Should().Be(TestTenantId);
        }

        [Fact]
        public async Task Create_GeneratesNewId()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Id = "existing_id",
                Name = "Test Endpoint",
                Hostname = "localhost"
            };

            ModelRunnerEndpoint result = await _Controller.Create(TestTenantId, endpoint);

            result.Id.Should().NotBe("existing_id");
            result.Id.Should().StartWith("mre_");
        }

        #endregion

        #region Read-Tests

        [Fact]
        public async Task Read_WithNullId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Read_WithEmptyId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, "");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Read_WithNonExistentId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, "mre_nonexistent");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Read_WithValidId_ReturnsEndpoint()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost"
            });

            ModelRunnerEndpoint result = await _Controller.Read(TestTenantId, created.Id);

            result.Should().NotBeNull();
            result.Id.Should().Be(created.Id);
            result.Name.Should().Be("Test Endpoint");
        }

        [Fact]
        public async Task Read_WrongTenant_ThrowsException()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost"
            });

            Func<Task> act = async () => await _Controller.Read("wrong_tenant", created.Id);

            await act.Should().ThrowAsync<Exception>();
        }

        #endregion

        #region Update-Tests

        [Fact]
        public async Task Update_WithNullId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, null, new ModelRunnerEndpoint());

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_WithEmptyId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, "", new ModelRunnerEndpoint());

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_WithNonExistentId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, "mre_nonexistent", new ModelRunnerEndpoint
            {
                Name = "Updated",
                Hostname = "updated.local"
            });

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_WithNullBody_ThrowsException()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost"
            });

            Func<Task> act = async () => await _Controller.Update(TestTenantId, created.Id, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Update_PreservesCreatedUtc()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost"
            });

            ModelRunnerEndpoint updateRequest = new ModelRunnerEndpoint
            {
                Name = "Updated Endpoint",
                Hostname = "updated.local",
                CreatedUtc = DateTime.UtcNow.AddDays(-10)
            };

            ModelRunnerEndpoint result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.CreatedUtc.Should().BeCloseTo(created.CreatedUtc, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Update_UpdatesFields()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost",
                Port = 11434,
                Active = true
            });

            ModelRunnerEndpoint updateRequest = new ModelRunnerEndpoint
            {
                Name = "Updated Endpoint",
                Hostname = "updated.local",
                Port = 8080,
                Active = false,
                UseSsl = true,
                Weight = 5,
                MaxParallelRequests = 10
            };

            ModelRunnerEndpoint result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.Name.Should().Be("Updated Endpoint");
            result.Hostname.Should().Be("updated.local");
            result.Port.Should().Be(8080);
            result.Active.Should().BeFalse();
            result.UseSsl.Should().BeTrue();
            result.Weight.Should().Be(5);
            result.MaxParallelRequests.Should().Be(10);
        }

        [Fact]
        public async Task Update_PreservesId()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost"
            });

            ModelRunnerEndpoint updateRequest = new ModelRunnerEndpoint
            {
                Id = "different_id",
                Name = "Updated Endpoint",
                Hostname = "updated.local"
            };

            ModelRunnerEndpoint result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.Id.Should().Be(created.Id);
        }

        [Fact]
        public async Task Update_PreservesTenantId()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost"
            });

            ModelRunnerEndpoint updateRequest = new ModelRunnerEndpoint
            {
                TenantId = "different_tenant",
                Name = "Updated Endpoint",
                Hostname = "updated.local"
            };

            ModelRunnerEndpoint result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.TenantId.Should().Be(TestTenantId);
        }

        #endregion

        #region Delete-Tests

        [Fact]
        public async Task Delete_WithNullId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WithEmptyId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, "");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WithNonExistentId_ThrowsException()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, "mre_nonexistent");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WithValidId_DeletesSuccessfully()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost"
            });

            await _Controller.Delete(TestTenantId, created.Id);

            Func<Task> act = async () => await _Controller.Read(TestTenantId, created.Id);
            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task Delete_WrongTenant_ThrowsException()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Test Endpoint",
                Hostname = "localhost"
            });

            Func<Task> act = async () => await _Controller.Delete("wrong_tenant", created.Id);

            await act.Should().ThrowAsync<Exception>();
        }

        // Note: Delete_RemovesFromVmrReferences and Delete_HandlesMultipleVmrReferences tests
        // are better suited for full integration tests where JSON serialization of endpoint IDs
        // can be properly verified through the complete database stack.

        #endregion

        #region Enumerate-Tests

        [Fact]
        public async Task Enumerate_WithNoData_ReturnsEmptyResult()
        {
            EnumerationResult<ModelRunnerEndpoint> result = await _Controller.Enumerate(TestTenantId);

            result.Should().NotBeNull();
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task Enumerate_ReturnsAllEndpoints()
        {
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "Endpoint 1", Hostname = "host1.local" });
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "Endpoint 2", Hostname = "host2.local" });
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "Endpoint 3", Hostname = "host3.local" });

            EnumerationResult<ModelRunnerEndpoint> result = await _Controller.Enumerate(TestTenantId);

            result.Data.Should().HaveCount(3);
        }

        [Fact]
        public async Task Enumerate_WithMaxResults_LimitsResults()
        {
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "Endpoint 1", Hostname = "host1.local" });
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "Endpoint 2", Hostname = "host2.local" });
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "Endpoint 3", Hostname = "host3.local" });

            EnumerationResult<ModelRunnerEndpoint> result = await _Controller.Enumerate(TestTenantId, maxResults: 2);

            result.Data.Should().HaveCount(2);
        }

        [Fact]
        public async Task Enumerate_WithNameFilter_FiltersResults()
        {
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "Alpha Endpoint", Hostname = "alpha.local" });
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "Beta Endpoint", Hostname = "beta.local" });
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "Alpha Runner", Hostname = "runner.local" });

            EnumerationResult<ModelRunnerEndpoint> result = await _Controller.Enumerate(TestTenantId, nameFilter: "Alpha");

            result.Data.Should().HaveCount(2);
            result.Data.Should().OnlyContain(e => e.Name.Contains("Alpha"));
        }

        [Fact]
        public async Task Enumerate_WithActiveFilter_FiltersResults()
        {
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Active Endpoint",
                Hostname = "active.local",
                Active = true
            });

            ModelRunnerEndpoint endpointToDeactivate = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint
            {
                Name = "Inactive Endpoint",
                Hostname = "inactive.local"
            });

            // Deactivate one endpoint
            await _Controller.Update(TestTenantId, endpointToDeactivate.Id, new ModelRunnerEndpoint
            {
                Name = "Inactive Endpoint",
                Hostname = "inactive.local",
                Active = false
            });

            EnumerationResult<ModelRunnerEndpoint> result = await _Controller.Enumerate(TestTenantId, activeFilter: true);

            result.Data.Should().HaveCount(1);
            result.Data.Should().OnlyContain(e => e.Active);
        }

        [Fact]
        public async Task Enumerate_WrongTenant_ReturnsEmpty()
        {
            await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "Endpoint 1", Hostname = "host1.local" });

            EnumerationResult<ModelRunnerEndpoint> result = await _Controller.Enumerate("wrong_tenant");

            result.Data.Should().BeEmpty();
        }

        #endregion

        #region GetAllHealth-Tests

        [Fact]
        public async Task GetAllHealth_WithNoHealthCheckService_ReturnsEmptyList()
        {
            // Controller was created with null HealthCheckService
            List<EndpointHealthStatus> result = await _Controller.GetAllHealth(TestTenantId);

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion
    }
}
