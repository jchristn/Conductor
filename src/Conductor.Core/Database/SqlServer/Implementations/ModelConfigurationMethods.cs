namespace Conductor.Core.Database.SqlServer.Implementations
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// SQL Server model configuration methods implementation.
    /// </summary>
    public class ModelConfigurationMethods : IModelConfigurationMethods
    {
        private readonly SqlServerDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the model configuration methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        public ModelConfigurationMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a model configuration.
        /// </summary>
        public async Task<ModelConfiguration> CreateAsync(ModelConfiguration configuration, CancellationToken token = default)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            configuration.CreatedUtc = DateTime.UtcNow;
            configuration.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO modelconfigurations (id, tenantid, name, contextwindowsize, temperature, topp, topk, repeatpenalty, maxtokens, model, pinnedembeddingsproperties, pinnedcompletionsproperties, active, createdutc, lastupdateutc, labels, tags, metadata) " +
                           "VALUES ('" + _Driver.Sanitize(configuration.Id) + "', " +
                           "'" + _Driver.Sanitize(configuration.TenantId) + "', " +
                           "'" + _Driver.Sanitize(configuration.Name) + "', " +
                           _Driver.FormatNullable(configuration.ContextWindowSize) + ", " +
                           _Driver.FormatNullable(configuration.Temperature) + ", " +
                           _Driver.FormatNullable(configuration.TopP) + ", " +
                           _Driver.FormatNullable(configuration.TopK) + ", " +
                           _Driver.FormatNullable(configuration.RepeatPenalty) + ", " +
                           _Driver.FormatNullable(configuration.MaxTokens) + ", " +
                           _Driver.FormatNullableString(configuration.Model) + ", " +
                           _Driver.FormatNullableString(configuration.PinnedEmbeddingsPropertiesJson) + ", " +
                           _Driver.FormatNullableString(configuration.PinnedCompletionsPropertiesJson) + ", " +
                           _Driver.FormatBoolean(configuration.Active) + ", " +
                           "'" + _Driver.FormatDateTime(configuration.CreatedUtc) + "', " +
                           "'" + _Driver.FormatDateTime(configuration.LastUpdateUtc) + "', " +
                           _Driver.FormatNullableString(configuration.LabelsJson) + ", " +
                           _Driver.FormatNullableString(configuration.TagsJson) + ", " +
                           _Driver.FormatNullableString(configuration.MetadataJson) + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return configuration;
        }

        /// <summary>
        /// Read a model configuration by ID.
        /// </summary>
        public async Task<ModelConfiguration> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM modelconfigurations WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return ModelConfiguration.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read a model configuration by ID without tenant filtering (admin use only).
        /// </summary>
        public async Task<ModelConfiguration> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM modelconfigurations WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return ModelConfiguration.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update a model configuration.
        /// </summary>
        public async Task<ModelConfiguration> UpdateAsync(ModelConfiguration configuration, CancellationToken token = default)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            configuration.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE modelconfigurations SET " +
                           "name = '" + _Driver.Sanitize(configuration.Name) + "', " +
                           "contextwindowsize = " + _Driver.FormatNullable(configuration.ContextWindowSize) + ", " +
                           "temperature = " + _Driver.FormatNullable(configuration.Temperature) + ", " +
                           "topp = " + _Driver.FormatNullable(configuration.TopP) + ", " +
                           "topk = " + _Driver.FormatNullable(configuration.TopK) + ", " +
                           "repeatpenalty = " + _Driver.FormatNullable(configuration.RepeatPenalty) + ", " +
                           "maxtokens = " + _Driver.FormatNullable(configuration.MaxTokens) + ", " +
                           "model = " + _Driver.FormatNullableString(configuration.Model) + ", " +
                           "pinnedembeddingsproperties = " + _Driver.FormatNullableString(configuration.PinnedEmbeddingsPropertiesJson) + ", " +
                           "pinnedcompletionsproperties = " + _Driver.FormatNullableString(configuration.PinnedCompletionsPropertiesJson) + ", " +
                           "active = " + _Driver.FormatBoolean(configuration.Active) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(configuration.LastUpdateUtc) + "', " +
                           "labels = " + _Driver.FormatNullableString(configuration.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(configuration.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(configuration.MetadataJson) + " " +
                           "WHERE tenantid = '" + _Driver.Sanitize(configuration.TenantId) + "' AND id = '" + _Driver.Sanitize(configuration.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return configuration;
        }

        /// <summary>
        /// Delete a model configuration by ID.
        /// </summary>
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM modelconfigurations WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a model configuration exists by ID.
        /// </summary>
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM modelconfigurations WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt32(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate model configurations.
        /// </summary>
        public async Task<EnumerationResult<ModelConfiguration>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            // If tenantId is null/empty, return all configurations (admin access)
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

            string countQuery = "SELECT COUNT(*) AS cnt FROM modelconfigurations " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            string query = "SELECT * FROM modelconfigurations " + whereClause + " " + orderBy +
                           " OFFSET " + offset + " ROWS FETCH NEXT " + (request.MaxResults + 1) + " ROWS ONLY;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            System.Collections.Generic.List<ModelConfiguration> data = ModelConfiguration.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore)
            {
                data.RemoveAt(data.Count - 1);
            }

            return new EnumerationResult<ModelConfiguration>
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
