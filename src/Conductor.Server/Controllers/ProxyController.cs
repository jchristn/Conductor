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

        /// <summary>
        /// Buffer size for streaming responses. Default is 8KB.
        /// </summary>
        public int StreamingBufferSize { get; set; } = 8192;

        /// <summary>
        /// Instantiate the proxy controller.
        /// </summary>
        public ProxyController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging, HealthCheckService healthCheckService = null)
            : base(database, authService, serializer, logging)
        {
            _HealthCheckService = healthCheckService;
        }

        /// <summary>
        /// Handle incoming proxy request.
        /// Supports standard HTTP responses, chunked transfer encoding, and Server-Sent Events (SSE).
        /// </summary>
        /// <param name="ctx">The HTTP context.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
        public async Task HandleRequest(HttpContextBase ctx, CancellationToken cancellationToken = default)
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

                // Get all endpoint IDs
                List<string> endpointIds = vmr.ModelRunnerEndpointIds;
                if (endpointIds == null || endpointIds.Count == 0)
                {
                    await SendBadGateway(ctx, "No model runners configured");
                    return;
                }

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
                ModelRunnerEndpoint endpoint = SelectEndpointWithWeight(availableEndpoints, vmr.LoadBalancingMode);

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

                    // Read and optionally modify request body
                    byte[] requestBody = null;
                    if (ctx.Request.Data != null)
                    {
                        requestBody = await ReadStreamToEndAsync(ctx.Request.Data);

                        // Apply model definition and configuration logic
                        if (requestBody != null && requestBody.Length > 0)
                        {
                            (bool success, byte[] modifiedBody) = await ApplyModelDefinitionAndConfigurationAsync(
                                requestBody, vmr, urlContext, ctx).ConfigureAwait(false);

                            if (!success)
                            {
                                // Error response already sent by the method
                                return;
                            }

                            requestBody = modifiedBody;
                        }

                        // Apply pinning if this is an embeddings or completions request
                        if (requestBody != null && requestBody.Length > 0 && vmr.ModelConfigurationIds != null && vmr.ModelConfigurationIds.Count > 0)
                        {
                            requestBody = await ApplyPinningAsync(requestBody, vmr, urlContext).ConfigureAwait(false);
                        }
                    }

                    // Forward the request and send response back
                    // Use using to ensure HttpResponseMessage is disposed after streaming completes
                    using (HttpResponseMessage response = await ForwardRequestAsync(ctx, targetUrl, endpoint, requestBody, cancellationToken).ConfigureAwait(false))
                    {
                        await SendProxyResponse(ctx, response, cancellationToken).ConfigureAwait(false);
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
        /// <returns>
        /// A tuple containing:
        /// - Success: true if processing succeeded, false if an error response was sent
        /// - Body: the modified request body (or original if no changes), null if error
        /// </returns>
        private async Task<(bool Success, byte[] Body)> ApplyModelDefinitionAndConfigurationAsync(
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
                    return (true, requestBody);
                }

                if (requestBody == null || requestBody.Length == 0)
                {
                    return (true, requestBody);
                }

                string bodyJson = Encoding.UTF8.GetString(requestBody);
                Dictionary<string, object> bodyDict = Serializer.DeserializeJson<Dictionary<string, object>>(bodyJson);
                if (bodyDict == null)
                {
                    return (true, requestBody);
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
                        return (false, null);
                    }

                    if (String.IsNullOrEmpty(requestedModel))
                    {
                        // No model specified in request, reject in strict mode
                        Logging.Warn(_Header + "strict mode: no model specified in request");
                        await SendUnauthorized(ctx, "Model not specified").ConfigureAwait(false);
                        return (false, null);
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
                        return (false, null);
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
                        return (false, null);
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
                        return (false, null);
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
                return (true, Encoding.UTF8.GetBytes(modifiedJson));
            }
            catch (Exception ex)
            {
                Logging.Warn(_Header + "model definition/configuration exception:" + Environment.NewLine + ex.ToString());
                return (true, requestBody);
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

        private async Task SendProxyResponse(HttpContextBase ctx, HttpResponseMessage response, CancellationToken cancellationToken)
        {
            ctx.Response.StatusCode = (int)response.StatusCode;

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
