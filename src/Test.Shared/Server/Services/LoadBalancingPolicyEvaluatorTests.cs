namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for load-balancing policy evaluation.
    /// </summary>
    public class LoadBalancingPolicyEvaluatorTests
    {
        private readonly LoadBalancingPolicyEvaluator _Evaluator = new LoadBalancingPolicyEvaluator();

        public void ValidatePolicy_WithUnsupportedMetric_ReturnsFalse()
        {
            LoadBalancingPolicy policy = new LoadBalancingPolicy
            {
                TenantId = "ten_test",
                Name = "Broken",
                Filters = new List<LoadBalancingPolicyFilter>
                {
                    new LoadBalancingPolicyFilter
                    {
                        Metric = "rig.not-real",
                        Operator = LoadBalancingPolicyOperatorEnum.Equal,
                        ValueType = LoadBalancingMetricValueTypeEnum.Boolean,
                        Value = "true"
                    }
                }
            };

            bool valid = _Evaluator.ValidatePolicy(policy, out string error);

            valid.Should().BeFalse();
            error.Should().Contain("Unsupported");
        }
        public void ValidatePolicy_WithBooleanRankingMetric_ReturnsFalse()
        {
            LoadBalancingPolicy policy = new LoadBalancingPolicy
            {
                TenantId = "ten_test",
                Name = "Broken Ranking",
                Ranking = new List<LoadBalancingPolicyRankingRule>
                {
                    new LoadBalancingPolicyRankingRule
                    {
                        Metric = "health.isHealthy",
                        Direction = LoadBalancingPolicyRankingDirectionEnum.Ascending,
                        Weight = 1
                    }
                }
            };

            bool valid = _Evaluator.ValidatePolicy(policy, out string error);

            valid.Should().BeFalse();
            error.Should().Contain("does not support ranking");
        }

        public void Evaluate_LowestCpuPolicy_PrefersLowestUtilization()
        {
            LoadBalancingPolicy policy = new LoadBalancingPolicy
            {
                TenantId = "ten_test",
                Name = "Lowest CPU",
                Filters = new List<LoadBalancingPolicyFilter>
                {
                    new LoadBalancingPolicyFilter
                    {
                        Metric = "health.isHealthy",
                        Operator = LoadBalancingPolicyOperatorEnum.Equal,
                        ValueType = LoadBalancingMetricValueTypeEnum.Boolean,
                        Value = "true"
                    }
                },
                Ranking = new List<LoadBalancingPolicyRankingRule>
                {
                    new LoadBalancingPolicyRankingRule
                    {
                        Metric = "rig.cpu.utilizationPercent",
                        Direction = LoadBalancingPolicyRankingDirectionEnum.Ascending,
                        Weight = 1
                    }
                }
            };

            EndpointAvailability fastEndpoint = CreateAvailability("mre_lowcpu");
            EndpointAvailability busyEndpoint = CreateAvailability("mre_highcpu");

            Dictionary<string, EndpointHealthState> states = new Dictionary<string, EndpointHealthState>
            {
                ["mre_lowcpu"] = CreateState(12, DateTime.UtcNow),
                ["mre_highcpu"] = CreateState(81, DateTime.UtcNow)
            };

            LoadBalancingPolicyEvaluator.EvaluationResult result = _Evaluator.Evaluate(
                policy,
                new List<EndpointAvailability> { busyEndpoint, fastEndpoint },
                endpointId => states[endpointId]);

            result.Success.Should().BeTrue();
            result.Candidates.Should().HaveCount(2);
            result.Candidates[0].Availability.Endpoint.Id.Should().Be("mre_lowcpu");
            result.Candidates[0].Score.Should().BeGreaterThan(result.Candidates[1].Score);
        }

        public void Evaluate_WithStaleTelemetry_ReturnsFailure()
        {
            LoadBalancingPolicy policy = new LoadBalancingPolicy
            {
                TenantId = "ten_test",
                Name = "Telemetry Freshness",
                MaxTelemetryAgeMs = 5000,
                Ranking = new List<LoadBalancingPolicyRankingRule>
                {
                    new LoadBalancingPolicyRankingRule
                    {
                        Metric = "rig.cpu.utilizationPercent",
                        Direction = LoadBalancingPolicyRankingDirectionEnum.Ascending,
                        Weight = 1
                    }
                }
            };

            EndpointAvailability endpoint = CreateAvailability("mre_stale");
            EndpointHealthState state = CreateState(22, DateTime.UtcNow.AddMinutes(-5));

            LoadBalancingPolicyEvaluator.EvaluationResult result = _Evaluator.Evaluate(
                policy,
                new List<EndpointAvailability> { endpoint },
                _ => state);

            result.Success.Should().BeFalse();
            result.FailureReason.Should().Contain("No endpoints");
        }

        public void IsEndpointEligible_WithGpuRequirementAndNoGpu_ReturnsFalse()
        {
            LoadBalancingPolicy policy = new LoadBalancingPolicy
            {
                TenantId = "ten_test",
                Name = "GPU Only",
                Filters = new List<LoadBalancingPolicyFilter>
                {
                    new LoadBalancingPolicyFilter
                    {
                        Metric = "rig.gpu.available",
                        Operator = LoadBalancingPolicyOperatorEnum.Equal,
                        ValueType = LoadBalancingMetricValueTypeEnum.Boolean,
                        Value = "true"
                    }
                }
            };

            bool eligible = _Evaluator.IsEndpointEligible(
                policy,
                CreateAvailability("mre_cpu"),
                CreateState(33, DateTime.UtcNow));

            eligible.Should().BeFalse();
        }
        public void Evaluate_WithDescendingRanking_PrefersLargestValue()
        {
            LoadBalancingPolicy policy = new LoadBalancingPolicy
            {
                TenantId = "ten_test",
                Name = "Most Free Memory",
                Ranking = new List<LoadBalancingPolicyRankingRule>
                {
                    new LoadBalancingPolicyRankingRule
                    {
                        Metric = "rig.memory.availableBytes",
                        Direction = LoadBalancingPolicyRankingDirectionEnum.Descending,
                        Weight = 1
                    }
                }
            };

            Dictionary<string, EndpointHealthState> states = new Dictionary<string, EndpointHealthState>
            {
                ["mre_small"] = CreateState(40, DateTime.UtcNow, availableMemoryBytes: 2L * 1024 * 1024 * 1024),
                ["mre_large"] = CreateState(40, DateTime.UtcNow, availableMemoryBytes: 6L * 1024 * 1024 * 1024)
            };

            LoadBalancingPolicyEvaluator.EvaluationResult result = _Evaluator.Evaluate(
                policy,
                new List<EndpointAvailability> { CreateAvailability("mre_small"), CreateAvailability("mre_large") },
                endpointId => states[endpointId]);

            result.Success.Should().BeTrue();
            result.Candidates[0].Availability.Endpoint.Id.Should().Be("mre_large");
        }
        public void Evaluate_WithMixedWeights_UsesCombinedScore()
        {
            LoadBalancingPolicy policy = new LoadBalancingPolicy
            {
                TenantId = "ten_test",
                Name = "Balanced Policy",
                Ranking = new List<LoadBalancingPolicyRankingRule>
                {
                    new LoadBalancingPolicyRankingRule
                    {
                        Metric = "rig.cpu.utilizationPercent",
                        Direction = LoadBalancingPolicyRankingDirectionEnum.Ascending,
                        Weight = 0.7
                    },
                    new LoadBalancingPolicyRankingRule
                    {
                        Metric = "health.inFlightRequests",
                        Direction = LoadBalancingPolicyRankingDirectionEnum.Ascending,
                        Weight = 0.3
                    }
                }
            };

            Dictionary<string, EndpointHealthState> states = new Dictionary<string, EndpointHealthState>
            {
                ["mre_balanced_a"] = CreateState(15, DateTime.UtcNow, inFlightRequests: 4),
                ["mre_balanced_b"] = CreateState(30, DateTime.UtcNow, inFlightRequests: 0)
            };

            LoadBalancingPolicyEvaluator.EvaluationResult result = _Evaluator.Evaluate(
                policy,
                new List<EndpointAvailability> { CreateAvailability("mre_balanced_a"), CreateAvailability("mre_balanced_b") },
                endpointId => states[endpointId]);

            result.Success.Should().BeTrue();
            result.Candidates[0].Availability.Endpoint.Id.Should().Be("mre_balanced_a");
        }

        private static EndpointAvailability CreateAvailability(string endpointId)
        {
            return new EndpointAvailability(new ModelRunnerEndpoint
            {
                Id = endpointId,
                Name = endpointId,
                Hostname = "localhost"
            }, true, true);
        }

        private static EndpointHealthState CreateState(
            double cpuUtilizationPercent,
            DateTime telemetryTimestampUtc,
            long availableMemoryBytes = 1024L * 1024 * 1024,
            int inFlightRequests = 0)
        {
            return new EndpointHealthState
            {
                EndpointId = "endpoint",
                IsHealthy = true,
                InFlightRequests = inFlightRequests,
                RigMonitor = new RigMonitorEndpointStatus
                {
                    Enabled = true,
                    LastTelemetryUtc = telemetryTimestampUtc,
                    Telemetry = new RigMonitorTelemetrySnapshot
                    {
                        CollectedUtc = telemetryTimestampUtc,
                        Cpu = new RigMonitorCpuTelemetry
                        {
                            UtilizationPercent = cpuUtilizationPercent
                        },
                        Memory = new RigMonitorMemoryTelemetry
                        {
                            AvailableBytes = availableMemoryBytes,
                            UtilizationPercent = 35
                        }
                    }
                }
            };
        }
    }
}
