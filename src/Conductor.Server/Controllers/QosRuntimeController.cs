namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Read-only access to live QoS runtime state: per-runner scheduler state, capacity usage, per-class admission
    /// statistics, endpoint concurrency, and time-bucketed admission history. All data is held in memory by the
    /// admission service and resets when the server restarts. Thread-safe.
    /// </summary>
    public class QosRuntimeController : BaseController
    {
        /// <summary>
        /// Longest history window that can be requested, in minutes. Default 1440 (24 hours), matching the
        /// admission service's default statistics retention.
        /// </summary>
        public int MaxHistoryWindowMinutes
        {
            get => _MaxHistoryWindowMinutes;
            set => _MaxHistoryWindowMinutes = (value < 1 ? 1440 : value);
        }

        private static readonly string _Header = "[QosRuntimeController] ";

        private readonly QosAdmissionService _AdmissionService;
        private readonly HealthCheckService _HealthCheckService;
        private readonly RoutingDecisionService _RoutingDecisionService;
        private int _MaxHistoryWindowMinutes = 1440;

        /// <summary>
        /// Instantiate the controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="admissionService">QoS admission service. Nullable; when null every runner reports state Idle.</param>
        /// <param name="healthCheckService">Health check service for endpoint concurrency. Nullable.</param>
        /// <param name="routingDecisionService">Routing decision service for endpoint resolution. Nullable; when null endpoints are omitted.</param>
        public QosRuntimeController(
            DatabaseDriverBase database,
            AuthenticationService authService,
            Serializer serializer,
            LoggingModule logging,
            QosAdmissionService admissionService,
            HealthCheckService healthCheckService,
            RoutingDecisionService routingDecisionService)
            : base(database, authService, serializer, logging)
        {
            _AdmissionService = admissionService;
            _HealthCheckService = healthCheckService;
            _RoutingDecisionService = routingDecisionService;
        }

        /// <summary>
        /// List the QoS runtime snapshot of every virtual model runner in scope.
        /// </summary>
        /// <param name="tenantId">Tenant id. Null or empty lists every tenant (global administrators only; enforced by the caller).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Snapshots ordered by runner name. Never null.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
        public async Task<List<QosRuntimeSnapshot>> Enumerate(string tenantId, CancellationToken token = default)
        {
            List<QosRuntimeSnapshot> snapshots = new List<QosRuntimeSnapshot>();
            string continuation = null;

            do
            {
                token.ThrowIfCancellationRequested();
                EnumerationResult<VirtualModelRunner> page = await Database.VirtualModelRunner.EnumerateAsync(
                    tenantId,
                    new EnumerationRequest { MaxResults = 1000, ContinuationToken = continuation },
                    token).ConfigureAwait(false);

                if (page?.Data != null)
                {
                    foreach (VirtualModelRunner vmr in page.Data)
                    {
                        snapshots.Add(await BuildSnapshotAsync(vmr, token).ConfigureAwait(false));
                    }
                }

                continuation = (page != null && page.HasMore) ? page.ContinuationToken : null;
            }
            while (!String.IsNullOrEmpty(continuation));

            return snapshots
                .OrderBy(item => item.VirtualModelRunnerName ?? item.VirtualModelRunnerId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Read the QoS runtime snapshot of one virtual model runner.
        /// </summary>
        /// <param name="tenantId">Tenant id. Null or empty reads across tenants (global administrators only).</param>
        /// <param name="vmrId">Virtual model runner id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The snapshot. Never null.</returns>
        /// <exception cref="WebserverException">Thrown with BadRequest when the id is missing, or NotFound when the runner does not exist in scope.</exception>
        public async Task<QosRuntimeSnapshot> Read(string tenantId, string vmrId, CancellationToken token = default)
        {
            VirtualModelRunner vmr = await ReadVmrAsync(tenantId, vmrId, token).ConfigureAwait(false);
            return await BuildSnapshotAsync(vmr, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Read time-bucketed QoS admission history for one virtual model runner.
        /// </summary>
        /// <param name="tenantId">Tenant id. Null or empty reads across tenants (global administrators only).</param>
        /// <param name="vmrId">Virtual model runner id.</param>
        /// <param name="startUtc">Window start as an ISO-8601 timestamp. Null or empty defaults to one hour before the end.</param>
        /// <param name="endUtc">Window end as an ISO-8601 timestamp. Null or empty defaults to now.</param>
        /// <param name="interval">Bucket interval: minute (default), 5minute, 15minute, or hour.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The history. Never null.</returns>
        /// <exception cref="WebserverException">
        /// Thrown with BadRequest when the id is missing, a timestamp or interval is invalid, the start is not before
        /// the end, or the window exceeds <see cref="MaxHistoryWindowMinutes"/>; or NotFound when the runner does not exist in scope.
        /// </exception>
        public async Task<QosRuntimeHistory> ReadHistory(string tenantId, string vmrId, string startUtc, string endUtc, string interval, CancellationToken token = default)
        {
            VirtualModelRunner vmr = await ReadVmrAsync(tenantId, vmrId, token).ConfigureAwait(false);

            DateTime end = ParseUtc(endUtc, "endUtc") ?? DateTime.UtcNow;
            DateTime start = ParseUtc(startUtc, "startUtc") ?? end.AddHours(-1);
            if (start >= end) throw new WebserverException(ApiResultEnum.BadRequest, "startUtc must be before endUtc.");
            if ((end - start).TotalMinutes > _MaxHistoryWindowMinutes)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "The requested window exceeds the maximum of " + _MaxHistoryWindowMinutes + " minutes.");
            }

            string normalizedInterval = String.IsNullOrWhiteSpace(interval) ? "minute" : interval.Trim().ToLowerInvariant();
            int intervalMinutes;
            switch (normalizedInterval)
            {
                case "minute": intervalMinutes = 1; break;
                case "5minute": intervalMinutes = 5; break;
                case "15minute": intervalMinutes = 15; break;
                case "hour": intervalMinutes = 60; break;
                default: throw new WebserverException(ApiResultEnum.BadRequest, "interval must be one of minute, 5minute, 15minute, or hour.");
            }

            List<QosRuntimeHistoryBucket> buckets = _AdmissionService != null
                ? _AdmissionService.GetHistory(vmr.Id, start, end, intervalMinutes)
                : new List<QosRuntimeHistoryBucket>();

            return new QosRuntimeHistory
            {
                VirtualModelRunnerId = vmr.Id,
                StartUtc = start,
                EndUtc = end,
                Interval = normalizedInterval,
                Classes = buckets.Select(item => item.ClassName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
                Buckets = buckets
            };
        }

        private async Task<VirtualModelRunner> ReadVmrAsync(string tenantId, string vmrId, CancellationToken token)
        {
            if (String.IsNullOrEmpty(vmrId)) throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            VirtualModelRunner vmr = String.IsNullOrEmpty(tenantId)
                ? await Database.VirtualModelRunner.ReadByIdAsync(vmrId, token).ConfigureAwait(false)
                : await Database.VirtualModelRunner.ReadAsync(tenantId, vmrId, token).ConfigureAwait(false);

            if (vmr == null) throw new WebserverException(ApiResultEnum.NotFound);
            return vmr;
        }

        private async Task<QosRuntimeSnapshot> BuildSnapshotAsync(VirtualModelRunner vmr, CancellationToken token)
        {
            QosRuntimeSnapshot snapshot = _AdmissionService != null
                ? _AdmissionService.GetSnapshot(vmr)
                : new QosRuntimeSnapshot
                {
                    TenantId = vmr.TenantId,
                    VirtualModelRunnerId = vmr.Id,
                    VirtualModelRunnerName = vmr.Name,
                    QosProfileId = vmr.QosProfileId
                };

            if (String.IsNullOrEmpty(snapshot.QosProfileName) && !String.IsNullOrEmpty(snapshot.QosProfileId))
            {
                try
                {
                    QosProfile profile = await Database.QosProfile.ReadByIdAsync(snapshot.QosProfileId, token).ConfigureAwait(false);
                    snapshot.QosProfileName = profile?.Name;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Logging.Debug(_Header + "failed to read QoS profile " + snapshot.QosProfileId + ": " + ex.Message);
                }
            }

            if (_RoutingDecisionService == null) return snapshot;

            List<ModelRunnerEndpoint> endpoints = await _RoutingDecisionService.GetEndpointsAsync(vmr, token).ConfigureAwait(false);
            foreach (ModelRunnerEndpoint endpoint in endpoints.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                EndpointHealthState state = _HealthCheckService?.GetHealthState(endpoint.Id);
                snapshot.Endpoints.Add(new QosEndpointSlotSnapshot
                {
                    EndpointId = endpoint.Id,
                    EndpointName = endpoint.Name,
                    InFlight = state?.InFlightRequests ?? 0,
                    MaxParallelRequests = endpoint.MaxParallelRequests,
                    IsHealthy = state == null || state.IsHealthy,
                    Active = endpoint.Active
                });
            }

            return snapshot;
        }

        private static DateTime? ParseUtc(string value, string name)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;

            // Query values arrive percent-encoded (an ISO-8601 colon is sent as %3A).
            string decoded;
            try { decoded = Uri.UnescapeDataString(value); }
            catch (UriFormatException) { decoded = value; }

            if (!DateTime.TryParse(decoded, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
            {
                throw new WebserverException(ApiResultEnum.BadRequest, name + " is not a valid timestamp.");
            }

            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
    }
}
