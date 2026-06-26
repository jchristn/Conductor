namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Threading;
    using Conductor.Core.Models;

    /// <summary>
    /// Tracks in-memory endpoint runtime statistics used by adaptive load balancing.
    /// </summary>
    public class EndpointRuntimeStatsService
    {
        private const string _HeaderRetryAfter = "Retry-After";
        private const string _HeaderRateLimitReset = "RateLimit-Reset";
        private const string _HeaderXRateLimitReset = "X-RateLimit-Reset";
        private readonly ConcurrentDictionary<string, RuntimeStatsState> _Stats = new ConcurrentDictionary<string, RuntimeStatsState>(StringComparer.Ordinal);
        private long _SelectionSequence = 0;

        /// <summary>
        /// Record that an endpoint was selected by routing.
        /// </summary>
        /// <param name="vmr">Virtual model runner.</param>
        /// <param name="endpoint">Selected endpoint.</param>
        public void RecordSelection(VirtualModelRunner vmr, ModelRunnerEndpoint endpoint)
        {
            if (vmr == null || endpoint == null || String.IsNullOrWhiteSpace(endpoint.Id))
            {
                return;
            }

            RuntimeStatsState state = GetState(vmr.TenantId, vmr.Id, endpoint.Id, endpoint.Name);
            lock (state.Lock)
            {
                state.EndpointName = endpoint.Name;
                state.LastSelectedUtc = DateTime.UtcNow;
                state.SelectionSequence = Interlocked.Increment(ref _SelectionSequence);
                state.LastUpdateUtc = state.LastSelectedUtc.Value;
            }
        }

        /// <summary>
        /// Record that a selected endpoint admitted a proxied request.
        /// </summary>
        /// <param name="vmr">Virtual model runner.</param>
        /// <param name="endpoint">Admitted endpoint.</param>
        public void RecordAdmission(VirtualModelRunner vmr, ModelRunnerEndpoint endpoint)
        {
            if (vmr == null || endpoint == null || String.IsNullOrWhiteSpace(endpoint.Id))
            {
                return;
            }

            RuntimeStatsState state = GetState(vmr.TenantId, vmr.Id, endpoint.Id, endpoint.Name);
            lock (state.Lock)
            {
                state.EndpointName = endpoint.Name;
                state.InFlight++;
                state.Pending++;
                state.LastAdmittedUtc = DateTime.UtcNow;
                state.LastUpdateUtc = state.LastAdmittedUtc.Value;
            }
        }

        /// <summary>
        /// Record a completed upstream response.
        /// </summary>
        /// <param name="vmr">Virtual model runner.</param>
        /// <param name="endpoint">Endpoint that handled the request.</param>
        /// <param name="statusCode">Upstream status code.</param>
        /// <param name="elapsedMs">Total elapsed milliseconds.</param>
        /// <param name="firstTokenTimeMs">Time to first token, if known.</param>
        /// <param name="settings">Adaptive settings for EWMA and backoff defaults.</param>
        /// <param name="responseHeaders">Response headers.</param>
        /// <param name="contentHeaders">Response content headers.</param>
        public void RecordCompletion(
            VirtualModelRunner vmr,
            ModelRunnerEndpoint endpoint,
            int statusCode,
            double elapsedMs,
            int? firstTokenTimeMs,
            AdaptiveLoadBalancingSettings settings,
            HttpResponseHeaders responseHeaders = null,
            HttpContentHeaders contentHeaders = null)
        {
            if (vmr == null || endpoint == null || String.IsNullOrWhiteSpace(endpoint.Id))
            {
                return;
            }

            AdaptiveLoadBalancingSettings effectiveSettings = settings ?? new AdaptiveLoadBalancingSettings();
            RuntimeStatsState state = GetState(vmr.TenantId, vmr.Id, endpoint.Id, endpoint.Name);
            lock (state.Lock)
            {
                DecrementPending(state);
                bool success = statusCode < 500 && statusCode != 429;
                ApplyOutcome(state, effectiveSettings, success, statusCode, null, elapsedMs, firstTokenTimeMs);
                if (statusCode == 429)
                {
                    TimeSpan? retryAfter = ParseRetryAfter(responseHeaders, contentHeaders, effectiveSettings);
                    ApplyBackoff(state, effectiveSettings, "RateLimited", retryAfter, true);
                }
                else if (statusCode >= 500)
                {
                    ApplyThresholdBackoff(state, effectiveSettings, "Upstream5xx");
                }
            }
        }

        /// <summary>
        /// Record an upstream failure before a response completed.
        /// </summary>
        /// <param name="vmr">Virtual model runner.</param>
        /// <param name="endpoint">Endpoint that failed.</param>
        /// <param name="errorCode">Stable error code.</param>
        /// <param name="elapsedMs">Elapsed milliseconds.</param>
        /// <param name="settings">Adaptive settings for EWMA and backoff defaults.</param>
        public void RecordFailure(
            VirtualModelRunner vmr,
            ModelRunnerEndpoint endpoint,
            string errorCode,
            double elapsedMs,
            AdaptiveLoadBalancingSettings settings)
        {
            if (vmr == null || endpoint == null || String.IsNullOrWhiteSpace(endpoint.Id))
            {
                return;
            }

            AdaptiveLoadBalancingSettings effectiveSettings = settings ?? new AdaptiveLoadBalancingSettings();
            RuntimeStatsState state = GetState(vmr.TenantId, vmr.Id, endpoint.Id, endpoint.Name);
            lock (state.Lock)
            {
                DecrementPending(state);
                string safeErrorCode = SanitizeErrorCode(errorCode);
                ApplyOutcome(state, effectiveSettings, false, null, safeErrorCode, elapsedMs, null);
                bool immediate = String.Equals(safeErrorCode, "Timeout", StringComparison.Ordinal)
                    || String.Equals(safeErrorCode, "ConnectionFailure", StringComparison.Ordinal);
                if (immediate)
                {
                    ApplyBackoff(state, effectiveSettings, safeErrorCode, null, true);
                }
                else
                {
                    ApplyThresholdBackoff(state, effectiveSettings, safeErrorCode);
                }
            }
        }

        /// <summary>
        /// Get the runtime statistics snapshot for a virtual model runner.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="virtualModelRunnerId">Virtual model runner identifier.</param>
        /// <param name="endpoints">Known endpoint records to include even when no runtime stats exist.</param>
        /// <returns>Runtime stats collection.</returns>
        public EndpointRuntimeStatsCollection GetStats(string tenantId, string virtualModelRunnerId, IEnumerable<ModelRunnerEndpoint> endpoints = null)
        {
            EndpointRuntimeStatsCollection collection = new EndpointRuntimeStatsCollection
            {
                TenantId = tenantId,
                VirtualModelRunnerId = virtualModelRunnerId,
                SnapshotUtc = DateTime.UtcNow
            };

            HashSet<string> included = new HashSet<string>(StringComparer.Ordinal);
            if (endpoints != null)
            {
                foreach (ModelRunnerEndpoint endpoint in endpoints)
                {
                    if (endpoint == null || String.IsNullOrWhiteSpace(endpoint.Id))
                    {
                        continue;
                    }

                    collection.Endpoints.Add(GetSnapshot(tenantId, virtualModelRunnerId, endpoint.Id, endpoint.Name));
                    included.Add(endpoint.Id);
                }
            }

            foreach (KeyValuePair<string, RuntimeStatsState> item in _Stats)
            {
                RuntimeStatsState state = item.Value;
                if (!String.Equals(state.TenantId, tenantId, StringComparison.Ordinal)
                    || !String.Equals(state.VirtualModelRunnerId, virtualModelRunnerId, StringComparison.Ordinal)
                    || included.Contains(state.EndpointId))
                {
                    continue;
                }

                collection.Endpoints.Add(CopySnapshot(state));
            }

            collection.Endpoints = collection.Endpoints.OrderBy(endpoint => endpoint.EndpointName).ThenBy(endpoint => endpoint.EndpointId).ToList();
            return collection;
        }

        /// <summary>
        /// Get one endpoint runtime statistics snapshot.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="virtualModelRunnerId">Virtual model runner identifier.</param>
        /// <param name="endpointId">Endpoint identifier.</param>
        /// <param name="endpointName">Endpoint display name.</param>
        /// <returns>Runtime stats snapshot.</returns>
        public EndpointRuntimeStatsSnapshot GetSnapshot(string tenantId, string virtualModelRunnerId, string endpointId, string endpointName = null)
        {
            if (String.IsNullOrWhiteSpace(endpointId))
            {
                return null;
            }

            RuntimeStatsState state = GetState(tenantId, virtualModelRunnerId, endpointId, endpointName);
            return CopySnapshot(state);
        }

        /// <summary>
        /// Reset runtime statistics for a VMR or one endpoint.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="virtualModelRunnerId">Virtual model runner identifier.</param>
        /// <param name="endpointId">Optional endpoint identifier.</param>
        public void Reset(string tenantId, string virtualModelRunnerId, string endpointId = null)
        {
            foreach (string key in _Stats.Keys)
            {
                if (!_Stats.TryGetValue(key, out RuntimeStatsState state))
                {
                    continue;
                }

                if (!String.Equals(state.TenantId, tenantId, StringComparison.Ordinal)
                    || !String.Equals(state.VirtualModelRunnerId, virtualModelRunnerId, StringComparison.Ordinal)
                    || (!String.IsNullOrWhiteSpace(endpointId) && !String.Equals(state.EndpointId, endpointId, StringComparison.Ordinal)))
                {
                    continue;
                }

                _Stats.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Clear transient backoff for a VMR or one endpoint.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="virtualModelRunnerId">Virtual model runner identifier.</param>
        /// <param name="endpointId">Optional endpoint identifier.</param>
        public void ClearBackoff(string tenantId, string virtualModelRunnerId, string endpointId = null)
        {
            foreach (RuntimeStatsState state in _Stats.Values)
            {
                if (!String.Equals(state.TenantId, tenantId, StringComparison.Ordinal)
                    || !String.Equals(state.VirtualModelRunnerId, virtualModelRunnerId, StringComparison.Ordinal)
                    || (!String.IsNullOrWhiteSpace(endpointId) && !String.Equals(state.EndpointId, endpointId, StringComparison.Ordinal)))
                {
                    continue;
                }

                lock (state.Lock)
                {
                    state.BackoffReason = null;
                    state.BackoffUntilUtc = null;
                    state.LastUpdateUtc = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Determine whether a VMR endpoint is currently in transient backoff.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="virtualModelRunnerId">Virtual model runner identifier.</param>
        /// <param name="endpointId">Endpoint identifier.</param>
        /// <returns>True when backoff is currently active.</returns>
        public bool IsBackoffActive(string tenantId, string virtualModelRunnerId, string endpointId)
        {
            EndpointRuntimeStatsSnapshot snapshot = GetSnapshot(tenantId, virtualModelRunnerId, endpointId);
            return snapshot != null && snapshot.BackoffActive;
        }

        private RuntimeStatsState GetState(string tenantId, string virtualModelRunnerId, string endpointId, string endpointName)
        {
            string key = BuildKey(tenantId, virtualModelRunnerId, endpointId);
            return _Stats.GetOrAdd(key, _ => new RuntimeStatsState
            {
                TenantId = tenantId,
                VirtualModelRunnerId = virtualModelRunnerId,
                EndpointId = endpointId,
                EndpointName = endpointName,
                LastUpdateUtc = DateTime.UtcNow
            });
        }

        private static EndpointRuntimeStatsSnapshot CopySnapshot(RuntimeStatsState state)
        {
            if (state == null)
            {
                return null;
            }

            lock (state.Lock)
            {
                DateTime now = DateTime.UtcNow;
                bool backoffActive = state.BackoffUntilUtc.HasValue && state.BackoffUntilUtc.Value > now;
                return new EndpointRuntimeStatsSnapshot
                {
                    TenantId = state.TenantId,
                    VirtualModelRunnerId = state.VirtualModelRunnerId,
                    EndpointId = state.EndpointId,
                    EndpointName = state.EndpointName,
                    InFlight = state.InFlight,
                    Pending = state.Pending,
                    CompletedCount = state.CompletedCount,
                    SuccessEwma = state.SuccessEwma,
                    ErrorEwma = state.ErrorEwma,
                    LatencyEwmaMs = state.LatencyEwmaMs,
                    TimeToFirstTokenEwmaMs = state.TimeToFirstTokenEwmaMs,
                    LastStatusCode = state.LastStatusCode,
                    LastErrorCode = state.LastErrorCode,
                    ConsecutiveFailures = state.ConsecutiveFailures,
                    BackoffActive = backoffActive,
                    BackoffReason = backoffActive ? state.BackoffReason : null,
                    BackoffUntilUtc = backoffActive ? state.BackoffUntilUtc : null,
                    LastSelectedUtc = state.LastSelectedUtc,
                    LastAdmittedUtc = state.LastAdmittedUtc,
                    SelectionSequence = state.SelectionSequence,
                    LastUpdateUtc = state.LastUpdateUtc
                };
            }
        }

        private static void ApplyOutcome(
            RuntimeStatsState state,
            AdaptiveLoadBalancingSettings settings,
            bool success,
            int? statusCode,
            string errorCode,
            double elapsedMs,
            int? firstTokenTimeMs)
        {
            double alpha = GetEwmaAlpha(settings);
            state.CompletedCount++;
            state.LastStatusCode = statusCode;
            state.LastErrorCode = errorCode;
            state.SuccessEwma = UpdateEwma(state.SuccessEwma, success ? 1 : 0, alpha);
            state.ErrorEwma = UpdateEwma(state.ErrorEwma, success ? 0 : 1, alpha);
            if (elapsedMs >= 0)
            {
                state.LatencyEwmaMs = UpdateEwma(state.LatencyEwmaMs, elapsedMs, alpha);
            }
            if (firstTokenTimeMs.HasValue && firstTokenTimeMs.Value >= 0)
            {
                state.TimeToFirstTokenEwmaMs = UpdateEwma(state.TimeToFirstTokenEwmaMs, firstTokenTimeMs.Value, alpha);
            }

            if (success)
            {
                state.ConsecutiveFailures = 0;
            }
            else
            {
                state.ConsecutiveFailures++;
            }

            state.LastUpdateUtc = DateTime.UtcNow;
        }

        private static double UpdateEwma(double? current, double next, double alpha)
        {
            if (!current.HasValue)
            {
                return next;
            }

            return (alpha * next) + ((1 - alpha) * current.Value);
        }

        private static void DecrementPending(RuntimeStatsState state)
        {
            if (state.InFlight > 0)
            {
                state.InFlight--;
            }

            if (state.Pending > 0)
            {
                state.Pending--;
            }
        }

        private static void ApplyThresholdBackoff(RuntimeStatsState state, AdaptiveLoadBalancingSettings settings, string reason)
        {
            if (state.ConsecutiveFailures >= GetFailureThreshold(settings))
            {
                ApplyBackoff(state, settings, reason, null, false);
            }
        }

        private static void ApplyBackoff(RuntimeStatsState state, AdaptiveLoadBalancingSettings settings, string reason, TimeSpan? explicitDuration, bool immediate)
        {
            int baseMs = GetBackoffBaseMs(settings);
            int maxMs = Math.Max(baseMs, GetBackoffMaxMs(settings));
            int failureFactor = Math.Max(0, state.ConsecutiveFailures - GetFailureThreshold(settings));
            double multiplier = Math.Pow(2, failureFactor);
            int computedDurationMs = (int)Math.Min(maxMs, Math.Max(baseMs, baseMs * multiplier));
            TimeSpan duration = explicitDuration ?? TimeSpan.FromMilliseconds(computedDurationMs);
            if (duration.TotalMilliseconds < baseMs && !immediate)
            {
                duration = TimeSpan.FromMilliseconds(baseMs);
            }
            if (duration.TotalMilliseconds > maxMs)
            {
                duration = TimeSpan.FromMilliseconds(maxMs);
            }

            state.BackoffReason = SanitizeErrorCode(reason);
            state.BackoffUntilUtc = DateTime.UtcNow.Add(duration);
            state.LastUpdateUtc = DateTime.UtcNow;
        }

        private static TimeSpan? ParseRetryAfter(HttpResponseHeaders responseHeaders, HttpContentHeaders contentHeaders, AdaptiveLoadBalancingSettings settings)
        {
            string value = GetHeaderValue(responseHeaders, contentHeaders, _HeaderRetryAfter);
            TimeSpan? parsed = ParseDelayHeader(value);
            if (!parsed.HasValue)
            {
                parsed = ParseDelayHeader(GetHeaderValue(responseHeaders, contentHeaders, _HeaderRateLimitReset));
            }
            if (!parsed.HasValue)
            {
                parsed = ParseDelayHeader(GetHeaderValue(responseHeaders, contentHeaders, _HeaderXRateLimitReset));
            }
            if (!parsed.HasValue)
            {
                return null;
            }

            int baseMs = GetBackoffBaseMs(settings);
            int maxMs = Math.Max(baseMs, GetBackoffMaxMs(settings));
            double bounded = Math.Max(baseMs, Math.Min(maxMs, parsed.Value.TotalMilliseconds));
            return TimeSpan.FromMilliseconds(bounded);
        }

        private static double GetEwmaAlpha(AdaptiveLoadBalancingSettings settings)
        {
            double alpha = settings?.EwmaAlpha ?? 0.20;
            if (alpha < 0.01) return 0.20;
            if (alpha > 1) return 1;
            return alpha;
        }

        private static int GetBackoffBaseMs(AdaptiveLoadBalancingSettings settings)
        {
            int value = settings?.BackoffBaseMs ?? 30000;
            return value < 1000 ? 30000 : value;
        }

        private static int GetBackoffMaxMs(AdaptiveLoadBalancingSettings settings)
        {
            int value = settings?.BackoffMaxMs ?? 300000;
            return value < 1000 ? 300000 : value;
        }

        private static int GetFailureThreshold(AdaptiveLoadBalancingSettings settings)
        {
            int value = settings?.FailureThreshold ?? 3;
            return value < 1 ? 3 : value;
        }

        private static string GetHeaderValue(HttpResponseHeaders responseHeaders, HttpContentHeaders contentHeaders, string name)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            IEnumerable<string> values;
            if (responseHeaders != null && responseHeaders.TryGetValues(name, out values))
            {
                return values.FirstOrDefault();
            }
            if (contentHeaders != null && contentHeaders.TryGetValues(name, out values))
            {
                return values.FirstOrDefault();
            }

            return null;
        }

        private static TimeSpan? ParseDelayHeader(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Int32.TryParse(value.Trim(), out int seconds))
            {
                if (seconds > 86400)
                {
                    DateTimeOffset reset = DateTimeOffset.FromUnixTimeSeconds(seconds);
                    TimeSpan remaining = reset.UtcDateTime - DateTime.UtcNow;
                    return remaining.TotalMilliseconds > 0 ? remaining : TimeSpan.Zero;
                }

                return TimeSpan.FromSeconds(Math.Max(0, seconds));
            }

            if (DateTimeOffset.TryParse(value, out DateTimeOffset date))
            {
                TimeSpan delay = date.UtcDateTime - DateTime.UtcNow;
                return delay.TotalMilliseconds > 0 ? delay : TimeSpan.Zero;
            }

            return null;
        }

        private static string SanitizeErrorCode(string errorCode)
        {
            if (String.IsNullOrWhiteSpace(errorCode))
            {
                return "UpstreamFailure";
            }

            string trimmed = errorCode.Trim();
            if (trimmed.Length > 64)
            {
                trimmed = trimmed.Substring(0, 64);
            }

            char[] chars = trimmed.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                if (!Char.IsLetterOrDigit(current) && current != '_' && current != '-')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static string BuildKey(string tenantId, string virtualModelRunnerId, string endpointId)
        {
            return (tenantId ?? String.Empty) + "|" + (virtualModelRunnerId ?? String.Empty) + "|" + (endpointId ?? String.Empty);
        }

        private sealed class RuntimeStatsState
        {
            public readonly object Lock = new object();
            public string TenantId { get; set; }
            public string VirtualModelRunnerId { get; set; }
            public string EndpointId { get; set; }
            public string EndpointName { get; set; }
            public int InFlight { get; set; }
            public int Pending { get; set; }
            public long CompletedCount { get; set; }
            public double? SuccessEwma { get; set; }
            public double? ErrorEwma { get; set; }
            public double? LatencyEwmaMs { get; set; }
            public double? TimeToFirstTokenEwmaMs { get; set; }
            public int? LastStatusCode { get; set; }
            public string LastErrorCode { get; set; }
            public int ConsecutiveFailures { get; set; }
            public string BackoffReason { get; set; }
            public DateTime? BackoffUntilUtc { get; set; }
            public DateTime? LastSelectedUtc { get; set; }
            public DateTime? LastAdmittedUtc { get; set; }
            public long SelectionSequence { get; set; }
            public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
        }
    }
}
