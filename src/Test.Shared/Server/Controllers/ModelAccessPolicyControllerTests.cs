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
    using WatsonWebserver.Core;

    /// <summary>
    /// Unit tests for ModelAccessPolicyController.
    /// </summary>
    public class ModelAccessPolicyControllerTests : ControllerTestBase
    {
        private ModelAccessPolicyController _Controller;
        private ModelAccessControlService _ModelAccessService;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _ModelAccessService = new ModelAccessControlService(Database, Logging, new ModelAccessControlSettings
            {
                Enabled = true,
                Mode = ModelAccessEnforcementModeEnum.Enforce,
                DefaultDecision = ModelAccessDefaultDecisionEnum.Permit,
                UnknownModelBehavior = ModelAccessUnknownModelBehaviorEnum.Permit,
                CacheTtlMs = 600000
            });
            _Controller = new ModelAccessPolicyController(Database, AuthService, Serializer, Logging, _ModelAccessService);
        }

        /// <summary>
        /// Cleanup after test.
        /// </summary>
        /// <returns>Task.</returns>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task Create_WithNestedRule_PersistsRulesAndEvaluates()
        {
            ModelAccessPolicy created = await _Controller.Create(TestTenantId, new ModelAccessPolicy
            {
                Name = "Deny policy",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Permit,
                Rules = new List<ModelAccessRule>
                {
                    DenyAnyRule("Deny all completions")
                }
            }).ConfigureAwait(false);

            created.Rules.Should().ContainSingle();

            ModelAccessEvaluationResult result = await _Controller.Evaluate(TestTenantId, created.Id, new ModelAccessEvaluationContext
            {
                RequestedModel = "llama3",
                EffectiveModel = "llama3",
                RequestType = RequestTypeEnum.OpenAIChatCompletions
            }).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
            result.RuleId.Should().Be(created.Rules[0].Id);
        }

        public async Task Update_ReplacesRulesAndInvalidatesCachedPolicy()
        {
            ModelAccessPolicy created = await _Controller.Create(TestTenantId, new ModelAccessPolicy
            {
                Name = "Cached policy",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Permit
            }).ConfigureAwait(false);

            ModelAccessEvaluationContext context = new ModelAccessEvaluationContext
            {
                TenantId = TestTenantId,
                ModelAccessPolicyId = created.Id,
                RequestedModel = "llama3",
                EffectiveModel = "llama3",
                RequestType = RequestTypeEnum.OpenAIChatCompletions
            };

            (await _ModelAccessService.EvaluateAsync(context).ConfigureAwait(false)).Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);

            await _Controller.Update(TestTenantId, created.Id, new ModelAccessPolicy
            {
                Name = "Cached policy",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny
            }).ConfigureAwait(false);

            (await _ModelAccessService.EvaluateAsync(context).ConfigureAwait(false)).Decision.Should().Be(ModelAccessDefaultDecisionEnum.Deny);
        }

        public async Task Delete_AttachedPolicyRequiresForceDetach()
        {
            ModelAccessPolicy created = await _Controller.Create(TestTenantId, new ModelAccessPolicy
            {
                Name = "Attached policy"
            }).ConfigureAwait(false);

            VirtualModelRunner vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Attached VMR",
                BasePath = "/v1.0/api/attached-map/",
                ModelAccessPolicyId = created.Id
            }).ConfigureAwait(false);

            Func<Task> deleteWithoutForce = async () => await _Controller.Delete(TestTenantId, created.Id).ConfigureAwait(false);
            await deleteWithoutForce.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);

            VirtualModelRunner stillAttached = await Database.VirtualModelRunner.ReadAsync(TestTenantId, vmr.Id).ConfigureAwait(false);
            stillAttached.ModelAccessPolicyId.Should().Be(created.Id);

            await _Controller.Delete(TestTenantId, created.Id, forceDetach: true).ConfigureAwait(false);

            VirtualModelRunner read = await Database.VirtualModelRunner.ReadAsync(TestTenantId, vmr.Id).ConfigureAwait(false);
            read.ModelAccessPolicyId.Should().BeNull();
        }

        public async Task GetEffective_UsesAttachedVmrPolicyAndCredentialLabels()
        {
            Credential credential = await CreateCredentialAsync("premium").ConfigureAwait(false);
            ModelAccessRule allowRule = new ModelAccessRule
            {
                Name = "Allow premium",
                Effect = ModelAccessRuleEffectEnum.Allow,
                SubjectType = ModelAccessSubjectTypeEnum.CredentialLabel,
                SubjectId = "premium",
                ResourceType = ModelAccessResourceTypeEnum.Any,
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
            };
            ModelAccessPolicy policy = await _Controller.Create(TestTenantId, new ModelAccessPolicy
            {
                Name = "Effective policy",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny,
                Rules = new List<ModelAccessRule> { allowRule }
            }).ConfigureAwait(false);
            VirtualModelRunner vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Effective VMR",
                BasePath = "/v1.0/api/effective-map/",
                ModelAccessPolicyId = policy.Id
            }).ConfigureAwait(false);

            ModelAccessEvaluationResult result = await _Controller.GetEffective(
                TestTenantId,
                credential.Id,
                null,
                vmr.Id,
                null,
                "llama3",
                ModelAccessActionEnum.Completions).ConfigureAwait(false);

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            result.RuleId.Should().Be(policy.Rules[0].Id);
        }

        private static ModelAccessRule DenyAnyRule(string name)
        {
            return new ModelAccessRule
            {
                Name = name,
                Effect = ModelAccessRuleEffectEnum.Deny,
                SubjectType = ModelAccessSubjectTypeEnum.Any,
                ResourceType = ModelAccessResourceTypeEnum.Any,
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions }
            };
        }

        private async Task<Credential> CreateCredentialAsync(params string[] labels)
        {
            UserMaster user = await Database.User.CreateAsync(new UserMaster
            {
                TenantId = TestTenantId,
                FirstName = "Model",
                LastName = "Access",
                Email = "model-access-" + Guid.NewGuid().ToString("N") + "@example.com",
                Password = "password"
            }).ConfigureAwait(false);

            return await Database.Credential.CreateAsync(new Credential
            {
                TenantId = TestTenantId,
                UserId = user.Id,
                Name = "Model Access Credential",
                Labels = new List<string>(labels ?? Array.Empty<string>())
            }).ConfigureAwait(false);
        }
    }
}
