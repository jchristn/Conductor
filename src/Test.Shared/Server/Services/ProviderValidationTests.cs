namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;
    using WatsonWebserver.Core;

    /// <summary>
    /// Tests for every v1.1.0 validation code.
    /// </summary>
    public class ProviderValidationTests : ControllerTestBase
    {
        private ConfigurationValidationService _Service;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Service = new ConfigurationValidationService(Database, Logging, new RoutingDecisionService(Database, Logging), new AnthropicSettings());
        }

        public async Task VmrApiTypeNotClientFacing_RejectsEndpointOnlyTypes()
        {
            foreach (ApiTypeEnum type in new[] { ApiTypeEnum.Ollaya, ApiTypeEnum.OllamaCloud, ApiTypeEnum.OpenAICompatible })
            {
                ResourceValidationResult result = await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, Vmr(type)).ConfigureAwait(false);
                result.IsValid.Should().BeFalse(type.ToString());
                ErrorCodes(result).Should().Contain("VmrApiTypeNotClientFacing");
            }

            foreach (ApiTypeEnum type in new[] { ApiTypeEnum.Ollama, ApiTypeEnum.OpenAI, ApiTypeEnum.Gemini, ApiTypeEnum.vLLM, ApiTypeEnum.Anthropic, ApiTypeEnum.TypeSafe })
            {
                ResourceValidationResult result = await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, Vmr(type)).ConfigureAwait(false);
                ErrorCodes(result).Should().NotContain("VmrApiTypeNotClientFacing", type.ToString());
            }
        }

        public async Task EndpointApiTypeMismatch_UsesCompatibilityTable()
        {
            ModelRunnerEndpoint compatible = await CreateEndpointAsync(ApiTypeEnum.OpenAICompatible, null).ConfigureAwait(false);
            ModelRunnerEndpoint vllm = await CreateEndpointAsync(ApiTypeEnum.vLLM, null).ConfigureAwait(false);
            ModelRunnerEndpoint cloud = await CreateEndpointAsync(ApiTypeEnum.OllamaCloud, "cloud-key").ConfigureAwait(false);
            ModelRunnerEndpoint ollaya = await CreateEndpointAsync(ApiTypeEnum.Ollaya, null).ConfigureAwait(false);
            ModelRunnerEndpoint anthropic = await CreateEndpointAsync(ApiTypeEnum.Anthropic, "sk-ant").ConfigureAwait(false);

            ErrorCodes(await ValidateAsync(ApiTypeEnum.OpenAI, compatible, vllm)).Should().NotContain("EndpointApiTypeMismatch");
            ErrorCodes(await ValidateAsync(ApiTypeEnum.Ollama, cloud)).Should().NotContain("EndpointApiTypeMismatch");
            ErrorCodes(await ValidateAsync(ApiTypeEnum.TypeSafe, ollaya)).Should().NotContain("EndpointApiTypeMismatch");
            ErrorCodes(await ValidateAsync(ApiTypeEnum.Anthropic, anthropic)).Should().NotContain("EndpointApiTypeMismatch");

            ErrorCodes(await ValidateAsync(ApiTypeEnum.vLLM, compatible)).Should().Contain("EndpointApiTypeMismatch");
            ErrorCodes(await ValidateAsync(ApiTypeEnum.Anthropic, compatible)).Should().Contain("EndpointApiTypeMismatch");
            ErrorCodes(await ValidateAsync(ApiTypeEnum.OpenAI, anthropic)).Should().Contain("EndpointApiTypeMismatch");
            ErrorCodes(await ValidateAsync(ApiTypeEnum.Gemini, ollaya)).Should().Contain("EndpointApiTypeMismatch");
        }

        public async Task ApiKeyMissing_ErrorsForAnthropicTypeSafeAndOllamaCloud()
        {
            ErrorCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, Endpoint(ApiTypeEnum.Anthropic, null)).ConfigureAwait(false)).Should().Contain("AnthropicApiKeyMissing");
            ErrorCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, Endpoint(ApiTypeEnum.TypeSafe, null)).ConfigureAwait(false)).Should().Contain("TypeSafeApiKeyMissing");
            ErrorCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, Endpoint(ApiTypeEnum.OllamaCloud, null)).ConfigureAwait(false)).Should().Contain("OllamaCloudApiKeyMissing");

            ErrorCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, Endpoint(ApiTypeEnum.Anthropic, "k")).ConfigureAwait(false)).Should().NotContain("AnthropicApiKeyMissing");
            ErrorCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, Endpoint(ApiTypeEnum.Ollaya, null)).ConfigureAwait(false)).Should().BeEmpty();
            ErrorCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, Endpoint(ApiTypeEnum.OpenAICompatible, null)).ConfigureAwait(false)).Should().BeEmpty();
        }

        public async Task AnthropicHealthCheckUnauthenticated_WarnsWhenEnabledWithoutAuth()
        {
            ModelRunnerEndpoint endpoint = Endpoint(ApiTypeEnum.Anthropic, "k");
            endpoint.HealthCheckEnabled = true;
            endpoint.HealthCheckUseAuth = false;
            WarningCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, endpoint).ConfigureAwait(false)).Should().Contain("AnthropicHealthCheckUnauthenticated");

            endpoint.HealthCheckUseAuth = true;
            WarningCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, endpoint).ConfigureAwait(false)).Should().NotContain("AnthropicHealthCheckUnauthenticated");

            endpoint.HealthCheckUseAuth = false;
            endpoint.HealthCheckEnabled = false;
            WarningCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, endpoint).ConfigureAwait(false)).Should().NotContain("AnthropicHealthCheckUnauthenticated");
        }

        public async Task HealthCheckBodyRequired_ErrorsForPostWithoutBody()
        {
            ModelRunnerEndpoint endpoint = Endpoint(ApiTypeEnum.Ollaya, null);
            endpoint.HealthCheckMethod = HealthCheckMethodEnum.POST;
            ErrorCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, endpoint).ConfigureAwait(false)).Should().Contain("HealthCheckBodyRequired");

            endpoint.HealthCheckRequestBody = "{\"ping\":true}";
            ErrorCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, endpoint).ConfigureAwait(false)).Should().NotContain("HealthCheckBodyRequired");
        }

        public async Task AnthropicVersionInvalid_ErrorsForWrongFormat()
        {
            ModelRunnerEndpoint endpoint = Endpoint(ApiTypeEnum.Anthropic, "k");
            endpoint.AnthropicVersion = "v2";
            ErrorCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, endpoint).ConfigureAwait(false)).Should().Contain("AnthropicVersionInvalid");
        }

        public async Task OllamaCloudRigMonitor_IsRejected()
        {
            ModelRunnerEndpoint endpoint = Endpoint(ApiTypeEnum.OllamaCloud, "k");
            endpoint.RigMonitor = new RigMonitorConfiguration { Enabled = true };
            ErrorCodes(await _Service.ValidateModelRunnerEndpointAsync(TestTenantId, endpoint).ConfigureAwait(false)).Should().Contain("OllamaCloudRigMonitorUnsupported");
        }

        public async Task AnthropicConfigurationMismatch_WarnsForRepeatPenaltyAndContextWindow()
        {
            ModelRunnerEndpoint anthropic = await CreateEndpointAsync(ApiTypeEnum.Anthropic, "k").ConfigureAwait(false);
            ModelConfiguration configuration = await CreateConfigurationAsync(item => { item.RepeatPenalty = 1.1m; }).ConfigureAwait(false);

            ResourceValidationResult result = await ValidateAsync(ApiTypeEnum.Anthropic, new[] { anthropic }, configuration).ConfigureAwait(false);

            WarningCodes(result).Should().Contain("AnthropicConfigurationMismatch");
            WarningCodes(result).Should().NotContain("ModelConfigurationSamplingIgnored");
        }

        public async Task ModelConfigurationSamplingIgnored_WarnsWhenEndpointVersionRemovesSampling()
        {
            ModelRunnerEndpoint newVersion = await CreateEndpointAsync(ApiTypeEnum.Anthropic, "k", "2026-10-21").ConfigureAwait(false);
            ModelRunnerEndpoint oldVersion = await CreateEndpointAsync(ApiTypeEnum.Anthropic, "k", "2025-01-01").ConfigureAwait(false);
            ModelConfiguration configuration = await CreateConfigurationAsync(item => { item.Temperature = 0.4m; }).ConfigureAwait(false);

            WarningCodes(await ValidateAsync(ApiTypeEnum.Anthropic, new[] { newVersion }, configuration).ConfigureAwait(false)).Should().Contain("ModelConfigurationSamplingIgnored");
            WarningCodes(await ValidateAsync(ApiTypeEnum.Anthropic, new[] { oldVersion }, configuration).ConfigureAwait(false)).Should().NotContain("ModelConfigurationSamplingIgnored");
        }

        public async Task TypeSafeConfigurationIgnored_WarnsWhenConfigurationsAttached()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync(ApiTypeEnum.TypeSafe, "k").ConfigureAwait(false);
            ModelConfiguration configuration = await CreateConfigurationAsync(item => { item.MaxTokens = 10; }).ConfigureAwait(false);

            WarningCodes(await ValidateAsync(ApiTypeEnum.TypeSafe, new[] { endpoint }, configuration).ConfigureAwait(false)).Should().Contain("TypeSafeConfigurationIgnored");
        }

        public async Task DecisionModelNotEnabled_WarnsForDefinitionsWithoutSupportsDecisions()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync(ApiTypeEnum.TypeSafe, "k").ConfigureAwait(false);
            ModelDefinition plain = await Database.ModelDefinition.CreateAsync(new ModelDefinition { TenantId = TestTenantId, Name = "jev-plain" }).ConfigureAwait(false);
            ModelDefinition decision = await Database.ModelDefinition.CreateAsync(new ModelDefinition { TenantId = TestTenantId, Name = "jev-latest", SupportsDecisions = true }).ConfigureAwait(false);

            VirtualModelRunner withPlain = Vmr(ApiTypeEnum.TypeSafe, endpoint);
            withPlain.ModelDefinitionIds = new List<string> { plain.Id };
            VirtualModelRunner withDecision = Vmr(ApiTypeEnum.TypeSafe, endpoint);
            withDecision.ModelDefinitionIds = new List<string> { decision.Id };

            WarningCodes(await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, withPlain).ConfigureAwait(false)).Should().Contain("DecisionModelNotEnabled");
            WarningCodes(await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, withDecision).ConfigureAwait(false)).Should().NotContain("DecisionModelNotEnabled");
        }

        public async Task DecisionsDisabledOnDecisionRoute_ErrorsForTypeSafeWithoutDecisions()
        {
            VirtualModelRunner vmr = Vmr(ApiTypeEnum.TypeSafe);
            vmr.AllowDecisions = false;
            ErrorCodes(await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, vmr).ConfigureAwait(false)).Should().Contain("DecisionsDisabledOnDecisionRoute");

            VirtualModelRunner openai = Vmr(ApiTypeEnum.OpenAI);
            openai.AllowDecisions = false;
            ErrorCodes(await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, openai).ConfigureAwait(false)).Should().NotContain("DecisionsDisabledOnDecisionRoute");
        }

        public async Task VirtualModelRunnerController_EnforcesTypeValidationOnCreate()
        {
            VirtualModelRunnerController controller = new VirtualModelRunnerController(Database, AuthService, Serializer, Logging, null, null, _Service);
            ModelRunnerEndpoint anthropic = await CreateEndpointAsync(ApiTypeEnum.Anthropic, "k").ConfigureAwait(false);

            Func<Task> notClientFacing = async () => await controller.Create(TestTenantId, Vmr(ApiTypeEnum.OpenAICompatible)).ConfigureAwait(false);
            Func<Task> mismatch = async () => await controller.Create(TestTenantId, Vmr(ApiTypeEnum.OpenAI, anthropic)).ConfigureAwait(false);

            await notClientFacing.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);
            await mismatch.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);
            VirtualModelRunner created = await controller.Create(TestTenantId, Vmr(ApiTypeEnum.Anthropic, anthropic)).ConfigureAwait(false);
            created.ApiType.Should().Be(ApiTypeEnum.Anthropic);
        }

        private async Task<ResourceValidationResult> ValidateAsync(ApiTypeEnum vmrType, params ModelRunnerEndpoint[] endpoints)
        {
            return await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, Vmr(vmrType, endpoints)).ConfigureAwait(false);
        }

        private async Task<ResourceValidationResult> ValidateAsync(ApiTypeEnum vmrType, ModelRunnerEndpoint[] endpoints, ModelConfiguration configuration)
        {
            VirtualModelRunner vmr = Vmr(vmrType, endpoints);
            vmr.ModelConfigurationIds = new List<string> { configuration.Id };
            return await _Service.ValidateVirtualModelRunnerAsync(TestTenantId, vmr).ConfigureAwait(false);
        }

        private VirtualModelRunner Vmr(ApiTypeEnum type, params ModelRunnerEndpoint[] endpoints)
        {
            string slug = "v" + Guid.NewGuid().ToString("N").Substring(0, 10);
            return new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "vmr " + slug,
                BasePath = "/v1.0/api/" + slug + "/",
                ApiType = type,
                ModelRunnerEndpointIds = (endpoints ?? new ModelRunnerEndpoint[0]).Select(item => item.Id).ToList()
            };
        }

        private ModelRunnerEndpoint Endpoint(ApiTypeEnum type, string apiKey)
        {
            return new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = type + " endpoint",
                Hostname = "upstream.local",
                Port = 8080,
                ApiType = type,
                ApiKey = apiKey
            };
        }

        private async Task<ModelRunnerEndpoint> CreateEndpointAsync(ApiTypeEnum type, string apiKey, string anthropicVersion = null)
        {
            ModelRunnerEndpoint endpoint = Endpoint(type, apiKey);
            endpoint.AnthropicVersion = anthropicVersion;
            return await Database.ModelRunnerEndpoint.CreateAsync(endpoint).ConfigureAwait(false);
        }

        private async Task<ModelConfiguration> CreateConfigurationAsync(Action<ModelConfiguration> configure)
        {
            ModelConfiguration configuration = new ModelConfiguration { TenantId = TestTenantId, Name = "cfg " + Guid.NewGuid().ToString("N").Substring(0, 6) };
            configure(configuration);
            return await Database.ModelConfiguration.CreateAsync(configuration).ConfigureAwait(false);
        }

        private static List<string> ErrorCodes(ResourceValidationResult result)
        {
            return result.Errors.Select(item => item.Code).ToList();
        }

        private static List<string> WarningCodes(ResourceValidationResult result)
        {
            return result.Warnings.Select(item => item.Code).ToList();
        }
    }
}
