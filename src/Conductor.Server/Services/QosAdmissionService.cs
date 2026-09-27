namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;
    using Conductor.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Owns the per-virtual-model-runner QoS admission runtimes and scheduler loops. It classifies each
    /// request, parks it in the profile's queues, and releases it in the discipline's order as a capacity
    /// permit frees. Admission gates against the runner's total endpoint capacity via a resizable
    /// <see cref="QosCapacityGate"/> that is refreshed when endpoint configuration changes; an unbounded runner
    /// is a transparent pass-through.
    /// Thread-safe. Robustness guarantees: the scheduler loop is supervised and restarts with backoff after an
    /// unexpected fault; every ticket is settled exactly once (admitted, rejected, timed out, or aborted) through
    /// an atomic compare-and-swap so a permit is never lost or double-granted; a profile rebuild compiles the new
    /// runtime before retiring the old one and moves parked requests into it instead of dropping them; no lock
    /// is held across a request's queue wait. Best-effort: a compile failure fails open (the runner admits
    /// without queueing) rather than blocking traffic.
    /// </summary>
    public sealed class QosAdmissionService : IAsyncDisposable
    {
        /// <summary>
        /// How often, in milliseconds, a runner's capacity is re-read from its endpoints, and how often a failed
        /// runtime build is retried. Minimum 250, default 5000. Endpoint changes also force a refresh through
        /// <see cref="InvalidateCapacity"/>.
        /// </summary>
        public int CapacityRefreshIntervalMs
        {
            get => _CapacityRefreshIntervalMs;
            set => _CapacityRefreshIntervalMs = (value < 250 ? 250 : value);
        }

        /// <summary>
        /// Initial delay, in milliseconds, before a faulted scheduler loop resumes. Doubles per consecutive fault up
        /// to <see cref="SchedulerMaxRestartDelayMs"/>. Minimum 10, default 100.
        /// </summary>
        public int SchedulerRestartDelayMs
        {
            get => _SchedulerRestartDelayMs;
            set => _SchedulerRestartDelayMs = (value < 10 ? 10 : value);
        }

        /// <summary>
        /// Maximum delay, in milliseconds, between scheduler restarts after consecutive faults. Minimum 10, default 5000.
        /// </summary>
        public int SchedulerMaxRestartDelayMs
        {
            get => _SchedulerMaxRestartDelayMs;
            set => _SchedulerMaxRestartDelayMs = (value < 10 ? 10 : value);
        }

        /// <summary>
        /// Minutes of per-minute runtime statistics retained per runner. Minimum 1, default 1440 (24 hours).
        /// Applies to runners first seen after the value is set.
        /// </summary>
        public int StatsRetentionMinutes
        {
            get => _StatsRetentionMinutes;
            set => _StatsRetentionMinutes = (value < 1 ? 1440 : value);
        }

        private static readonly string _Header = "[QosAdmissionService] ";

        private readonly QosProfileCompiler _Compiler;
        private readonly IQosCapacityResolver _CapacityResolver;
        private readonly Func<string, CancellationToken, Task<QosProfile>> _ProfileLoader;
        private readonly LoggingModule _Logging;
        private readonly Func<QosProfile, QosRuntime> _RuntimeFactory;
        private readonly ConcurrentDictionary<string, QosVmrState> _States = new ConcurrentDictionary<string, QosVmrState>();
        private readonly CancellationTokenSource _ServiceCts = new CancellationTokenSource();
        private int _CapacityRefreshIntervalMs = 5000;
        private int _SchedulerRestartDelayMs = 100;
        private int _SchedulerMaxRestartDelayMs = 5000;
        private int _StatsRetentionMinutes = 1440;
        private int _Disposed;

        /// <summary>
        /// Instantiate the admission service.
        /// </summary>
        /// <param name="compiler">Profile compiler. Must not be null.</param>
        /// <param name="capacityResolver">Capacity resolver. Must not be null.</param>
        /// <param name="profileLoader">Loads a profile aggregate by id. Must not be null.</param>
        /// <param name="logging">Logging module. Nullable.</param>
        /// <param name="runtimeFactory">
        /// Builds a runtime from a profile in place of <paramref name="compiler"/>, for custom compilation or testing.
        /// Nullable; when null the compiler is used. May throw to signal a compile failure (the runner then admits without queueing).
        /// </param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public QosAdmissionService(
            QosProfileCompiler compiler,
            IQosCapacityResolver capacityResolver,
            Func<string, CancellationToken, Task<QosProfile>> profileLoader,
            LoggingModule logging = null,
            Func<QosProfile, QosRuntime> runtimeFactory = null)
        {
            _Compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            _CapacityResolver = capacityResolver ?? throw new ArgumentNullException(nameof(capacityResolver));
            _ProfileLoader = profileLoader ?? throw new ArgumentNullException(nameof(profileLoader));
            _Logging = logging;
            _RuntimeFactory = runtimeFactory;
        }

        /// <summary>
        /// Admit a request through the runner's QoS profile, waiting in queue order until a capacity permit frees.
        /// </summary>
        /// <param name="vmr">The resolved virtual model runner. Nullable (null admits immediately).</param>
        /// <param name="ctx">Classification context. Nullable.</param>
        /// <param name="requestAborted">Token that fires when the client disconnects.</param>
        /// <returns>
        /// The admission result. When admitted, call <see cref="QosAdmissionResult.Complete"/> once the request
        /// finishes to return the permit; extra calls are ignored.
        /// </returns>
        public async Task<QosAdmissionResult> AdmitAsync(VirtualModelRunner vmr, QosClassificationContext ctx, CancellationToken requestAborted)
        {
            if (vmr == null || Volatile.Read(ref _Disposed) == 1) return QosAdmissionResult.ForAdmitted(null, null);

            QosVmrState state;
            try
            {
                state = await GetOrBuildStateAsync(vmr, requestAborted).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                return QosAdmissionResult.ForRejection(QosAdmissionOutcomeEnum.Aborted, null, 429, false, 0, "aborted");
            }
            catch (Exception ex)
            {
                _Logging?.Warn(_Header + "failed to build QoS runtime for vmr=" + vmr.Id + "; admitting without queueing: " + ex.Message);
                return QosAdmissionResult.ForAdmitted(null, null);
            }

            QosRuntime runtime;
            QosAdmissionTicket ticket = null;
            string className = null;
            bool depthExceeded = false;
            bool enqueued = false;

            try
            {
                state.RuntimeLock.EnterReadLock();
            }
            catch (ObjectDisposedException)
            {
                // The service is shutting down.
                return QosAdmissionResult.ForAdmitted(null, null);
            }

            try
            {
                runtime = state.Runtime;
                if (runtime != null)
                {
                    className = SafeClassify(runtime, ctx);
                    if (runtime.MaxTotalDepth > 0 && Volatile.Read(ref state.WaitingCount) >= runtime.MaxTotalDepth)
                    {
                        depthExceeded = true;
                    }
                    else
                    {
                        ticket = new QosAdmissionTicket
                        {
                            ClassKey = className,
                            TenantId = ctx?.TenantId,
                            UserId = ctx?.UserId,
                            CredentialId = ctx?.CredentialId,
                            Model = ctx?.Model,
                            RequestAborted = requestAborted,
                            EnqueuedTicks = Stopwatch.GetTimestamp()
                        };

                        Interlocked.Increment(ref state.WaitingCount);
                        try { enqueued = runtime.Enqueue(ticket); }
                        catch (Exception ex)
                        {
                            _Logging?.Debug(_Header + "enqueue threw for vmr=" + state.VmrId + ": " + ex.Message);
                            enqueued = false;
                        }

                        if (!enqueued) Interlocked.Decrement(ref state.WaitingCount);
                    }
                }
            }
            finally
            {
                state.RuntimeLock.ExitReadLock();
            }

            if (runtime == null)
            {
                // Pass-through: no active profile, so no queue and no permit.
                return QosAdmissionResult.ForAdmitted(null, null);
            }

            if (depthExceeded)
            {
                state.Stats.RecordImmediate(className, QosAdmissionOutcomeEnum.Rejected);
                return RejectAndEmit(state, runtime, className, QosAdmissionOutcomeEnum.Rejected, "total_depth");
            }

            if (!enqueued)
            {
                state.Stats.RecordImmediate(className, QosAdmissionOutcomeEnum.Rejected);
                return RejectAndEmit(state, runtime, className, QosAdmissionOutcomeEnum.Rejected, "queue_full");
            }

            state.Stats.RecordWaitStarted(className);
            ConductorTelemetry.QosQueueDepth.Add(1, DepthTags(state));

            bool granted = false;
            QosAdmissionOutcomeEnum outcome = QosAdmissionOutcomeEnum.TimedOut;
            string reason = "wait_timeout";

            try
            {
                using (CancellationTokenSource waitCts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, _ServiceCts.Token))
                {
                    Task<bool> releaseTask = ticket.Release.Task;
                    Task delayTask = runtime.MaxQueueWaitMs > 0
                        ? Task.Delay(runtime.MaxQueueWaitMs, waitCts.Token)
                        : Task.Delay(Timeout.Infinite, waitCts.Token);

                    Task completed = await Task.WhenAny(releaseTask, delayTask).ConfigureAwait(false);
                    if (completed == releaseTask)
                    {
                        granted = releaseTask.Result;
                        if (!granted)
                        {
                            outcome = QosAdmissionOutcomeEnum.Rejected;
                            reason = "service_rejected";
                        }
                    }
                    else
                    {
                        int prior = Interlocked.CompareExchange(ref ticket.Settled, 2, 0);
                        if (prior == 1)
                        {
                            // The scheduler granted a permit just as the wait ended; the permit is ours.
                            granted = true;
                        }
                        else if (prior == 2)
                        {
                            outcome = QosAdmissionOutcomeEnum.Rejected;
                            reason = "service_rejected";
                        }
                        else
                        {
                            ticket.Abandoned.Cancel();
                            if (requestAborted.IsCancellationRequested)
                            {
                                outcome = QosAdmissionOutcomeEnum.Aborted;
                                reason = "aborted";
                            }
                            else if (_ServiceCts.IsCancellationRequested)
                            {
                                outcome = QosAdmissionOutcomeEnum.Rejected;
                                reason = "shutdown";
                            }
                        }
                    }

                    // Stop the deadline timer promptly instead of letting it run to expiry.
                    waitCts.Cancel();
                }
            }
            catch (Exception ex)
            {
                // Unexpected: settle the ticket so the scheduler cannot grant it later, and return any permit it already won.
                int prior = Interlocked.CompareExchange(ref ticket.Settled, 2, 0);
                if (prior == 1) state.Gate.Release();
                ticket.Abandoned.Cancel();
                granted = false;
                outcome = QosAdmissionOutcomeEnum.Rejected;
                reason = "internal_error";
                _Logging?.Warn(_Header + "admission wait failed for vmr=" + state.VmrId + ": " + ex.Message);
            }
            finally
            {
                Interlocked.Decrement(ref state.WaitingCount);
                ConductorTelemetry.QosQueueDepth.Add(-1, DepthTags(state));
            }

            double waitMs = ElapsedMs(ticket.EnqueuedTicks);
            state.Stats.RecordWaitEnded(className, granted ? QosAdmissionOutcomeEnum.Admitted : outcome, waitMs);

            if (!granted)
            {
                QosAdmissionResult rejection = RejectAndEmit(state, runtime, className, outcome, reason);
                rejection.WaitMs = waitMs;
                return rejection;
            }

            EmitAdmitted(state, className, waitMs);
            DateTime? deadlineUtc = runtime.MaxQueueWaitMs > 0
                ? DateTime.UtcNow.AddMilliseconds(Math.Max(0, runtime.MaxQueueWaitMs - waitMs))
                : (DateTime?)null;

            QosCapacityGate gate = state.Gate;
            QosAdmissionResult admitted = QosAdmissionResult.ForAdmitted(className, () => gate.Release(), deadlineUtc);
            admitted.WaitMs = waitMs;
            return admitted;
        }

        /// <summary>
        /// Force runtimes bound to the given profile to rebuild on their next admission. Parked requests are moved
        /// into the rebuilt runtime.
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

        /// <summary>
        /// Force every runner to re-read its capacity from its endpoints on its next admission. Call when endpoint,
        /// endpoint group, or virtual model runner configuration changes. Cheap and non-blocking.
        /// </summary>
        public void InvalidateCapacity()
        {
            foreach (QosVmrState state in _States.Values)
            {
                state.CapacityStale = true;
            }
        }

        /// <summary>
        /// Build a live snapshot of a runner's QoS runtime. Endpoint concurrency is not included; the caller adds it.
        /// </summary>
        /// <param name="vmr">The virtual model runner. Must not be null.</param>
        /// <returns>The snapshot. Never null; a runner that has not admitted a request since server start reports state Idle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when vmr is null.</exception>
        public QosRuntimeSnapshot GetSnapshot(VirtualModelRunner vmr)
        {
            if (vmr == null) throw new ArgumentNullException(nameof(vmr));

            QosRuntimeSnapshot snapshot = new QosRuntimeSnapshot
            {
                TenantId = vmr.TenantId,
                VirtualModelRunnerId = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                QosProfileId = vmr.QosProfileId,
                SchedulerState = "Idle"
            };

            if (!_States.TryGetValue(vmr.Id, out QosVmrState state)) return snapshot;

            QosRuntime runtime = state.Runtime;
            long errorTicks = Interlocked.Read(ref state.LastSchedulerErrorTicks);

            snapshot.QosProfileId = state.ProfileId ?? vmr.QosProfileId;
            snapshot.QosProfileName = state.ProfileName;
            snapshot.SchedulerState = runtime == null ? "PassThrough" : (state.SchedulerRecovering ? "Recovering" : "Running");
            snapshot.SchedulerFaultCount = Interlocked.Read(ref state.SchedulerFaultCount);
            snapshot.LastSchedulerError = state.LastSchedulerError;
            snapshot.LastSchedulerErrorUtc = errorTicks > 0 ? new DateTime(errorTicks, DateTimeKind.Utc) : (DateTime?)null;
            snapshot.Capacity = state.Gate.Capacity;
            snapshot.InUse = state.Gate.InUse;
            snapshot.Waiting = Math.Max(0, Volatile.Read(ref state.WaitingCount));
            snapshot.MaxQueueWaitMs = runtime?.MaxQueueWaitMs ?? 0;
            snapshot.Classes = state.Stats.SnapshotClasses();
            return snapshot;
        }

        /// <summary>
        /// Build time-bucketed admission history for a runner.
        /// </summary>
        /// <param name="vmrId">Virtual model runner id. Nullable (returns empty).</param>
        /// <param name="startUtc">Window start.</param>
        /// <param name="endUtc">Window end (exclusive).</param>
        /// <param name="intervalMinutes">Bucket size in minutes. Minimum 1.</param>
        /// <returns>Buckets with activity. Never null.</returns>
        public List<QosRuntimeHistoryBucket> GetHistory(string vmrId, DateTime startUtc, DateTime endUtc, int intervalMinutes)
        {
            if (String.IsNullOrEmpty(vmrId) || !_States.TryGetValue(vmrId, out QosVmrState state)) return new List<QosRuntimeHistoryBucket>();
            return state.Stats.BuildHistory(startUtc, endUtc, intervalMinutes);
        }

        /// <summary>
        /// Record that a request admitted by QoS then timed out waiting for a free endpoint slot.
        /// </summary>
        /// <param name="vmrId">Virtual model runner id. Nullable (no-op).</param>
        /// <param name="className">Traffic class. Nullable.</param>
        public void RecordEndpointSlotTimeout(string vmrId, string className)
        {
            if (String.IsNullOrEmpty(vmrId) || !_States.TryGetValue(vmrId, out QosVmrState state)) return;
            state.Stats.RecordEndpointSlotTimeout(className);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _Disposed, 1) == 1) return;

            try { _ServiceCts.Cancel(); } catch (ObjectDisposedException) { }

            foreach (QosVmrState state in _States.Values)
            {
                await StopSchedulerAsync(state).ConfigureAwait(false);

                QosRuntime runtime = state.Runtime;
                if (runtime != null)
                {
                    try { await runtime.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception ex) { _Logging?.Debug(_Header + "runtime dispose failed for vmr=" + state.VmrId + ": " + ex.Message); }
                }

                state.Gate.Dispose();
                try { state.RuntimeLock.Dispose(); }
                catch (SynchronizationLockException) { /* an admission still holds the read side; it exits on its own */ }
            }

            _ServiceCts.Dispose();
        }

        private async Task<QosVmrState> GetOrBuildStateAsync(VirtualModelRunner vmr, CancellationToken requestAborted)
        {
            QosVmrState state = _States.GetOrAdd(vmr.Id, id => new QosVmrState(id, _StatsRetentionMinutes));
            state.VmrName = vmr.Name;
            state.TenantId = vmr.TenantId;

            if (NeedsBuild(state, vmr.QosProfileId))
            {
                using (CancellationTokenSource waitCts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, _ServiceCts.Token))
                {
                    await state.BuildLock.WaitAsync(waitCts.Token).ConfigureAwait(false);
                }

                try
                {
                    if (NeedsBuild(state, vmr.QosProfileId))
                    {
                        await RebuildAsync(state, vmr).ConfigureAwait(false);
                        state.LastBuildFailed = false;
                    }
                }
                catch (Exception ex) when (!_ServiceCts.IsCancellationRequested)
                {
                    // The current runtime (if any) keeps serving; the build is retried after the refresh interval.
                    state.LastBuildFailed = true;
                    _Logging?.Warn(_Header + "runtime build failed for vmr=" + vmr.Id + "; keeping the current runtime: " + ex.Message);
                }
                finally
                {
                    state.BuildLock.Release();
                }
            }
            else if (CapacityRefreshDue(state) && state.BuildLock.Wait(0))
            {
                // Non-blocking: if another request is already refreshing or rebuilding, admit against the current capacity.
                try
                {
                    await RefreshCapacityAsync(state, vmr).ConfigureAwait(false);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException && _ServiceCts.IsCancellationRequested))
                {
                    _Logging?.Debug(_Header + "capacity refresh failed for vmr=" + vmr.Id + ": " + ex.Message);
                }
                finally
                {
                    state.BuildLock.Release();
                }
            }

            return state;
        }

        private bool NeedsBuild(QosVmrState state, string desiredProfileId)
        {
            bool retryWindowElapsed = Environment.TickCount64 - Interlocked.Read(ref state.LastBuildAttemptTicks) >= _CapacityRefreshIntervalMs;
            if (state.LastBuildFailed && !retryWindowElapsed) return false;
            if (state.Invalidated) return true;
            if (!String.Equals(state.ProfileId, desiredProfileId, StringComparison.Ordinal)) return true;
            if (state.Runtime != null || String.IsNullOrEmpty(desiredProfileId)) return false;

            // A previous build produced no runtime (profile missing or failed to compile): retry periodically, not on every request.
            return retryWindowElapsed;
        }

        private bool CapacityRefreshDue(QosVmrState state)
        {
            return state.CapacityStale
                || Environment.TickCount64 - Interlocked.Read(ref state.CapacityCheckedTicks) >= _CapacityRefreshIntervalMs;
        }

        private async Task RefreshCapacityAsync(QosVmrState state, VirtualModelRunner vmr)
        {
            state.CapacityStale = false;
            int capacity = await _CapacityResolver.GetTotalCapacityAsync(vmr, _ServiceCts.Token).ConfigureAwait(false);
            state.Gate.SetCapacity(state.Runtime == null ? 0 : capacity);
            Interlocked.Exchange(ref state.CapacityCheckedTicks, Environment.TickCount64);
        }

        private async Task RebuildAsync(QosVmrState state, VirtualModelRunner vmr)
        {
            string desiredProfileId = vmr.QosProfileId;
            Interlocked.Exchange(ref state.LastBuildAttemptTicks, Environment.TickCount64);

            // 1. Build the replacement first; on any failure the current runtime keeps serving untouched.
            QosProfile profile = null;
            if (!String.IsNullOrEmpty(desiredProfileId))
            {
                profile = await _ProfileLoader(desiredProfileId, _ServiceCts.Token).ConfigureAwait(false);
            }

            QosRuntime fresh = null;
            if (profile != null && profile.Active)
            {
                try
                {
                    fresh = _RuntimeFactory != null ? _RuntimeFactory(profile) : _Compiler.Compile(profile);
                    await fresh.StartAsync(_ServiceCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (!_ServiceCts.IsCancellationRequested)
                {
                    _Logging?.Warn(_Header + "compile failed for profile=" + profile.Id + "; runner admits without queueing: " + ex.Message);
                    if (fresh != null) await DisposeQuietlyAsync(fresh, state.VmrId).ConfigureAwait(false);
                    fresh = null;
                }
            }

            int capacity = state.Gate.Capacity;
            try
            {
                capacity = await _CapacityResolver.GetTotalCapacityAsync(vmr, _ServiceCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!_ServiceCts.IsCancellationRequested)
            {
                _Logging?.Warn(_Header + "capacity lookup failed for vmr=" + vmr.Id + "; keeping capacity " + capacity + ": " + ex.Message);
            }

            // 2. Retire the current runtime's scheduler and links so nothing moves while it is drained.
            QosRuntime old = state.Runtime;
            await StopSchedulerAsync(state).ConfigureAwait(false);
            if (old != null) await old.StopLinksAsync().ConfigureAwait(false);

            // 3. Swap and move parked tickets under the write lock; enqueues wait on the read side, so none is lost.
            List<QosAdmissionTicket> orphaned = new List<QosAdmissionTicket>();
            state.RuntimeLock.EnterWriteLock();
            try
            {
                List<QosAdmissionTicket> parked = new List<QosAdmissionTicket>();
                QosAdmissionTicket interrupted = Interlocked.Exchange(ref state.InterruptedTicket, null);
                if (interrupted != null) parked.Add(interrupted);
                if (old != null) parked.AddRange(old.DrainParked());
                state.Runtime = fresh;
                state.ProfileId = desiredProfileId;
                state.ProfileName = profile?.Name;
                state.Invalidated = false;

                foreach (QosAdmissionTicket ticket in parked)
                {
                    if (Volatile.Read(ref ticket.Settled) != 0) continue;

                    bool moved = false;
                    if (fresh != null)
                    {
                        try { moved = fresh.Enqueue(ticket); }
                        catch (Exception ex) { _Logging?.Debug(_Header + "re-enqueue threw for vmr=" + state.VmrId + ": " + ex.Message); }
                    }

                    if (!moved) orphaned.Add(ticket);
                }
            }
            finally
            {
                state.RuntimeLock.ExitWriteLock();
            }

            state.Gate.SetCapacity(fresh == null ? 0 : capacity);
            state.CapacityStale = false;
            Interlocked.Exchange(ref state.CapacityCheckedTicks, Environment.TickCount64);

            // 4. Settle tickets that could not be moved: grant them when the runner is now a pass-through, else reject.
            foreach (QosAdmissionTicket ticket in orphaned)
            {
                if (fresh == null && state.Gate.TryTake())
                {
                    if (Interlocked.CompareExchange(ref ticket.Settled, 1, 0) == 0) ticket.Release.TrySetResult(true);
                    else state.Gate.Release();
                }
                else if (Interlocked.CompareExchange(ref ticket.Settled, 2, 0) == 0)
                {
                    ticket.Release.TrySetResult(false);
                }
            }

            // 5. Start the new scheduler, then dispose the retired runtime.
            if (fresh != null) StartScheduler(state, fresh);
            if (old != null) await DisposeQuietlyAsync(old, state.VmrId).ConfigureAwait(false);

            if (orphaned.Count > 0)
            {
                _Logging?.Warn(_Header + "vmr=" + state.VmrId + " rebuild could not move " + orphaned.Count + " parked request(s) into the new runtime");
            }
        }

        private void StartScheduler(QosVmrState state, QosRuntime runtime)
        {
            CancellationTokenSource schedCts = CancellationTokenSource.CreateLinkedTokenSource(_ServiceCts.Token);
            state.SchedulerCts = schedCts;
            state.SchedulerRecovering = false;
            state.SchedulerTask = Task.Run(() => RunSchedulerAsync(state, runtime, schedCts.Token));
        }

        private async Task RunSchedulerAsync(QosVmrState state, QosRuntime runtime, CancellationToken ct)
        {
            int consecutiveFaults = 0;

            while (!ct.IsCancellationRequested)
            {
                QosAdmissionTicket ticket = null;
                bool holdsPermit = false;

                try
                {
                    ticket = await runtime.Tail.DequeueAsync(ct).ConfigureAwait(false);

                    if (IsDead(ticket))
                    {
                        Interlocked.CompareExchange(ref ticket.Settled, 2, 0);
                        continue;
                    }

                    using (CancellationTokenSource permitCts = CancellationTokenSource.CreateLinkedTokenSource(ct, ticket.RequestAborted, ticket.Abandoned.Token))
                    {
                        try
                        {
                            await state.Gate.WaitAsync(permitCts.Token).ConfigureAwait(false);
                            holdsPermit = true;
                        }
                        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                        {
                            // The waiter gave up or disconnected while this ticket waited for a permit; no permit is held.
                            Interlocked.CompareExchange(ref ticket.Settled, 2, 0);
                            continue;
                        }
                    }

                    if (Interlocked.CompareExchange(ref ticket.Settled, 1, 0) == 0)
                    {
                        holdsPermit = false;
                        ticket.Release.TrySetResult(true);
                    }

                    if (consecutiveFaults > 0)
                    {
                        consecutiveFaults = 0;
                        state.SchedulerRecovering = false;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    HandBack(state, ticket, holdsPermit);
                    break;
                }
                catch (ObjectDisposedException) when (ct.IsCancellationRequested)
                {
                    HandBack(state, ticket, holdsPermit);
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveFaults++;
                    state.SchedulerRecovering = true;
                    Interlocked.Increment(ref state.SchedulerFaultCount);
                    Interlocked.Exchange(ref state.LastSchedulerErrorTicks, DateTime.UtcNow.Ticks);
                    state.LastSchedulerError = ex.GetType().Name + ": " + ex.Message;
                    _Logging?.Warn(_Header + "scheduler for vmr=" + state.VmrId + " faulted (" + consecutiveFaults + " consecutive), restarting: " + ex);

                    if (ticket != null && !holdsPermit) RequeueOrReject(state, runtime, ticket);

                    int delayMs = (int)Math.Min(_SchedulerMaxRestartDelayMs, (long)_SchedulerRestartDelayMs << Math.Min(consecutiveFaults - 1, 16));
                    try
                    {
                        await Task.Delay(delayMs, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                finally
                {
                    if (holdsPermit) state.Gate.Release();
                }
            }
        }

        private static void HandBack(QosVmrState state, QosAdmissionTicket ticket, bool holdsPermit)
        {
            // Stopped while holding a dequeued ticket that was not granted: keep it for the next rebuild, which moves
            // it into the new runtime. A held permit is returned by the loop's finally block.
            if (ticket == null || holdsPermit || Volatile.Read(ref ticket.Settled) != 0) return;
            Interlocked.Exchange(ref state.InterruptedTicket, ticket);
        }

        private void RequeueOrReject(QosVmrState state, QosRuntime runtime, QosAdmissionTicket ticket)
        {
            if (Volatile.Read(ref ticket.Settled) != 0) return;

            bool requeued = false;
            try
            {
                state.RuntimeLock.EnterReadLock();
                try
                {
                    if (ReferenceEquals(state.Runtime, runtime)) requeued = runtime.Enqueue(ticket);
                }
                finally
                {
                    state.RuntimeLock.ExitReadLock();
                }
            }
            catch (Exception ex)
            {
                _Logging?.Debug(_Header + "requeue after fault failed for vmr=" + state.VmrId + ": " + ex.Message);
            }

            if (!requeued && Interlocked.CompareExchange(ref ticket.Settled, 2, 0) == 0)
            {
                ticket.Release.TrySetResult(false);
            }
        }

        private static bool IsDead(QosAdmissionTicket ticket)
        {
            return Volatile.Read(ref ticket.Settled) != 0
                || ticket.RequestAborted.IsCancellationRequested
                || ticket.Abandoned.IsCancellationRequested;
        }

        private async Task StopSchedulerAsync(QosVmrState state)
        {
            CancellationTokenSource cts = state.SchedulerCts;
            Task task = state.SchedulerTask;
            state.SchedulerCts = null;
            state.SchedulerTask = null;

            if (cts != null)
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
            }

            if (task != null)
            {
                try { await task.ConfigureAwait(false); }
                catch (Exception ex) { _Logging?.Debug(_Header + "scheduler for vmr=" + state.VmrId + " ended with: " + ex.Message); }
            }

            cts?.Dispose();
        }

        private async Task DisposeQuietlyAsync(QosRuntime runtime, string vmrId)
        {
            try { await runtime.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { _Logging?.Debug(_Header + "runtime dispose failed for vmr=" + vmrId + ": " + ex.Message); }
        }

        private string SafeClassify(QosRuntime runtime, QosClassificationContext ctx)
        {
            string className;
            try { className = runtime.Classifier(ctx); }
            catch (Exception ex)
            {
                _Logging?.Warn(_Header + "classifier threw for profile=" + runtime.ProfileId + ": " + ex.Message);
                className = null;
            }

            return String.IsNullOrEmpty(className) ? "default" : className;
        }

        private static double ElapsedMs(long startTicks)
        {
            double ms = (Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency;
            return ms < 0 ? 0 : ms;
        }

        private static TagList DepthTags(QosVmrState state)
        {
            return new TagList { { ConductorTelemetry.TagVmr, state.VmrName ?? state.VmrId } };
        }

        private static void EmitAdmitted(QosVmrState state, string className, double waitMs)
        {
            string vmr = state.VmrName ?? state.VmrId;
            ConductorTelemetry.QosAdmissions.Add(1, new TagList
            {
                { ConductorTelemetry.TagVmr, vmr },
                { ConductorTelemetry.TagQosClass, className },
                { ConductorTelemetry.TagOutcome, "admitted" }
            });

            ConductorTelemetry.QosQueueWaitDuration.Record(waitMs / 1000.0, new TagList
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

            return QosAdmissionResult.ForRejection(
                outcome,
                className,
                runtime.RejectionStatusCode,
                runtime.IncludeRetryAfter,
                runtime.RetryAfterSeconds,
                reason);
        }
    }
}
