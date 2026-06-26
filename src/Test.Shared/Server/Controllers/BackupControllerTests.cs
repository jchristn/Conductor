namespace Test.Shared.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for backup validation behavior.
    /// </summary>
    public class BackupControllerTests : ControllerTestBase
    {
        private BackupController _Controller;
        private ModelAccessControlService _ModelAccessService;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            RoutingDecisionService routingDecisionService = new RoutingDecisionService(Database, Logging);
            ConfigurationValidationService validationService = new ConfigurationValidationService(Database, Logging, routingDecisionService);
            _ModelAccessService = new ModelAccessControlService(Database, Logging, new ModelAccessControlSettings
            {
                Enabled = true,
                Mode = ModelAccessEnforcementModeEnum.Enforce,
                DefaultDecision = ModelAccessDefaultDecisionEnum.Permit,
                UnknownModelBehavior = ModelAccessUnknownModelBehaviorEnum.Permit,
                CacheTtlMs = 600000
            });
            _Controller = new BackupController(Database, AuthService, Serializer, Logging, validationService, _ModelAccessService);
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

        public async Task CreateBackup_IncludesModelAccessPoliciesAndRules()
        {
            ModelAccessPolicy policy = await Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "Backup ACL",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny
            }).ConfigureAwait(false);

            ModelAccessRule rule = await Database.ModelAccessPolicy.CreateRuleAsync(new ModelAccessRule
            {
                TenantId = TestTenantId,
                PolicyId = policy.Id,
                Name = "Allow completions",
                Effect = ModelAccessRuleEffectEnum.Allow,
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
            }).ConfigureAwait(false);

            await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "ACL VMR",
                BasePath = "/v1.0/api/acl-backup/",
                ModelAccessPolicyId = policy.Id
            }).ConfigureAwait(false);

            BackupPackage package = await _Controller.CreateBackup("admin@example.com").ConfigureAwait(false);

            package.ModelAccessPolicies.Should().ContainSingle(item => item.Id == policy.Id);
            package.ModelAccessRules.Should().ContainSingle(item => item.Id == rule.Id);
            package.VirtualModelRunners.Should().ContainSingle(item => item.ModelAccessPolicyId == policy.Id);

            ValidationResult validation = await _Controller.ValidateBackup(package).ConfigureAwait(false);
            validation.Summary.ModelAccessPolicyCount.Should().Be(1);
            validation.Summary.ModelAccessRuleCount.Should().Be(1);
        }

        public async Task CreateBackup_IncludesVirtualModelRunnerReservations()
        {
            VirtualModelRunner vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Reservation Backup VMR",
                BasePath = "/v1.0/api/reservation-backup/"
            }).ConfigureAwait(false);
            UserMaster user = await Database.User.ReadAsync(TestTenantId, TestUserId).ConfigureAwait(false);
            VirtualModelRunnerReservation reservation = await Database.VirtualModelRunnerReservation.CreateAsync(new VirtualModelRunnerReservation
            {
                TenantId = TestTenantId,
                VirtualModelRunnerId = vmr.Id,
                Name = "Reservation Backup",
                StartUtc = DateTime.UtcNow.AddHours(1),
                EndUtc = DateTime.UtcNow.AddHours(2),
                Subjects = new List<VirtualModelRunnerReservationSubject>
                {
                    new VirtualModelRunnerReservationSubject
                    {
                        SubjectType = ReservationSubjectTypeEnum.User,
                        SubjectId = user.Id
                    }
                }
            }).ConfigureAwait(false);

            BackupPackage package = await _Controller.CreateBackup("admin@example.com").ConfigureAwait(false);

            package.VirtualModelRunnerReservations.Should().ContainSingle(item => item.Id == reservation.Id);
            package.VirtualModelRunnerReservations[0].Subjects.Should().ContainSingle(item => item.SubjectId == user.Id);

            ValidationResult validation = await _Controller.ValidateBackup(package).ConfigureAwait(false);
            validation.Summary.VirtualModelRunnerReservationCount.Should().Be(1);
        }

        public async Task CreateBackup_IncludesEndpointGroups()
        {
            ModelRunnerEndpoint endpoint = await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = "Grouped Endpoint",
                Hostname = "http://localhost:11434",
                ApiType = ApiTypeEnum.Ollama
            }).ConfigureAwait(false);
            EndpointGroup group = await Database.EndpointGroup.CreateAsync(new EndpointGroup
            {
                TenantId = TestTenantId,
                Name = "Primary Group",
                Priority = 0,
                TrafficWeight = 100,
                EndpointIds = new List<string> { endpoint.Id }
            }).ConfigureAwait(false);

            BackupPackage package = await _Controller.CreateBackup("admin@example.com").ConfigureAwait(false);

            package.EndpointGroups.Should().ContainSingle(item => item.Id == group.Id);

            ValidationResult validation = await _Controller.ValidateBackup(package).ConfigureAwait(false);
            validation.Summary.EndpointGroupCount.Should().Be(1);
        }

        public async Task RestoreBackup_WithEndpointGroup_RestoresBeforeVirtualModelRunner()
        {
            TenantMetadata tenant = new TenantMetadata
            {
                Name = "Restored Endpoint Group Tenant",
                Active = true
            };
            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                TenantId = tenant.Id,
                Name = "Restored Grouped Endpoint",
                Hostname = "http://localhost:11434",
                ApiType = ApiTypeEnum.Ollama
            };
            EndpointGroup group = new EndpointGroup
            {
                TenantId = tenant.Id,
                Name = "Restored Primary Group",
                Priority = 0,
                TrafficWeight = 100,
                EndpointIds = new List<string> { endpoint.Id }
            };
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = tenant.Id,
                Name = "Restored Group VMR",
                BasePath = "/v1.0/api/restored-group/",
                ModelRunnerEndpointIds = new List<string> { endpoint.Id },
                EndpointGroupIds = new List<string> { group.Id }
            };

            BackupPackage package = new BackupPackage
            {
                SchemaVersion = "1.4",
                Tenants = new List<TenantMetadata> { tenant },
                ModelRunnerEndpoints = new List<ModelRunnerEndpoint> { endpoint },
                EndpointGroups = new List<EndpointGroup> { group },
                VirtualModelRunners = new List<VirtualModelRunner> { vmr }
            };

            RestoreResult result = await _Controller.RestoreBackup(new RestoreRequest
            {
                Package = package,
                Options = new RestoreOptions
                {
                    ConflictResolution = ConflictResolutionMode.Fail
                }
            }).ConfigureAwait(false);

            result.Success.Should().BeTrue();
            result.Summary.EndpointGroups.Created.Should().Be(1);
            EndpointGroup restoredGroup = await Database.EndpointGroup.ReadAsync(tenant.Id, group.Id).ConfigureAwait(false);
            restoredGroup.Should().NotBeNull();
            restoredGroup.EndpointIds.Should().ContainSingle(endpoint.Id);
            VirtualModelRunner restoredVmr = await Database.VirtualModelRunner.ReadAsync(tenant.Id, vmr.Id).ConfigureAwait(false);
            restoredVmr.EndpointGroupIds.Should().ContainSingle(group.Id);
        }

        public async Task ValidateBackup_WithCrossTenantEndpointGroupReference_ReturnsValidationError()
        {
            TenantMetadata otherTenant = await Database.Tenant.CreateAsync(new TenantMetadata
            {
                Name = "Endpoint Group Other Tenant",
                Active = true
            }).ConfigureAwait(false);
            EndpointGroup otherGroup = await Database.EndpointGroup.CreateAsync(new EndpointGroup
            {
                TenantId = otherTenant.Id,
                Name = "Other Tenant Group",
                EndpointIds = new List<string> { "mre_missing" }
            }).ConfigureAwait(false);

            BackupPackage package = new BackupPackage
            {
                SchemaVersion = "1.4",
                VirtualModelRunners = new List<VirtualModelRunner>
                {
                    new VirtualModelRunner
                    {
                        TenantId = TestTenantId,
                        Name = "Cross Tenant Group VMR",
                        BasePath = "/v1.0/api/cross-tenant-group/",
                        EndpointGroupIds = new List<string> { otherGroup.Id }
                    }
                }
            };

            ValidationResult result = await _Controller.ValidateBackup(package).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(item => item.Contains("cross-tenant endpoint group"));
        }

        public async Task RestoreBackup_WithVirtualModelRunnerReservation_RestoresSubjects()
        {
            TenantMetadata tenant = new TenantMetadata
            {
                Name = "Restored Reservation Tenant",
                Active = true
            };
            UserMaster user = new UserMaster
            {
                TenantId = tenant.Id,
                FirstName = "Restored",
                LastName = "Reservation",
                Email = "restored-reservation-" + Guid.NewGuid().ToString("N") + "@example.com",
                Password = "password",
                Active = true
            };
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = tenant.Id,
                Name = "Restored Reservation VMR",
                BasePath = "/v1.0/api/restored-reservation/"
            };
            VirtualModelRunnerReservation reservation = new VirtualModelRunnerReservation
            {
                TenantId = tenant.Id,
                VirtualModelRunnerId = vmr.Id,
                Name = "Restored Reservation",
                StartUtc = DateTime.UtcNow.AddHours(4),
                EndUtc = DateTime.UtcNow.AddHours(5),
                Subjects = new List<VirtualModelRunnerReservationSubject>
                {
                    new VirtualModelRunnerReservationSubject
                    {
                        TenantId = tenant.Id,
                        SubjectType = ReservationSubjectTypeEnum.User,
                        SubjectId = user.Id
                    }
                }
            };

            BackupPackage package = new BackupPackage
            {
                SchemaVersion = "1.3",
                Tenants = new List<TenantMetadata> { tenant },
                Users = new List<UserMaster> { user },
                VirtualModelRunners = new List<VirtualModelRunner> { vmr },
                VirtualModelRunnerReservations = new List<VirtualModelRunnerReservation> { reservation }
            };

            RestoreResult result = await _Controller.RestoreBackup(new RestoreRequest
            {
                Package = package,
                Options = new RestoreOptions
                {
                    ConflictResolution = ConflictResolutionMode.Fail
                }
            }).ConfigureAwait(false);

            result.Success.Should().BeTrue();
            result.Summary.VirtualModelRunnerReservations.Created.Should().Be(1);
            VirtualModelRunnerReservation restored = await Database.VirtualModelRunnerReservation.ReadAsync(tenant.Id, reservation.Id).ConfigureAwait(false);
            restored.Should().NotBeNull();
            restored.Subjects.Should().ContainSingle(item => item.SubjectId == user.Id);
        }

        public async Task ValidateBackup_WithMissingModelAccessRulePolicy_ReturnsValidationError()
        {
            BackupPackage package = new BackupPackage
            {
                SchemaVersion = "1.0",
                ModelAccessRules = new List<ModelAccessRule>
                {
                    new ModelAccessRule
                    {
                        TenantId = TestTenantId,
                        PolicyId = "map_missing",
                        Name = "Broken rule",
                        Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
                    }
                }
            };

            ValidationResult result = await _Controller.ValidateBackup(package).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(item => item.Contains("references missing policy"));
        }

        public async Task ValidateBackup_WithCrossTenantModelAccessRuleReference_ReturnsValidationError()
        {
            TenantMetadata otherTenant = await Database.Tenant.CreateAsync(new TenantMetadata
            {
                Name = "Other Tenant",
                Active = true
            }).ConfigureAwait(false);
            ModelDefinition otherTenantModel = await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = otherTenant.Id,
                Name = "other-tenant-model",
                Active = true
            }).ConfigureAwait(false);
            ModelAccessPolicy policy = new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "Cross Tenant Policy",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny
            };

            BackupPackage package = new BackupPackage
            {
                SchemaVersion = "1.0",
                ModelAccessPolicies = new List<ModelAccessPolicy> { policy },
                ModelAccessRules = new List<ModelAccessRule>
                {
                    new ModelAccessRule
                    {
                        TenantId = TestTenantId,
                        PolicyId = policy.Id,
                        Name = "Cross tenant model",
                        Effect = ModelAccessRuleEffectEnum.Allow,
                        SubjectType = ModelAccessSubjectTypeEnum.Any,
                        ResourceType = ModelAccessResourceTypeEnum.ModelDefinition,
                        ResourceId = otherTenantModel.Id,
                        Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
                    }
                }
            };

            ValidationResult result = await _Controller.ValidateBackup(package).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(item => item.Contains("cross-tenant model definition"));
        }

        public async Task RestoreBackup_WithCrossTenantModelAccessRuleReference_Fails()
        {
            TenantMetadata tenant = await Database.Tenant.ReadAsync(TestTenantId).ConfigureAwait(false);
            TenantMetadata otherTenant = await Database.Tenant.CreateAsync(new TenantMetadata
            {
                Name = "Restore Other Tenant",
                Active = true
            }).ConfigureAwait(false);
            ModelDefinition otherTenantModel = await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = otherTenant.Id,
                Name = "restore-other-tenant-model",
                Active = true
            }).ConfigureAwait(false);
            ModelAccessPolicy policy = new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "Restore Cross Tenant Policy",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny
            };

            BackupPackage package = new BackupPackage
            {
                SchemaVersion = "1.0",
                Tenants = new List<TenantMetadata> { tenant },
                ModelAccessPolicies = new List<ModelAccessPolicy> { policy },
                ModelAccessRules = new List<ModelAccessRule>
                {
                    new ModelAccessRule
                    {
                        TenantId = TestTenantId,
                        PolicyId = policy.Id,
                        Name = "Cross tenant model",
                        Effect = ModelAccessRuleEffectEnum.Allow,
                        SubjectType = ModelAccessSubjectTypeEnum.Any,
                        ResourceType = ModelAccessResourceTypeEnum.ModelDefinition,
                        ResourceId = otherTenantModel.Id,
                        Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
                    }
                }
            };

            RestoreResult result = await _Controller.RestoreBackup(new RestoreRequest
            {
                Package = package,
                Options = new RestoreOptions
                {
                    ConflictResolution = ConflictResolutionMode.Skip
                }
            }).ConfigureAwait(false);

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("cross-tenant model definition");
        }

        public async Task RestoreBackup_WithOverwrittenModelAccessPolicy_InvalidatesEvaluatorCache()
        {
            TenantMetadata tenant = await Database.Tenant.ReadAsync(TestTenantId).ConfigureAwait(false);
            ModelAccessPolicy policy = await Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "Cached Restore Policy",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Permit
            }).ConfigureAwait(false);
            ModelAccessEvaluationContext context = new ModelAccessEvaluationContext
            {
                TenantId = TestTenantId,
                ModelAccessPolicyId = policy.Id,
                RequestedModel = "llama3",
                EffectiveModel = "llama3",
                RequestType = RequestTypeEnum.OpenAIChatCompletions
            };

            (await _ModelAccessService.EvaluateAsync(context).ConfigureAwait(false)).Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);

            BackupPackage package = new BackupPackage
            {
                SchemaVersion = "1.0",
                Tenants = new List<TenantMetadata> { tenant },
                ModelAccessPolicies = new List<ModelAccessPolicy>
                {
                    new ModelAccessPolicy
                    {
                        Id = policy.Id,
                        TenantId = TestTenantId,
                        Name = "Cached Restore Policy",
                        DefaultDecision = ModelAccessDefaultDecisionEnum.Deny
                    }
                }
            };

            RestoreResult result = await _Controller.RestoreBackup(new RestoreRequest
            {
                Package = package,
                Options = new RestoreOptions
                {
                    ConflictResolution = ConflictResolutionMode.Overwrite
                }
            }).ConfigureAwait(false);

            result.Success.Should().BeTrue();
            (await _ModelAccessService.EvaluateAsync(context).ConfigureAwait(false)).Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
        }
    }
}
