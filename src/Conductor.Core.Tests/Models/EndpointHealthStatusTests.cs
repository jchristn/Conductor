namespace Conductor.Core.Tests.Models
{
    using System;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for EndpointHealthStatus.
    /// </summary>
    public class EndpointHealthStatusTests
    {
        #region Default-Value-Tests

        [Fact]
        public void IsHealthy_DefaultsToFalse()
        {
            EndpointHealthStatus status = new EndpointHealthStatus();
            status.IsHealthy.Should().BeFalse();
        }

        [Fact]
        public void TotalUptimeMs_DefaultsToZero()
        {
            EndpointHealthStatus status = new EndpointHealthStatus();
            status.TotalUptimeMs.Should().Be(0);
        }

        [Fact]
        public void TotalDowntimeMs_DefaultsToZero()
        {
            EndpointHealthStatus status = new EndpointHealthStatus();
            status.TotalDowntimeMs.Should().Be(0);
        }

        [Fact]
        public void ConsecutiveSuccesses_DefaultsToZero()
        {
            EndpointHealthStatus status = new EndpointHealthStatus();
            status.ConsecutiveSuccesses.Should().Be(0);
        }

        [Fact]
        public void ConsecutiveFailures_DefaultsToZero()
        {
            EndpointHealthStatus status = new EndpointHealthStatus();
            status.ConsecutiveFailures.Should().Be(0);
        }

        [Fact]
        public void InFlightRequests_DefaultsToZero()
        {
            EndpointHealthStatus status = new EndpointHealthStatus();
            status.InFlightRequests.Should().Be(0);
        }

        [Fact]
        public void MaxParallelRequests_DefaultsToZero()
        {
            EndpointHealthStatus status = new EndpointHealthStatus();
            status.MaxParallelRequests.Should().Be(0);
        }

        [Fact]
        public void Weight_DefaultsToZero()
        {
            EndpointHealthStatus status = new EndpointHealthStatus();
            status.Weight.Should().Be(0);
        }

        #endregion

        #region UptimePercentage-Tests

        [Fact]
        public void UptimePercentage_WhenNoTime_ReturnsZero()
        {
            EndpointHealthStatus status = new EndpointHealthStatus
            {
                TotalUptimeMs = 0,
                TotalDowntimeMs = 0
            };
            status.UptimePercentage.Should().Be(0);
        }

        [Fact]
        public void UptimePercentage_WhenAllUptime_Returns100()
        {
            EndpointHealthStatus status = new EndpointHealthStatus
            {
                TotalUptimeMs = 60000,
                TotalDowntimeMs = 0
            };
            status.UptimePercentage.Should().Be(100);
        }

        [Fact]
        public void UptimePercentage_WhenAllDowntime_ReturnsZero()
        {
            EndpointHealthStatus status = new EndpointHealthStatus
            {
                TotalUptimeMs = 0,
                TotalDowntimeMs = 60000
            };
            status.UptimePercentage.Should().Be(0);
        }

        [Fact]
        public void UptimePercentage_WhenMixed_ReturnsCorrectPercentage()
        {
            EndpointHealthStatus status = new EndpointHealthStatus
            {
                TotalUptimeMs = 90000,
                TotalDowntimeMs = 10000
            };
            status.UptimePercentage.Should().Be(90);
        }

        [Fact]
        public void UptimePercentage_RoundsToTwoDecimalPlaces()
        {
            EndpointHealthStatus status = new EndpointHealthStatus
            {
                TotalUptimeMs = 1,
                TotalDowntimeMs = 3
            };
            status.UptimePercentage.Should().Be(25);
        }

        #endregion

        #region Property-Setting-Tests

        [Fact]
        public void CanSetEndpointId()
        {
            EndpointHealthStatus status = new EndpointHealthStatus { EndpointId = "mre_test" };
            status.EndpointId.Should().Be("mre_test");
        }

        [Fact]
        public void CanSetEndpointName()
        {
            EndpointHealthStatus status = new EndpointHealthStatus { EndpointName = "Test Endpoint" };
            status.EndpointName.Should().Be("Test Endpoint");
        }

        [Fact]
        public void CanSetLastCheckUtc()
        {
            DateTime now = DateTime.UtcNow;
            EndpointHealthStatus status = new EndpointHealthStatus { LastCheckUtc = now };
            status.LastCheckUtc.Should().Be(now);
        }

        [Fact]
        public void CanSetLastError()
        {
            EndpointHealthStatus status = new EndpointHealthStatus { LastError = "Connection refused" };
            status.LastError.Should().Be("Connection refused");
        }

        #endregion

        #region FromState-Tests

        [Fact]
        public void FromState_WithStateAndEndpoint_CopiesAllProperties()
        {
            DateTime now = DateTime.UtcNow;
            EndpointHealthState state = new EndpointHealthState
            {
                EndpointId = "mre_test",
                EndpointName = "State Name",
                IsHealthy = true,
                LastCheckUtc = now,
                LastHealthyUtc = now,
                LastUnhealthyUtc = now.AddMinutes(-10),
                FirstCheckUtc = now.AddHours(-1),
                TotalUptimeMs = 50000,
                TotalDowntimeMs = 10000,
                ConsecutiveSuccesses = 5,
                ConsecutiveFailures = 0,
                InFlightRequests = 2,
                LastError = null
            };

            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Endpoint Name",
                MaxParallelRequests = 10,
                Weight = 5
            };

            EndpointHealthStatus status = EndpointHealthStatus.FromState(state, endpoint);

            status.EndpointId.Should().Be("mre_test");
            status.EndpointName.Should().Be("State Name");
            status.IsHealthy.Should().BeTrue();
            status.LastCheckUtc.Should().Be(now);
            status.TotalUptimeMs.Should().Be(50000);
            status.TotalDowntimeMs.Should().Be(10000);
            status.ConsecutiveSuccesses.Should().Be(5);
            status.InFlightRequests.Should().Be(2);
            status.MaxParallelRequests.Should().Be(10);
            status.Weight.Should().Be(5);
        }

        [Fact]
        public void FromState_WhenStateNameIsNull_UsesEndpointName()
        {
            EndpointHealthState state = new EndpointHealthState
            {
                EndpointId = "mre_test",
                EndpointName = null
            };

            ModelRunnerEndpoint endpoint = new ModelRunnerEndpoint
            {
                Name = "Endpoint Name"
            };

            EndpointHealthStatus status = EndpointHealthStatus.FromState(state, endpoint);

            status.EndpointName.Should().Be("Endpoint Name");
        }

        [Fact]
        public void FromState_WhenEndpointIsNull_UsesDefaults()
        {
            EndpointHealthState state = new EndpointHealthState
            {
                EndpointId = "mre_test",
                EndpointName = "State Name"
            };

            EndpointHealthStatus status = EndpointHealthStatus.FromState(state, null);

            status.MaxParallelRequests.Should().Be(0);
            status.Weight.Should().Be(1);
        }

        #endregion
    }
}
