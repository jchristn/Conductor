namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// HTTP client for RigMonitor REST APIs.
    /// </summary>
    public class RigMonitorClient : IDisposable
    {
        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly string _Header = "[RigMonitorClient] ";
        private readonly HttpClient _HttpClient = new HttpClient();
        private readonly LoggingModule _Logging;

        /// <summary>
        /// Instantiate the RigMonitor client.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        public RigMonitorClient(LoggingModule logging)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Build the RigMonitor base URL for an endpoint.
        /// </summary>
        public static string GetBaseUrl(ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null || endpoint.RigMonitor == null || !endpoint.RigMonitor.Enabled) return null;

            string host = !String.IsNullOrEmpty(endpoint.RigMonitor.HostnameOverride)
                ? endpoint.RigMonitor.HostnameOverride
                : endpoint.Hostname;
            string scheme = endpoint.RigMonitor.UseSsl ? "https" : "http";
            return scheme + "://" + host + ":" + endpoint.RigMonitor.Port.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Resolve selector list from an endpoint configuration.
        /// </summary>
        public static List<string> ResolveTelemetrySelectors(ModelRunnerEndpoint endpoint)
        {
            List<string> selectors = new List<string>();
            RigMonitorConfiguration config = endpoint?.RigMonitor;
            if (config == null) return selectors;

            switch (config.TelemetryProfile)
            {
                case RigMonitorTelemetryProfileEnum.Basic:
                    selectors.AddRange(new[] { "system", "cpu", "memory" });
                    break;
                case RigMonitorTelemetryProfileEnum.GpuPlacement:
                    selectors.AddRange(new[] { "system", "cpu", "memory", "gpu" });
                    break;
                case RigMonitorTelemetryProfileEnum.OllamaPlacement:
                    selectors.AddRange(new[] { "system", "cpu", "memory", "ollama" });
                    break;
                case RigMonitorTelemetryProfileEnum.Custom:
                    if (config.TelemetrySelectors != null) selectors.AddRange(config.TelemetrySelectors);
                    break;
                case RigMonitorTelemetryProfileEnum.Full:
                default:
                    break;
            }

            return selectors;
        }

        /// <summary>
        /// Retrieve RigMonitor readiness status for an endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ready status payload.</returns>
        public async Task<RigMonitorReadyStatus> GetReadyStatusAsync(ModelRunnerEndpoint endpoint, CancellationToken token = default)
        {
            string baseUrl = GetBaseUrl(endpoint) ?? throw new InvalidOperationException("RigMonitor is not enabled for the endpoint.");
            string url = baseUrl + "/readyz";

            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(endpoint.RigMonitor.TimeoutMs);
                using (HttpResponseMessage response = await _HttpClient.GetAsync(url, cts.Token).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.ServiceUnavailable)
                    {
                        throw new InvalidOperationException("RigMonitor readyz failed with status " + (int)response.StatusCode);
                    }

                    RigMonitorReadyStatus status = JsonSerializer.Deserialize<RigMonitorReadyStatus>(body, _JsonOptions) ?? new RigMonitorReadyStatus();
                    if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        status.Ready = false;
                        if (String.IsNullOrEmpty(status.Status)) status.Status = "warming";
                    }
                    return status;
                }
            }
        }

        /// <summary>
        /// Retrieve RigMonitor capabilities for an endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Capabilities payload.</returns>
        public async Task<RigMonitorCapabilities> GetCapabilitiesAsync(ModelRunnerEndpoint endpoint, CancellationToken token = default)
        {
            string baseUrl = GetBaseUrl(endpoint) ?? throw new InvalidOperationException("RigMonitor is not enabled for the endpoint.");
            return await GetJsonAsync<RigMonitorCapabilities>(baseUrl + "/v1/capabilities", endpoint.RigMonitor.TimeoutMs, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve RigMonitor telemetry for an endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Telemetry snapshot.</returns>
        public async Task<RigMonitorTelemetrySnapshot> GetTelemetryAsync(ModelRunnerEndpoint endpoint, CancellationToken token = default)
        {
            string baseUrl = GetBaseUrl(endpoint) ?? throw new InvalidOperationException("RigMonitor is not enabled for the endpoint.");
            string url = baseUrl + "/v1/telemetry";

            List<string> selectors = ResolveTelemetrySelectors(endpoint);
            if (selectors != null && selectors.Count > 0)
            {
                url += "?" + String.Join("&", selectors);
            }

            return await GetJsonAsync<RigMonitorTelemetrySnapshot>(url, endpoint.RigMonitor.TimeoutMs, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Probe RigMonitor readiness, capabilities, and telemetry for an endpoint.
        /// </summary>
        /// <param name="endpoint">Model runner endpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Aggregated endpoint status.</returns>
        public async Task<RigMonitorEndpointStatus> ProbeAsync(ModelRunnerEndpoint endpoint, CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            RigMonitorEndpointStatus status = new RigMonitorEndpointStatus
            {
                Enabled = endpoint.RigMonitor?.Enabled ?? false,
                BaseUrl = GetBaseUrl(endpoint)
            };

            if (!status.Enabled) return status;

            try
            {
                RigMonitorReadyStatus ready = await GetReadyStatusAsync(endpoint, token).ConfigureAwait(false);
                status.Ready = ready.Ready;
                status.ReadyStatus = ready.Status;
                status.ReadyMessage = ready.Message;
                status.LastReadyzUtc = ready.TimestampUtc ?? DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                status.LastError = ex.Message;
                _Logging.Debug(_Header + "readyz probe failed for endpoint " + endpoint.Id + ": " + ex.Message);
            }

            try
            {
                status.Capabilities = await GetCapabilitiesAsync(endpoint, token).ConfigureAwait(false);
                status.LastCapabilitiesUtc = status.Capabilities?.CollectedUtc ?? DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                status.LastError = ex.Message;
                _Logging.Debug(_Header + "capabilities probe failed for endpoint " + endpoint.Id + ": " + ex.Message);
            }

            try
            {
                status.Telemetry = await GetTelemetryAsync(endpoint, token).ConfigureAwait(false);
                status.LastTelemetryUtc = status.Telemetry?.CollectedUtc ?? DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                status.LastError = ex.Message;
                _Logging.Debug(_Header + "telemetry probe failed for endpoint " + endpoint.Id + ": " + ex.Message);
            }

            return status;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _HttpClient.Dispose();
        }

        private async Task<T> GetJsonAsync<T>(string url, int timeoutMs, CancellationToken token)
        {
            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(timeoutMs);
                using (HttpResponseMessage response = await _HttpClient.GetAsync(url, cts.Token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonSerializer.Deserialize<T>(body, _JsonOptions);
                }
            }
        }
    }
}
