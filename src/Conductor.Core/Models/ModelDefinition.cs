namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json.Serialization;
    using Conductor.Core.Helpers;
    using Conductor.Core.Serialization;

    /// <summary>
    /// Model definition metadata.
    /// </summary>
    public class ModelDefinition
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewModelDefinitionId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// Model name (e.g., "llama3.2:latest").
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Name)) : value);
        }

        /// <summary>
        /// Source URL for pulling the model.
        /// </summary>
        public string SourceUrl { get; set; } = null;

        /// <summary>
        /// Model family (e.g., "llama", "mistral", "qwen").
        /// </summary>
        public string Family { get; set; } = null;

        /// <summary>
        /// Parameter size (e.g., "7B", "13B", "70B").
        /// </summary>
        public string ParameterSize { get; set; } = null;

        /// <summary>
        /// Quantization level (e.g., "Q4_0", "Q8_0", "F16").
        /// </summary>
        public string QuantizationLevel { get; set; } = null;

        /// <summary>
        /// Context window size.
        /// </summary>
        public int? ContextWindowSize { get; set; } = null;

        /// <summary>
        /// Boolean indicating if the model supports embeddings.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// Boolean indicating if the model supports completions.
        /// </summary>
        public bool SupportsCompletions { get; set; } = true;

        /// <summary>
        /// Boolean indicating if the model is active.
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
        private string _Name = "llama3.2:latest";
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate the model definition.
        /// </summary>
        public ModelDefinition()
        {
        }

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance.</returns>
        public static ModelDefinition FromDataRow(DataRow row)
        {
            if (row == null) return null;

            ModelDefinition obj = new ModelDefinition
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                Name = DataTableHelper.GetStringValue(row, "name") ?? "llama3.2:latest",
                SourceUrl = DataTableHelper.GetStringValue(row, "sourceurl"),
                Family = DataTableHelper.GetStringValue(row, "family"),
                ParameterSize = DataTableHelper.GetStringValue(row, "parametersize"),
                QuantizationLevel = DataTableHelper.GetStringValue(row, "quantizationlevel"),
                ContextWindowSize = DataTableHelper.GetNullableIntValue(row, "contextwindowsize"),
                SupportsEmbeddings = DataTableHelper.GetBooleanValue(row, "supportsembeddings"),
                SupportsCompletions = DataTableHelper.GetBooleanValue(row, "supportscompletions"),
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
        public static List<ModelDefinition> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<ModelDefinition>();

            List<ModelDefinition> ret = new List<ModelDefinition>();
            foreach (DataRow row in table.Rows)
            {
                ModelDefinition obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
