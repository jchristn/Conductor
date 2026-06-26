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
                ["reservationGuid"] = "vmrr_1",
                ["reservationReasonCode"] = "ReservationDenied",
                ["empty"] = ""
            });

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://conductor.local/v1.0/analytics/summary?range=lastDay&tenantId=ten_1&reservationGuid=vmrr_1&reservationReasonCode=ReservationDenied", request.Uri.ToString());
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
                    RequestorUserIds = new[] { "usr_1" },
                    ReservationReasonCodes = new[] { "ReservationDenied" }
                }
            });

            RecordedRequest request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://conductor.local/v1.0/analytics/query", request.Uri.ToString());
            Assert.Contains("\"TokenUnitCost\":0.01", request.Body);
            Assert.Contains("\"RequestorUserIds\":[\"usr_1\"]", request.Body);
            Assert.Contains("\"ReservationReasonCodes\":[\"ReservationDenied\"]", request.Body);
        }

        [Fact]
        public async Task EndpointGroupMethods_UseExpectedRoutesAndBodies()
        {
            RecordingHandler handler = new RecordingHandler(request =>
            {
                if (request.Method == HttpMethod.Delete) return new HttpResponseMessage(HttpStatusCode.NoContent);
                return JsonResponse("{}");
            });
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            var group = new { Name = "Primary", EndpointIds = new[] { "mre_1" } };
            using JsonDocument list = await client.ListEndpointGroupsAsync(new Dictionary<string, string> { ["tenantId"] = "ten_1", ["activeFilter"] = "true" });
            using JsonDocument get = await client.GetEndpointGroupAsync("egp_1", "ten_1");
            using JsonDocument create = await client.CreateEndpointGroupAsync(group);
            using JsonDocument update = await client.UpdateEndpointGroupAsync("egp_1", group);
            await client.DeleteEndpointGroupAsync("egp_1", "ten_1");
            using JsonDocument validate = await client.ValidateEndpointGroupAsync(group, "egp_1");

            Assert.Equal("https://conductor.local/v1.0/endpointgroups?tenantId=ten_1&activeFilter=true", handler.Requests[0].Uri.ToString());
            Assert.Equal("https://conductor.local/v1.0/endpointgroups/egp_1?tenantId=ten_1", handler.Requests[1].Uri.ToString());
            Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
            Assert.Equal("https://conductor.local/v1.0/endpointgroups", handler.Requests[2].Uri.ToString());
            Assert.Contains("\"Name\":\"Primary\"", handler.Requests[2].Body);
            Assert.Equal(HttpMethod.Put, handler.Requests[3].Method);
            Assert.Equal("https://conductor.local/v1.0/endpointgroups/egp_1", handler.Requests[3].Uri.ToString());
            Assert.Equal(HttpMethod.Delete, handler.Requests[4].Method);
            Assert.Equal("https://conductor.local/v1.0/endpointgroups/egp_1?tenantId=ten_1", handler.Requests[4].Uri.ToString());
            Assert.Equal("https://conductor.local/v1.0/endpointgroups/validate?existingId=egp_1", handler.Requests[5].Uri.ToString());
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
        public async Task ReservationMethods_UseExpectedRoutesAndTenantScope()
        {
            RecordingHandler handler = new RecordingHandler(request =>
            {
                if (request.Method == HttpMethod.Delete) return new HttpResponseMessage(HttpStatusCode.NoContent);
                return JsonResponse("{}");
            });
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            object reservation = new
            {
                TenantId = "ten_1",
                VirtualModelRunnerId = "vmr_1",
                Name = "Benchmark window"
            };

            using JsonDocument list = await client.ListVirtualModelRunnerReservationsAsync(new Dictionary<string, string>
            {
                ["tenantId"] = "ten_1",
                ["vmrId"] = "vmr_1",
                ["state"] = "upcoming"
            });
            using JsonDocument get = await client.GetVirtualModelRunnerReservationAsync("vmrr_1", "ten_1");
            using JsonDocument create = await client.CreateVirtualModelRunnerReservationAsync(reservation);
            using JsonDocument update = await client.UpdateVirtualModelRunnerReservationAsync("vmrr_1", reservation);
            await client.DeleteVirtualModelRunnerReservationAsync("vmrr_1", "ten_1");
            using JsonDocument validate = await client.ValidateVirtualModelRunnerReservationAsync(reservation);
            using JsonDocument scoped = await client.ListReservationsForVirtualModelRunnerAsync("vmr_1", new Dictionary<string, string> { ["tenantId"] = "ten_1" });
            using JsonDocument effective = await client.GetVirtualModelRunnerReservationEffectiveAsync("vmr_1", new Dictionary<string, string>
            {
                ["tenantId"] = "ten_1",
                ["credentialId"] = "cred_1"
            });

            Assert.Collection(
                handler.Requests,
                request =>
                {
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/vmrreservations?tenantId=ten_1&vmrId=vmr_1&state=upcoming", request.Uri.ToString());
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/vmrreservations/vmrr_1?tenantId=ten_1", request.Uri.ToString());
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/vmrreservations", request.Uri.ToString());
                    Assert.Contains("\"VirtualModelRunnerId\":\"vmr_1\"", request.Body);
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Put, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/vmrreservations/vmrr_1", request.Uri.ToString());
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Delete, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/vmrreservations/vmrr_1?tenantId=ten_1", request.Uri.ToString());
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/vmrreservations/validate", request.Uri.ToString());
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/virtualmodelrunners/vmr_1/reservations?tenantId=ten_1", request.Uri.ToString());
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/virtualmodelrunners/vmr_1/reservation-effective?tenantId=ten_1&credentialId=cred_1", request.Uri.ToString());
                });
        }

        [Fact]
        public async Task VirtualModelRunnerRuntimeMethods_UseExpectedRoutesAndBodies()
        {
            RecordingHandler handler = new RecordingHandler(_ => JsonResponse("{\"Endpoints\":[]}"));
            using HttpClient httpClient = new HttpClient(handler);
            using ConductorClient client = new ConductorClient("https://conductor.local", httpClient: httpClient);

            Dictionary<string, string> filters = new Dictionary<string, string>
            {
                ["tenantId"] = "ten_1",
                ["endpointId"] = "mre_1"
            };

            using JsonDocument stats = await client.GetVirtualModelRunnerRuntimeStatsAsync("vmr_1", filters);
            using JsonDocument reset = await client.ResetVirtualModelRunnerRuntimeStatsAsync("vmr_1", filters);
            using JsonDocument clear = await client.ClearVirtualModelRunnerRuntimeBackoffAsync("vmr_1", filters);

            Assert.Collection(
                handler.Requests,
                request =>
                {
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/virtualmodelrunners/vmr_1/runtime-stats?tenantId=ten_1&endpointId=mre_1", request.Uri.ToString());
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/virtualmodelrunners/vmr_1/runtime-stats/reset?tenantId=ten_1&endpointId=mre_1", request.Uri.ToString());
                    Assert.Equal("{}", request.Body);
                },
                request =>
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("https://conductor.local/v1.0/virtualmodelrunners/vmr_1/runtime-backoff/clear?tenantId=ten_1&endpointId=mre_1", request.Uri.ToString());
                    Assert.Equal("{}", request.Body);
                });
        }

        [Fact]
        public void AdaptiveLoadBalancingModels_SerializeExpectedContractShape()
        {
            Assert.Equal(3, (int)LoadBalancingMode.LeastRecentlyUsed);
            Assert.Equal(4, (int)LoadBalancingMode.Adaptive);

            AdaptiveLoadBalancingSettings settings = new AdaptiveLoadBalancingSettings
            {
                SampleCount = 3,
                BackoffBreaksSessionAffinity = false,
                Weights = new AdaptiveScoreWeights
                {
                    Success = 40,
                    Latency = 30,
                    TimeToFirstToken = 10,
                    Pending = 10,
                    EndpointWeight = 10
                }
            };
            EndpointGroup group = new EndpointGroup
            {
                Id = "primary",
                Name = "Primary",
                Priority = 0,
                TrafficWeight = 90,
                EndpointIds = new List<string> { "mre_1", "mre_2" }
            };

            string settingsJson = JsonSerializer.Serialize(settings);
            string groupJson = JsonSerializer.Serialize(group);

            Assert.Contains("\"SampleCount\":3", settingsJson);
            Assert.Contains("\"BackoffBreaksSessionAffinity\":false", settingsJson);
            Assert.Contains("\"Latency\":30", settingsJson);
            Assert.Contains("\"EndpointIds\":[\"mre_1\",\"mre_2\"]", groupJson);
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
