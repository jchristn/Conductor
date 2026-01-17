namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json.Serialization;
    using Conductor.Core.Helpers;
    using Conductor.Core.Serialization;

    /// <summary>
    /// Model configuration with pinned properties for request modification.
    /// </summary>
    public class ModelConfiguration
    {
        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewModelConfigurationId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantId)) : value);
        }

        /// <summary>
        /// Configuration name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Name)) : value);
        }

        /// <summary>
        /// Context window size override.
        /// </summary>
        public int? ContextWindowSize { get; set; } = null;

        /// <summary>
        /// Temperature for generation.
        /// </summary>
        public decimal? Temperature { get; set; } = null;

        /// <summary>
        /// Top-P sampling parameter.
        /// </summary>
        public decimal? TopP { get; set; } = null;

        /// <summary>
        /// Top-K sampling parameter.
        /// </summary>
        public int? TopK { get; set; } = null;

        /// <summary>
        /// Repeat penalty.
        /// </summary>
        public decimal? RepeatPenalty { get; set; } = null;

        /// <summary>
        /// Maximum tokens to generate.
        /// </summary>
        public int? MaxTokens { get; set; } = null;

        /// <summary>
        /// Model name this configuration applies to.
        /// When null, the configuration applies to all models.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Pinned properties for embeddings requests.
        /// These properties are merged into every embeddings request.
        /// </summary>
        public Dictionary<string, object> PinnedEmbeddingsProperties
        {
            get => _PinnedEmbeddingsProperties;
            set
            {
                _PinnedEmbeddingsProperties = (value != null ? value : new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase));
                _PinnedEmbeddingsPropertiesJson = _Serializer.SerializeJson(_PinnedEmbeddingsProperties, false);
            }
        }

        /// <summary>
        /// Pinned properties for completions requests.
        /// These properties are merged into every completions request.
        /// </summary>
        public Dictionary<string, object> PinnedCompletionsProperties
        {
            get => _PinnedCompletionsProperties;
            set
            {
                _PinnedCompletionsProperties = (value != null ? value : new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase));
                _PinnedCompletionsPropertiesJson = _Serializer.SerializeJson(_PinnedCompletionsProperties, false);
            }
        }

        /// <summary>
        /// JSON-serialized pinned embeddings properties for database storage.
        /// </summary>
        [JsonIgnore]
        public string PinnedEmbeddingsPropertiesJson
        {
            get => _PinnedEmbeddingsPropertiesJson;
            set
            {
                _PinnedEmbeddingsPropertiesJson = (String.IsNullOrEmpty(value) ? "{}" : value);
                _PinnedEmbeddingsProperties = _Serializer.DeserializeJson<Dictionary<string, object>>(_PinnedEmbeddingsPropertiesJson);
            }
        }

        /// <summary>
        /// JSON-serialized pinned completions properties for database storage.
        /// </summary>
        [JsonIgnore]
        public string PinnedCompletionsPropertiesJson
        {
            get => _PinnedCompletionsPropertiesJson;
            set
            {
                _PinnedCompletionsPropertiesJson = (String.IsNullOrEmpty(value) ? "{}" : value);
                _PinnedCompletionsProperties = _Serializer.DeserializeJson<Dictionary<string, object>>(_PinnedCompletionsPropertiesJson);
            }
        }

        /// <summary>
        /// Boolean indicating if the configuration is active.
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
        private string _Name = "Default Configuration";
        private Dictionary<string, object> _PinnedEmbeddingsProperties = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);
        private string _PinnedEmbeddingsPropertiesJson = "{}";
        private Dictionary<string, object> _PinnedCompletionsProperties = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);
        private string _PinnedCompletionsPropertiesJson = "{}";
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate the model configuration.
        /// </summary>
        public ModelConfiguration()
        {
        }

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance.</returns>
        public static ModelConfiguration FromDataRow(DataRow row)
        {
            if (row == null) return null;

            ModelConfiguration obj = new ModelConfiguration
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantId = DataTableHelper.GetStringValue(row, "tenantid"),
                Name = DataTableHelper.GetStringValue(row, "name") ?? "Default Configuration",
                ContextWindowSize = DataTableHelper.GetNullableIntValue(row, "contextwindowsize"),
                Temperature = DataTableHelper.GetNullableDecimalValue(row, "temperature"),
                TopP = DataTableHelper.GetNullableDecimalValue(row, "topp"),
                TopK = DataTableHelper.GetNullableIntValue(row, "topk"),
                RepeatPenalty = DataTableHelper.GetNullableDecimalValue(row, "repeatpenalty"),
                MaxTokens = DataTableHelper.GetNullableIntValue(row, "maxtokens"),
                Model = DataTableHelper.GetStringValue(row, "model"),
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc")
            };

            string pinnedEmbeddingsJson = DataTableHelper.GetStringValue(row, "pinnedembeddingsproperties");
            if (!String.IsNullOrEmpty(pinnedEmbeddingsJson))
            {
                obj.PinnedEmbeddingsPropertiesJson = pinnedEmbeddingsJson;
            }

            string pinnedCompletionsJson = DataTableHelper.GetStringValue(row, "pinnedcompletionsproperties");
            if (!String.IsNullOrEmpty(pinnedCompletionsJson))
            {
                obj.PinnedCompletionsPropertiesJson = pinnedCompletionsJson;
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
        public static List<ModelConfiguration> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<ModelConfiguration>();

            List<ModelConfiguration> ret = new List<ModelConfiguration>();
            foreach (DataRow row in table.Rows)
            {
                ModelConfiguration obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
