namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using ConductorConstants = Conductor.Core.Constants;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using JsonMerge;
    using SyslogLogging;
    using SwiftStack;
    using WatsonWebserver.Core;

    using HttpMethod = WatsonWebserver.Core.HttpMethod;
    /// <summary>
    /// Proxy controller for virtual model runner requests.
    /// </summary>
    public class ProxyController : BaseController
    {
        private static readonly string _Header = "[ProxyController] ";
        private static readonly HttpClient _HttpClient = new HttpClient();
        private static int _RoundRobinIndex = 0;
        private static readonly object _RoundRobinLock = new object();
        private static readonly Random _Random = new Random();
        private readonly HealthCheckService _HealthCheckService;
        private readonly SessionAffinityService _SessionAffinityService;

        /// <summary>
        /// Buffer size for streaming responses. Default is 8KB.
        /// </summary>
        public int StreamingBufferSize { get; set; } = 8192;

        /// <summary>
        /// Instantiate the proxy controller.
        /// </summary>
        public ProxyController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging, HealthCheckService healthCheckService = null, SessionAffinityService sessionAffinityService = null)
            : base(database, authService, serializer, logging)
        {
            _HealthCheckService = healthCheckService;
            _SessionAffinityService = sessionAffinityService;
        }

        /// <summary>
        /// Handle incoming proxy request.
        /// Supports standard HTTP responses, chunked transfer encoding, and Server-Sent Events (SSE).
        /// </summary>
        /// <param name="ctx">The HTTP context.</param>
        /// <param name="requestContext">Request context containing the pre-read request body.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <exception cref="ArgumentNullException">Thrown if requestContext is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
        public async Task HandleRequest(HttpContextBase ctx, RequestContext requestContext, CancellationToken cancellationToken = default)
        {
            try
            {
                // Parse the URL to extract VMR base path
                UrlContext urlContext = UrlContext.Parse(ctx.Request.Url.RawWithoutQuery, ctx.Request.Method.ToString());
                if (urlContext == null || String.IsNullOrEmpty(urlContext.BasePath))
                {
                    await SendNotFound(ctx);
                    return;
                }

                // Find the VMR by base path
                VirtualModelRunner vmr = await Database.VirtualModelRunner.ReadByBasePathAsync(urlContext.BasePath);
                if (vmr == null || !vmr.Active)
                {
                    await SendNotFound(ctx);
                    return;
                }

                // Check if request type is allowed
                if (urlContext.IsEmbeddingsRequest && !vmr.AllowEmbeddings)
                {
                    await SendForbidden(ctx, "Embeddings not allowed on this endpoint");
                    return;
                }

                if (urlContext.IsCompletionsRequest && !vmr.AllowCompletions)
                {
                    await SendForbidden(ctx, "Completions not allowed on this endpoint");
                    return;
                }

                if (urlContext.IsModelManagementRequest && !vmr.AllowModelManagement)
                {
                    await SendForbidden(ctx, "Model management not allowed on this endpoint");
                    return;
                }

                // Derive client key for session affinity
                string clientKey = null;
                bool sessionPinUsed = false;

                if (vmr.SessionAffinityMode != Conductor.Core.Enums.SessionAffinityModeEnum.None)
                {
                    clientKey = DeriveClientKey(ctx, vmr.SessionAffinityMode, vmr.SessionAffinityHeader);
                }

                // Populate request context
                requestContext.ClientIpAddress = ctx.Request.Source.IpAddress;
                requestContext.ClientIdentifier = clientKey;

                // Get all endpoint IDs
                List<string> endpointIds = vmr.ModelRunnerEndpointIds;
                if (endpointIds == null || endpointIds.Count == 0)
                {
                    await SendBadGateway(ctx, "No model runners configured");
                    return;
                }

                // Attempt session affinity lookup before filtering
                ModelRunnerEndpoint endpoint = null;

                if (!String.IsNullOrEmpty(clientKey) && _SessionAffinityService != null)
                {
                    if (_SessionAffinityService.TryGetPinnedEndpoint(vmr.Id, clientKey, out string pinnedEndpointId))
                    {
                        // Verify the pinned endpoint is still valid
                        ModelRunnerEndpoint pinnedEp = await Database.ModelRunnerEndpoint.ReadAsync(vmr.TenantId, pinnedEndpointId);
                        if (pinnedEp != null && pinnedEp.Active && endpointIds.Contains(pinnedEndpointId))
                        {
                            bool pinnedHealthy = true;
                            bool pinnedHasCapacity = true;

                            if (_HealthCheckService != null)
                            {
                                EndpointHealthState pinnedState = _HealthCheckService.GetHealthState(pinnedEndpointId);
                                if (pinnedState != null)
                                {
                                    pinnedHealthy = pinnedState.IsHealthy;
                                    if (pinnedEp.MaxParallelRequests > 0 && pinnedState.InFlightRequests >= pinnedEp.MaxParallelRequests)
                                    {
                                        pinnedHasCapacity = false;
                                    }
                                }
                            }

                            if (pinnedHealthy && pinnedHasCapacity)
                            {
                                endpoint = pinnedEp;
                                sessionPinUsed = true;
                            }
                            else
                            {
                                // Pinned endpoint failed checks, remove pin
                                _SessionAffinityService.RemovePinnedEndpoint(vmr.Id, clientKey);
                                Logging.Debug(_Header + "removed stale session pin for client " + clientKey + " to endpoint " + pinnedEndpointId);
                            }
                        }
                        else
                        {
                            // Pinned endpoint no longer valid
                            _SessionAffinityService.RemovePinnedEndpoint(vmr.Id, clientKey);
                        }
                    }
                }

                // If no pinned endpoint was used, proceed with normal load balancing
                if (endpoint == null)
                {
                    // Filter endpoints by health and capacity
                    List<EndpointAvailability> availableEndpoints = new List<EndpointAvailability>();
                    bool anyUnhealthy = false;
                    bool anyAtCapacity = false;

                    foreach (string endpointId in endpointIds)
                    {
                        ModelRunnerEndpoint ep = await Database.ModelRunnerEndpoint.ReadAsync(vmr.TenantId, endpointId);
                        if (ep == null || !ep.Active) continue;

                        bool isHealthy = true;
                        bool hasCapacity = true;

                        if (_HealthCheckService != null)
                        {
                            EndpointHealthState healthState = _HealthCheckService.GetHealthState(endpointId);
                            if (healthState != null)
                            {
                                isHealthy = healthState.IsHealthy;
                                // Check capacity (0 = unlimited)
                                if (ep.MaxParallelRequests > 0 && healthState.InFlightRequests >= ep.MaxParallelRequests)
                                {
                                    hasCapacity = false;
                                }
                            }
                        }

                        if (!isHealthy) anyUnhealthy = true;
                        if (!hasCapacity) anyAtCapacity = true;

                        if (isHealthy && hasCapacity)
                        {
                            availableEndpoints.Add(new EndpointAvailability(ep, isHealthy, hasCapacity));
                        }
                    }

                    // Check if we have any available endpoints
                    if (availableEndpoints.Count == 0)
                    {
                        if (anyAtCapacity && !anyUnhealthy)
                        {
                            await SendTooManyRequests(ctx, "All endpoints at capacity");
                        }
                        else
                        {
                            await SendBadGateway(ctx, "No healthy endpoints available");
                        }
                        return;
                    }

                    // Select endpoint using load balancing (with weights)
                    endpoint = SelectEndpointWithWeight(availableEndpoints, vmr.LoadBalancingMode);

                    // Pin the selected endpoint for this client
                    if (!String.IsNullOrEmpty(clientKey) && _SessionAffinityService != null)
                    {
                        _SessionAffinityService.SetPinnedEndpoint(vmr.Id, clientKey, endpoint.Id, vmr.SessionTimeoutMs, vmr.SessionMaxEntries);
                    }
                }

                // Track in-flight request
                bool incrementedInFlight = false;
                if (_HealthCheckService != null)
                {
                    incrementedInFlight = _HealthCheckService.TryIncrementInFlight(endpoint.Id, endpoint.MaxParallelRequests);
                    if (!incrementedInFlight)
                    {
                        // Race condition - endpoint went to capacity between check and increment
                        await SendTooManyRequests(ctx, "Endpoint at capacity");
                        return;
                    }
                }

                try
                {
                    // Build the target URL
                    string targetUrl = BuildTargetUrl(endpoint, urlContext);

                    string effectiveModel = null;
                    if (requestContext.Data != null && requestContext.Data.Length > 0)
                    {
                        // Apply model definition and configuration logic
                        ModelResolutionResult resolution = await ApplyModelDefinitionAndConfigurationAsync(
                            requestContext.Data, 
                            vmr, 
                            urlContext, ctx).ConfigureAwait(false);

                        if (!resolution.Success)
                        {
                            // Error response already sent by the method
                            return;
                        }

                        requestContext.Data = resolution.Body;
                        effectiveModel = resolution.EffectiveModel;

                        // Apply pinning if this is an embeddings or completions request
                        if (requestContext.Data != null 
                            && requestContext.Data.Length > 0 
                            && vmr.ModelConfigurationIds != null 
                            && vmr.ModelConfigurationIds.Count > 0)
                        {
                            requestContext.Data = await ApplyPinningAsync(
                                requestContext.Data, 
                                vmr, 
                                urlContext).ConfigureAwait(false);
                        }
                    }

                    // Forward the request and send response back
                    // Use using to ensure HttpResponseMessage is disposed after streaming completes
                    using (HttpResponseMessage response = await ForwardRequestAsync(
                        ctx,
                        targetUrl,
                        endpoint,
                        requestContext.Data,
                        cancellationToken).ConfigureAwait(false))
                    {
                        await SendProxyResponse(
                            ctx,
                            response,
                            vmr.Id,
                            endpoint.Id,
                            effectiveModel,
                            sessionPinUsed,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    // Decrement in-flight count when request completes
                    if (incrementedInFlight && _HealthCheckService != null)
                    {
                        _HealthCheckService.DecrementInFlight(endpoint.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Warn(_Header + "proxy exception:" + Environment.NewLine + ex.ToString());
                await SendBadGateway(ctx, "Proxy error: " + ex.Message);
            }
        }

        private ModelRunnerEndpoint SelectEndpointWithWeight(List<EndpointAvailability> endpoints, LoadBalancingModeEnum mode)
        {
            if (endpoints.Count == 0) return null;
            if (endpoints.Count == 1) return endpoints[0].Endpoint;

            switch (mode)
            {
                case LoadBalancingModeEnum.Random:
                    // Weighted random selection
                    int totalWeight = 0;
                    foreach (EndpointAvailability item in endpoints)
                    {
                        totalWeight += item.Endpoint.Weight;
                    }

                    int randomValue = _Random.Next(totalWeight);
                    int cumulative = 0;
                    foreach (EndpointAvailability item in endpoints)
                    {
                        cumulative += item.Endpoint.Weight;
                        if (randomValue < cumulative)
                        {
                            return item.Endpoint;
                        }
                    }
                    return endpoints[endpoints.Count - 1].Endpoint;

                case LoadBalancingModeEnum.FirstAvailable:
                    return endpoints[0].Endpoint;

                case LoadBalancingModeEnum.RoundRobin:
                default:
                    // Weighted round-robin: build expanded list based on weights
                    lock (_RoundRobinLock)
                    {
                        int totalWeightRR = 0;
                        foreach (EndpointAvailability item in endpoints)
                        {
                            totalWeightRR += item.Endpoint.Weight;
                        }

                        int index = _RoundRobinIndex % totalWeightRR;
                        _RoundRobinIndex++;

                        int cumulativeRR = 0;
                        foreach (EndpointAvailability item in endpoints)
                        {
                            cumulativeRR += item.Endpoint.Weight;
                            if (index < cumulativeRR)
                            {
                                return item.Endpoint;
                            }
                        }
                        return endpoints[endpoints.Count - 1].Endpoint;
                    }
            }
        }

        private string BuildTargetUrl(ModelRunnerEndpoint endpoint, UrlContext urlContext)
        {
            string scheme = endpoint.UseSsl ? "https" : "http";
            string baseUrl = scheme + "://" + endpoint.Hostname + ":" + endpoint.Port;

            // Map the relative path based on API type
            string relativePath = urlContext.RelativePath;
            if (String.IsNullOrEmpty(relativePath))
            {
                relativePath = "/";
            }

            return baseUrl + relativePath;
        }

        /// <summary>
        /// Applies model definitions and configurations to the request body.
        /// </summary>
        /// <param name="requestBody">The original request body.</param>
        /// <param name="vmr">The virtual model runner.</param>
        /// <param name="urlContext">The URL context containing request type information.</param>
        /// <param name="ctx">HTTP context for sending error responses.</param>
        /// <returns>A ModelResolutionResult containing the success status, modified body, and resolved model name.</returns>
        private async Task<ModelResolutionResult> ApplyModelDefinitionAndConfigurationAsync(
            byte[] requestBody,
            VirtualModelRunner vmr,
            UrlContext urlContext,
            HttpContextBase ctx)
        {
            try
            {
                // Only process embeddings and completions requests
                if (!urlContext.IsCompletionsRequest && !urlContext.IsEmbeddingsRequest)
                {
                    return new ModelResolutionResult(true, requestBody, null);
                }

                if (requestBody == null || requestBody.Length == 0)
                {
                    return new ModelResolutionResult(true, requestBody, null);
                }

                string bodyJson = Encoding.UTF8.GetString(requestBody);
                Dictionary<string, object> bodyDict = Serializer.DeserializeJson<Dictionary<string, object>>(bodyJson);
                if (bodyDict == null)
                {
                    return new ModelResolutionResult(true, requestBody, null);
                }

                // Extract the current model from request
                string requestedModel = null;
                if (bodyDict.TryGetValue("model", out object modelValue) && modelValue != null)
                {
                    requestedModel = modelValue.ToString();
                }

                // Retrieve model definitions
                List<ModelDefinition> modelDefinitions = new List<ModelDefinition>();
                if (vmr.ModelDefinitionIds != null && vmr.ModelDefinitionIds.Count > 0)
                {
                    foreach (string defId in vmr.ModelDefinitionIds)
                    {
                        ModelDefinition def = await Database.ModelDefinition.ReadAsync(vmr.TenantId, defId).ConfigureAwait(false);
                        if (def != null && def.Active)
                        {
                            modelDefinitions.Add(def);
                        }
                    }
                }

                // Enforce StrictMode: only accept requests for models defined by attached ModelDefinitions
                if (vmr.StrictMode)
                {
                    if (modelDefinitions.Count == 0)
                    {
                        // No ModelDefinitions attached, reject all requests in strict mode
                        Logging.Warn(_Header + "strict mode enabled but no model definitions attached");
                        await SendUnauthorized(ctx, "No models available").ConfigureAwait(false);
                        return new ModelResolutionResult(false, null, null);
                    }

                    if (String.IsNullOrEmpty(requestedModel))
                    {
                        // No model specified in request, reject in strict mode
                        Logging.Warn(_Header + "strict mode: no model specified in request");
                        await SendUnauthorized(ctx, "Model not specified").ConfigureAwait(false);
                        return new ModelResolutionResult(false, null, null);
                    }

                    // Check if requested model is in the list of defined models
                    bool modelAllowed = false;
                    foreach (ModelDefinition def in modelDefinitions)
                    {
                        if (String.Equals(def.Name, requestedModel, StringComparison.OrdinalIgnoreCase))
                        {
                            modelAllowed = true;
                            break;
                        }
                    }

                    if (!modelAllowed)
                    {
                        Logging.Warn(_Header + "strict mode: invalid model requested: " + requestedModel);
                        await SendUnauthorized(ctx, "Invalid model requested").ConfigureAwait(false);
                        return new ModelResolutionResult(false, null, null);
                    }
                }

                // Apply model definition logic
                string effectiveModel = requestedModel;

                if (modelDefinitions.Count == 1)
                {
                    // Step 1: If one ModelDefinition is found, apply the model from the ModelDefinition
                    effectiveModel = modelDefinitions[0].Name;
                    bodyDict["model"] = effectiveModel;
                }
                else if (modelDefinitions.Count > 1)
                {
                    // Step 2: If multiple ModelDefinition objects are found, ensure that the specified model
                    // is listed in one of the ModelDefinition objects
                    if (String.IsNullOrEmpty(requestedModel))
                    {
                        Logging.Warn(_Header + "invalid model requested: (empty) - no model specified and multiple definitions available");
                        await SendUnauthorized(ctx, "Model not specified").ConfigureAwait(false);
                        return new ModelResolutionResult(false, null, null);
                    }

                    bool modelFound = false;
                    foreach (ModelDefinition def in modelDefinitions)
                    {
                        if (String.Equals(def.Name, requestedModel, StringComparison.OrdinalIgnoreCase))
                        {
                            modelFound = true;
                            effectiveModel = def.Name;
                            break;
                        }
                    }

                    if (!modelFound)
                    {
                        Logging.Warn(_Header + "invalid model requested: " + requestedModel);
                        await SendUnauthorized(ctx, "Invalid model requested").ConfigureAwait(false);
                        return new ModelResolutionResult(false, null, null);
                    }
                }

                // Retrieve model configurations
                List<ModelConfiguration> modelConfigurations = new List<ModelConfiguration>();
                if (vmr.ModelConfigurationIds != null && vmr.ModelConfigurationIds.Count > 0)
                {
                    foreach (string configId in vmr.ModelConfigurationIds)
                    {
                        ModelConfiguration config = await Database.ModelConfiguration.ReadAsync(vmr.TenantId, configId).ConfigureAwait(false);
                        if (config != null && config.Active)
                        {
                            modelConfigurations.Add(config);
                        }
                    }
                }

                // Apply model configuration logic (only for completions requests)
                if (urlContext.IsCompletionsRequest && modelConfigurations.Count > 0)
                {
                    if (modelConfigurations.Count == 1)
                    {
                        // Step 3: If one ModelConfiguration is available, apply its settings
                        // But only if its Model is null/empty (applies to all) or matches the effective model
                        ModelConfiguration singleConfig = modelConfigurations[0];
                        if (String.IsNullOrEmpty(singleConfig.Model) ||
                            String.Equals(singleConfig.Model, effectiveModel, StringComparison.OrdinalIgnoreCase))
                        {
                            ApplyConfigurationToBody(bodyDict, singleConfig, urlContext);
                        }
                    }
                    else
                    {
                        // Step 4: If multiple ModelConfigurations are available, find the first one
                        // that matches the effective model (which may have been set in step 1)
                        ModelConfiguration matchingConfig = null;

                        foreach (ModelConfiguration config in modelConfigurations)
                        {
                            // Match if config.Model is null/empty (applies to all) or matches the effective model
                            if (String.IsNullOrEmpty(config.Model) ||
                                String.Equals(config.Model, effectiveModel, StringComparison.OrdinalIgnoreCase))
                            {
                                matchingConfig = config;
                                break;
                            }
                        }

                        if (matchingConfig != null)
                        {
                            ApplyConfigurationToBody(bodyDict, matchingConfig, urlContext);
                        }
                        // If no matching config found, no changes are applied (as specified)
                    }
                }

                // Serialize back to bytes
                string modifiedJson = Serializer.SerializeJson(bodyDict, false);
                return new ModelResolutionResult(true, Encoding.UTF8.GetBytes(modifiedJson), effectiveModel);
            }
            catch (Exception ex)
            {
                Logging.Warn(_Header + "model definition/configuration exception:" + Environment.NewLine + ex.ToString());
                return new ModelResolutionResult(true, requestBody, null);
            }
        }

        /// <summary>
        /// Applies configuration parameters to the request body dictionary.
        /// </summary>
        /// <param name="bodyDict">Request body dictionary to modify.</param>
        /// <param name="config">Model configuration to apply.</param>
        /// <param name="urlContext">URL context for request type information.</param>
        private void ApplyConfigurationToBody(Dictionary<string, object> bodyDict, ModelConfiguration config, UrlContext urlContext)
        {
            // Apply temperature if set
            if (config.Temperature.HasValue)
            {
                bodyDict["temperature"] = config.Temperature.Value;
            }

            // Apply top_p if set
            if (config.TopP.HasValue)
            {
                bodyDict["top_p"] = config.TopP.Value;
            }

            // Apply top_k if set (for Ollama-style APIs)
            if (config.TopK.HasValue)
            {
                bodyDict["top_k"] = config.TopK.Value;
            }

            // Apply max_tokens if set
            if (config.MaxTokens.HasValue)
            {
                bodyDict["max_tokens"] = config.MaxTokens.Value;
            }

            // Apply repeat_penalty if set (for Ollama-style APIs)
            if (config.RepeatPenalty.HasValue)
            {
                bodyDict["repeat_penalty"] = config.RepeatPenalty.Value;
            }

            // Apply context window size if set
            // Note: This is typically used in Ollama APIs as num_ctx
            if (config.ContextWindowSize.HasValue)
            {
                bodyDict["num_ctx"] = config.ContextWindowSize.Value;
            }
        }

        /// <summary>
        /// Applies pinned properties from model configurations to the request body.
        /// </summary>
        /// <param name="requestBody">The original request body.</param>
        /// <param name="vmr">The virtual model runner.</param>
        /// <param name="urlContext">The URL context containing request type information.</param>
        /// <returns>The modified request body with pinned properties merged in.</returns>
        private async Task<byte[]> ApplyPinningAsync(byte[] requestBody, VirtualModelRunner vmr, UrlContext urlContext)
        {
            try
            {
                // Check if VMR has any model configurations
                List<string> configIds = vmr.ModelConfigurationIds;
                if (configIds == null || configIds.Count == 0) return requestBody;

                string bodyJson = Encoding.UTF8.GetString(requestBody);

                // Get pinned properties based on request type
                Dictionary<string, object> pinnedProps = null;

                foreach (string configId in configIds)
                {
                    ModelConfiguration config = await Database.ModelConfiguration.ReadAsync(vmr.TenantId, configId).ConfigureAwait(false);
                    if (config == null) continue;

                    if (urlContext.IsEmbeddingsRequest && config.PinnedEmbeddingsProperties != null && config.PinnedEmbeddingsProperties.Count > 0)
                    {
                        pinnedProps = config.PinnedEmbeddingsProperties;
                        break;
                    }
                    else if (urlContext.IsCompletionsRequest && config.PinnedCompletionsProperties != null && config.PinnedCompletionsProperties.Count > 0)
                    {
                        pinnedProps = config.PinnedCompletionsProperties;
                        break;
                    }
                }

                if (pinnedProps == null || pinnedProps.Count == 0) return requestBody;

                // Merge pinned properties into request body
                string pinnedJson = Serializer.SerializeJson(pinnedProps, false);
                string mergedJson = JsonMerger.MergeJson(bodyJson, pinnedJson);

                return Encoding.UTF8.GetBytes(mergedJson);
            }
            catch (Exception ex)
            {
                Logging.Warn(_Header + "pinning exception:" + Environment.NewLine + ex.ToString());
                return requestBody;
            }
        }

        private async Task<HttpResponseMessage> ForwardRequestAsync(
            HttpContextBase ctx,
            string targetUrl,
            ModelRunnerEndpoint endpoint,
            byte[] requestBody,
            CancellationToken cancellationToken)
        {
            Logging.Info(
                _Header + "forwarding " + ctx.Request.Method + " to " + targetUrl
                + " endpoint " + endpoint.Id
                + " (body: " + (requestBody != null ? requestBody.Length.ToString() : "null") + " bytes)");

            HttpRequestMessage request = new HttpRequestMessage();
            request.RequestUri = new Uri(targetUrl);

            // Set HTTP method
            switch (ctx.Request.Method)
            {
                case HttpMethod.GET:
                    request.Method = System.Net.Http.HttpMethod.Get;
                    break;
                case HttpMethod.POST:
                    request.Method = System.Net.Http.HttpMethod.Post;
                    break;
                case HttpMethod.PUT:
                    request.Method = System.Net.Http.HttpMethod.Put;
                    break;
                case HttpMethod.DELETE:
                    request.Method = System.Net.Http.HttpMethod.Delete;
                    break;
                default:
                    request.Method = System.Net.Http.HttpMethod.Get;
                    break;
            }

            // Set request body
            if (requestBody != null && requestBody.Length > 0)
            {
                request.Content = new ByteArrayContent(requestBody);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            }

            // Add API key if configured
            if (!String.IsNullOrEmpty(endpoint.ApiKey))
            {
                request.Headers.Add("Authorization", "Bearer " + endpoint.ApiKey);
            }

            // Forward relevant headers
            string contentType = ctx.Request.Headers.Get("Content-Type");
            if (!String.IsNullOrEmpty(contentType) && request.Content != null)
            {
                request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
            }

            // Add proxy headers
            string clientIp = ctx.Request.Source.IpAddress;
            string existingForwardedFor = ctx.Request.Headers.Get(ConductorConstants.HeaderXForwardedFor);
            if (!String.IsNullOrEmpty(existingForwardedFor))
            {
                request.Headers.TryAddWithoutValidation(ConductorConstants.HeaderXForwardedFor, existingForwardedFor + ", " + clientIp);
            }
            else
            {
                request.Headers.TryAddWithoutValidation(ConductorConstants.HeaderXForwardedFor, clientIp);
            }

            string host = ctx.Request.Headers.Get("Host");
            if (!String.IsNullOrEmpty(host))
            {
                request.Headers.TryAddWithoutValidation(ConductorConstants.HeaderXForwardedHost, host);
            }

            string proto = !String.IsNullOrEmpty(ctx.Request.Url.Scheme)
                ? ctx.Request.Url.Scheme
                : "http";
            request.Headers.TryAddWithoutValidation(ConductorConstants.HeaderXForwardedProto, proto);

            // Create a cancellation token with timeout
            using (CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(endpoint.TimeoutMs)))
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
            {
                try
                {
                    // Use ResponseHeadersRead to enable streaming - we get the response as soon as headers arrive
                    // rather than waiting for the entire response body
                    return await _HttpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        linkedCts.Token).ConfigureAwait(false);
                }
                catch
                {
                    request.Dispose();
                    throw;
                }
            }
        }

        private async Task SendProxyResponse(
            HttpContextBase ctx,
            HttpResponseMessage response,
            string vmrId,
            string endpointId,
            string modelName,
            bool sessionPinUsed,
            CancellationToken cancellationToken)
        {
            ctx.Response.StatusCode = (int)response.StatusCode;

            // Add Conductor identification headers
            if (!String.IsNullOrEmpty(vmrId))
            {
                ctx.Response.Headers.Add(ConductorConstants.HeaderVmrId, vmrId);
            }

            if (!String.IsNullOrEmpty(endpointId))
            {
                ctx.Response.Headers.Add(ConductorConstants.HeaderEndpointId, endpointId);
            }

            if (!String.IsNullOrEmpty(modelName))
            {
                ctx.Response.Headers.Add(ConductorConstants.HeaderModelName, modelName);
            }

            // Add session affinity header
            ctx.Response.Headers.Add(ConductorConstants.HeaderSessionPinned, sessionPinUsed ? "true" : "false");

            // Copy content type
            if (response.Content?.Headers?.ContentType != null)
            {
                ctx.Response.ContentType = response.Content.Headers.ContentType.ToString();
            }

            // Determine if this is a streaming response
            bool isStreaming = IsStreamingResponse(response);

            if (isStreaming)
            {
                await SendStreamingResponseAsync(ctx, response, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Standard response - read entire body and send
                byte[] responseBody = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                await ctx.Response.Send(responseBody).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Determines if the response should be streamed based on content type and transfer encoding.
        /// </summary>
        /// <param name="response">The HTTP response message.</param>
        /// <returns>True if the response should be streamed, false otherwise.</returns>
        private bool IsStreamingResponse(HttpResponseMessage response)
        {
            // Check for Server-Sent Events content type
            string contentType = response.Content?.Headers?.ContentType?.MediaType;
            if (!String.IsNullOrEmpty(contentType))
            {
                if (contentType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Check for chunked transfer encoding
            if (response.Headers.TransferEncodingChunked == true)
            {
                return true;
            }

            // Check for streaming indicators in Content-Type (e.g., application/x-ndjson for streaming JSON)
            if (!String.IsNullOrEmpty(contentType))
            {
                if (contentType.Equals("application/x-ndjson", StringComparison.OrdinalIgnoreCase) ||
                    contentType.Equals("application/stream+json", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Streams the response body to the client chunk by chunk.
        /// Supports SSE (Server-Sent Events), chunked transfer encoding, and other streaming formats.
        /// </summary>
        /// <param name="ctx">The HTTP context.</param>
        /// <param name="response">The upstream HTTP response.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task SendStreamingResponseAsync(HttpContextBase ctx, HttpResponseMessage response, CancellationToken cancellationToken)
        {
            // For SSE, set appropriate headers
            string contentType = response.Content?.Headers?.ContentType?.MediaType;
            if (!String.IsNullOrEmpty(contentType) && contentType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
            {
                // Ensure Cache-Control is set for SSE
                ctx.Response.Headers.Add("Cache-Control", "no-cache");
                ctx.Response.Headers.Add("Connection", "keep-alive");
            }

            // Use chunked transfer encoding for the client response
            ctx.Response.ChunkedTransfer = true;

            // Get the response stream for reading
            using (Stream responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                byte[] buffer = new byte[StreamingBufferSize];
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Send this chunk to the client
                    byte[] chunk = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);

                    // SendChunk(data, isFinal, cancellationToken) - send non-final chunk
                    bool success = await ctx.Response.SendChunk(chunk, false, cancellationToken).ConfigureAwait(false);
                    if (!success)
                    {
                        // Client disconnected or write failed
                        Logging.Debug(_Header + "client disconnected during streaming");
                        break;
                    }
                }

                // Send final empty chunk to signal end of stream
                await ctx.Response.SendChunk(Array.Empty<byte>(), true, cancellationToken).ConfigureAwait(false);
            }
        }

        private string DeriveClientKey(HttpContextBase ctx, Conductor.Core.Enums.SessionAffinityModeEnum mode, string headerName)
        {
            switch (mode)
            {
                case Conductor.Core.Enums.SessionAffinityModeEnum.SourceIP:
                    string forwardedFor = ctx.Request.Headers.Get(ConductorConstants.HeaderXForwardedFor);
                    if (!String.IsNullOrEmpty(forwardedFor))
                    {
                        // Use the leftmost (original client) IP
                        string firstIp = forwardedFor.Split(',')[0].Trim();
                        if (!String.IsNullOrEmpty(firstIp)) return firstIp;
                    }
                    return ctx.Request.Source.IpAddress;

                case Conductor.Core.Enums.SessionAffinityModeEnum.ApiKey:
                    string authHeader = ctx.Request.Headers.Get("Authorization");
                    if (!String.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        string token = authHeader.Substring(7).Trim();
                        if (!String.IsNullOrEmpty(token)) return token;
                    }
                    return null;

                case Conductor.Core.Enums.SessionAffinityModeEnum.Header:
                    if (String.IsNullOrEmpty(headerName)) return null;
                    string headerValue = ctx.Request.Headers.Get(headerName);
                    if (!String.IsNullOrEmpty(headerValue)) return headerValue;
                    return null;

                case Conductor.Core.Enums.SessionAffinityModeEnum.None:
                default:
                    return null;
            }
        }

        private async Task SendErrorResponse(HttpContextBase ctx, ApiErrorResponse error)
        {
            ctx.Response.StatusCode = error.StatusCode;
            ctx.Response.ContentType = "application/json";
            string json = Serializer.SerializeJson(error, true);
            await ctx.Response.Send(json);
        }

        private async Task SendBadGateway(HttpContextBase ctx, string message)
        {
            await SendErrorResponse(ctx, ApiErrorResponse.BadGateway(message));
        }

        private async Task SendForbidden(HttpContextBase ctx, string message)
        {
            await SendErrorResponse(ctx, ApiErrorResponse.Forbidden(message));
        }

        private async Task SendNotFound(HttpContextBase ctx)
        {
            await SendErrorResponse(ctx, ApiErrorResponse.NotFound());
        }

        private async Task SendTooManyRequests(HttpContextBase ctx, string message)
        {
            await SendErrorResponse(ctx, ApiErrorResponse.TooManyRequests(message));
        }

        private async Task SendUnauthorized(HttpContextBase ctx, string message)
        {
            await SendErrorResponse(ctx, ApiErrorResponse.Unauthorized(message));
        }
    }
}
