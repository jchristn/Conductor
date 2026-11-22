namespace Test.Shared.Server.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Authorization;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;

    /// <summary>
    /// Tests for the model access mapping of the new request types, including that Completions never implies Decisions.
    /// </summary>
    public class DecisionAccessMappingTests : ControllerTestBase
    {
        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
        }

        public void MapAction_NewRequestTypes()
        {
            ModelAccessControlService.MapAction(RequestTypeEnum.TypeSafeDecide).Should().Be(ModelAccessActionEnum.Decisions);
            ModelAccessControlService.MapAction(RequestTypeEnum.TypeSafeListModels).Should().Be(ModelAccessActionEnum.ListModels);
            ModelAccessControlService.MapAction(RequestTypeEnum.AnthropicMessages).Should().Be(ModelAccessActionEnum.Completions);
            ModelAccessControlService.MapAction(RequestTypeEnum.AnthropicCountTokens).Should().Be(ModelAccessActionEnum.Completions);
            ModelAccessControlService.MapAction(RequestTypeEnum.AnthropicListModels).Should().Be(ModelAccessActionEnum.ListModels);
            ModelAccessControlService.MapAction(RequestTypeEnum.AnthropicGetModel).Should().Be(ModelAccessActionEnum.ShowModel);
        }

        public void AuthorizationConfig_NewRoutesRequireAuthentication()
        {
            foreach (RequestTypeEnum type in new[]
            {
                RequestTypeEnum.AnthropicMessages, RequestTypeEnum.AnthropicCountTokens, RequestTypeEnum.AnthropicListModels,
                RequestTypeEnum.AnthropicGetModel, RequestTypeEnum.TypeSafeDecide, RequestTypeEnum.TypeSafeListModels,
                RequestTypeEnum.ReadModelRunnerEndpointApiKey
            })
            {
                AuthorizationConfig.RequiresAnyAuth(type).Should().BeTrue(type.ToString());
                AuthorizationConfig.IsPublic(type).Should().BeFalse(type.ToString());
            }

            // UT-017: revealing a full upstream key requires a tenant admin or global admin, unlike updating the endpoint.
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.ReadModelRunnerEndpointApiKey).Should().BeTrue();
            AuthorizationConfig.RequiresTenantAdmin(RequestTypeEnum.UpdateModelRunnerEndpoint).Should().BeFalse();
            RequestTypeResolver.Resolve("GET", "/v1.0/modelrunnerendpoints/mre_123/apikey").Should().Be(RequestTypeEnum.ReadModelRunnerEndpointApiKey);
            RequestTypeResolver.Resolve("POST", "/v1.0/api/vmr_1/v1/chat/completions").Should().Be(RequestTypeEnum.Unknown);
        }

        public void QosMetadataTypes_IncludeNewModelListingRoutes()
        {
            Conductor.Core.Helpers.QosProfileFactory.MetadataRequestTypes.Should().Contain(new[]
            {
                RequestTypeEnum.AnthropicListModels, RequestTypeEnum.AnthropicGetModel, RequestTypeEnum.TypeSafeListModels
            });
            Conductor.Core.Helpers.QosProfileFactory.MetadataRequestTypes.Should().NotContain(RequestTypeEnum.TypeSafeDecide);
            Conductor.Core.Helpers.QosProfileFactory.MetadataRequestTypes.Should().NotContain(RequestTypeEnum.AnthropicMessages);
        }

        public void ApiFamilyResolver_NewFamilies()
        {
            ApiFamilyResolver.Resolve(RequestTypeEnum.AnthropicMessages).Should().Be("Anthropic");
            ApiFamilyResolver.Resolve(RequestTypeEnum.AnthropicGetModel).Should().Be("Anthropic");
            ApiFamilyResolver.Resolve(RequestTypeEnum.TypeSafeDecide).Should().Be("TypeSafe");
            ApiFamilyResolver.Resolve(RequestTypeEnum.OpenAIChatCompletions).Should().Be("OpenAI");
            ApiFamilyResolver.Resolve(RequestTypeEnum.OllamaChat).Should().Be("Ollama");
            ApiFamilyResolver.Resolve(RequestTypeEnum.GeminiListModels).Should().Be("Gemini");
            ApiFamilyResolver.Resolve(RequestTypeEnum.Unknown).Should().Be("Management");
        }

        public async Task CompletionsAllowRule_DoesNotAllowDecisions()
        {
            ModelDefinition definition = await Database.ModelDefinition.CreateAsync(new ModelDefinition { TenantId = TestTenantId, Name = "jev-latest", SupportsDecisions = true }).ConfigureAwait(false);
            ModelAccessPolicy policy = await Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "completions only",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny,
                Active = true
            }).ConfigureAwait(false);
            await Database.ModelAccessPolicy.CreateRuleAsync(new ModelAccessRule
            {
                TenantId = TestTenantId,
                PolicyId = policy.Id,
                Name = "allow completions",
                Priority = 100,
                Effect = ModelAccessRuleEffectEnum.Allow,
                SubjectType = ModelAccessSubjectTypeEnum.User,
                SubjectId = TestUserId,
                ResourceType = ModelAccessResourceTypeEnum.ModelDefinition,
                ResourceId = definition.Id,
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.Completions },
                Active = true
            }).ConfigureAwait(false);

            ModelAccessControlSettings settings = new ModelAccessControlSettings { Enabled = true, Mode = ModelAccessEnforcementModeEnum.Enforce, DefaultDecision = ModelAccessDefaultDecisionEnum.Deny };
            ModelAccessControlService service = new ModelAccessControlService(Database, Logging, settings);

            ModelAccessEvaluationResult completions = await service.EvaluateAsync(Context(policy, definition, ModelAccessActionEnum.Completions, RequestTypeEnum.AnthropicMessages)).ConfigureAwait(false);
            ModelAccessEvaluationResult decisions = await service.EvaluateAsync(Context(policy, definition, ModelAccessActionEnum.Decisions, RequestTypeEnum.TypeSafeDecide)).ConfigureAwait(false);

            completions.Allowed.Should().BeTrue();
            decisions.Allowed.Should().BeFalse();
        }

        private ModelAccessEvaluationContext Context(ModelAccessPolicy policy, ModelDefinition definition, ModelAccessActionEnum action, RequestTypeEnum requestType)
        {
            return new ModelAccessEvaluationContext
            {
                TenantId = TestTenantId,
                UserId = TestUserId,
                ModelAccessPolicyId = policy.Id,
                RequestedModel = definition.Name,
                EffectiveModel = definition.Name,
                ModelDefinitionId = definition.Id,
                ModelDefinitionName = definition.Name,
                Action = action,
                RequestType = requestType,
                ApiType = ApiTypeEnum.TypeSafe
            };
        }
    }
}
