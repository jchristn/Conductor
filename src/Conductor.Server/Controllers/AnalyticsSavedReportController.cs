namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Analytics saved report API controller.
    /// </summary>
    public class AnalyticsSavedReportController : BaseController
    {
        private static readonly HashSet<string> SensitiveMetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "apikey",
            "authorization",
            "bearer",
            "body",
            "cookie",
            "headers",
            "password",
            "prompt",
            "providerkey",
            "rawcompletion",
            "rawprompt",
            "requestbody",
            "responsebody",
            "secret",
            "setcookie"
        };

        /// <summary>
        /// Instantiate the analytics saved report controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        public AnalyticsSavedReportController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging)
            : base(database, authService, serializer, logging)
        {
        }

        /// <summary>
        /// Create a saved report.
        /// </summary>
        /// <param name="tenantId">Tenant ID, or null for global scope.</param>
        /// <param name="ownerUserId">Owner user ID.</param>
        /// <param name="report">Report to create.</param>
        /// <returns>Created report.</returns>
        public async Task<AnalyticsSavedReport> Create(string tenantId, string ownerUserId, AnalyticsSavedReport report)
        {
            if (report == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            NormalizeForScope(tenantId, ownerUserId, report);
            report.Id = IdGenerator.NewAnalyticsSavedReportId();

            if (String.IsNullOrWhiteSpace(report.Name))
                throw new WebserverException(ApiResultEnum.BadRequest, "Name is required");

            return await Database.AnalyticsSavedReport.CreateAsync(report).ConfigureAwait(false);
        }

        /// <summary>
        /// Read a saved report.
        /// </summary>
        /// <param name="tenantId">Tenant ID, or null for global scope.</param>
        /// <param name="id">Report ID.</param>
        /// <returns>Saved report.</returns>
        public async Task<AnalyticsSavedReport> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            AnalyticsSavedReport report = await Database.AnalyticsSavedReport.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (report == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return report;
        }

        /// <summary>
        /// Update a saved report.
        /// </summary>
        /// <param name="tenantId">Tenant ID, or null for global scope.</param>
        /// <param name="ownerUserId">Owner user ID.</param>
        /// <param name="id">Report ID.</param>
        /// <param name="report">Updated report.</param>
        /// <returns>Updated report.</returns>
        public async Task<AnalyticsSavedReport> Update(string tenantId, string ownerUserId, string id, AnalyticsSavedReport report)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            AnalyticsSavedReport existing = await Database.AnalyticsSavedReport.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (existing == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            if (report == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            NormalizeForScope(tenantId, ownerUserId ?? existing.OwnerUserId, report);
            report.Id = id;
            report.CreatedUtc = existing.CreatedUtc;

            if (String.IsNullOrWhiteSpace(report.Name))
                throw new WebserverException(ApiResultEnum.BadRequest, "Name is required");

            return await Database.AnalyticsSavedReport.UpdateAsync(report).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a saved report.
        /// </summary>
        /// <param name="tenantId">Tenant ID, or null for global scope.</param>
        /// <param name="id">Report ID.</param>
        /// <returns>Task.</returns>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.AnalyticsSavedReport.ExistsAsync(tenantId, id).ConfigureAwait(false);
            if (!exists)
                throw new WebserverException(ApiResultEnum.NotFound);

            await Database.AnalyticsSavedReport.DeleteAsync(tenantId, id).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate saved reports.
        /// </summary>
        /// <param name="tenantId">Tenant ID, or null for global scope.</param>
        /// <param name="maxResults">Maximum number of results.</param>
        /// <param name="continuationToken">Continuation token.</param>
        /// <param name="nameFilter">Name filter.</param>
        /// <param name="ownerUserId">Owner user filter.</param>
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult<AnalyticsSavedReport>> Enumerate(string tenantId, int? maxResults = null, string continuationToken = null, string nameFilter = null, string ownerUserId = null)
        {
            EnumerationRequest request = new EnumerationRequest
            {
                ContinuationToken = continuationToken,
                NameFilter = nameFilter
            };

            if (maxResults.HasValue) request.MaxResults = maxResults.Value;

            return await Database.AnalyticsSavedReport.EnumerateAsync(tenantId, request, ownerUserId).ConfigureAwait(false);
        }

        private static void NormalizeForScope(string tenantId, string ownerUserId, AnalyticsSavedReport report)
        {
            report.TenantId = tenantId;
            report.OwnerUserId = ownerUserId ?? report.OwnerUserId;
            report.Scope = String.IsNullOrEmpty(tenantId) ? "Global" : "Tenant";
            report.Query = report.Query ?? new AnalyticsQueryRequest();
            AnalyticsQueryService.ValidateRequestDefinition(report.Query);
            ValidateSafeReportMetadata(report);
        }

        private static void ValidateSafeReportMetadata(AnalyticsSavedReport report)
        {
            if (report == null)
            {
                return;
            }

            if (report.Labels != null)
            {
                foreach (string label in report.Labels)
                {
                    ThrowIfSensitiveMetadataName("label", label);
                    ThrowIfSensitiveMetadataValue("label", label);
                }
            }

            if (report.Tags != null)
            {
                foreach (KeyValuePair<string, string> tag in report.Tags)
                {
                    ThrowIfSensitiveMetadataName("tag key", tag.Key);
                    ThrowIfSensitiveMetadataValue("tag value", tag.Value);
                }
            }

            if (report.DisplayState == null)
            {
                return;
            }

            string displayStateJson = report.DisplayStateJson;
            if (String.IsNullOrWhiteSpace(displayStateJson))
            {
                return;
            }

            using JsonDocument document = JsonDocument.Parse(displayStateJson);
            ValidateSafeJsonElement("DisplayState", document.RootElement);
        }

        private static void ValidateSafeJsonElement(string path, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        ThrowIfSensitiveMetadataName(path + "." + property.Name, property.Name);
                        ValidateSafeJsonElement(path + "." + property.Name, property.Value);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        ValidateSafeJsonElement(path, item);
                    }
                    break;

                case JsonValueKind.String:
                    ThrowIfSensitiveMetadataValue(path, element.GetString());
                    break;
            }
        }

        private static void ThrowIfSensitiveMetadataName(string fieldName, string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string normalized = NormalizeMetadataName(value);
            if (SensitiveMetadataKeys.Contains(normalized))
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Analytics saved reports cannot persist sensitive payload or secret metadata field: " + fieldName + ".");
            }
        }

        private static string NormalizeMetadataName(string value)
        {
            char[] chars = value.ToCharArray();
            List<char> normalized = new List<char>();
            foreach (char c in chars)
            {
                if (Char.IsLetterOrDigit(c))
                {
                    normalized.Add(Char.ToLowerInvariant(c));
                }
            }

            return new string(normalized.ToArray());
        }

        private static void ThrowIfSensitiveMetadataValue(string fieldName, string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (value.IndexOf("Bearer ", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Authorization:", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("-----BEGIN ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, "Analytics saved reports cannot persist sensitive payload or secret metadata value: " + fieldName + ".");
            }
        }
    }
}
