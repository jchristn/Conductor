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
    /// Rule belonging to a tenant-scoped model access policy.
    /// </summary>
    public class ModelAccessRule
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewModelAccessRuleId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// Parent model access policy identifier.
        /// </summary>
        public string PolicyId
        {
            get => _PolicyId;
            set => _PolicyId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(PolicyId)) : value);
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
        /// Rule priority. Higher values are evaluated before lower values.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Effect applied when this rule matches.
        /// </summary>
        public ModelAccessRuleEffectEnum Effect { get; set; } = ModelAccessRuleEffectEnum.Allow;

        /// <summary>
        /// Subject type matched by this rule.
        /// </summary>
        public ModelAccessSubjectTypeEnum SubjectType { get; set; } = ModelAccessSubjectTypeEnum.Any;

        /// <summary>
        /// Subject identifier or literal value, depending on subject type.
        /// </summary>
        public string SubjectId { get; set; } = null;

        /// <summary>
        /// Structured subject selector data used by label or pattern matchers.
        /// </summary>
        public Dictionary<string, string> SubjectSelector
        {
            get => _SubjectSelector;
            set => _SubjectSelector = (value != null ? value : new Dictionary<string, string>());
        }

        /// <summary>
        /// Resource type matched by this rule.
        /// </summary>
        public ModelAccessResourceTypeEnum ResourceType { get; set; } = ModelAccessResourceTypeEnum.Any;

        /// <summary>
        /// Resource identifier or literal value, depending on resource type.
        /// </summary>
        public string ResourceId { get; set; } = null;

        /// <summary>
        /// Structured resource selector data used by label or pattern matchers.
        /// </summary>
        public Dictionary<string, string> ResourceSelector
        {
            get => _ResourceSelector;
            set => _ResourceSelector = (value != null ? value : new Dictionary<string, string>());
        }

        /// <summary>
        /// Optional virtual model runner scope for this rule.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Actions matched by this rule.
        /// </summary>
        public List<ModelAccessActionEnum> Actions
        {
            get => _Actions;
            set => _Actions = (value != null ? value : new List<ModelAccessActionEnum>());
        }

        /// <summary>
        /// Whether this rule is active.
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
        /// JSON-serialized subject selector used for persistence.
        /// </summary>
        [JsonIgnore]
        public string SubjectSelectorJson
        {
            get => _Serializer.SerializeJson(_SubjectSelector, false);
            set => _SubjectSelector = (String.IsNullOrEmpty(value) ? new Dictionary<string, string>() : _Serializer.DeserializeJson<Dictionary<string, string>>(value));
        }

        /// <summary>
        /// JSON-serialized resource selector used for persistence.
        /// </summary>
        [JsonIgnore]
        public string ResourceSelectorJson
        {
            get => _Serializer.SerializeJson(_ResourceSelector, false);
            set => _ResourceSelector = (String.IsNullOrEmpty(value) ? new Dictionary<string, string>() : _Serializer.DeserializeJson<Dictionary<string, string>>(value));
        }

        /// <summary>
        /// JSON-serialized actions used for persistence.
        /// </summary>
        [JsonIgnore]
        public string ActionsJson
        {
            get => _Serializer.SerializeJson(_Actions, false);
            set => _Actions = (String.IsNullOrEmpty(value) ? new List<ModelAccessActionEnum>() : _Serializer.DeserializeJson<List<ModelAccessActionEnum>>(value));
        }

        private string _TenantId = null;
        private string _PolicyId = null;
        private string _Name = "Default Model Access Rule";
        private Dictionary<string, string> _SubjectSelector = new Dictionary<string, string>();
        private Dictionary<string, string> _ResourceSelector = new Dictionary<string, string>();
        private List<ModelAccessActionEnum> _Actions = new List<ModelAccessActionEnum>();

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance.</returns>
        public static ModelAccessRule FromDataRow(DataRow row)
        {
            if (row == null) return null;

            ModelAccessRule obj = new ModelAccessRule
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                PolicyId = DataTableHelper.GetStringValue(row, "policyid"),
                Name = DataTableHelper.GetStringValue(row, "name") ?? "Default Model Access Rule",
                Description = DataTableHelper.GetStringValue(row, "description"),
                Priority = DataTableHelper.GetIntValue(row, "priority"),
                Effect = DataTableHelper.GetEnumValue<ModelAccessRuleEffectEnum>(row, "effect", ModelAccessRuleEffectEnum.Allow),
                SubjectType = DataTableHelper.GetEnumValue<ModelAccessSubjectTypeEnum>(row, "subjecttype", ModelAccessSubjectTypeEnum.Any),
                SubjectId = DataTableHelper.GetStringValue(row, "subjectid"),
                ResourceType = DataTableHelper.GetEnumValue<ModelAccessResourceTypeEnum>(row, "resourcetype", ModelAccessResourceTypeEnum.Any),
                ResourceId = DataTableHelper.GetStringValue(row, "resourceid"),
                VirtualModelRunnerId = DataTableHelper.GetStringValue(row, "vmrid"),
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc")
            };

            string subjectSelectorJson = DataTableHelper.GetStringValue(row, "subjectselector");
            if (!String.IsNullOrEmpty(subjectSelectorJson))
            {
                obj.SubjectSelectorJson = subjectSelectorJson;
            }

            string resourceSelectorJson = DataTableHelper.GetStringValue(row, "resourceselector");
            if (!String.IsNullOrEmpty(resourceSelectorJson))
            {
                obj.ResourceSelectorJson = resourceSelectorJson;
            }

            string actionsJson = DataTableHelper.GetStringValue(row, "actions");
            if (!String.IsNullOrEmpty(actionsJson))
            {
                obj.ActionsJson = actionsJson;
            }

            return obj;
        }

        /// <summary>
        /// Instantiate list from DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of instances.</returns>
        public static List<ModelAccessRule> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<ModelAccessRule>();

            List<ModelAccessRule> ret = new List<ModelAccessRule>();
            foreach (DataRow row in table.Rows)
            {
                ModelAccessRule obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
