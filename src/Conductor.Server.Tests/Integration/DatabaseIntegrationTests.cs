namespace Conductor.Server.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Integration tests for database operations using SQLite.
    /// Tests CRUD operations and data integrity across all entity types.
    /// </summary>
    public class DatabaseIntegrationTests : IAsyncLifetime, IDisposable
    {
        private readonly string _DatabaseFile;
        private DatabaseDriverBase _Database;
        private string _TestTenantId;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate the integration tests.
        /// </summary>
        public DatabaseIntegrationTests()
        {
            _DatabaseFile = Path.Combine(Path.GetTempPath(), $"conductor_integration_{Guid.NewGuid():N}.db");
        }

        /// <summary>
        /// Initialize the database and test data.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                Filename = _DatabaseFile,
                LogQueries = false
            };

            _Database = new SqliteDatabaseDriver(settings);
            await _Database.InitializeAsync().ConfigureAwait(false);

            // Create test tenant
            TenantMetadata tenant = new TenantMetadata { Name = "Integration Test Tenant", Active = true };
            tenant = await _Database.Tenant.CreateAsync(tenant).ConfigureAwait(false);
            _TestTenantId = tenant.Id;
        }

        /// <summary>
        /// Cleanup after tests.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                if (File.Exists(_DatabaseFile))
                {
                    try { File.Delete(_DatabaseFile); }
                    catch { /* ignore cleanup failures */ }
                }
            }

            _Disposed = true;
        }

        #region Tenant-Tests

        [Fact]
        public async Task Tenant_CreateAndRead_RoundTrips()
        {
            TenantMetadata tenant = new TenantMetadata
            {
                Name = "New Tenant",
                Active = true
            };

            TenantMetadata created = await _Database.Tenant.CreateAsync(tenant);
            TenantMetadata read = await _Database.Tenant.ReadAsync(created.Id);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.Name.Should().Be("New Tenant");
            read.Active.Should().BeTrue();
        }

        [Fact]
        public async Task Tenant_Update_UpdatesName()
        {
            TenantMetadata tenant = await _Database.Tenant.CreateAsync(new TenantMetadata { Name = "Original" });

            tenant.Name = "Updated";
            TenantMetadata updated = await _Database.Tenant.UpdateAsync(tenant);

            updated.Name.Should().Be("Updated");
        }

        [Fact]
        public async Task Tenant_Delete_RemovesEntity()
        {
            TenantMetadata tenant = await _Database.Tenant.CreateAsync(new TenantMetadata { Name = "To Delete" });
            await _Database.Tenant.DeleteAsync(tenant.Id);

            TenantMetadata read = await _Database.Tenant.ReadAsync(tenant.Id);
            read.Should().BeNull();
        }

        [Fact]
        public async Task Tenant_Exists_ReturnsTrueForExisting()
        {
            TenantMetadata tenant = await _Database.Tenant.CreateAsync(new TenantMetadata { Name = "Exists Test" });

            bool exists = await _Database.Tenant.ExistsAsync(tenant.Id);

            exists.Should().BeTrue();
        }

        [Fact]
        public async Task Tenant_Exists_ReturnsFalseForNonExistent()
        {
            bool exists = await _Database.Tenant.ExistsAsync("ten_nonexistent");

            exists.Should().BeFalse();
        }

        #endregion

        #region User-Tests

        [Fact]
        public async Task User_CreateAndRead_RoundTrips()
        {
            UserMaster user = new UserMaster
            {
                TenantId = _TestTenantId,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@test.com",
                Password = "password123",
                Active = true
            };

            UserMaster created = await _Database.User.CreateAsync(user);
            UserMaster read = await _Database.User.ReadAsync(_TestTenantId, created.Id);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.FirstName.Should().Be("John");
            read.LastName.Should().Be("Doe");
            read.Email.Should().Be("john.doe@test.com");
        }

        [Fact]
        public async Task User_ReadByEmail_FindsUser()
        {
            string uniqueEmail = $"findme_{Guid.NewGuid():N}@test.com";
            UserMaster user = await _Database.User.CreateAsync(new UserMaster
            {
                TenantId = _TestTenantId,
                FirstName = "Find",
                LastName = "Me",
                Email = uniqueEmail,
                Password = "password"
            });

            UserMaster found = await _Database.User.ReadByEmailAsync(_TestTenantId, uniqueEmail);

            found.Should().NotBeNull();
            found.Id.Should().Be(user.Id);
        }

        [Fact]
        public async Task User_Delete_RemovesEntity()
        {
            UserMaster user = await _Database.User.CreateAsync(new UserMaster
            {
                TenantId = _TestTenantId,
                FirstName = "Delete",
                LastName = "Me",
                Email = $"delete_{Guid.NewGuid():N}@test.com",
                Password = "password"
            });

            await _Database.User.DeleteAsync(_TestTenantId, user.Id);

            UserMaster read = await _Database.User.ReadAsync(_TestTenantId, user.Id);
            read.Should().BeNull();
        }

        #endregion

        #region Credential-Tests

        [Fact]
        public async Task Credential_CreateAndRead_RoundTrips()
        {
            // First create a user
            UserMaster user = await _Database.User.CreateAsync(new UserMaster
            {
                TenantId = _TestTenantId,
                FirstName = "Cred",
                LastName = "User",
                Email = $"cred_{Guid.NewGuid():N}@test.com",
                Password = "password"
            });

            Credential credential = new Credential
            {
                TenantId = _TestTenantId,
                UserId = user.Id,
                Name = "Test Credential"
            };

            Credential created = await _Database.Credential.CreateAsync(credential);
            Credential read = await _Database.Credential.ReadAsync(_TestTenantId, created.Id);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.Name.Should().Be("Test Credential");
            read.BearerToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Credential_ReadByBearerToken_FindsCredential()
        {
            UserMaster user = await _Database.User.CreateAsync(new UserMaster
            {
                TenantId = _TestTenantId,
                FirstName = "Token",
                LastName = "User",
                Email = $"token_{Guid.NewGuid():N}@test.com",
                Password = "password"
            });

            string uniqueToken = $"unique_token_{Guid.NewGuid():N}_padding_to_64_chars_1234567890";
            Credential credential = await _Database.Credential.CreateAsync(new Credential
            {
                TenantId = _TestTenantId,
                UserId = user.Id,
                BearerToken = uniqueToken
            });

            Credential found = await _Database.Credential.ReadByBearerTokenAsync(uniqueToken);

            found.Should().NotBeNull();
            found.Id.Should().Be(credential.Id);
        }

        #endregion

        #region ModelRunnerEndpoint-Tests

        [Fact]
        public async Task ModelRunnerEndpoint_CreateAndRead_RoundTrips()
        {
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                TenantId = _TestTenantId,
                Name = "Test Endpoint",
                Hostname = "localhost",
                Port = 11434
            };

            ModelRunnerEndpoint created = await _Database.ModelRunnerEndpoint.CreateAsync(endpoint);
            ModelRunnerEndpoint read = await _Database.ModelRunnerEndpoint.ReadAsync(_TestTenantId, created.Id);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.Name.Should().Be("Test Endpoint");
            read.Hostname.Should().Be("localhost");
            read.Port.Should().Be(11434);
        }

        [Fact]
        public async Task ModelRunnerEndpoint_Update_UpdatesFields()
        {
            ModelRunnerEndpoint endpoint = await _Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = _TestTenantId,
                Name = "Original",
                Hostname = "original.local",
                Port = 8080
            });

            endpoint.Name = "Updated";
            endpoint.Hostname = "updated.local";
            endpoint.Port = 9090;

            ModelRunnerEndpoint updated = await _Database.ModelRunnerEndpoint.UpdateAsync(endpoint);

            updated.Name.Should().Be("Updated");
            updated.Hostname.Should().Be("updated.local");
            updated.Port.Should().Be(9090);
        }

        [Fact]
        public async Task ModelRunnerEndpoint_Enumerate_WithActiveFilter_ReturnsOnlyActive()
        {
            // Create active endpoint
            await _Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = _TestTenantId,
                Name = "Active Endpoint",
                Hostname = "active.local",
                Active = true
            });

            // Create inactive endpoint
            ModelRunnerEndpoint inactive = await _Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = _TestTenantId,
                Name = "Inactive Endpoint",
                Hostname = "inactive.local",
                Active = true
            });
            inactive.Active = false;
            await _Database.ModelRunnerEndpoint.UpdateAsync(inactive);

            EnumerationResult<ModelRunnerEndpoint> result = await _Database.ModelRunnerEndpoint.EnumerateAsync(
                _TestTenantId,
                new EnumerationRequest { ActiveFilter = true });

            result.Data.Should().OnlyContain(e => e.Active);
        }

        #endregion

        #region ModelDefinition-Tests

        [Fact]
        public async Task ModelDefinition_CreateAndRead_RoundTrips()
        {
            ModelDefinition definition = new ModelDefinition
            {
                TenantId = _TestTenantId,
                Name = "llama3.2:latest"
            };

            ModelDefinition created = await _Database.ModelDefinition.CreateAsync(definition);
            ModelDefinition read = await _Database.ModelDefinition.ReadAsync(_TestTenantId, created.Id);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.Name.Should().Be("llama3.2:latest");
        }

        #endregion

        #region ModelConfiguration-Tests

        [Fact]
        public async Task ModelConfiguration_CreateAndRead_RoundTrips()
        {
            ModelConfiguration config = new ModelConfiguration
            {
                TenantId = _TestTenantId,
                Name = "Test Config",
                Temperature = 0.7m,
                MaxTokens = 1000
            };

            ModelConfiguration created = await _Database.ModelConfiguration.CreateAsync(config);
            ModelConfiguration read = await _Database.ModelConfiguration.ReadAsync(_TestTenantId, created.Id);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.Name.Should().Be("Test Config");
            read.Temperature.Should().Be(0.7m);
            read.MaxTokens.Should().Be(1000);
        }

        [Fact]
        public async Task ModelConfiguration_PinnedProperties_RoundTrip()
        {
            ModelConfiguration config = new ModelConfiguration
            {
                TenantId = _TestTenantId,
                Name = "Pinned Config",
                PinnedCompletionsProperties = new Dictionary<string, object>
                {
                    { "stream", true },
                    { "top_p", 0.9 }
                }
            };

            ModelConfiguration created = await _Database.ModelConfiguration.CreateAsync(config);
            ModelConfiguration read = await _Database.ModelConfiguration.ReadAsync(_TestTenantId, created.Id);

            read.PinnedCompletionsProperties.Should().ContainKey("stream");
            read.PinnedCompletionsProperties.Should().ContainKey("top_p");
        }

        #endregion

        #region VirtualModelRunner-Tests

        [Fact]
        public async Task VirtualModelRunner_CreateAndRead_RoundTrips()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "Test VMR",
                BasePath = "/test/vmr/"
            };

            VirtualModelRunner created = await _Database.VirtualModelRunner.CreateAsync(vmr);
            VirtualModelRunner read = await _Database.VirtualModelRunner.ReadAsync(_TestTenantId, created.Id);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.Name.Should().Be("Test VMR");
            read.BasePath.Should().Be("/test/vmr/");
        }

        [Fact]
        public async Task VirtualModelRunner_ReadByBasePath_FindsVmr()
        {
            string uniquePath = $"/unique/{Guid.NewGuid():N}/";
            VirtualModelRunner vmr = await _Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "Path Test VMR",
                BasePath = uniquePath
            });

            VirtualModelRunner found = await _Database.VirtualModelRunner.ReadByBasePathAsync(uniquePath);

            found.Should().NotBeNull();
            found.Id.Should().Be(vmr.Id);
        }

        [Fact]
        public async Task VirtualModelRunner_EndpointIds_RoundTrip()
        {
            // Create endpoints first
            ModelRunnerEndpoint ep1 = await _Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = _TestTenantId,
                Name = "Endpoint 1",
                Hostname = "ep1.local"
            });

            ModelRunnerEndpoint ep2 = await _Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = _TestTenantId,
                Name = "Endpoint 2",
                Hostname = "ep2.local"
            });

            // Create VMR with endpoint references
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "VMR with Endpoints",
                BasePath = $"/vmr/{Guid.NewGuid():N}/",
                ModelRunnerEndpointIds = new List<string> { ep1.Id, ep2.Id }
            };

            VirtualModelRunner created = await _Database.VirtualModelRunner.CreateAsync(vmr);
            VirtualModelRunner read = await _Database.VirtualModelRunner.ReadAsync(_TestTenantId, created.Id);

            read.ModelRunnerEndpointIds.Should().HaveCount(2);
            read.ModelRunnerEndpointIds.Should().Contain(ep1.Id);
            read.ModelRunnerEndpointIds.Should().Contain(ep2.Id);
        }

        [Fact]
        public async Task VirtualModelRunner_Labels_RoundTrip()
        {
            // Labels is List<string>
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "VMR with Labels",
                BasePath = $"/vmr/{Guid.NewGuid():N}/",
                Labels = new List<string> { "production", "gpu", "high-priority" }
            };

            VirtualModelRunner created = await _Database.VirtualModelRunner.CreateAsync(vmr);
            VirtualModelRunner read = await _Database.VirtualModelRunner.ReadAsync(_TestTenantId, created.Id);

            read.Labels.Should().HaveCount(3);
            read.Labels.Should().Contain("production");
            read.Labels.Should().Contain("gpu");
            read.Labels.Should().Contain("high-priority");
        }

        [Fact]
        public async Task VirtualModelRunner_Tags_RoundTrip()
        {
            // Tags is Dictionary<string, string>
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "VMR with Tags",
                BasePath = $"/vmr/{Guid.NewGuid():N}/",
                Tags = new Dictionary<string, string>
                {
                    { "environment", "production" },
                    { "team", "ml" },
                    { "version", "1.0" }
                }
            };

            VirtualModelRunner created = await _Database.VirtualModelRunner.CreateAsync(vmr);
            VirtualModelRunner read = await _Database.VirtualModelRunner.ReadAsync(_TestTenantId, created.Id);

            read.Tags.Should().ContainKey("environment");
            read.Tags["environment"].Should().Be("production");
            read.Tags.Should().ContainKey("team");
            read.Tags["team"].Should().Be("ml");
        }

        #endregion

        #region VirtualModelRunner-SessionAffinity-Tests

        [Fact]
        public async Task VirtualModelRunner_SessionAffinityFields_RoundTrip()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "Session Affinity VMR",
                BasePath = $"/session/{Guid.NewGuid():N}/",
                SessionAffinityMode = SessionAffinityModeEnum.SourceIP,
                SessionAffinityHeader = "X-Custom-Session",
                SessionTimeoutMs = 300000,
                SessionMaxEntries = 5000
            };

            VirtualModelRunner created = await _Database.VirtualModelRunner.CreateAsync(vmr);
            VirtualModelRunner read = await _Database.VirtualModelRunner.ReadAsync(_TestTenantId, created.Id);

            read.Should().NotBeNull();
            read.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.SourceIP);
            read.SessionAffinityHeader.Should().Be("X-Custom-Session");
            read.SessionTimeoutMs.Should().Be(300000);
            read.SessionMaxEntries.Should().Be(5000);
        }

        [Fact]
        public async Task VirtualModelRunner_SessionAffinityDefaults_RoundTrip()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "Default Session VMR",
                BasePath = $"/default-session/{Guid.NewGuid():N}/"
            };

            VirtualModelRunner created = await _Database.VirtualModelRunner.CreateAsync(vmr);
            VirtualModelRunner read = await _Database.VirtualModelRunner.ReadAsync(_TestTenantId, created.Id);

            read.Should().NotBeNull();
            read.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.None);
            read.SessionAffinityHeader.Should().BeNull();
            read.SessionTimeoutMs.Should().Be(600000);
            read.SessionMaxEntries.Should().Be(10000);
        }

        [Fact]
        public async Task VirtualModelRunner_SessionAffinityUpdate_PersistsChanges()
        {
            VirtualModelRunner vmr = await _Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "Update Session VMR",
                BasePath = $"/update-session/{Guid.NewGuid():N}/",
                SessionAffinityMode = SessionAffinityModeEnum.None
            });

            vmr.SessionAffinityMode = SessionAffinityModeEnum.ApiKey;
            vmr.SessionTimeoutMs = 120000;
            vmr.SessionMaxEntries = 2000;

            VirtualModelRunner updated = await _Database.VirtualModelRunner.UpdateAsync(vmr);
            VirtualModelRunner read = await _Database.VirtualModelRunner.ReadAsync(_TestTenantId, updated.Id);

            read.SessionAffinityMode.Should().Be(SessionAffinityModeEnum.ApiKey);
            read.SessionTimeoutMs.Should().Be(120000);
            read.SessionMaxEntries.Should().Be(2000);
        }

        #endregion

        #region Administrator-Tests

        [Fact]
        public async Task Administrator_CreateAndRead_RoundTrips()
        {
            Administrator admin = new Administrator
            {
                Email = $"admin_{Guid.NewGuid():N}@test.com",
                PasswordSha256 = Administrator.ComputePasswordHash("password123"),
                FirstName = "Test",
                LastName = "Admin"
            };

            Administrator created = await _Database.Administrator.CreateAsync(admin);
            Administrator read = await _Database.Administrator.ReadAsync(created.Id);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.Email.Should().Be(admin.Email.ToLower());
            read.FirstName.Should().Be("Test");
            read.LastName.Should().Be("Admin");
        }

        [Fact]
        public async Task Administrator_ReadByEmail_FindsAdmin()
        {
            string uniqueEmail = $"findadmin_{Guid.NewGuid():N}@test.com";
            Administrator admin = await _Database.Administrator.CreateAsync(new Administrator
            {
                Email = uniqueEmail,
                PasswordSha256 = Administrator.ComputePasswordHash("password")
            });

            Administrator found = await _Database.Administrator.ReadByEmailAsync(uniqueEmail);

            found.Should().NotBeNull();
            found.Id.Should().Be(admin.Id);
        }

        [Fact]
        public async Task Administrator_ExistsByEmail_ChecksUniqueness()
        {
            string uniqueEmail = $"unique_{Guid.NewGuid():N}@test.com";
            await _Database.Administrator.CreateAsync(new Administrator
            {
                Email = uniqueEmail,
                PasswordSha256 = Administrator.ComputePasswordHash("password")
            });

            bool exists = await _Database.Administrator.ExistsByEmailAsync(uniqueEmail);
            bool notExists = await _Database.Administrator.ExistsByEmailAsync($"nonexistent_{Guid.NewGuid():N}@test.com");

            exists.Should().BeTrue();
            notExists.Should().BeFalse();
        }

        #endregion

        #region Enumeration-Tests

        [Fact]
        public async Task Enumerate_WithPagination_ReturnsCorrectPage()
        {
            // Create multiple VMRs
            for (int i = 0; i < 5; i++)
            {
                await _Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
                {
                    TenantId = _TestTenantId,
                    Name = $"Pagination VMR {i}",
                    BasePath = $"/pagination/{Guid.NewGuid():N}/"
                });
            }

            EnumerationResult<VirtualModelRunner> page1 = await _Database.VirtualModelRunner.EnumerateAsync(
                _TestTenantId,
                new EnumerationRequest { MaxResults = 2 });

            page1.Data.Should().HaveCount(2);
            page1.ContinuationToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Enumerate_WithNameFilter_ReturnsFilteredResults()
        {
            // Create VMRs with different names
            await _Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "Filter Alpha VMR",
                BasePath = $"/filter/{Guid.NewGuid():N}/"
            });

            await _Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "Filter Beta VMR",
                BasePath = $"/filter/{Guid.NewGuid():N}/"
            });

            await _Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "Filter Alpha Runner",
                BasePath = $"/filter/{Guid.NewGuid():N}/"
            });

            EnumerationResult<VirtualModelRunner> result = await _Database.VirtualModelRunner.EnumerateAsync(
                _TestTenantId,
                new EnumerationRequest { NameFilter = "Alpha" });

            result.Data.Should().OnlyContain(v => v.Name.Contains("Alpha"));
        }

        #endregion
    }
}
