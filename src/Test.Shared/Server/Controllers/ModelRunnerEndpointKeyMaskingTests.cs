namespace Test.Shared.Server.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Responses;
    using Conductor.Core.Serialization;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using FluentAssertions;
    using WatsonWebserver.Core;

    /// <summary>
    /// Tests for upstream key masking, the reveal route semantics and type defaults on create.
    /// </summary>
    public class ModelRunnerEndpointKeyMaskingTests : ControllerTestBase
    {
        private ModelRunnerEndpointController _Controller;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            ConfigurationValidationService validation = new ConfigurationValidationService(Database, Logging, new RoutingDecisionService(Database, Logging));
            _Controller = new ModelRunnerEndpointController(Database, AuthService, Serializer, Logging, null, validation);
        }

        public async Task CreateReadEnumerate_MaskKeyButStoreFullValue()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, NewEndpoint("sk-ant-api03-secret-ABCD")).ConfigureAwait(false);

            created.ApiKey.Should().Be("****ABCD");
            (await _Controller.Read(TestTenantId, created.Id).ConfigureAwait(false)).ApiKey.Should().Be("****ABCD");
            EnumerationResult<ModelRunnerEndpoint> list = await _Controller.Enumerate(TestTenantId).ConfigureAwait(false);
            list.Data.Should().OnlyContain(item => item.ApiKey == null || item.ApiKey.StartsWith("****", StringComparison.Ordinal));
            (await Database.ModelRunnerEndpoint.ReadAsync(TestTenantId, created.Id).ConfigureAwait(false)).ApiKey.Should().Be("sk-ant-api03-secret-ABCD");
        }

        public async Task Read_WithoutKey_ReturnsNullApiKey()
        {
            ModelRunnerEndpoint endpoint = NewEndpoint(null);
            endpoint.ApiType = ApiTypeEnum.Ollama;
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, endpoint).ConfigureAwait(false);
            created.ApiKey.Should().BeNull();
            (await _Controller.Read(TestTenantId, created.Id).ConfigureAwait(false)).ApiKey.Should().BeNull();
        }

        public async Task Update_KeepsStoredKeyForNullAndMaskedValue_ClearsForEmpty_ReplacesOtherwise()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, NewEndpoint("sk-ant-original-1111")).ConfigureAwait(false);

            ModelRunnerEndpoint omitted = Clone(created);
            omitted.ApiKey = null;
            (await _Controller.Update(TestTenantId, created.Id, omitted).ConfigureAwait(false)).ApiKey.Should().Be("****1111");
            (await StoredKeyAsync(created.Id).ConfigureAwait(false)).Should().Be("sk-ant-original-1111");

            ModelRunnerEndpoint masked = Clone(created);
            masked.ApiKey = "****1111";
            await _Controller.Update(TestTenantId, created.Id, masked).ConfigureAwait(false);
            (await StoredKeyAsync(created.Id).ConfigureAwait(false)).Should().Be("sk-ant-original-1111");

            ModelRunnerEndpoint replaced = Clone(created);
            replaced.ApiKey = "sk-ant-rotated-2222";
            (await _Controller.Update(TestTenantId, created.Id, replaced).ConfigureAwait(false)).ApiKey.Should().Be("****2222");
            (await StoredKeyAsync(created.Id).ConfigureAwait(false)).Should().Be("sk-ant-rotated-2222");

            ModelRunnerEndpoint ollama = await _Controller.Create(TestTenantId, new ModelRunnerEndpoint { Name = "ollama keyed", Hostname = "ollama.local", ApiType = ApiTypeEnum.Ollama, ApiKey = "ollama-key-3333" }).ConfigureAwait(false);
            ModelRunnerEndpoint cleared = Clone(ollama);
            cleared.ApiKey = String.Empty;
            (await _Controller.Update(TestTenantId, ollama.Id, cleared).ConfigureAwait(false)).ApiKey.Should().BeNull();
            (await StoredKeyAsync(ollama.Id).ConfigureAwait(false)).Should().BeNull();
        }

        public async Task Update_ClearingRequiredAnthropicKey_FailsValidation()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, NewEndpoint("sk-ant-required-4444")).ConfigureAwait(false);
            ModelRunnerEndpoint cleared = Clone(created);
            cleared.ApiKey = String.Empty;

            Func<Task> act = async () => await _Controller.Update(TestTenantId, created.Id, cleared).ConfigureAwait(false);

            await act.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);
        }

        public async Task RevealApiKey_ReturnsFullValueAndIsTenantScoped()
        {
            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, NewEndpoint("sk-ant-reveal-5555")).ConfigureAwait(false);

            ModelRunnerEndpointApiKeyResponse revealed = await _Controller.RevealApiKey(TestTenantId, created.Id, "user:test").ConfigureAwait(false);
            revealed.ApiKey.Should().Be("sk-ant-reveal-5555");

            Func<Task> otherTenant = async () => await _Controller.RevealApiKey("ten_other", created.Id).ConfigureAwait(false);
            await otherTenant.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);
        }

        public async Task Create_AppliesTypeDefaultsOnlyToOmittedFields()
        {
            ModelRunnerEndpoint request = new Serializer().DeserializeJson<ModelRunnerEndpoint>(
                "{\"Name\":\"typesafe\",\"ApiType\":\"TypeSafe\",\"ApiKey\":\"ts-key-6666\",\"HealthCheckIntervalMs\":45000}");

            ModelRunnerEndpoint created = await _Controller.Create(TestTenantId, request).ConfigureAwait(false);
            ModelRunnerEndpoint stored = await Database.ModelRunnerEndpoint.ReadAsync(TestTenantId, created.Id).ConfigureAwait(false);

            stored.Hostname.Should().Be("api.typesafe.ai");
            stored.Port.Should().Be(443);
            stored.UseSsl.Should().BeTrue();
            stored.HealthCheckMethod.Should().Be(HealthCheckMethodEnum.POST);
            stored.HealthCheckRequestBody.Should().Contain("\"reachable\"");
            stored.HealthCheckEnabled.Should().BeFalse();
            stored.HealthCheckUseAuth.Should().BeTrue();
            stored.HealthCheckIntervalMs.Should().Be(45000);
        }

        public async Task NewEndpointFields_RoundTripThroughDatabase()
        {
            ModelRunnerEndpoint endpoint = NewEndpoint("sk-ant-roundtrip-7777");
            endpoint.TenantId = TestTenantId;
            endpoint.AnthropicVersion = "2024-10-22";
            endpoint.HealthCheckEnabled = false;
            endpoint.HealthCheckMethod = HealthCheckMethodEnum.POST;
            endpoint.HealthCheckRequestBody = "{\"probe\":1}";
            ModelRunnerEndpoint created = await Database.ModelRunnerEndpoint.CreateAsync(endpoint).ConfigureAwait(false);

            ModelRunnerEndpoint stored = await Database.ModelRunnerEndpoint.ReadAsync(TestTenantId, created.Id).ConfigureAwait(false);
            stored.ApiType.Should().Be(ApiTypeEnum.Anthropic);
            stored.AnthropicVersion.Should().Be("2024-10-22");
            stored.HealthCheckEnabled.Should().BeFalse();
            stored.HealthCheckMethod.Should().Be(HealthCheckMethodEnum.POST);
            stored.HealthCheckRequestBody.Should().Be("{\"probe\":1}");

            ModelDefinition definition = await Database.ModelDefinition.CreateAsync(new ModelDefinition { TenantId = TestTenantId, Name = "jev", SupportsDecisions = true }).ConfigureAwait(false);
            (await Database.ModelDefinition.ReadAsync(TestTenantId, definition.Id).ConfigureAwait(false)).SupportsDecisions.Should().BeTrue();

            VirtualModelRunner vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner { TenantId = TestTenantId, Name = "ts", BasePath = "/v1.0/api/ts-rt/", ApiType = ApiTypeEnum.TypeSafe, AllowDecisions = false }).ConfigureAwait(false);
            VirtualModelRunner storedVmr = await Database.VirtualModelRunner.ReadAsync(TestTenantId, vmr.Id).ConfigureAwait(false);
            storedVmr.AllowDecisions.Should().BeFalse();
            storedVmr.ApiType.Should().Be(ApiTypeEnum.TypeSafe);
        }

        private ModelRunnerEndpoint NewEndpoint(string apiKey)
        {
            return new ModelRunnerEndpoint
            {
                Name = "anthropic " + Guid.NewGuid().ToString("N").Substring(0, 6),
                ApiType = ApiTypeEnum.Anthropic,
                ApiKey = apiKey
            };
        }

        private static ModelRunnerEndpoint Clone(ModelRunnerEndpoint endpoint)
        {
            Serializer serializer = new Serializer();
            return serializer.DeserializeJson<ModelRunnerEndpoint>(serializer.SerializeJson(endpoint, false));
        }

        private async Task<string> StoredKeyAsync(string id)
        {
            return (await Database.ModelRunnerEndpoint.ReadAsync(TestTenantId, id).ConfigureAwait(false)).ApiKey;
        }
    }
}
