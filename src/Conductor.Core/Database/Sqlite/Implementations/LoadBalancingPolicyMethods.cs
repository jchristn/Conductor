namespace Conductor.Core.Database.Sqlite.Implementations
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// SQLite load-balancing policy methods implementation.
    /// </summary>
    public class LoadBalancingPolicyMethods : ILoadBalancingPolicyMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        public LoadBalancingPolicyMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public async Task<LoadBalancingPolicy> CreateAsync(LoadBalancingPolicy policy, CancellationToken token = default)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            policy.CreatedUtc = DateTime.UtcNow;
            policy.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO loadbalancingpolicies (id, tenantid, name, description, maxtelemetryagems, filters, ranking, fallbackmode, tiebreaker, active, createdutc, lastupdateutc, labels, tags, metadata) VALUES ('" +
                           _Driver.Sanitize(policy.Id) + "', '" +
                           _Driver.Sanitize(policy.TenantId) + "', '" +
                           _Driver.Sanitize(policy.Name) + "', " +
                           _Driver.FormatNullableString(policy.Description) + ", " +
                           policy.MaxTelemetryAgeMs + ", " +
                           _Driver.FormatNullableString(policy.FiltersJson) + ", " +
                           _Driver.FormatNullableString(policy.RankingJson) + ", " +
                           (int)policy.FallbackMode + ", " +
                           (int)policy.TieBreaker + ", " +
                           _Driver.FormatBoolean(policy.Active) + ", '" +
                           _Driver.FormatDateTime(policy.CreatedUtc) + "', '" +
                           _Driver.FormatDateTime(policy.LastUpdateUtc) + "', " +
                           _Driver.FormatNullableString(policy.LabelsJson) + ", " +
                           _Driver.FormatNullableString(policy.TagsJson) + ", " +
                           _Driver.FormatNullableString(policy.MetadataJson) + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return policy;
        }

        public async Task<LoadBalancingPolicy> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM loadbalancingpolicies WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return LoadBalancingPolicy.FromDataRow(result.Rows[0]);
        }

        public async Task<LoadBalancingPolicy> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM loadbalancingpolicies WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return LoadBalancingPolicy.FromDataRow(result.Rows[0]);
        }

        public async Task<LoadBalancingPolicy> UpdateAsync(LoadBalancingPolicy policy, CancellationToken token = default)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            policy.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE loadbalancingpolicies SET " +
                           "name = '" + _Driver.Sanitize(policy.Name) + "', " +
                           "description = " + _Driver.FormatNullableString(policy.Description) + ", " +
                           "maxtelemetryagems = " + policy.MaxTelemetryAgeMs + ", " +
                           "filters = " + _Driver.FormatNullableString(policy.FiltersJson) + ", " +
                           "ranking = " + _Driver.FormatNullableString(policy.RankingJson) + ", " +
                           "fallbackmode = " + (int)policy.FallbackMode + ", " +
                           "tiebreaker = " + (int)policy.TieBreaker + ", " +
                           "active = " + _Driver.FormatBoolean(policy.Active) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(policy.LastUpdateUtc) + "', " +
                           "labels = " + _Driver.FormatNullableString(policy.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(policy.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(policy.MetadataJson) + " " +
                           "WHERE tenantid = '" + _Driver.Sanitize(policy.TenantId) + "' AND id = '" + _Driver.Sanitize(policy.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return policy;
        }

        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM loadbalancingpolicies WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM loadbalancingpolicies WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt32(result.Rows[0]["cnt"]) > 0;
        }

        public async Task<EnumerationResult<LoadBalancingPolicy>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            System.Collections.Generic.List<string> conditions = new System.Collections.Generic.List<string>();
            if (!String.IsNullOrEmpty(tenantId)) conditions.Add("tenantid = '" + _Driver.Sanitize(tenantId) + "'");
            if (!String.IsNullOrEmpty(request.NameFilter)) conditions.Add("name LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");
            if (request.ActiveFilter.HasValue) conditions.Add("active = " + _Driver.FormatBoolean(request.ActiveFilter.Value));
            string whereClause = conditions.Count > 0 ? "WHERE " + String.Join(" AND ", conditions) : "";

            string orderBy = GetOrderBy(request.Order);
            int offset = 0;
            if (!String.IsNullOrEmpty(request.ContinuationToken)) Int32.TryParse(request.ContinuationToken, out offset);

            string countQuery = "SELECT COUNT(*) AS cnt FROM loadbalancingpolicies " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0) totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            string query = "SELECT * FROM loadbalancingpolicies " + whereClause + " " + orderBy +
                           " LIMIT " + (request.MaxResults + 1) + " OFFSET " + offset + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            System.Collections.Generic.List<LoadBalancingPolicy> data = LoadBalancingPolicy.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore) data.RemoveAt(data.Count - 1);

            return new EnumerationResult<LoadBalancingPolicy>
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
