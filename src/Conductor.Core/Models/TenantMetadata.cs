namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json.Serialization;
    using Conductor.Core.Helpers;
    using Conductor.Core.Serialization;

    /// <summary>
    /// Tenant metadata.
    /// </summary>
    public class TenantMetadata
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewTenantId();

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Name)) : value);
        }

        /// <summary>
        /// Boolean indicating if the tenant is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC timestamp from creation.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp from last update.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Labels for categorization.
        /// </summary>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = (value != null ? value : new List<string>());
        }

        /// <summary>
        /// Tags for key-value metadata.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = (value != null ? value : new Dictionary<string, string>());
        }

        /// <summary>
        /// Free-form metadata object.
        /// </summary>
        public object Metadata { get; set; } = null;

        /// <summary>
        /// JSON-serialized labels for database storage.
        /// </summary>
        [JsonIgnore]
        public string LabelsJson
        {
            get => _Serializer.SerializeJson(_Labels, false);
            set => _Labels = (String.IsNullOrEmpty(value) ? new List<string>() : _Serializer.DeserializeJson<List<string>>(value));
        }

        /// <summary>
        /// JSON-serialized tags for database storage.
        /// </summary>
        [JsonIgnore]
        public string TagsJson
        {
            get => _Serializer.SerializeJson(_Tags, false);
            set => _Tags = (String.IsNullOrEmpty(value) ? new Dictionary<string, string>() : _Serializer.DeserializeJson<Dictionary<string, string>>(value));
        }

        /// <summary>
        /// JSON-serialized metadata for database storage.
        /// </summary>
        [JsonIgnore]
        public string MetadataJson
        {
            get => (Metadata != null ? _Serializer.SerializeJson(Metadata, false) : null);
            set => Metadata = (String.IsNullOrEmpty(value) ? null : _Serializer.DeserializeJson<object>(value));
        }

        private string _Name = "Default Tenant";
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate the tenant metadata.
        /// </summary>
        public TenantMetadata()
        {
        }

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance.</returns>
        public static TenantMetadata FromDataRow(DataRow row)
        {
            if (row == null) return null;

            TenantMetadata obj = new TenantMetadata
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                Name = DataTableHelper.GetStringValue(row, "name") ?? "Default Tenant",
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc")
            };

            string labelsJson = DataTableHelper.GetStringValue(row, "labels");
            if (!String.IsNullOrEmpty(labelsJson))
            {
                obj.Labels = _Serializer.DeserializeJson<List<string>>(labelsJson);
            }

            string tagsJson = DataTableHelper.GetStringValue(row, "tags");
            if (!String.IsNullOrEmpty(tagsJson))
            {
                obj.Tags = _Serializer.DeserializeJson<Dictionary<string, string>>(tagsJson);
            }

            string metadataJson = DataTableHelper.GetStringValue(row, "metadata");
            if (!String.IsNullOrEmpty(metadataJson))
            {
                obj.Metadata = _Serializer.DeserializeJson<object>(metadataJson);
            }

            return obj;
        }

        /// <summary>
        /// Instantiate list from DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of instances.</returns>
        public static List<TenantMetadata> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<TenantMetadata>();

            List<TenantMetadata> ret = new List<TenantMetadata>();
            foreach (DataRow row in table.Rows)
            {
                TenantMetadata obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
