namespace Conductor.Server.Services
{
    using System;

    /// <summary>
    /// Running totals for one traffic class. Not thread-safe; guarded by the owning <see cref="QosRuntimeStats"/> lock.
    /// </summary>
    internal sealed class QosClassAccumulator
    {
        internal const int RecentWaitCapacity = 512;

        internal long Admitted;
        internal long Rejected;
        internal long TimedOut;
        internal long Aborted;
        internal long EndpointSlotTimeouts;
        internal int Waiting;
        internal double WaitSumMs;
        internal long WaitCount;
        internal double WaitMaxMs;
        internal DateTime? LastAdmittedUtc;
        internal DateTime? LastRejectedUtc;

        private readonly double[] _RecentWaits = new double[RecentWaitCapacity];
        private int _RecentCount;
        private int _RecentNext;

        internal void AddWait(double waitMs)
        {
            WaitSumMs += waitMs;
            WaitCount++;
            if (waitMs > WaitMaxMs) WaitMaxMs = waitMs;

            _RecentWaits[_RecentNext] = waitMs;
            _RecentNext = (_RecentNext + 1) % RecentWaitCapacity;
            if (_RecentCount < RecentWaitCapacity) _RecentCount++;
        }

        internal double P95WaitMs()
        {
            if (_RecentCount == 0) return 0;
            double[] copy = new double[_RecentCount];
            Array.Copy(_RecentWaits, copy, _RecentCount);
            Array.Sort(copy);
            int index = (int)Math.Ceiling(0.95 * _RecentCount) - 1;
            return copy[Math.Clamp(index, 0, _RecentCount - 1)];
        }
    }
}
