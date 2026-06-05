namespace Test.Shared.Server.Services
{
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for operational metrics aggregation.
    /// </summary>
    public class OperationalMetricsServiceTests
    {
        public void GetSnapshot_AggregatesRequestsDenialsAndHitRate()
        {
            OperationalMetricsService metrics = new OperationalMetricsService();

            metrics.RecordRoutingDecision("ten_test", "vmr_one", "VMR One", "Ollama", true, "Routed", null, "Hit", false, 12);
            metrics.RecordRoutingDecision("ten_test", "vmr_one", "VMR One", "Ollama", false, "Denied", "AllEndpointsAtCapacity", "Miss", true, 45);
            metrics.RecordRequestCompletion("ten_test", "vmr_one", "VMR One", "Ollama", 200, 30);
            metrics.RecordTelemetryFreshnessFailure("ten_test", "vmr_one", "VMR One", "Ollama", "lbp_one");

            Conductor.Core.Models.ObservabilityMetricsSnapshot snapshot = metrics.GetSnapshot();

            snapshot.Overall.TotalRequests.Should().Be(2);
            snapshot.Overall.RoutedRequests.Should().Be(1);
            snapshot.Overall.DeniedRequests.Should().Be(1);
            snapshot.Overall.PolicyFallbacks.Should().Be(1);
            snapshot.Overall.TelemetryFreshnessFailures.Should().Be(1);
            snapshot.Overall.SaturationDenials.Should().Be(1);
            snapshot.Overall.SessionAffinityHitRate.Should().Be(50);
            snapshot.Overall.RouteDecisionDurationMs.Count.Should().Be(2);
            snapshot.Overall.TotalDurationMs.Count.Should().Be(1);
            snapshot.Overall.FirstTokenTimeMs.Count.Should().Be(1);
            snapshot.VirtualModelRunners.Should().ContainSingle(item => item.VirtualModelRunnerId == "vmr_one");
        }

        public void RenderPrometheus_IncludesExpectedMetricFamilies()
        {
            OperationalMetricsService metrics = new OperationalMetricsService();

            metrics.RecordRoutingDecision("ten_test", "vmr_one", "VMR One", "Ollama", true, "Routed", null, null, false, 10);
            metrics.RecordRequestCompletion("ten_test", "vmr_one", "VMR One", "Ollama", 120, 22);
            metrics.RecordModelLoadRequest("ten_test", "ModelRunnerEndpoint", "mre_one", true, "Loaded", 3000);
            metrics.RecordModelLoadEndpointAttempt("ten_test", "ModelRunnerEndpoint", "mre_one", "mre_one", "Ollama", "OllamaGenerate", true, "Loaded", 3000);

            string output = metrics.RenderPrometheus();

            output.Should().Contain("conductor_requests_total");
            output.Should().Contain("conductor_route_decision_duration_ms_bucket");
            output.Should().Contain("conductor_total_duration_ms_count");
            output.Should().Contain("conductor_first_token_time_ms_sum");
            output.Should().Contain("conductor_model_load_requests_total");
            output.Should().Contain("conductor_model_load_endpoint_attempts_total");
            output.Should().Contain("conductor_model_load_duration_ms_count");
            output.Should().Contain("conductor_model_load_endpoint_duration_ms_count");
        }
    }
}
