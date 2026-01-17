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
    /// PostgreSQL credential methods implementation.
    /// </summary>
    public class CredentialMethods : ICredentialMethods
    {
        private readonly PostgreSqlDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the credential methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        public CredentialMethods(PostgreSqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a credential.
        /// </summary>
        public async Task<Credential> CreateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            credential.CreatedUtc = DateTime.UtcNow;
            credential.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO credentials (id, tenantid, userid, name, bearertoken, active, createdutc, lastupdateutc, labels, tags, metadata) " +
                           "VALUES ('" + _Driver.Sanitize(credential.Id) + "', " +
                           "'" + _Driver.Sanitize(credential.TenantId) + "', " +
                           "'" + _Driver.Sanitize(credential.UserId) + "', " +
                           _Driver.FormatNullableString(credential.Name) + ", " +
                           "'" + _Driver.Sanitize(credential.BearerToken) + "', " +
                           _Driver.FormatBoolean(credential.Active) + ", " +
                           "'" + _Driver.FormatDateTime(credential.CreatedUtc) + "', " +
                           "'" + _Driver.FormatDateTime(credential.LastUpdateUtc) + "', " +
                           _Driver.FormatNullableString(credential.LabelsJson) + ", " +
                           _Driver.FormatNullableString(credential.TagsJson) + ", " +
                           _Driver.FormatNullableString(credential.MetadataJson) + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return credential;
        }

        /// <summary>
        /// Read a credential by ID.
        /// </summary>
        public async Task<Credential> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM credentials WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return Credential.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read a credential by bearer token.
        /// </summary>
        public async Task<Credential> ReadByBearerTokenAsync(string bearerToken, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(bearerToken)) throw new ArgumentNullException(nameof(bearerToken));

            string query = "SELECT * FROM credentials WHERE bearertoken = '" + _Driver.Sanitize(bearerToken) + "' AND active = TRUE;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return Credential.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update a credential.
        /// </summary>
        public async Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            credential.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE credentials SET " +
                           "name = " + _Driver.FormatNullableString(credential.Name) + ", " +
                           "bearertoken = '" + _Driver.Sanitize(credential.BearerToken) + "', " +
                           "active = " + _Driver.FormatBoolean(credential.Active) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(credential.LastUpdateUtc) + "', " +
                           "labels = " + _Driver.FormatNullableString(credential.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(credential.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(credential.MetadataJson) + " " +
                           "WHERE tenantid = '" + _Driver.Sanitize(credential.TenantId) + "' AND id = '" + _Driver.Sanitize(credential.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return credential;
        }

        /// <summary>
        /// Delete a credential by ID.
        /// </summary>
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM credentials WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a credential exists by ID.
        /// </summary>
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM credentials WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt32(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate credentials.
        /// </summary>
        public async Task<EnumerationResult<Credential>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            // If tenantId is null/empty, return all credentials (admin access)
            System.Collections.Generic.List<string> conditions = new System.Collections.Generic.List<string>();
            if (!String.IsNullOrEmpty(tenantId))
            {
                conditions.Add("tenantid = '" + _Driver.Sanitize(tenantId) + "'");
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

            string countQuery = "SELECT COUNT(*) AS cnt FROM credentials " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            string query = "SELECT * FROM credentials " + whereClause + " " + orderBy +
                           " LIMIT " + (request.MaxResults + 1) + " OFFSET " + offset + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            System.Collections.Generic.List<Credential> data = Credential.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore)
            {
                data.RemoveAt(data.Count - 1);
            }

            return new EnumerationResult<Credential>
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
