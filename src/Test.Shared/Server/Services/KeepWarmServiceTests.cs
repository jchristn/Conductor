namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Responses;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for keep-warm loading, slot accounting, scheduling and persistence.
    /// </summary>
    public class KeepWarmServiceTests : Test.Shared.Server.Controllers.ControllerTestBase
    {
        private PathTransport _Transport;

        /// <summary>
        /// Initialize the test.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Transport = new PathTransport();
        }

        public async Task KeepWarm_WhenModelResident_ReportsAlreadyAvailableWithoutLoading()
        {
            _Transport.ResidentModels.Add("qwen:122b");
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Resident Endpoint", ApiTypeEnum.Ollama, 4).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(endpoint, new KeepWarmSettings { Enabled = true, Models = new List<string> { "qwen:122b" } }).ConfigureAwait(false);

            using (ModelLoadService service = new ModelLoadService(Database, Logging, null, null, null, _Transport))
            {
                ModelLoadResponse response = await service.KeepWarmVirtualModelRunnerAsync(vmr, "qwen:122b", vmr.KeepWarm).ConfigureAwait(false);

                response.EndpointResults.Should().ContainSingle();
                response.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.AlreadyAvailable);
                _Transport.Paths.Should().OnlyContain(path => path == "/api/ps");
            }
        }

        public async Task KeepWarm_WhenModelNotResident_LoadsWhileHoldingEndpointSlot()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Cold Endpoint", ApiTypeEnum.Ollama, 2).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(endpoint, new KeepWarmSettings { Enabled = true, Models = new List<string> { "qwen:122b" }, KeepAlive = "24h" }).ConfigureAwait(false);

            HealthCheckService health = await StartHealthyAsync(endpoint).ConfigureAwait(false);
            try
            {
                _Transport.OnGenerate = () => _Transport.InFlightDuringLoad = health.GetHealthState(endpoint.Id).InFlightRequests;

                using (ModelLoadService service = new ModelLoadService(Database, Logging, null, health, null, _Transport))
                {
                    ModelLoadResponse response = await service.KeepWarmVirtualModelRunnerAsync(vmr, "qwen:122b", vmr.KeepWarm).ConfigureAwait(false);

                    response.EndpointResults.Should().ContainSingle();
                    response.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.Loaded);
                    _Transport.InFlightDuringLoad.Should().Be(1, "a warm-up load must take one MaxParallelRequests slot");
                    health.GetHealthState(endpoint.Id).InFlightRequests.Should().Be(0, "the slot must be released after the load");
                    _Transport.GenerateBodies.Should().ContainSingle().Which.Should().Contain("\"keep_alive\":\"24h\"");
                }
            }
            finally
            {
                await health.StopAsync().ConfigureAwait(false);
                health.Dispose();
            }
        }

        public async Task KeepWarm_WhenEndpointAtMaxParallelRequests_SkipsWithoutLoading()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Busy Endpoint", ApiTypeEnum.Ollama, 1).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(endpoint, new KeepWarmSettings { Enabled = true, Models = new List<string> { "qwen:122b" } }).ConfigureAwait(false);

            HealthCheckService health = await StartHealthyAsync(endpoint).ConfigureAwait(false);
            try
            {
                health.TryIncrementInFlight(endpoint.Id, endpoint.MaxParallelRequests).Should().BeTrue();

                using (ModelLoadService service = new ModelLoadService(Database, Logging, null, health, null, _Transport))
                {
                    ModelLoadResponse response = await service.KeepWarmVirtualModelRunnerAsync(vmr, "qwen:122b", vmr.KeepWarm).ConfigureAwait(false);

                    response.EndpointResults.Should().ContainSingle();
                    response.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.Skipped);
                    _Transport.GenerateBodies.Should().BeEmpty();
                    health.GetHealthState(endpoint.Id).InFlightRequests.Should().Be(1, "keep-warm must not touch a slot it did not take");
                }
            }
            finally
            {
                health.DecrementInFlight(endpoint.Id);
                await health.StopAsync().ConfigureAwait(false);
                health.Dispose();
            }
        }

        public async Task KeepWarm_WithNonOllamaEndpoint_Skips()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("vLLM Endpoint", ApiTypeEnum.vLLM, 4).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(endpoint, new KeepWarmSettings { Enabled = true, Models = new List<string> { "qwen:122b" } }).ConfigureAwait(false);

            using (ModelLoadService service = new ModelLoadService(Database, Logging, null, null, null, _Transport))
            {
                ModelLoadResponse response = await service.KeepWarmVirtualModelRunnerAsync(vmr, "qwen:122b", vmr.KeepWarm).ConfigureAwait(false);

                response.EndpointResults.Should().ContainSingle();
                response.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.Skipped);
                _Transport.Paths.Should().BeEmpty();
            }
        }

        public async Task RunDueAsync_RunsEnabledRunnerOnceThenWaitsForInterval()
        {
            _Transport.ResidentModels.Add("qwen:122b");
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Scheduled Endpoint", ApiTypeEnum.Ollama, 4).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(endpoint, new KeepWarmSettings { Enabled = true, Models = new List<string> { "qwen:122b" } }).ConfigureAwait(false);
            await CreateVmrAsync(endpoint, new KeepWarmSettings { Enabled = false, Models = new List<string> { "qwen:122b" } }, "Disabled").ConfigureAwait(false);

            using (ModelLoadService loadService = new ModelLoadService(Database, Logging, null, null, null, _Transport))
            using (KeepWarmService keepWarm = new KeepWarmService(Database, Logging, loadService))
            {
                (await keepWarm.RunDueAsync().ConfigureAwait(false)).Should().Be(1, "only the enabled runner is due");
                (await keepWarm.RunDueAsync().ConfigureAwait(false)).Should().Be(0, "the interval has not elapsed");

                KeepWarmStatus status = keepWarm.GetStatus(vmr);
                status.Enabled.Should().BeTrue();
                status.LastRunSucceeded.Should().BeTrue();
                status.LastError.Should().BeNull();
                status.Models.Should().Equal("qwen:122b");
                status.NextRunUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(4));
                status.LastResults.Should().ContainSingle().Which.EndpointResults[0].OutcomeCode.Should().Be(ModelLoadOutcomeEnum.AlreadyAvailable);
            }
        }

        public async Task RunNowAsync_WithoutModels_ReportsError()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("No Model Endpoint", ApiTypeEnum.Ollama, 4).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(endpoint, new KeepWarmSettings { Enabled = true }).ConfigureAwait(false);

            using (ModelLoadService loadService = new ModelLoadService(Database, Logging, null, null, null, _Transport))
            using (KeepWarmService keepWarm = new KeepWarmService(Database, Logging, loadService))
            {
                KeepWarmStatus status = await keepWarm.RunNowAsync(vmr).ConfigureAwait(false);

                status.LastRunSucceeded.Should().BeFalse();
                status.LastError.Should().Contain("No models to keep warm");
            }
        }

        public async Task KeepWarmSettings_PersistThroughDatabase()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Persist Endpoint", ApiTypeEnum.Ollama, 4).ConfigureAwait(false);
            VirtualModelRunner vmr = await CreateVmrAsync(endpoint, new KeepWarmSettings
            {
                Enabled = true,
                Models = new List<string> { "a", "b" },
                IntervalMs = 120000,
                KeepAlive = "24h",
                TimeoutMs = 900000
            }).ConfigureAwait(false);

            VirtualModelRunner read = await Database.VirtualModelRunner.ReadAsync(TestTenantId, vmr.Id).ConfigureAwait(false);

            read.KeepWarm.Enabled.Should().BeTrue();
            read.KeepWarm.Models.Should().Equal("a", "b");
            read.KeepWarm.IntervalMs.Should().Be(120000);
            read.KeepWarm.KeepAlive.Should().Be("24h");
            read.KeepWarm.TimeoutMs.Should().Be(900000);
        }

        public async Task Validation_WarnsWhenKeepAliveNotLongerThanInterval()
        {
            ModelRunnerEndpoint endpoint = await CreateEndpointAsync("Validate Endpoint", ApiTypeEnum.Ollama, 4).ConfigureAwait(false);
            VirtualModelRunner vmr = new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Validate VMR",
                BasePath = "/v1.0/api/validate-keepwarm/",
                ApiType = ApiTypeEnum.Ollama,
                ModelRunnerEndpointIds = new List<string> { endpoint.Id },
                KeepWarm = new KeepWarmSettings { Enabled = true, Models = new List<string> { "m" }, IntervalMs = 600000, KeepAlive = "5m" }
            };

            ConfigurationValidationService validation = new ConfigurationValidationService(Database, Logging, new RoutingDecisionService(Database, Logging));
            ResourceValidationResult result = await validation.ValidateVirtualModelRunnerAsync(TestTenantId, vmr).ConfigureAwait(false);

            result.Warnings.Should().Contain(item => item.Code == "KeepWarmKeepAliveTooShort");

            vmr.KeepWarm.KeepAlive = "1h";
            result = await validation.ValidateVirtualModelRunnerAsync(TestTenantId, vmr).ConfigureAwait(false);
            result.Warnings.Should().NotContain(item => item.Code == "KeepWarmKeepAliveTooShort");
        }

        private async Task<ModelRunnerEndpoint> CreateEndpointAsync(string name, ApiTypeEnum apiType, int maxParallel)
        {
            return await Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = TestTenantId,
                Name = name,
                Hostname = name.ToLowerInvariant().Replace(" ", "-") + ".local",
                ApiType = apiType,
                Active = true,
                MaxParallelRequests = maxParallel,
                HealthCheckIntervalMs = 50,
                HealthyThreshold = 1
            }).ConfigureAwait(false);
        }

        private async Task<VirtualModelRunner> CreateVmrAsync(ModelRunnerEndpoint endpoint, KeepWarmSettings keepWarm, string suffix = "")
        {
            string slug = "keepwarm-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            return await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = "Keep Warm VMR " + suffix,
                BasePath = "/v1.0/api/" + slug + "/",
                ApiType = ApiTypeEnum.Ollama,
                ModelRunnerEndpointIds = new List<string> { endpoint.Id },
                KeepWarm = keepWarm
            }).ConfigureAwait(false);
        }

        private async Task<HealthCheckService> StartHealthyAsync(ModelRunnerEndpoint endpoint)
        {
            HealthCheckService health = new HealthCheckService(Database, Logging, new OkHandler());
            await health.StartAsync().ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                EndpointHealthState state = health.GetHealthState(endpoint.Id);
                if (state != null && state.IsHealthy) return health;
                await Task.Delay(10).ConfigureAwait(false);
            }

            throw new TimeoutException("Endpoint did not become healthy.");
        }

        private sealed class OkHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }

        private sealed class PathTransport : IModelLoadTransport
        {
            private readonly object _Lock = new object();

            public List<string> ResidentModels { get; } = new List<string>();

            public List<string> Paths { get; } = new List<string>();

            public List<string> GenerateBodies { get; } = new List<string>();

            public Action OnGenerate { get; set; }

            public int InFlightDuringLoad { get; set; } = -1;

            public Task<ModelLoadTransportResponse> SendAsync(ModelRunnerEndpoint endpoint, ModelLoadProbePlan plan, int timeoutMs, CancellationToken token = default)
            {
                lock (_Lock)
                {
                    Paths.Add(plan.Path);
                    if (plan.Path == "/api/ps")
                    {
                        string models = String.Join(",", ResidentModels.Select(item => "{\"name\":\"" + item + "\"}"));
                        return Task.FromResult(new ModelLoadTransportResponse { StatusCode = 200, Body = "{\"models\":[" + models + "]}" });
                    }

                    GenerateBodies.Add(plan.BodyJson);
                    OnGenerate?.Invoke();
                    return Task.FromResult(new ModelLoadTransportResponse { StatusCode = 200, Body = "{\"done\":true}" });
                }
            }
        }
    }
}
