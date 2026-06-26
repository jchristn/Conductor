namespace Conductor.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Lightweight Conductor management-plane client.
    /// </summary>
    public sealed class ConductorClient : IDisposable
    {
        private readonly HttpClient _HttpClient;
        private readonly bool _OwnsHttpClient;
        private readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null
        };

        /// <summary>
        /// Instantiate the client.
        /// </summary>
        /// <param name="baseUrl">Conductor server base URL.</param>
        /// <param name="bearerToken">Bearer token or API key.</param>
        /// <param name="adminEmail">Administrator email for admin-header authentication.</param>
        /// <param name="adminPassword">Administrator password for admin-header authentication.</param>
        /// <param name="httpClient">Optional HTTP client.</param>
        public ConductorClient(
            string baseUrl,
            string bearerToken = null,
            string adminEmail = null,
            string adminPassword = null,
            HttpClient httpClient = null)
        {
            if (String.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentNullException(nameof(baseUrl));
            }

            _HttpClient = httpClient ?? new HttpClient();
            _OwnsHttpClient = httpClient == null;
            _HttpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

            if (!String.IsNullOrWhiteSpace(adminEmail) && !String.IsNullOrWhiteSpace(adminPassword))
            {
                _HttpClient.DefaultRequestHeaders.Add("x-admin-email", adminEmail);
                _HttpClient.DefaultRequestHeaders.Add("x-admin-password", adminPassword);
            }
            else if (!String.IsNullOrWhiteSpace(bearerToken))
            {
                _HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }
        }

        /// <summary>
        /// Validate a virtual model runner draft.
        /// </summary>
        /// <param name="draft">Virtual model runner draft.</param>
        /// <param name="existingId">Optional existing virtual model runner ID for update validation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> ValidateVirtualModelRunnerAsync(object draft, string existingId = null, CancellationToken token = default)
        {
            Dictionary<string, string> query = new Dictionary<string, string> { ["existingId"] = existingId };
            return await PostJsonAsync("/v1.0/virtualmodelrunners/validate" + QueryString(query), draft ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// List endpoint groups.
        /// </summary>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> ListEndpointGroupsAsync(IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/endpointgroups" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get an endpoint group.
        /// </summary>
        /// <param name="id">Endpoint group ID.</param>
        /// <param name="tenantId">Optional tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetEndpointGroupAsync(string id, string tenantId = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/endpointgroups/" + Uri.EscapeDataString(id) + QueryString(new Dictionary<string, string> { ["tenantId"] = tenantId }), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create an endpoint group.
        /// </summary>
        /// <param name="group">Endpoint group payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> CreateEndpointGroupAsync(object group, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/endpointgroups", group ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update an endpoint group.
        /// </summary>
        /// <param name="id">Endpoint group ID.</param>
        /// <param name="group">Endpoint group payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> UpdateEndpointGroupAsync(string id, object group, CancellationToken token = default)
        {
            return await PutJsonAsync("/v1.0/endpointgroups/" + Uri.EscapeDataString(id), group ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete an endpoint group.
        /// </summary>
        /// <param name="id">Endpoint group ID.</param>
        /// <param name="tenantId">Optional tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteEndpointGroupAsync(string id, string tenantId = null, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/endpointgroups/" + Uri.EscapeDataString(id) + QueryString(new Dictionary<string, string> { ["tenantId"] = tenantId }), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Validate an endpoint group draft.
        /// </summary>
        /// <param name="draft">Endpoint group draft.</param>
        /// <param name="existingId">Optional existing endpoint group ID for update validation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> ValidateEndpointGroupAsync(object draft, string existingId = null, CancellationToken token = default)
        {
            Dictionary<string, string> query = new Dictionary<string, string> { ["existingId"] = existingId };
            return await PostJsonAsync("/v1.0/endpointgroups/validate" + QueryString(query), draft ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get effective virtual model runner configuration.
        /// </summary>
        /// <param name="id">Virtual model runner ID.</param>
        /// <param name="tenantId">Optional tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetVirtualModelRunnerEffectiveConfigurationAsync(string id, string tenantId = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/virtualmodelrunners/" + Uri.EscapeDataString(id) + "/effective" + QueryString(new Dictionary<string, string> { ["tenantId"] = tenantId }), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Load or verify a model through a virtual model runner.
        /// </summary>
        /// <param name="id">Virtual model runner ID.</param>
        /// <param name="payload">Load model request payload.</param>
        /// <param name="tenantId">Optional tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> LoadVirtualModelRunnerModelAsync(string id, object payload, string tenantId = null, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/virtualmodelrunners/" + Uri.EscapeDataString(id) + "/load-model" + QueryString(new Dictionary<string, string> { ["tenantId"] = tenantId }), payload ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Explain endpoint routing for a virtual model runner.
        /// </summary>
        /// <param name="id">Virtual model runner ID.</param>
        /// <param name="payload">Routing simulation payload.</param>
        /// <param name="tenantId">Optional tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> ExplainVirtualModelRunnerRoutingAsync(string id, object payload, string tenantId = null, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/virtualmodelrunners/" + Uri.EscapeDataString(id) + "/explain-routing" + QueryString(new Dictionary<string, string> { ["tenantId"] = tenantId }), payload ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get endpoint runtime statistics for a virtual model runner.
        /// </summary>
        /// <param name="id">Virtual model runner ID.</param>
        /// <param name="filters">Query-string filters such as tenantId and endpointId.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetVirtualModelRunnerRuntimeStatsAsync(string id, IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/virtualmodelrunners/" + Uri.EscapeDataString(id) + "/runtime-stats" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Reset endpoint runtime statistics for a virtual model runner.
        /// </summary>
        /// <param name="id">Virtual model runner ID.</param>
        /// <param name="filters">Query-string filters such as tenantId and endpointId.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> ResetVirtualModelRunnerRuntimeStatsAsync(string id, IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/virtualmodelrunners/" + Uri.EscapeDataString(id) + "/runtime-stats/reset" + QueryString(filters), new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Clear transient runtime backoff for a virtual model runner.
        /// </summary>
        /// <param name="id">Virtual model runner ID.</param>
        /// <param name="filters">Query-string filters such as tenantId and endpointId.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> ClearVirtualModelRunnerRuntimeBackoffAsync(string id, IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/virtualmodelrunners/" + Uri.EscapeDataString(id) + "/runtime-backoff/clear" + QueryString(filters), new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// List virtual model runner reservations.
        /// </summary>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> ListVirtualModelRunnerReservationsAsync(IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/vmrreservations" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a virtual model runner reservation.
        /// </summary>
        /// <param name="id">Reservation ID.</param>
        /// <param name="tenantId">Optional tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetVirtualModelRunnerReservationAsync(string id, string tenantId = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/vmrreservations/" + Uri.EscapeDataString(id) + QueryString(new Dictionary<string, string> { ["tenantId"] = tenantId }), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create a virtual model runner reservation.
        /// </summary>
        /// <param name="reservation">Reservation object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> CreateVirtualModelRunnerReservationAsync(object reservation, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/vmrreservations", reservation ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a virtual model runner reservation.
        /// </summary>
        /// <param name="id">Reservation ID.</param>
        /// <param name="reservation">Reservation object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> UpdateVirtualModelRunnerReservationAsync(string id, object reservation, CancellationToken token = default)
        {
            return await PutJsonAsync("/v1.0/vmrreservations/" + Uri.EscapeDataString(id), reservation ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deactivate a virtual model runner reservation.
        /// </summary>
        /// <param name="id">Reservation ID.</param>
        /// <param name="tenantId">Optional tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteVirtualModelRunnerReservationAsync(string id, string tenantId = null, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/vmrreservations/" + Uri.EscapeDataString(id) + QueryString(new Dictionary<string, string> { ["tenantId"] = tenantId }), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Validate a virtual model runner reservation draft.
        /// </summary>
        /// <param name="reservation">Reservation object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> ValidateVirtualModelRunnerReservationAsync(object reservation, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/vmrreservations/validate", reservation ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// List reservations for a virtual model runner.
        /// </summary>
        /// <param name="virtualModelRunnerId">Virtual model runner ID.</param>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> ListReservationsForVirtualModelRunnerAsync(string virtualModelRunnerId, IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/virtualmodelrunners/" + Uri.EscapeDataString(virtualModelRunnerId) + "/reservations" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluate effective reservation access for a virtual model runner.
        /// </summary>
        /// <param name="virtualModelRunnerId">Virtual model runner ID.</param>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetVirtualModelRunnerReservationEffectiveAsync(string virtualModelRunnerId, IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/virtualmodelrunners/" + Uri.EscapeDataString(virtualModelRunnerId) + "/reservation-effective" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the analytics catalog.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetAnalyticsCatalogAsync(CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/analytics/catalog", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Run a typed analytics query.
        /// </summary>
        /// <param name="query">Query object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> QueryAnalyticsAsync(object query, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/analytics/query", query ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// List analytics saved reports.
        /// </summary>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> ListAnalyticsSavedReportsAsync(IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/analytics/reports" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create an analytics saved report.
        /// </summary>
        /// <param name="report">Saved report object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> CreateAnalyticsSavedReportAsync(object report, CancellationToken token = default)
        {
            return await PostJsonAsync("/v1.0/analytics/reports", report ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get an analytics saved report.
        /// </summary>
        /// <param name="id">Saved report ID.</param>
        /// <param name="tenantId">Optional tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetAnalyticsSavedReportAsync(string id, string tenantId = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/analytics/reports/" + Uri.EscapeDataString(id) + QueryString(new Dictionary<string, string> { ["tenantId"] = tenantId }), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update an analytics saved report.
        /// </summary>
        /// <param name="id">Saved report ID.</param>
        /// <param name="report">Saved report object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> UpdateAnalyticsSavedReportAsync(string id, object report, CancellationToken token = default)
        {
            return await PutJsonAsync("/v1.0/analytics/reports/" + Uri.EscapeDataString(id), report ?? new object(), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete an analytics saved report.
        /// </summary>
        /// <param name="id">Saved report ID.</param>
        /// <param name="tenantId">Optional tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAnalyticsSavedReportAsync(string id, string tenantId = null, CancellationToken token = default)
        {
            await DeleteAsync("/v1.0/analytics/reports/" + Uri.EscapeDataString(id) + QueryString(new Dictionary<string, string> { ["tenantId"] = tenantId }), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get analytics summary.
        /// </summary>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetAnalyticsSummaryAsync(IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/analytics/summary" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get analytics time series.
        /// </summary>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetAnalyticsTimeSeriesAsync(IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/analytics/timeseries" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get TTFT analytics.
        /// </summary>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetAnalyticsTtftAsync(IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/analytics/ttft" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get token analytics.
        /// </summary>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetAnalyticsTokensAsync(IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/analytics/tokens" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get estimate-only cost analytics.
        /// </summary>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetAnalyticsCostsAsync(IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/analytics/costs" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get user analytics.
        /// </summary>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetAnalyticsUsersAsync(IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/analytics/users" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get access and reliability analytics.
        /// </summary>
        /// <param name="filters">Query-string filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>JSON response.</returns>
        public async Task<JsonDocument> GetAnalyticsAccessAsync(IDictionary<string, string> filters = null, CancellationToken token = default)
        {
            return await GetJsonAsync("/v1.0/analytics/access" + QueryString(filters), token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_OwnsHttpClient)
            {
                _HttpClient.Dispose();
            }
        }

        private async Task<JsonDocument> GetJsonAsync(string endpoint, CancellationToken token)
        {
            using HttpResponseMessage response = await _HttpClient.GetAsync(endpoint.TrimStart('/'), token).ConfigureAwait(false);
            return await ReadJsonResponseAsync(endpoint, response, token).ConfigureAwait(false);
        }

        private async Task<JsonDocument> PostJsonAsync(string endpoint, object body, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(body, _JsonOptions);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _HttpClient.PostAsync(endpoint.TrimStart('/'), content, token).ConfigureAwait(false);
            return await ReadJsonResponseAsync(endpoint, response, token).ConfigureAwait(false);
        }

        private async Task<JsonDocument> PutJsonAsync(string endpoint, object body, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(body, _JsonOptions);
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _HttpClient.PutAsync(endpoint.TrimStart('/'), content, token).ConfigureAwait(false);
            return await ReadJsonResponseAsync(endpoint, response, token).ConfigureAwait(false);
        }

        private async Task DeleteAsync(string endpoint, CancellationToken token)
        {
            using HttpResponseMessage response = await _HttpClient.DeleteAsync(endpoint.TrimStart('/'), token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                throw new ConductorApiException(
                    "Conductor API request failed with HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                    response.StatusCode,
                    endpoint,
                    responseBody);
            }
        }

        private static async Task<JsonDocument> ReadJsonResponseAsync(string endpoint, HttpResponseMessage response, CancellationToken token)
        {
            string responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new ConductorApiException(
                    "Conductor API request failed with HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                    response.StatusCode,
                    endpoint,
                    responseBody);
            }

            return JsonDocument.Parse(responseBody);
        }

        private static string QueryString(IDictionary<string, string> filters)
        {
            if (filters == null || filters.Count < 1)
            {
                return "";
            }

            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string> filter in filters)
            {
                if (!String.IsNullOrEmpty(filter.Value))
                {
                    parts.Add(Uri.EscapeDataString(filter.Key) + "=" + Uri.EscapeDataString(filter.Value));
                }
            }

            return parts.Count > 0 ? "?" + String.Join("&", parts) : "";
        }
    }
}
