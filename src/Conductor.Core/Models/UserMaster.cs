namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json.Serialization;
    using Conductor.Core.Helpers;
    using Conductor.Core.Serialization;

    /// <summary>
    /// User master record.
    /// </summary>
    public class UserMaster
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewUserId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// First name.
        /// </summary>
        public string FirstName
        {
            get => _FirstName;
            set => _FirstName = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(FirstName)) : value);
        }

        /// <summary>
        /// Last name.
        /// </summary>
        public string LastName
        {
            get => _LastName;
            set => _LastName = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(LastName)) : value);
        }

        /// <summary>
        /// Email address.
        /// </summary>
        public string Email
        {
            get => _Email;
            set => _Email = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Email)) : value);
        }

        /// <summary>
        /// Password (hashed).
        /// </summary>
        public string Password
        {
            get => _Password;
            set => _Password = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Password)) : value);
        }

        /// <summary>
        /// Boolean indicating if the user is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Boolean indicating if the user has global admin rights (cross-tenant access).
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Boolean indicating if the user has tenant admin rights (can manage users/credentials in their tenant).
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

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
        private string _FirstName = "First";
        private string _LastName = "Last";
        private string _Email = "user@example.com";
        private string _Password = "password";
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate the user master.
        /// </summary>
        public UserMaster()
        {
        }

        /// <summary>
        /// Redact sensitive information.
        /// </summary>
        /// <param name="user">User to redact.</param>
        /// <returns>Redacted user.</returns>
        public static UserMaster Redact(UserMaster user)
        {
            if (user == null) return null;
            UserMaster redacted = _Serializer.CopyObject<UserMaster>(user);

            if (!String.IsNullOrEmpty(redacted.Password))
            {
                int len = redacted.Password.Length;
                if (len < 5)
                {
                    redacted.Password = "****";
                }
                else
                {
                    redacted.Password = new String('*', len - 4) + redacted.Password.Substring(len - 4);
                }
            }

            return redacted;
        }

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance.</returns>
        public static UserMaster FromDataRow(DataRow row)
        {
            if (row == null) return null;

            UserMaster obj = new UserMaster
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                FirstName = DataTableHelper.GetStringValue(row, "firstname") ?? "First",
                LastName = DataTableHelper.GetStringValue(row, "lastname") ?? "Last",
                Email = DataTableHelper.GetStringValue(row, "email") ?? "user@example.com",
                Password = DataTableHelper.GetStringValue(row, "password") ?? "password",
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                IsAdmin = DataTableHelper.GetBooleanValue(row, "isadmin"),
                IsTenantAdmin = DataTableHelper.GetBooleanValue(row, "istenantadmin"),
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
        public static List<UserMaster> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<UserMaster>();

            List<UserMaster> ret = new List<UserMaster>();
            foreach (DataRow row in table.Rows)
            {
                UserMaster obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
