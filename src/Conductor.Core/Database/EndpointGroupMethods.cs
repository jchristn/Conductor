namespace Conductor.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// Provider-neutral endpoint group database implementation.
    /// </summary>
    public class EndpointGroupMethods : IEndpointGroupMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly RequestAnalyticsSqlDialect _Dialect;

        /// <summary>
        /// Instantiate endpoint group methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <param name="dialect">SQL dialect.</param>
        public EndpointGroupMethods(DatabaseDriverBase driver, RequestAnalyticsSqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Dialect = dialect;
        }

        /// <inheritdoc />
        public async Task<EndpointGroup> CreateAsync(EndpointGroup group, CancellationToken token = default)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            group.CreatedUtc = DateTime.UtcNow;
            group.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO endpointgroups (id, tenantid, name, description, priority, active, trafficweight, endpointids, createdutc, lastupdateutc, labels, tags, metadata) VALUES ('" +
                           _Driver.Sanitize(group.Id) + "', '" +
                           _Driver.Sanitize(group.TenantId) + "', '" +
                           _Driver.Sanitize(group.Name) + "', " +
                           _Driver.FormatNullableString(group.Description) + ", " +
                           group.Priority + ", " +
                           _Driver.FormatBoolean(group.Active) + ", " +
                           group.TrafficWeight + ", " +
                           _Driver.FormatNullableString(group.EndpointIdsJson) + ", '" +
                           _Driver.FormatDateTime(group.CreatedUtc) + "', '" +
                           _Driver.FormatDateTime(group.LastUpdateUtc) + "', " +
                           _Driver.FormatNullableString(group.LabelsJson) + ", " +
                           _Driver.FormatNullableString(group.TagsJson) + ", " +
                           _Driver.FormatNullableString(group.MetadataJson) + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return group;
        }

        /// <inheritdoc />
        public async Task<EndpointGroup> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM endpointgroups WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return EndpointGroup.FromDataRow(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<EndpointGroup> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM endpointgroups WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return EndpointGroup.FromDataRow(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<EndpointGroup> UpdateAsync(EndpointGroup group, CancellationToken token = default)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            group.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE endpointgroups SET " +
                           "name = '" + _Driver.Sanitize(group.Name) + "', " +
                           "description = " + _Driver.FormatNullableString(group.Description) + ", " +
                           "priority = " + group.Priority + ", " +
                           "active = " + _Driver.FormatBoolean(group.Active) + ", " +
                           "trafficweight = " + group.TrafficWeight + ", " +
                           "endpointids = " + _Driver.FormatNullableString(group.EndpointIdsJson) + ", " +
                           "labels = " + _Driver.FormatNullableString(group.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(group.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(group.MetadataJson) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(group.LastUpdateUtc) + "' " +
                           "WHERE tenantid = '" + _Driver.Sanitize(group.TenantId) + "' AND id = '" + _Driver.Sanitize(group.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return group;
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM endpointgroups WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM endpointgroups WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<EndpointGroup>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            request ??= new EnumerationRequest();

            List<string> conditions = new List<string>();
            if (!String.IsNullOrEmpty(tenantId)) conditions.Add("tenantid = '" + _Driver.Sanitize(tenantId) + "'");
            if (!String.IsNullOrEmpty(request.NameFilter)) conditions.Add("name LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");
            if (request.ActiveFilter.HasValue) conditions.Add("active = " + _Driver.FormatBoolean(request.ActiveFilter.Value));
            string whereClause = conditions.Count > 0 ? "WHERE " + String.Join(" AND ", conditions) : "";

            string orderBy = GetOrderBy(request.Order);
            int offset = ParseOffset(request.ContinuationToken);

            string countQuery = "SELECT COUNT(*) AS cnt FROM endpointgroups " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0) totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            string query = BuildPagedQuery("endpointgroups", whereClause, orderBy, request.MaxResults + 1, offset);
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<EndpointGroup> data = EndpointGroup.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore) data.RemoveAt(data.Count - 1);

            return new EnumerationResult<EndpointGroup>
            {
                Data = data,
                TotalCount = totalCount,
                HasMore = hasMore,
                ContinuationToken = hasMore ? (offset + request.MaxResults).ToString() : null
            };
        }

        private string BuildPagedQuery(string tableName, string whereClause, string orderBy, int take, int offset)
        {
            if (_Dialect == RequestAnalyticsSqlDialect.SqlServer)
            {
                return "SELECT * FROM " + tableName + " " + whereClause + " " + orderBy +
                       " OFFSET " + offset + " ROWS FETCH NEXT " + take + " ROWS ONLY;";
            }

            return "SELECT * FROM " + tableName + " " + whereClause + " " + orderBy +
                   " LIMIT " + take + " OFFSET " + offset + ";";
        }

        private static int ParseOffset(string continuationToken)
        {
            int offset = 0;
            if (!String.IsNullOrEmpty(continuationToken)) Int32.TryParse(continuationToken, out offset);
            return offset;
        }

        private static string GetOrderBy(EnumerationOrderEnum order)
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
