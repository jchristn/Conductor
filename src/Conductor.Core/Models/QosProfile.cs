namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json.Serialization;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Serialization;

    /// <summary>
    /// A tenant-scoped QoS profile: how a virtual model runner's traffic is classified, queued, and
    /// admitted. The child collections (<see cref="Rules"/>, <see cref="Nodes"/>, <see cref="Links"/>,
    /// <see cref="IngressRoutes"/>) are assembled from their own tables by the persistence layer.
    /// Nullable properties are noted. This type is not thread-safe.
    /// </summary>
    public class QosProfile
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier. Never null.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewQosProfileId();

        /// <summary>
        /// Tenant identifier. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null or empty.</exception>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// Display name. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null or empty.</exception>
        public string Name
        {
            get => _Name;
            set => _Name = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Name)) : value);
        }

        /// <summary>
        /// Optional description. Nullable.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Whether this is the tenant's non-deletable default FIFO profile.
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Whether this profile is active. Inactive profiles are not compiled.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// The class assigned when no classifier rule matches. Never null in a valid profile.
        /// </summary>
        public string DefaultClass { get; set; } = "default";

        /// <summary>
        /// How admitted requests enter the topology.
        /// </summary>
        public QosIngressModeEnum IngressMode { get; set; } = QosIngressModeEnum.Single;

        /// <summary>
        /// The ingress node used for the single-node mode or as the router default. Nullable until set.
        /// </summary>
        public string IngressDefaultNode { get; set; } = null;

        /// <summary>
        /// The terminal node the scheduler drains. Nullable until set.
        /// </summary>
        public string TailNode { get; set; } = null;

        /// <summary>
        /// Maximum total parked requests across the whole profile; 0 means unbounded. Minimum 0.
        /// </summary>
        public int MaxTotalDepth { get; set; } = 0;

        /// <summary>
        /// Maximum time a request may wait before admission, in milliseconds. Minimum 0 (no deadline).
        /// </summary>
        public int MaxQueueWaitMs { get; set; } = 30000;

        /// <summary>
        /// HTTP status returned on rejection. Default 429.
        /// </summary>
        public int RejectionStatusCode { get; set; } = 429;

        /// <summary>
        /// Whether to include a Retry-After header on rejection.
        /// </summary>
        public bool IncludeRetryAfter { get; set; } = true;

        /// <summary>
        /// The Retry-After value in seconds when included. Minimum 0.
        /// </summary>
        public int RetryAfterSeconds { get; set; } = 5;

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Updated UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Classification rules. Never null.
        /// </summary>
        public List<QosClassifierRule> Rules
        {
            get => _Rules;
            set => _Rules = (value != null ? value : new List<QosClassifierRule>());
        }

        /// <summary>
        /// Queue nodes. Never null.
        /// </summary>
        public List<QosQueueNode> Nodes
        {
            get => _Nodes;
            set => _Nodes = (value != null ? value : new List<QosQueueNode>());
        }

        /// <summary>
        /// Hierarchy links. Never null.
        /// </summary>
        public List<QosQueueLink> Links
        {
            get => _Links;
            set => _Links = (value != null ? value : new List<QosQueueLink>());
        }

        /// <summary>
        /// Class-to-ingress-node routes (Router mode). Never null.
        /// </summary>
        public List<QosIngressRoute> IngressRoutes
        {
            get => _IngressRoutes;
            set => _IngressRoutes = (value != null ? value : new List<QosIngressRoute>());
        }

        /// <summary>
        /// Labels for categorization. Never null.
        /// </summary>
        public List<string> Labels
        {
            get => _Labels;
            set => _Labels = (value != null ? value : new List<string>());
        }

        /// <summary>
        /// Tags for metadata. Never null.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = (value != null ? value : new Dictionary<string, string>());
        }

        /// <summary>
        /// Free-form metadata. Nullable.
        /// </summary>
        public object Metadata { get; set; } = null;

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

        /// <summary>
        /// JSON-serialized metadata used for persistence.
        /// </summary>
        [JsonIgnore]
        public string MetadataJson
        {
            get => (Metadata != null ? _Serializer.SerializeJson(Metadata, false) : null);
            set => Metadata = (String.IsNullOrEmpty(value) ? null : _Serializer.DeserializeJson<object>(value));
        }

        private string _TenantId = null;
        private string _Name = null;
        private List<QosClassifierRule> _Rules = new List<QosClassifierRule>();
        private List<QosQueueNode> _Nodes = new List<QosQueueNode>();
        private List<QosQueueLink> _Links = new List<QosQueueLink>();
        private List<QosIngressRoute> _IngressRoutes = new List<QosIngressRoute>();
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QosProfile()
        {
        }

        /// <summary>
        /// Instantiate the profile row (scalar fields only; child collections are assembled by the
        /// persistence layer). Returns null when the row is null.
        /// </summary>
        /// <param name="row">Data row. Nullable.</param>
        /// <returns>Instance, or null.</returns>
        public static QosProfile FromDataRow(DataRow row)
        {
            if (row == null) return null;

            QosProfile obj = new QosProfile
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                Name = DataTableHelper.GetStringValue(row, "name"),
                Description = DataTableHelper.GetStringValue(row, "description"),
                IsDefault = DataTableHelper.GetBooleanValue(row, "isdefault"),
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                DefaultClass = DataTableHelper.GetStringValue(row, "defaultclass") ?? "default",
                IngressMode = DataTableHelper.GetEnumValue<QosIngressModeEnum>(row, "ingressmode", QosIngressModeEnum.Single),
                IngressDefaultNode = DataTableHelper.GetStringValue(row, "ingressdefaultnode"),
                TailNode = DataTableHelper.GetStringValue(row, "tailnode"),
                MaxTotalDepth = DataTableHelper.GetIntValue(row, "maxtotaldepth"),
                MaxQueueWaitMs = DataTableHelper.GetIntValue(row, "maxqueuewaitms"),
                RejectionStatusCode = DataTableHelper.GetIntValue(row, "rejectionstatuscode"),
                IncludeRetryAfter = DataTableHelper.GetBooleanValue(row, "includeretryafter"),
                RetryAfterSeconds = DataTableHelper.GetIntValue(row, "retryafterseconds"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc")
            };

            if (obj.RejectionStatusCode == 0) obj.RejectionStatusCode = 429;

            string labelsJson = DataTableHelper.GetStringValue(row, "labels");
            if (!String.IsNullOrEmpty(labelsJson)) obj.LabelsJson = labelsJson;

            string tagsJson = DataTableHelper.GetStringValue(row, "tags");
            if (!String.IsNullOrEmpty(tagsJson)) obj.TagsJson = tagsJson;

            string metadataJson = DataTableHelper.GetStringValue(row, "metadata");
            if (!String.IsNullOrEmpty(metadataJson)) obj.MetadataJson = metadataJson;

            return obj;
        }

        /// <summary>
        /// Instantiate a list of profile rows from a DataTable. Returns null when the table is null.
        /// </summary>
        /// <param name="table">Data table. Nullable.</param>
        /// <returns>List of instances, or null.</returns>
        public static List<QosProfile> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<QosProfile>();

            List<QosProfile> ret = new List<QosProfile>();
            foreach (DataRow row in table.Rows)
            {
                QosProfile obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
