namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json.Serialization;
    using Conductor.Core.Helpers;
    using Conductor.Core.Serialization;

    /// <summary>
    /// Time-bound reservation for a virtual model runner.
    /// </summary>
    public class VirtualModelRunnerReservation
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewVirtualModelRunnerReservationId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// Virtual model runner identifier.
        /// </summary>
        public string VirtualModelRunnerId
        {
            get => _VirtualModelRunnerId;
            set => _VirtualModelRunnerId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(VirtualModelRunnerId)) : value);
        }

        /// <summary>
        /// Reservation name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Name)) : value);
        }

        /// <summary>
        /// Optional description.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// UTC start timestamp, inclusive.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC end timestamp, exclusive.
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow.AddHours(1);

        /// <summary>
        /// Optional pre-start admission drain window in milliseconds.
        /// </summary>
        public int AdmissionDrainLeadMs
        {
            get => _AdmissionDrainLeadMs;
            set => _AdmissionDrainLeadMs = (value < 0 ? 0 : value);
        }

        /// <summary>
        /// Whether this reservation is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// User who created the reservation.
        /// </summary>
        public string CreatedByUserId { get; set; } = null;

        /// <summary>
        /// Credential used to create the reservation.
        /// </summary>
        public string CreatedByCredentialId { get; set; } = null;

        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Reservation subjects.
        /// </summary>
        public List<VirtualModelRunnerReservationSubject> Subjects
        {
            get => _Subjects;
            set => _Subjects = (value != null ? value : new List<VirtualModelRunnerReservationSubject>());
        }

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
        private string _VirtualModelRunnerId = null;
        private string _Name = "VMR Reservation";
        private int _AdmissionDrainLeadMs = 0;
        private List<VirtualModelRunnerReservationSubject> _Subjects = new List<VirtualModelRunnerReservationSubject>();
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate the reservation.
        /// </summary>
        public VirtualModelRunnerReservation()
        {
        }

        /// <summary>
        /// Instantiate from a data row.
        /// </summary>
        /// <param name="row">Data row.</param>
        /// <returns>Reservation.</returns>
        public static VirtualModelRunnerReservation FromDataRow(DataRow row)
        {
            if (row == null) return null;

            VirtualModelRunnerReservation obj = new VirtualModelRunnerReservation
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                VirtualModelRunnerId = DataTableHelper.GetStringValue(row, "vmrid"),
                Name = DataTableHelper.GetStringValue(row, "name") ?? "VMR Reservation",
                Description = DataTableHelper.GetStringValue(row, "description"),
                StartUtc = DataTableHelper.GetDateTimeValue(row, "startutc"),
                EndUtc = DataTableHelper.GetDateTimeValue(row, "endutc"),
                AdmissionDrainLeadMs = DataTableHelper.GetIntValue(row, "admissiondrainleadms"),
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                CreatedByUserId = DataTableHelper.GetStringValue(row, "createdbyuserid"),
                CreatedByCredentialId = DataTableHelper.GetStringValue(row, "createdbycredentialid"),
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
        /// Instantiate a list from a data table.
        /// </summary>
        /// <param name="table">Data table.</param>
        /// <returns>Reservations.</returns>
        public static List<VirtualModelRunnerReservation> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<VirtualModelRunnerReservation>();

            List<VirtualModelRunnerReservation> ret = new List<VirtualModelRunnerReservation>();
            foreach (DataRow row in table.Rows)
            {
                VirtualModelRunnerReservation obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
