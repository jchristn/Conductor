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
    /// Provider-neutral model access policy database implementation.
    /// </summary>
    public class ModelAccessPolicyMethods : IModelAccessPolicyMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly RequestAnalyticsSqlDialect _Dialect;

        /// <summary>
        /// Instantiate model access policy methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <param name="dialect">SQL dialect.</param>
        public ModelAccessPolicyMethods(DatabaseDriverBase driver, RequestAnalyticsSqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Dialect = dialect;
        }

        /// <inheritdoc />
        public async Task<ModelAccessPolicy> CreateAsync(ModelAccessPolicy policy, CancellationToken token = default)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            policy.CreatedUtc = DateTime.UtcNow;
            policy.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO modelaccesspolicies (id, tenantid, name, description, defaultdecision, active, labels, tags, metadata, createdutc, lastupdateutc) VALUES ('" +
                           _Driver.Sanitize(policy.Id) + "', '" +
                           _Driver.Sanitize(policy.TenantId) + "', '" +
                           _Driver.Sanitize(policy.Name) + "', " +
                           _Driver.FormatNullableString(policy.Description) + ", " +
                           (int)policy.DefaultDecision + ", " +
                           _Driver.FormatBoolean(policy.Active) + ", " +
                           _Driver.FormatNullableString(policy.LabelsJson) + ", " +
                           _Driver.FormatNullableString(policy.TagsJson) + ", " +
                           _Driver.FormatNullableString(policy.MetadataJson) + ", '" +
                           _Driver.FormatDateTime(policy.CreatedUtc) + "', '" +
                           _Driver.FormatDateTime(policy.LastUpdateUtc) + "');";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return policy;
        }

        /// <inheritdoc />
        public async Task<ModelAccessPolicy> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM modelaccesspolicies WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return ModelAccessPolicy.FromDataRow(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<ModelAccessPolicy> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM modelaccesspolicies WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return ModelAccessPolicy.FromDataRow(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<ModelAccessPolicy> UpdateAsync(ModelAccessPolicy policy, CancellationToken token = default)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            policy.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE modelaccesspolicies SET " +
                           "name = '" + _Driver.Sanitize(policy.Name) + "', " +
                           "description = " + _Driver.FormatNullableString(policy.Description) + ", " +
                           "defaultdecision = " + (int)policy.DefaultDecision + ", " +
                           "active = " + _Driver.FormatBoolean(policy.Active) + ", " +
                           "labels = " + _Driver.FormatNullableString(policy.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(policy.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(policy.MetadataJson) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(policy.LastUpdateUtc) + "' " +
                           "WHERE tenantid = '" + _Driver.Sanitize(policy.TenantId) + "' AND id = '" + _Driver.Sanitize(policy.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return policy;
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            List<string> queries = new List<string>
            {
                "DELETE FROM modelaccessrules WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND policyid = '" + _Driver.Sanitize(id) + "';",
                "DELETE FROM modelaccesspolicies WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';"
            };

            await _Driver.ExecuteQueriesAsync(queries, false, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM modelaccesspolicies WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<ModelAccessPolicy>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            List<string> conditions = new List<string>();
            if (!String.IsNullOrEmpty(tenantId)) conditions.Add("tenantid = '" + _Driver.Sanitize(tenantId) + "'");
            if (!String.IsNullOrEmpty(request.NameFilter)) conditions.Add("name LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");
            if (request.ActiveFilter.HasValue) conditions.Add("active = " + _Driver.FormatBoolean(request.ActiveFilter.Value));
            string whereClause = conditions.Count > 0 ? "WHERE " + String.Join(" AND ", conditions) : "";

            string orderBy = GetOrderBy(request.Order);
            int offset = ParseOffset(request.ContinuationToken);

            string countQuery = "SELECT COUNT(*) AS cnt FROM modelaccesspolicies " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0) totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            string query = BuildPagedQuery("modelaccesspolicies", whereClause, orderBy, request.MaxResults + 1, offset);
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<ModelAccessPolicy> data = ModelAccessPolicy.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore) data.RemoveAt(data.Count - 1);

            return new EnumerationResult<ModelAccessPolicy>
            {
                Data = data,
                TotalCount = totalCount,
                HasMore = hasMore,
                ContinuationToken = hasMore ? (offset + request.MaxResults).ToString() : null
            };
        }

        /// <inheritdoc />
        public async Task<ModelAccessRule> CreateRuleAsync(ModelAccessRule rule, CancellationToken token = default)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            rule.CreatedUtc = DateTime.UtcNow;
            rule.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO modelaccessrules (id, tenantid, policyid, name, description, priority, effect, subjecttype, subjectid, subjectselector, resourcetype, resourceid, resourceselector, vmrid, actions, active, createdutc, lastupdateutc) VALUES ('" +
                           _Driver.Sanitize(rule.Id) + "', '" +
                           _Driver.Sanitize(rule.TenantId) + "', '" +
                           _Driver.Sanitize(rule.PolicyId) + "', '" +
                           _Driver.Sanitize(rule.Name) + "', " +
                           _Driver.FormatNullableString(rule.Description) + ", " +
                           rule.Priority + ", " +
                           (int)rule.Effect + ", " +
                           (int)rule.SubjectType + ", " +
                           _Driver.FormatNullableString(rule.SubjectId) + ", " +
                           _Driver.FormatNullableString(rule.SubjectSelectorJson) + ", " +
                           (int)rule.ResourceType + ", " +
                           _Driver.FormatNullableString(rule.ResourceId) + ", " +
                           _Driver.FormatNullableString(rule.ResourceSelectorJson) + ", " +
                           _Driver.FormatNullableString(rule.VirtualModelRunnerId) + ", " +
                           _Driver.FormatNullableString(rule.ActionsJson) + ", " +
                           _Driver.FormatBoolean(rule.Active) + ", '" +
                           _Driver.FormatDateTime(rule.CreatedUtc) + "', '" +
                           _Driver.FormatDateTime(rule.LastUpdateUtc) + "');";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return rule;
        }

        /// <inheritdoc />
        public async Task<ModelAccessRule> ReadRuleAsync(string tenantId, string policyId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(policyId)) throw new ArgumentNullException(nameof(policyId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM modelaccessrules WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND policyid = '" + _Driver.Sanitize(policyId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return ModelAccessRule.FromDataRow(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<ModelAccessRule> ReadRuleByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM modelaccessrules WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return ModelAccessRule.FromDataRow(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<ModelAccessRule> UpdateRuleAsync(ModelAccessRule rule, CancellationToken token = default)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            rule.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE modelaccessrules SET " +
                           "name = '" + _Driver.Sanitize(rule.Name) + "', " +
                           "description = " + _Driver.FormatNullableString(rule.Description) + ", " +
                           "priority = " + rule.Priority + ", " +
                           "effect = " + (int)rule.Effect + ", " +
                           "subjecttype = " + (int)rule.SubjectType + ", " +
                           "subjectid = " + _Driver.FormatNullableString(rule.SubjectId) + ", " +
                           "subjectselector = " + _Driver.FormatNullableString(rule.SubjectSelectorJson) + ", " +
                           "resourcetype = " + (int)rule.ResourceType + ", " +
                           "resourceid = " + _Driver.FormatNullableString(rule.ResourceId) + ", " +
                           "resourceselector = " + _Driver.FormatNullableString(rule.ResourceSelectorJson) + ", " +
                           "vmrid = " + _Driver.FormatNullableString(rule.VirtualModelRunnerId) + ", " +
                           "actions = " + _Driver.FormatNullableString(rule.ActionsJson) + ", " +
                           "active = " + _Driver.FormatBoolean(rule.Active) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(rule.LastUpdateUtc) + "' " +
                           "WHERE tenantid = '" + _Driver.Sanitize(rule.TenantId) + "' AND policyid = '" + _Driver.Sanitize(rule.PolicyId) + "' AND id = '" + _Driver.Sanitize(rule.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return rule;
        }

        /// <inheritdoc />
        public async Task DeleteRuleAsync(string tenantId, string policyId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(policyId)) throw new ArgumentNullException(nameof(policyId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM modelaccessrules WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND policyid = '" + _Driver.Sanitize(policyId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteRulesByPolicyAsync(string tenantId, string policyId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(policyId)) throw new ArgumentNullException(nameof(policyId));

            string query = "DELETE FROM modelaccessrules WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND policyid = '" + _Driver.Sanitize(policyId) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsRuleAsync(string tenantId, string policyId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(policyId)) throw new ArgumentNullException(nameof(policyId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM modelaccessrules WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND policyid = '" + _Driver.Sanitize(policyId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<ModelAccessRule>> EnumerateRulesAsync(string tenantId, string policyId, EnumerationRequest request, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(policyId)) throw new ArgumentNullException(nameof(policyId));
            if (request == null) request = new EnumerationRequest();

            List<string> conditions = new List<string>
            {
                "tenantid = '" + _Driver.Sanitize(tenantId) + "'",
                "policyid = '" + _Driver.Sanitize(policyId) + "'"
            };
            if (!String.IsNullOrEmpty(request.NameFilter)) conditions.Add("name LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");
            if (request.ActiveFilter.HasValue) conditions.Add("active = " + _Driver.FormatBoolean(request.ActiveFilter.Value));
            string whereClause = "WHERE " + String.Join(" AND ", conditions);

            string orderBy = GetOrderBy(request.Order);
            int offset = ParseOffset(request.ContinuationToken);

            string countQuery = "SELECT COUNT(*) AS cnt FROM modelaccessrules " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0) totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            string query = BuildPagedQuery("modelaccessrules", whereClause, orderBy, request.MaxResults + 1, offset);
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<ModelAccessRule> data = ModelAccessRule.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore) data.RemoveAt(data.Count - 1);

            return new EnumerationResult<ModelAccessRule>
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
