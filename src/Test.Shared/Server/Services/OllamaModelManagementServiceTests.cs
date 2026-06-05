namespace Test.Shared.Server.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Requests;
    using Conductor.Core.Responses;
    using Conductor.Server.Services;
    using FluentAssertions;
    using WatsonWebserver.Core;

    /// <summary>
    /// Unit tests for Ollama endpoint model management.
    /// </summary>
    public class OllamaModelManagementServiceTests
    {
        public async Task ListModelsAsync_WithOllamaTagsResponse_ReturnsParsedModels()
        {
            FakeModelLoadTransport transport = new FakeModelLoadTransport();
            transport.Responses.Enqueue(new ModelLoadTransportResponse
            {
                StatusCode = 200,
                Body = "{\"models\":[{\"name\":\"gemma3:4b\",\"model\":\"gemma3:4b\",\"size\":333,\"digest\":\"sha256:abc\",\"details\":{\"family\":\"gemma\",\"parameter_size\":\"4B\",\"quantization_level\":\"Q4_0\"}}]}"
            });
            OllamaModelManagementService service = new OllamaModelManagementService(transport);

            OllamaModelListResponse response = await service.ListModelsAsync(CreateEndpoint(ApiTypeEnum.Ollama)).ConfigureAwait(false);

            response.Success.Should().BeTrue();
            response.Models.Should().ContainSingle();
            response.Models[0].Name.Should().Be("gemma3:4b");
            response.Models[0].Details.Family.Should().Be("gemma");
            transport.Plans.Should().ContainSingle();
            transport.Plans[0].Method.Should().Be("GET");
            transport.Plans[0].Path.Should().Be("/api/tags");
        }

        public async Task PullModelAsync_WithModel_SendsNonStreamingPullRequest()
        {
            FakeModelLoadTransport transport = new FakeModelLoadTransport();
            transport.Responses.Enqueue(new ModelLoadTransportResponse
            {
                StatusCode = 200,
                Body = "{\"status\":\"success\"}"
            });
            OllamaModelManagementService service = new OllamaModelManagementService(transport);

            OllamaModelOperationResponse response = await service.PullModelAsync(
                CreateEndpoint(ApiTypeEnum.Ollama),
                new OllamaModelPullRequest { Model = "llama3.2:latest", Insecure = true }).ConfigureAwait(false);

            response.Success.Should().BeTrue();
            response.Operation.Should().Be("Pull");
            transport.Plans.Should().ContainSingle();
            transport.Plans[0].Method.Should().Be("POST");
            transport.Plans[0].Path.Should().Be("/api/pull");
            transport.Plans[0].BodyJson.Should().Contain("\"model\":\"llama3.2:latest\"");
            transport.Plans[0].BodyJson.Should().Contain("\"stream\":false");
            transport.Plans[0].BodyJson.Should().Contain("\"insecure\":true");
        }

        public async Task DeleteModelAsync_WithModel_SendsDeleteRequestWithModelField()
        {
            FakeModelLoadTransport transport = new FakeModelLoadTransport();
            transport.Responses.Enqueue(new ModelLoadTransportResponse
            {
                StatusCode = 200,
                Body = "{}"
            });
            OllamaModelManagementService service = new OllamaModelManagementService(transport);

            OllamaModelOperationResponse response = await service.DeleteModelAsync(
                CreateEndpoint(ApiTypeEnum.Ollama),
                new OllamaModelDeleteRequest { Model = "llama3.2:latest" }).ConfigureAwait(false);

            response.Success.Should().BeTrue();
            response.Operation.Should().Be("Delete");
            transport.Plans.Should().ContainSingle();
            transport.Plans[0].Method.Should().Be("DELETE");
            transport.Plans[0].Path.Should().Be("/api/delete");
            transport.Plans[0].BodyJson.Should().Contain("\"model\":\"llama3.2:latest\"");
        }

        public async Task ListModelsAsync_WithNonOllamaEndpoint_RejectsRequest()
        {
            OllamaModelManagementService service = new OllamaModelManagementService(new FakeModelLoadTransport());

            System.Func<Task> action = async () =>
                await service.ListModelsAsync(CreateEndpoint(ApiTypeEnum.OpenAI)).ConfigureAwait(false);

            await action.Should().ThrowAsync<WebserverException>().ConfigureAwait(false);
        }

        private static ModelRunnerEndpoint CreateEndpoint(ApiTypeEnum apiType)
        {
            return new ModelRunnerEndpoint
            {
                TenantId = "default",
                Name = "Test Endpoint",
                Hostname = "localhost",
                Port = 11434,
                ApiType = apiType,
                Active = true
            };
        }

        private sealed class FakeModelLoadTransport : IModelLoadTransport
        {
            public List<ModelLoadProbePlan> Plans { get; } = new List<ModelLoadProbePlan>();

            public Queue<ModelLoadTransportResponse> Responses { get; } = new Queue<ModelLoadTransportResponse>();

            public Task<ModelLoadTransportResponse> SendAsync(
                ModelRunnerEndpoint endpoint,
                ModelLoadProbePlan plan,
                int timeoutMs,
                CancellationToken token = default)
            {
                Plans.Add(plan);
                if (Responses.Count > 0)
                {
                    return Task.FromResult(Responses.Dequeue());
                }

                return Task.FromResult(new ModelLoadTransportResponse
                {
                    StatusCode = 200,
                    Body = "{}"
                });
            }
        }
    }
}
