namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json.Serialization;
    using Conductor.Core.Helpers;
    using Conductor.Core.Serialization;

    /// <summary>
    /// Tenant-scoped reusable priority and traffic-split grouping for model runner endpoints.
    /// </summary>
    public class EndpointGroup
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Group identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewEndpointGroupId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = value;
        }

        /// <summary>
        /// Operator-facing group name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = value;
        }

        /// <summary>
        /// Optional description.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Lower priority numbers are preferred before higher priority numbers.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Whether the group is available for routing.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Traffic weight inside the selected priority level.
        /// </summary>
        public int TrafficWeight
        {
            get => _TrafficWeight;
            set => _TrafficWeight = value;
        }

        /// <summary>
        /// Endpoint identifiers that belong to this group.
        /// </summary>
        public List<string> EndpointIds
        {
            get => _EndpointIds;
            set => _EndpointIds = value ?? new List<string>();
        }

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
        /// Tags for key-value metadata.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = value ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Free-form metadata.
        /// </summary>
        public object Metadata { get; set; } = null;

        /// <summary>
        /// JSON-serialized endpoint identifiers for persistence.
        /// </summary>
        [JsonIgnore]
        public string EndpointIdsJson
        {
            get => _Serializer.SerializeJson(_EndpointIds, false);
            set => _EndpointIds = String.IsNullOrEmpty(value) ? new List<string>() : _Serializer.DeserializeJson<List<string>>(value);
        }

        /// <summary>
        /// JSON-serialized labels used for persistence.
        /// </summary>
        [JsonIgnore]
        public string LabelsJson
        {
            get => _Serializer.SerializeJson(_Labels, false);
            set => _Labels = String.IsNullOrEmpty(value) ? new List<string>() : _Serializer.DeserializeJson<List<string>>(value);
        }

        /// <summary>
        /// JSON-serialized tags used for persistence.
        /// </summary>
        [JsonIgnore]
        public string TagsJson
        {
            get => _Serializer.SerializeJson(_Tags, false);
            set => _Tags = String.IsNullOrEmpty(value) ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : _Serializer.DeserializeJson<Dictionary<string, string>>(value);
        }

        /// <summary>
        /// JSON-serialized metadata used for persistence.
        /// </summary>
        [JsonIgnore]
        public string MetadataJson
        {
            get => Metadata != null ? _Serializer.SerializeJson(Metadata, false) : null;
            set => Metadata = String.IsNullOrEmpty(value) ? null : _Serializer.DeserializeJson<object>(value);
        }

        private string _TenantId = null;
        private string _Name = "Default";
        private int _TrafficWeight = 100;
        private List<string> _EndpointIds = new List<string>();
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Endpoint group instance.</returns>
        public static EndpointGroup FromDataRow(DataRow row)
        {
            if (row == null) return null;

            EndpointGroup obj = new EndpointGroup
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                Name = DataTableHelper.GetStringValue(row, "name") ?? "Default",
                Description = DataTableHelper.GetStringValue(row, "description"),
                Priority = DataTableHelper.GetIntValue(row, "priority"),
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                TrafficWeight = DataTableHelper.GetIntValue(row, "trafficweight"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc")
            };

            string endpointIdsJson = DataTableHelper.GetStringValue(row, "endpointids");
            if (!String.IsNullOrEmpty(endpointIdsJson))
            {
                obj.EndpointIdsJson = endpointIdsJson;
            }

            string labelsJson = DataTableHelper.GetStringValue(row, "labels");
            if (!String.IsNullOrEmpty(labelsJson))
            {
                obj.LabelsJson = labelsJson;
            }

            string tagsJson = DataTableHelper.GetStringValue(row, "tags");
            if (!String.IsNullOrEmpty(tagsJson))
            {
                obj.TagsJson = tagsJson;
            }

            string metadataJson = DataTableHelper.GetStringValue(row, "metadata");
            if (!String.IsNullOrEmpty(metadataJson))
            {
                obj.MetadataJson = metadataJson;
            }

            return obj;
        }

        /// <summary>
        /// Instantiate list from DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of endpoint groups.</returns>
        public static List<EndpointGroup> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<EndpointGroup>();

            List<EndpointGroup> ret = new List<EndpointGroup>();
            foreach (DataRow row in table.Rows)
            {
                EndpointGroup obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }

            return ret;
        }
    }
}
