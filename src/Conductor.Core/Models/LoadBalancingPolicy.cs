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
    /// First-class load-balancing policy resource.
    /// </summary>
    public class LoadBalancingPolicy
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewLoadBalancingPolicyId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// Display name.
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
        /// Maximum allowed age for RigMonitor telemetry when evaluating this policy.
        /// </summary>
        public int MaxTelemetryAgeMs
        {
            get => _MaxTelemetryAgeMs;
            set => _MaxTelemetryAgeMs = (value < 1000 ? 30000 : value);
        }

        /// <summary>
        /// Policy filters.
        /// </summary>
        public List<LoadBalancingPolicyFilter> Filters
        {
            get => _Filters;
            set => _Filters = (value != null ? value : new List<LoadBalancingPolicyFilter>());
        }

        /// <summary>
        /// Policy ranking rules.
        /// </summary>
        public List<LoadBalancingPolicyRankingRule> Ranking
        {
            get => _Ranking;
            set => _Ranking = (value != null ? value : new List<LoadBalancingPolicyRankingRule>());
        }

        /// <summary>
        /// Fallback behavior when the policy cannot select an endpoint.
        /// </summary>
        public LoadBalancingPolicyFallbackModeEnum FallbackMode { get; set; } = LoadBalancingPolicyFallbackModeEnum.UseLegacyLoadBalancingMode;

        /// <summary>
        /// Tie-breaker used when multiple endpoints have equal scores.
        /// </summary>
        public LoadBalancingPolicyTieBreakerEnum TieBreaker { get; set; } = LoadBalancingPolicyTieBreakerEnum.RoundRobin;

        /// <summary>
        /// Whether this policy is active.
        /// </summary>
        public bool Active { get; set; } = true;

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
            set => _Labels = (value != null ? value : new List<string>());
        }

        /// <summary>
        /// Tags for metadata.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get => _Tags;
            set => _Tags = (value != null ? value : new Dictionary<string, string>());
        }

        /// <summary>
        /// Free-form metadata.
        /// </summary>
        public object Metadata { get; set; } = null;

        [JsonIgnore]
        public string FiltersJson
        {
            get => _Serializer.SerializeJson(_Filters, false);
            set => _Filters = (String.IsNullOrEmpty(value) ? new List<LoadBalancingPolicyFilter>() : _Serializer.DeserializeJson<List<LoadBalancingPolicyFilter>>(value));
        }

        [JsonIgnore]
        public string RankingJson
        {
            get => _Serializer.SerializeJson(_Ranking, false);
            set => _Ranking = (String.IsNullOrEmpty(value) ? new List<LoadBalancingPolicyRankingRule>() : _Serializer.DeserializeJson<List<LoadBalancingPolicyRankingRule>>(value));
        }

        [JsonIgnore]
        public string LabelsJson
        {
            get => _Serializer.SerializeJson(_Labels, false);
            set => _Labels = (String.IsNullOrEmpty(value) ? new List<string>() : _Serializer.DeserializeJson<List<string>>(value));
        }

        [JsonIgnore]
        public string TagsJson
        {
            get => _Serializer.SerializeJson(_Tags, false);
            set => _Tags = (String.IsNullOrEmpty(value) ? new Dictionary<string, string>() : _Serializer.DeserializeJson<Dictionary<string, string>>(value));
        }

        [JsonIgnore]
        public string MetadataJson
        {
            get => (Metadata != null ? _Serializer.SerializeJson(Metadata, false) : null);
            set => Metadata = (String.IsNullOrEmpty(value) ? null : _Serializer.DeserializeJson<object>(value));
        }

        private string _TenantId = null;
        private string _Name = "Default Load Balancing Policy";
        private int _MaxTelemetryAgeMs = 30000;
        private List<LoadBalancingPolicyFilter> _Filters = new List<LoadBalancingPolicyFilter>();
        private List<LoadBalancingPolicyRankingRule> _Ranking = new List<LoadBalancingPolicyRankingRule>();
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        public static LoadBalancingPolicy FromDataRow(DataRow row)
        {
            if (row == null) return null;

            LoadBalancingPolicy obj = new LoadBalancingPolicy
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                Name = DataTableHelper.GetStringValue(row, "name") ?? "Default Load Balancing Policy",
                Description = DataTableHelper.GetStringValue(row, "description"),
                MaxTelemetryAgeMs = DataTableHelper.GetIntValue(row, "maxtelemetryagems"),
                FallbackMode = DataTableHelper.GetEnumValue<LoadBalancingPolicyFallbackModeEnum>(row, "fallbackmode", LoadBalancingPolicyFallbackModeEnum.UseLegacyLoadBalancingMode),
                TieBreaker = DataTableHelper.GetEnumValue<LoadBalancingPolicyTieBreakerEnum>(row, "tiebreaker", LoadBalancingPolicyTieBreakerEnum.RoundRobin),
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc")
            };

            if (obj.MaxTelemetryAgeMs == 0) obj.MaxTelemetryAgeMs = 30000;

            string filtersJson = DataTableHelper.GetStringValue(row, "filters");
            if (!String.IsNullOrEmpty(filtersJson))
            {
                obj.FiltersJson = filtersJson;
            }

            string rankingJson = DataTableHelper.GetStringValue(row, "ranking");
            if (!String.IsNullOrEmpty(rankingJson))
            {
                obj.RankingJson = rankingJson;
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
        public static List<LoadBalancingPolicy> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<LoadBalancingPolicy>();

            List<LoadBalancingPolicy> ret = new List<LoadBalancingPolicy>();
            foreach (DataRow row in table.Rows)
            {
                LoadBalancingPolicy obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
