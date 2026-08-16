namespace Conductor.Sdk.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    /// <summary>
    /// Tests for the core plumbing of <see cref="ConductorClient"/>: constructor guards,
    /// authentication modes, base-URL composition, query-string handling, HttpClient
    /// ownership/disposal, error surfacing, and the routes not exercised by the analytics suite.
    /// </summary>
    public class ConductorClientCoreTests
    {
        #region Constructor-Guard-Tests
        [Fact]
        public void Constructor_WithNullBaseUrl_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new ConductorClient(null));
            Assert.Equal("baseUrl", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithWhitespaceBaseUrl_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new ConductorClient("   "));
            Assert.Equal("baseUrl", exception.ParamName);
        }
        #endregion

        #region Authentication-Tests
        [Fact]
        public async Task BearerToken_SetsAuthorizationHeader()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", bearerToken: "abc", httpClient: httpClient);

            using JsonDocument response = await client.GetAnalyticsCatalogAsync();

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Equal("Bearer", request.AuthorizationScheme);
            Assert.Equal("abc", request.AuthorizationParameter);
            Assert.False(request.Headers.ContainsKey("x-admin-email"));
        }

        [Fact]
        public async Task AdminCredentials_SetAdminHeadersInsteadOfBearer()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient(
                "https://conductor.local",
                adminEmail: "admin@conductor.local",
                adminPassword: "hunter2",
                httpClient: httpClient);

            using JsonDocument response = await client.GetAnalyticsCatalogAsync();

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Null(request.AuthorizationScheme);
            Assert.Equal("admin@conductor.local", request.Headers["x-admin-email"]);
            Assert.Equal("hunter2", request.Headers["x-admin-password"]);
        }

        [Fact]
        public async Task AdminCredentials_TakePrecedenceOverBearerToken()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient(
                "https://conductor.local",
                bearerToken: "should-be-ignored",
                adminEmail: "admin@conductor.local",
                adminPassword: "hunter2",
                httpClient: httpClient);

            using JsonDocument response = await client.GetAnalyticsCatalogAsync();

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Null(request.AuthorizationScheme);
            Assert.True(request.Headers.ContainsKey("x-admin-email"));
        }

        [Fact]
        public async Task NoCredentials_SendsNoAuthenticationHeaders()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            using JsonDocument response = await client.GetAnalyticsCatalogAsync();

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Null(request.AuthorizationScheme);
            Assert.False(request.Headers.ContainsKey("x-admin-email"));
        }
        #endregion

        #region BaseUrl-And-QueryString-Tests
        [Fact]
        public async Task BaseUrl_WithTrailingSlash_ComposesSingleSlashPaths()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local/", httpClient: httpClient);

            using JsonDocument response = await client.GetAnalyticsCatalogAsync();

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Equal("https://conductor.local/v1.0/analytics/catalog", request.Uri.ToString());
        }

        [Fact]
        public async Task QueryString_OmitsNullAndEmptyValues()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            // tenantId is null, so it must not appear in the query string.
            using JsonDocument response = await client.GetEndpointGroupAsync("egp_1", tenantId: null);

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Equal("https://conductor.local/v1.0/endpointgroups/egp_1", request.Uri.ToString());
        }

        [Fact]
        public async Task Ids_AreUrlEscapedInPaths()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            using JsonDocument response = await client.GetEndpointGroupAsync("egp/child");

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Equal("https://conductor.local/v1.0/endpointgroups/egp%2Fchild", request.Uri.ToString());
        }
        #endregion

        #region HttpClient-Ownership-Tests
        [Fact]
        public async Task Dispose_WithExternalHttpClient_DoesNotDisposeIt()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            client.Dispose();

            // The externally supplied HttpClient must still be usable after the client is disposed.
            using HttpResponseMessage response = await httpClient.GetAsync("https://conductor.local/v1.0/analytics/catalog");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            using HttpClient httpClient = new HttpClient(new RecordingHandler(_ => JsonResponse("{}")));
            ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            client.Dispose();

            // A second dispose must be safe (no exception); the test fails if it throws.
            client.Dispose();
        }
        #endregion

        #region Uncovered-Route-Tests
        [Fact]
        public async Task ValidateVirtualModelRunnerAsync_PostsToValidateWithExistingId()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            using JsonDocument response = await client.ValidateVirtualModelRunnerAsync(new { Name = "Route A" }, existingId: "vmr_1");

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://conductor.local/v1.0/virtualmodelrunners/validate?existingId=vmr_1", request.Uri.ToString());
            Assert.Contains("\"Name\":\"Route A\"", request.Body);
        }

        [Fact]
        public async Task VirtualModelRunnerConfigAndRoutingMethods_UseExpectedRoutes()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            using JsonDocument effective = await client.GetVirtualModelRunnerEffectiveConfigurationAsync("vmr_1", "ten_1");
            using JsonDocument load = await client.LoadVirtualModelRunnerModelAsync("vmr_1", new { model = "llama3.1" }, "ten_1");
            using JsonDocument explain = await client.ExplainVirtualModelRunnerRoutingAsync("vmr_1", new { prompt = "hi" });

            Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
            Assert.Equal("https://conductor.local/v1.0/virtualmodelrunners/vmr_1/effective?tenantId=ten_1", handler.Requests[0].Uri.ToString());
            Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
            Assert.Equal("https://conductor.local/v1.0/virtualmodelrunners/vmr_1/load-model?tenantId=ten_1", handler.Requests[1].Uri.ToString());
            Assert.Contains("\"model\":\"llama3.1\"", handler.Requests[1].Body);
            Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
            Assert.Equal("https://conductor.local/v1.0/virtualmodelrunners/vmr_1/explain-routing", handler.Requests[2].Uri.ToString());
        }

        [Fact]
        public async Task AnalyticsReadMethods_UseExpectedRoutes()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            Dictionary<string, string> range = new Dictionary<string, string> { ["range"] = "lastDay" };

            using JsonDocument timeSeries = await client.GetAnalyticsTimeSeriesAsync(range);
            using JsonDocument ttft = await client.GetAnalyticsTtftAsync(range);
            using JsonDocument tokens = await client.GetAnalyticsTokensAsync(range);
            using JsonDocument costs = await client.GetAnalyticsCostsAsync(range);
            using JsonDocument users = await client.GetAnalyticsUsersAsync(range);
            using JsonDocument access = await client.GetAnalyticsAccessAsync(range);

            string[] expected = new[]
            {
                "https://conductor.local/v1.0/analytics/timeseries?range=lastDay",
                "https://conductor.local/v1.0/analytics/ttft?range=lastDay",
                "https://conductor.local/v1.0/analytics/tokens?range=lastDay",
                "https://conductor.local/v1.0/analytics/costs?range=lastDay",
                "https://conductor.local/v1.0/analytics/users?range=lastDay",
                "https://conductor.local/v1.0/analytics/access?range=lastDay"
            };

            Assert.Equal(expected, handler.Requests.Select(r => r.Uri.ToString()).ToArray());
            Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        }
        #endregion

        #region Error-Handling-Tests
        [Fact]
        public async Task DeleteAsync_OnApiError_ThrowsConductorApiException()
        {
            RecordingHandler handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"missing\"}", Encoding.UTF8, "application/json")
            });
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            ConductorApiException exception = await Assert.ThrowsAsync<ConductorApiException>(
                async () => await client.DeleteEndpointGroupAsync("egp_1", "ten_1"));

            Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
            Assert.Equal("/v1.0/endpointgroups/egp_1?tenantId=ten_1", exception.Endpoint);
            Assert.Contains("missing", exception.ResponseBody);
        }
        #endregion

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _Responder;

            public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _Responder = responder;
            }

            public List<RecordedRequest> Requests { get; } = new List<RecordedRequest>();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string body = request.Content == null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
                {
                    headers[header.Key] = String.Join(",", header.Value);
                }

                Requests.Add(new RecordedRequest
                {
                    Method = request.Method,
                    Uri = request.RequestUri,
                    Body = body,
                    AuthorizationScheme = request.Headers.Authorization?.Scheme,
                    AuthorizationParameter = request.Headers.Authorization?.Parameter,
                    Headers = headers
                });

                return _Responder(request);
            }
        }

        private sealed class RecordedRequest
        {
            public HttpMethod Method { get; set; }

            public Uri Uri { get; set; }

            public string Body { get; set; }

            public string AuthorizationScheme { get; set; }

            public string AuthorizationParameter { get; set; }

            public Dictionary<string, string> Headers { get; set; }
        }
    }
}
