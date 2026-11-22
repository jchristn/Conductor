namespace Conductor.Sdk.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Xunit;

    /// <summary>
    /// Tests for v1.1.0 provider support: the <see cref="ApiType"/> enum, the compatibility table,
    /// endpoint, model definition, and virtual model runner CRUD, key masking, and the API key reveal route.
    /// </summary>
    public class ConductorClientProviderTests
    {
        private const string _BaseUrl = "http://127.0.0.1:9000";

        [Theory]
        [InlineData(ApiType.Ollama, 0, "Ollama")]
        [InlineData(ApiType.OpenAI, 1, "OpenAI")]
        [InlineData(ApiType.Gemini, 2, "Gemini")]
        [InlineData(ApiType.vLLM, 3, "vLLM")]
        [InlineData(ApiType.Anthropic, 4, "Anthropic")]
        [InlineData(ApiType.TypeSafe, 5, "TypeSafe")]
        [InlineData(ApiType.Ollaya, 6, "Ollaya")]
        [InlineData(ApiType.OllamaCloud, 7, "OllamaCloud")]
        [InlineData(ApiType.OpenAICompatible, 8, "OpenAICompatible")]
        public void ApiType_HasContractNameAndValue(ApiType apiType, int expectedValue, string expectedName)
        {
            Assert.Equal(expectedValue, (int)apiType);
            Assert.Equal(expectedName, apiType.ToString());
            Assert.Equal("\"" + expectedName + "\"", JsonSerializer.Serialize(apiType));
            Assert.Equal(apiType, JsonSerializer.Deserialize<ApiType>("\"" + expectedName + "\""));
        }

        [Fact]
        public void ApiType_HasExactlyNineValues()
        {
            Assert.Equal(9, Enum.GetValues<ApiType>().Length);
        }

        [Fact]
        public void ApiTypeCompatibility_ClientFacingTypes_MatchContract()
        {
            ApiType[] expected = { ApiType.Ollama, ApiType.OpenAI, ApiType.Gemini, ApiType.vLLM, ApiType.Anthropic, ApiType.TypeSafe };
            Assert.Equal(expected, ApiTypeCompatibility.ClientFacingTypes.ToArray());
            Assert.False(ApiTypeCompatibility.IsClientFacing(ApiType.Ollaya));
            Assert.False(ApiTypeCompatibility.IsClientFacing(ApiType.OllamaCloud));
            Assert.False(ApiTypeCompatibility.IsClientFacing(ApiType.OpenAICompatible));
        }

        [Fact]
        public void ApiTypeCompatibility_AcceptedEndpoints_MatchContract()
        {
            Assert.Equal(new[] { ApiType.Ollama, ApiType.OllamaCloud }, ApiTypeCompatibility.GetAcceptedEndpointTypes(ApiType.Ollama));
            Assert.Equal(new[] { ApiType.OpenAI, ApiType.OpenAICompatible, ApiType.vLLM }, ApiTypeCompatibility.GetAcceptedEndpointTypes(ApiType.OpenAI));
            Assert.Equal(new[] { ApiType.vLLM }, ApiTypeCompatibility.GetAcceptedEndpointTypes(ApiType.vLLM));
            Assert.Equal(new[] { ApiType.Gemini }, ApiTypeCompatibility.GetAcceptedEndpointTypes(ApiType.Gemini));
            Assert.Equal(new[] { ApiType.Anthropic }, ApiTypeCompatibility.GetAcceptedEndpointTypes(ApiType.Anthropic));
            Assert.Equal(new[] { ApiType.TypeSafe, ApiType.Ollaya }, ApiTypeCompatibility.GetAcceptedEndpointTypes(ApiType.TypeSafe));
            Assert.Empty(ApiTypeCompatibility.GetAcceptedEndpointTypes(ApiType.Ollaya));
            Assert.True(ApiTypeCompatibility.AcceptsEndpoint(ApiType.TypeSafe, ApiType.Ollaya));
            Assert.False(ApiTypeCompatibility.AcceptsEndpoint(ApiType.Anthropic, ApiType.OpenAI));
            Assert.False(ApiTypeCompatibility.AcceptsEndpoint(ApiType.vLLM, ApiType.OpenAI));
        }

        [Fact]
        public async Task RevealModelRunnerEndpointApiKey_UsesApiKeyRouteAndReturnsValue()
        {
            CapturingHandler handler = new CapturingHandler(_ => JsonResponse("{\"ApiKey\":\"sk-ant-full-secret-1234\"}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient(_BaseUrl, bearerToken: "admin-token", httpClient: httpClient);

            string apiKey = await client.RevealModelRunnerEndpointApiKeyAsync("mre_1");
            string scoped = await client.RevealModelRunnerEndpointApiKeyAsync("mre 2", "ten_1");

            Assert.Equal("sk-ant-full-secret-1234", apiKey);
            Assert.Equal("sk-ant-full-secret-1234", scoped);
            Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
            Assert.Equal("http://127.0.0.1:9000/v1.0/modelrunnerendpoints/mre_1/apikey", handler.Requests[0].Uri.ToString());
            Assert.Equal("http://127.0.0.1:9000/v1.0/modelrunnerendpoints/mre%202/apikey?tenantId=ten_1", handler.Requests[1].Uri.AbsoluteUri);
            Assert.Equal("Bearer admin-token", handler.Requests[0].Headers["Authorization"]);
        }

        [Fact]
        public async Task RevealModelRunnerEndpointApiKey_ReturnsNullWhenUnset()
        {
            CapturingHandler handler = new CapturingHandler(_ => JsonResponse("{\"ApiKey\":null}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient(_BaseUrl, httpClient: httpClient);

            Assert.Null(await client.RevealModelRunnerEndpointApiKeyAsync("mre_1"));
        }

        [Fact]
        public async Task RevealModelRunnerEndpointApiKey_RejectsEmptyId()
        {
            using ConductorClient client = new ConductorClient(_BaseUrl);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.RevealModelRunnerEndpointApiKeyAsync(""));
        }

        [Fact]
        public async Task RevealModelRunnerEndpointApiKey_SurfacesForbidden()
        {
            CapturingHandler handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"Error\":\"AccessDenied\"}", Encoding.UTF8, "application/json")
            });
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient(_BaseUrl, httpClient: httpClient);

            ConductorApiException exception = await Assert.ThrowsAsync<ConductorApiException>(() => client.RevealModelRunnerEndpointApiKeyAsync("mre_1"));
            Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        }

        [Fact]
        public async Task CreateModelRunnerEndpoint_AnthropicFieldsPassThrough()
        {
            CapturingHandler handler = new CapturingHandler(_ => JsonResponse("{\"Id\":\"mre_1\",\"ApiType\":\"Anthropic\",\"ApiKey\":\"****cdef\"}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient(_BaseUrl, httpClient: httpClient);

            using JsonDocument response = await client.CreateModelRunnerEndpointAsync(new
            {
                Name = "Anthropic",
                Hostname = "api.anthropic.com",
                Port = 443,
                UseSsl = true,
                ApiType = ApiType.Anthropic,
                ApiKey = "sk-ant-abcdef",
                AnthropicVersion = "2023-06-01",
                HealthCheckEnabled = true,
                HealthCheckMethod = "POST",
                HealthCheckRequestBody = "{\"ping\":true}"
            });

            CapturedRequest request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://127.0.0.1:9000/v1.0/modelrunnerendpoints", request.Uri.ToString());

            using JsonDocument body = JsonDocument.Parse(request.Body);
            Assert.Equal("Anthropic", body.RootElement.GetProperty("ApiType").GetString());
            Assert.Equal("2023-06-01", body.RootElement.GetProperty("AnthropicVersion").GetString());
            Assert.True(body.RootElement.GetProperty("HealthCheckEnabled").GetBoolean());
            Assert.Equal("POST", body.RootElement.GetProperty("HealthCheckMethod").GetString());
            Assert.Equal("{\"ping\":true}", body.RootElement.GetProperty("HealthCheckRequestBody").GetString());
            Assert.Equal("****cdef", response.RootElement.GetProperty("ApiKey").GetString());
        }

        [Fact]
        public async Task GetModelRunnerEndpoint_ParsesMaskedApiKeyAndNewFields()
        {
            const string json = "{\"Id\":\"mre_1\",\"ApiType\":\"TypeSafe\",\"ApiKey\":\"****wxyz\",\"AnthropicVersion\":null,"
                + "\"HealthCheckEnabled\":false,\"HealthCheckMethod\":\"POST\",\"HealthCheckRequestBody\":\"{}\"}";
            CapturingHandler handler = new CapturingHandler(_ => JsonResponse(json));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient(_BaseUrl, httpClient: httpClient);

            using JsonDocument response = await client.GetModelRunnerEndpointAsync("mre_1", "ten_1");

            Assert.Equal("http://127.0.0.1:9000/v1.0/modelrunnerendpoints/mre_1?tenantId=ten_1", handler.Requests[0].Uri.ToString());
            JsonElement root = response.RootElement;
            Assert.StartsWith("****", root.GetProperty("ApiKey").GetString());
            Assert.Equal(8, root.GetProperty("ApiKey").GetString().Length);
            Assert.Equal(ApiType.TypeSafe, root.GetProperty("ApiType").Deserialize<ApiType>());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("AnthropicVersion").ValueKind);
            Assert.False(root.GetProperty("HealthCheckEnabled").GetBoolean());
        }

        [Fact]
        public async Task ModelRunnerEndpointCrud_UsesExpectedRoutes()
        {
            CapturingHandler handler = new CapturingHandler(request => request.Method == HttpMethod.Delete
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient(_BaseUrl, httpClient: httpClient);

            using JsonDocument list = await client.ListModelRunnerEndpointsAsync(new Dictionary<string, string> { ["tenantId"] = "ten_1", ["maxResults"] = "10" });
            using JsonDocument update = await client.UpdateModelRunnerEndpointAsync("mre_1", new { ApiKey = "****cdef", HealthCheckEnabled = false });
            await client.DeleteModelRunnerEndpointAsync("mre_1", "ten_1");

            Assert.Equal("http://127.0.0.1:9000/v1.0/modelrunnerendpoints?tenantId=ten_1&maxResults=10", handler.Requests[0].Uri.ToString());
            Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
            Assert.Equal("http://127.0.0.1:9000/v1.0/modelrunnerendpoints/mre_1", handler.Requests[1].Uri.ToString());
            Assert.Contains("\"HealthCheckEnabled\":false", handler.Requests[1].Body);
            Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
            Assert.Equal("http://127.0.0.1:9000/v1.0/modelrunnerendpoints/mre_1?tenantId=ten_1", handler.Requests[2].Uri.ToString());
        }

        [Fact]
        public async Task ModelDefinitionAndVirtualModelRunnerCrud_UseExpectedRoutesAndPassNewFields()
        {
            CapturingHandler handler = new CapturingHandler(request => request.Method == HttpMethod.Delete
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient(_BaseUrl, httpClient: httpClient);

            using JsonDocument createDefinition = await client.CreateModelDefinitionAsync(new { Name = "jev-latest", SupportsCompletions = false, SupportsDecisions = true });
            using JsonDocument listDefinitions = await client.ListModelDefinitionsAsync();
            using JsonDocument getDefinition = await client.GetModelDefinitionAsync("md_1");
            using JsonDocument updateDefinition = await client.UpdateModelDefinitionAsync("md_1", new { SupportsDecisions = false });
            await client.DeleteModelDefinitionAsync("md_1");
            using JsonDocument createVmr = await client.CreateVirtualModelRunnerAsync(new
            {
                Name = "decisions",
                BasePath = "/v1.0/api/decisions/",
                ApiType = ApiType.TypeSafe,
                AllowDecisions = true,
                ModelRunnerEndpointIds = new[] { "mre_1" }
            });
            using JsonDocument listVmrs = await client.ListVirtualModelRunnersAsync(new Dictionary<string, string> { ["nameFilter"] = "dec" });
            using JsonDocument getVmr = await client.GetVirtualModelRunnerAsync("vmr_1", "ten_1");
            using JsonDocument updateVmr = await client.UpdateVirtualModelRunnerAsync("vmr_1", new { AllowDecisions = false });
            await client.DeleteVirtualModelRunnerAsync("vmr_1", "ten_1");

            Assert.Equal("http://127.0.0.1:9000/v1.0/modeldefinitions", handler.Requests[0].Uri.ToString());
            Assert.Contains("\"SupportsDecisions\":true", handler.Requests[0].Body);
            Assert.Equal("http://127.0.0.1:9000/v1.0/modeldefinitions", handler.Requests[1].Uri.ToString());
            Assert.Equal("http://127.0.0.1:9000/v1.0/modeldefinitions/md_1", handler.Requests[2].Uri.ToString());
            Assert.Equal(HttpMethod.Put, handler.Requests[3].Method);
            Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
            Assert.Equal("http://127.0.0.1:9000/v1.0/virtualmodelrunners", handler.Requests[5].Uri.ToString());
            Assert.Contains("\"ApiType\":\"TypeSafe\"", handler.Requests[5].Body);
            Assert.Contains("\"AllowDecisions\":true", handler.Requests[5].Body);
            Assert.Equal("http://127.0.0.1:9000/v1.0/virtualmodelrunners?nameFilter=dec", handler.Requests[6].Uri.ToString());
            Assert.Equal("http://127.0.0.1:9000/v1.0/virtualmodelrunners/vmr_1?tenantId=ten_1", handler.Requests[7].Uri.ToString());
            Assert.Equal("http://127.0.0.1:9000/v1.0/virtualmodelrunners/vmr_1", handler.Requests[8].Uri.ToString());
            Assert.Contains("\"AllowDecisions\":false", handler.Requests[8].Body);
            Assert.Equal("http://127.0.0.1:9000/v1.0/virtualmodelrunners/vmr_1?tenantId=ten_1", handler.Requests[9].Uri.ToString());
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
