namespace Conductor.Server.Tests.Services
{
    using System;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit tests for HealthCheckService-related models.
    /// Note: Full HealthCheckService tests require integration tests due to non-mockable DatabaseDriverBase.
    /// </summary>
    public class HealthCheckServiceTests
    {
        #region EndpointHealthState-Default-Values-Tests

        [Fact]
        public void EndpointHealthState_IsHealthy_DefaultsToFalse()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.IsHealthy.Should().BeFalse();
        }

        [Fact]
        public void EndpointHealthState_TotalUptimeMs_DefaultsToZero()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.TotalUptimeMs.Should().Be(0);
        }

        [Fact]
        public void EndpointHealthState_TotalDowntimeMs_DefaultsToZero()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.TotalDowntimeMs.Should().Be(0);
        }

        [Fact]
        public void EndpointHealthState_ConsecutiveSuccesses_DefaultsToZero()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.ConsecutiveSuccesses.Should().Be(0);
        }

        [Fact]
        public void EndpointHealthState_ConsecutiveFailures_DefaultsToZero()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.ConsecutiveFailures.Should().Be(0);
        }

        [Fact]
        public void EndpointHealthState_InFlightRequests_DefaultsToZero()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.InFlightRequests.Should().Be(0);
        }

        #endregion

        #region EndpointHealthState-Property-Setting-Tests

        [Fact]
        public void EndpointHealthState_CanSetEndpointId()
        {
            EndpointHealthState state = new EndpointHealthState { EndpointId = "mre_test" };
            state.EndpointId.Should().Be("mre_test");
        }

        [Fact]
        public void EndpointHealthState_CanSetEndpointName()
        {
            EndpointHealthState state = new EndpointHealthState { EndpointName = "Test Endpoint" };
            state.EndpointName.Should().Be("Test Endpoint");
        }

        [Fact]
        public void EndpointHealthState_CanSetTenantId()
        {
            EndpointHealthState state = new EndpointHealthState { TenantId = "ten_test" };
            state.TenantId.Should().Be("ten_test");
        }

        [Fact]
        public void EndpointHealthState_CanSetIsHealthy()
        {
            EndpointHealthState state = new EndpointHealthState { IsHealthy = true };
            state.IsHealthy.Should().BeTrue();
        }

        [Fact]
        public void EndpointHealthState_CanSetLastCheckUtc()
        {
            DateTime now = DateTime.UtcNow;
            EndpointHealthState state = new EndpointHealthState { LastCheckUtc = now };
            state.LastCheckUtc.Should().Be(now);
        }

        [Fact]
        public void EndpointHealthState_CanSetLastHealthyUtc()
        {
            DateTime now = DateTime.UtcNow;
            EndpointHealthState state = new EndpointHealthState { LastHealthyUtc = now };
            state.LastHealthyUtc.Should().Be(now);
        }

        [Fact]
        public void EndpointHealthState_CanSetLastUnhealthyUtc()
        {
            DateTime now = DateTime.UtcNow;
            EndpointHealthState state = new EndpointHealthState { LastUnhealthyUtc = now };
            state.LastUnhealthyUtc.Should().Be(now);
        }

        [Fact]
        public void EndpointHealthState_CanSetFirstCheckUtc()
        {
            DateTime now = DateTime.UtcNow;
            EndpointHealthState state = new EndpointHealthState { FirstCheckUtc = now };
            state.FirstCheckUtc.Should().Be(now);
        }

        [Fact]
        public void EndpointHealthState_CanSetLastStateChangeUtc()
        {
            DateTime now = DateTime.UtcNow;
            EndpointHealthState state = new EndpointHealthState { LastStateChangeUtc = now };
            state.LastStateChangeUtc.Should().Be(now);
        }

        [Fact]
        public void EndpointHealthState_CanSetTotalUptimeMs()
        {
            EndpointHealthState state = new EndpointHealthState { TotalUptimeMs = 60000 };
            state.TotalUptimeMs.Should().Be(60000);
        }

        [Fact]
        public void EndpointHealthState_CanSetTotalDowntimeMs()
        {
            EndpointHealthState state = new EndpointHealthState { TotalDowntimeMs = 30000 };
            state.TotalDowntimeMs.Should().Be(30000);
        }

        [Fact]
        public void EndpointHealthState_CanSetConsecutiveSuccesses()
        {
            EndpointHealthState state = new EndpointHealthState { ConsecutiveSuccesses = 5 };
            state.ConsecutiveSuccesses.Should().Be(5);
        }

        [Fact]
        public void EndpointHealthState_CanSetConsecutiveFailures()
        {
            EndpointHealthState state = new EndpointHealthState { ConsecutiveFailures = 3 };
            state.ConsecutiveFailures.Should().Be(3);
        }

        [Fact]
        public void EndpointHealthState_CanSetInFlightRequests()
        {
            EndpointHealthState state = new EndpointHealthState { InFlightRequests = 2 };
            state.InFlightRequests.Should().Be(2);
        }

        [Fact]
        public void EndpointHealthState_CanSetLastError()
        {
            EndpointHealthState state = new EndpointHealthState { LastError = "Connection refused" };
            state.LastError.Should().Be("Connection refused");
        }

        #endregion

        #region EndpointHealthState-Copy-Tests

        [Fact]
        public void EndpointHealthState_Copy_CopiesAllProperties()
        {
            DateTime now = DateTime.UtcNow;
            EndpointHealthState state = new EndpointHealthState
            {
                EndpointId = "mre_test",
                EndpointName = "Test",
                TenantId = "ten_test",
                IsHealthy = true,
                LastCheckUtc = now,
                LastHealthyUtc = now,
                LastUnhealthyUtc = now.AddMinutes(-5),
                FirstCheckUtc = now.AddHours(-1),
                LastStateChangeUtc = now.AddMinutes(-1),
                TotalUptimeMs = 50000,
                TotalDowntimeMs = 10000,
                ConsecutiveSuccesses = 10,
                ConsecutiveFailures = 0,
                InFlightRequests = 3,
                LastError = null
            };

            EndpointHealthState copy = state.Copy();

            copy.EndpointId.Should().Be(state.EndpointId);
            copy.EndpointName.Should().Be(state.EndpointName);
            copy.TenantId.Should().Be(state.TenantId);
            copy.IsHealthy.Should().Be(state.IsHealthy);
            copy.LastCheckUtc.Should().Be(state.LastCheckUtc);
            copy.LastHealthyUtc.Should().Be(state.LastHealthyUtc);
            copy.LastUnhealthyUtc.Should().Be(state.LastUnhealthyUtc);
            copy.FirstCheckUtc.Should().Be(state.FirstCheckUtc);
            copy.LastStateChangeUtc.Should().Be(state.LastStateChangeUtc);
            copy.TotalUptimeMs.Should().Be(state.TotalUptimeMs);
            copy.TotalDowntimeMs.Should().Be(state.TotalDowntimeMs);
            copy.ConsecutiveSuccesses.Should().Be(state.ConsecutiveSuccesses);
            copy.ConsecutiveFailures.Should().Be(state.ConsecutiveFailures);
            copy.InFlightRequests.Should().Be(state.InFlightRequests);
            copy.LastError.Should().Be(state.LastError);
        }

        [Fact]
        public void EndpointHealthState_Copy_ReturnsNewInstance()
        {
            EndpointHealthState state = new EndpointHealthState { EndpointId = "mre_test" };
            EndpointHealthState copy = state.Copy();
            copy.Should().NotBeSameAs(state);
        }

        [Fact]
        public void EndpointHealthState_Copy_DoesNotCopyLockObject()
        {
            EndpointHealthState state = new EndpointHealthState();
            EndpointHealthState copy = state.Copy();
            copy.Lock.Should().NotBeSameAs(state.Lock);
        }

        [Fact]
        public void EndpointHealthState_Copy_ModificationsToOriginalDoNotAffectCopy()
        {
            EndpointHealthState state = new EndpointHealthState { InFlightRequests = 1 };
            EndpointHealthState copy = state.Copy();

            state.InFlightRequests = 10;

            copy.InFlightRequests.Should().Be(1);
        }

        [Fact]
        public void EndpointHealthState_Copy_ModificationsToCopyDoNotAffectOriginal()
        {
            EndpointHealthState state = new EndpointHealthState { InFlightRequests = 1 };
            EndpointHealthState copy = state.Copy();

            copy.InFlightRequests = 10;

            state.InFlightRequests.Should().Be(1);
        }

        #endregion

        #region EndpointHealthState-Lock-Tests

        [Fact]
        public void EndpointHealthState_Lock_IsNotNull()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.Lock.Should().NotBeNull();
        }

        [Fact]
        public void EndpointHealthState_Lock_CanBeUsedForSynchronization()
        {
            EndpointHealthState state = new EndpointHealthState();
            bool lockAcquired = false;

            lock (state.Lock)
            {
                lockAcquired = true;
            }

            lockAcquired.Should().BeTrue();
        }

        #endregion

        #region Uptime-Percentage-Tests

        [Fact]
        public void EndpointHealthState_UptimePercentage_CanBeCalculated()
        {
            EndpointHealthState state = new EndpointHealthState
            {
                TotalUptimeMs = 90000,
                TotalDowntimeMs = 10000
            };

            long totalMs = state.TotalUptimeMs + state.TotalDowntimeMs;
            double uptimePercentage = totalMs > 0 ? (double)state.TotalUptimeMs / totalMs * 100 : 0;

            uptimePercentage.Should().Be(90);
        }

        [Fact]
        public void EndpointHealthState_UptimePercentage_IsZeroWhenNoTime()
        {
            EndpointHealthState state = new EndpointHealthState
            {
                TotalUptimeMs = 0,
                TotalDowntimeMs = 0
            };

            long totalMs = state.TotalUptimeMs + state.TotalDowntimeMs;
            double uptimePercentage = totalMs > 0 ? (double)state.TotalUptimeMs / totalMs * 100 : 0;

            uptimePercentage.Should().Be(0);
        }

        [Fact]
        public void EndpointHealthState_UptimePercentage_Is100WhenNoDowntime()
        {
            EndpointHealthState state = new EndpointHealthState
            {
                TotalUptimeMs = 60000,
                TotalDowntimeMs = 0
            };

            long totalMs = state.TotalUptimeMs + state.TotalDowntimeMs;
            double uptimePercentage = totalMs > 0 ? (double)state.TotalUptimeMs / totalMs * 100 : 0;

            uptimePercentage.Should().Be(100);
        }

        #endregion
    }
}
