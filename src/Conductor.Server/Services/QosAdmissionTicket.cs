namespace Conductor.Server.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A lightweight admission ticket carried through the QoS queues. It holds only what the queue
    /// selectors read plus a release signal — never the request body or HTTP context — so a deep
    /// backlog stays cheap and nothing about the payload reaches a metric tag. Not thread-safe beyond
    /// its <see cref="Release"/> completion source.
    /// </summary>
    public sealed class QosAdmissionTicket
    {
        /// <summary>The profile-classified traffic class. Never null.</summary>
        public string ClassKey { get; set; } = "default";

        /// <summary>Tenant identifier, for flow-source resolution. Nullable.</summary>
        public string TenantId { get; set; }

        /// <summary>User identifier, for flow-source resolution. Nullable.</summary>
        public string UserId { get; set; }

        /// <summary>Credential identifier, for flow-source resolution. Nullable.</summary>
        public string CredentialId { get; set; }

        /// <summary>Model name, for flow-source resolution. Nullable.</summary>
        public string Model { get; set; }

        /// <summary>Scheduling cost; default 1.</summary>
        public int Cost { get; set; } = 1;

        /// <summary>Monotonic timestamp (Stopwatch ticks) captured at enqueue, for wait-time metrics.</summary>
        public long EnqueuedTicks { get; set; }

        /// <summary>Token when the client aborts the request.</summary>
        public CancellationToken RequestAborted { get; set; }

        /// <summary>
        /// Settle state used to race the waiter (timeout/abort) against the scheduler (admit) with a
        /// single atomic winner: 0 = pending, 1 = admitted, 2 = abandoned. Mutate only via Interlocked.
        /// </summary>
        public int Settled;

        /// <summary>Completed with true when the ticket is admitted.</summary>
        public TaskCompletionSource<bool> Release { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
