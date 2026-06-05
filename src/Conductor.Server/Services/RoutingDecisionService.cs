namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// Shared routing engine used by both the proxy path and management-plane simulations.
    /// </summary>
    public class RoutingDecisionService
    {
        private static readonly object _RoundRobinLock = new object();
        private static readonly Random _Random = new Random();
        private static int _RoundRobinIndex = 0;

        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly HealthCheckService _HealthCheckService;
        private readonly SessionAffinityService _SessionAffinityService;
        private readonly LoadBalancingPolicyEvaluator _PolicyEvaluator = new LoadBalancingPolicyEvaluator();
        private readonly OperationalMetricsService _Metrics;
        private readonly RoutingModelMutationService _ModelMutationService;
        private readonly RoutingEffectiveConfigurationBuilder _EffectiveConfigurationBuilder;

        /// <summary>
        /// Instantiate the routing decision service.
        /// </summary>
        public RoutingDecisionService(
            DatabaseDriverBase database,
            LoggingModule logging,
            HealthCheckService healthCheckService = null,
            SessionAffinityService sessionAffinityService = null,
            OperationalMetricsService metrics = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _ModelMutationService = new RoutingModelMutationService(_Database);
            _EffectiveConfigurationBuilder = new RoutingEffectiveConfigurationBuilder(_Database);
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _HealthCheckService = healthCheckService;
            _SessionAffinityService = sessionAffinityService;
            _Metrics = metrics;
        }

        /// <summary>
        /// Evaluate a live or simulated request against a specific virtual model runner.
        /// </summary>
        public async Task<RoutingExecutionResult> EvaluateAsync(
            VirtualModelRunner vmr,
            UrlContext urlContext,
            RequestContext requestContext,
            bool persistSessionPin,
            CancellationToken token = default)
        {
            if (vmr == null) throw new ArgumentNullException(nameof(vmr));
            if (urlContext == null) throw new ArgumentNullException(nameof(urlContext));
            if (requestContext == null) throw new ArgumentNullException(nameof(requestContext));

            DateTime decisionStart = DateTime.UtcNow;
            RoutingExecutionResult result = new RoutingExecutionResult();
            RoutingDecision decision = new RoutingDecision
            {
                TenantId = vmr.TenantId,
                VirtualModelRunnerId = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                ApiType = urlContext.ApiType,
                RequestType = urlContext.RequestType,
                HttpStatusCode = 200
            };

            result.Decision = decision;
            result.UrlContext = urlContext;
            result.RequestBody = requestContext.Data;

            AddTimeline(decision, "VmrResolved", "Virtual Model Runner", "Passed", "Resolved virtual model runner '" + vmr.Name + "'.");

            if (!vmr.Active)
            {
                Deny(decision, 404, "VirtualModelRunnerInactive", "The requested virtual model runner is inactive.");
                return FinalizeMetrics(result, vmr, requestContext, decisionStart);
            }

            requestContext.TenantId = vmr.TenantId;
            requestContext.VirtualModelRunnerId = vmr.Id;
            requestContext.ApiType = urlContext.ApiType;
            requestContext.RequestType = urlContext.RequestType;

            if (urlContext.IsEmbeddingsRequest && !vmr.AllowEmbeddings)
            {
                Deny(decision, 403, "EmbeddingsNotAllowed", "Embeddings are not allowed on this virtual model runner.");
                AddTimeline(decision, "RequestTypeGate", "Request Type Gate", "Denied", decision.Message);
                return FinalizeMetrics(result, vmr, requestContext, decisionStart);
            }

            if (urlContext.IsCompletionsRequest && !vmr.AllowCompletions)
            {
                Deny(decision, 403, "CompletionsNotAllowed", "Completions are not allowed on this virtual model runner.");
                AddTimeline(decision, "RequestTypeGate", "Request Type Gate", "Denied", decision.Message);
                return FinalizeMetrics(result, vmr, requestContext, decisionStart);
            }

            if (urlContext.IsModelManagementRequest && !vmr.AllowModelManagement)
            {
                Deny(decision, 403, "ModelManagementNotAllowed", "Model-management operations are not allowed on this virtual model runner.");
                AddTimeline(decision, "RequestTypeGate", "Request Type Gate", "Denied", decision.Message);
                return FinalizeMetrics(result, vmr, requestContext, decisionStart);
            }

            AddTimeline(decision, "RequestTypeGate", "Request Type Gate", "Passed", "Request type is allowed.");

            LoadBalancingPolicy policy = await ResolvePolicyAsync(vmr, decision, token).ConfigureAwait(false);
            result.Policy = policy;

            string clientKey = DeriveClientKey(vmr, requestContext);
            if (!String.IsNullOrEmpty(clientKey))
            {
                requestContext.ClientIdentifier = clientKey;
            }

            List<ModelRunnerEndpoint> endpoints = await ResolveEndpointsAsync(vmr, token).ConfigureAwait(false);
            if (endpoints.Count < 1)
            {
                Deny(decision, 502, "NoConfiguredEndpoints", "No model runner endpoints are configured for this virtual model runner.");
                AddTimeline(decision, "EndpointInventory", "Endpoint Inventory", "Denied", decision.Message);
                return FinalizeMetrics(result, vmr, requestContext, decisionStart);
            }

            AddTimeline(decision, "EndpointInventory", "Endpoint Inventory", "Passed", "Resolved " + endpoints.Count + " referenced endpoint(s).");

            ModelRunnerEndpoint selectedEndpoint = null;
            bool sessionPinUsed = false;

            if (!String.IsNullOrEmpty(clientKey) && _SessionAffinityService != null)
            {
                selectedEndpoint = await TryResolvePinnedEndpointAsync(vmr, clientKey, endpoints, policy, decision, token).ConfigureAwait(false);
                sessionPinUsed = selectedEndpoint != null;
            }
            else
            {
                decision.SessionAffinityOutcome = "None";
                AddTimeline(decision, "SessionAffinity", "Session Affinity", "Skipped", "Session affinity is not active for this request.");
            }

            List<EndpointAvailability> availableEndpoints = new List<EndpointAvailability>();
            AvailabilitySummary availabilitySummary = new AvailabilitySummary();
            PopulateAvailability(decision, endpoints, availableEndpoints, availabilitySummary);

            if (selectedEndpoint == null)
            {
                if (availableEndpoints.Count < 1)
                {
                    ApplyAvailabilityDenial(decision, availabilitySummary);
                    return FinalizeMetrics(result, vmr, requestContext, decisionStart);
                }

                if (policy != null)
                {
                    LoadBalancingPolicyEvaluator.EvaluationResult evaluation = _PolicyEvaluator.Evaluate(
                        policy,
                        availableEndpoints,
                        endpointId => _HealthCheckService?.GetHealthState(endpointId));

                    MergePolicyDiagnostics(decision, evaluation);
                    if (evaluation.TelemetryFreshnessFailures > 0 && _Metrics != null)
                    {
                        for (int i = 0; i < evaluation.TelemetryFreshnessFailures; i++)
                        {
                            _Metrics.RecordTelemetryFreshnessFailure(vmr.TenantId, vmr.Id, vmr.Name, GetApiFamily(urlContext.RequestType), policy.Id);
                        }
                    }

                    if (evaluation.Success && evaluation.Candidates.Count > 0)
                    {
                        List<EndpointAvailability> tiedCandidates = GetTiedCandidates(evaluation.Candidates);
                        selectedEndpoint = SelectEndpointWithWeight(tiedCandidates, MapTieBreaker(policy.TieBreaker));
                        decision.LoadBalancingPolicyId = policy.Id;
                        decision.LoadBalancingPolicyName = policy.Name;
                        AddTimeline(decision, "PolicyEvaluation", "Policy Evaluation", "Passed", "The attached policy ranked " + evaluation.Candidates.Count + " candidate endpoint(s).");
                    }
                    else if (policy.FallbackMode == LoadBalancingPolicyFallbackModeEnum.FailClosed)
                    {
                        string message = !String.IsNullOrEmpty(evaluation.FailureReason)
                            ? evaluation.FailureReason
                            : "No endpoints satisfied the attached load-balancing policy.";
                        Deny(decision, 502, "PolicyRejected", message);
                        AddTimeline(decision, "PolicyEvaluation", "Policy Evaluation", "Denied", message);
                        return FinalizeMetrics(result, vmr, requestContext, decisionStart);
                    }
                    else
                    {
                        decision.PolicyFallbackUsed = true;
                        AddTimeline(decision, "PolicyEvaluation", "Policy Evaluation", "Fallback", evaluation.FailureReason ?? "The attached policy could not produce a candidate, so Conductor fell back to the route's legacy load-balancing mode.");
                    }
                }

                if (selectedEndpoint == null)
                {
                    selectedEndpoint = SelectEndpointWithWeight(availableEndpoints, vmr.LoadBalancingMode);
                    AddTimeline(decision, "EndpointSelection", "Endpoint Selection", "Passed", "Selected endpoint '" + selectedEndpoint.Name + "' using " + vmr.LoadBalancingMode + ".");
                }

                if (!String.IsNullOrEmpty(clientKey) && _SessionAffinityService != null && persistSessionPin && selectedEndpoint != null)
                {
                    _SessionAffinityService.SetPinnedEndpoint(vmr.Id, clientKey, selectedEndpoint.Id, vmr.SessionTimeoutMs, vmr.SessionMaxEntries);
                    decision.SessionAffinityOutcome = "Created";
                }
            }
            else
            {
                AddTimeline(decision, "EndpointSelection", "Endpoint Selection", "Passed", "Reused session-affinity pin for endpoint '" + selectedEndpoint.Name + "'.");
            }

            if (selectedEndpoint == null)
            {
                Deny(decision, 502, "NoCandidateSelected", "Conductor could not select an endpoint.");
                return FinalizeMetrics(result, vmr, requestContext, decisionStart);
            }

            result.Endpoint = selectedEndpoint;
            decision.SelectedEndpointId = selectedEndpoint.Id;
            decision.SelectedEndpointName = selectedEndpoint.Name;
            decision.SelectedEndpointUrl = selectedEndpoint.GetBaseUrl();
            decision.SessionPinUsed = sessionPinUsed;
            requestContext.SelectedEndpointId = selectedEndpoint.Id;

            RoutingModelMutationResult mutation = await _ModelMutationService.ApplyAsync(vmr, urlContext, requestContext.Data, token).ConfigureAwait(false);
            result.RequestBody = mutation.RequestBody;
            result.ModelDefinition = mutation.ModelDefinition;
            result.ModelConfiguration = mutation.ModelConfiguration;

            decision.RequestedModel = mutation.RequestedModel;
            decision.EffectiveModel = mutation.EffectiveModel;
            decision.ModelDefinitionId = mutation.ModelDefinition?.Id;
            decision.ModelDefinitionName = mutation.ModelDefinition?.Name;
            decision.ModelConfigurationId = mutation.ModelConfiguration?.Id;
            decision.ModelConfigurationName = mutation.ModelConfiguration?.Name;
            decision.RequestWasMutated = mutation.MutationSummary.HasMutations;
            decision.MutationSummary = mutation.MutationSummary;

            result.EffectiveModel = mutation.EffectiveModel;

            if (!mutation.Success)
            {
                decision.HttpStatusCode = mutation.HttpStatusCode;
                decision.OutcomeCode = mutation.OutcomeCode;
                decision.Message = mutation.Message;
                decision.DenialReasonCode = mutation.OutcomeCode;
                decision.DenialReason = mutation.Message;
                return FinalizeMetrics(result, vmr, requestContext, decisionStart);
            }

            AddTimeline(decision, "RequestMutation", "Request Mutation", "Passed", mutation.MutationSummary.HasMutations
                ? "Applied model-definition or configuration mutations before forwarding."
                : "No model-definition or configuration mutation was required.");

            decision.Success = true;
            decision.OutcomeCode = "Routed";
            decision.Message = "Request routed to endpoint '" + selectedEndpoint.Name + "'.";
            MarkSelectedCandidate(decision, selectedEndpoint.Id);
            return FinalizeMetrics(result, vmr, requestContext, decisionStart);
        }

        /// <summary>
        /// Build a routing explanation for a representative request shape.
        /// </summary>
        public async Task<RoutingDecision> ExplainAsync(VirtualModelRunner vmr, RoutingSimulationRequest request, CancellationToken token = default)
        {
            if (vmr == null) throw new ArgumentNullException(nameof(vmr));
            if (request == null) throw new ArgumentNullException(nameof(request));

            string relativePath = String.IsNullOrWhiteSpace(request.RelativePath)
                ? GetDefaultRelativePath(vmr.ApiType)
                : request.RelativePath.Trim();
            if (!relativePath.StartsWith("/")) relativePath = "/" + relativePath;

            string fullPath = vmr.BasePath.TrimEnd('/') + relativePath;
            if (!String.IsNullOrWhiteSpace(request.QueryString))
            {
                fullPath += request.QueryString.StartsWith("?") ? request.QueryString : "?" + request.QueryString;
            }

            UrlContext urlContext = UrlContext.Parse(fullPath, request.Method ?? "POST");
            RequestContext requestContext = new RequestContext
            {
                HttpMethod = request.Method ?? "POST",
                Path = fullPath,
                OriginalUrl = fullPath,
                QueryString = request.QueryString,
                Headers = request.Headers ?? new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase),
                ClientIpAddress = String.IsNullOrWhiteSpace(request.SourceIp) ? "127.0.0.1" : request.SourceIp,
                Data = !String.IsNullOrWhiteSpace(request.Body) ? Encoding.UTF8.GetBytes(request.Body) : null
            };

            RoutingExecutionResult execution = await EvaluateAsync(vmr, urlContext, requestContext, false, token).ConfigureAwait(false);
            return execution.Decision;
        }

        /// <summary>
        /// Build a resolved, read-only effective view of a virtual model runner.
        /// </summary>
        public async Task<EffectiveVirtualModelRunnerConfiguration> BuildEffectiveConfigurationAsync(VirtualModelRunner vmr, CancellationToken token = default)
        {
            return await _EffectiveConfigurationBuilder.BuildAsync(vmr, token).ConfigureAwait(false);
        }

        private RoutingExecutionResult FinalizeMetrics(RoutingExecutionResult result, VirtualModelRunner vmr, RequestContext requestContext, DateTime decisionStart)
        {
            RoutingDecision decision = result.Decision;
            double routeDecisionDurationMs = (DateTime.UtcNow - decisionStart).TotalMilliseconds;
            string apiFamily = GetApiFamily(requestContext.RequestType);

            if (_Metrics != null)
            {
                _Metrics.RecordRoutingDecision(
                    vmr.TenantId,
                    vmr.Id,
                    vmr.Name,
                    apiFamily,
                    decision.Success,
                    decision.OutcomeCode,
                    decision.DenialReasonCode,
                    decision.SessionAffinityOutcome,
                    decision.PolicyFallbackUsed,
                    routeDecisionDurationMs);
            }

            decision.EvaluatedUtc = DateTime.UtcNow;
            return result;
        }

        private async Task<LoadBalancingPolicy> ResolvePolicyAsync(VirtualModelRunner vmr, RoutingDecision decision, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(vmr.LoadBalancingPolicyId))
            {
                AddTimeline(decision, "PolicyResolution", "Policy Resolution", "Skipped", "No load-balancing policy is attached to this virtual model runner.");
                return null;
            }

            LoadBalancingPolicy policy = await _Database.LoadBalancingPolicy.ReadAsync(vmr.TenantId, vmr.LoadBalancingPolicyId, token).ConfigureAwait(false);
            if (policy == null)
            {
                AddTimeline(decision, "PolicyResolution", "Policy Resolution", "Warning", "The referenced load-balancing policy '" + vmr.LoadBalancingPolicyId + "' was not found. Conductor will use the route's legacy load-balancing mode.");
                return null;
            }

            if (!policy.Active)
            {
                AddTimeline(decision, "PolicyResolution", "Policy Resolution", "Warning", "The referenced load-balancing policy '" + policy.Name + "' is inactive. Conductor will use the route's legacy load-balancing mode.");
                return null;
            }

            decision.LoadBalancingPolicyId = policy.Id;
            decision.LoadBalancingPolicyName = policy.Name;
            AddTimeline(decision, "PolicyResolution", "Policy Resolution", "Passed", "Resolved active load-balancing policy '" + policy.Name + "'.");
            return policy;
        }

        private async Task<List<ModelRunnerEndpoint>> ResolveEndpointsAsync(VirtualModelRunner vmr, CancellationToken token)
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

        private async Task<ModelRunnerEndpoint> TryResolvePinnedEndpointAsync(
            VirtualModelRunner vmr,
            string clientKey,
            List<ModelRunnerEndpoint> endpoints,
            LoadBalancingPolicy policy,
            RoutingDecision decision,
            CancellationToken token)
        {
            if (!_SessionAffinityService.TryGetPinnedEndpoint(vmr.Id, clientKey, out string pinnedEndpointId))
            {
                decision.SessionAffinityOutcome = "Miss";
                AddTimeline(decision, "SessionAffinity", "Session Affinity", "Miss", "No sticky-session pin exists for this client.");
                return null;
            }

            ModelRunnerEndpoint pinnedEndpoint = endpoints.Find(item => String.Equals(item.Id, pinnedEndpointId, StringComparison.Ordinal));
            if (pinnedEndpoint == null)
            {
                _SessionAffinityService.RemovePinnedEndpoint(vmr.Id, clientKey);
                decision.SessionAffinityOutcome = "StaleRemoved";
                AddTimeline(decision, "SessionAffinity", "Session Affinity", "Warning", "Removed a stale sticky-session pin because the referenced endpoint no longer exists on this route.");
                return null;
            }

            if (!pinnedEndpoint.Active)
            {
                _SessionAffinityService.RemovePinnedEndpoint(vmr.Id, clientKey);
                decision.SessionAffinityOutcome = "StaleRemoved";
                AddTimeline(decision, "SessionAffinity", "Session Affinity", "Warning", "Removed a stale sticky-session pin because the referenced endpoint is inactive.");
                return null;
            }

            if (pinnedEndpoint.ServiceState == EndpointServiceStateEnum.Quarantined)
            {
                _SessionAffinityService.RemovePinnedEndpoint(vmr.Id, clientKey);
                decision.SessionAffinityOutcome = "Quarantined";
                AddTimeline(decision, "SessionAffinity", "Session Affinity", "Denied", "The pinned endpoint is quarantined and cannot be reused.");
                return null;
            }

            EndpointHealthState pinnedState = _HealthCheckService?.GetHealthState(pinnedEndpoint.Id);
            bool pinnedHealthy = pinnedState == null || pinnedState.IsHealthy;
            bool pinnedHasCapacity = pinnedEndpoint.MaxParallelRequests <= 0 || pinnedState == null || pinnedState.InFlightRequests < pinnedEndpoint.MaxParallelRequests;

            if (!pinnedHealthy || !pinnedHasCapacity)
            {
                _SessionAffinityService.RemovePinnedEndpoint(vmr.Id, clientKey);
                decision.SessionAffinityOutcome = "StaleRemoved";
                AddTimeline(decision, "SessionAffinity", "Session Affinity", "Warning", "Removed a sticky-session pin because the pinned endpoint is unavailable.");
                return null;
            }

            if (policy != null)
            {
                EndpointAvailability availability = new EndpointAvailability(pinnedEndpoint, true, true);
                if (!_PolicyEvaluator.IsEndpointEligible(policy, availability, pinnedState))
                {
                    _SessionAffinityService.RemovePinnedEndpoint(vmr.Id, clientKey);
                    decision.SessionAffinityOutcome = "StaleRemoved";
                    AddTimeline(decision, "SessionAffinity", "Session Affinity", "Warning", "Removed a sticky-session pin because the pinned endpoint no longer satisfies the attached policy.");
                    return null;
                }
            }

            decision.SessionAffinityOutcome = "Hit";
            AddTimeline(decision, "SessionAffinity", "Session Affinity", "Hit", "Reused sticky-session pin for endpoint '" + pinnedEndpoint.Name + "'.");
            return pinnedEndpoint;
        }

        private void PopulateAvailability(
            RoutingDecision decision,
            List<ModelRunnerEndpoint> endpoints,
            List<EndpointAvailability> availableEndpoints,
            AvailabilitySummary availabilitySummary)
        {
            foreach (ModelRunnerEndpoint endpoint in endpoints)
            {
                RoutingEndpointCandidate candidate = new RoutingEndpointCandidate
                {
                    EndpointId = endpoint.Id,
                    EndpointName = endpoint.Name,
                    EndpointUrl = endpoint.GetBaseUrl(),
                    Active = endpoint.Active,
                    ServiceState = endpoint.ServiceState
                };

                EndpointHealthState state = _HealthCheckService?.GetHealthState(endpoint.Id);
                candidate.IsHealthy = state == null || state.IsHealthy;
                candidate.HasCapacity = endpoint.MaxParallelRequests <= 0 || state == null || state.InFlightRequests < endpoint.MaxParallelRequests;

                if (!endpoint.Active)
                {
                    availabilitySummary.InactiveCount++;
                    candidate.ExclusionReasonCode = "EndpointInactive";
                    candidate.ExclusionReason = "The endpoint is inactive.";
                }
                else if (endpoint.ServiceState == EndpointServiceStateEnum.Quarantined)
                {
                    availabilitySummary.QuarantinedCount++;
                    candidate.ExclusionReasonCode = "EndpointQuarantined";
                    candidate.ExclusionReason = "The endpoint is quarantined and excluded from routing.";
                }
                else if (endpoint.ServiceState == EndpointServiceStateEnum.Draining)
                {
                    availabilitySummary.DrainingCount++;
                    candidate.ExclusionReasonCode = "EndpointDraining";
                    candidate.ExclusionReason = "The endpoint is draining and excluded from new assignments.";
                }
                else if (!candidate.IsHealthy)
                {
                    availabilitySummary.UnhealthyCount++;
                    candidate.ExclusionReasonCode = "EndpointUnhealthy";
                    candidate.ExclusionReason = "The endpoint is unhealthy.";
                }
                else if (!candidate.HasCapacity)
                {
                    availabilitySummary.AtCapacityCount++;
                    candidate.ExclusionReasonCode = "AllEndpointsAtCapacity";
                    candidate.ExclusionReason = "The endpoint is at capacity.";
                }
                else
                {
                    candidate.Included = true;
                    availableEndpoints.Add(new EndpointAvailability(endpoint, candidate.IsHealthy, candidate.HasCapacity));
                }

                decision.Candidates.Add(candidate);
            }

            AddTimeline(decision, "Availability", "Availability Screening", "Passed", availableEndpoints.Count + " endpoint(s) remain eligible after active, service-state, health, and capacity screening.");
        }

        private void ApplyAvailabilityDenial(RoutingDecision decision, AvailabilitySummary summary)
        {
            if (summary.AtCapacityCount > 0 && summary.UnhealthyCount == 0 && summary.QuarantinedCount == 0 && summary.DrainingCount == 0)
            {
                Deny(decision, 429, "AllEndpointsAtCapacity", "All eligible endpoints are at capacity.");
            }
            else if (summary.QuarantinedCount > 0 && summary.QuarantinedCount == decision.Candidates.Count)
            {
                Deny(decision, 503, "AllEndpointsQuarantined", "All configured endpoints are quarantined.");
            }
            else if (summary.DrainingCount > 0 && summary.DrainingCount == decision.Candidates.Count)
            {
                Deny(decision, 503, "AllEndpointsDraining", "All configured endpoints are draining and unavailable for new work.");
            }
            else
            {
                Deny(decision, 502, "NoHealthyEndpointsAvailable", "No healthy endpoints are currently available for routing.");
            }

            AddTimeline(decision, "Availability", "Availability Screening", "Denied", decision.Message);
        }

        private void MergePolicyDiagnostics(RoutingDecision decision, LoadBalancingPolicyEvaluator.EvaluationResult evaluation)
        {
            foreach (LoadBalancingPolicyEvaluator.EndpointDiagnostic diagnostic in evaluation.Diagnostics ?? new List<LoadBalancingPolicyEvaluator.EndpointDiagnostic>())
            {
                RoutingEndpointCandidate candidate = decision.Candidates.Find(item => String.Equals(item.EndpointId, diagnostic.Availability?.Endpoint?.Id, StringComparison.Ordinal));
                if (candidate == null)
                {
                    continue;
                }

                if (!diagnostic.Included)
                {
                    candidate.Included = false;
                    candidate.ExclusionReasonCode = diagnostic.ExclusionReasonCode;
                    candidate.ExclusionReason = diagnostic.ExclusionReason;
                }
                candidate.PolicyScore = diagnostic.Score;

                foreach (LoadBalancingPolicyEvaluator.FilterDiagnostic filter in diagnostic.Filters)
                {
                    candidate.Evidence.Add(new RoutingDecisionStage
                    {
                        Code = "PolicyFilter",
                        Title = filter.Metric,
                        Outcome = filter.Passed ? "Passed" : "Failed",
                        Message = filter.Message,
                        Attributes = new Dictionary<string, string>
                        {
                            { "Expected", filter.ExpectedValue ?? String.Empty },
                            { "Actual", filter.ActualValue ?? String.Empty },
                            { "FailureCode", filter.FailureCode ?? String.Empty }
                        }
                    });
                }

                foreach (LoadBalancingPolicyEvaluator.RankingDiagnostic ranking in diagnostic.Ranking)
                {
                    candidate.Evidence.Add(new RoutingDecisionStage
                    {
                        Code = "PolicyRanking",
                        Title = ranking.Metric,
                        Outcome = String.IsNullOrEmpty(ranking.FailureCode) ? "Scored" : "Unavailable",
                        Message = ranking.Message,
                        Attributes = new Dictionary<string, string>
                        {
                            { "RawValue", ranking.RawValue.HasValue ? ranking.RawValue.Value.ToString("G") : String.Empty },
                            { "NormalizedValue", ranking.NormalizedValue.HasValue ? ranking.NormalizedValue.Value.ToString("G") : String.Empty },
                            { "WeightedContribution", ranking.WeightedContribution.HasValue ? ranking.WeightedContribution.Value.ToString("G") : String.Empty },
                            { "FailureCode", ranking.FailureCode ?? String.Empty }
                        }
                    });
                }
            }
        }

        private static List<EndpointAvailability> GetTiedCandidates(List<LoadBalancingPolicyEvaluator.EndpointCandidate> candidates)
        {
            List<EndpointAvailability> tiedCandidates = new List<EndpointAvailability>();
            if (candidates == null || candidates.Count < 1) return tiedCandidates;

            double topScore = candidates[0].Score;
            foreach (LoadBalancingPolicyEvaluator.EndpointCandidate candidate in candidates)
            {
                if (Math.Abs(candidate.Score - topScore) < 0.000001)
                {
                    tiedCandidates.Add(candidate.Availability);
                }
            }

            return tiedCandidates;
        }

        private static string GetApiFamily(RequestTypeEnum requestType)
        {
            switch (requestType)
            {
                case RequestTypeEnum.OpenAIChatCompletions:
                case RequestTypeEnum.OpenAICompletions:
                case RequestTypeEnum.OpenAIEmbeddings:
                case RequestTypeEnum.OpenAIListModels:
                    return "OpenAI";
                case RequestTypeEnum.GeminiGenerateContent:
                case RequestTypeEnum.GeminiStreamGenerateContent:
                case RequestTypeEnum.GeminiEmbedContent:
                case RequestTypeEnum.GeminiListModels:
                    return "Gemini";
                case RequestTypeEnum.OllamaGenerate:
                case RequestTypeEnum.OllamaChat:
                case RequestTypeEnum.OllamaEmbeddings:
                case RequestTypeEnum.OllamaListTags:
                case RequestTypeEnum.OllamaListRunningModels:
                case RequestTypeEnum.OllamaPullModel:
                case RequestTypeEnum.OllamaDeleteModel:
                case RequestTypeEnum.OllamaShowModelInfo:
                    return "Ollama";
                default:
                    return "Management";
            }
        }

        private static string GetDefaultRelativePath(ApiTypeEnum apiType)
        {
            switch (apiType)
            {
                case ApiTypeEnum.OpenAI:
                    return "/v1/chat/completions";
                case ApiTypeEnum.Gemini:
                    return "/v1beta/models/gemini-1.5-flash:generateContent";
                case ApiTypeEnum.Ollama:
                default:
                    return "/api/chat";
            }
        }

        private static string DeriveClientKey(VirtualModelRunner vmr, RequestContext requestContext)
        {
            if (vmr == null || requestContext == null) return null;

            switch (vmr.SessionAffinityMode)
            {
                case SessionAffinityModeEnum.SourceIP:
                    if (requestContext.Headers != null && requestContext.Headers.TryGetValue("X-Forwarded-For", out string forwardedFor) && !String.IsNullOrWhiteSpace(forwardedFor))
                    {
                        string[] parts = forwardedFor.Split(',');
                        if (parts.Length > 0 && !String.IsNullOrWhiteSpace(parts[0]))
                        {
                            return parts[0].Trim();
                        }
                    }
                    return requestContext.ClientIpAddress;
                case SessionAffinityModeEnum.ApiKey:
                    if (requestContext.Headers != null && requestContext.Headers.TryGetValue("Authorization", out string authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return authorization.Substring(7).Trim();
                    }
                    return null;
                case SessionAffinityModeEnum.Header:
                    if (requestContext.Headers != null && !String.IsNullOrWhiteSpace(vmr.SessionAffinityHeader) && requestContext.Headers.TryGetValue(vmr.SessionAffinityHeader, out string headerValue))
                    {
                        return headerValue;
                    }
                    return null;
                case SessionAffinityModeEnum.None:
                default:
                    return null;
            }
        }

        private static LoadBalancingModeEnum MapTieBreaker(LoadBalancingPolicyTieBreakerEnum tieBreaker)
        {
            switch (tieBreaker)
            {
                case LoadBalancingPolicyTieBreakerEnum.Random:
                    return LoadBalancingModeEnum.Random;
                case LoadBalancingPolicyTieBreakerEnum.FirstAvailable:
                    return LoadBalancingModeEnum.FirstAvailable;
                case LoadBalancingPolicyTieBreakerEnum.RoundRobin:
                default:
                    return LoadBalancingModeEnum.RoundRobin;
            }
        }

        private static ModelRunnerEndpoint SelectEndpointWithWeight(List<EndpointAvailability> endpoints, LoadBalancingModeEnum mode)
        {
            if (endpoints == null || endpoints.Count < 1) return null;
            if (endpoints.Count == 1) return endpoints[0].Endpoint;

            switch (mode)
            {
                case LoadBalancingModeEnum.Random:
                    int totalWeight = endpoints.Sum(item => item.Endpoint.Weight);
                    int randomValue = _Random.Next(totalWeight);
                    int randomCumulative = 0;
                    foreach (EndpointAvailability item in endpoints)
                    {
                        randomCumulative += item.Endpoint.Weight;
                        if (randomValue < randomCumulative)
                        {
                            return item.Endpoint;
                        }
                    }
                    return endpoints[endpoints.Count - 1].Endpoint;
                case LoadBalancingModeEnum.FirstAvailable:
                    return endpoints[0].Endpoint;
                case LoadBalancingModeEnum.RoundRobin:
                default:
                    lock (_RoundRobinLock)
                    {
                        int totalRoundRobinWeight = endpoints.Sum(item => item.Endpoint.Weight);
                        int index = _RoundRobinIndex % totalRoundRobinWeight;
                        _RoundRobinIndex++;

                        int cumulative = 0;
                        foreach (EndpointAvailability item in endpoints)
                        {
                            cumulative += item.Endpoint.Weight;
                            if (index < cumulative)
                            {
                                return item.Endpoint;
                            }
                        }
                    }
                    return endpoints[endpoints.Count - 1].Endpoint;
            }
        }

        private static void AddTimeline(RoutingDecision decision, string code, string title, string outcome, string message)
        {
            decision.Timeline.Add(new RoutingDecisionStage
            {
                Code = code,
                Title = title,
                Outcome = outcome,
                Message = message
            });
        }

        private static void Deny(RoutingDecision decision, int httpStatusCode, string denialReasonCode, string message)
        {
            decision.Success = false;
            decision.HttpStatusCode = httpStatusCode;
            decision.OutcomeCode = "Denied";
            decision.DenialReasonCode = denialReasonCode;
            decision.DenialReason = message;
            decision.Message = message;
        }

        private static void MarkSelectedCandidate(RoutingDecision decision, string endpointId)
        {
            if (decision == null || String.IsNullOrWhiteSpace(endpointId)) return;

            RoutingEndpointCandidate candidate = decision.Candidates.Find(item => String.Equals(item.EndpointId, endpointId, StringComparison.Ordinal));
            if (candidate != null)
            {
                candidate.Selected = true;
            }
        }

        private sealed class AvailabilitySummary
        {
            public int InactiveCount { get; set; }
            public int DrainingCount { get; set; }
            public int QuarantinedCount { get; set; }
            public int UnhealthyCount { get; set; }
            public int AtCapacityCount { get; set; }
        }

    }
}
