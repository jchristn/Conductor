namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for model access control service evaluation and validation.
    /// </summary>
    public class ModelAccessControlServiceTests : Test.Shared.Server.Controllers.ControllerTestBase
    {
        private UserMaster _User;
        private Credential _Credential;
        private ModelDefinition _ModelDefinition;
        private VirtualModelRunner _Vmr;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _User = await Database.User.ReadAsync(TestTenantId, TestUserId).ConfigureAwait(false);
            _User.Labels = new List<string> { "engineering" };
            _User = await Database.User.UpdateAsync(_User).ConfigureAwait(false);

            _Credential = await Database.Credential.CreateAsync(new Credential
            {
                TenantId = TestTenantId,
                UserId = TestUserId,
                Name = "ACL Test Credential",
                Labels = new List<string> { "finance" }
            }).ConfigureAwait(false);

            _ModelDefinition = await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = TestTenantId,
                Name = "llama3.2:latest",
                Labels = new List<string> { "general", "chat" },
                Active = true
            }).ConfigureAwait(false);

            _Vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "ACL Test VMR",
                BasePath = "/v1.0/api/acl-service/"
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task Evaluate_DisabledMode_PermitsAndRecordsDisabledState()
        {
            ModelAccessControlService service = CreateService(enabled: false, mode: ModelAccessEnforcementModeEnum.Enforce, defaultDecision: ModelAccessDefaultDecisionEnum.Deny);

            ModelAccessEvaluationResult result = await service.EvaluateAsync(CreateContext()).ConfigureAwait(false);

            result.Allowed.Should().BeTrue();
            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            result.Mode.Should().Be(ModelAccessEnforcementModeEnum.Disabled);
            result.ReasonCode.Should().Be("ModelAccessDisabled");
        }

        public async Task Evaluate_NoPolicy_UsesGlobalDefault()
        {
            ModelAccessControlService service = CreateService(defaultDecision: ModelAccessDefaultDecisionEnum.Deny);
            ModelAccessEvaluationContext context = CreateContext();
            context.ModelAccessPolicyId = null;

            ModelAccessEvaluationResult result = await service.EvaluateAsync(context).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            result.Allowed.Should().BeFalse();
            result.DefaultSource.Should().Be("Global");
            result.ReasonCode.Should().Be("NoModelAccessPolicy");
        }

        public async Task Evaluate_TenantAdministratorBypass_RequiresSetting()
        {
            ModelAccessEvaluationContext context = CreateContext();
            context.ModelAccessPolicyId = null;
            context.IsUserTenantAdmin = true;

            ModelAccessEvaluationResult denied = await CreateService(defaultDecision: ModelAccessDefaultDecisionEnum.Deny)
                .EvaluateAsync(context)
                .ConfigureAwait(false);

            denied.Allowed.Should().BeFalse();
            denied.ReasonCode.Should().Be("NoModelAccessPolicy");

            ModelAccessEvaluationResult allowed = await CreateService(
                    defaultDecision: ModelAccessDefaultDecisionEnum.Deny,
                    allowAdministratorBypass: true)
                .EvaluateAsync(context)
                .ConfigureAwait(false);

            allowed.Allowed.Should().BeTrue();
            allowed.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            allowed.DefaultSource.Should().Be("AdministratorBypass");
            allowed.ReasonCode.Should().Be("AdministratorBypass");
        }

        public async Task Evaluate_GlobalAdministratorBypass_CanUseGlobalOnlySetting()
        {
            ModelAccessEvaluationContext context = CreateContext();
            context.ModelAccessPolicyId = null;
            context.IsUserAdmin = true;

            ModelAccessEvaluationResult result = await CreateService(
                    defaultDecision: ModelAccessDefaultDecisionEnum.Deny,
                    allowGlobalAdministratorBypass: true)
                .EvaluateAsync(context)
                .ConfigureAwait(false);

            result.Allowed.Should().BeTrue();
            result.DefaultSource.Should().Be("AdministratorBypass");
        }

        public async Task Evaluate_MonitorMode_DenyIsAllowedAndWouldDeny()
        {
            ModelAccessPolicy policy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Deny,
                new ModelAccessRule
                {
                    TenantId = TestTenantId,
                    Name = "Deny all completions",
                    SubjectType = ModelAccessSubjectTypeEnum.Any,
                    ResourceType = ModelAccessResourceTypeEnum.Any,
                    Effect = ModelAccessRuleEffectEnum.Deny,
                    Priority = 10,
                    Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
                }).ConfigureAwait(false);

            ModelAccessControlService service = CreateService(mode: ModelAccessEnforcementModeEnum.Monitor);

            ModelAccessEvaluationResult result = await service.EvaluateAsync(CreateContext(policy.Id)).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            result.Allowed.Should().BeTrue();
            result.WouldDeny.Should().BeTrue();
            result.Mode.Should().Be(ModelAccessEnforcementModeEnum.Monitor);
        }

        public async Task Evaluate_ExplicitCredentialAllow_Permits()
        {
            ModelAccessPolicy policy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Deny,
                new ModelAccessRule
                {
                    TenantId = TestTenantId,
                    Name = "Allow credential",
                    SubjectType = ModelAccessSubjectTypeEnum.Credential,
                    SubjectId = _Credential.Id,
                    ResourceType = ModelAccessResourceTypeEnum.ModelDefinition,
                    ResourceId = _ModelDefinition.Id,
                    Effect = ModelAccessRuleEffectEnum.Allow,
                    Priority = 10,
                    Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
                }).ConfigureAwait(false);

            ModelAccessEvaluationResult result = await CreateService().EvaluateAsync(CreateContext(policy.Id)).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            result.Allowed.Should().BeTrue();
            result.RuleName.Should().Be("Allow credential");
            result.ReasonCode.Should().Be("MatchedAllowRule");
        }

        public async Task Evaluate_CredentialLabelDeny_Denies()
        {
            ModelAccessPolicy policy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Permit,
                new ModelAccessRule
                {
                    TenantId = TestTenantId,
                    Name = "Deny finance",
                    SubjectType = ModelAccessSubjectTypeEnum.CredentialLabel,
                    SubjectId = "finance",
                    ResourceType = ModelAccessResourceTypeEnum.Any,
                    Effect = ModelAccessRuleEffectEnum.Deny,
                    Priority = 10,
                    Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
                }).ConfigureAwait(false);

            ModelAccessEvaluationResult result = await CreateService().EvaluateAsync(CreateContext(policy.Id)).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            result.Allowed.Should().BeFalse();
            result.ReasonCode.Should().Be("MatchedDenyRule");
        }

        public async Task Evaluate_UserAndUserLabelRules_Match()
        {
            ModelAccessPolicy policy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Deny,
                new ModelAccessRule
                {
                    TenantId = TestTenantId,
                    Name = "Allow engineering users",
                    SubjectType = ModelAccessSubjectTypeEnum.UserLabel,
                    SubjectSelector = new Dictionary<string, string> { { "label", "engineering" } },
                    ResourceType = ModelAccessResourceTypeEnum.ModelName,
                    ResourceId = "llama3.2:latest",
                    Effect = ModelAccessRuleEffectEnum.Allow,
                    Priority = 10,
                    Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
                }).ConfigureAwait(false);

            ModelAccessEvaluationResult result = await CreateService().EvaluateAsync(CreateContext(policy.Id)).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            result.RuleName.Should().Be("Allow engineering users");
        }

        public async Task Evaluate_TenantAndAnyRules_Match()
        {
            ModelAccessPolicy policy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Deny,
                new ModelAccessRule
                {
                    TenantId = TestTenantId,
                    Name = "Tenant allow",
                    SubjectType = ModelAccessSubjectTypeEnum.Tenant,
                    SubjectId = TestTenantId,
                    ResourceType = ModelAccessResourceTypeEnum.Any,
                    Effect = ModelAccessRuleEffectEnum.Allow,
                    Priority = 10,
                    Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
                }).ConfigureAwait(false);

            ModelAccessEvaluationResult result = await CreateService().EvaluateAsync(CreateContext(policy.Id)).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            result.RuleName.Should().Be("Tenant allow");
        }

        public async Task Evaluate_ResourceMatchers_CoverModelNameLabelsAndVmr()
        {
            ModelAccessPolicy modelNamePolicy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Deny,
                AllowRule("Allow effective model", ModelAccessResourceTypeEnum.ModelName, "mutated-model")).ConfigureAwait(false);
            ModelAccessEvaluationContext effectiveContext = CreateContext(modelNamePolicy.Id);
            effectiveContext.EffectiveModel = "mutated-model";

            ModelAccessPolicy labelPolicy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Deny,
                AllowRule("Allow chat labels", ModelAccessResourceTypeEnum.ModelLabel, "chat")).ConfigureAwait(false);

            ModelAccessPolicy vmrPolicy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Deny,
                AllowRule("Allow VMR", ModelAccessResourceTypeEnum.VirtualModelRunner, _Vmr.Id)).ConfigureAwait(false);

            (await CreateService().EvaluateAsync(effectiveContext).ConfigureAwait(false)).Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            (await CreateService().EvaluateAsync(CreateContext(labelPolicy.Id)).ConfigureAwait(false)).Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            (await CreateService().EvaluateAsync(CreateContext(vmrPolicy.Id)).ConfigureAwait(false)).Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
        }

        public async Task Evaluate_HigherPriorityWinsBeforeLowerPriority()
        {
            ModelAccessPolicy policy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Deny,
                DenyRule("Low deny", 10),
                AllowRule("High allow", ModelAccessResourceTypeEnum.Any, null, priority: 20)).ConfigureAwait(false);

            ModelAccessEvaluationResult result = await CreateService().EvaluateAsync(CreateContext(policy.Id)).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            result.RuleName.Should().Be("High allow");
        }

        public async Task Evaluate_DenyWinsOverAllowAtSamePriority()
        {
            ModelAccessPolicy policy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Permit,
                AllowRule("Same allow", ModelAccessResourceTypeEnum.Any, null, priority: 10),
                DenyRule("Same deny", 10)).ConfigureAwait(false);

            ModelAccessEvaluationResult result = await CreateService().EvaluateAsync(CreateContext(policy.Id)).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            result.RuleName.Should().Be("Same deny");
        }

        public async Task Evaluate_IgnoresInactiveRules()
        {
            ModelAccessRule inactiveAllow = AllowRule("Inactive allow", ModelAccessResourceTypeEnum.Any, null);
            inactiveAllow.Active = false;
            ModelAccessPolicy policy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Deny,
                inactiveAllow).ConfigureAwait(false);

            ModelAccessEvaluationResult result = await CreateService().EvaluateAsync(CreateContext(policy.Id)).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            result.RuleId.Should().BeNull();
            result.ReasonCode.Should().Be("PolicyDefaultDeny");
        }

        public async Task Evaluate_IgnoresInactivePolicies()
        {
            ModelAccessPolicy policy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Permit,
                DenyRule("Deny active model", 10)).ConfigureAwait(false);
            policy.Active = false;
            await Database.ModelAccessPolicy.UpdateAsync(policy).ConfigureAwait(false);

            ModelAccessEvaluationResult result = await CreateService(defaultDecision: ModelAccessDefaultDecisionEnum.Deny)
                .EvaluateAsync(CreateContext(policy.Id))
                .ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            result.DefaultSource.Should().Be("Global");
            result.ReasonCode.Should().Be("ModelAccessPolicyInactive");
        }

        public async Task Evaluate_UsesPolicyDefaultWhenNoRuleMatches()
        {
            ModelAccessPolicy policy = await CreatePolicyWithRulesAsync(
                ModelAccessDefaultDecisionEnum.Deny,
                new ModelAccessRule
                {
                    TenantId = TestTenantId,
                    Name = "Embeddings only",
                    SubjectType = ModelAccessSubjectTypeEnum.Any,
                    ResourceType = ModelAccessResourceTypeEnum.Any,
                    Effect = ModelAccessRuleEffectEnum.Allow,
                    Priority = 10,
                    Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Embeddings }
                }).ConfigureAwait(false);

            ModelAccessEvaluationResult result = await CreateService().EvaluateAsync(CreateContext(policy.Id)).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            result.DefaultSource.Should().Be("Policy");
            result.ReasonCode.Should().Be("PolicyDefaultDeny");
        }

        public async Task Evaluate_UnknownModelBehaviorDeny_DeniesUnresolvedModel()
        {
            ModelAccessControlService service = CreateService(unknownModelBehavior: ModelAccessUnknownModelBehaviorEnum.Deny);
            ModelAccessEvaluationContext context = CreateContext();
            context.RequestedModel = null;
            context.EffectiveModel = null;
            context.ModelDefinitionId = null;

            ModelAccessEvaluationResult result = await service.EvaluateAsync(context).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            result.ReasonCode.Should().Be("UnknownModelDenied");
        }

        public async Task Evaluate_UnknownModelBehaviorPermit_AllowsUnresolvedModel()
        {
            ModelAccessControlService service = CreateService(unknownModelBehavior: ModelAccessUnknownModelBehaviorEnum.Permit);
            ModelAccessEvaluationContext context = CreateContext();
            context.RequestedModel = null;
            context.EffectiveModel = null;
            context.ModelDefinitionId = null;
            context.ModelLabels = new List<string>();

            ModelAccessEvaluationResult result = await service.EvaluateAsync(context).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            result.Allowed.Should().BeTrue();
            result.ReasonCode.Should().Be("NoModelAccessPolicy");
        }

        public void MapAction_MapsListModelsAcrossApis()
        {
            ModelAccessControlService.MapAction(RequestTypeEnum.OpenAIListModels).Should().Be(ModelAccessActionEnum.ListModels);
            ModelAccessControlService.MapAction(RequestTypeEnum.GeminiListModels).Should().Be(ModelAccessActionEnum.ListModels);
            ModelAccessControlService.MapAction(RequestTypeEnum.OllamaListTags).Should().Be(ModelAccessActionEnum.ListModels);
        }

        public async Task ValidatePolicy_RejectsInvalidReferencesAndSelectors()
        {
            TenantMetadata otherTenant = await Database.Tenant.CreateAsync(new TenantMetadata { Name = "Other Tenant" }).ConfigureAwait(false);
            UserMaster otherUser = await Database.User.CreateAsync(new UserMaster
            {
                TenantId = otherTenant.Id,
                FirstName = "Other",
                LastName = "User",
                Email = "other@example.com",
                Password = "password"
            }).ConfigureAwait(false);

            ModelAccessPolicy policy = new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "Invalid policy",
                Rules = new List<ModelAccessRule>
                {
                    new ModelAccessRule
                    {
                        TenantId = TestTenantId,
                        PolicyId = "map_test",
                        Name = "Broken",
                        SubjectType = ModelAccessSubjectTypeEnum.User,
                        SubjectId = otherUser.Id,
                        ResourceType = ModelAccessResourceTypeEnum.ModelName,
                        ResourceSelector = new Dictionary<string, string> { { "unsupported", "x" } },
                        Actions = new List<ModelAccessActionEnum>()
                    }
                }
            };

            ResourceValidationResult result = await CreateService().ValidatePolicyAsync(TestTenantId, policy).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(item => item.Code == "ActionsRequired");
            result.Errors.Should().Contain(item => item.Code == "SelectorInvalid");
            result.Errors.Should().Contain(item => item.Code == "CrossTenantUser");
        }

        public async Task ValidatePolicy_RejectsCrossTenantCredentialAndModelReferences()
        {
            TenantMetadata otherTenant = await Database.Tenant.CreateAsync(new TenantMetadata { Name = "Other Tenant" }).ConfigureAwait(false);
            UserMaster otherUser = await Database.User.CreateAsync(new UserMaster
            {
                TenantId = otherTenant.Id,
                FirstName = "Other",
                LastName = "User",
                Email = "other-credential@example.com",
                Password = "password"
            }).ConfigureAwait(false);
            Credential otherCredential = await Database.Credential.CreateAsync(new Credential
            {
                TenantId = otherTenant.Id,
                UserId = otherUser.Id,
                Name = "Other Credential"
            }).ConfigureAwait(false);
            ModelDefinition otherModel = await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = otherTenant.Id,
                Name = "other-model",
                Active = true
            }).ConfigureAwait(false);

            ModelAccessPolicy policy = new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "Invalid cross tenant policy",
                Rules = new List<ModelAccessRule>
                {
                    new ModelAccessRule
                    {
                        TenantId = TestTenantId,
                        PolicyId = "map_test",
                        Name = "Broken cross tenant rule",
                        SubjectType = ModelAccessSubjectTypeEnum.Credential,
                        SubjectId = otherCredential.Id,
                        ResourceType = ModelAccessResourceTypeEnum.ModelDefinition,
                        ResourceId = otherModel.Id,
                        Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
                    }
                }
            };

            ResourceValidationResult result = await CreateService().ValidatePolicyAsync(TestTenantId, policy).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(item => item.Code == "CrossTenantCredential");
            result.Errors.Should().Contain(item => item.Code == "CrossTenantModelDefinition");
        }

        public async Task ValidatePolicy_RejectsEmptyName()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "   "
            };

            ResourceValidationResult result = await CreateService().ValidatePolicyAsync(TestTenantId, policy).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(item => item.Code == "NameRequired");
        }

        public async Task ValidatePolicy_WarnsOnDuplicatePriorities()
        {
            ModelAccessPolicy policy = new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "Duplicate priorities",
                Rules = new List<ModelAccessRule>
                {
                    AllowRule("Allow one", ModelAccessResourceTypeEnum.Any, null, priority: 10),
                    DenyRule("Deny one", 10)
                }
            };

            ResourceValidationResult result = await CreateService().ValidatePolicyAsync(TestTenantId, policy).ConfigureAwait(false);

            result.IsValid.Should().BeTrue();
            result.Warnings.Should().ContainSingle(item => item.Code == "DuplicatePriority");
        }

        public async Task ValidatePolicy_RejectsInactiveReferencedModelDefinition()
        {
            _ModelDefinition.Active = false;
            _ModelDefinition = await Database.ModelDefinition.UpdateAsync(_ModelDefinition).ConfigureAwait(false);

            ModelAccessPolicy policy = new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "Inactive model ref",
                Rules = new List<ModelAccessRule>
                {
                    AllowRule("Allow inactive model", ModelAccessResourceTypeEnum.ModelDefinition, _ModelDefinition.Id)
                }
            };

            ResourceValidationResult result = await CreateService().ValidatePolicyAsync(TestTenantId, policy).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(item => item.Code == "ModelDefinitionInactive");
        }

        private ModelAccessControlService CreateService(
            bool enabled = true,
            ModelAccessEnforcementModeEnum mode = ModelAccessEnforcementModeEnum.Enforce,
            ModelAccessDefaultDecisionEnum defaultDecision = ModelAccessDefaultDecisionEnum.Permit,
            ModelAccessUnknownModelBehaviorEnum unknownModelBehavior = ModelAccessUnknownModelBehaviorEnum.Permit,
            bool allowAdministratorBypass = false,
            bool allowGlobalAdministratorBypass = false)
        {
            return new ModelAccessControlService(Database, Logging, new ModelAccessControlSettings
            {
                Enabled = enabled,
                Mode = mode,
                DefaultDecision = defaultDecision,
                UnknownModelBehavior = unknownModelBehavior,
                AllowAdministratorBypass = allowAdministratorBypass,
                AllowGlobalAdministratorBypass = allowGlobalAdministratorBypass,
                CacheTtlMs = 0
            });
        }

        private ModelAccessEvaluationContext CreateContext(string policyId = null)
        {
            return new ModelAccessEvaluationContext
            {
                TenantId = TestTenantId,
                UserId = TestUserId,
                UserLabels = _User.Labels,
                CredentialId = _Credential.Id,
                CredentialLabels = _Credential.Labels,
                VirtualModelRunnerId = _Vmr.Id,
                ModelAccessPolicyId = policyId,
                RequestedModel = _ModelDefinition.Name,
                EffectiveModel = _ModelDefinition.Name,
                ModelDefinitionId = _ModelDefinition.Id,
                ModelDefinitionName = _ModelDefinition.Name,
                ModelLabels = _ModelDefinition.Labels,
                Action = ModelAccessActionEnum.Completions,
                RequestType = RequestTypeEnum.OpenAIChatCompletions,
                ApiType = ApiTypeEnum.OpenAI
            };
        }

        private async Task<ModelAccessPolicy> CreatePolicyWithRulesAsync(ModelAccessDefaultDecisionEnum defaultDecision, params ModelAccessRule[] rules)
        {
            ModelAccessPolicy policy = await Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "ACL Policy " + Guid.NewGuid().ToString("N"),
                DefaultDecision = defaultDecision,
                Active = true
            }).ConfigureAwait(false);

            foreach (ModelAccessRule rule in rules)
            {
                rule.TenantId = TestTenantId;
                rule.PolicyId = policy.Id;
                await Database.ModelAccessPolicy.CreateRuleAsync(rule).ConfigureAwait(false);
            }

            return policy;
        }

        private ModelAccessRule AllowRule(string name, ModelAccessResourceTypeEnum resourceType, string resourceId, int priority = 10)
        {
            return new ModelAccessRule
            {
                TenantId = TestTenantId,
                Name = name,
                SubjectType = ModelAccessSubjectTypeEnum.Any,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Effect = ModelAccessRuleEffectEnum.Allow,
                Priority = priority,
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
            };
        }

        private ModelAccessRule DenyRule(string name, int priority)
        {
            return new ModelAccessRule
            {
                TenantId = TestTenantId,
                Name = name,
                SubjectType = ModelAccessSubjectTypeEnum.Any,
                ResourceType = ModelAccessResourceTypeEnum.Any,
                Effect = ModelAccessRuleEffectEnum.Deny,
                Priority = priority,
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
            };
        }
    }
}
