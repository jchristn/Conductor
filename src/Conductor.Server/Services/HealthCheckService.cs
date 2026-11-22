namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Service for monitoring health of model runner endpoints.
    /// Implements IDisposable to properly clean up HttpClient and CancellationTokenSource resources.
    /// </summary>
    public class HealthCheckService : IDisposable
    {
        private static readonly string _Header = "[HealthCheckService] ";

        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly HttpClient _HttpClient;
        private readonly AnthropicSettings _AnthropicSettings;
        private readonly RigMonitorClient _RigMonitorClient;
        private readonly ConcurrentDictionary<string, EndpointHealthState> _HealthStates;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _CancellationTokens;
        private readonly ConcurrentDictionary<string, Task> _RunningTasks;
        private readonly ConcurrentDictionary<string, ModelRunnerEndpoint> _ActiveEndpoints;
        private readonly ConcurrentDictionary<string, string> _EndpointHealthCheckKeys;
        private readonly ConcurrentDictionary<string, bool> _ImmediateCheckRequested = new ConcurrentDictionary<string, bool>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _GroupWakeSignals = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
        private TaskCompletionSource<bool> _CapacityPulse = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenSource _GlobalCancellation;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the health check service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="httpMessageHandler">Optional HTTP message handler for tests.</param>
        /// <param name="anthropicSettings">Anthropic settings used for the anthropic-version header (optional); when null, defaults are used.</param>
        public HealthCheckService(DatabaseDriverBase database, LoggingModule logging, HttpMessageHandler httpMessageHandler = null, AnthropicSettings anthropicSettings = null)
        {
            _AnthropicSettings = anthropicSettings ?? new AnthropicSettings();
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _HttpClient = httpMessageHandler == null ? UpstreamHttpClientFactory.CreateClient() : new HttpClient(httpMessageHandler);
            _RigMonitorClient = new RigMonitorClient(_Logging);
            _HealthStates = new ConcurrentDictionary<string, EndpointHealthState>();
            _CancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
            _RunningTasks = new ConcurrentDictionary<string, Task>();
            _ActiveEndpoints = new ConcurrentDictionary<string, ModelRunnerEndpoint>();
            _EndpointHealthCheckKeys = new ConcurrentDictionary<string, string>();

            RegisterTelemetry();
        }

        /// <summary>
        /// Register observable gauges reporting live endpoint-health state to OpenTelemetry.
        /// The gauge callbacks take a point-in-time snapshot of the health-state map.
        /// </summary>
        private void RegisterTelemetry()
        {
            ConductorTelemetry.HealthMeter.CreateObservableGauge<int>(
                "conductor.health.endpoints.healthy",
                () => new Measurement<int>(_HealthStates.Values.Count(state => state.IsHealthy)),
                "{endpoint}",
                "Number of model runner endpoints currently reporting healthy.");

            ConductorTelemetry.HealthMeter.CreateObservableGauge<int>(
                "conductor.health.endpoints.unhealthy",
                () => new Measurement<int>(_HealthStates.Values.Count(state => !state.IsHealthy)),
                "{endpoint}",
                "Number of model runner endpoints currently reporting unhealthy.");

            ConductorTelemetry.HealthMeter.CreateObservableGauge<int>(
                "conductor.health.endpoints.total",
                () => new Measurement<int>(_HealthStates.Count),
                "{endpoint}",
                "Total number of monitored model runner endpoints.");

            ConductorTelemetry.HealthMeter.CreateObservableGauge<long>(
                "conductor.health.inflight.requests",
                () => new Measurement<long>(_HealthStates.Values.Sum(state => (long)state.InFlightRequests)),
                "{request}",
                "Total number of in-flight proxied requests across all endpoints.");
        }

        /// <summary>
        /// Start the health check service and begin monitoring all endpoints.
        /// </summary>
        public async Task StartAsync(CancellationToken token = default)
        {
            _GlobalCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Logging.Info(_Header + "starting");

            try
            {
                // Load all endpoints from database (null tenantId = all tenants)
                EnumerationResult<ModelRunnerEndpoint> result = await _Database.ModelRunnerEndpoint.EnumerateAsync(
                    null,
                    new EnumerationRequest { MaxResults = 10000 },
                    token).ConfigureAwait(false);

                if (result?.Data != null)
                {
                    foreach (ModelRunnerEndpoint endpoint in result.Data)
                    {
                        if (endpoint.Active)
                        {
                            StartHealthCheckTask(endpoint);
                        }
                    }
                    _Logging.Info(_Header + "started monitoring " + result.Data.Count(e => e.Active) + " active endpoints");
                }
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "start exception:" + Environment.NewLine + ex.ToString());
            }
        }

        /// <summary>
        /// Stop the health check service.
        /// </summary>
        public async Task StopAsync()
        {
            _Logging.Info(_Header + "stopping");
            _GlobalCancellation?.Cancel();

            // Cancel all individual tasks
            foreach (KeyValuePair<string, CancellationTokenSource> kvp in _CancellationTokens)
            {
                kvp.Value.Cancel();
            }

            // Wait for all tasks to complete (with timeout)
            try
            {
                List<Task> tasks = _RunningTasks.Values.ToList();
                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            _HealthStates.Clear();
            _CancellationTokens.Clear();
            _RunningTasks.Clear();
            _ActiveEndpoints.Clear();
            _EndpointHealthCheckKeys.Clear();
            _ImmediateCheckRequested.Clear();
            foreach (string key in _GroupWakeSignals.Keys.ToList()) WakeHealthCheckGroup(key);
            _Logging.Info(_Header + "stopped");
        }

        /// <summary>
        /// Get the health state for a specific endpoint.
        /// Includes current ongoing uptime/downtime since last state change.
        /// </summary>
        public EndpointHealthState GetHealthState(string endpointId)
        {
            if (String.IsNullOrEmpty(endpointId)) return null;

            if (_HealthStates.TryGetValue(endpointId, out EndpointHealthState state))
            {
                lock (state.Lock)
                {
                    EndpointHealthState copy = state.Copy();

                    // Add current ongoing uptime/downtime since last state change
                    if (copy.LastStateChangeUtc.HasValue)
                    {
                        long currentPeriodMs = (long)(DateTime.UtcNow - copy.LastStateChangeUtc.Value).TotalMilliseconds;
                        if (copy.IsHealthy)
                        {
                            copy.TotalUptimeMs += currentPeriodMs;
                        }
                        else
                        {
                            copy.TotalDowntimeMs += currentPeriodMs;
                        }
                    }

                    return copy;
                }
            }
            return null;
        }

        /// <summary>
        /// Get all health states for a specific tenant.
        /// Includes current ongoing uptime/downtime since last state change.
        /// </summary>
        public List<EndpointHealthState> GetAllHealthStates(string tenantId = null)
        {
            List<EndpointHealthState> states = new List<EndpointHealthState>();
            DateTime now = DateTime.UtcNow;

            foreach (KeyValuePair<string, EndpointHealthState> kvp in _HealthStates)
            {
                EndpointHealthState state = kvp.Value;
                if (tenantId == null || state.TenantId == tenantId)
                {
                    lock (state.Lock)
                    {
                        EndpointHealthState copy = state.Copy();

                        // Add current ongoing uptime/downtime since last state change
                        if (copy.LastStateChangeUtc.HasValue)
                        {
                            long currentPeriodMs = (long)(now - copy.LastStateChangeUtc.Value).TotalMilliseconds;
                            if (copy.IsHealthy)
                            {
                                copy.TotalUptimeMs += currentPeriodMs;
                            }
                            else
                            {
                                copy.TotalDowntimeMs += currentPeriodMs;
                            }
                        }

                        states.Add(copy);
                    }
                }
            }
            return states;
        }

        /// <summary>
        /// Get cached RigMonitor status for an endpoint.
        /// </summary>
        public RigMonitorEndpointStatus GetRigMonitorState(string endpointId)
        {
            if (String.IsNullOrEmpty(endpointId)) return null;
            if (_HealthStates.TryGetValue(endpointId, out EndpointHealthState state))
            {
                lock (state.Lock)
                {
                    return state.RigMonitor?.DeepCopy();
                }
            }

            return null;
        }

        /// <summary>
        /// Called when an endpoint is created.
        /// </summary>
        public void OnEndpointCreated(ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null || !endpoint.Active) return;
            _Logging.Debug(_Header + "endpoint created: " + endpoint.Id);
            StartHealthCheckTask(endpoint);
            PulseCapacity();
        }

        /// <summary>
        /// Called when an endpoint is updated.
        /// </summary>
        public void OnEndpointUpdated(ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null) return;
            _Logging.Debug(_Header + "endpoint updated: " + endpoint.Id);

            // Stop existing task
            StopHealthCheckTask(endpoint.Id);

            // Restart if still active
            if (endpoint.Active)
            {
                StartHealthCheckTask(endpoint);
            }

            PulseCapacity();
        }

        /// <summary>
        /// Called when an endpoint is deleted.
        /// </summary>
        public void OnEndpointDeleted(string endpointId)
        {
            if (String.IsNullOrEmpty(endpointId)) return;
            _Logging.Debug(_Header + "endpoint deleted: {endpointId}");
            StopHealthCheckTask(endpointId);
            _HealthStates.TryRemove(endpointId, out _);
            _ImmediateCheckRequested.TryRemove(endpointId, out _);
        }

        /// <summary>
        /// Try to increment the in-flight request count for an endpoint.
        /// Returns true if successful, false if at capacity.
        /// </summary>
        public bool TryIncrementInFlight(string endpointId, int maxParallel)
        {
            if (String.IsNullOrEmpty(endpointId)) return false;

            if (_HealthStates.TryGetValue(endpointId, out EndpointHealthState state))
            {
                lock (state.Lock)
                {
                    // 0 = unlimited
                    if (maxParallel == 0 || state.InFlightRequests < maxParallel)
                    {
                        state.InFlightRequests++;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Decrement the in-flight request count for an endpoint.
        /// </summary>
        public void DecrementInFlight(string endpointId)
        {
            if (String.IsNullOrEmpty(endpointId)) return;

            if (_HealthStates.TryGetValue(endpointId, out EndpointHealthState state))
            {
                lock (state.Lock)
                {
                    if (state.InFlightRequests > 0)
                    {
                        state.InFlightRequests--;
                    }
                }
            }

            PulseCapacity();
        }

        /// <summary>
        /// Wait until endpoint capacity may have changed (an in-flight request completed, an endpoint became
        /// healthy, or endpoint configuration changed), or until the wait elapses. Callers re-check capacity
        /// afterwards; a wake-up does not guarantee a free slot. Holds no lock while waiting.
        /// </summary>
        /// <param name="maxWaitMs">Maximum wait in milliseconds. Values below 1 return immediately.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task that completes on a capacity change or when the wait elapses.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
        public async Task WaitForCapacityChangeAsync(int maxWaitMs, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (maxWaitMs < 1) return;

            Task pulse = Volatile.Read(ref _CapacityPulse).Task;
            using (CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                Task delay = Task.Delay(maxWaitMs, delayCts.Token);
                await Task.WhenAny(pulse, delay).ConfigureAwait(false);
                delayCts.Cancel();
            }

            token.ThrowIfCancellationRequested();
        }

        private void PulseCapacity()
        {
            TaskCompletionSource<bool> previous = Interlocked.Exchange(
                ref _CapacityPulse,
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            previous.TrySetResult(true);
        }

        private void StartHealthCheckTask(ModelRunnerEndpoint endpoint)
        {
            if (!endpoint.HealthCheckEnabled)
            {
                MarkHealthCheckDisabled(endpoint);
                return;
            }

            EndpointHealthState state = null;
            if (_HealthStates.TryGetValue(endpoint.Id, out EndpointHealthState existingState))
            {
                lock (existingState.Lock)
                {
                    existingState.EndpointName = endpoint.Name;
                    existingState.TenantId = endpoint.TenantId;
                    existingState.ServiceState = endpoint.ServiceState;

                    if (IsRigMonitorActive(endpoint))
                    {
                        existingState.RigMonitor ??= new RigMonitorEndpointStatus();
                        existingState.RigMonitor.Enabled = true;
                        existingState.RigMonitor.BaseUrl = RigMonitorClient.GetBaseUrl(endpoint);
                    }
                    else
                    {
                        existingState.RigMonitor = null;
                    }

                    state = existingState;
                }
            }
            else
            {
                state = new EndpointHealthState
                {
                    EndpointId = endpoint.Id,
                    EndpointName = endpoint.Name,
                    TenantId = endpoint.TenantId,
                    IsHealthy = false,
                    FirstCheckUtc = DateTime.UtcNow,
                    LastStateChangeUtc = DateTime.UtcNow,
                    ServiceState = endpoint.ServiceState,
                    RigMonitor = IsRigMonitorActive(endpoint)
                        ? new RigMonitorEndpointStatus
                        {
                            Enabled = true,
                            BaseUrl = RigMonitorClient.GetBaseUrl(endpoint)
                        }
                        : null
                };
                _HealthStates.AddOrUpdate(endpoint.Id, state, (k, v) => state);
            }

            string healthCheckKey = BuildHealthCheckKey(endpoint);
            if (_EndpointHealthCheckKeys.TryGetValue(endpoint.Id, out string existingKey)
                && !String.Equals(existingKey, healthCheckKey, StringComparison.Ordinal))
            {
                _ActiveEndpoints.TryRemove(endpoint.Id, out _);
                _EndpointHealthCheckKeys.TryRemove(endpoint.Id, out _);
                StopHealthCheckGroupIfEmpty(existingKey);

                // The probe target (URL, method, auth or body) changed, so the last result no longer describes it: probe now.
                _ImmediateCheckRequested[endpoint.Id] = true;
            }

            _ActiveEndpoints.AddOrUpdate(endpoint.Id, endpoint, (k, v) => endpoint);
            _EndpointHealthCheckKeys.AddOrUpdate(endpoint.Id, healthCheckKey, (k, v) => healthCheckKey);
            EnsureHealthCheckGroupTask(healthCheckKey);

            // The group's loop may already be sleeping (for example because another endpoint with the same health check URL
            // was probed moments ago). Wake it so a newly registered endpoint, or one with an immediate probe request, is
            // probed now instead of after the current sleep.
            WakeHealthCheckGroup(healthCheckKey);

            _Logging.Info(_Header + $"health check registered for endpoint {endpoint.Id} ({endpoint.Name}) - interval: {endpoint.HealthCheckIntervalMs}ms, URL: {BuildHealthCheckUrl(endpoint)}");
        }

        private void MarkHealthCheckDisabled(ModelRunnerEndpoint endpoint)
        {
            // Health checks are off: send no probe and treat the endpoint as healthy.
            if (_EndpointHealthCheckKeys.TryRemove(endpoint.Id, out string existingKey))
            {
                _ActiveEndpoints.TryRemove(endpoint.Id, out _);
                StopHealthCheckGroupIfEmpty(existingKey);
            }
            else
            {
                _ActiveEndpoints.TryRemove(endpoint.Id, out _);
            }

            DateTime now = DateTime.UtcNow;
            EndpointHealthState state = _HealthStates.GetOrAdd(endpoint.Id, _ => new EndpointHealthState
            {
                EndpointId = endpoint.Id,
                FirstCheckUtc = now,
                LastStateChangeUtc = now
            });

            lock (state.Lock)
            {
                state.EndpointName = endpoint.Name;
                state.TenantId = endpoint.TenantId;
                state.ServiceState = endpoint.ServiceState;
                state.LastError = null;
                state.ConsecutiveFailures = 0;
                if (!state.IsHealthy)
                {
                    state.IsHealthy = true;
                    state.LastHealthyUtc = now;
                    state.LastStateChangeUtc = now;
                }
            }

            PulseCapacity();
            _Logging.Info(_Header + $"health checks disabled for endpoint {endpoint.Id} ({endpoint.Name}); treating it as healthy");
        }

        private void StopHealthCheckTask(string endpointId)
        {
            // Get endpoint name from health state before stopping
            string endpointName = null;
            if (_HealthStates.TryGetValue(endpointId, out EndpointHealthState state))
            {
                endpointName = state.EndpointName;
            }

            _ActiveEndpoints.TryRemove(endpointId, out _);
            if (_EndpointHealthCheckKeys.TryRemove(endpointId, out string healthCheckKey))
            {
                StopHealthCheckGroupIfEmpty(healthCheckKey);
            }

            _Logging.Info(_Header + $"health check stopped for endpoint {endpointId}" + (endpointName != null ? $" ({endpointName})" : ""));
        }

        private void EnsureHealthCheckGroupTask(string healthCheckKey)
        {
            if (String.IsNullOrWhiteSpace(healthCheckKey)) return;

            CancellationTokenSource cts = _CancellationTokens.GetOrAdd(healthCheckKey, _ => new CancellationTokenSource());
            _RunningTasks.AddOrUpdate(
                healthCheckKey,
                _ => Task.Run(async () => await HealthCheckLoop(healthCheckKey, cts.Token).ConfigureAwait(false)),
                (key, existingTask) =>
                {
                    if (existingTask != null && !existingTask.IsCompleted)
                    {
                        return existingTask;
                    }

                    CancellationTokenSource replacementCts = new CancellationTokenSource();
                    _CancellationTokens.AddOrUpdate(key, replacementCts, (k, existingCts) =>
                    {
                        existingCts.Cancel();
                        existingCts.Dispose();
                        return replacementCts;
                    });
                    return Task.Run(async () => await HealthCheckLoop(key, replacementCts.Token).ConfigureAwait(false));
                });
        }

        private void StopHealthCheckGroupIfEmpty(string healthCheckKey)
        {
            if (String.IsNullOrWhiteSpace(healthCheckKey)) return;
            if (HasActiveEndpointsForHealthCheckKey(healthCheckKey)) return;

            if (_CancellationTokens.TryRemove(healthCheckKey, out CancellationTokenSource cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            _RunningTasks.TryRemove(healthCheckKey, out _);
            WakeHealthCheckGroup(healthCheckKey);
        }

        private Task GetHealthCheckGroupWakeTask(string healthCheckKey)
        {
            return _GroupWakeSignals.GetOrAdd(
                healthCheckKey,
                _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }

        private void WakeHealthCheckGroup(string healthCheckKey)
        {
            if (String.IsNullOrWhiteSpace(healthCheckKey)) return;
            if (_GroupWakeSignals.TryRemove(healthCheckKey, out TaskCompletionSource<bool> signal))
            {
                signal.TrySetResult(true);
            }
        }

        private async Task WaitUntilNextDueOrWokenAsync(string healthCheckKey, CancellationToken token)
        {
            // Take the wake signal before computing the delay: a registration that happens after this point completes the
            // signal, and one that happened before it is already visible to the delay computation.
            Task wakeTask = GetHealthCheckGroupWakeTask(healthCheckKey);
            int delayMs = GetDelayUntilNextDueMs(healthCheckKey, DateTime.UtcNow);

            using (CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                Task delayTask = Task.Delay(delayMs, delayCts.Token);
                await Task.WhenAny(delayTask, wakeTask).ConfigureAwait(false);
                delayCts.Cancel();
            }

            token.ThrowIfCancellationRequested();
        }

        private bool HasActiveEndpointsForHealthCheckKey(string healthCheckKey)
        {
            foreach (KeyValuePair<string, string> kvp in _EndpointHealthCheckKeys)
            {
                if (String.Equals(kvp.Value, healthCheckKey, StringComparison.Ordinal)
                    && _ActiveEndpoints.ContainsKey(kvp.Key))
                {
                    return true;
                }
            }

            return false;
        }

        private List<ModelRunnerEndpoint> GetEndpointsForHealthCheckKey(string healthCheckKey)
        {
            List<ModelRunnerEndpoint> endpoints = new List<ModelRunnerEndpoint>();
            foreach (KeyValuePair<string, string> kvp in _EndpointHealthCheckKeys)
            {
                if (String.Equals(kvp.Value, healthCheckKey, StringComparison.Ordinal)
                    && _ActiveEndpoints.TryGetValue(kvp.Key, out ModelRunnerEndpoint endpoint))
                {
                    endpoints.Add(endpoint);
                }
            }

            return endpoints;
        }

        private List<ModelRunnerEndpoint> GetDueEndpoints(string healthCheckKey, DateTime now)
        {
            List<ModelRunnerEndpoint> dueEndpoints = new List<ModelRunnerEndpoint>();
            List<ModelRunnerEndpoint> endpoints = GetEndpointsForHealthCheckKey(healthCheckKey);
            foreach (ModelRunnerEndpoint endpoint in endpoints)
            {
                if (!_HealthStates.TryGetValue(endpoint.Id, out EndpointHealthState state))
                {
                    dueEndpoints.Add(endpoint);
                    continue;
                }

                if (GetRemainingMs(endpoint, state, now) < 1)
                {
                    dueEndpoints.Add(endpoint);
                }
            }

            return dueEndpoints;
        }

        private int GetDelayUntilNextDueMs(string healthCheckKey, DateTime now)
        {
            List<ModelRunnerEndpoint> endpoints = GetEndpointsForHealthCheckKey(healthCheckKey);
            int shortestDelayMs = 1000;

            foreach (ModelRunnerEndpoint endpoint in endpoints)
            {
                int remainingMs = 0;
                if (_HealthStates.TryGetValue(endpoint.Id, out EndpointHealthState state))
                {
                    remainingMs = GetRemainingMs(endpoint, state, now);
                }

                if (remainingMs < 1)
                {
                    return 1;
                }

                if (remainingMs < shortestDelayMs)
                {
                    shortestDelayMs = remainingMs;
                }
            }

            return shortestDelayMs;
        }

        private int GetRemainingMs(ModelRunnerEndpoint endpoint, EndpointHealthState state, DateTime now)
        {
            // An endpoint that has never been checked (new, just created, or just registered at startup), or whose probe
            // target changed, is checked immediately instead of after a full interval.
            if (_ImmediateCheckRequested.ContainsKey(endpoint.Id)) return 0;

            lock (state.Lock)
            {
                if (!state.LastCheckUtc.HasValue) return 0;
                return endpoint.HealthCheckIntervalMs - (int)(now - state.LastCheckUtc.Value).TotalMilliseconds;
            }
        }

        private async Task HealthCheckLoop(string healthCheckKey, CancellationToken token)
        {
            while (!token.IsCancellationRequested && !(_GlobalCancellation?.IsCancellationRequested ?? false))
            {
                try
                {
                    if (!HasActiveEndpointsForHealthCheckKey(healthCheckKey))
                    {
                        break;
                    }

                    await WaitUntilNextDueOrWokenAsync(healthCheckKey, token).ConfigureAwait(false);

                    if (token.IsCancellationRequested) break;

                    DateTime now = DateTime.UtcNow;
                    List<ModelRunnerEndpoint> dueEndpoints = GetDueEndpoints(healthCheckKey, now);
                    if (dueEndpoints.Count < 1)
                    {
                        continue;
                    }

                    ModelRunnerEndpoint probeEndpoint = dueEndpoints[0];
                    int timeoutMs = dueEndpoints.Max(item => item.HealthCheckTimeoutMs);
                    HealthCheckProbeResult probeResult = null;
                    Stopwatch stopwatch = Stopwatch.StartNew();

                    try
                    {
                        probeResult = await PerformHealthCheck(probeEndpoint, timeoutMs, token).ConfigureAwait(false);
                        stopwatch.Stop();
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        probeResult = new HealthCheckProbeResult
                        {
                            Error = ex.Message
                        };
                    }

                    _Logging.Debug(_Header + "shared health check completed for " + dueEndpoints.Count + " endpoint(s), key " + GetLogSafeHealthCheckKey(probeEndpoint) + " (" + stopwatch.Elapsed.TotalMilliseconds.ToString("F2") + "ms)");

                    foreach (ModelRunnerEndpoint endpoint in dueEndpoints)
                    {
                        _ImmediateCheckRequested.TryRemove(endpoint.Id, out _);
                        bool success = EvaluateHealthCheckResult(endpoint, probeResult, out string error);

                        if (success)
                        {
                            _Logging.Debug(_Header + "health check succeeded for endpoint " + endpoint.Id + " (" + endpoint.Name + ")");
                        }
                        else
                        {
                            _Logging.Debug(_Header + "health check failed for endpoint " + endpoint.Id + " (" + endpoint.Name + ")" + (!String.IsNullOrEmpty(error) ? ": " + error : ""));
                        }

                        if (IsRigMonitorActive(endpoint))
                        {
                            RigMonitorRefreshResult rigMonitorResult = await RefreshRigMonitorStatusAsync(endpoint, token).ConfigureAwait(false);
                            if (endpoint.RigMonitor.HealthAffectedByRigMonitor)
                            {
                                success = success && rigMonitorResult.Success;
                                if (!rigMonitorResult.Success && String.IsNullOrEmpty(error))
                                {
                                    error = rigMonitorResult.Error;
                                }
                            }
                        }

                        UpdateHealthState(endpoint, success, error);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "error for shared health check group " + healthCheckKey + Environment.NewLine + ex.ToString());
                }
            }
        }

        private async Task<RigMonitorRefreshResult> RefreshRigMonitorStatusAsync(ModelRunnerEndpoint endpoint, CancellationToken token)
        {
            if (endpoint == null || !IsRigMonitorActive(endpoint))
            {
                return new RigMonitorRefreshResult { Success = true };
            }

            if (!_HealthStates.TryGetValue(endpoint.Id, out EndpointHealthState state))
            {
                return new RigMonitorRefreshResult
                {
                    Success = false,
                    Error = "No endpoint health state is available."
                };
            }

            DateTime now = DateTime.UtcNow;
            RigMonitorEndpointStatus rigState;
            lock (state.Lock)
            {
                state.RigMonitor ??= new RigMonitorEndpointStatus();
                rigState = state.RigMonitor;
                rigState.Enabled = true;
                rigState.BaseUrl = RigMonitorClient.GetBaseUrl(endpoint);
            }

            bool success = true;
            string error = null;
            RigMonitorReadyStatus readyStatus = null;
            RigMonitorCapabilities capabilities = null;
            RigMonitorTelemetrySnapshot telemetry = null;

            try
            {
                readyStatus = await _RigMonitorClient.GetReadyStatusAsync(endpoint, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                success = false;
                error = ex.Message;
            }

            bool shouldRefreshCapabilities;
            lock (state.Lock)
            {
                shouldRefreshCapabilities =
                    !rigState.LastCapabilitiesUtc.HasValue ||
                    (now - rigState.LastCapabilitiesUtc.Value).TotalMilliseconds >= endpoint.RigMonitor.CapabilitiesRefreshIntervalMs;
            }

            if (shouldRefreshCapabilities)
            {
                try
                {
                    capabilities = await _RigMonitorClient.GetCapabilitiesAsync(endpoint, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (String.IsNullOrEmpty(error))
                    {
                        error = ex.Message;
                    }
                }
            }

            bool readyForTelemetry = readyStatus?.Ready ?? false;
            if (endpoint.RigMonitor.CollectDuringHealthCheck)
            {
                if (endpoint.RigMonitor.RequireReadyz && !readyForTelemetry)
                {
                    success = false;
                    if (String.IsNullOrEmpty(error))
                    {
                        error = !String.IsNullOrEmpty(readyStatus?.Message) ? readyStatus.Message : "RigMonitor telemetry is not ready.";
                    }
                }
                else
                {
                    try
                    {
                        telemetry = await _RigMonitorClient.GetTelemetryAsync(endpoint, token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        if (String.IsNullOrEmpty(error))
                        {
                            error = ex.Message;
                        }
                    }
                }
            }

            lock (state.Lock)
            {
                rigState.LastError = error;

                if (readyStatus != null)
                {
                    rigState.Ready = readyStatus.Ready;
                    rigState.ReadyStatus = readyStatus.Status;
                    rigState.ReadyMessage = readyStatus.Message;
                    rigState.LastReadyzUtc = readyStatus.TimestampUtc ?? DateTime.UtcNow;
                }

                if (capabilities != null)
                {
                    rigState.Capabilities = capabilities;
                    rigState.LastCapabilitiesUtc = capabilities.CollectedUtc ?? DateTime.UtcNow;
                }

                if (telemetry != null)
                {
                    rigState.Telemetry = telemetry;
                    rigState.LastTelemetryUtc = telemetry.CollectedUtc ?? DateTime.UtcNow;
                }
            }

            return new RigMonitorRefreshResult
            {
                Success = success,
                Error = error
            };
        }

        private async Task<HealthCheckProbeResult> PerformHealthCheck(ModelRunnerEndpoint endpoint, int timeoutMs, CancellationToken token)
        {
            string url = BuildHealthCheckUrl(endpoint);
            string method = GetHealthCheckMethodName(endpoint);

            _Logging.Debug(_Header + "performing shared health check " + method + " " + url);

            using (HttpRequestMessage request = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = GetHealthCheckHttpMethod(endpoint)
            })
            {
                if (endpoint.HealthCheckMethod == HealthCheckMethodEnum.POST)
                {
                    request.Content = new StringContent(endpoint.HealthCheckRequestBody ?? String.Empty, Encoding.UTF8);
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                }

                if (endpoint.HealthCheckUseAuth)
                {
                    UpstreamAuthHeaders.Apply(request, endpoint, _AnthropicSettings);
                }
                else if (endpoint.ApiType == ApiTypeEnum.Anthropic)
                {
                    request.Headers.TryAddWithoutValidation(
                        UpstreamAuthHeaders.AnthropicVersionHeader,
                        UpstreamAuthHeaders.ResolveAnthropicVersion(null, endpoint, _AnthropicSettings));
                }

                using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    cts.CancelAfter(timeoutMs);
                    using (HttpResponseMessage response = await _HttpClient.SendAsync(request, cts.Token).ConfigureAwait(false))
                    {
                        int statusCode = (int)response.StatusCode;
                        return new HealthCheckProbeResult
                        {
                            StatusCode = statusCode
                        };
                    }
                }
            }
        }

        private bool EvaluateHealthCheckResult(ModelRunnerEndpoint endpoint, HealthCheckProbeResult result, out string error)
        {
            error = null;
            if (result == null)
            {
                error = "No health check result was returned.";
                return false;
            }

            if (!String.IsNullOrEmpty(result.Error))
            {
                error = result.Error;
                return false;
            }

            if (!result.StatusCode.HasValue)
            {
                error = "No HTTP status code was returned.";
                return false;
            }

            bool success = result.StatusCode.Value == endpoint.HealthCheckExpectedStatusCode;
            if (!success)
            {
                error = "HTTP status code " + result.StatusCode.Value + " did not match expected " + endpoint.HealthCheckExpectedStatusCode + ".";
                _Logging.Warn(_Header + "health check for endpoint " + endpoint.Id + " (" + endpoint.Name + ") returned status code " + result.StatusCode.Value + " (expected " + endpoint.HealthCheckExpectedStatusCode + ")");
            }

            return success;
        }

        private static string BuildHealthCheckUrl(ModelRunnerEndpoint endpoint)
        {
            return endpoint.GetBaseUrl().TrimEnd('/') + NormalizeHealthCheckUrl(endpoint.HealthCheckUrl);
        }

        private static string NormalizeHealthCheckUrl(string healthCheckUrl)
        {
            if (String.IsNullOrWhiteSpace(healthCheckUrl))
            {
                return "/";
            }

            return healthCheckUrl.StartsWith("/") ? healthCheckUrl : "/" + healthCheckUrl;
        }

        private static bool IsRigMonitorActive(ModelRunnerEndpoint endpoint)
        {
            // RigMonitor describes a local rig; it never applies to OllamaCloud endpoints.
            return endpoint != null
                && endpoint.RigMonitor != null
                && endpoint.RigMonitor.Enabled
                && endpoint.ApiType != ApiTypeEnum.OllamaCloud;
        }

        private static string GetHealthCheckMethodName(ModelRunnerEndpoint endpoint)
        {
            switch (endpoint.HealthCheckMethod)
            {
                case HealthCheckMethodEnum.HEAD:
                    return "HEAD";
                case HealthCheckMethodEnum.POST:
                    return "POST";
                default:
                    return "GET";
            }
        }

        private static HttpMethod GetHealthCheckHttpMethod(ModelRunnerEndpoint endpoint)
        {
            switch (endpoint.HealthCheckMethod)
            {
                case HealthCheckMethodEnum.HEAD:
                    return HttpMethod.Head;
                case HealthCheckMethodEnum.POST:
                    return HttpMethod.Post;
                default:
                    return HttpMethod.Get;
            }
        }

        private static string BuildHealthCheckKey(ModelRunnerEndpoint endpoint)
        {
            string method = GetHealthCheckMethodName(endpoint);
            string authKey = "none";
            if (endpoint.HealthCheckUseAuth && !String.IsNullOrEmpty(endpoint.ApiKey))
            {
                string headerName = UpstreamAuthHeaders.GetKeyHeaderName(endpoint.ApiType);
                authKey = headerName + ":" + HashSecret(endpoint.ApiKey);
            }

            if (endpoint.ApiType == ApiTypeEnum.Anthropic)
            {
                authKey += " version=" + (endpoint.AnthropicVersion ?? "default");
            }

            if (endpoint.HealthCheckMethod == HealthCheckMethodEnum.POST)
            {
                authKey += " body=" + HashSecret(endpoint.HealthCheckRequestBody);
            }

            Uri uri = new Uri(BuildHealthCheckUrl(endpoint));
            string normalizedUrl = uri.Scheme.ToLowerInvariant()
                + "://"
                + uri.Host.ToLowerInvariant()
                + ":"
                + uri.Port
                + uri.PathAndQuery;
            return method + " " + normalizedUrl + " auth=" + authKey;
        }

        private static string GetLogSafeHealthCheckKey(ModelRunnerEndpoint endpoint)
        {
            string method = GetHealthCheckMethodName(endpoint);
            return method + " " + BuildHealthCheckUrl(endpoint);
        }

        private static string HashSecret(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] data = Encoding.UTF8.GetBytes(value ?? String.Empty);
                byte[] hash = sha256.ComputeHash(data);
                return Convert.ToBase64String(hash);
            }
        }

        private void UpdateHealthState(ModelRunnerEndpoint endpoint, bool success, string error)
        {
            if (!_HealthStates.TryGetValue(endpoint.Id, out EndpointHealthState state)) return;

            lock (state.Lock)
            {
                DateTime now = DateTime.UtcNow;
                state.ServiceState = endpoint.ServiceState;
                state.LastCheckUtc = now;
                state.LastError = success ? null : error;

                // Record the health check in history
                state.CheckHistory.Add(new HealthCheckRecord
                {
                    TimestampUtc = now,
                    Success = success
                });

                // Trim history if it exceeds the maximum
                if (state.CheckHistory.Count > state.MaxHistoryRecords)
                {
                    state.CheckHistory.RemoveRange(0, state.CheckHistory.Count - state.MaxHistoryRecords);
                }

                if (success)
                {
                    state.ConsecutiveSuccesses++;
                    state.ConsecutiveFailures = 0;

                    // Transition to healthy after HealthyThreshold consecutive successes, or on the very first success of an
                    // endpoint that has never been healthy (new, just created, or just started), so a fresh endpoint does not
                    // wait HealthyThreshold full intervals before it can take traffic.
                    bool firstSuccess = !state.LastHealthyUtc.HasValue;
                    if (!state.IsHealthy && (state.ConsecutiveSuccesses >= endpoint.HealthyThreshold || firstSuccess))
                    {
                        // Calculate downtime before transition
                        if (state.LastStateChangeUtc.HasValue)
                        {
                            long downtimeMs = (long)(now - state.LastStateChangeUtc.Value).TotalMilliseconds;
                            state.TotalDowntimeMs += downtimeMs;
                        }

                        state.IsHealthy = true;
                        PulseCapacity();
                        state.LastHealthyUtc = now;
                        state.LastStateChangeUtc = now;

                        _Logging.Info(_Header + "endpoint " + endpoint.Id + " " + endpoint.Name + " is now HEALTHY after " + state.ConsecutiveSuccesses + " consecutive successful check(s)" + (firstSuccess ? " (first successful check)" : String.Empty));
                    }
                }
                else
                {
                    state.ConsecutiveFailures++;
                    state.ConsecutiveSuccesses = 0;

                    // Check if we should transition to unhealthy
                    if (state.IsHealthy && state.ConsecutiveFailures >= endpoint.UnhealthyThreshold)
                    {
                        // Calculate uptime before transition
                        if (state.LastStateChangeUtc.HasValue)
                        {
                            long uptimeMs = (long)(now - state.LastStateChangeUtc.Value).TotalMilliseconds;
                            state.TotalUptimeMs += uptimeMs;
                        }

                        state.IsHealthy = false;
                        state.LastUnhealthyUtc = now;
                        state.LastStateChangeUtc = now;

                        _Logging.Warn(_Header + "endpoint " + endpoint.Id + " (" + endpoint.Name + ") is now UNHEALTHY after " + state.ConsecutiveFailures + " consecutive failed checks, error: " + error);
                    }
                }
            }
        }

        private sealed class RigMonitorRefreshResult
        {
            public bool Success { get; set; } = false;

            public string Error { get; set; } = null;
        }

        private sealed class HealthCheckProbeResult
        {
            public int? StatusCode { get; set; } = null;

            public string Error { get; set; } = null;
        }

        /// <summary>
        /// Disposes of managed resources including HttpClient and CancellationTokenSource instances.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                // Dispose global cancellation token source
                if (_GlobalCancellation != null)
                {
                    _GlobalCancellation.Cancel();
                    _GlobalCancellation.Dispose();
                    _GlobalCancellation = null;
                }

                // Dispose all endpoint-specific cancellation token sources
                foreach (KeyValuePair<string, CancellationTokenSource> kvp in _CancellationTokens)
                {
                    kvp.Value.Cancel();
                    kvp.Value.Dispose();
                }
                _CancellationTokens.Clear();

                // Dispose HttpClient
                _HttpClient?.Dispose();
                _RigMonitorClient?.Dispose();

                _HealthStates.Clear();
                _RunningTasks.Clear();
                _ActiveEndpoints.Clear();
                _EndpointHealthCheckKeys.Clear();
            }

            _Disposed = true;
        }
    }
}
