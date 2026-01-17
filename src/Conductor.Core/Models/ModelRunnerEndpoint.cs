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
    /// Model runner endpoint configuration.
    /// </summary>
    public class ModelRunnerEndpoint
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewModelRunnerEndpointId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// Endpoint name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Name)) : value);
        }

        /// <summary>
        /// Hostname or IP address.
        /// </summary>
        public string Hostname
        {
            get => _Hostname;
            set => _Hostname = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Hostname)) : value);
        }

        /// <summary>
        /// Port number.
        /// </summary>
        public int Port
        {
            get => _Port;
            set => _Port = (value < 1 || value > 65535 ? 11434 : value);
        }

        /// <summary>
        /// API key for authentication.
        /// </summary>
        public string ApiKey { get; set; } = null;

        /// <summary>
        /// API type (Ollama or OpenAI).
        /// </summary>
        public ApiTypeEnum ApiType { get; set; } = ApiTypeEnum.Ollama;

        /// <summary>
        /// Use SSL/TLS.
        /// </summary>
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// Request timeout in milliseconds.
        /// </summary>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set => _TimeoutMs = (value < 1000 ? 60000 : value);
        }

        /// <summary>
        /// Boolean indicating if the endpoint is active.
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
        /// Health check URL path (appended to base URL).
        /// </summary>
        public string HealthCheckUrl
        {
            get => _HealthCheckUrl;
            set => _HealthCheckUrl = (String.IsNullOrEmpty(value) ? "/" : value);
        }

        /// <summary>
        /// Health check HTTP method.
        /// </summary>
        public HealthCheckMethodEnum HealthCheckMethod
        {
            get => _HealthCheckMethod;
            set => _HealthCheckMethod = value;
        }

        /// <summary>
        /// Health check interval in milliseconds.
        /// </summary>
        public int HealthCheckIntervalMs
        {
            get => _HealthCheckIntervalMs;
            set => _HealthCheckIntervalMs = (value < 1 ? 5000 : value);
        }

        /// <summary>
        /// Health check timeout in milliseconds.
        /// </summary>
        public int HealthCheckTimeoutMs
        {
            get => _HealthCheckTimeoutMs;
            set => _HealthCheckTimeoutMs = (value < 1 ? 5000 : value);
        }

        /// <summary>
        /// Expected HTTP status code for health check (100-599).
        /// </summary>
        public int HealthCheckExpectedStatusCode
        {
            get => _HealthCheckExpectedStatusCode;
            set => _HealthCheckExpectedStatusCode = (value < 100 || value > 599 ? 200 : value);
        }

        /// <summary>
        /// Number of consecutive failures before marking endpoint unhealthy.
        /// </summary>
        public int UnhealthyThreshold
        {
            get => _UnhealthyThreshold;
            set => _UnhealthyThreshold = (value < 1 ? 2 : value);
        }

        /// <summary>
        /// Number of consecutive successes before marking endpoint healthy.
        /// </summary>
        public int HealthyThreshold
        {
            get => _HealthyThreshold;
            set => _HealthyThreshold = (value < 1 ? 2 : value);
        }

        /// <summary>
        /// Boolean indicating whether to include authentication material (API key) in health check requests.
        /// </summary>
        public bool HealthCheckUseAuth { get; set; } = false;

        /// <summary>
        /// Maximum number of concurrent requests (0 = unlimited).
        /// </summary>
        public int MaxParallelRequests
        {
            get => _MaxParallelRequests;
            set => _MaxParallelRequests = (value < 0 ? 4 : value);
        }

        /// <summary>
        /// Weight for load balancing (higher = more traffic). Valid range: 1-1000.
        /// </summary>
        public int Weight
        {
            get => _Weight;
            set => _Weight = (value < 1 ? 1 : (value > 1000 ? 1000 : value));
        }

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
        private string _Name = "Default Endpoint";
        private string _Hostname = "localhost";
        private int _Port = 11434;
        private int _TimeoutMs = 60000;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();
        private string _HealthCheckUrl = "/";
        private HealthCheckMethodEnum _HealthCheckMethod = HealthCheckMethodEnum.GET;
        private int _HealthCheckIntervalMs = 5000;
        private int _HealthCheckTimeoutMs = 5000;
        private int _HealthCheckExpectedStatusCode = 200;
        private int _UnhealthyThreshold = 2;
        private int _HealthyThreshold = 2;
        private int _MaxParallelRequests = 4;
        private int _Weight = 1;

        /// <summary>
        /// Instantiate the model runner endpoint.
        /// </summary>
        public ModelRunnerEndpoint()
        {
        }

        /// <summary>
        /// Get the base URL for this endpoint.
        /// </summary>
        /// <returns>Base URL.</returns>
        public string GetBaseUrl()
        {
            string protocol = UseSsl ? "https" : "http";
            return protocol + "://" + Hostname + ":" + Port;
        }

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance.</returns>
        public static ModelRunnerEndpoint FromDataRow(DataRow row)
        {
            if (row == null) return null;

            ModelRunnerEndpoint obj = new ModelRunnerEndpoint
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                Name = DataTableHelper.GetStringValue(row, "name") ?? "Default Endpoint",
                Hostname = DataTableHelper.GetStringValue(row, "hostname") ?? "localhost",
                Port = DataTableHelper.GetIntValue(row, "port"),
                ApiKey = DataTableHelper.GetStringValue(row, "apikey"),
                ApiType = DataTableHelper.GetEnumValue<ApiTypeEnum>(row, "apitype", ApiTypeEnum.Ollama),
                UseSsl = DataTableHelper.GetBooleanValue(row, "usessl"),
                TimeoutMs = DataTableHelper.GetIntValue(row, "timeoutms"),
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc"),
                HealthCheckUrl = DataTableHelper.GetStringValue(row, "healthcheckurl") ?? "/",
                HealthCheckMethod = DataTableHelper.GetEnumValue<HealthCheckMethodEnum>(row, "healthcheckmethod", HealthCheckMethodEnum.GET),
                HealthCheckIntervalMs = DataTableHelper.GetIntValue(row, "healthcheckintervalms"),
                HealthCheckTimeoutMs = DataTableHelper.GetIntValue(row, "healthchecktimeoutms"),
                HealthCheckExpectedStatusCode = DataTableHelper.GetIntValue(row, "healthcheckexpectedstatuscode"),
                UnhealthyThreshold = DataTableHelper.GetIntValue(row, "unhealthythreshold"),
                HealthyThreshold = DataTableHelper.GetIntValue(row, "healthythreshold"),
                HealthCheckUseAuth = DataTableHelper.GetBooleanValue(row, "healthcheckuseauth"),
                MaxParallelRequests = DataTableHelper.GetIntValue(row, "maxparallelrequests"),
                Weight = DataTableHelper.GetIntValue(row, "weight")
            };

            if (obj.Port == 0) obj.Port = 11434;
            if (obj.TimeoutMs == 0) obj.TimeoutMs = 60000;
            if (obj.HealthCheckIntervalMs == 0) obj.HealthCheckIntervalMs = 5000;
            if (obj.HealthCheckTimeoutMs == 0) obj.HealthCheckTimeoutMs = 5000;
            if (obj.HealthCheckExpectedStatusCode == 0) obj.HealthCheckExpectedStatusCode = 200;
            if (obj.UnhealthyThreshold == 0) obj.UnhealthyThreshold = 2;
            if (obj.HealthyThreshold == 0) obj.HealthyThreshold = 2;
            // Note: MaxParallelRequests=0 means unlimited, so no default check needed
            if (obj.Weight == 0) obj.Weight = 1;

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
        public static List<ModelRunnerEndpoint> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<ModelRunnerEndpoint>();

            List<ModelRunnerEndpoint> ret = new List<ModelRunnerEndpoint>();
            foreach (DataRow row in table.Rows)
            {
                ModelRunnerEndpoint obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
