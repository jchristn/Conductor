namespace Conductor.Core.Database.MySql.Implementations
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// MySQL model definition methods implementation.
    /// </summary>
    public class ModelDefinitionMethods : IModelDefinitionMethods
    {
        private readonly MySqlDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the model definition methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        public ModelDefinitionMethods(MySqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a model definition.
        /// </summary>
        public async Task<ModelDefinition> CreateAsync(ModelDefinition definition, CancellationToken token = default)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            definition.CreatedUtc = DateTime.UtcNow;
            definition.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO modeldefinitions (id, tenantid, name, sourceurl, family, parametersize, quantizationlevel, contextwindowsize, supportsembeddings, supportscompletions, active, createdutc, lastupdateutc, labels, tags, metadata) " +
                           "VALUES ('" + _Driver.Sanitize(definition.Id) + "', " +
                           "'" + _Driver.Sanitize(definition.TenantId) + "', " +
                           "'" + _Driver.Sanitize(definition.Name) + "', " +
                           _Driver.FormatNullableString(definition.SourceUrl) + ", " +
                           _Driver.FormatNullableString(definition.Family) + ", " +
                           _Driver.FormatNullableString(definition.ParameterSize) + ", " +
                           _Driver.FormatNullableString(definition.QuantizationLevel) + ", " +
                           _Driver.FormatNullable(definition.ContextWindowSize) + ", " +
                           _Driver.FormatBoolean(definition.SupportsEmbeddings) + ", " +
                           _Driver.FormatBoolean(definition.SupportsCompletions) + ", " +
                           _Driver.FormatBoolean(definition.Active) + ", " +
                           "'" + _Driver.FormatDateTime(definition.CreatedUtc) + "', " +
                           "'" + _Driver.FormatDateTime(definition.LastUpdateUtc) + "', " +
                           _Driver.FormatNullableString(definition.LabelsJson) + ", " +
                           _Driver.FormatNullableString(definition.TagsJson) + ", " +
                           _Driver.FormatNullableString(definition.MetadataJson) + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return definition;
        }

        /// <summary>
        /// Read a model definition by ID.
        /// </summary>
        public async Task<ModelDefinition> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM modeldefinitions WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return ModelDefinition.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update a model definition.
        /// </summary>
        public async Task<ModelDefinition> UpdateAsync(ModelDefinition definition, CancellationToken token = default)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            definition.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE modeldefinitions SET " +
                           "name = '" + _Driver.Sanitize(definition.Name) + "', " +
                           "sourceurl = " + _Driver.FormatNullableString(definition.SourceUrl) + ", " +
                           "family = " + _Driver.FormatNullableString(definition.Family) + ", " +
                           "parametersize = " + _Driver.FormatNullableString(definition.ParameterSize) + ", " +
                           "quantizationlevel = " + _Driver.FormatNullableString(definition.QuantizationLevel) + ", " +
                           "contextwindowsize = " + _Driver.FormatNullable(definition.ContextWindowSize) + ", " +
                           "supportsembeddings = " + _Driver.FormatBoolean(definition.SupportsEmbeddings) + ", " +
                           "supportscompletions = " + _Driver.FormatBoolean(definition.SupportsCompletions) + ", " +
                           "active = " + _Driver.FormatBoolean(definition.Active) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(definition.LastUpdateUtc) + "', " +
                           "labels = " + _Driver.FormatNullableString(definition.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(definition.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(definition.MetadataJson) + " " +
                           "WHERE tenantid = '" + _Driver.Sanitize(definition.TenantId) + "' AND id = '" + _Driver.Sanitize(definition.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return definition;
        }

        /// <summary>
        /// Delete a model definition by ID.
        /// </summary>
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM modeldefinitions WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a model definition exists by ID.
        /// </summary>
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM modeldefinitions WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt32(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate model definitions.
        /// </summary>
        public async Task<EnumerationResult<ModelDefinition>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            // If tenantId is null/empty, return all definitions (admin access)
            System.Collections.Generic.List<string> conditions = new System.Collections.Generic.List<string>();
            if (!String.IsNullOrEmpty(tenantId))
            {
                conditions.Add("tenantid = '" + _Driver.Sanitize(tenantId) + "'");
            }
            if (!String.IsNullOrEmpty(request.NameFilter))
            {
                conditions.Add("name LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");
            }
            if (request.ActiveFilter.HasValue)
            {
                conditions.Add("active = " + _Driver.FormatBoolean(request.ActiveFilter.Value));
            }
            string whereClause = conditions.Count > 0 ? "WHERE " + String.Join(" AND ", conditions) : "";

            string orderBy = GetOrderBy(request.Order);
            int offset = 0;
            if (!String.IsNullOrEmpty(request.ContinuationToken))
            {
                Int32.TryParse(request.ContinuationToken, out offset);
            }

            string countQuery = "SELECT COUNT(*) AS cnt FROM modeldefinitions " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            string query = "SELECT * FROM modeldefinitions " + whereClause + " " + orderBy +
                           " LIMIT " + (request.MaxResults + 1) + " OFFSET " + offset + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            System.Collections.Generic.List<ModelDefinition> data = ModelDefinition.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore)
            {
                data.RemoveAt(data.Count - 1);
            }

            return new EnumerationResult<ModelDefinition>
            {
                Data = data,
                TotalCount = totalCount,
                HasMore = hasMore,
                ContinuationToken = hasMore ? (offset + request.MaxResults).ToString() : null
            };
        }

        private string GetOrderBy(EnumerationOrderEnum order)
        {
            switch (order)
            {
                case EnumerationOrderEnum.CreatedAscending:
                    return "ORDER BY createdutc ASC";
                case EnumerationOrderEnum.CreatedDescending:
                    return "ORDER BY createdutc DESC";
                case EnumerationOrderEnum.LastUpdateAscending:
                    return "ORDER BY lastupdateutc ASC";
                case EnumerationOrderEnum.LastUpdateDescending:
                    return "ORDER BY lastupdateutc DESC";
                case EnumerationOrderEnum.NameAscending:
                    return "ORDER BY name ASC";
                case EnumerationOrderEnum.NameDescending:
                    return "ORDER BY name DESC";
                default:
                    return "ORDER BY createdutc DESC";
            }
        }
    }
}
