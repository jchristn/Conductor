namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using QoSKit;

    /// <summary>
    /// A compiled, runnable QoS profile: the classifier, the ingress enqueue routing, the queue nodes,
    /// the optional pipeline that moves work to the tail, and the profile's admission limits. Thread-safe
    /// for concurrent enqueue and a single scheduler draining the tail.
    /// </summary>
    public sealed class QosRuntime : IAsyncDisposable
    {
        /// <summary>The profile id this runtime was compiled from. Never null.</summary>
        public string ProfileId { get; }

        /// <summary>A monotonic build stamp used to detect a stale cached runtime. Never null.</summary>
        public string BuildStamp { get; }

        /// <summary>Classifies a request into a traffic class name. Never null.</summary>
        public Func<QosClassificationContext, string> Classifier { get; }

        /// <summary>Enqueues a ticket at ingress, returning false when overflow rejects it. Never null.</summary>
        public Func<QosAdmissionTicket, bool> Enqueue { get; }

        /// <summary>The tail queue the scheduler drains. Never null.</summary>
        public IQoSQueue<QosAdmissionTicket> Tail { get; }

        /// <summary>Maximum total parked requests; 0 means unbounded.</summary>
        public int MaxTotalDepth { get; }

        /// <summary>Maximum wait before admission, in milliseconds; 0 means no deadline.</summary>
        public int MaxQueueWaitMs { get; }

        /// <summary>HTTP status returned on rejection.</summary>
        public int RejectionStatusCode { get; }

        /// <summary>Whether a Retry-After header is included on rejection.</summary>
        public bool IncludeRetryAfter { get; }

        /// <summary>Retry-After value in seconds.</summary>
        public int RetryAfterSeconds { get; }

        private readonly QoSPipeline<QosAdmissionTicket> _Pipeline;
        private readonly List<IQoSQueue<QosAdmissionTicket>> _Nodes;
        private int _Started;
        private int _Disposed;

        /// <summary>
        /// Instantiate a runtime. Intended for use by <see cref="QosProfileCompiler"/>.
        /// </summary>
        /// <param name="profileId">Profile id. Must not be null.</param>
        /// <param name="buildStamp">Build stamp. Must not be null.</param>
        /// <param name="classifier">Classifier delegate. Must not be null.</param>
        /// <param name="enqueue">Ingress enqueue delegate. Must not be null.</param>
        /// <param name="tail">Tail queue. Must not be null.</param>
        /// <param name="pipeline">Optional pipeline; null for a single-node profile.</param>
        /// <param name="nodes">All node queues, for disposal. Must not be null.</param>
        /// <param name="maxTotalDepth">Max total depth.</param>
        /// <param name="maxQueueWaitMs">Max wait milliseconds.</param>
        /// <param name="rejectionStatusCode">Rejection status.</param>
        /// <param name="includeRetryAfter">Include Retry-After.</param>
        /// <param name="retryAfterSeconds">Retry-After seconds.</param>
        public QosRuntime(
            string profileId,
            string buildStamp,
            Func<QosClassificationContext, string> classifier,
            Func<QosAdmissionTicket, bool> enqueue,
            IQoSQueue<QosAdmissionTicket> tail,
            QoSPipeline<QosAdmissionTicket> pipeline,
            List<IQoSQueue<QosAdmissionTicket>> nodes,
            int maxTotalDepth,
            int maxQueueWaitMs,
            int rejectionStatusCode,
            bool includeRetryAfter,
            int retryAfterSeconds)
        {
            ProfileId = profileId ?? throw new ArgumentNullException(nameof(profileId));
            BuildStamp = buildStamp ?? throw new ArgumentNullException(nameof(buildStamp));
            Classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            Enqueue = enqueue ?? throw new ArgumentNullException(nameof(enqueue));
            Tail = tail ?? throw new ArgumentNullException(nameof(tail));
            _Pipeline = pipeline;
            _Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            MaxTotalDepth = maxTotalDepth;
            MaxQueueWaitMs = maxQueueWaitMs;
            RejectionStatusCode = rejectionStatusCode;
            IncludeRetryAfter = includeRetryAfter;
            RetryAfterSeconds = retryAfterSeconds;
        }

        /// <summary>
        /// Start the pipeline pumps (no-op for a single-node profile).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task StartAsync(CancellationToken token = default)
        {
            if (Interlocked.Exchange(ref _Started, 1) == 1) return;
            if (_Pipeline != null) await _Pipeline.StartAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _Disposed, 1) == 1) return;

            if (_Pipeline != null)
            {
                try { await _Pipeline.DisposeAsync().ConfigureAwait(false); }
                catch { /* best effort */ }
            }

            foreach (IQoSQueue<QosAdmissionTicket> node in _Nodes)
            {
                try { await node.DisposeAsync().ConfigureAwait(false); }
                catch { /* best effort */ }
            }
        }
    }
}
