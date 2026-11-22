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
            set
            {
                _Hostname = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Hostname)) : value);
                MarkSet(nameof(Hostname));
            }
        }

        /// <summary>
        /// Port number.
        /// </summary>
        public int Port
        {
            get => _Port;
            set
            {
                _Port = (value < 1 || value > 65535 ? 11434 : value);
                MarkSet(nameof(Port));
            }
        }

        /// <summary>
        /// Upstream API key. Null when unset. Management API responses mask this value as four asterisks followed by the
        /// last four characters; use the reveal route to read the full value.
        /// </summary>
        public string ApiKey { get; set; } = null;

        /// <summary>
        /// Endpoint API type. Determines the wire API, upstream authentication headers and type defaults.
        /// Default: Ollama.
        /// </summary>
        public ApiTypeEnum ApiType { get; set; } = ApiTypeEnum.Ollama;

        /// <summary>
        /// Use SSL/TLS. Default: false (type defaults may set it on create when omitted).
        /// </summary>
        public bool UseSsl
        {
            get => _UseSsl;
            set
            {
                _UseSsl = value;
                MarkSet(nameof(UseSsl));
            }
        }

        /// <summary>
        /// The anthropic-version header value sent upstream when the client does not send one. Anthropic endpoints only.
        /// Must use the format yyyy-MM-dd. Null (the default) means the server setting Anthropic.DefaultVersion.
        /// Empty or whitespace values are stored as null; values in other formats are rejected by validation.
        /// </summary>
        public string AnthropicVersion
        {
            get => _AnthropicVersion;
            set => _AnthropicVersion = (String.IsNullOrWhiteSpace(value) ? null : value.Trim());
        }

        /// <summary>
        /// Time to first byte, in milliseconds: how long Conductor waits for this endpoint to return response headers
        /// for a proxied request. This covers model load, prompt evaluation and, for non-streaming requests, the whole
        /// generation. Streamed responses are not limited after the first chunk arrives.
        /// The effective limit for a request is the smaller of this value and the virtual model runner's TimeoutMs.
        /// Minimum: 1000. Values below the minimum reset to the default. Default: 300000 (5 minutes).
        /// </summary>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set => _TimeoutMs = (value < 1000 ? Constants.DefaultRequestTimeoutMs : value);
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
            set
            {
                _HealthCheckUrl = (String.IsNullOrEmpty(value) ? "/" : value);
                MarkSet(nameof(HealthCheckUrl));
            }
        }

        /// <summary>
        /// Health check HTTP method. POST sends HealthCheckRequestBody with Content-Type application/json.
        /// Default: GET.
        /// </summary>
        public HealthCheckMethodEnum HealthCheckMethod
        {
            get => _HealthCheckMethod;
            set
            {
                _HealthCheckMethod = value;
                MarkSet(nameof(HealthCheckMethod));
            }
        }

        /// <summary>
        /// Boolean indicating whether Conductor sends health check probes to this endpoint. When false, no probe is sent
        /// and the endpoint is treated as healthy. Default: true (the TypeSafe type default is false).
        /// </summary>
        public bool HealthCheckEnabled
        {
            get => _HealthCheckEnabled;
            set
            {
                _HealthCheckEnabled = value;
                MarkSet(nameof(HealthCheckEnabled));
            }
        }

        /// <summary>
        /// Request body sent with health checks when HealthCheckMethod is POST. Null (the default) when unset; empty or
        /// whitespace values are stored as null. Validation requires a body when HealthCheckMethod is POST.
        /// </summary>
        public string HealthCheckRequestBody
        {
            get => _HealthCheckRequestBody;
            set
            {
                _HealthCheckRequestBody = (String.IsNullOrWhiteSpace(value) ? null : value);
                MarkSet(nameof(HealthCheckRequestBody));
            }
        }

        /// <summary>
        /// Health check interval in milliseconds.
        /// </summary>
        public int HealthCheckIntervalMs
        {
            get => _HealthCheckIntervalMs;
            set
            {
                _HealthCheckIntervalMs = (value < 1 ? 5000 : value);
                MarkSet(nameof(HealthCheckIntervalMs));
            }
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
        /// Default: false (type defaults may set it on create when omitted).
        /// </summary>
        public bool HealthCheckUseAuth
        {
            get => _HealthCheckUseAuth;
            set
            {
                _HealthCheckUseAuth = value;
                MarkSet(nameof(HealthCheckUseAuth));
            }
        }

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
        /// Operator-managed service state.
        /// </summary>
        public EndpointServiceStateEnum ServiceState { get; set; } = EndpointServiceStateEnum.Normal;

        /// <summary>
        /// RigMonitor integration settings for this endpoint.
        /// </summary>
        public RigMonitorConfiguration RigMonitor
        {
            get => _RigMonitor;
            set => _RigMonitor = (value ?? new RigMonitorConfiguration());
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

        /// <summary>
        /// JSON-serialized RigMonitor settings for database storage.
        /// </summary>
        [JsonIgnore]
        public string RigMonitorJson
        {
            get => _Serializer.SerializeJson(_RigMonitor, false);
            set => _RigMonitor = (String.IsNullOrEmpty(value) ? new RigMonitorConfiguration() : _Serializer.DeserializeJson<RigMonitorConfiguration>(value));
        }

        private string _TenantId = null;
        private string _Name = "Default Endpoint";
        private string _Hostname = "localhost";
        private int _Port = 11434;
        private int _TimeoutMs = Constants.DefaultRequestTimeoutMs;
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
        private RigMonitorConfiguration _RigMonitor = new RigMonitorConfiguration();
        private bool _UseSsl = false;
        private string _AnthropicVersion = null;
        private bool _HealthCheckEnabled = true;
        private string _HealthCheckRequestBody = null;
        private bool _HealthCheckUseAuth = false;
        private readonly HashSet<string> _ExplicitlySetProperties = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Instantiate the model runner endpoint.
        /// </summary>
        public ModelRunnerEndpoint()
        {
        }

        /// <summary>
        /// Determine whether a property that participates in type defaults was assigned since this instance was created
        /// or since ClearExplicitlySetProperties was called. Tracked properties: Hostname, Port, UseSsl, HealthCheckUrl,
        /// HealthCheckMethod, HealthCheckEnabled, HealthCheckRequestBody, HealthCheckIntervalMs and HealthCheckUseAuth.
        /// JSON deserialization assigns only the properties present in the payload, so this reports what a client sent.
        /// Not thread safe.
        /// </summary>
        /// <param name="propertyName">Property name, for example nameof(ModelRunnerEndpoint.Port). Null returns false.</param>
        /// <returns>True when the property was assigned.</returns>
        public bool WasExplicitlySet(string propertyName)
        {
            if (String.IsNullOrEmpty(propertyName)) return false;
            return _ExplicitlySetProperties.Contains(propertyName);
        }

        /// <summary>
        /// Forget which tracked properties were assigned. Not thread safe.
        /// </summary>
        public void ClearExplicitlySetProperties()
        {
            _ExplicitlySetProperties.Clear();
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
                AnthropicVersion = DataTableHelper.GetStringValue(row, "anthropicversion"),
                HealthCheckRequestBody = DataTableHelper.GetStringValue(row, "healthcheckrequestbody"),
                MaxParallelRequests = DataTableHelper.GetIntValue(row, "maxparallelrequests"),
                Weight = DataTableHelper.GetIntValue(row, "weight"),
                ServiceState = DataTableHelper.GetEnumValue<EndpointServiceStateEnum>(row, "servicestate", EndpointServiceStateEnum.Normal)
            };

            obj.HealthCheckEnabled = !row.Table.Columns.Contains("healthcheckenabled")
                || row["healthcheckenabled"] == DBNull.Value
                || DataTableHelper.GetBooleanValue(row, "healthcheckenabled");

            if (obj.Port == 0) obj.Port = 11434;
            if (obj.TimeoutMs == 0) obj.TimeoutMs = Constants.DefaultRequestTimeoutMs;
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

            string rigMonitorJson = DataTableHelper.GetStringValue(row, "rigmonitor");
            if (!String.IsNullOrEmpty(rigMonitorJson))
            {
                obj.RigMonitorJson = rigMonitorJson;
            }

            obj.ClearExplicitlySetProperties();
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

        private void MarkSet(string propertyName)
        {
            _ExplicitlySetProperties?.Add(propertyName);
        }
    }
}
