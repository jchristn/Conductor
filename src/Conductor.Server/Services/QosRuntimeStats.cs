namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Conductor.Core.Models;

    /// <summary>
    /// In-memory QoS admission statistics for one virtual model runner: per-class totals since server start and
    /// one-minute buckets for the retention window. Thread-safe; every operation takes one short lock and does
    /// constant or bounded work (the bucket map is pruned to the retention window on each write).
    /// </summary>
    public sealed class QosRuntimeStats
    {
        /// <summary>
        /// Number of one-minute buckets retained. Minimum 1, default 1440 (24 hours).
        /// </summary>
        public int RetentionMinutes
        {
            get => _RetentionMinutes;
            set => _RetentionMinutes = (value < 1 ? 1440 : value);
        }

        private readonly object _Lock = new object();
        private readonly Func<DateTime> _UtcNow;
        private readonly Dictionary<string, QosClassAccumulator> _Classes = new Dictionary<string, QosClassAccumulator>(StringComparer.OrdinalIgnoreCase);
        private readonly SortedDictionary<long, Dictionary<string, QosBucketAccumulator>> _Buckets = new SortedDictionary<long, Dictionary<string, QosBucketAccumulator>>();
        private int _RetentionMinutes = 1440;

        /// <summary>
        /// Instantiate the statistics.
        /// </summary>
        /// <param name="retentionMinutes">Number of one-minute buckets retained. Values below 1 use the default of 1440.</param>
        /// <param name="utcNow">Clock. Null uses <see cref="DateTime.UtcNow"/>.</param>
        public QosRuntimeStats(int retentionMinutes = 1440, Func<DateTime> utcNow = null)
        {
            RetentionMinutes = retentionMinutes;
            _UtcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// Record that a request of the class started waiting in the queue.
        /// </summary>
        /// <param name="className">Traffic class. Null or empty records as "default".</param>
        public void RecordWaitStarted(string className)
        {
            lock (_Lock)
            {
                QosClassAccumulator cls = GetClass(className);
                cls.Waiting++;
                QosBucketAccumulator bucket = GetBucket(className, _UtcNow());
                if (cls.Waiting > bucket.PeakWaiting) bucket.PeakWaiting = cls.Waiting;
            }
        }

        /// <summary>
        /// Record the outcome of a request that was waiting in the queue, ending its wait.
        /// </summary>
        /// <param name="className">Traffic class. Null or empty records as "default".</param>
        /// <param name="outcome">Admission outcome.</param>
        /// <param name="waitMs">Time spent waiting, in milliseconds. Negative values count as zero.</param>
        public void RecordWaitEnded(string className, QosAdmissionOutcomeEnum outcome, double waitMs)
        {
            lock (_Lock)
            {
                QosClassAccumulator cls = GetClass(className);
                if (cls.Waiting > 0) cls.Waiting--;
                RecordOutcome(cls, className, outcome, waitMs);
            }
        }

        /// <summary>
        /// Record the outcome of a request that never waited (for example, rejected on arrival because the queue was full).
        /// </summary>
        /// <param name="className">Traffic class. Null or empty records as "default".</param>
        /// <param name="outcome">Admission outcome.</param>
        public void RecordImmediate(string className, QosAdmissionOutcomeEnum outcome)
        {
            lock (_Lock)
            {
                RecordOutcome(GetClass(className), className, outcome, 0);
            }
        }

        /// <summary>
        /// Record that an admitted request timed out waiting for a free endpoint slot.
        /// </summary>
        /// <param name="className">Traffic class. Null or empty records as "default".</param>
        public void RecordEndpointSlotTimeout(string className)
        {
            lock (_Lock)
            {
                QosClassAccumulator cls = GetClass(className);
                DateTime now = _UtcNow();
                cls.EndpointSlotTimeouts++;
                cls.LastRejectedUtc = now;
                GetBucket(className, now).EndpointSlotTimeouts++;
            }
        }

        /// <summary>
        /// Snapshot the per-class totals.
        /// </summary>
        /// <returns>Per-class statistics ordered by class name. Never null.</returns>
        public List<QosClassRuntimeSnapshot> SnapshotClasses()
        {
            lock (_Lock)
            {
                return _Classes
                    .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kvp => new QosClassRuntimeSnapshot
                    {
                        ClassName = kvp.Key,
                        Waiting = kvp.Value.Waiting,
                        Admitted = kvp.Value.Admitted,
                        Rejected = kvp.Value.Rejected,
                        TimedOut = kvp.Value.TimedOut,
                        Aborted = kvp.Value.Aborted,
                        EndpointSlotTimeouts = kvp.Value.EndpointSlotTimeouts,
                        AverageWaitMs = kvp.Value.WaitCount > 0 ? kvp.Value.WaitSumMs / kvp.Value.WaitCount : 0,
                        P95WaitMs = kvp.Value.P95WaitMs(),
                        MaxWaitMs = kvp.Value.WaitMaxMs,
                        LastAdmittedUtc = kvp.Value.LastAdmittedUtc,
                        LastRejectedUtc = kvp.Value.LastRejectedUtc
                    })
                    .ToList();
            }
        }

        /// <summary>
        /// Build time-bucketed history for a window, aggregating the one-minute buckets into the interval.
        /// </summary>
        /// <param name="startUtc">Window start (inclusive). Aligned down to the interval.</param>
        /// <param name="endUtc">Window end (exclusive).</param>
        /// <param name="intervalMinutes">Bucket size in minutes. Minimum 1.</param>
        /// <returns>Buckets with activity, ordered by timestamp then class name. Never null.</returns>
        public List<QosRuntimeHistoryBucket> BuildHistory(DateTime startUtc, DateTime endUtc, int intervalMinutes)
        {
            if (intervalMinutes < 1) intervalMinutes = 1;
            long startMinute = (long)Math.Floor(TotalMinutes(startUtc)) / intervalMinutes * intervalMinutes;
            long endMinuteExclusive = (long)Math.Ceiling(TotalMinutes(endUtc));

            SortedDictionary<long, Dictionary<string, QosBucketAccumulator>> merged = new SortedDictionary<long, Dictionary<string, QosBucketAccumulator>>();
            lock (_Lock)
            {
                foreach (KeyValuePair<long, Dictionary<string, QosBucketAccumulator>> minute in _Buckets)
                {
                    if (minute.Key < startMinute) continue;
                    if (minute.Key >= endMinuteExclusive) break;

                    long bucketMinute = minute.Key / intervalMinutes * intervalMinutes;
                    if (!merged.TryGetValue(bucketMinute, out Dictionary<string, QosBucketAccumulator> targets))
                    {
                        targets = new Dictionary<string, QosBucketAccumulator>(StringComparer.OrdinalIgnoreCase);
                        merged[bucketMinute] = targets;
                    }

                    foreach (KeyValuePair<string, QosBucketAccumulator> cls in minute.Value)
                    {
                        if (!targets.TryGetValue(cls.Key, out QosBucketAccumulator target))
                        {
                            target = new QosBucketAccumulator();
                            targets[cls.Key] = target;
                        }

                        target.Merge(cls.Value);
                    }
                }
            }

            List<QosRuntimeHistoryBucket> result = new List<QosRuntimeHistoryBucket>();
            foreach (KeyValuePair<long, Dictionary<string, QosBucketAccumulator>> bucket in merged)
            {
                DateTime timestampUtc = DateTime.SpecifyKind(DateTime.UnixEpoch.AddMinutes(bucket.Key), DateTimeKind.Utc);
                foreach (KeyValuePair<string, QosBucketAccumulator> cls in bucket.Value.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(new QosRuntimeHistoryBucket
                    {
                        TimestampUtc = timestampUtc,
                        ClassName = cls.Key,
                        Admitted = cls.Value.Admitted,
                        Rejected = cls.Value.Rejected,
                        TimedOut = cls.Value.TimedOut,
                        Aborted = cls.Value.Aborted,
                        EndpointSlotTimeouts = cls.Value.EndpointSlotTimeouts,
                        AverageWaitMs = cls.Value.WaitCount > 0 ? cls.Value.WaitSumMs / cls.Value.WaitCount : 0,
                        MaxWaitMs = cls.Value.WaitMaxMs,
                        PeakWaiting = cls.Value.PeakWaiting
                    });
                }
            }

            return result;
        }

        private void RecordOutcome(QosClassAccumulator cls, string className, QosAdmissionOutcomeEnum outcome, double waitMs)
        {
            if (waitMs < 0) waitMs = 0;
            DateTime now = _UtcNow();
            QosBucketAccumulator bucket = GetBucket(className, now);

            switch (outcome)
            {
                case QosAdmissionOutcomeEnum.Admitted:
                    cls.Admitted++;
                    cls.AddWait(waitMs);
                    cls.LastAdmittedUtc = now;
                    bucket.Admitted++;
                    bucket.WaitSumMs += waitMs;
                    bucket.WaitCount++;
                    if (waitMs > bucket.WaitMaxMs) bucket.WaitMaxMs = waitMs;
                    break;
                case QosAdmissionOutcomeEnum.TimedOut:
                    cls.TimedOut++;
                    cls.LastRejectedUtc = now;
                    bucket.TimedOut++;
                    break;
                case QosAdmissionOutcomeEnum.Aborted:
                    cls.Aborted++;
                    cls.LastRejectedUtc = now;
                    bucket.Aborted++;
                    break;
                default:
                    cls.Rejected++;
                    cls.LastRejectedUtc = now;
                    bucket.Rejected++;
                    break;
            }
        }

        private QosClassAccumulator GetClass(string className)
        {
            string key = String.IsNullOrEmpty(className) ? "default" : className;
            if (!_Classes.TryGetValue(key, out QosClassAccumulator cls))
            {
                cls = new QosClassAccumulator();
                _Classes[key] = cls;
            }

            return cls;
        }

        private QosBucketAccumulator GetBucket(string className, DateTime utcNow)
        {
            string key = String.IsNullOrEmpty(className) ? "default" : className;
            long minute = MinuteKey(utcNow);

            if (!_Buckets.TryGetValue(minute, out Dictionary<string, QosBucketAccumulator> classes))
            {
                classes = new Dictionary<string, QosBucketAccumulator>(StringComparer.OrdinalIgnoreCase);
                _Buckets[minute] = classes;
                Prune(minute);
            }

            if (!classes.TryGetValue(key, out QosBucketAccumulator bucket))
            {
                bucket = new QosBucketAccumulator();
                classes[key] = bucket;
            }

            return bucket;
        }

        private void Prune(long currentMinute)
        {
            long oldestKept = currentMinute - _RetentionMinutes + 1;
            while (_Buckets.Count > 0)
            {
                long first = _Buckets.Keys.First();
                if (first >= oldestKept) break;
                _Buckets.Remove(first);
            }
        }

        private static long MinuteKey(DateTime utc)
        {
            return (long)Math.Floor(TotalMinutes(utc));
        }

        private static double TotalMinutes(DateTime utc)
        {
            DateTime normalized = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc;
            return (normalized - DateTime.UnixEpoch).TotalMinutes;
        }
    }
}
