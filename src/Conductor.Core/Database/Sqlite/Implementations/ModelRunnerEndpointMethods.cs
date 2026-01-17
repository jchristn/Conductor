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
    /// SQLite model runner endpoint methods implementation.
    /// </summary>
    public class ModelRunnerEndpointMethods : IModelRunnerEndpointMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the model runner endpoint methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        public ModelRunnerEndpointMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a model runner endpoint.
        /// </summary>
        public async Task<ModelRunnerEndpoint> CreateAsync(ModelRunnerEndpoint endpoint, CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            endpoint.CreatedUtc = DateTime.UtcNow;
            endpoint.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO modelrunnerendpoints (id, tenantid, name, hostname, port, apikey, apitype, usessl, timeoutms, active, " +
                           "healthcheckurl, healthcheckmethod, healthcheckintervalms, healthchecktimeoutms, healthcheckexpectedstatuscode, " +
                           "unhealthythreshold, healthythreshold, healthcheckuseauth, maxparallelrequests, weight, " +
                           "createdutc, lastupdateutc, labels, tags, metadata) " +
                           "VALUES ('" + _Driver.Sanitize(endpoint.Id) + "', " +
                           "'" + _Driver.Sanitize(endpoint.TenantId) + "', " +
                           "'" + _Driver.Sanitize(endpoint.Name) + "', " +
                           "'" + _Driver.Sanitize(endpoint.Hostname) + "', " +
                           endpoint.Port + ", " +
                           _Driver.FormatNullableString(endpoint.ApiKey) + ", " +
                           (int)endpoint.ApiType + ", " +
                           _Driver.FormatBoolean(endpoint.UseSsl) + ", " +
                           endpoint.TimeoutMs + ", " +
                           _Driver.FormatBoolean(endpoint.Active) + ", " +
                           "'" + _Driver.Sanitize(endpoint.HealthCheckUrl) + "', " +
                           (int)endpoint.HealthCheckMethod + ", " +
                           endpoint.HealthCheckIntervalMs + ", " +
                           endpoint.HealthCheckTimeoutMs + ", " +
                           endpoint.HealthCheckExpectedStatusCode + ", " +
                           endpoint.UnhealthyThreshold + ", " +
                           endpoint.HealthyThreshold + ", " +
                           _Driver.FormatBoolean(endpoint.HealthCheckUseAuth) + ", " +
                           endpoint.MaxParallelRequests + ", " +
                           endpoint.Weight + ", " +
                           "'" + _Driver.FormatDateTime(endpoint.CreatedUtc) + "', " +
                           "'" + _Driver.FormatDateTime(endpoint.LastUpdateUtc) + "', " +
                           _Driver.FormatNullableString(endpoint.LabelsJson) + ", " +
                           _Driver.FormatNullableString(endpoint.TagsJson) + ", " +
                           _Driver.FormatNullableString(endpoint.MetadataJson) + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return endpoint;
        }

        /// <summary>
        /// Read a model runner endpoint by ID.
        /// </summary>
        public async Task<ModelRunnerEndpoint> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM modelrunnerendpoints WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return ModelRunnerEndpoint.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update a model runner endpoint.
        /// </summary>
        public async Task<ModelRunnerEndpoint> UpdateAsync(ModelRunnerEndpoint endpoint, CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            endpoint.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE modelrunnerendpoints SET " +
                           "name = '" + _Driver.Sanitize(endpoint.Name) + "', " +
                           "hostname = '" + _Driver.Sanitize(endpoint.Hostname) + "', " +
                           "port = " + endpoint.Port + ", " +
                           "apikey = " + _Driver.FormatNullableString(endpoint.ApiKey) + ", " +
                           "apitype = " + (int)endpoint.ApiType + ", " +
                           "usessl = " + _Driver.FormatBoolean(endpoint.UseSsl) + ", " +
                           "timeoutms = " + endpoint.TimeoutMs + ", " +
                           "active = " + _Driver.FormatBoolean(endpoint.Active) + ", " +
                           "healthcheckurl = '" + _Driver.Sanitize(endpoint.HealthCheckUrl) + "', " +
                           "healthcheckmethod = " + (int)endpoint.HealthCheckMethod + ", " +
                           "healthcheckintervalms = " + endpoint.HealthCheckIntervalMs + ", " +
                           "healthchecktimeoutms = " + endpoint.HealthCheckTimeoutMs + ", " +
                           "healthcheckexpectedstatuscode = " + endpoint.HealthCheckExpectedStatusCode + ", " +
                           "unhealthythreshold = " + endpoint.UnhealthyThreshold + ", " +
                           "healthythreshold = " + endpoint.HealthyThreshold + ", " +
                           "healthcheckuseauth = " + _Driver.FormatBoolean(endpoint.HealthCheckUseAuth) + ", " +
                           "maxparallelrequests = " + endpoint.MaxParallelRequests + ", " +
                           "weight = " + endpoint.Weight + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(endpoint.LastUpdateUtc) + "', " +
                           "labels = " + _Driver.FormatNullableString(endpoint.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(endpoint.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(endpoint.MetadataJson) + " " +
                           "WHERE tenantid = '" + _Driver.Sanitize(endpoint.TenantId) + "' AND id = '" + _Driver.Sanitize(endpoint.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return endpoint;
        }

        /// <summary>
        /// Delete a model runner endpoint by ID.
        /// </summary>
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM modelrunnerendpoints WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a model runner endpoint exists by ID.
        /// </summary>
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM modelrunnerendpoints WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt32(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate model runner endpoints.
        /// </summary>
        public async Task<EnumerationResult<ModelRunnerEndpoint>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            // If tenantId is null/empty, return all endpoints (admin access)
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

            string countQuery = "SELECT COUNT(*) AS cnt FROM modelrunnerendpoints " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            string query = "SELECT * FROM modelrunnerendpoints " + whereClause + " " + orderBy +
                           " LIMIT " + (request.MaxResults + 1) + " OFFSET " + offset + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            System.Collections.Generic.List<ModelRunnerEndpoint> data = ModelRunnerEndpoint.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore)
            {
                data.RemoveAt(data.Count - 1);
            }

            return new EnumerationResult<ModelRunnerEndpoint>
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
