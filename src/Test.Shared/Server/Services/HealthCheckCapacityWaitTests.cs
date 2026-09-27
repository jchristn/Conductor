namespace Test.Shared.Server.Services
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for <see cref="HealthCheckService.WaitForCapacityChangeAsync"/>: wake-up on a released endpoint
    /// slot, bounded waits, and cancellation.
    /// </summary>
    public class HealthCheckCapacityWaitTests : Test.Shared.Server.Controllers.ControllerTestBase
    {
        private HealthCheckService _Service;

        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Service = new HealthCheckService(Database, Logging);
        }

        public Task DisposeAsync()
        {
            _Service?.Dispose();
            return Task.CompletedTask;
        }

        public async Task Wait_WakesWhenAnEndpointSlotIsReleased()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Task waiting = _Service.WaitForCapacityChangeAsync(5000);
            await Task.Delay(50).ConfigureAwait(false);
            waiting.IsCompleted.Should().BeFalse();

            _Service.DecrementInFlight("mre_any");
            await waiting.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
        }

        public async Task Wait_WithoutChange_ReturnsAfterMaxWait()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await _Service.WaitForCapacityChangeAsync(100).ConfigureAwait(false);
            stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(80);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
        }

        public async Task Wait_WhenCancelled_Throws()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(50))
            {
                Func<Task> act = () => _Service.WaitForCapacityChangeAsync(5000, cts.Token);
                await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
            }
        }
    }
}
