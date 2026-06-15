namespace Conductor.Sdk.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ConductorClientAnalyticsTests
    {
        [Fact]
        public async Task GetAnalyticsSummaryAsync_SerializesQueryStringAndBearerToken()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", bearerToken: "token-123", httpClient: httpClient);

            using JsonDocument response = await client.GetAnalyticsSummaryAsync(new Dictionary<string, string>
            {
                ["range"] = "lastDay",
                ["tenantId"] = "ten_1",
                ["empty"] = ""
            });

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://conductor.local/v1.0/analytics/summary?range=lastDay&tenantId=ten_1", request.Uri.ToString());
            Assert.Equal("Bearer", request.AuthorizationScheme);
            Assert.Equal("token-123", request.AuthorizationParameter);
        }

        [Fact]
        public async Task QueryAnalyticsAsync_PostsJsonBodyWithTokenUnitCost()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{\"TotalRequests\":1}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            using JsonDocument response = await client.QueryAnalyticsAsync(new
            {
                Range = "lastDay",
                TokenUnitCost = 0.01m,
                Filters = new
                {
                    RequestorUserIds = new[] { "usr_1" }
                }
            });

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://conductor.local/v1.0/analytics/query", request.Uri.ToString());
            Assert.Contains("\"TokenUnitCost\":0.01", request.Body);
            Assert.Contains("\"RequestorUserIds\":[\"usr_1\"]", request.Body);
        }

        [Fact]
        public async Task SavedReportMethods_UseExpectedRoutesAndTenantScope()
        {
            RecordingHandler handler = new RecordingHandler(request =>
            {
                if (request.Method == HttpMethod.Delete) return new HttpResponseMessage(HttpStatusCode.NoContent);
                return JsonResponse("{}");
            });
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            using JsonDocument list = await client.ListAnalyticsSavedReportsAsync(new Dictionary<string, string> { ["tenantId"] = "ten_1" });
            using JsonDocument create = await client.CreateAnalyticsSavedReportAsync(new { Name = "TTFT report" });
            using JsonDocument get = await client.GetAnalyticsSavedReportAsync("asr_1", "ten_1");
            using JsonDocument update = await client.UpdateAnalyticsSavedReportAsync("asr_1", new { Name = "Updated" });
            await client.DeleteAnalyticsSavedReportAsync("asr_1", "ten_1");

            Assert.Collection(
                handler.Requests,
                request =>
                {
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/analytics/reports?tenantId=ten_1", request.Uri.ToString());
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/analytics/reports", request.Uri.ToString());
                    Assert.Contains("\"Name\":\"TTFT report\"", request.Body);
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/analytics/reports/asr_1?tenantId=ten_1", request.Uri.ToString());
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Put, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/analytics/reports/asr_1", request.Uri.ToString());
                    Assert.Contains("\"Name\":\"Updated\"", request.Body);
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Delete, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/analytics/reports/asr_1?tenantId=ten_1", request.Uri.ToString());
                });
        }

        [Fact]
        public async Task GetAnalyticsCatalogAsync_PropagatesCancellationToken()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);
            using CancellationTokenSource cts = new CancellationTokenSource();

            using JsonDocument response = await client.GetAnalyticsCatalogAsync(cts.Token);

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.True(request.CancellationToken.CanBeCanceled);
        }

        [Fact]
        public async Task GetAnalyticsCatalogAsync_OnApiErrorThrowsConductorApiException()
        {
            RecordingHandler handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"error\":\"denied\"}", Encoding.UTF8, "application/json")
            });
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            ConductorApiException exception = await Assert.ThrowsAsync<ConductorApiException>(
                async () => await client.GetAnalyticsCatalogAsync());

            Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
            Assert.Equal("/v1.0/analytics/catalog", exception.Endpoint);
            Assert.Contains("denied", exception.ResponseBody);
        }

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

                Requests.Add(new RecordedRequest
                {
                    Method = request.Method,
                    Uri = request.RequestUri,
                    Body = body,
                    AuthorizationScheme = request.Headers.Authorization?.Scheme,
                    AuthorizationParameter = request.Headers.Authorization?.Parameter,
                    CancellationToken = cancellationToken
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

            public CancellationToken CancellationToken { get; set; }
        }
    }
}
