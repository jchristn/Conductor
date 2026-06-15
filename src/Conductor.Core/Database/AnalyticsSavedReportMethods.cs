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
    /// Provider-neutral analytics saved report database implementation.
    /// </summary>
    public class AnalyticsSavedReportMethods : IAnalyticsSavedReportMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly RequestAnalyticsSqlDialect _Dialect;

        /// <summary>
        /// Instantiate analytics saved report methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <param name="dialect">SQL dialect.</param>
        public AnalyticsSavedReportMethods(DatabaseDriverBase driver, RequestAnalyticsSqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Dialect = dialect;
        }

        /// <inheritdoc />
        public async Task<AnalyticsSavedReport> CreateAsync(AnalyticsSavedReport report, CancellationToken token = default)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            report.CreatedUtc = DateTime.UtcNow;
            report.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO analyticssavedreports (id, tenantid, owneruserid, name, description, scope, queryjson, displaystatejson, labels, tags, createdutc, lastupdateutc) VALUES ('" +
                           _Driver.Sanitize(report.Id) + "', " +
                           _Driver.FormatNullableString(report.TenantId) + ", " +
                           _Driver.FormatNullableString(report.OwnerUserId) + ", '" +
                           _Driver.Sanitize(report.Name) + "', " +
                           _Driver.FormatNullableString(report.Description) + ", '" +
                           _Driver.Sanitize(report.Scope) + "', " +
                           _Driver.FormatNullableString(report.QueryJson) + ", " +
                           _Driver.FormatNullableString(report.DisplayStateJson) + ", " +
                           _Driver.FormatNullableString(report.LabelsJson) + ", " +
                           _Driver.FormatNullableString(report.TagsJson) + ", '" +
                           _Driver.FormatDateTime(report.CreatedUtc) + "', '" +
                           _Driver.FormatDateTime(report.LastUpdateUtc) + "');";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return report;
        }

        /// <inheritdoc />
        public async Task<AnalyticsSavedReport> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM analyticssavedreports WHERE " + BuildTenantCondition(tenantId) + " AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return AnalyticsSavedReport.FromDataRow(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<AnalyticsSavedReport> UpdateAsync(AnalyticsSavedReport report, CancellationToken token = default)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            report.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE analyticssavedreports SET " +
                           "owneruserid = " + _Driver.FormatNullableString(report.OwnerUserId) + ", " +
                           "name = '" + _Driver.Sanitize(report.Name) + "', " +
                           "description = " + _Driver.FormatNullableString(report.Description) + ", " +
                           "scope = '" + _Driver.Sanitize(report.Scope) + "', " +
                           "queryjson = " + _Driver.FormatNullableString(report.QueryJson) + ", " +
                           "displaystatejson = " + _Driver.FormatNullableString(report.DisplayStateJson) + ", " +
                           "labels = " + _Driver.FormatNullableString(report.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(report.TagsJson) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(report.LastUpdateUtc) + "' " +
                           "WHERE " + BuildTenantCondition(report.TenantId) + " AND id = '" + _Driver.Sanitize(report.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return report;
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM analyticssavedreports WHERE " + BuildTenantCondition(tenantId) + " AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM analyticssavedreports WHERE " + BuildTenantCondition(tenantId) + " AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<AnalyticsSavedReport>> EnumerateAsync(string tenantId, EnumerationRequest request, string ownerUserId = null, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            List<string> conditions = new List<string>
            {
                BuildTenantCondition(tenantId)
            };

            if (!String.IsNullOrEmpty(ownerUserId)) conditions.Add("owneruserid = '" + _Driver.Sanitize(ownerUserId) + "'");
            if (!String.IsNullOrEmpty(request.NameFilter)) conditions.Add("name LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");
            string whereClause = "WHERE " + String.Join(" AND ", conditions);

            string orderBy = GetOrderBy(request.Order);
            int offset = ParseOffset(request.ContinuationToken);

            string countQuery = "SELECT COUNT(*) AS cnt FROM analyticssavedreports " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0) totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            string query = BuildPagedQuery("analyticssavedreports", whereClause, orderBy, request.MaxResults + 1, offset);
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<AnalyticsSavedReport> data = AnalyticsSavedReport.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore) data.RemoveAt(data.Count - 1);

            return new EnumerationResult<AnalyticsSavedReport>
            {
                Data = data,
                TotalCount = totalCount,
                HasMore = hasMore,
                ContinuationToken = hasMore ? (offset + request.MaxResults).ToString() : null
            };
        }

        private string BuildTenantCondition(string tenantId)
        {
            return String.IsNullOrEmpty(tenantId)
                ? "tenantid IS NULL"
                : "tenantid = '" + _Driver.Sanitize(tenantId) + "'";
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
