namespace Test.Shared.Core.Models
{
    using System;
    using Conductor.Core.Models;
    using FluentAssertions;
    
    /// <summary>
    /// Unit tests for EndpointHealthState.
    /// </summary>
    public class EndpointHealthStateTests
    {
        #region Default-Value-Tests
        public void IsHealthy_DefaultsToFalse()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.IsHealthy.Should().BeFalse();
        }
        public void TotalUptimeMs_DefaultsToZero()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.TotalUptimeMs.Should().Be(0);
        }
        public void TotalDowntimeMs_DefaultsToZero()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.TotalDowntimeMs.Should().Be(0);
        }
        public void ConsecutiveSuccesses_DefaultsToZero()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.ConsecutiveSuccesses.Should().Be(0);
        }
        public void ConsecutiveFailures_DefaultsToZero()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.ConsecutiveFailures.Should().Be(0);
        }
        public void InFlightRequests_DefaultsToZero()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.InFlightRequests.Should().Be(0);
        }
        public void LastCheckUtc_DefaultsToNull()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.LastCheckUtc.Should().BeNull();
        }
        public void LastHealthyUtc_DefaultsToNull()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.LastHealthyUtc.Should().BeNull();
        }
        public void LastUnhealthyUtc_DefaultsToNull()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.LastUnhealthyUtc.Should().BeNull();
        }
        public void LastStateChangeUtc_DefaultsToNull()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.LastStateChangeUtc.Should().BeNull();
        }
        public void LastError_DefaultsToNull()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.LastError.Should().BeNull();
        }
        public void Lock_IsNotNull()
        {
            EndpointHealthState state = new EndpointHealthState();
            state.Lock.Should().NotBeNull();
        }

        #endregion

        #region Copy-Tests
        public void Copy_CopiesEndpointId()
        {
            EndpointHealthState state = CreateTestState();
            EndpointHealthState copy = state.Copy();
            copy.EndpointId.Should().Be(state.EndpointId);
        }
        public void Copy_CopiesEndpointName()
        {
            EndpointHealthState state = CreateTestState();
            EndpointHealthState copy = state.Copy();
            copy.EndpointName.Should().Be(state.EndpointName);
        }
        public void Copy_CopiesTenantId()
        {
            EndpointHealthState state = CreateTestState();
            EndpointHealthState copy = state.Copy();
            copy.TenantId.Should().Be(state.TenantId);
        }
        public void Copy_CopiesIsHealthy()
        {
            EndpointHealthState state = CreateTestState();
            state.IsHealthy = true;
            EndpointHealthState copy = state.Copy();
            copy.IsHealthy.Should().BeTrue();
        }
        public void Copy_CopiesLastCheckUtc()
        {
            EndpointHealthState state = CreateTestState();
            state.LastCheckUtc = DateTime.UtcNow;
            EndpointHealthState copy = state.Copy();
            copy.LastCheckUtc.Should().Be(state.LastCheckUtc);
        }
        public void Copy_CopiesLastHealthyUtc()
        {
            EndpointHealthState state = CreateTestState();
            state.LastHealthyUtc = DateTime.UtcNow.AddMinutes(-5);
            EndpointHealthState copy = state.Copy();
            copy.LastHealthyUtc.Should().Be(state.LastHealthyUtc);
        }
        public void Copy_CopiesLastUnhealthyUtc()
        {
            EndpointHealthState state = CreateTestState();
            state.LastUnhealthyUtc = DateTime.UtcNow.AddMinutes(-10);
            EndpointHealthState copy = state.Copy();
            copy.LastUnhealthyUtc.Should().Be(state.LastUnhealthyUtc);
        }
        public void Copy_CopiesFirstCheckUtc()
        {
            EndpointHealthState state = CreateTestState();
            EndpointHealthState copy = state.Copy();
            copy.FirstCheckUtc.Should().Be(state.FirstCheckUtc);
        }
        public void Copy_CopiesLastStateChangeUtc()
        {
            EndpointHealthState state = CreateTestState();
            state.LastStateChangeUtc = DateTime.UtcNow.AddMinutes(-1);
            EndpointHealthState copy = state.Copy();
            copy.LastStateChangeUtc.Should().Be(state.LastStateChangeUtc);
        }
        public void Copy_CopiesTotalUptimeMs()
        {
            EndpointHealthState state = CreateTestState();
            state.TotalUptimeMs = 60000;
            EndpointHealthState copy = state.Copy();
            copy.TotalUptimeMs.Should().Be(60000);
        }
        public void Copy_CopiesTotalDowntimeMs()
        {
            EndpointHealthState state = CreateTestState();
            state.TotalDowntimeMs = 30000;
            EndpointHealthState copy = state.Copy();
            copy.TotalDowntimeMs.Should().Be(30000);
        }
        public void Copy_CopiesConsecutiveSuccesses()
        {
            EndpointHealthState state = CreateTestState();
            state.ConsecutiveSuccesses = 5;
            EndpointHealthState copy = state.Copy();
            copy.ConsecutiveSuccesses.Should().Be(5);
        }
        public void Copy_CopiesConsecutiveFailures()
        {
            EndpointHealthState state = CreateTestState();
            state.ConsecutiveFailures = 3;
            EndpointHealthState copy = state.Copy();
            copy.ConsecutiveFailures.Should().Be(3);
        }
        public void Copy_CopiesInFlightRequests()
        {
            EndpointHealthState state = CreateTestState();
            state.InFlightRequests = 2;
            EndpointHealthState copy = state.Copy();
            copy.InFlightRequests.Should().Be(2);
        }
        public void Copy_CopiesLastError()
        {
            EndpointHealthState state = CreateTestState();
            state.LastError = "Connection refused";
            EndpointHealthState copy = state.Copy();
            copy.LastError.Should().Be("Connection refused");
        }
        public void Copy_ReturnsNewInstance()
        {
            EndpointHealthState state = CreateTestState();
            EndpointHealthState copy = state.Copy();
            copy.Should().NotBeSameAs(state);
        }
        public void Copy_DoesNotCopyLockObject()
        {
            EndpointHealthState state = CreateTestState();
            EndpointHealthState copy = state.Copy();
            copy.Lock.Should().NotBeSameAs(state.Lock);
        }
        public void Copy_ChangesToCopyDoNotAffectOriginal()
        {
            EndpointHealthState state = CreateTestState();
            state.InFlightRequests = 1;

            EndpointHealthState copy = state.Copy();
            copy.InFlightRequests = 5;

            state.InFlightRequests.Should().Be(1);
        }

        #endregion

        #region Thread-Safety-Tests
        public void Lock_CanBeUsedForSynchronization()
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

        #region Helper-Methods

        private EndpointHealthState CreateTestState()
        {
            return new EndpointHealthState
            {
                EndpointId = "mre_test123",
                EndpointName = "Test Endpoint",
                TenantId = "ten_test",
                FirstCheckUtc = DateTime.UtcNow.AddMinutes(-30)
            };
        }

        #endregion
    }
}
