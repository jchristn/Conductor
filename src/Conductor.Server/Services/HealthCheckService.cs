namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
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
        private readonly ConcurrentDictionary<string, EndpointHealthState> _HealthStates;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _CancellationTokens;
        private readonly ConcurrentDictionary<string, Task> _RunningTasks;
        private CancellationTokenSource _GlobalCancellation;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the health check service.
        /// </summary>
        public HealthCheckService(DatabaseDriverBase database, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _HttpClient = new HttpClient();
            _HealthStates = new ConcurrentDictionary<string, EndpointHealthState>();
            _CancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
            _RunningTasks = new ConcurrentDictionary<string, Task>();
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
        /// Called when an endpoint is created.
        /// </summary>
        public void OnEndpointCreated(ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null || !endpoint.Active) return;
            _Logging.Debug(_Header + "endpoint created: {endpoint.Id}");
            StartHealthCheckTask(endpoint);
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
        }

        private void StartHealthCheckTask(ModelRunnerEndpoint endpoint)
        {
            // Create health state entry
            EndpointHealthState state = new EndpointHealthState
            {
                EndpointId = endpoint.Id,
                EndpointName = endpoint.Name,
                TenantId = endpoint.TenantId,
                IsHealthy = false, // Start unhealthy until proven healthy
                FirstCheckUtc = DateTime.UtcNow,
                LastStateChangeUtc = DateTime.UtcNow
            };
            _HealthStates.AddOrUpdate(endpoint.Id, state, (k, v) => state);

            // Create cancellation token for this endpoint
            CancellationTokenSource cts = new CancellationTokenSource();
            _CancellationTokens.AddOrUpdate(endpoint.Id, cts, (k, v) => { v.Cancel(); v.Dispose(); return cts; });

            // Start the health check loop
            Task task = Task.Run(async () => await HealthCheckLoop(endpoint, cts.Token).ConfigureAwait(false));
            _RunningTasks.AddOrUpdate(endpoint.Id, task, (k, v) => task);

            _Logging.Info(_Header + $"health check started for endpoint {endpoint.Id} ({endpoint.Name}) - interval: {endpoint.HealthCheckIntervalMs}ms, URL: {endpoint.GetBaseUrl()}{endpoint.HealthCheckUrl}");
        }

        private void StopHealthCheckTask(string endpointId)
        {
            // Get endpoint name from health state before stopping
            string endpointName = null;
            if (_HealthStates.TryGetValue(endpointId, out EndpointHealthState state))
            {
                endpointName = state.EndpointName;
            }

            if (_CancellationTokens.TryRemove(endpointId, out CancellationTokenSource cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
            _RunningTasks.TryRemove(endpointId, out _);

            _Logging.Info(_Header + $"health check stopped for endpoint {endpointId}" + (endpointName != null ? $" ({endpointName})" : ""));
        }

        private async Task HealthCheckLoop(ModelRunnerEndpoint endpoint, CancellationToken token)
        {
            while (!token.IsCancellationRequested && !(_GlobalCancellation?.IsCancellationRequested ?? false))
            {
                try
                {
                    // Wait for the interval
                    await Task.Delay(endpoint.HealthCheckIntervalMs, token).ConfigureAwait(false);

                    if (token.IsCancellationRequested) break;

                    // Perform health check
                    bool success = false;
                    string error = null;

                    try
                    {
                        success = await PerformHealthCheck(endpoint, token).ConfigureAwait(false);
                        _Logging.Debug(_Header + "health check " + (success ? "succeeded" : "failed") + " for endpoint " + endpoint.Id + " (" + endpoint.Name + ")");
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        _Logging.Debug(_Header + "health check failed for endpoint " + endpoint.Id + " (" + endpoint.Name + "): " + ex.Message);
                    }

                    // Update state
                    UpdateHealthState(endpoint, success, error);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "error for endpoint " + endpoint.Id + Environment.NewLine + ex.ToString());
                }
            }
        }

        private async Task<bool> PerformHealthCheck(ModelRunnerEndpoint endpoint, CancellationToken token)
        {
            string url = endpoint.GetBaseUrl() + endpoint.HealthCheckUrl;
            string method = endpoint.HealthCheckMethod == HealthCheckMethodEnum.HEAD ? "HEAD" : "GET";

            _Logging.Debug(_Header + "performing health check for endpoint " + endpoint.Id + " (" + endpoint.Name + ") " + method + " " + url);

            using (HttpRequestMessage request = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = endpoint.HealthCheckMethod == HealthCheckMethodEnum.HEAD
                    ? HttpMethod.Head
                    : HttpMethod.Get
            })
            {
                if (endpoint.HealthCheckUseAuth && !String.IsNullOrEmpty(endpoint.ApiKey))
                {
                    request.Headers.Add("Authorization", "Bearer " + endpoint.ApiKey);
                }

                using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    cts.CancelAfter(endpoint.HealthCheckTimeoutMs);
                    using (HttpResponseMessage response = await _HttpClient.SendAsync(request, cts.Token).ConfigureAwait(false))
                    {
                        int statusCode = (int)response.StatusCode;
                        bool success = statusCode == endpoint.HealthCheckExpectedStatusCode;
                        if (!success)
                        {
                            _Logging.Warn(_Header + "health check for endpoint " + endpoint.Id + " (" + endpoint.Name + ") returned status code " + statusCode + " (expected " + endpoint.HealthCheckExpectedStatusCode + ")");
                        }
                        return success;
                    }
                }
            }
        }

        private void UpdateHealthState(ModelRunnerEndpoint endpoint, bool success, string error)
        {
            if (!_HealthStates.TryGetValue(endpoint.Id, out EndpointHealthState state)) return;

            lock (state.Lock)
            {
                DateTime now = DateTime.UtcNow;
                state.LastCheckUtc = now;
                state.LastError = success ? null : error;

                if (success)
                {
                    state.ConsecutiveSuccesses++;
                    state.ConsecutiveFailures = 0;

                    // Check if we should transition to healthy
                    if (!state.IsHealthy && state.ConsecutiveSuccesses >= endpoint.HealthyThreshold)
                    {
                        // Calculate downtime before transition
                        if (state.LastStateChangeUtc.HasValue)
                        {
                            long downtimeMs = (long)(now - state.LastStateChangeUtc.Value).TotalMilliseconds;
                            state.TotalDowntimeMs += downtimeMs;
                        }

                        state.IsHealthy = true;
                        state.LastHealthyUtc = now;
                        state.LastStateChangeUtc = now;

                        _Logging.Info(_Header + "endpoint " + endpoint.Id + " " + endpoint.Name + " is now HEALTHY after " + state.ConsecutiveSuccesses + " consecutive successful checks");
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

                _HealthStates.Clear();
                _RunningTasks.Clear();
            }

            _Disposed = true;
        }
    }
}
