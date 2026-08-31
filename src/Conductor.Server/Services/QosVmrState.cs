namespace Conductor.Server.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Per-virtual-model-runner QoS admission state: the compiled runtime, its capacity gate, the
    /// scheduler task draining the tail, and the parked-request counter. Managed by
    /// <see cref="QosAdmissionService"/>; fields targeted by Interlocked are exposed directly.
    /// </summary>
    internal sealed class QosVmrState
    {
        /// <summary>The virtual model runner id. Never null.</summary>
        public string VmrId { get; }

        /// <summary>The virtual model runner display name, used in metric tags. Nullable.</summary>
        public string VmrName { get; set; }

        /// <summary>Guards (re)build of the runtime and scheduler.</summary>
        public SemaphoreSlim BuildLock { get; } = new SemaphoreSlim(1, 1);

        /// <summary>The compiled runtime, or null when the runner is a pass-through (no profile).</summary>
        public QosRuntime Runtime { get; set; }

        /// <summary>The profile id the current runtime was built from. Nullable.</summary>
        public string ProfileId { get; set; }

        /// <summary>Total concurrent capacity; 0 means unbounded (pass-through).</summary>
        public int TotalCapacity { get; set; }

        /// <summary>The capacity gate; null when unbounded. Acquire before releasing a ticket; release on completion.</summary>
        public SemaphoreSlim CapacitySem { get; set; }

        /// <summary>Cancels the scheduler loop on rebuild or dispose.</summary>
        public CancellationTokenSource SchedulerCts { get; set; }

        /// <summary>The scheduler task draining the tail. Nullable.</summary>
        public Task SchedulerTask { get; set; }

        /// <summary>Current number of parked (enqueued, not yet admitted) requests. Interlocked target.</summary>
        public int ParkedCount;

        /// <summary>Set when a linked profile changes, forcing a rebuild on the next admission.</summary>
        public volatile bool Invalidated;

        /// <summary>
        /// Instantiate the state for a runner.
        /// </summary>
        /// <param name="vmrId">The runner id. Must not be null.</param>
        public QosVmrState(string vmrId)
        {
            VmrId = vmrId;
        }
    }
}
