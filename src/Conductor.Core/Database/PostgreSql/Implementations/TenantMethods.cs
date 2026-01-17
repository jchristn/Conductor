namespace Conductor.Core.Database.PostgreSql.Implementations
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// PostgreSQL tenant methods implementation.
    /// </summary>
    public class TenantMethods : ITenantMethods
    {
        private readonly PostgreSqlDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the tenant methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        public TenantMethods(PostgreSqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a tenant.
        /// </summary>
        public async Task<TenantMetadata> CreateAsync(TenantMetadata tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));

            tenant.CreatedUtc = DateTime.UtcNow;
            tenant.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO tenants (id, name, active, createdutc, lastupdateutc, labels, tags, metadata) " +
                           "VALUES ('" + _Driver.Sanitize(tenant.Id) + "', " +
                           "'" + _Driver.Sanitize(tenant.Name) + "', " +
                           _Driver.FormatBoolean(tenant.Active) + ", " +
                           "'" + _Driver.FormatDateTime(tenant.CreatedUtc) + "', " +
                           "'" + _Driver.FormatDateTime(tenant.LastUpdateUtc) + "', " +
                           _Driver.FormatNullableString(tenant.LabelsJson) + ", " +
                           _Driver.FormatNullableString(tenant.TagsJson) + ", " +
                           _Driver.FormatNullableString(tenant.MetadataJson) + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return tenant;
        }

        /// <summary>
        /// Read a tenant by ID.
        /// </summary>
        public async Task<TenantMetadata> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM tenants WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return TenantMetadata.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update a tenant.
        /// </summary>
        public async Task<TenantMetadata> UpdateAsync(TenantMetadata tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));

            tenant.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE tenants SET " +
                           "name = '" + _Driver.Sanitize(tenant.Name) + "', " +
                           "active = " + _Driver.FormatBoolean(tenant.Active) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(tenant.LastUpdateUtc) + "', " +
                           "labels = " + _Driver.FormatNullableString(tenant.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(tenant.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(tenant.MetadataJson) + " " +
                           "WHERE id = '" + _Driver.Sanitize(tenant.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return tenant;
        }

        /// <summary>
        /// Delete a tenant by ID.
        /// </summary>
        public async Task DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM tenants WHERE id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a tenant exists by ID.
        /// </summary>
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM tenants WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt32(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate tenants.
        /// </summary>
        public async Task<EnumerationResult<TenantMetadata>> EnumerateAsync(EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            string whereClause = "WHERE 1=1";
            if (!String.IsNullOrEmpty(request.NameFilter))
            {
                whereClause += " AND name ILIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'";
            }
            if (request.ActiveFilter.HasValue)
            {
                whereClause += " AND active = " + _Driver.FormatBoolean(request.ActiveFilter.Value);
            }

            string orderBy = GetOrderBy(request.Order);
            int offset = 0;
            if (!String.IsNullOrEmpty(request.ContinuationToken))
            {
                Int32.TryParse(request.ContinuationToken, out offset);
            }

            string countQuery = "SELECT COUNT(*) AS cnt FROM tenants " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            string query = "SELECT * FROM tenants " + whereClause + " " + orderBy +
                           " LIMIT " + (request.MaxResults + 1) + " OFFSET " + offset + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            System.Collections.Generic.List<TenantMetadata> data = TenantMetadata.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore)
            {
                data.RemoveAt(data.Count - 1);
            }

            return new EnumerationResult<TenantMetadata>
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
