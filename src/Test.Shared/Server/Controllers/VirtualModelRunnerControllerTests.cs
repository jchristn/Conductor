namespace Test.Shared.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using FluentAssertions;
    
    /// <summary>
    /// Unit tests for VirtualModelRunnerController.
    /// </summary>
    public class VirtualModelRunnerControllerTests : ControllerTestBase
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
        public async Task Create_WithNullBody_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Create(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Create_WithDefaultName_UsesDefault()
        {
            // VirtualModelRunner has a default Name value, so it doesn't throw
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                BasePath = "/v1.0/api/test/"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Should().NotBeNull();
            result.Name.Should().NotBeNullOrEmpty();
        }
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
            result.BasePath.Should().Be($"/v1.0/api/{result.Id}/");
        }
        public async Task Create_WithBasePathMissingTrailingSlash_NormalizesBasePath()
        {
            VirtualModelRunner result = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Normalized VMR",
                BasePath = "/v1.0/api/normalized"
            });

            result.BasePath.Should().Be("/v1.0/api/normalized/");
        }
        public async Task Create_WithValidInput_ReturnsCreatedVmr()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Should().NotBeNull();
            result.Id.Should().StartWith("vmr_");
            result.Name.Should().Be("Test VMR");
            result.BasePath.Should().Be("/v1.0/api/test/");
            result.TenantId.Should().Be(TestTenantId);
        }
        public async Task Create_SetsDefaultValues()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Active.Should().BeTrue();
            result.StrictMode.Should().BeFalse();
            result.LoadBalancingMode.Should().Be(LoadBalancingModeEnum.RoundRobin);
            result.AllowEmbeddings.Should().BeTrue();
            result.AllowCompletions.Should().BeTrue();
            result.AllowModelManagement.Should().BeFalse(); // Default is false
        }
        public async Task Create_OverridesTenantId()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = "wrong_tenant",
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.TenantId.Should().Be(TestTenantId);
        }
        public async Task Create_GeneratesNewId()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Id = "existing_id",
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Id.Should().NotBe("existing_id");
            result.Id.Should().StartWith("vmr_");
        }
        public async Task Create_WithInvalidBasePath_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Invalid VMR",
                BasePath = "/test"
            });

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Create_WithUnknownLoadBalancingPolicyId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Invalid Policy VMR",
                BasePath = "/v1.0/api/invalid-policy/",
                LoadBalancingPolicyId = "lbp_missing"
            });

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Create_WithValidLoadBalancingPolicyId_PersistsAttachment()
        {
            LoadBalancingPolicy policy = await Database.LoadBalancingPolicy.CreateAsync(new LoadBalancingPolicy
            {
                TenantId = TestTenantId,
                Name = "CPU Policy"
            }).ConfigureAwait(false);

            VirtualModelRunner result = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Policy VMR",
                BasePath = "/v1.0/api/policy-vmr/",
                LoadBalancingPolicyId = policy.Id
            }).ConfigureAwait(false);

            result.LoadBalancingPolicyId.Should().Be(policy.Id);
        }

        #endregion

        #region Read-Tests
        public async Task Read_WithNullId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Read_WithEmptyId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, "");

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Read_WithNonExistentId_ThrowsNotFound()
        {
            Func<Task> act = async () => await _Controller.Read(TestTenantId, "vmr_nonexistent");

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Read_WithValidId_ReturnsVmr()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            VirtualModelRunner result = await _Controller.Read(TestTenantId, created.Id);

            result.Should().NotBeNull();
            result.Id.Should().Be(created.Id);
            result.Name.Should().Be("Test VMR");
        }
        public async Task Read_WrongTenant_ThrowsNotFound()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            Func<Task> act = async () => await _Controller.Read("wrong_tenant", created.Id);

            await act.Should().ThrowAsync<Exception>();
        }

        #endregion

        #region Update-Tests
        public async Task Update_WithNullId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, null, new VirtualModelRunner());

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Update_WithEmptyId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, "", new VirtualModelRunner());

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Update_WithNonExistentId_ThrowsNotFound()
        {
            Func<Task> act = async () => await _Controller.Update(TestTenantId, "vmr_nonexistent", new VirtualModelRunner
            {
                Name = "Updated",
                BasePath = "/v1.0/api/updated/"
            });

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Update_WithUnknownLoadBalancingPolicyId_ThrowsBadRequest()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Update Policy VMR",
                BasePath = "/v1.0/api/update-policy/"
            }).ConfigureAwait(false);

            Func<Task> act = async () => await _Controller.Update(TestTenantId, created.Id, new VirtualModelRunner
            {
                Name = "Update Policy VMR",
                BasePath = "/v1.0/api/update-policy/",
                LoadBalancingPolicyId = "lbp_missing"
            }).ConfigureAwait(false);

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Update_WithValidLoadBalancingPolicyId_PersistsAttachment()
        {
            LoadBalancingPolicy policy = await Database.LoadBalancingPolicy.CreateAsync(new LoadBalancingPolicy
            {
                TenantId = TestTenantId,
                Name = "Update Policy"
            }).ConfigureAwait(false);

            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Update Policy VMR",
                BasePath = "/v1.0/api/update-policy-valid/"
            }).ConfigureAwait(false);

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, new VirtualModelRunner
            {
                Name = "Update Policy VMR",
                BasePath = "/v1.0/api/update-policy-valid/",
                LoadBalancingPolicyId = policy.Id
            }).ConfigureAwait(false);

            result.LoadBalancingPolicyId.Should().Be(policy.Id);
        }
        public async Task Update_WithNullBody_ThrowsBadRequest()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            Func<Task> act = async () => await _Controller.Update(TestTenantId, created.Id, null);

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Update_PreservesCreatedUtc()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                Name = "Updated VMR",
                BasePath = "/v1.0/api/updated/",
                CreatedUtc = DateTime.UtcNow.AddDays(-10) // Try to override
            };

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.CreatedUtc.Should().BeCloseTo(created.CreatedUtc, TimeSpan.FromSeconds(1));
        }
        public async Task Update_UpdatesFields()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/",
                Active = true
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                Name = "Updated VMR",
                BasePath = "/v1.0/api/updated/",
                Active = false,
                StrictMode = true,
                TimeoutMs = 120000
            };

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.Name.Should().Be("Updated VMR");
            result.BasePath.Should().Be("/v1.0/api/updated/");
            result.Active.Should().BeFalse();
            result.StrictMode.Should().BeTrue();
            result.TimeoutMs.Should().Be(120000);
        }
        public async Task Update_PreservesId()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                Id = "different_id",
                Name = "Updated VMR",
                BasePath = "/v1.0/api/updated/"
            };

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.Id.Should().Be(created.Id);
        }
        public async Task Update_PreservesTenantId()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                TenantId = "different_tenant",
                Name = "Updated VMR",
                BasePath = "/v1.0/api/updated/"
            };

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.TenantId.Should().Be(TestTenantId);
        }
        public async Task Update_WithBasePathMissingTrailingSlash_NormalizesBasePath()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, new VirtualModelRunner
            {
                Name = "Updated VMR",
                BasePath = "/v1.0/api/normalized-update"
            });

            result.BasePath.Should().Be("/v1.0/api/normalized-update/");
        }
        public async Task Update_WithInvalidBasePath_ThrowsBadRequest()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            Func<Task> act = async () => await _Controller.Update(TestTenantId, created.Id, new VirtualModelRunner
            {
                Name = "Updated VMR",
                BasePath = "/updated"
            });

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Update_WithoutExplicitBasePath_ThrowsBadRequest()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            Func<Task> act = async () => await _Controller.Update(TestTenantId, created.Id, new VirtualModelRunner
            {
                Name = "Updated VMR"
            });

            await act.Should().ThrowAsync<Exception>();
        }

        #endregion

        #region Delete-Tests
        public async Task Delete_WithNullId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Delete_WithEmptyId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, "");

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Delete_WithNonExistentId_ThrowsNotFound()
        {
            Func<Task> act = async () => await _Controller.Delete(TestTenantId, "vmr_nonexistent");

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Delete_WithValidId_DeletesSuccessfully()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            await _Controller.Delete(TestTenantId, created.Id);

            Func<Task> act = async () => await _Controller.Read(TestTenantId, created.Id);
            await act.Should().ThrowAsync<Exception>();
        }
        public async Task Delete_WrongTenant_ThrowsNotFound()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            Func<Task> act = async () => await _Controller.Delete("wrong_tenant", created.Id);

            await act.Should().ThrowAsync<Exception>();
        }

        #endregion

        #region Enumerate-Tests
        public async Task Enumerate_WithNoData_ReturnsEmptyResult()
        {
            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate(TestTenantId);

            result.Should().NotBeNull();
            result.Data.Should().NotBeNull();
            result.Data.Should().BeEmpty();
        }
        public async Task Enumerate_ReturnsAllVmrs()
        {
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 1", BasePath = "/v1.0/api/vmr1/" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 2", BasePath = "/v1.0/api/vmr2/" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 3", BasePath = "/v1.0/api/vmr3/" });

            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate(TestTenantId);

            result.Data.Should().HaveCount(3);
        }
        public async Task Enumerate_WithMaxResults_LimitsResults()
        {
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 1", BasePath = "/v1.0/api/vmr1/" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 2", BasePath = "/v1.0/api/vmr2/" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 3", BasePath = "/v1.0/api/vmr3/" });

            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate(TestTenantId, maxResults: 2);

            result.Data.Should().HaveCount(2);
        }
        public async Task Enumerate_WithNameFilter_FiltersResults()
        {
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "Alpha VMR", BasePath = "/v1.0/api/alpha/" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "Beta VMR", BasePath = "/v1.0/api/beta/" });
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "Alpha Runner", BasePath = "/v1.0/api/runner/" });

            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate(TestTenantId, nameFilter: "Alpha");

            result.Data.Should().HaveCount(2);
            result.Data.Should().OnlyContain(v => v.Name.Contains("Alpha"));
        }
        public async Task Enumerate_WithActiveFilter_FiltersResults()
        {
            VirtualModelRunner activeVmr = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Active VMR",
                BasePath = "/v1.0/api/active/",
                Active = true
            });

            VirtualModelRunner vmrToDeactivate = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Inactive VMR",
                BasePath = "/v1.0/api/inactive/"
            });

            // Deactivate one VMR
            await _Controller.Update(TestTenantId, vmrToDeactivate.Id, new VirtualModelRunner
            {
                Name = "Inactive VMR",
                BasePath = "/v1.0/api/inactive/",
                Active = false
            });

            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate(TestTenantId, activeFilter: true);

            result.Data.Should().HaveCount(1);
            result.Data.Should().OnlyContain(v => v.Active);
        }
        public async Task Enumerate_WrongTenant_ReturnsEmpty()
        {
            await _Controller.Create(TestTenantId, new VirtualModelRunner { Name = "VMR 1", BasePath = "/v1.0/api/vmr1/" });

            EnumerationResult<VirtualModelRunner> result = await _Controller.Enumerate("wrong_tenant");

            result.Data.Should().BeEmpty();
        }

        #endregion

        #region SessionAffinity-Tests
        public async Task Create_WithSessionAffinity_ReturnsCreatedVmr()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Session VMR",
                BasePath = "/v1.0/api/session/",
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
        public async Task Create_WithSessionAffinityHeader_ReturnsCreatedVmr()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Header Session VMR",
                BasePath = "/v1.0/api/header-session/",
                SessionAffinityMode = SessionAffinityModeEnum.Header,
                SessionAffinityHeader = "X-Session-Id"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.Should().NotBeNull();
            result.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.Header);
            result.SessionAffinityHeader.Should().Be("X-Session-Id");
        }
        public async Task Update_SessionAffinityMode_UpdatesSuccessfully()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "VMR to Update Session",
                BasePath = "/v1.0/api/update-session/",
                SessionAffinityMode = SessionAffinityModeEnum.None
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                Name = "VMR to Update Session",
                BasePath = "/v1.0/api/update-session/",
                SessionAffinityMode = SessionAffinityModeEnum.ApiKey,
                SessionTimeoutMs = 120000,
                SessionMaxEntries = 2000
            };

            VirtualModelRunner result = await _Controller.Update(TestTenantId, created.Id, updateRequest);

            result.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.ApiKey);
            result.SessionTimeoutMs.Should().Be(120000);
            result.SessionMaxEntries.Should().Be(2000);
        }
        public async Task Create_SetsDefaultSessionAffinityValues()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                Name = "Default Session VMR",
                BasePath = "/v1.0/api/default-session/"
            };

            VirtualModelRunner result = await _Controller.Create(TestTenantId, vmr);

            result.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.None);
            result.SessionAffinityHeader.Should().BeNull();
            result.SessionTimeoutMs.Should().Be(600000);
            result.SessionMaxEntries.Should().Be(10000);
        }
        public async Task Update_SessionAffinityFields_ReadBackCorrectly()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Read Back VMR",
                BasePath = "/v1.0/api/readback/"
            });

            VirtualModelRunner updateRequest = new VirtualModelRunner
            {
                Name = "Read Back VMR",
                BasePath = "/v1.0/api/readback/",
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
        public async Task GetHealth_WithNullId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.GetHealth(TestTenantId, null);

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task GetHealth_WithEmptyId_ThrowsBadRequest()
        {
            Func<Task> act = async () => await _Controller.GetHealth(TestTenantId, "");

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task GetHealth_WithNonExistentId_ThrowsNotFound()
        {
            Func<Task> act = async () => await _Controller.GetHealth(TestTenantId, "vmr_nonexistent");

            await act.Should().ThrowAsync<Exception>();
        }
        public async Task GetHealth_WithNoEndpoints_ReturnsUnhealthyStatus()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            VirtualModelRunnerHealthStatus result = await _Controller.GetHealth(TestTenantId, created.Id);

            result.Should().NotBeNull();
            result.VirtualModelRunnerId.Should().Be(created.Id);
            result.TotalEndpointCount.Should().Be(0);
            result.HealthyEndpointCount.Should().Be(0);
            result.OverallHealthy.Should().BeFalse();
        }
        public async Task GetHealth_WithSessionAffinityEnabled_ReturnsActiveSessionCount()
        {
            using SessionAffinityService sessionAffinityService = new SessionAffinityService(Logging);
            VirtualModelRunnerController controller = new VirtualModelRunnerController(
                Database,
                AuthService,
                Serializer,
                Logging,
                healthCheckService: null,
                sessionAffinityService: sessionAffinityService);

            VirtualModelRunner created = await controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Session VMR",
                BasePath = "/v1.0/api/session-health/",
                SessionAffinityMode = SessionAffinityModeEnum.SourceIP
            });

            sessionAffinityService.SetPinnedEndpoint(created.Id, "client-1", "mre_1", 600000, 10000);
            sessionAffinityService.SetPinnedEndpoint(created.Id, "client-2", "mre_2", 600000, 10000);

            VirtualModelRunnerHealthStatus result = await controller.GetHealth(TestTenantId, created.Id);

            result.ActiveSessionCount.Should().Be(2);
        }
        public async Task GetHealth_ReturnsCorrectVmrInfo()
        {
            VirtualModelRunner created = await _Controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "My Test VMR",
                BasePath = "/v1.0/api/test/"
            });

            VirtualModelRunnerHealthStatus result = await _Controller.GetHealth(TestTenantId, created.Id);

            result.VirtualModelRunnerId.Should().Be(created.Id);
            result.VirtualModelRunnerName.Should().Be("My Test VMR");
            result.CheckedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
        public async Task GetHealth_IncludesDrainingAndQuarantinedEndpointCounts()
        {
            HealthCheckService healthCheckService = new HealthCheckService(Database, Logging);
            VirtualModelRunnerController controller = new VirtualModelRunnerController(
                Database,
                AuthService,
                Serializer,
                Logging,
                healthCheckService);

            ModelRunnerEndpoint draining = await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = "Draining Endpoint",
                Hostname = "draining.local",
                ServiceState = EndpointServiceStateEnum.Draining
            }).ConfigureAwait(false);

            ModelRunnerEndpoint quarantined = await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = "Quarantined Endpoint",
                Hostname = "quarantined.local",
                ServiceState = EndpointServiceStateEnum.Quarantined
            }).ConfigureAwait(false);

            VirtualModelRunner vmr = await controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Health Count VMR",
                BasePath = "/v1.0/api/health-count/",
                ModelRunnerEndpointIds = new List<string> { draining.Id, quarantined.Id }
            }).ConfigureAwait(false);

            VirtualModelRunnerHealthStatus result = await controller.GetHealth(TestTenantId, vmr.Id).ConfigureAwait(false);

            result.DrainingEndpointCount.Should().Be(1);
            result.QuarantinedEndpointCount.Should().Be(1);
            result.Endpoints.Should().HaveCount(2);
        }
        public async Task Validate_WithStrictModeAndNoDefinitions_ReturnsError()
        {
            RoutingDecisionService routingDecisionService = new RoutingDecisionService(Database, Logging);
            ConfigurationValidationService validationService = new ConfigurationValidationService(Database, Logging, routingDecisionService);
            VirtualModelRunnerController controller = new VirtualModelRunnerController(Database, AuthService, Serializer, Logging, null, null, validationService, routingDecisionService);

            ResourceValidationResult result = await controller.Validate(TestTenantId, new VirtualModelRunner
            {
                Name = "Strict Validation VMR",
                BasePath = "/v1.0/api/strict-validation/",
                StrictMode = true
            }).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(item => item.Code == "StrictModeRequiresDefinitions");
        }
        public async Task GetEffectiveConfiguration_ReturnsResolvedResources()
        {
            RoutingDecisionService routingDecisionService = new RoutingDecisionService(Database, Logging);
            ConfigurationValidationService validationService = new ConfigurationValidationService(Database, Logging, routingDecisionService);
            VirtualModelRunnerController controller = new VirtualModelRunnerController(Database, AuthService, Serializer, Logging, null, null, validationService, routingDecisionService);

            ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = "Effective Endpoint",
                Hostname = "effective.local",
                ServiceState = EndpointServiceStateEnum.Draining
            }).ConfigureAwait(false);

            ModelDefinition definition = await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = TestTenantId,
                Name = "llama3.2:latest"
            }).ConfigureAwait(false);

            ModelConfiguration configuration = await Database.ModelConfiguration.CreateAsync(new ModelConfiguration
            {
                TenantId = TestTenantId,
                Name = "Pinned Temperature",
                Model = "llama3.2:latest",
                Temperature = 0.2m
            }).ConfigureAwait(false);

            LoadBalancingPolicy policy = await Database.LoadBalancingPolicy.CreateAsync(new LoadBalancingPolicy
            {
                TenantId = TestTenantId,
                Name = "Effective Policy"
            }).ConfigureAwait(false);

            VirtualModelRunner vmr = await controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Effective VMR",
                BasePath = "/v1.0/api/effective/",
                LoadBalancingPolicyId = policy.Id,
                ModelRunnerEndpointIds = new List<string> { endpoint.Id },
                ModelDefinitionIds = new List<string> { definition.Id },
                ModelConfigurationIds = new List<string> { configuration.Id },
                ModelConfigurationMappings = new Dictionary<string, string>
                {
                    { definition.Name, configuration.Id }
                }
            }).ConfigureAwait(false);

            EffectiveVirtualModelRunnerConfiguration effective = await controller.GetEffectiveConfiguration(TestTenantId, vmr.Id).ConfigureAwait(false);

            effective.Policy.Should().NotBeNull();
            effective.Policy.Id.Should().Be(policy.Id);
            effective.Endpoints.Should().ContainSingle(item => item.Id == endpoint.Id && item.ServiceState == EndpointServiceStateEnum.Draining);
            effective.ModelDefinitions.Should().ContainSingle(item => item.Id == definition.Id);
            effective.ModelConfigurations.Should().ContainSingle(item => item.Id == configuration.Id);
            effective.ModelConfigurationMappings.Should().ContainKey(definition.Name);
            effective.ModelConfigurationMappings[definition.Name].Should().Be(configuration.Id);
        }
        public async Task ExplainRouting_ReturnsTimelineCandidatesAndMutationSummary()
        {
            RoutingDecisionService routingDecisionService = new RoutingDecisionService(Database, Logging);
            ConfigurationValidationService validationService = new ConfigurationValidationService(Database, Logging, routingDecisionService);
            VirtualModelRunnerController controller = new VirtualModelRunnerController(Database, AuthService, Serializer, Logging, null, null, validationService, routingDecisionService);

            ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = "Explain Endpoint",
                Hostname = "explain.local"
            }).ConfigureAwait(false);

            ModelDefinition definition = await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = TestTenantId,
                Name = "llama3.2:latest"
            }).ConfigureAwait(false);

            ModelConfiguration configuration = await Database.ModelConfiguration.CreateAsync(new ModelConfiguration
            {
                TenantId = TestTenantId,
                Name = "Explain Config",
                Model = "llama3.2:latest",
                MaxTokens = 256
            }).ConfigureAwait(false);

            VirtualModelRunner vmr = await controller.Create(TestTenantId, new VirtualModelRunner
            {
                Name = "Explain VMR",
                BasePath = "/v1.0/api/explain/",
                ModelRunnerEndpointIds = new List<string> { endpoint.Id },
                ModelDefinitionIds = new List<string> { definition.Id },
                ModelConfigurationIds = new List<string> { configuration.Id },
                ModelConfigurationMappings = new Dictionary<string, string>
                {
                    { definition.Name, configuration.Id }
                }
            }).ConfigureAwait(false);

            RoutingDecision decision = await controller.ExplainRouting(TestTenantId, vmr.Id, new RoutingSimulationRequest
            {
                Method = "POST",
                RelativePath = "/api/chat",
                Body = "{\"model\":\"llama3.2:latest\",\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}"
            }).ConfigureAwait(false);

            decision.Success.Should().BeTrue();
            decision.SelectedEndpointId.Should().Be(endpoint.Id);
            decision.RequestWasMutated.Should().BeTrue();
            decision.MutationSummary.Should().NotBeNull();
            decision.MutationSummary.Changes.Should().Contain(item => item.PropertyName == "max_tokens");
            decision.Candidates.Should().ContainSingle(item => item.EndpointId == endpoint.Id && item.Included);
            decision.Timeline.Should().NotBeEmpty();
        }

        #endregion
    }
}
