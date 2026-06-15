namespace Test.Shared.Server.Integration
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

    /// <summary>
    /// Integration tests for database operations using SQLite.
    /// Tests CRUD operations and data integrity across all entity types.
    /// </summary>
    public class DatabaseIntegrationTests : IDisposable
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
        public async Task Tenant_Update_UpdatesName()
        {
            TenantMetadata tenant = await _Database.Tenant.CreateAsync(new TenantMetadata { Name = "Original" });

            tenant.Name = "Updated";
            TenantMetadata updated = await _Database.Tenant.UpdateAsync(tenant);

            updated.Name.Should().Be("Updated");
        }
        public async Task Tenant_Delete_RemovesEntity()
        {
            TenantMetadata tenant = await _Database.Tenant.CreateAsync(new TenantMetadata { Name = "To Delete" });
            await _Database.Tenant.DeleteAsync(tenant.Id);

            TenantMetadata read = await _Database.Tenant.ReadAsync(tenant.Id);
            read.Should().BeNull();
        }
        public async Task Tenant_Exists_ReturnsTrueForExisting()
        {
            TenantMetadata tenant = await _Database.Tenant.CreateAsync(new TenantMetadata { Name = "Exists Test" });

            bool exists = await _Database.Tenant.ExistsAsync(tenant.Id);

            exists.Should().BeTrue();
        }
        public async Task Tenant_Exists_ReturnsFalseForNonExistent()
        {
            bool exists = await _Database.Tenant.ExistsAsync("ten_nonexistent");

            exists.Should().BeFalse();
        }

        #endregion

        #region User-Tests
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

        #region Sqlite-Tests
        public async Task Sqlite_ConcurrentReads_DoNotThrow()
        {
            ModelDefinition definition = await _Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = _TestTenantId,
                Name = "gemma3:4b",
                Active = true
            });

            ModelConfiguration configuration = await _Database.ModelConfiguration.CreateAsync(new ModelConfiguration
            {
                TenantId = _TestTenantId,
                Name = "gemma3:4b-config",
                Model = "gemma3:4b",
                Active = true
            });

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 25; i++)
            {
                tasks.Add(_Database.Tenant.ReadAsync(_TestTenantId));
                tasks.Add(_Database.ModelDefinition.ReadAsync(_TestTenantId, definition.Id));
                tasks.Add(_Database.ModelConfiguration.ReadAsync(_TestTenantId, configuration.Id));
                tasks.Add(_Database.ModelDefinition.EnumerateAsync(_TestTenantId, new EnumerationRequest { MaxResults = 10 }));
                tasks.Add(_Database.ModelConfiguration.EnumerateAsync(_TestTenantId, new EnumerationRequest { MaxResults = 10 }));
            }

            Func<Task> act = async () => await Task.WhenAll(tasks).ConfigureAwait(false);
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Credential-Tests
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
        public async Task ModelRunnerEndpoint_ServiceState_RoundTrip()
        {
            ModelRunnerEndpoint created = await _Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = _TestTenantId,
                Name = "Draining Endpoint",
                Hostname = "draining.local",
                ServiceState = EndpointServiceStateEnum.Draining
            });

            ModelRunnerEndpoint read = await _Database.ModelRunnerEndpoint.ReadAsync(_TestTenantId, created.Id);

            read.ServiceState.Should().Be(EndpointServiceStateEnum.Draining);
        }

        #endregion

        #region ModelDefinition-Tests
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
            read.RequestHistoryEnabled.Should().BeTrue();
        }
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
        public async Task VirtualModelRunner_ModelConfigurationMappings_RoundTrip()
        {
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "VMR with Mappings",
                BasePath = $"/vmr/{Guid.NewGuid():N}/",
                ModelConfigurationMappings = new Dictionary<string, string>
                {
                    { "llama3.2:latest", "mc_llama" },
                    { "nomic-embed-text", "mc_embed" }
                }
            };

            VirtualModelRunner created = await _Database.VirtualModelRunner.CreateAsync(vmr);
            VirtualModelRunner read = await _Database.VirtualModelRunner.ReadAsync(_TestTenantId, created.Id);

            read.ModelConfigurationMappings.Should().ContainKey("llama3.2:latest");
            read.ModelConfigurationMappings["llama3.2:latest"].Should().Be("mc_llama");
            read.ModelConfigurationMappings.Should().ContainKey("nomic-embed-text");
        }

        #endregion

        #region ModelAccessPolicy-Tests

        public async Task ModelAccessPolicy_CreateReadUpdateDelete_RoundTrips()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy
            {
                TenantId = _TestTenantId,
                Name = "Production Model Access",
                Description = "Restrict production models",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny,
                Active = true,
                Labels = new List<string> { "prod" },
                Tags = new Dictionary<string, string> { { "owner", "platform" } }
            };

            ModelAccessPolicy created = await _Database.ModelAccessPolicy.CreateAsync(policy);
            ModelAccessPolicy read = await _Database.ModelAccessPolicy.ReadAsync(_TestTenantId, created.Id);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.Name.Should().Be("Production Model Access");
            read.DefaultDecision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            read.Labels.Should().Contain("prod");
            read.Tags.Should().ContainKey("owner");

            read.DefaultDecision = ModelAccessDefaultDecisionEnum.Permit;
            read.Active = false;
            ModelAccessPolicy updated = await _Database.ModelAccessPolicy.UpdateAsync(read);

            updated.DefaultDecision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            updated.Active.Should().BeFalse();

            bool exists = await _Database.ModelAccessPolicy.ExistsAsync(_TestTenantId, created.Id);
            exists.Should().BeTrue();

            await _Database.ModelAccessPolicy.DeleteAsync(_TestTenantId, created.Id);
            ModelAccessPolicy deleted = await _Database.ModelAccessPolicy.ReadAsync(_TestTenantId, created.Id);
            deleted.Should().BeNull();
        }

        public async Task ModelAccessPolicy_Enumerate_FiltersByTenantAndActive()
        {
            await _Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = _TestTenantId,
                Name = "Active ACL",
                Active = true
            });

            ModelAccessPolicy inactive = await _Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = _TestTenantId,
                Name = "Inactive ACL",
                Active = true
            });
            inactive.Active = false;
            await _Database.ModelAccessPolicy.UpdateAsync(inactive);

            EnumerationResult<ModelAccessPolicy> result = await _Database.ModelAccessPolicy.EnumerateAsync(
                _TestTenantId,
                new EnumerationRequest { ActiveFilter = true });

            result.Data.Should().NotBeEmpty();
            result.Data.Should().OnlyContain(policy => policy.TenantId == _TestTenantId && policy.Active);
        }

        public async Task ModelAccessRule_CreateReadUpdateDelete_RoundTrips()
        {
            ModelAccessPolicy policy = await _Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = _TestTenantId,
                Name = "Rule Test Policy",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny
            });

            ModelAccessRule rule = new ModelAccessRule
            {
                TenantId = _TestTenantId,
                PolicyId = policy.Id,
                Name = "Allow finance embeddings",
                Priority = 100,
                Effect = ModelAccessRuleEffectEnum.Allow,
                SubjectType = ModelAccessSubjectTypeEnum.CredentialLabel,
                SubjectId = "finance",
                SubjectSelector = new Dictionary<string, string> { { "label", "finance" } },
                ResourceType = ModelAccessResourceTypeEnum.ModelLabel,
                ResourceId = "embeddings",
                ResourceSelector = new Dictionary<string, string> { { "label", "embeddings" } },
                VirtualModelRunnerId = "vmr_test",
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Embeddings }
            };

            ModelAccessRule created = await _Database.ModelAccessPolicy.CreateRuleAsync(rule);
            ModelAccessRule read = await _Database.ModelAccessPolicy.ReadRuleAsync(_TestTenantId, policy.Id, created.Id);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.Priority.Should().Be(100);
            read.Effect.Should().Be(ModelAccessRuleEffectEnum.Allow);
            read.SubjectSelector.Should().ContainKey("label");
            read.ResourceSelector.Should().ContainKey("label");
            read.Actions.Should().Contain(ModelAccessActionEnum.Embeddings);

            read.Priority = 200;
            read.Effect = ModelAccessRuleEffectEnum.Deny;
            read.Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions };
            ModelAccessRule updated = await _Database.ModelAccessPolicy.UpdateRuleAsync(read);

            updated.Priority.Should().Be(200);
            updated.Effect.Should().Be(ModelAccessRuleEffectEnum.Deny);
            updated.Actions.Should().Contain(ModelAccessActionEnum.Completions);

            EnumerationResult<ModelAccessRule> rules = await _Database.ModelAccessPolicy.EnumerateRulesAsync(
                _TestTenantId,
                policy.Id,
                new EnumerationRequest { ActiveFilter = true });

            rules.Data.Should().ContainSingle(item => item.Id == created.Id);

            bool exists = await _Database.ModelAccessPolicy.ExistsRuleAsync(_TestTenantId, policy.Id, created.Id);
            exists.Should().BeTrue();

            await _Database.ModelAccessPolicy.DeleteRuleAsync(_TestTenantId, policy.Id, created.Id);
            ModelAccessRule deleted = await _Database.ModelAccessPolicy.ReadRuleAsync(_TestTenantId, policy.Id, created.Id);
            deleted.Should().BeNull();
        }

        public async Task ModelAccessPolicy_Delete_RemovesRules()
        {
            ModelAccessPolicy policy = await _Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = _TestTenantId,
                Name = "Cascade ACL"
            });

            ModelAccessRule rule = await _Database.ModelAccessPolicy.CreateRuleAsync(new ModelAccessRule
            {
                TenantId = _TestTenantId,
                PolicyId = policy.Id,
                Name = "Any allow",
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
            });

            await _Database.ModelAccessPolicy.DeleteAsync(_TestTenantId, policy.Id);

            ModelAccessRule readRule = await _Database.ModelAccessPolicy.ReadRuleByIdAsync(rule.Id);
            readRule.Should().BeNull();
        }

        public async Task VirtualModelRunner_ModelAccessPolicyId_RoundTrip()
        {
            ModelAccessPolicy policy = await _Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = _TestTenantId,
                Name = "VMR Attachment ACL"
            });

            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "VMR with Model Access Policy",
                BasePath = $"/acl/{Guid.NewGuid():N}/",
                ModelAccessPolicyId = policy.Id
            };

            VirtualModelRunner created = await _Database.VirtualModelRunner.CreateAsync(vmr);
            VirtualModelRunner read = await _Database.VirtualModelRunner.ReadAsync(_TestTenantId, created.Id);

            read.Should().NotBeNull();
            read.ModelAccessPolicyId.Should().Be(policy.Id);

            read.ModelAccessPolicyId = null;
            await _Database.VirtualModelRunner.UpdateAsync(read);

            VirtualModelRunner cleared = await _Database.VirtualModelRunner.ReadAsync(_TestTenantId, created.Id);
            cleared.ModelAccessPolicyId.Should().BeNull();
        }

        #endregion

        #region RequestHistory-Tests
        public async Task RequestHistory_SummaryFilterByDenialReason_ReturnsExpectedCounts()
        {
            DateTime now = DateTime.UtcNow;
            VirtualModelRunner vmr = await _Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "History VMR",
                BasePath = $"/history/{Guid.NewGuid():N}/"
            });

            await _Database.RequestHistory.CreateAsync(new RequestHistoryDetail
            {
                TenantGuid = _TestTenantId,
                VirtualModelRunnerGuid = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/api/chat",
                ObjectKey = "history-one.json",
                CreatedUtc = now.AddMinutes(-10),
                HttpStatus = 429,
                RoutingOutcomeCode = "Denied",
                DenialReasonCode = "AllEndpointsAtCapacity"
            });

            await _Database.RequestHistory.CreateAsync(new RequestHistoryDetail
            {
                TenantGuid = _TestTenantId,
                VirtualModelRunnerGuid = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/api/chat",
                ObjectKey = "history-two.json",
                CreatedUtc = now.AddMinutes(-5),
                HttpStatus = 503,
                RoutingOutcomeCode = "Denied",
                DenialReasonCode = "AllEndpointsQuarantined"
            });

            RequestHistorySummaryResult result = await _Database.RequestHistory.GetSummaryAsync(new RequestHistorySummaryFilter
            {
                TenantGuid = _TestTenantId,
                StartUtc = now.AddHours(-1),
                EndUtc = now.AddHours(1),
                DenialReasonCode = "AllEndpointsAtCapacity"
            });

            result.TotalFailure.Should().Be(1);
            result.DenialReasonCounts.Should().ContainKey("AllEndpointsAtCapacity");
            result.DenialReasonCounts["AllEndpointsAtCapacity"].Should().Be(1);
        }

        public async Task RequestHistory_ModelAccessFields_RoundTripAndFilter()
        {
            DateTime now = DateTime.UtcNow;
            string suffix = Guid.NewGuid().ToString("N");
            string policyId = "map_" + suffix;
            string ruleId = "mar_" + suffix;
            VirtualModelRunner vmr = await _Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "ACL History VMR",
                BasePath = $"/acl-history/{suffix}/"
            });

            RequestHistoryEntry denied = await _Database.RequestHistory.CreateAsync(new RequestHistoryDetail
            {
                TenantGuid = _TestTenantId,
                VirtualModelRunnerGuid = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/api/chat",
                ObjectKey = "acl-history-denied-" + suffix + ".json",
                CreatedUtc = now.AddMinutes(-10),
                HttpStatus = 403,
                RoutingOutcomeCode = "Denied",
                DenialReasonCode = "ModelAccessDenied",
                ModelAccessPolicyGuid = policyId,
                ModelAccessPolicyName = "Tenant ACL",
                ModelAccessRuleGuid = ruleId,
                ModelAccessRuleName = "Deny private model",
                ModelAccessDecision = "Deny",
                ModelAccessWouldDeny = false
            });

            await _Database.RequestHistory.CreateAsync(new RequestHistoryDetail
            {
                TenantGuid = _TestTenantId,
                VirtualModelRunnerGuid = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/api/chat",
                ObjectKey = "acl-history-allowed-" + suffix + ".json",
                CreatedUtc = now.AddMinutes(-5),
                HttpStatus = 200,
                RoutingOutcomeCode = "Routed",
                ModelAccessPolicyGuid = "map_other_" + suffix,
                ModelAccessRuleGuid = "mar_other_" + suffix,
                ModelAccessDecision = "Deny",
                ModelAccessWouldDeny = true
            });

            RequestHistoryEntry read = await _Database.RequestHistory.ReadByIdAsync(denied.Id);
            read.ModelAccessPolicyGuid.Should().Be(policyId);
            read.ModelAccessPolicyName.Should().Be("Tenant ACL");
            read.ModelAccessRuleGuid.Should().Be(ruleId);
            read.ModelAccessRuleName.Should().Be("Deny private model");
            read.ModelAccessDecision.Should().Be("Deny");
            read.ModelAccessWouldDeny.Should().BeFalse();

            RequestHistorySearchResult search = await _Database.RequestHistory.SearchAsync(new RequestHistorySearchFilter
            {
                TenantGuid = _TestTenantId,
                ModelAccessPolicyGuid = policyId,
                ModelAccessRuleGuid = ruleId,
                ModelAccessDecision = "Deny",
                ModelAccessWouldDeny = false,
                PageSize = 10
            });

            search.TotalCount.Should().Be(1);
            search.Data.Should().ContainSingle(item => item.Id == denied.Id);

            RequestHistorySummaryResult summary = await _Database.RequestHistory.GetSummaryAsync(new RequestHistorySummaryFilter
            {
                TenantGuid = _TestTenantId,
                StartUtc = now.AddHours(-1),
                EndUtc = now.AddHours(1),
                ModelAccessPolicyGuid = policyId,
                ModelAccessRuleGuid = ruleId,
                ModelAccessDecision = "Deny",
                ModelAccessWouldDeny = false
            });

            summary.TotalFailure.Should().Be(1);
        }

        public async Task RequestAnalytics_CreateAndList_RoundTrips()
        {
            DateTime now = DateTime.UtcNow;
            VirtualModelRunner vmr = await _Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = _TestTenantId,
                Name = "Analytics VMR",
                BasePath = $"/analytics/{Guid.NewGuid():N}/"
            });

            RequestHistoryEntry entry = await _Database.RequestHistory.CreateAsync(new RequestHistoryDetail
            {
                TenantGuid = _TestTenantId,
                VirtualModelRunnerGuid = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                RequestorSourceIp = "127.0.0.1",
                HttpMethod = "POST",
                HttpUrl = "/api/chat",
                ObjectKey = "analytics.json",
                CreatedUtc = now,
                TraceId = "trc_test"
            });

            await _Database.RequestAnalytics.CreateAsync(new RequestAnalyticsEvent
            {
                TenantGuid = _TestTenantId,
                RequestHistoryId = entry.Id,
                TraceId = entry.TraceId,
                VirtualModelRunnerGuid = entry.VirtualModelRunnerGuid,
                VirtualModelRunnerName = entry.VirtualModelRunnerName,
                StageKind = "generation",
                StageName = "Streaming Generation",
                StartedUtc = now,
                CompletedUtc = now.AddMilliseconds(125),
                DurationMs = 125,
                Success = true,
                HttpStatus = 200,
                CreatedUtc = now
            });

            List<RequestAnalyticsEvent> events = await _Database.RequestAnalytics.ListByRequestHistoryIdAsync(entry.Id);

            events.Should().HaveCount(1);
            events[0].StageKind.Should().Be("generation");
            events[0].DurationMs.Should().Be(125);
        }

        public async Task AnalyticsSavedReport_CRUD_RoundTripsTenantAndGlobalScopes()
        {
            AnalyticsSavedReport tenantReport = await _Database.AnalyticsSavedReport.CreateAsync(new AnalyticsSavedReport
            {
                TenantId = _TestTenantId,
                OwnerUserId = "usr_owner",
                Name = "Daily user cost",
                Description = "Estimate user token cost over the last day.",
                Scope = "Tenant",
                Query = new AnalyticsQueryRequest
                {
                    Range = "lastDay",
                    TokenUnitCost = 0.000001m,
                    CostCurrency = "USD",
                    GroupBy = new List<string> { "RequestorUserId" }
                },
                DisplayState = new Dictionary<string, object>
                {
                    ["chart"] = "tokens"
                },
                Labels = new List<string> { "chargeback" },
                Tags = new Dictionary<string, string>
                {
                    ["owner"] = "ops"
                }
            });

            AnalyticsSavedReport readTenantReport = await _Database.AnalyticsSavedReport.ReadAsync(_TestTenantId, tenantReport.Id);
            EnumerationResult<AnalyticsSavedReport> tenantReports = await _Database.AnalyticsSavedReport.EnumerateAsync(
                _TestTenantId,
                new EnumerationRequest { MaxResults = 10, NameFilter = "Daily" },
                "usr_owner");

            readTenantReport.Should().NotBeNull();
            readTenantReport.Name.Should().Be("Daily user cost");
            readTenantReport.Query.Range.Should().Be("lastDay");
            readTenantReport.Query.TokenUnitCost.Should().Be(0.000001m);
            readTenantReport.Query.GroupBy.Should().ContainSingle().Which.Should().Be("RequestorUserId");
            readTenantReport.Labels.Should().Contain("chargeback");
            readTenantReport.Tags.Should().ContainKey("owner").WhoseValue.Should().Be("ops");
            tenantReports.Data.Should().ContainSingle(item => item.Id == tenantReport.Id);

            readTenantReport.Name = "Daily user cost updated";
            await _Database.AnalyticsSavedReport.UpdateAsync(readTenantReport);

            AnalyticsSavedReport updated = await _Database.AnalyticsSavedReport.ReadAsync(_TestTenantId, tenantReport.Id);
            updated.Name.Should().Be("Daily user cost updated");

            AnalyticsSavedReport globalReport = await _Database.AnalyticsSavedReport.CreateAsync(new AnalyticsSavedReport
            {
                TenantId = null,
                OwnerUserId = "usr_admin",
                Name = "Global TTFT",
                Scope = "Global",
                Query = new AnalyticsQueryRequest
                {
                    Range = "lastHour",
                    GroupBy = new List<string> { "VirtualModelRunnerId" }
                }
            });

            AnalyticsSavedReport readGlobalReport = await _Database.AnalyticsSavedReport.ReadAsync(null, globalReport.Id);
            readGlobalReport.Should().NotBeNull();
            readGlobalReport.TenantId.Should().BeNull();
            readGlobalReport.Scope.Should().Be("Global");

            await _Database.AnalyticsSavedReport.DeleteAsync(_TestTenantId, tenantReport.Id);
            (await _Database.AnalyticsSavedReport.ExistsAsync(_TestTenantId, tenantReport.Id)).Should().BeFalse();
            (await _Database.AnalyticsSavedReport.ExistsAsync(null, globalReport.Id)).Should().BeTrue();
        }

        #endregion

        #region VirtualModelRunner-SessionAffinity-Tests
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
