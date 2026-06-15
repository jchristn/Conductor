namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json.Serialization;
    using Conductor.Core.Helpers;
    using Conductor.Core.Serialization;

    /// <summary>
    /// Saved Analytics workspace report.
    /// </summary>
    public class AnalyticsSavedReport
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewAnalyticsSavedReportId();

        /// <summary>
        /// Tenant ID, or null for a global system-admin report.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// User ID that created or owns the report.
        /// </summary>
        public string OwnerUserId { get; set; } = null;

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(Name)) : value);
        }

        /// <summary>
        /// Optional description.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Report scope, such as Global or Tenant.
        /// </summary>
        public string Scope { get; set; } = "Tenant";

        /// <summary>
        /// Saved query definition.
        /// </summary>
        public AnalyticsQueryRequest Query
        {
            get => _Query;
            set => _Query = value ?? new AnalyticsQueryRequest();
        }

        /// <summary>
        /// Dashboard display state such as selected visualization, columns, or chart options.
        /// </summary>
        public object DisplayState { get; set; } = null;

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Updated UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Labels for categorization.
        /// </summary>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = value ?? new List<string>();
        }

        /// <summary>
        /// Tags for metadata.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// JSON-serialized query used for persistence.
        /// </summary>
        [JsonIgnore]
        public string QueryJson
        {
            get => _Serializer.SerializeJson(_Query, false);
            set => _Query = (String.IsNullOrEmpty(value) ? new AnalyticsQueryRequest() : _Serializer.DeserializeJson<AnalyticsQueryRequest>(value));
        }

        /// <summary>
        /// JSON-serialized display state used for persistence.
        /// </summary>
        [JsonIgnore]
        public string DisplayStateJson
        {
            get => (DisplayState != null ? _Serializer.SerializeJson(DisplayState, false) : null);
            set => DisplayState = (String.IsNullOrEmpty(value) ? null : _Serializer.DeserializeJson<object>(value));
        }

        /// <summary>
        /// JSON-serialized labels used for persistence.
        /// </summary>
        [JsonIgnore]
        public string LabelsJson
        {
            get => _Serializer.SerializeJson(_Labels, false);
            set => _Labels = (String.IsNullOrEmpty(value) ? new List<string>() : _Serializer.DeserializeJson<List<string>>(value));
        }

        /// <summary>
        /// JSON-serialized tags used for persistence.
        /// </summary>
        [JsonIgnore]
        public string TagsJson
        {
            get => _Serializer.SerializeJson(_Tags, false);
            set => _Tags = (String.IsNullOrEmpty(value) ? new Dictionary<string, string>() : _Serializer.DeserializeJson<Dictionary<string, string>>(value));
        }

        private string _Name = "Analytics report";
        private AnalyticsQueryRequest _Query = new AnalyticsQueryRequest();
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance.</returns>
        public static AnalyticsSavedReport FromDataRow(DataRow row)
        {
            if (row == null) return null;

            AnalyticsSavedReport report = new AnalyticsSavedReport
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                OwnerUserId = DataTableHelper.GetStringValue(row, "owneruserid"),
                Name = DataTableHelper.GetStringValue(row, "name") ?? "Analytics report",
                Description = DataTableHelper.GetStringValue(row, "description"),
                Scope = DataTableHelper.GetStringValue(row, "scope") ?? "Tenant",
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc")
            };

            report.QueryJson = DataTableHelper.GetStringValue(row, "queryjson");
            report.DisplayStateJson = DataTableHelper.GetStringValue(row, "displaystatejson");
            report.LabelsJson = DataTableHelper.GetStringValue(row, "labels");
            report.TagsJson = DataTableHelper.GetStringValue(row, "tags");
            return report;
        }

        /// <summary>
        /// Instantiate list from DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of saved reports.</returns>
        public static List<AnalyticsSavedReport> FromDataTable(DataTable table)
        {
            if (table == null) return new List<AnalyticsSavedReport>();

            List<AnalyticsSavedReport> reports = new List<AnalyticsSavedReport>();
            foreach (DataRow row in table.Rows)
            {
                AnalyticsSavedReport report = FromDataRow(row);
                if (report != null) reports.Add(report);
            }

            return reports;
        }
    }
}
