namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;

    /// <summary>
    /// Helpers shared by the end-to-end proxy tests. All members are static and thread safe.
    /// </summary>
    public static class ProxyTestHelpers
    {
        /// <summary>
        /// Build a JSON request message.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Path relative to the proxy base URL.</param>
        /// <param name="json">JSON body. Null sends no body.</param>
        /// <param name="headers">Extra request headers. Null allowed.</param>
        /// <returns>The request. Never null.</returns>
        public static HttpRequestMessage Build(HttpMethod method, string path, string json, Dictionary<string, string> headers = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, path);
            if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return request;
        }

        /// <summary>
        /// Get a response header value from either the response or content header collections.
        /// </summary>
        /// <param name="response">Response. Must not be null.</param>
        /// <param name="name">Header name.</param>
        /// <returns>The joined value, or null when absent.</returns>
        public static string GetHeader(HttpResponseMessage response, string name)
        {
            if (response.Headers.TryGetValues(name, out IEnumerable<string> values)) return String.Join(", ", values);
            if (response.Content != null && response.Content.Headers.TryGetValues(name, out IEnumerable<string> contentValues)) return String.Join(", ", contentValues);
            return null;
        }

        /// <summary>
        /// Wait until a request history entry for a runner has completed (HttpStatus set) and return it.
        /// </summary>
        /// <param name="database">Database. Must not be null.</param>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="vmrId">Runner id.</param>
        /// <returns>The completed entry, or null after 10 seconds.</returns>
        public static async Task<RequestHistoryEntry> WaitForHistoryAsync(DatabaseDriverBase database, string tenantId, string vmrId)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                RequestHistorySearchResult result = await database.RequestHistory.SearchAsync(new RequestHistorySearchFilter
                {
                    TenantGuid = tenantId,
                    VirtualModelRunnerGuid = vmrId,
                    Page = 1,
                    PageSize = 10
                }).ConfigureAwait(false);

                RequestHistoryEntry entry = result?.Data?.FirstOrDefault(item => item.HttpStatus.HasValue && item.HttpStatus.Value > 0 && item.CompletedUtc.HasValue);
                if (entry != null) return entry;
                await Task.Delay(50).ConfigureAwait(false);
            }

            return null;
        }
    }
}
