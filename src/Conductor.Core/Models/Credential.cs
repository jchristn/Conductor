namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json.Serialization;
    using Conductor.Core.Helpers;
    using Conductor.Core.Serialization;

    /// <summary>
    /// Credential for API access.
    /// </summary>
    public class Credential
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewCredentialId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// User identifier.
        /// </summary>
        public string UserId
        {
            get => _UserId;
            set => _UserId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(UserId)) : value);
        }

        /// <summary>
        /// Credential name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Bearer token for authentication.
        /// </summary>
        public string BearerToken { get; set; } = IdGenerator.NewBearerToken();

        /// <summary>
        /// Boolean indicating if the credential is active.
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

        private string _TenantId = null;
        private string _UserId = null;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate the credential.
        /// </summary>
        public Credential()
        {
        }

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance.</returns>
        public static Credential FromDataRow(DataRow row)
        {
            if (row == null) return null;

            Credential obj = new Credential
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                UserId = DataTableHelper.GetStringValue(row, "userid"),
                Name = DataTableHelper.GetStringValue(row, "name"),
                BearerToken = DataTableHelper.GetStringValue(row, "bearertoken"),
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
        public static List<Credential> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<Credential>();

            List<Credential> ret = new List<Credential>();
            foreach (DataRow row in table.Rows)
            {
                Credential obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
