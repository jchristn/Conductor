namespace Conductor.Server.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Per-virtual-model-runner QoS admission state: the compiled runtime, the resizable capacity gate, the
    /// supervised scheduler task draining the tail, the waiting-request counter, and the runtime statistics.
    /// The gate and statistics live for the life of the state and survive runtime rebuilds, so a profile or
    /// capacity change never strands a held permit or resets the counters. Managed by
    /// <see cref="QosAdmissionService"/>; fields targeted by Interlocked are exposed directly.
    /// </summary>
    internal sealed class QosVmrState
    {
        /// <summary>The virtual model runner id. Never null.</summary>
        public string VmrId { get; }

        /// <summary>The virtual model runner display name, used in metric tags. Nullable.</summary>
        public string VmrName { get; set; }

        /// <summary>The tenant id of the runner. Nullable.</summary>
        public string TenantId { get; set; }

        /// <summary>Guards (re)build of the runtime and scheduler, and capacity refresh. Never held across a request's queue wait.</summary>
        public SemaphoreSlim BuildLock { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Excludes enqueues (read) from a runtime swap and drain (write). Held only around synchronous work,
        /// never across an await.
        /// </summary>
        public ReaderWriterLockSlim RuntimeLock { get; } = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        /// <summary>The compiled runtime, or null when the runner is a pass-through (no active profile or a compile failure).</summary>
        public QosRuntime Runtime { get; set; }

        /// <summary>The profile id the current runtime was built from. Nullable.</summary>
        public string ProfileId { get; set; }

        /// <summary>The profile name the current runtime was built from. Nullable.</summary>
        public string ProfileName { get; set; }

        /// <summary>The capacity gate. Never null; capacity zero means unbounded.</summary>
        public QosCapacityGate Gate { get; } = new QosCapacityGate(0);

        /// <summary>Runtime statistics. Never null.</summary>
        public QosRuntimeStats Stats { get; }

        /// <summary>Cancels the scheduler loop on rebuild or dispose.</summary>
        public CancellationTokenSource SchedulerCts { get; set; }

        /// <summary>
        /// A ticket the scheduler had dequeued but not yet granted when it was stopped. The next rebuild moves it
        /// into the new runtime ahead of the drained tickets, since it was at the head of the queue. Interlocked target.
        /// </summary>
        public QosAdmissionTicket InterruptedTicket;

        /// <summary>The scheduler task draining the tail. Nullable.</summary>
        public Task SchedulerTask { get; set; }

        /// <summary>Current number of requests waiting for admission. Interlocked target.</summary>
        public int WaitingCount;

        /// <summary>Scheduler faults recovered from. Interlocked target.</summary>
        public long SchedulerFaultCount;

        /// <summary>True while the scheduler is backing off after a fault.</summary>
        public volatile bool SchedulerRecovering;

        /// <summary>Message of the most recent scheduler fault. Nullable.</summary>
        public volatile string LastSchedulerError;

        /// <summary>Ticks (UTC) of the most recent scheduler fault; zero when none. Interlocked target.</summary>
        public long LastSchedulerErrorTicks;

        /// <summary>Environment tick count at the last capacity refresh. Interlocked target.</summary>
        public long CapacityCheckedTicks;

        /// <summary>Environment tick count at the last runtime build attempt. Interlocked target.</summary>
        public long LastBuildAttemptTicks;

        /// <summary>True when the last runtime build threw; the build is retried after the refresh interval.</summary>
        public volatile bool LastBuildFailed;

        /// <summary>Set when a linked profile changes, forcing a rebuild on the next admission.</summary>
        public volatile bool Invalidated;

        /// <summary>Set when endpoint configuration changes, forcing a capacity refresh on the next admission.</summary>
        public volatile bool CapacityStale = true;

        /// <summary>
        /// Instantiate the state for a runner.
        /// </summary>
        /// <param name="vmrId">The runner id. Must not be null.</param>
        /// <param name="statsRetentionMinutes">Statistics retention in minutes.</param>
        public QosVmrState(string vmrId, int statsRetentionMinutes)
        {
            VmrId = vmrId ?? throw new ArgumentNullException(nameof(vmrId));
            Stats = new QosRuntimeStats(statsRetentionMinutes);
        }
    }
}
