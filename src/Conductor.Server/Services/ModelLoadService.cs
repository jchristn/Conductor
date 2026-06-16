namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Requests;
    using Conductor.Core.Responses;
    using Conductor.Core.Serialization;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Service for loading or verifying models on concrete endpoints and virtual model runners.
    /// </summary>
    public class ModelLoadService : IDisposable
    {
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly RoutingDecisionService _RoutingDecisionService;
        private readonly HealthCheckService _HealthCheckService;
        private readonly OperationalMetricsService _Metrics;
        private readonly IModelLoadTransport _Transport;
        private readonly VirtualModelRunnerReservationService _ReservationService;
        private readonly bool _OwnsTransport;
        private readonly ModelLoadProbeBuilder _ProbeBuilder;
        private readonly ModelLoadVerificationService _VerificationService;
        private readonly Serializer _Serializer = new Serializer();
        private bool _Disposed;

        /// <summary>
        /// Instantiate the model load service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="routingDecisionService">Routing decision service.</param>
        /// <param name="healthCheckService">Health check service.</param>
        /// <param name="metrics">Operational metrics service.</param>
        /// <param name="transport">Optional transport override for tests.</param>
        public ModelLoadService(
            DatabaseDriverBase database,
            LoggingModule logging,
            RoutingDecisionService routingDecisionService = null,
            HealthCheckService healthCheckService = null,
            OperationalMetricsService metrics = null,
            IModelLoadTransport transport = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _RoutingDecisionService = routingDecisionService;
            _HealthCheckService = healthCheckService;
            _Metrics = metrics;
            _Transport = transport ?? new DefaultModelLoadTransport();
            _ReservationService = new VirtualModelRunnerReservationService(_Database, _Logging);
            _OwnsTransport = transport == null;
            _ProbeBuilder = new ModelLoadProbeBuilder();
            _VerificationService = new ModelLoadVerificationService(_Transport, _ProbeBuilder);
        }

        /// <summary>
        /// Load or verify a model on a concrete model runner endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="request">Model load request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model load response.</returns>
        public async Task<ModelLoadResponse> LoadEndpointAsync(
            ModelRunnerEndpoint endpoint,
            ModelLoadRequest request,
            CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            ModelLoadRequest normalizedRequest = NormalizeRequest(request);
            if (String.IsNullOrWhiteSpace(normalizedRequest.Model))
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Model is required.");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            ModelLoadResponse response = CreateResponse("ModelRunnerEndpoint", endpoint.Id, endpoint.TenantId, normalizedRequest.Model, normalizedRequest.ProbeKind);

            ModelLoadEndpointResult endpointResult = await ExecuteForEndpointAsync(endpoint, normalizedRequest, normalizedRequest.Model, token).ConfigureAwait(false);
            response.EndpointResults.Add(endpointResult);

            CompleteResponse(response, stopwatch);
            RecordMetrics(response);
            return response;
        }

        /// <summary>
        /// Load or verify a model through a virtual model runner.
        /// </summary>
        /// <param name="vmr">Virtual model runner.</param>
        /// <param name="request">Model load request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model load response.</returns>
        public async Task<ModelLoadResponse> LoadVirtualModelRunnerAsync(
            VirtualModelRunner vmr,
            ModelLoadRequest request,
            CancellationToken token = default)
        {
            return await LoadVirtualModelRunnerAsync(vmr, request, null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Load or verify a model through a virtual model runner.
        /// </summary>
        /// <param name="vmr">Virtual model runner.</param>
        /// <param name="request">Model load request.</param>
        /// <param name="requestContext">Authenticated request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model load response.</returns>
        public async Task<ModelLoadResponse> LoadVirtualModelRunnerAsync(
            VirtualModelRunner vmr,
            ModelLoadRequest request,
            RequestContext requestContext,
            CancellationToken token = default)
        {
            if (vmr == null) throw new ArgumentNullException(nameof(vmr));

            ModelLoadRequest normalizedRequest = NormalizeRequest(request);
            await EnforceReservationGateAsync(vmr, requestContext, token).ConfigureAwait(false);
            string model = await ResolveModelAsync(vmr, normalizedRequest, token).ConfigureAwait(false);
            Stopwatch stopwatch = Stopwatch.StartNew();
            ModelLoadResponse response = CreateResponse("VirtualModelRunner", vmr.Id, vmr.TenantId, model, normalizedRequest.ProbeKind);

            ModelLoadTargetSelection selection = await ResolveTargetsAsync(vmr, normalizedRequest, model, token).ConfigureAwait(false);
            response.RoutingDecision = selection.RoutingDecision;
            if (!String.IsNullOrWhiteSpace(selection.EffectiveModel))
            {
                model = selection.EffectiveModel;
                response.Model = model;
            }

            foreach (ModelLoadEndpointResult skipped in selection.SkippedResults)
            {
                response.EndpointResults.Add(skipped);
            }

            if (selection.Endpoints.Count < 1)
            {
                if (response.EndpointResults.Count < 1)
                {
                    response.OutcomeCode = ModelLoadOutcomeEnum.NoEligibleEndpoints;
                    response.Message = "No endpoints were eligible for model loading.";
                }

                CompleteResponse(response, stopwatch);
                RecordMetrics(response);
                return response;
            }

            foreach (ModelRunnerEndpoint endpoint in selection.Endpoints)
            {
                token.ThrowIfCancellationRequested();
                ModelLoadEndpointResult endpointResult = await ExecuteForEndpointAsync(endpoint, normalizedRequest, model, token).ConfigureAwait(false);
                response.EndpointResults.Add(endpointResult);
            }

            CompleteResponse(response, stopwatch);
            RecordMetrics(response);
            return response;
        }

        private async Task EnforceReservationGateAsync(VirtualModelRunner vmr, RequestContext requestContext, CancellationToken token)
        {
            string credentialOwnerUserId = await ResolveCredentialOwnerUserIdAsync(vmr.TenantId, requestContext, token).ConfigureAwait(false);
            ReservationEvaluationResult reservationResult = await _ReservationService.EvaluateAsync(new ReservationEvaluationContext
            {
                TenantId = vmr.TenantId,
                VirtualModelRunnerId = vmr.Id,
                UserId = requestContext?.UserId,
                CredentialId = requestContext?.CredentialId,
                CredentialOwnerUserId = credentialOwnerUserId,
                RequestType = RequestTypeEnum.LoadVirtualModelRunnerModel,
                AtUtc = DateTime.UtcNow
            }, token).ConfigureAwait(false);

            if (reservationResult == null || reservationResult.Allowed)
            {
                return;
            }

            _Logging.Warn(
                "[ModelLoadService] reservation denied tenant=" + vmr.TenantId
                + " vmr=" + vmr.Id
                + " reservation=" + (reservationResult.ReservationId ?? String.Empty)
                + " user=" + (requestContext?.UserId ?? String.Empty)
                + " credential=" + (requestContext?.CredentialId ?? String.Empty)
                + " reason=" + (reservationResult.ReasonCode ?? String.Empty));

            throw new WebserverException(ApiResultEnum.BadRequest, reservationResult.ReasonText ?? "The virtual model runner is reserved.");
        }

        private async Task<string> ResolveCredentialOwnerUserIdAsync(string tenantId, RequestContext requestContext, CancellationToken token)
        {
            if (requestContext == null)
            {
                return null;
            }

            if (!String.IsNullOrWhiteSpace(requestContext.UserId))
            {
                return requestContext.UserId;
            }

            if (String.IsNullOrWhiteSpace(tenantId) || String.IsNullOrWhiteSpace(requestContext.CredentialId))
            {
                return null;
            }

            Credential credential = await _Database.Credential.ReadAsync(tenantId, requestContext.CredentialId, token).ConfigureAwait(false);
            return credential?.UserId;
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">True when disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing && _OwnsTransport && _Transport is IDisposable disposableTransport)
            {
                disposableTransport.Dispose();
            }

            _Disposed = true;
        }

        private async Task<ModelLoadEndpointResult> ExecuteForEndpointAsync(
            ModelRunnerEndpoint endpoint,
            ModelLoadRequest request,
            string model,
            CancellationToken token)
        {
            DateTime startedUtc = DateTime.UtcNow;
            Stopwatch stopwatch = Stopwatch.StartNew();
            ModelLoadEndpointResult result = new ModelLoadEndpointResult
            {
                EndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                ApiType = endpoint.ApiType,
                BaseUrl = endpoint.GetBaseUrl(),
                StartedUtc = startedUtc
            };

            try
            {
                ModelLoadProbePlan plan = _ProbeBuilder.Build(endpoint, request, model);
                result.Mechanism = plan.Mechanism;
                result.RequestPath = plan.Path;
                result.IgnoredFields = new List<string>(plan.IgnoredFields ?? new List<string>());

                if (request.DryRun)
                {
                    result.Success = true;
                    result.OutcomeCode = ModelLoadOutcomeEnum.DryRun;
                    return FinalizeEndpointResult(result, stopwatch);
                }

                if (endpoint.ApiType == ApiTypeEnum.Ollama && request.VerifyLoaded && plan.ExplicitLoad)
                {
                    bool alreadyLoaded = await _VerificationService.VerifyAsync(endpoint, model, request.TimeoutMs, token).ConfigureAwait(false);
                    if (alreadyLoaded)
                    {
                        result.Success = true;
                        result.OutcomeCode = ModelLoadOutcomeEnum.AlreadyAvailable;
                        result.VerifiedLoaded = true;
                        result.Mechanism = "OllamaRunningModels";
                        result.RequestPath = "/api/ps";
                        return FinalizeEndpointResult(result, stopwatch);
                    }
                }

                ModelLoadTransportResponse transportResponse = await SendWithRetriesAsync(endpoint, plan, request, token).ConfigureAwait(false);
                result.ProviderStatusCode = transportResponse?.StatusCode;

                if (transportResponse == null)
                {
                    result.Success = false;
                    result.OutcomeCode = ModelLoadOutcomeEnum.Failed;
                    result.ErrorMessage = "No upstream response was returned.";
                    return FinalizeEndpointResult(result, stopwatch);
                }

                if (!transportResponse.IsSuccessStatusCode)
                {
                    result.Success = false;
                    result.OutcomeCode = transportResponse.StatusCode == 401 || transportResponse.StatusCode == 403
                        ? ModelLoadOutcomeEnum.UnauthorizedUpstream
                        : ModelLoadOutcomeEnum.Failed;
                    result.ErrorMessage = "Upstream provider returned HTTP " + transportResponse.StatusCode + ".";
                    return FinalizeEndpointResult(result, stopwatch);
                }

                if (plan.MetadataOnly)
                {
                    bool found = _VerificationService.ContainsModel(endpoint.ApiType, transportResponse.Body, model);
                    result.Success = found;
                    result.VerifiedLoaded = found;
                    result.OutcomeCode = found
                        ? DetermineMetadataSuccessOutcome(endpoint.ApiType)
                        : ModelLoadOutcomeEnum.Failed;
                    result.ErrorMessage = found ? null : "Model was not found in provider metadata.";
                    return FinalizeEndpointResult(result, stopwatch);
                }

                bool verified = false;
                if (request.VerifyLoaded)
                {
                    verified = await _VerificationService.VerifyAsync(endpoint, model, request.TimeoutMs, token).ConfigureAwait(false);
                }

                result.Success = true;
                result.VerifiedLoaded = verified;
                result.OutcomeCode = DetermineProbeSuccessOutcome(endpoint.ApiType);
                return FinalizeEndpointResult(result, stopwatch);
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.OutcomeCode = ModelLoadOutcomeEnum.TimedOut;
                result.ErrorMessage = "The model load request timed out or was cancelled.";
                return FinalizeEndpointResult(result, stopwatch);
            }
            catch (Exception ex)
            {
                _Logging.Warn("[ModelLoadService] endpoint load failed for " + endpoint.Id + ": " + ex.Message);
                result.Success = false;
                result.OutcomeCode = ModelLoadOutcomeEnum.Failed;
                result.ErrorMessage = ex.Message;
                return FinalizeEndpointResult(result, stopwatch);
            }
        }

        private async Task<ModelLoadTransportResponse> SendWithRetriesAsync(
            ModelRunnerEndpoint endpoint,
            ModelLoadProbePlan plan,
            ModelLoadRequest request,
            CancellationToken token)
        {
            ModelLoadTransportResponse response = null;
            int attempts = request.MaxRetries + 1;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                response = await _Transport.SendAsync(endpoint, plan, request.TimeoutMs, token).ConfigureAwait(false);
                if (response != null && response.IsSuccessStatusCode)
                {
                    return response;
                }
            }

            return response;
        }

        private async Task<string> ResolveModelAsync(VirtualModelRunner vmr, ModelLoadRequest request, CancellationToken token)
        {
            List<ModelDefinition> activeDefinitions = await ResolveActiveModelDefinitionsAsync(vmr, token).ConfigureAwait(false);

            if (!String.IsNullOrWhiteSpace(request.ModelDefinitionId))
            {
                ModelDefinition selectedDefinition = activeDefinitions.Find(item => String.Equals(item.Id, request.ModelDefinitionId, StringComparison.Ordinal));
                if (selectedDefinition == null)
                {
                    throw new WebserverException(ApiResultEnum.BadRequest, "ModelDefinitionId is not attached to this virtual model runner.");
                }

                if (!String.IsNullOrWhiteSpace(request.Model)
                    && !String.Equals(request.Model, selectedDefinition.Name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new WebserverException(ApiResultEnum.BadRequest, "Model must match the selected model definition.");
                }

                return selectedDefinition.Name;
            }

            if (!String.IsNullOrWhiteSpace(request.Model))
            {
                if (activeDefinitions.Count > 0
                    && !activeDefinitions.Exists(item => String.Equals(item.Name, request.Model, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new WebserverException(ApiResultEnum.BadRequest, "The requested model is not attached to this virtual model runner.");
                }

                return request.Model;
            }

            if (activeDefinitions.Count == 1)
            {
                return activeDefinitions[0].Name;
            }

            throw new WebserverException(ApiResultEnum.BadRequest, "Model is required when the virtual model runner does not have exactly one active model definition.");
        }

        private async Task<List<ModelDefinition>> ResolveActiveModelDefinitionsAsync(VirtualModelRunner vmr, CancellationToken token)
        {
            List<ModelDefinition> definitions = new List<ModelDefinition>();
            foreach (string definitionId in vmr.ModelDefinitionIds ?? new List<string>())
            {
                ModelDefinition definition = await _Database.ModelDefinition.ReadAsync(vmr.TenantId, definitionId, token).ConfigureAwait(false);
                if (definition != null && definition.Active)
                {
                    definitions.Add(definition);
                }
            }

            return definitions;
        }

        private async Task<ModelLoadTargetSelection> ResolveTargetsAsync(
            VirtualModelRunner vmr,
            ModelLoadRequest request,
            string model,
            CancellationToken token)
        {
            switch (request.TargetMode)
            {
                case ModelLoadTargetModeEnum.SelectedEndpoint:
                    return await ResolveSelectedEndpointAsync(vmr, request, model, token).ConfigureAwait(false);
                case ModelLoadTargetModeEnum.AllEligibleEndpoints:
                    return await ResolveAllConfiguredEndpointsAsync(vmr, request, true, token).ConfigureAwait(false);
                case ModelLoadTargetModeEnum.AllConfiguredEndpoints:
                    return await ResolveAllConfiguredEndpointsAsync(vmr, request, false, token).ConfigureAwait(false);
                case ModelLoadTargetModeEnum.SpecificEndpointIds:
                    return await ResolveSpecificEndpointsAsync(vmr, request, token).ConfigureAwait(false);
                default:
                    throw new WebserverException(ApiResultEnum.BadRequest, "Unsupported target mode.");
            }
        }

        private async Task<ModelLoadTargetSelection> ResolveSelectedEndpointAsync(
            VirtualModelRunner vmr,
            ModelLoadRequest request,
            string model,
            CancellationToken token)
        {
            if (_RoutingDecisionService == null)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Routing selection is not available.");
            }

            RoutingSimulationRequest simulation = BuildRoutingSimulation(vmr, request, model);
            string fullPath = vmr.BasePath.TrimEnd('/') + simulation.RelativePath;
            UrlContext urlContext = UrlContext.Parse(fullPath, simulation.Method);
            RequestContext requestContext = new RequestContext
            {
                HttpMethod = simulation.Method,
                Path = fullPath,
                OriginalUrl = fullPath,
                Headers = simulation.Headers,
                ClientIpAddress = simulation.SourceIp,
                Data = Encoding.UTF8.GetBytes(simulation.Body)
            };

            RoutingExecutionResult execution = await _RoutingDecisionService.EvaluateAsync(vmr, urlContext, requestContext, false, token).ConfigureAwait(false);
            if (execution == null || execution.Decision == null || !execution.Decision.Success || execution.Endpoint == null)
            {
                string message = execution?.Decision?.Message ?? "No endpoint was selected for model loading.";
                throw new WebserverException(ApiResultEnum.BadRequest, message);
            }

            ModelLoadTargetSelection selection = new ModelLoadTargetSelection
            {
                RoutingDecision = execution.Decision,
                EffectiveModel = !String.IsNullOrWhiteSpace(execution.EffectiveModel) ? execution.EffectiveModel : model
            };
            selection.Endpoints.Add(execution.Endpoint);
            return selection;
        }

        private async Task<ModelLoadTargetSelection> ResolveAllConfiguredEndpointsAsync(
            VirtualModelRunner vmr,
            ModelLoadRequest request,
            bool onlyEligible,
            CancellationToken token)
        {
            ModelLoadTargetSelection selection = new ModelLoadTargetSelection();
            List<ModelRunnerEndpoint> endpoints = await ReadConfiguredEndpointsAsync(vmr, token).ConfigureAwait(false);

            foreach (ModelRunnerEndpoint endpoint in endpoints)
            {
                ModelLoadEndpointResult skipResult = GetSkipResult(endpoint, request, onlyEligible);
                if (skipResult != null)
                {
                    selection.SkippedResults.Add(skipResult);
                }
                else
                {
                    selection.Endpoints.Add(endpoint);
                }
            }

            return selection;
        }

        private async Task<ModelLoadTargetSelection> ResolveSpecificEndpointsAsync(
            VirtualModelRunner vmr,
            ModelLoadRequest request,
            CancellationToken token)
        {
            if (request.EndpointIds == null || request.EndpointIds.Count < 1)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "EndpointIds is required when TargetMode is SpecificEndpointIds.");
            }

            ModelLoadTargetSelection selection = new ModelLoadTargetSelection();
            List<ModelRunnerEndpoint> endpoints = await ReadConfiguredEndpointsAsync(vmr, token).ConfigureAwait(false);
            foreach (string endpointId in request.EndpointIds)
            {
                ModelRunnerEndpoint endpoint = endpoints.Find(item => String.Equals(item.Id, endpointId, StringComparison.Ordinal));
                if (endpoint == null)
                {
                    throw new WebserverException(ApiResultEnum.BadRequest, "Endpoint '" + endpointId + "' is not attached to this virtual model runner.");
                }

                ModelLoadEndpointResult skipResult = GetSkipResult(endpoint, request, false);
                if (skipResult != null)
                {
                    selection.SkippedResults.Add(skipResult);
                }
                else
                {
                    selection.Endpoints.Add(endpoint);
                }
            }

            return selection;
        }

        private async Task<List<ModelRunnerEndpoint>> ReadConfiguredEndpointsAsync(VirtualModelRunner vmr, CancellationToken token)
        {
            List<ModelRunnerEndpoint> endpoints = new List<ModelRunnerEndpoint>();
            foreach (string endpointId in vmr.ModelRunnerEndpointIds ?? new List<string>())
            {
                ModelRunnerEndpoint endpoint = await _Database.ModelRunnerEndpoint.ReadAsync(vmr.TenantId, endpointId, token).ConfigureAwait(false);
                if (endpoint != null)
                {
                    endpoints.Add(endpoint);
                }
            }

            return endpoints;
        }

        private ModelLoadEndpointResult GetSkipResult(ModelRunnerEndpoint endpoint, ModelLoadRequest request, bool onlyEligible)
        {
            if (!endpoint.Active && !request.IncludeInactive)
            {
                return CreateSkippedResult(endpoint, "Endpoint is inactive.");
            }

            if (!onlyEligible)
            {
                return null;
            }

            if (endpoint.ServiceState == EndpointServiceStateEnum.Draining)
            {
                return CreateSkippedResult(endpoint, "Endpoint is draining.");
            }

            if (endpoint.ServiceState == EndpointServiceStateEnum.Quarantined)
            {
                return CreateSkippedResult(endpoint, "Endpoint is quarantined.");
            }

            EndpointHealthState state = _HealthCheckService?.GetHealthState(endpoint.Id);
            if (state != null && !state.IsHealthy)
            {
                return CreateSkippedResult(endpoint, "Endpoint is unhealthy.");
            }

            if (state != null && endpoint.MaxParallelRequests > 0 && state.InFlightRequests >= endpoint.MaxParallelRequests)
            {
                return CreateSkippedResult(endpoint, "Endpoint is at capacity.");
            }

            return null;
        }

        private static ModelLoadEndpointResult CreateSkippedResult(ModelRunnerEndpoint endpoint, string message)
        {
            DateTime now = DateTime.UtcNow;
            return new ModelLoadEndpointResult
            {
                EndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                ApiType = endpoint.ApiType,
                BaseUrl = endpoint.GetBaseUrl(),
                Success = false,
                OutcomeCode = ModelLoadOutcomeEnum.Skipped,
                StartedUtc = now,
                CompletedUtc = now,
                ErrorMessage = message
            };
        }

        private RoutingSimulationRequest BuildRoutingSimulation(VirtualModelRunner vmr, ModelLoadRequest request, string model)
        {
            ModelLoadProbeKindEnum probe = request.ProbeKind;
            if (probe == ModelLoadProbeKindEnum.Auto)
            {
                probe = !vmr.AllowCompletions && vmr.AllowEmbeddings
                    ? ModelLoadProbeKindEnum.Embeddings
                    : ModelLoadProbeKindEnum.ChatCompletion;
            }

            switch (vmr.ApiType)
            {
                case ApiTypeEnum.Ollama:
                    return BuildOllamaRoutingSimulation(probe, model, request.InputText);
                case ApiTypeEnum.Gemini:
                    return BuildGeminiRoutingSimulation(probe, model, request.InputText);
                case ApiTypeEnum.OpenAI:
                case ApiTypeEnum.vLLM:
                default:
                    return BuildOpenAIRoutingSimulation(probe, model, request.InputText);
            }
        }

        private RoutingSimulationRequest BuildOllamaRoutingSimulation(ModelLoadProbeKindEnum probe, string model, string inputText)
        {
            if (probe == ModelLoadProbeKindEnum.Embeddings)
            {
                return new RoutingSimulationRequest
                {
                    Method = "POST",
                    RelativePath = "/api/embed",
                    SourceIp = "127.0.0.1",
                    Body = _Serializer.SerializeJson(new Dictionary<string, object>
                    {
                        { "model", model },
                        { "input", inputText }
                    }, false)
                };
            }

            return new RoutingSimulationRequest
            {
                Method = "POST",
                RelativePath = "/api/chat",
                SourceIp = "127.0.0.1",
                Body = _Serializer.SerializeJson(new Dictionary<string, object>
                {
                    { "model", model },
                    { "messages", new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                { "role", "user" },
                                { "content", inputText }
                            }
                        }
                    }
                }, false)
            };
        }

        private RoutingSimulationRequest BuildOpenAIRoutingSimulation(ModelLoadProbeKindEnum probe, string model, string inputText)
        {
            if (probe == ModelLoadProbeKindEnum.Embeddings)
            {
                return new RoutingSimulationRequest
                {
                    Method = "POST",
                    RelativePath = "/v1/embeddings",
                    SourceIp = "127.0.0.1",
                    Body = _Serializer.SerializeJson(new Dictionary<string, object>
                    {
                        { "model", model },
                        { "input", inputText }
                    }, false)
                };
            }

            return new RoutingSimulationRequest
            {
                Method = "POST",
                RelativePath = "/v1/chat/completions",
                SourceIp = "127.0.0.1",
                Body = _Serializer.SerializeJson(new Dictionary<string, object>
                {
                    { "model", model },
                    { "messages", new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                { "role", "user" },
                                { "content", inputText }
                            }
                        }
                    }
                }, false)
            };
        }

        private RoutingSimulationRequest BuildGeminiRoutingSimulation(ModelLoadProbeKindEnum probe, string model, string inputText)
        {
            if (probe == ModelLoadProbeKindEnum.Embeddings)
            {
                return new RoutingSimulationRequest
                {
                    Method = "POST",
                    RelativePath = "/v1beta/models/" + model + ":embedContent",
                    SourceIp = "127.0.0.1",
                    Body = _Serializer.SerializeJson(new Dictionary<string, object>
                    {
                        { "content", new Dictionary<string, object>
                            {
                                { "parts", new List<Dictionary<string, object>>
                                    {
                                        new Dictionary<string, object> { { "text", inputText } }
                                    }
                                }
                            }
                        }
                    }, false)
                };
            }

            return new RoutingSimulationRequest
            {
                Method = "POST",
                RelativePath = "/v1beta/models/" + model + ":generateContent",
                SourceIp = "127.0.0.1",
                Body = _Serializer.SerializeJson(new Dictionary<string, object>
                {
                    { "contents", new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                { "parts", new List<Dictionary<string, object>>
                                    {
                                        new Dictionary<string, object> { { "text", inputText } }
                                    }
                                }
                            }
                        }
                    }
                }, false)
            };
        }

        private static ModelLoadEndpointResult FinalizeEndpointResult(ModelLoadEndpointResult result, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            result.CompletedUtc = DateTime.UtcNow;
            result.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            return result;
        }

        private static ModelLoadRequest NormalizeRequest(ModelLoadRequest request)
        {
            if (request == null)
            {
                return new ModelLoadRequest();
            }

            request.EndpointIds = request.EndpointIds ?? new List<string>();
            if (String.IsNullOrWhiteSpace(request.InputText))
            {
                request.InputText = "conductor warmup";
            }

            return request;
        }

        private static ModelLoadResponse CreateResponse(string targetType, string targetId, string tenantId, string model, ModelLoadProbeKindEnum probeKind)
        {
            return new ModelLoadResponse
            {
                TargetType = targetType,
                TargetId = targetId,
                TenantId = tenantId,
                Model = model,
                ProbeKind = probeKind,
                StartedUtc = DateTime.UtcNow
            };
        }

        private static void CompleteResponse(ModelLoadResponse response, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            response.CompletedUtc = DateTime.UtcNow;
            response.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            List<ModelLoadEndpointResult> attemptedResults = response.EndpointResults
                .Where(item => item.OutcomeCode != ModelLoadOutcomeEnum.Skipped)
                .ToList();

            if (attemptedResults.Count < 1)
            {
                response.Success = false;
                response.OutcomeCode = response.OutcomeCode == ModelLoadOutcomeEnum.Failed
                    ? ModelLoadOutcomeEnum.NoEligibleEndpoints
                    : response.OutcomeCode;
                response.Message = response.Message ?? "No endpoint load attempts were made.";
                return;
            }

            int successCount = attemptedResults.Count(item => item.Success);
            response.Success = successCount == attemptedResults.Count;
            response.OutcomeCode = DetermineOverallOutcome(attemptedResults);
            response.Message = "Model load probe completed on " + successCount + " of " + attemptedResults.Count + " endpoint(s).";
        }

        private static ModelLoadOutcomeEnum DetermineOverallOutcome(List<ModelLoadEndpointResult> results)
        {
            if (results == null || results.Count < 1)
            {
                return ModelLoadOutcomeEnum.NoEligibleEndpoints;
            }

            if (results.Exists(item => !item.Success))
            {
                return ModelLoadOutcomeEnum.Failed;
            }

            if (results.Exists(item => item.OutcomeCode == ModelLoadOutcomeEnum.Loaded)) return ModelLoadOutcomeEnum.Loaded;
            if (results.Exists(item => item.OutcomeCode == ModelLoadOutcomeEnum.AlreadyAvailable)) return ModelLoadOutcomeEnum.AlreadyAvailable;
            if (results.Exists(item => item.OutcomeCode == ModelLoadOutcomeEnum.VerifiedRemote)) return ModelLoadOutcomeEnum.VerifiedRemote;
            if (results.Exists(item => item.OutcomeCode == ModelLoadOutcomeEnum.Verified)) return ModelLoadOutcomeEnum.Verified;
            if (results.Exists(item => item.OutcomeCode == ModelLoadOutcomeEnum.DryRun)) return ModelLoadOutcomeEnum.DryRun;
            return results[0].OutcomeCode;
        }

        private static ModelLoadOutcomeEnum DetermineMetadataSuccessOutcome(ApiTypeEnum apiType)
        {
            if (apiType == ApiTypeEnum.OpenAI || apiType == ApiTypeEnum.Gemini)
            {
                return ModelLoadOutcomeEnum.VerifiedRemote;
            }

            return ModelLoadOutcomeEnum.Verified;
        }

        private static ModelLoadOutcomeEnum DetermineProbeSuccessOutcome(ApiTypeEnum apiType)
        {
            if (apiType == ApiTypeEnum.Ollama)
            {
                return ModelLoadOutcomeEnum.Loaded;
            }

            if (apiType == ApiTypeEnum.OpenAI || apiType == ApiTypeEnum.Gemini)
            {
                return ModelLoadOutcomeEnum.VerifiedRemote;
            }

            return ModelLoadOutcomeEnum.Verified;
        }

        private void RecordMetrics(ModelLoadResponse response)
        {
            if (_Metrics == null || response == null)
            {
                return;
            }

            _Metrics.RecordModelLoadRequest(
                response.TenantId,
                response.TargetType,
                response.TargetId,
                response.Success,
                response.OutcomeCode.ToString(),
                response.DurationMs);

            foreach (ModelLoadEndpointResult endpointResult in response.EndpointResults)
            {
                _Metrics.RecordModelLoadEndpointAttempt(
                    response.TenantId,
                    response.TargetType,
                    response.TargetId,
                    endpointResult.EndpointId,
                    endpointResult.ApiType.ToString(),
                    endpointResult.Mechanism,
                    endpointResult.Success,
                    endpointResult.OutcomeCode.ToString(),
                    endpointResult.DurationMs);
            }
        }
    }
}
