namespace Conductor.Core.Database.SqlServer.Implementations
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
    /// SQL Server QoS traffic class methods implementation.
    /// </summary>
    public class QosTrafficClassMethods : IQosTrafficClassMethods
    {
        private readonly SqlServerDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the SQL Server QoS traffic class methods implementation.
        /// </summary>
        /// <param name="driver">Database driver. Must not be null.</param>
        public QosTrafficClassMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<QosTrafficClass> CreateAsync(QosTrafficClass trafficClass, CancellationToken token = default)
        {
            if (trafficClass == null) throw new ArgumentNullException(nameof(trafficClass));

            trafficClass.CreatedUtc = DateTime.UtcNow;
            trafficClass.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO qostrafficclasses (id, tenantid, name, description, tier, issystem, createdutc, lastupdateutc) VALUES ('" +
                           _Driver.Sanitize(trafficClass.Id) + "', '" +
                           _Driver.Sanitize(trafficClass.TenantId) + "', '" +
                           _Driver.Sanitize(trafficClass.Name) + "', " +
                           _Driver.FormatNullableString(trafficClass.Description) + ", " +
                           (int)trafficClass.Tier + ", " +
                           _Driver.FormatBoolean(trafficClass.IsSystem) + ", '" +
                           _Driver.FormatDateTime(trafficClass.CreatedUtc) + "', '" +
                           _Driver.FormatDateTime(trafficClass.LastUpdateUtc) + "');";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return trafficClass;
        }

        /// <inheritdoc />
        public async Task<QosTrafficClass> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM qostrafficclasses WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return QosTrafficClass.FromDataRow(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<QosTrafficClass> ReadByNameAsync(string tenantId, string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            string query = "SELECT TOP 1 * FROM qostrafficclasses WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND name = '" + _Driver.Sanitize(name) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return null;
            return QosTrafficClass.FromDataRow(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<QosTrafficClass> UpdateAsync(QosTrafficClass trafficClass, CancellationToken token = default)
        {
            if (trafficClass == null) throw new ArgumentNullException(nameof(trafficClass));

            trafficClass.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE qostrafficclasses SET " +
                           "name = '" + _Driver.Sanitize(trafficClass.Name) + "', " +
                           "description = " + _Driver.FormatNullableString(trafficClass.Description) + ", " +
                           "tier = " + (int)trafficClass.Tier + ", " +
                           "issystem = " + _Driver.FormatBoolean(trafficClass.IsSystem) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(trafficClass.LastUpdateUtc) + "' " +
                           "WHERE tenantid = '" + _Driver.Sanitize(trafficClass.TenantId) + "' AND id = '" + _Driver.Sanitize(trafficClass.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return trafficClass;
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM qostrafficclasses WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM qostrafficclasses WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt32(result.Rows[0]["cnt"]) > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<QosTrafficClass>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            List<string> conditions = new List<string>();
            if (!String.IsNullOrEmpty(tenantId)) conditions.Add("tenantid = '" + _Driver.Sanitize(tenantId) + "'");
            if (!String.IsNullOrEmpty(request.NameFilter)) conditions.Add("name LIKE '%" + _Driver.Sanitize(request.NameFilter) + "%'");
            string whereClause = conditions.Count > 0 ? "WHERE " + String.Join(" AND ", conditions) : "";

            int offset = 0;
            if (!String.IsNullOrEmpty(request.ContinuationToken)) Int32.TryParse(request.ContinuationToken, out offset);

            string countQuery = "SELECT COUNT(*) AS cnt FROM qostrafficclasses " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0) totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);

            string query = "SELECT * FROM qostrafficclasses " + whereClause + " ORDER BY name ASC" +
                           " OFFSET " + offset + " ROWS FETCH NEXT " + (request.MaxResults + 1) + " ROWS ONLY;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<QosTrafficClass> data = QosTrafficClass.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore) data.RemoveAt(data.Count - 1);

            return new EnumerationResult<QosTrafficClass>
            {
                Data = data,
                TotalCount = totalCount,
                HasMore = hasMore,
                ContinuationToken = hasMore ? (offset + request.MaxResults).ToString() : null
            };
        }
    }
}
