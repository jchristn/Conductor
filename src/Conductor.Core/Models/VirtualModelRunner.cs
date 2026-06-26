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
    /// Virtual model runner configuration.
    /// </summary>
    public class VirtualModelRunner
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewVirtualModelRunnerId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// Virtual model runner name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Name)) : value);
        }

        /// <summary>
        /// Hostname for routing (optional, for host-based routing).
        /// </summary>
        public string Hostname { get; set; } = null;

        /// <summary>
        /// Base path for this virtual model runner (e.g., /v1.0/api/vmr_xxx/).
        /// </summary>
        public string BasePath
        {
            get => _BasePath;
            set => _BasePath = (String.IsNullOrEmpty(value) ? "/v1.0/api/" + Id + "/" : value);
        }

        /// <summary>
        /// API type exposed by this virtual model runner.
        /// </summary>
        public ApiTypeEnum ApiType { get; set; } = ApiTypeEnum.Ollama;

        /// <summary>
        /// Load balancing mode.
        /// </summary>
        public LoadBalancingModeEnum LoadBalancingMode { get; set; } = LoadBalancingModeEnum.RoundRobin;

        /// <summary>
        /// Optional load-balancing policy ID.
        /// </summary>
        public string LoadBalancingPolicyId { get; set; } = null;

        /// <summary>
        /// Optional model access policy ID.
        /// </summary>
        public string ModelAccessPolicyId { get; set; } = null;

        /// <summary>
        /// List of model runner endpoint identifiers.
        /// </summary>
        public List<string> ModelRunnerEndpointIds
        {
            get => _ModelRunnerEndpointIds;
            set
            {
                _ModelRunnerEndpointIds = (value != null ? value : new List<string>());
                _ModelRunnerEndpointIdsJson = _Serializer.SerializeJson(_ModelRunnerEndpointIds, false);
            }
        }

        /// <summary>
        /// JSON-serialized model runner endpoint IDs for database storage.
        /// </summary>
        [JsonIgnore]
        public string ModelRunnerEndpointIdsJson
        {
            get => _ModelRunnerEndpointIdsJson;
            set
            {
                _ModelRunnerEndpointIdsJson = (String.IsNullOrEmpty(value) ? "[]" : value);
                _ModelRunnerEndpointIds = _Serializer.DeserializeJson<List<string>>(_ModelRunnerEndpointIdsJson);
            }
        }

        /// <summary>
        /// Adaptive load-balancing settings.
        /// </summary>
        public AdaptiveLoadBalancingSettings AdaptiveLoadBalancing
        {
            get => _AdaptiveLoadBalancing;
            set
            {
                _AdaptiveLoadBalancing = value ?? new AdaptiveLoadBalancingSettings();
                _AdaptiveLoadBalancingJson = _Serializer.SerializeJson(_AdaptiveLoadBalancing, false);
            }
        }

        /// <summary>
        /// JSON-serialized adaptive load-balancing settings for database storage.
        /// </summary>
        [JsonIgnore]
        public string AdaptiveLoadBalancingJson
        {
            get => _Serializer.SerializeJson(_AdaptiveLoadBalancing ?? new AdaptiveLoadBalancingSettings(), false);
            set
            {
                _AdaptiveLoadBalancingJson = String.IsNullOrEmpty(value) ? "{}" : value;
                _AdaptiveLoadBalancing = _Serializer.DeserializeJson<AdaptiveLoadBalancingSettings>(_AdaptiveLoadBalancingJson) ?? new AdaptiveLoadBalancingSettings();
            }
        }

        /// <summary>
        /// Priority groups and traffic-split configuration for endpoint references.
        /// </summary>
        public List<EndpointGroup> EndpointGroups
        {
            get => _EndpointGroups;
            set
            {
                _EndpointGroups = value ?? new List<EndpointGroup>();
                _EndpointGroupsJson = _Serializer.SerializeJson(_EndpointGroups, false);
            }
        }

        /// <summary>
        /// JSON-serialized endpoint groups for database storage.
        /// </summary>
        [JsonIgnore]
        public string EndpointGroupsJson
        {
            get => _Serializer.SerializeJson(_EndpointGroups ?? new List<EndpointGroup>(), false);
            set
            {
                _EndpointGroupsJson = String.IsNullOrEmpty(value) ? "[]" : value;
                _EndpointGroups = _Serializer.DeserializeJson<List<EndpointGroup>>(_EndpointGroupsJson) ?? new List<EndpointGroup>();
            }
        }

        /// <summary>
        /// Reusable endpoint group identifiers attached to this route.
        /// </summary>
        public List<string> EndpointGroupIds
        {
            get => _EndpointGroupIds;
            set
            {
                _EndpointGroupIds = value ?? new List<string>();
                _EndpointGroupIdsJson = _Serializer.SerializeJson(_EndpointGroupIds, false);
            }
        }

        /// <summary>
        /// JSON-serialized reusable endpoint group IDs for database storage.
        /// </summary>
        [JsonIgnore]
        public string EndpointGroupIdsJson
        {
            get => _EndpointGroupIdsJson;
            set
            {
                _EndpointGroupIdsJson = String.IsNullOrEmpty(value) ? "[]" : value;
                _EndpointGroupIds = _Serializer.DeserializeJson<List<string>>(_EndpointGroupIdsJson) ?? new List<string>();
            }
        }

        /// <summary>
        /// List of model configuration identifiers.
        /// </summary>
        public List<string> ModelConfigurationIds
        {
            get => _ModelConfigurationIds;
            set
            {
                _ModelConfigurationIds = (value != null ? value : new List<string>());
                _ModelConfigurationIdsJson = _Serializer.SerializeJson(_ModelConfigurationIds, false);
            }
        }

        /// <summary>
        /// JSON-serialized model configuration IDs for database storage.
        /// </summary>
        [JsonIgnore]
        public string ModelConfigurationIdsJson
        {
            get => _ModelConfigurationIdsJson;
            set
            {
                _ModelConfigurationIdsJson = (String.IsNullOrEmpty(value) ? "[]" : value);
                _ModelConfigurationIds = _Serializer.DeserializeJson<List<string>>(_ModelConfigurationIdsJson);
            }
        }

        /// <summary>
        /// List of model definition identifiers.
        /// </summary>
        public List<string> ModelDefinitionIds
        {
            get => _ModelDefinitionIds;
            set
            {
                _ModelDefinitionIds = (value != null ? value : new List<string>());
                _ModelDefinitionIdsJson = _Serializer.SerializeJson(_ModelDefinitionIds, false);
            }
        }

        /// <summary>
        /// JSON-serialized model definition IDs for database storage.
        /// </summary>
        [JsonIgnore]
        public string ModelDefinitionIdsJson
        {
            get => _ModelDefinitionIdsJson;
            set
            {
                _ModelDefinitionIdsJson = (String.IsNullOrEmpty(value) ? "[]" : value);
                _ModelDefinitionIds = _Serializer.DeserializeJson<List<string>>(_ModelDefinitionIdsJson);
            }
        }

        /// <summary>
        /// Explicit model-to-configuration mappings.
        /// </summary>
        public Dictionary<string, string> ModelConfigurationMappings
        {
            get => _ModelConfigurationMappings;
            set
            {
                _ModelConfigurationMappings = (value != null ? value : new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase));
                _ModelConfigurationMappingsJson = _Serializer.SerializeJson(_ModelConfigurationMappings, false);
            }
        }

        /// <summary>
        /// JSON-serialized model-to-configuration mappings for database storage.
        /// </summary>
        [JsonIgnore]
        public string ModelConfigurationMappingsJson
        {
            get => _ModelConfigurationMappingsJson;
            set
            {
                _ModelConfigurationMappingsJson = (String.IsNullOrEmpty(value) ? "{}" : value);
                _ModelConfigurationMappings = _Serializer.DeserializeJson<Dictionary<string, string>>(_ModelConfigurationMappingsJson)
                    ?? new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// Request timeout in milliseconds.
        /// </summary>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set => _TimeoutMs = (value < 1000 ? 60000 : value);
        }

        /// <summary>
        /// Allow embeddings requests.
        /// </summary>
        public bool AllowEmbeddings { get; set; } = true;

        /// <summary>
        /// Allow completions requests.
        /// </summary>
        public bool AllowCompletions { get; set; } = true;

        /// <summary>
        /// Allow model management requests (pull, delete, list).
        /// </summary>
        public bool AllowModelManagement { get; set; } = false;

        /// <summary>
        /// When enabled, only accepts requests for models defined by attached ModelDefinitions.
        /// If no ModelDefinitions are attached, all requests are rejected.
        /// </summary>
        public bool StrictMode { get; set; } = false;

        /// <summary>
        /// Session affinity mode controlling how client identity is derived for sticky session routing.
        /// Default is <see cref="SessionAffinityModeEnum.None"/> (no session affinity).
        /// </summary>
        public SessionAffinityModeEnum SessionAffinityMode { get; set; } = SessionAffinityModeEnum.None;

        /// <summary>
        /// When <see cref="SessionAffinityMode"/> is <see cref="SessionAffinityModeEnum.Header"/>,
        /// specifies the request header name to use as the client identity key (e.g., X-Session-Id).
        /// Ignored for other modes. May be null.
        /// </summary>
        public string SessionAffinityHeader { get; set; } = null;

        /// <summary>
        /// Duration in milliseconds that a session pin remains active since last use.
        /// Refreshed on each request. Minimum: 60000 (1 minute). Maximum: 86400000 (24 hours). Default: 600000 (10 minutes).
        /// Values below minimum clamp to 600000. Values above maximum clamp to 86400000.
        /// </summary>
        public int SessionTimeoutMs
        {
            get => _SessionTimeoutMs;
            set => _SessionTimeoutMs = (value < 60000 ? 600000 : (value > 86400000 ? 86400000 : value));
        }

        /// <summary>
        /// Maximum number of concurrent session entries per VMR before oldest entries are evicted.
        /// Minimum: 100. Maximum: 1000000. Default: 10000.
        /// Values below minimum clamp to 10000. Values above maximum clamp to 1000000.
        /// </summary>
        public int SessionMaxEntries
        {
            get => _SessionMaxEntries;
            set => _SessionMaxEntries = (value < 100 ? 10000 : (value > 1000000 ? 1000000 : value));
        }

        /// <summary>
        /// Enable request history for this virtual model runner.
        /// When enabled (and global request history is enabled), requests and responses are captured.
        /// Default is true so Request History and Analytics populate for newly-created VMRs unless explicitly disabled.
        /// </summary>
        public bool RequestHistoryEnabled { get; set; } = true;

        /// <summary>
        /// Boolean indicating if the virtual model runner is active.
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

        /// <summary>
        /// Internal index for round-robin load balancing.
        /// </summary>
        [JsonIgnore]
        internal int LastEndpointIndex = 0;

        /// <summary>
        /// Internal lock for thread safety.
        /// </summary>
        [JsonIgnore]
        internal readonly object Lock = new object();

        private string _TenantId = null;
        private string _Name = "Default Virtual Model Runner";
        private string _BasePath = null;
        private List<string> _ModelRunnerEndpointIds = new List<string>();
        private string _ModelRunnerEndpointIdsJson = "[]";
        private AdaptiveLoadBalancingSettings _AdaptiveLoadBalancing = new AdaptiveLoadBalancingSettings();
        private string _AdaptiveLoadBalancingJson = "{}";
        private List<EndpointGroup> _EndpointGroups = new List<EndpointGroup>();
        private string _EndpointGroupsJson = "[]";
        private List<string> _EndpointGroupIds = new List<string>();
        private string _EndpointGroupIdsJson = "[]";
        private List<string> _ModelConfigurationIds = new List<string>();
        private string _ModelConfigurationIdsJson = "[]";
        private List<string> _ModelDefinitionIds = new List<string>();
        private string _ModelDefinitionIdsJson = "[]";
        private Dictionary<string, string> _ModelConfigurationMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        private string _ModelConfigurationMappingsJson = "{}";
        private int _TimeoutMs = 60000;
        private int _SessionTimeoutMs = 600000;
        private int _SessionMaxEntries = 10000;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate the virtual model runner.
        /// </summary>
        public VirtualModelRunner()
        {
            _BasePath = "/v1.0/api/" + Id + "/";
        }

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance.</returns>
        public static VirtualModelRunner FromDataRow(DataRow row)
        {
            if (row == null) return null;

            VirtualModelRunner obj = new VirtualModelRunner
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                Name = DataTableHelper.GetStringValue(row, "name") ?? "Default Virtual Model Runner",
                Hostname = DataTableHelper.GetStringValue(row, "hostname"),
                BasePath = DataTableHelper.GetStringValue(row, "basepath"),
                ApiType = DataTableHelper.GetEnumValue<ApiTypeEnum>(row, "apitype", ApiTypeEnum.Ollama),
                LoadBalancingMode = DataTableHelper.GetEnumValue<LoadBalancingModeEnum>(row, "loadbalancingmode", LoadBalancingModeEnum.RoundRobin),
                LoadBalancingPolicyId = DataTableHelper.GetStringValue(row, "loadbalancingpolicyid"),
                ModelAccessPolicyId = DataTableHelper.GetStringValue(row, "modelaccesspolicyid"),
                TimeoutMs = DataTableHelper.GetIntValue(row, "timeoutms"),
                AllowEmbeddings = DataTableHelper.GetBooleanValue(row, "allowembeddings"),
                AllowCompletions = DataTableHelper.GetBooleanValue(row, "allowcompletions"),
                AllowModelManagement = DataTableHelper.GetBooleanValue(row, "allowmodelmanagement"),
                StrictMode = DataTableHelper.GetBooleanValue(row, "strictmode"),
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc")
            };

            if (obj.TimeoutMs == 0) obj.TimeoutMs = 60000;
            if (String.IsNullOrEmpty(obj.BasePath)) obj.BasePath = "/v1.0/api/" + obj.Id + "/";

            obj.SessionAffinityMode = DataTableHelper.GetEnumValue<SessionAffinityModeEnum>(row, "sessionaffinitymode", SessionAffinityModeEnum.None);
            obj.SessionAffinityHeader = DataTableHelper.GetStringValue(row, "sessionaffinityheader");
            obj.SessionTimeoutMs = DataTableHelper.GetIntValue(row, "sessiontimeoutms");
            if (obj.SessionTimeoutMs == 0) obj.SessionTimeoutMs = 600000;
            obj.SessionMaxEntries = DataTableHelper.GetIntValue(row, "sessionmaxentries");
            if (obj.SessionMaxEntries == 0) obj.SessionMaxEntries = 10000;
            if (row.Table.Columns.Contains("requesthistoryenabled"))
            {
                obj.RequestHistoryEnabled = DataTableHelper.GetBooleanValue(row, "requesthistoryenabled");
            }

            string endpointIdsJson = DataTableHelper.GetStringValue(row, "modelrunnerendpointids");
            if (!String.IsNullOrEmpty(endpointIdsJson))
            {
                obj.ModelRunnerEndpointIdsJson = endpointIdsJson;
            }

            if (row.Table.Columns.Contains("adaptiveloadbalancing"))
            {
                string adaptiveLoadBalancingJson = DataTableHelper.GetStringValue(row, "adaptiveloadbalancing");
                if (!String.IsNullOrEmpty(adaptiveLoadBalancingJson))
                {
                    obj.AdaptiveLoadBalancingJson = adaptiveLoadBalancingJson;
                }
            }

            if (row.Table.Columns.Contains("endpointgroups"))
            {
                string endpointGroupsJson = DataTableHelper.GetStringValue(row, "endpointgroups");
                if (!String.IsNullOrEmpty(endpointGroupsJson))
                {
                    obj.EndpointGroupsJson = endpointGroupsJson;
                }
            }

            if (row.Table.Columns.Contains("endpointgroupids"))
            {
                string endpointGroupIdsJson = DataTableHelper.GetStringValue(row, "endpointgroupids");
                if (!String.IsNullOrEmpty(endpointGroupIdsJson))
                {
                    obj.EndpointGroupIdsJson = endpointGroupIdsJson;
                }
            }

            string configIdsJson = DataTableHelper.GetStringValue(row, "modelconfigurationids");
            if (!String.IsNullOrEmpty(configIdsJson))
            {
                obj.ModelConfigurationIdsJson = configIdsJson;
            }

            string configMappingsJson = DataTableHelper.GetStringValue(row, "modelconfigurationmappings");
            if (!String.IsNullOrEmpty(configMappingsJson))
            {
                obj.ModelConfigurationMappingsJson = configMappingsJson;
            }

            string defIdsJson = DataTableHelper.GetStringValue(row, "modeldefinitionids");
            if (!String.IsNullOrEmpty(defIdsJson))
            {
                obj.ModelDefinitionIdsJson = defIdsJson;
            }

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
        public static List<VirtualModelRunner> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<VirtualModelRunner>();

            List<VirtualModelRunner> ret = new List<VirtualModelRunner>();
            foreach (DataRow row in table.Rows)
            {
                VirtualModelRunner obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
