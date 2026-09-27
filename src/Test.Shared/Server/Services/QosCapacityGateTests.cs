namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Server.Services;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for <see cref="QosCapacityGate"/>: permit accounting, FIFO grants, resizing, cancellation that
    /// never loses a permit, and disposal.
    /// </summary>
    public class QosCapacityGateTests
    {
        public async Task Wait_UpToCapacity_GrantsImmediately()
        {
            using (QosCapacityGate gate = new QosCapacityGate(2))
            {
                await gate.WaitAsync();
                await gate.WaitAsync();
                gate.InUse.Should().Be(2);
                gate.TryTake().Should().BeFalse();
            }
        }

        public async Task Release_GrantsLongestWaiterFirst()
        {
            using (QosCapacityGate gate = new QosCapacityGate(1))
            {
                await gate.WaitAsync();
                List<int> order = new List<int>();
                Task first = gate.WaitAsync().ContinueWith(_ => { lock (order) order.Add(1); }, TaskScheduler.Default);
                Task second = gate.WaitAsync().ContinueWith(_ => { lock (order) order.Add(2); }, TaskScheduler.Default);
                gate.Waiting.Should().Be(2);

                gate.Release();
                await first;
                gate.Release();
                await second;

                order.Should().Equal(1, 2);
                gate.InUse.Should().Be(1);
            }
        }

        public async Task CancelledWaiter_ThrowsAndHoldsNoPermit()
        {
            using (QosCapacityGate gate = new QosCapacityGate(1))
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                await gate.WaitAsync();
                Task waiting = gate.WaitAsync(cts.Token);
                cts.Cancel();

                Func<Task> act = () => waiting;
                await act.Should().ThrowAsync<OperationCanceledException>();
                gate.Waiting.Should().Be(0);

                gate.Release();
                gate.InUse.Should().Be(0);
                gate.TryTake().Should().BeTrue();
            }
        }

        public async Task CancellationRacingGrants_NeverLeaksOrDoubleGrantsPermits()
        {
            using (QosCapacityGate gate = new QosCapacityGate(3))
            {
                int concurrent = 0;
                int maxConcurrent = 0;
                List<Task> workers = new List<Task>();

                for (int i = 0; i < 400; i++)
                {
                    int n = i;
                    workers.Add(Task.Run(async () =>
                    {
                        using (CancellationTokenSource cts = new CancellationTokenSource())
                        {
                            if (n % 3 == 0) cts.CancelAfter(n % 7);
                            try
                            {
                                await gate.WaitAsync(cts.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }

                            int now = Interlocked.Increment(ref concurrent);
                            int seen;
                            while ((seen = Volatile.Read(ref maxConcurrent)) < now && Interlocked.CompareExchange(ref maxConcurrent, now, seen) != seen) { }
                            await Task.Delay(n % 4).ConfigureAwait(false);
                            Interlocked.Decrement(ref concurrent);
                            gate.Release();
                        }
                    }));
                }

                await Task.WhenAll(workers);

                maxConcurrent.Should().BeLessThanOrEqualTo(3);
                gate.InUse.Should().Be(0);
                gate.Waiting.Should().Be(0);
            }
        }

        public async Task SetCapacity_RaisingGrantsParkedWaiters()
        {
            using (QosCapacityGate gate = new QosCapacityGate(1))
            {
                await gate.WaitAsync();
                Task parked = gate.WaitAsync();
                parked.IsCompleted.Should().BeFalse();

                gate.SetCapacity(2);
                await parked;
                gate.InUse.Should().Be(2);
            }
        }

        public async Task SetCapacity_LoweringDrainsHeldPermits()
        {
            using (QosCapacityGate gate = new QosCapacityGate(3))
            {
                await gate.WaitAsync();
                await gate.WaitAsync();
                gate.SetCapacity(1);

                gate.TryTake().Should().BeFalse();
                gate.Release();
                gate.TryTake().Should().BeFalse();
                gate.Release();
                gate.TryTake().Should().BeTrue();
            }
        }

        public void Release_WithoutPermit_IsIgnored()
        {
            using (QosCapacityGate gate = new QosCapacityGate(1))
            {
                gate.Release();
                gate.Release();
                gate.InUse.Should().Be(0);
                gate.TryTake().Should().BeTrue();
                gate.TryTake().Should().BeFalse();
            }
        }

        public async Task ZeroCapacity_IsUnboundedButCounted()
        {
            using (QosCapacityGate gate = new QosCapacityGate(0))
            {
                for (int i = 0; i < 50; i++) await gate.WaitAsync();
                gate.InUse.Should().Be(50);
            }
        }

        public async Task Dispose_FailsParkedWaiters()
        {
            QosCapacityGate gate = new QosCapacityGate(1);
            await gate.WaitAsync();
            Task parked = gate.WaitAsync();
            gate.Dispose();

            Func<Task> act = () => parked;
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }
    }
}
