namespace Conductor.Server.Services
{
    /// <summary>
    /// Activity of one traffic class during one minute. Not thread-safe; guarded by the owning <see cref="QosRuntimeStats"/> lock.
    /// </summary>
    internal sealed class QosBucketAccumulator
    {
        internal long Admitted;
        internal long Rejected;
        internal long TimedOut;
        internal long Aborted;
        internal long EndpointSlotTimeouts;
        internal double WaitSumMs;
        internal long WaitCount;
        internal double WaitMaxMs;
        internal int PeakWaiting;

        internal void Merge(QosBucketAccumulator other)
        {
            Admitted += other.Admitted;
            Rejected += other.Rejected;
            TimedOut += other.TimedOut;
            Aborted += other.Aborted;
            EndpointSlotTimeouts += other.EndpointSlotTimeouts;
            WaitSumMs += other.WaitSumMs;
            WaitCount += other.WaitCount;
            if (other.WaitMaxMs > WaitMaxMs) WaitMaxMs = other.WaitMaxMs;
            if (other.PeakWaiting > PeakWaiting) PeakWaiting = other.PeakWaiting;
        }
    }
}
