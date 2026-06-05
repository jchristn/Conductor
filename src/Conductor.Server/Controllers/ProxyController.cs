namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
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
    using SyslogLogging;
    using WatsonWebserver.Core;

    using HttpMethod = WatsonWebserver.Core.HttpMethod;
    /// <summary>
    /// Proxy controller for virtual model runner requests.
    /// </summary>
    public class ProxyController : BaseController
    {
        private static readonly string _Header = "[ProxyController] ";
        private readonly HealthCheckService _HealthCheckService;
        private readonly RequestHistoryService _RequestHistoryService;
        private readonly RoutingDecisionService _RoutingDecisionService;
        private readonly OperationalMetricsService _Metrics;

        /// <summary>
        /// Buffer size for streaming responses. Default is 8KB.
        /// </summary>
        public int StreamingBufferSize { get; set; } = 8192;

        /// <summary>
        /// Instantiate the proxy controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="healthCheckService">Health check service (optional).</param>
        /// <param name="sessionAffinityService">Session affinity service (optional).</param>
        /// <param name="requestHistoryService">Request history service (optional).</param>
        /// <param name="routingDecisionService">Shared routing-decision service (optional).</param>
        /// <param name="metrics">Operational metrics service (optional).</param>
        public ProxyController(
            DatabaseDriverBase database,
            AuthenticationService authService,
            Serializer serializer,
            LoggingModule logging,
            HealthCheckService healthCheckService = null,
            SessionAffinityService sessionAffinityService = null,
            RequestHistoryService requestHistoryService = null,
            RoutingDecisionService routingDecisionService = null,
            OperationalMetricsService metrics = null)
            : base(database, authService, serializer, logging)
        {
            _HealthCheckService = healthCheckService;
            _RequestHistoryService = requestHistoryService;
            _RoutingDecisionService = routingDecisionService;
            _Metrics = metrics;
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
            Stopwatch stopwatch = Stopwatch.StartNew();
            RequestHistoryDetail historyDetail = null;
            RoutingExecutionResult routingResult = null;
            ModelRunnerEndpoint endpoint = null;
            VirtualModelRunner vmr = null;
            bool incrementedInFlight = false;
            RequestAnalyticsCapture analyticsCapture = new RequestAnalyticsCapture();

            try
            {
                UrlContext urlContext = UrlContext.Parse(ctx.Request.Url.RawWithQuery, ctx.Request.Method.ToString());
                if (urlContext == null || String.IsNullOrEmpty(urlContext.BasePath))
                {
                    await SendNotFound(ctx);
                    return;
                }

                vmr = await Database.VirtualModelRunner.ReadByBasePathAsync(urlContext.BasePath);
                if (vmr == null || !vmr.Active)
                {
                    await SendNotFound(ctx);
                    return;
                }

                PopulateRequestContext(ctx, requestContext, urlContext, vmr);
                analyticsCapture.RequestBytes = requestContext?.Data?.LongLength;

                if (_RequestHistoryService != null && _RequestHistoryService.IsEnabled && vmr.RequestHistoryEnabled)
                {
                    Dictionary<string, string> requestHeaders = GetRequestHeaders(ctx);
                    string requestBody = requestContext.Data != null ? Encoding.UTF8.GetString(requestContext.Data) : null;

                    historyDetail = await _RequestHistoryService.CreateEntryAsync(
                        vmr,
                        requestContext,
                        ctx.Request.Url.RawWithoutQuery,
                        requestHeaders,
                        requestBody,
                        cancellationToken).ConfigureAwait(false);
                }

                routingResult = await _RoutingDecisionService.EvaluateAsync(vmr, urlContext, requestContext, true, cancellationToken).ConfigureAwait(false);
                analyticsCapture.RoutingDurationMs = (int)stopwatch.ElapsedMilliseconds;
                endpoint = routingResult.Endpoint;

                if (routingResult.Decision == null || !routingResult.Decision.Success || endpoint == null)
                {
                    analyticsCapture.ErrorType = routingResult?.Decision?.DenialReasonCode ?? "RoutingDenied";
                    analyticsCapture.ErrorMessage = routingResult?.Decision?.DenialReason;
                    await SendRoutingDecisionResponse(ctx, routingResult?.Decision).ConfigureAwait(false);
                    if (historyDetail != null && _RequestHistoryService != null)
                    {
                        await _RequestHistoryService.UpdateWithResponseAsync(
                            historyDetail,
                            routingResult?.Decision,
                            null,
                            routingResult?.ModelDefinition,
                            routingResult?.ModelConfiguration,
                            routingResult?.Decision?.HttpStatusCode ?? 502,
                            null,
                            null,
                            stopwatch,
                            cancellationToken,
                            null,
                            analyticsCapture).ConfigureAwait(false);
                    }
                    return;
                }

                if (_HealthCheckService != null)
                {
                    int limiterStartMs = (int)stopwatch.ElapsedMilliseconds;
                    incrementedInFlight = _HealthCheckService.TryIncrementInFlight(endpoint.Id, endpoint.MaxParallelRequests);
                    analyticsCapture.EndpointLimiterWaitMs = Math.Max(0, (int)stopwatch.ElapsedMilliseconds - limiterStartMs);
                    if (!incrementedInFlight)
                    {
                        routingResult.Decision.Success = false;
                        routingResult.Decision.OutcomeCode = "Denied";
                        routingResult.Decision.HttpStatusCode = 429;
                        routingResult.Decision.DenialReasonCode = "EndpointAtCapacity";
                        routingResult.Decision.DenialReason = "The selected endpoint reached capacity before the request was admitted.";
                        routingResult.Decision.Message = routingResult.Decision.DenialReason;
                        analyticsCapture.ErrorType = routingResult.Decision.DenialReasonCode;
                        analyticsCapture.ErrorMessage = routingResult.Decision.DenialReason;
                        await SendRoutingDecisionResponse(ctx, routingResult.Decision).ConfigureAwait(false);
                        if (historyDetail != null && _RequestHistoryService != null)
                        {
                            await _RequestHistoryService.UpdateWithResponseAsync(
                                historyDetail,
                                routingResult.Decision,
                                endpoint,
                                routingResult.ModelDefinition,
                                routingResult.ModelConfiguration,
                                429,
                                null,
                                null,
                                stopwatch,
                                cancellationToken,
                                null,
                                analyticsCapture).ConfigureAwait(false);
                        }
                        return;
                    }
                }

                string targetUrl = BuildTargetUrl(endpoint, routingResult.UrlContext);
                analyticsCapture.UpstreamStartOffsetMs = (int)stopwatch.ElapsedMilliseconds;
                using (HttpResponseMessage response = await ForwardRequestAsync(
                    ctx,
                    targetUrl,
                    vmr,
                    endpoint,
                    routingResult.RequestBody,
                    cancellationToken).ConfigureAwait(false))
                {
                    analyticsCapture.UpstreamHeadersOffsetMs = (int)stopwatch.ElapsedMilliseconds;
                    await SendProxyResponse(
                        ctx,
                        response,
                        vmr,
                        routingResult,
                        requestContext,
                        historyDetail,
                        stopwatch,
                        analyticsCapture,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logging.Warn(_Header + "proxy exception:" + Environment.NewLine + ex.ToString());
                analyticsCapture.ErrorType = ex.GetType().Name;
                analyticsCapture.ErrorMessage = ex.Message;
                await SendBadGateway(ctx, "Proxy error: " + ex.Message);

                if (historyDetail != null && _RequestHistoryService != null)
                {
                    await _RequestHistoryService.UpdateWithResponseAsync(
                        historyDetail,
                        routingResult?.Decision,
                        endpoint,
                        routingResult?.ModelDefinition,
                        routingResult?.ModelConfiguration,
                        502,
                        null,
                        "Proxy error: " + ex.Message,
                        stopwatch,
                        cancellationToken,
                        null,
                        analyticsCapture).ConfigureAwait(false);
                }
            }
            finally
            {
                if (incrementedInFlight && _HealthCheckService != null && endpoint != null)
                {
                    _HealthCheckService.DecrementInFlight(endpoint.Id);
                }
            }
        }

        private string BuildTargetUrl(ModelRunnerEndpoint endpoint, UrlContext urlContext)
        {
            string scheme = endpoint.UseSsl ? "https" : "http";
            string baseUrl = scheme + "://" + endpoint.Hostname + ":" + endpoint.Port;

            // For Gemini endpoints, replace the ?key= query parameter (which holds the
            // Conductor credential) with the endpoint's own upstream API key.
            string endpointApiKey = endpoint.ApiType == ApiTypeEnum.Gemini ? endpoint.ApiKey : null;
            return urlContext.BuildTargetUrl(baseUrl, endpointApiKey);
        }

        private async Task<HttpResponseMessage> ForwardRequestAsync(
            HttpContextBase ctx,
            string targetUrl,
            VirtualModelRunner vmr,
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
                if (endpoint.ApiType == ApiTypeEnum.Gemini)
                {
                    request.Headers.Add("x-goog-api-key", endpoint.ApiKey);
                }
                else
                {
                    request.Headers.Add("Authorization", "Bearer " + endpoint.ApiKey);
                }
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

            int effectiveTimeoutMs = endpoint.TimeoutMs;
            if (vmr != null && vmr.TimeoutMs > 0)
            {
                effectiveTimeoutMs = Math.Min(vmr.TimeoutMs, endpoint.TimeoutMs);
            }

            HttpClient httpClient = ProxyHttpClientCache.GetHttpClient(endpoint);

            // Create a cancellation token with timeout
            using (CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(effectiveTimeoutMs)))
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
            {
                try
                {
                    // Use ResponseHeadersRead to enable streaming - we get the response as soon as headers arrive
                    // rather than waiting for the entire response body
                    return await httpClient.SendAsync(
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
            VirtualModelRunner vmr,
            RoutingExecutionResult routingResult,
            RequestContext requestContext,
            RequestHistoryDetail historyDetail,
            Stopwatch stopwatch,
            RequestAnalyticsCapture analyticsCapture,
            CancellationToken cancellationToken)
        {
            RoutingDecision decision = routingResult?.Decision ?? new RoutingDecision();
            ModelRunnerEndpoint endpoint = routingResult?.Endpoint;
            ModelDefinition modelDefinition = routingResult?.ModelDefinition;
            ModelConfiguration modelConfiguration = routingResult?.ModelConfiguration;

            ctx.Response.StatusCode = (int)response.StatusCode;

            // Add Conductor identification headers
            if (!String.IsNullOrEmpty(vmr?.Id))
            {
                ctx.Response.Headers.Add(ConductorConstants.HeaderVmrId, vmr.Id);
            }

            if (!String.IsNullOrEmpty(endpoint?.Id))
            {
                ctx.Response.Headers.Add(ConductorConstants.HeaderEndpointId, endpoint.Id);
            }

            if (!String.IsNullOrEmpty(routingResult?.EffectiveModel))
            {
                ctx.Response.Headers.Add(ConductorConstants.HeaderModelName, routingResult.EffectiveModel);
            }

            // Add session affinity header
            ctx.Response.Headers.Add(ConductorConstants.HeaderSessionPinned, decision.SessionPinUsed ? "true" : "false");

            // Copy content type
            if (response.Content?.Headers?.ContentType != null)
            {
                ctx.Response.ContentType = response.Content.Headers.ContentType.ToString();
            }

            // Determine if this is a streaming response
            bool isStreaming = IsStreamingResponse(response);
            string responseBodyString = null;
            int? firstTokenTimeMs = null;

            // Set transfer type indicators on history detail
            if (historyDetail != null)
            {
                // Request transfer type
                if (requestContext != null && requestContext.IsChunkedRequest)
                {
                    historyDetail.RequestTransferType = TransferTypeEnum.Chunked;
                }

                // Response transfer type
                string responseContentType = response.Content?.Headers?.ContentType?.MediaType;
                if (!String.IsNullOrEmpty(responseContentType) &&
                    responseContentType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
                {
                    historyDetail.ResponseTransferType = TransferTypeEnum.ServerSentEvents;
                }
                else if (response.Headers.TransferEncodingChunked == true ||
                    (!String.IsNullOrEmpty(responseContentType) &&
                     (responseContentType.Equals("application/x-ndjson", StringComparison.OrdinalIgnoreCase) ||
                      responseContentType.Equals("application/stream+json", StringComparison.OrdinalIgnoreCase))))
                {
                    historyDetail.ResponseTransferType = TransferTypeEnum.Chunked;
                }
            }

            if (isStreaming)
            {
                if (analyticsCapture != null)
                {
                    analyticsCapture.IsStreaming = true;
                }

                // Capture streaming body when request history is enabled
                StringBuilder streamBody = null;
                int maxCaptureBytes = 0;
                if (historyDetail != null && _RequestHistoryService != null)
                {
                    maxCaptureBytes = _RequestHistoryService.MaxResponseBodyBytes;
                    streamBody = new StringBuilder(Math.Min(maxCaptureBytes, 65536));
                }

                firstTokenTimeMs = await SendStreamingResponseAsync(ctx, response, maxCaptureBytes, streamBody, stopwatch, analyticsCapture, cancellationToken).ConfigureAwait(false);
                responseBodyString = streamBody != null ? streamBody.ToString() : null;
            }
            else
            {
                // Standard response - read entire body and send
                byte[] responseBody = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (analyticsCapture != null)
                {
                    analyticsCapture.IsStreaming = false;
                    analyticsCapture.ResponseBytes = responseBody?.LongLength;
                }
                await ctx.Response.Send(responseBody).ConfigureAwait(false);
                responseBodyString = responseBody != null ? Encoding.UTF8.GetString(responseBody) : null;
            }

            // Update request history with response
            if (historyDetail != null && _RequestHistoryService != null)
            {
                Dictionary<string, string> responseHeaders = GetResponseHeaders(response);

                await _RequestHistoryService.UpdateWithResponseAsync(
                    historyDetail,
                    decision,
                    endpoint,
                    modelDefinition,
                    modelConfiguration,
                    (int)response.StatusCode,
                    responseHeaders,
                    responseBodyString,
                    stopwatch,
                    cancellationToken,
                    firstTokenTimeMs,
                    analyticsCapture).ConfigureAwait(false);
            }

            if (_Metrics != null && vmr != null)
            {
                _Metrics.RecordRequestCompletion(
                    vmr.TenantId,
                    vmr.Id,
                    vmr.Name,
                    GetApiFamily(requestContext.RequestType),
                    stopwatch.Elapsed.TotalMilliseconds,
                    firstTokenTimeMs);
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
        /// <param name="maxCaptureBytes">Maximum number of bytes to capture for request history. Zero disables capture.</param>
        /// <param name="capturedBody">StringBuilder to accumulate the captured body text. May be null to skip capture.</param>
        /// <param name="stopwatch">Stopwatch started at request beginning.</param>
        /// <param name="analyticsCapture">Captured proxy-stage timings and transfer sizes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task<int?> SendStreamingResponseAsync(
            HttpContextBase ctx,
            HttpResponseMessage response,
            int maxCaptureBytes,
            StringBuilder capturedBody,
            Stopwatch stopwatch,
            RequestAnalyticsCapture analyticsCapture,
            CancellationToken cancellationToken)
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
                int? firstTokenTimeMs = null;
                long totalBytes = 0;
                int chunkCount = 0;

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    totalBytes += bytesRead;
                    chunkCount++;

                    if (!firstTokenTimeMs.HasValue)
                    {
                        firstTokenTimeMs = (int)stopwatch.ElapsedMilliseconds;
                    }

                    // Send this chunk to the client
                    byte[] chunk = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);

                    // Capture streaming body for request history
                    if (capturedBody != null && capturedBody.Length < maxCaptureBytes)
                    {
                        string chunkText = Encoding.UTF8.GetString(chunk);
                        int remaining = maxCaptureBytes - capturedBody.Length;
                        capturedBody.Append(remaining >= chunkText.Length
                            ? chunkText
                            : chunkText.Substring(0, remaining));
                    }

                    // SendChunk(data, isFinal, cancellationToken) - send non-final chunk
                    bool success = await ctx.Response.SendChunk(chunk, false, cancellationToken).ConfigureAwait(false);
                    if (!success)
                    {
                        // Client disconnected or write failed
                        Logging.Debug(_Header + "client disconnected during streaming");
                        break;
                    }
                }

                if (analyticsCapture != null)
                {
                    analyticsCapture.ResponseBytes = totalBytes;
                    analyticsCapture.StreamingChunkCount = chunkCount;
                }

                // Send final empty chunk to signal end of stream
                await ctx.Response.SendChunk(Array.Empty<byte>(), true, cancellationToken).ConfigureAwait(false);

                return firstTokenTimeMs ?? (int)stopwatch.ElapsedMilliseconds;
            }
        }

        private async Task SendErrorResponse(HttpContextBase ctx, Conductor.Core.Models.ApiErrorResponse error)
        {
            ctx.Response.StatusCode = error.StatusCode;
            ctx.Response.ContentType = "application/json";
            string json = Serializer.SerializeJson(error, true);
            await ctx.Response.Send(json);
        }

        private async Task SendBadGateway(HttpContextBase ctx, string message)
        {
            await SendErrorResponse(ctx, Conductor.Core.Models.ApiErrorResponse.BadGateway(message));
        }

        private async Task SendForbidden(HttpContextBase ctx, string message)
        {
            await SendErrorResponse(ctx, Conductor.Core.Models.ApiErrorResponse.Forbidden(message));
        }

        private async Task SendNotFound(HttpContextBase ctx)
        {
            await SendErrorResponse(ctx, Conductor.Core.Models.ApiErrorResponse.NotFound());
        }

        private async Task SendTooManyRequests(HttpContextBase ctx, string message)
        {
            await SendErrorResponse(ctx, Conductor.Core.Models.ApiErrorResponse.TooManyRequests(message));
        }

        private async Task SendUnauthorized(HttpContextBase ctx, string message)
        {
            await SendErrorResponse(ctx, Conductor.Core.Models.ApiErrorResponse.Unauthorized(message));
        }

        private Dictionary<string, string> GetRequestHeaders(HttpContextBase ctx)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            try
            {
                if (ctx.Request.Headers != null && ctx.Request.Headers.Count > 0)
                {
                    foreach (string key in ctx.Request.Headers.AllKeys)
                    {
                        if (!String.IsNullOrEmpty(key))
                        {
                            headers[key] = ctx.Request.Headers.Get(key);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Warn(_Header + "failed to extract request headers: " + ex.Message);
            }
            return headers;
        }

        private void PopulateRequestContext(HttpContextBase ctx, RequestContext requestContext, UrlContext urlContext, VirtualModelRunner vmr)
        {
            string rawUrl = ctx.Request.Url.RawWithQuery;
            int queryStart = String.IsNullOrEmpty(rawUrl) ? -1 : rawUrl.IndexOf('?');

            requestContext.HttpMethod = ctx.Request.Method.ToString();
            requestContext.OriginalUrl = rawUrl;
            requestContext.Path = ctx.Request.Url.RawWithoutQuery;
            requestContext.QueryString = queryStart >= 0 ? rawUrl.Substring(queryStart) : null;
            requestContext.ClientIpAddress = ctx.Request.Source.IpAddress;
            requestContext.Headers = GetRequestHeaders(ctx);
            requestContext.ContentType = ctx.Request.ContentType;
            requestContext.ContentLength = requestContext.Data?.LongLength ?? 0;
            requestContext.ModelName = urlContext.RequestedModel;
            requestContext.RequestType = urlContext.RequestType;
            requestContext.ApiType = urlContext.ApiType;
            requestContext.TenantId = vmr?.TenantId;
            requestContext.VirtualModelRunnerId = vmr?.Id;

            if (ctx.Metadata is AuthenticationResult authResult)
            {
                requestContext.UserId = authResult.User?.Id;
                requestContext.UserEmail = authResult.User?.Email;
                requestContext.CredentialId = authResult.Credential?.Id;
                requestContext.CredentialName = authResult.Credential?.Name;
                requestContext.TenantId = authResult.Tenant?.Id ?? requestContext.TenantId;
            }
        }

        private async Task SendRoutingDecisionResponse(HttpContextBase ctx, RoutingDecision decision)
        {
            if (decision == null)
            {
                await SendBadGateway(ctx, "Conductor could not evaluate the request.");
                return;
            }

            switch (decision.HttpStatusCode)
            {
                case 401:
                    await SendUnauthorized(ctx, decision.Message ?? decision.DenialReason ?? "Unauthorized");
                    break;
                case 403:
                    await SendForbidden(ctx, decision.Message ?? decision.DenialReason ?? "Forbidden");
                    break;
                case 404:
                    await SendNotFound(ctx);
                    break;
                case 429:
                    await SendTooManyRequests(ctx, decision.Message ?? decision.DenialReason ?? "All endpoints at capacity");
                    break;
                case 503:
                    await SendErrorResponse(ctx, Conductor.Core.Models.ApiErrorResponse.ServiceUnavailable(decision.Message ?? decision.DenialReason ?? "Service unavailable"));
                    break;
                default:
                    await SendBadGateway(ctx, decision.Message ?? decision.DenialReason ?? "Routing denied.");
                    break;
            }
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

        private Dictionary<string, string> GetResponseHeaders(HttpResponseMessage response)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            try
            {
                if (response.Headers != null)
                {
                    foreach (System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in response.Headers)
                    {
                        headers[header.Key] = String.Join(", ", header.Value);
                    }
                }
                if (response.Content?.Headers != null)
                {
                    foreach (System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in response.Content.Headers)
                    {
                        headers[header.Key] = String.Join(", ", header.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Warn(_Header + "failed to extract response headers: " + ex.Message);
            }
            return headers;
        }
    }
}
