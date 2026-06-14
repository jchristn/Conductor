namespace Test.Shared.Server.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Tests for model access filtering of list-model proxy responses.
    /// </summary>
    public class ModelAccessListModelsResponseFilterTests : Test.Shared.Server.Controllers.ControllerTestBase
    {
        private Credential _Credential;
        private ModelDefinition _AllowedDefinition;
        private ModelDefinition _DeniedDefinition;
        private VirtualModelRunner _Vmr;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);

            _Credential = await Database.Credential.CreateAsync(new Credential
            {
                TenantId = TestTenantId,
                UserId = TestUserId,
                Name = "List Models Credential"
            }).ConfigureAwait(false);

            _AllowedDefinition = await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = TestTenantId,
                Name = "allowed-model",
                SupportsCompletions = true,
                SupportsEmbeddings = true,
                Active = true
            }).ConfigureAwait(false);

            _DeniedDefinition = await Database.ModelDefinition.CreateAsync(new ModelDefinition
            {
                TenantId = TestTenantId,
                Name = "denied-model",
                SupportsCompletions = true,
                Active = true
            }).ConfigureAwait(false);

            ModelAccessPolicy policy = await Database.ModelAccessPolicy.CreateAsync(new ModelAccessPolicy
            {
                TenantId = TestTenantId,
                Name = "List models policy",
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny,
                Active = true
            }).ConfigureAwait(false);

            await Database.ModelAccessPolicy.CreateRuleAsync(new ModelAccessRule
            {
                TenantId = TestTenantId,
                PolicyId = policy.Id,
                Name = "Allow listed model",
                Priority = 100,
                Effect = ModelAccessRuleEffectEnum.Allow,
                SubjectType = ModelAccessSubjectTypeEnum.Credential,
                SubjectId = _Credential.Id,
                ResourceType = ModelAccessResourceTypeEnum.ModelDefinition,
                ResourceId = _AllowedDefinition.Id,
                Actions = new List<ModelAccessActionEnum> { ModelAccessActionEnum.ListModels },
                Active = true
            }).ConfigureAwait(false);

            _Vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "List Models VMR",
                BasePath = "/v1.0/api/list-models/",
                ModelAccessPolicyId = policy.Id,
                ModelDefinitionIds = new List<string> { _AllowedDefinition.Id, _DeniedDefinition.Id }
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

        public async Task Apply_FilterMode_RemovesDeniedOpenAIModels()
        {
            ModelAccessListModelsResponseFilter filter = CreateFilter(ModelAccessListModelsBehaviorEnum.Filter);
            byte[] upstream = Encoding.UTF8.GetBytes(
                "{\"object\":\"list\",\"data\":[{\"id\":\"allowed-model\"},{\"id\":\"denied-model\"},{\"id\":\"unknown-model\"}]}");

            byte[] filtered = await filter.ApplyAsync(_Vmr, CreateRoutingResult(), CreateRequestContext(RequestTypeEnum.OpenAIListModels, ApiTypeEnum.OpenAI), upstream).ConfigureAwait(false);

            JsonNode root = JsonNode.Parse(Encoding.UTF8.GetString(filtered));
            List<string> modelIds = root["data"].AsArray().Select(item => item["id"].GetValue<string>()).ToList();
            modelIds.Should().Equal("allowed-model");
        }

        public async Task Apply_SynthesizeMode_ReturnsAllowedGeminiModels()
        {
            ModelAccessListModelsResponseFilter filter = CreateFilter(ModelAccessListModelsBehaviorEnum.Synthesize);
            byte[] upstream = Encoding.UTF8.GetBytes("{\"models\":[{\"name\":\"models/denied-model\"}]}");

            byte[] filtered = await filter.ApplyAsync(_Vmr, CreateRoutingResult(), CreateRequestContext(RequestTypeEnum.GeminiListModels, ApiTypeEnum.Gemini), upstream).ConfigureAwait(false);

            JsonNode root = JsonNode.Parse(Encoding.UTF8.GetString(filtered));
            JsonArray models = root["models"].AsArray();
            models.Should().HaveCount(1);
            models[0]["name"].GetValue<string>().Should().Be("models/allowed-model");
            models[0]["supportedGenerationMethods"].AsArray().Select(item => item.GetValue<string>())
                .Should().Equal("generateContent", "embedContent");
        }

        public async Task Apply_RawPassThrough_ReturnsOriginalOllamaResponse()
        {
            ModelAccessListModelsResponseFilter filter = CreateFilter(ModelAccessListModelsBehaviorEnum.RawPassThrough);
            byte[] upstream = Encoding.UTF8.GetBytes(
                "{\"models\":[{\"name\":\"allowed-model\"},{\"name\":\"denied-model\"}]}");

            byte[] filtered = await filter.ApplyAsync(_Vmr, CreateRoutingResult(), CreateRequestContext(RequestTypeEnum.OllamaListTags, ApiTypeEnum.Ollama), upstream).ConfigureAwait(false);

            Encoding.UTF8.GetString(filtered).Should().Be(Encoding.UTF8.GetString(upstream));
        }

        private ModelAccessListModelsResponseFilter CreateFilter(ModelAccessListModelsBehaviorEnum behavior)
        {
            ModelAccessControlSettings settings = new ModelAccessControlSettings
            {
                Enabled = true,
                Mode = ModelAccessEnforcementModeEnum.Enforce,
                DefaultDecision = ModelAccessDefaultDecisionEnum.Deny,
                ListModelsBehavior = behavior
            };

            return new ModelAccessListModelsResponseFilter(
                Database,
                new ModelAccessControlService(Database, Logging, settings),
                settings,
                Logging);
        }

        private RoutingExecutionResult CreateRoutingResult()
        {
            return new RoutingExecutionResult
            {
                Decision = new RoutingDecision
                {
                    Success = true,
                    HttpStatusCode = 200
                }
            };
        }

        private RequestContext CreateRequestContext(RequestTypeEnum requestType, ApiTypeEnum apiType)
        {
            return new RequestContext
            {
                TenantId = TestTenantId,
                UserId = TestUserId,
                CredentialId = _Credential.Id,
                VirtualModelRunnerId = _Vmr.Id,
                RequestType = requestType,
                ApiType = apiType
            };
        }
    }
}
