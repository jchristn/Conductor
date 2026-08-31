namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Owns the per-virtual-model-runner QoS admission runtimes and scheduler loops. It classifies each
    /// request, parks it in the profile's queues, and releases it in the discipline's order as an
    /// endpoint slot frees. Admission gates against the runner's total endpoint capacity via a semaphore;
    /// an unbounded runner is a transparent pass-through. Thread-safe. Best-effort: a compile failure
    /// fails open (the runner admits without queueing) rather than blocking traffic.
    /// </summary>
    public sealed class QosAdmissionService : IAsyncDisposable
    {
        private static readonly string _Header = "[QosAdmissionService] ";

        private readonly QosProfileCompiler _Compiler;
        private readonly IQosCapacityResolver _CapacityResolver;
        private readonly Func<string, CancellationToken, Task<QosProfile>> _ProfileLoader;
        private readonly LoggingModule _Logging;
        private readonly ConcurrentDictionary<string, QosVmrState> _States = new ConcurrentDictionary<string, QosVmrState>();
        private readonly CancellationTokenSource _ServiceCts = new CancellationTokenSource();
        private int _Disposed;

        /// <summary>
        /// Instantiate the admission service.
        /// </summary>
        /// <param name="compiler">Profile compiler. Must not be null.</param>
        /// <param name="capacityResolver">Capacity resolver. Must not be null.</param>
        /// <param name="profileLoader">Loads a profile aggregate by id. Must not be null.</param>
        /// <param name="logging">Logging module. Nullable.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public QosAdmissionService(
            QosProfileCompiler compiler,
            IQosCapacityResolver capacityResolver,
            Func<string, CancellationToken, Task<QosProfile>> profileLoader,
            LoggingModule logging = null)
        {
            _Compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            _CapacityResolver = capacityResolver ?? throw new ArgumentNullException(nameof(capacityResolver));
            _ProfileLoader = profileLoader ?? throw new ArgumentNullException(nameof(profileLoader));
            _Logging = logging;
        }

        /// <summary>
        /// Admit a request through the runner's QoS profile, waiting in queue order until a slot frees.
        /// </summary>
        /// <param name="vmr">The resolved virtual model runner. Nullable (null admits immediately).</param>
        /// <param name="ctx">Classification context. Nullable.</param>
        /// <param name="requestAborted">Token that fires when the client disconnects.</param>
        /// <returns>The admission result; call <see cref="QosAdmissionResult.Complete"/> when admitted.</returns>
        public async Task<QosAdmissionResult> AdmitAsync(VirtualModelRunner vmr, QosClassificationContext ctx, CancellationToken requestAborted)
        {
            if (vmr == null) return QosAdmissionResult.ForAdmitted(null, null);

            QosVmrState state;
            try
            {
                state = await GetOrBuildStateAsync(vmr).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging?.Warn(_Header + "failed to build QoS runtime for vmr=" + vmr.Id + "; admitting without queueing: " + ex.Message);
                return QosAdmissionResult.ForAdmitted(null, null);
            }

            QosRuntime runtime = state.Runtime;
            if (runtime == null) return QosAdmissionResult.ForAdmitted(null, null);

            string className = SafeClassify(runtime, ctx);
            SemaphoreSlim sem = state.CapacitySem;

            QosAdmissionTicket ticket = new QosAdmissionTicket
            {
                ClassKey = String.IsNullOrEmpty(className) ? "default" : className,
                TenantId = ctx?.TenantId,
                UserId = ctx?.UserId,
                CredentialId = ctx?.CredentialId,
                Model = ctx?.Model,
                RequestAborted = requestAborted
            };

            if (runtime.MaxTotalDepth > 0 && Volatile.Read(ref state.ParkedCount) >= runtime.MaxTotalDepth)
                return RejectAndEmit(state, runtime, className, QosAdmissionOutcomeEnum.Rejected, "total_depth");

            Interlocked.Increment(ref state.ParkedCount);
            ConductorTelemetry.QosQueueDepth.Add(1, DepthTags(state));
            ticket.EnqueuedTicks = Stopwatch.GetTimestamp();

            bool enqueued;
            try { enqueued = runtime.Enqueue(ticket); }
            catch { enqueued = false; }

            if (!enqueued)
            {
                Interlocked.Decrement(ref state.ParkedCount);
                ConductorTelemetry.QosQueueDepth.Add(-1, DepthTags(state));
                return RejectAndEmit(state, runtime, className, QosAdmissionOutcomeEnum.Rejected, "queue_full");
            }

            using (CancellationTokenSource waitCts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, _ServiceCts.Token))
            {
                Task releaseTask = ticket.Release.Task;
                Task delayTask = runtime.MaxQueueWaitMs > 0
                    ? Task.Delay(runtime.MaxQueueWaitMs, waitCts.Token)
                    : Task.Delay(Timeout.Infinite, waitCts.Token);

                Task completed = await Task.WhenAny(releaseTask, delayTask).ConfigureAwait(false);
                if (completed == releaseTask)
                {
                    EmitAdmitted(state, ticket, className);
                    return QosAdmissionResult.ForAdmitted(className, MakeComplete(sem));
                }

                int prior = Interlocked.CompareExchange(ref ticket.Settled, 2, 0);
                if (prior == 1)
                {
                    EmitAdmitted(state, ticket, className);
                    return QosAdmissionResult.ForAdmitted(className, MakeComplete(sem));
                }

                QosAdmissionOutcomeEnum outcome = requestAborted.IsCancellationRequested
                    ? QosAdmissionOutcomeEnum.Aborted
                    : QosAdmissionOutcomeEnum.TimedOut;
                return RejectAndEmit(state, runtime, className, outcome, outcome == QosAdmissionOutcomeEnum.Aborted ? "aborted" : "wait_timeout");
            }
        }

        /// <summary>
        /// Force runtimes bound to the given profile to rebuild on their next admission.
        /// </summary>
        /// <param name="profileId">The profile id that changed. Nullable (no-op).</param>
        public void Invalidate(string profileId)
        {
            if (String.IsNullOrEmpty(profileId)) return;
            foreach (QosVmrState state in _States.Values)
            {
                if (String.Equals(state.ProfileId, profileId, StringComparison.Ordinal)) state.Invalidated = true;
            }
        }

        private static Action MakeComplete(SemaphoreSlim sem)
        {
            if (sem == null) return () => { };
            return () =>
            {
                try { sem.Release(); }
                catch (SemaphoreFullException) { /* over-release guard */ }
                catch (ObjectDisposedException) { /* rebuilt */ }
            };
        }

        private string SafeClassify(QosRuntime runtime, QosClassificationContext ctx)
        {
            try { return runtime.Classifier(ctx); }
            catch (Exception ex)
            {
                _Logging?.Warn(_Header + "classifier threw for profile=" + runtime.ProfileId + ": " + ex.Message);
                return "default";
            }
        }

        private static TagList DepthTags(QosVmrState state)
        {
            return new TagList { { ConductorTelemetry.TagVmr, state.VmrName ?? state.VmrId } };
        }

        private static void EmitAdmitted(QosVmrState state, QosAdmissionTicket ticket, string className)
        {
            string vmr = state.VmrName ?? state.VmrId;
            ConductorTelemetry.QosAdmissions.Add(1, new TagList
            {
                { ConductorTelemetry.TagVmr, vmr },
                { ConductorTelemetry.TagQosClass, className },
                { ConductorTelemetry.TagOutcome, "admitted" }
            });

            double waitSeconds = (Stopwatch.GetTimestamp() - ticket.EnqueuedTicks) / (double)Stopwatch.Frequency;
            if (waitSeconds < 0) waitSeconds = 0;
            ConductorTelemetry.QosQueueWaitDuration.Record(waitSeconds, new TagList
            {
                { ConductorTelemetry.TagVmr, vmr },
                { ConductorTelemetry.TagQosClass, className }
            });
        }

        private static QosAdmissionResult RejectAndEmit(QosVmrState state, QosRuntime runtime, string className, QosAdmissionOutcomeEnum outcome, string reason)
        {
            string vmr = state.VmrName ?? state.VmrId;
            ConductorTelemetry.QosAdmissions.Add(1, new TagList
            {
                { ConductorTelemetry.TagVmr, vmr },
                { ConductorTelemetry.TagQosClass, className },
                { ConductorTelemetry.TagOutcome, outcome.ToString().ToLowerInvariant() }
            });
            ConductorTelemetry.QosRejections.Add(1, new TagList
            {
                { ConductorTelemetry.TagVmr, vmr },
                { ConductorTelemetry.TagReason, reason }
            });
            return Reject(runtime, className, outcome, reason);
        }

        private static QosAdmissionResult Reject(QosRuntime runtime, string className, QosAdmissionOutcomeEnum outcome, string reason)
        {
            return QosAdmissionResult.ForRejection(
                outcome,
                className,
                runtime.RejectionStatusCode,
                runtime.IncludeRetryAfter,
                runtime.RetryAfterSeconds,
                reason);
        }

        private async Task<QosVmrState> GetOrBuildStateAsync(VirtualModelRunner vmr)
        {
            QosVmrState state = _States.GetOrAdd(vmr.Id, id => new QosVmrState(id));
            string desiredProfileId = vmr.QosProfileId;

            if (!state.Invalidated
                && String.Equals(state.ProfileId, desiredProfileId, StringComparison.Ordinal)
                && (state.Runtime != null || String.IsNullOrEmpty(desiredProfileId)))
            {
                return state;
            }

            await state.BuildLock.WaitAsync(_ServiceCts.Token).ConfigureAwait(false);
            try
            {
                if (!state.Invalidated
                    && String.Equals(state.ProfileId, desiredProfileId, StringComparison.Ordinal)
                    && (state.Runtime != null || String.IsNullOrEmpty(desiredProfileId)))
                {
                    return state;
                }

                await StopSchedulerAsync(state).ConfigureAwait(false);
                if (state.Runtime != null)
                {
                    try { await state.Runtime.DisposeAsync().ConfigureAwait(false); } catch { /* best effort */ }
                    state.Runtime = null;
                }

                QosProfile profile = null;
                if (!String.IsNullOrEmpty(desiredProfileId))
                {
                    profile = await _ProfileLoader(desiredProfileId, _ServiceCts.Token).ConfigureAwait(false);
                }

                state.ProfileId = desiredProfileId;
                state.Invalidated = false;

                if (profile == null || !profile.Active)
                {
                    state.Runtime = null;
                    state.CapacitySem = null;
                    state.TotalCapacity = 0;
                    return state;
                }

                QosRuntime runtime;
                try
                {
                    runtime = _Compiler.Compile(profile);
                    await runtime.StartAsync(_ServiceCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _Logging?.Warn(_Header + "compile failed for profile=" + profile.Id + "; runner admits without queueing: " + ex.Message);
                    state.Runtime = null;
                    state.CapacitySem = null;
                    state.TotalCapacity = 0;
                    return state;
                }

                int capacity = await _CapacityResolver.GetTotalCapacityAsync(vmr, _ServiceCts.Token).ConfigureAwait(false);
                SemaphoreSlim sem = capacity > 0 ? new SemaphoreSlim(capacity, capacity) : null;
                CancellationTokenSource schedCts = CancellationTokenSource.CreateLinkedTokenSource(_ServiceCts.Token);

                state.TotalCapacity = capacity;
                state.CapacitySem = sem;
                state.SchedulerCts = schedCts;
                state.ParkedCount = 0;
                state.Runtime = runtime;
                state.VmrName = vmr.Name;
                state.SchedulerTask = Task.Run(() => RunSchedulerAsync(state, runtime, sem, schedCts.Token));

                return state;
            }
            finally
            {
                state.BuildLock.Release();
            }
        }

        private async Task RunSchedulerAsync(QosVmrState state, QosRuntime runtime, SemaphoreSlim sem, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    QosAdmissionTicket ticket = await runtime.Tail.DequeueAsync(ct).ConfigureAwait(false);
                    Interlocked.Decrement(ref state.ParkedCount);
                    ConductorTelemetry.QosQueueDepth.Add(-1, DepthTags(state));

                    if (Volatile.Read(ref ticket.Settled) == 2) continue;
                    if (ticket.RequestAborted.IsCancellationRequested)
                    {
                        Interlocked.CompareExchange(ref ticket.Settled, 2, 0);
                        continue;
                    }

                    if (sem != null) await sem.WaitAsync(ct).ConfigureAwait(false);

                    if (Interlocked.CompareExchange(ref ticket.Settled, 1, 0) == 0)
                    {
                        ticket.Release.TrySetResult(true);
                    }
                    else
                    {
                        if (sem != null)
                        {
                            try { sem.Release(); } catch (SemaphoreFullException) { }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // scheduler stopped
            }
            catch (Exception ex)
            {
                _Logging?.Warn(_Header + "scheduler for vmr=" + state.VmrId + " stopped on error: " + ex.Message);
            }
        }

        private static async Task StopSchedulerAsync(QosVmrState state)
        {
            if (state.SchedulerCts != null)
            {
                try { state.SchedulerCts.Cancel(); } catch { }
            }
            if (state.SchedulerTask != null)
            {
                try { await state.SchedulerTask.ConfigureAwait(false); } catch { }
            }
            if (state.SchedulerCts != null)
            {
                state.SchedulerCts.Dispose();
                state.SchedulerCts = null;
            }
            state.SchedulerTask = null;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _Disposed, 1) == 1) return;

            try { _ServiceCts.Cancel(); } catch { }

            foreach (QosVmrState state in _States.Values)
            {
                await StopSchedulerAsync(state).ConfigureAwait(false);
                if (state.Runtime != null)
                {
                    try { await state.Runtime.DisposeAsync().ConfigureAwait(false); } catch { }
                }
            }

            _ServiceCts.Dispose();
        }
    }
}
