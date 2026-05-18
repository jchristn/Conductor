namespace Test.Shared.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Models;
    using FluentAssertions;
    
    /// <summary>
    /// Unit tests for VirtualModelRunnerHealthStatus.
    /// </summary>
    public class VirtualModelRunnerHealthStatusTests
    {
        #region Default-Value-Tests
        public void OverallHealthy_DefaultsToFalse()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus();
            status.OverallHealthy.Should().BeFalse();
        }
        public void HealthyEndpointCount_DefaultsToZero()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus();
            status.HealthyEndpointCount.Should().Be(0);
        }
        public void TotalEndpointCount_DefaultsToZero()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus();
            status.TotalEndpointCount.Should().Be(0);
        }
        public void Endpoints_DefaultsToEmptyList()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus();
            status.Endpoints.Should().NotBeNull();
            status.Endpoints.Should().BeEmpty();
        }

        #endregion

        #region Property-Setting-Tests
        public void CanSetVirtualModelRunnerId()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                VirtualModelRunnerId = "vmr_test"
            };
            status.VirtualModelRunnerId.Should().Be("vmr_test");
        }
        public void CanSetVirtualModelRunnerName()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                VirtualModelRunnerName = "Test VMR"
            };
            status.VirtualModelRunnerName.Should().Be("Test VMR");
        }
        public void CanSetCheckedUtc()
        {
            DateTime now = DateTime.UtcNow;
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                CheckedUtc = now
            };
            status.CheckedUtc.Should().Be(now);
        }
        public void CanSetOverallHealthy()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                OverallHealthy = true
            };
            status.OverallHealthy.Should().BeTrue();
        }
        public void CanSetHealthyEndpointCount()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                HealthyEndpointCount = 5
            };
            status.HealthyEndpointCount.Should().Be(5);
        }
        public void CanSetTotalEndpointCount()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                TotalEndpointCount = 10
            };
            status.TotalEndpointCount.Should().Be(10);
        }
        public void CanSetEndpoints()
        {
            List<EndpointHealthStatus> endpoints = new List<EndpointHealthStatus>
            {
                new EndpointHealthStatus { EndpointId = "mre_1" },
                new EndpointHealthStatus { EndpointId = "mre_2" }
            };

            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                Endpoints = endpoints
            };

            status.Endpoints.Should().HaveCount(2);
            status.Endpoints[0].EndpointId.Should().Be("mre_1");
        }

        #endregion

        #region Usage-Scenario-Tests
        public void AllEndpointsHealthy_OverallHealthyIsTrue()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                OverallHealthy = true,
                HealthyEndpointCount = 3,
                TotalEndpointCount = 3,
                Endpoints = new List<EndpointHealthStatus>
                {
                    new EndpointHealthStatus { IsHealthy = true },
                    new EndpointHealthStatus { IsHealthy = true },
                    new EndpointHealthStatus { IsHealthy = true }
                }
            };

            status.OverallHealthy.Should().BeTrue();
            status.HealthyEndpointCount.Should().Be(status.TotalEndpointCount);
        }
        public void SomeEndpointsUnhealthy_OverallHealthyIsFalse()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                OverallHealthy = false,
                HealthyEndpointCount = 2,
                TotalEndpointCount = 3,
                Endpoints = new List<EndpointHealthStatus>
                {
                    new EndpointHealthStatus { IsHealthy = true },
                    new EndpointHealthStatus { IsHealthy = true },
                    new EndpointHealthStatus { IsHealthy = false }
                }
            };

            status.OverallHealthy.Should().BeFalse();
            status.HealthyEndpointCount.Should().BeLessThan(status.TotalEndpointCount);
        }
        public void NoEndpoints_CountsAreZero()
        {
            VirtualModelRunnerHealthStatus status = new VirtualModelRunnerHealthStatus
            {
                OverallHealthy = false,
                HealthyEndpointCount = 0,
                TotalEndpointCount = 0,
                Endpoints = new List<EndpointHealthStatus>()
            };

            status.HealthyEndpointCount.Should().Be(0);
            status.TotalEndpointCount.Should().Be(0);
            status.Endpoints.Should().BeEmpty();
        }

        #endregion
    }
}
