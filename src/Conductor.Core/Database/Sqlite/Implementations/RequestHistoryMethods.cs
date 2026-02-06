namespace Conductor.Core.Database.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Models;

    /// <summary>
    /// SQLite request history methods implementation.
    /// </summary>
    public class RequestHistoryMethods : IRequestHistoryMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the request history methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
        public RequestHistoryMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a request history entry.
        /// </summary>
        public async Task<RequestHistoryEntry> CreateAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query = "INSERT INTO requesthistory (id, tenantguid, virtualmodelrunnerguid, virtualmodelrunnername, " +
                           "modelendpointguid, modelendpointname, modelendpointurl, modeldefinitionguid, modeldefinitionname, " +
                           "modelconfigurationguid, requestorsourceip, httpmethod, httpurl, requestbodylength, responsebodylength, " +
                           "httpstatus, responsetimems, objectkey, createdutc, completedutc) " +
                           "VALUES ('" + _Driver.Sanitize(entry.Id) + "', " +
                           "'" + _Driver.Sanitize(entry.TenantGuid) + "', " +
                           "'" + _Driver.Sanitize(entry.VirtualModelRunnerGuid) + "', " +
                           "'" + _Driver.Sanitize(entry.VirtualModelRunnerName) + "', " +
                           _Driver.FormatNullableString(entry.ModelEndpointGuid) + ", " +
                           _Driver.FormatNullableString(entry.ModelEndpointName) + ", " +
                           _Driver.FormatNullableString(entry.ModelEndpointUrl) + ", " +
                           _Driver.FormatNullableString(entry.ModelDefinitionGuid) + ", " +
                           _Driver.FormatNullableString(entry.ModelDefinitionName) + ", " +
                           _Driver.FormatNullableString(entry.ModelConfigurationGuid) + ", " +
                           "'" + _Driver.Sanitize(entry.RequestorSourceIp) + "', " +
                           "'" + _Driver.Sanitize(entry.HttpMethod) + "', " +
                           "'" + _Driver.Sanitize(entry.HttpUrl) + "', " +
                           entry.RequestBodyLength + ", " +
                           _Driver.FormatNullable(entry.ResponseBodyLength) + ", " +
                           _Driver.FormatNullable(entry.HttpStatus) + ", " +
                           _Driver.FormatNullable(entry.ResponseTimeMs) + ", " +
                           "'" + _Driver.Sanitize(entry.ObjectKey) + "', " +
                           "'" + _Driver.FormatDateTime(entry.CreatedUtc) + "', " +
                           (entry.CompletedUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.CompletedUtc.Value) + "'" : "NULL") + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return entry;
        }

        /// <summary>
        /// Update a request history entry with response data.
        /// </summary>
        public async Task<RequestHistoryEntry> UpdateAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query = "UPDATE requesthistory SET " +
                           "modelendpointguid = " + _Driver.FormatNullableString(entry.ModelEndpointGuid) + ", " +
                           "modelendpointname = " + _Driver.FormatNullableString(entry.ModelEndpointName) + ", " +
                           "modelendpointurl = " + _Driver.FormatNullableString(entry.ModelEndpointUrl) + ", " +
                           "modeldefinitionguid = " + _Driver.FormatNullableString(entry.ModelDefinitionGuid) + ", " +
                           "modeldefinitionname = " + _Driver.FormatNullableString(entry.ModelDefinitionName) + ", " +
                           "modelconfigurationguid = " + _Driver.FormatNullableString(entry.ModelConfigurationGuid) + ", " +
                           "responsebodylength = " + _Driver.FormatNullable(entry.ResponseBodyLength) + ", " +
                           "httpstatus = " + _Driver.FormatNullable(entry.HttpStatus) + ", " +
                           "responsetimems = " + _Driver.FormatNullable(entry.ResponseTimeMs) + ", " +
                           "completedutc = " + (entry.CompletedUtc.HasValue ? "'" + _Driver.FormatDateTime(entry.CompletedUtc.Value) + "'" : "NULL") + " " +
                           "WHERE id = '" + _Driver.Sanitize(entry.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return entry;
        }

        /// <summary>
        /// Read a request history entry by ID.
        /// </summary>
        public async Task<RequestHistoryEntry> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM requesthistory WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return RequestHistoryEntry.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Search request history entries with pagination.
        /// </summary>
        public async Task<RequestHistorySearchResult> SearchAsync(RequestHistorySearchFilter filter, CancellationToken token = default)
        {
            if (filter == null) filter = new RequestHistorySearchFilter();

            string whereClause = BuildWhereClause(filter);
            int offset = (filter.Page - 1) * filter.PageSize;

            // Count query
            string countQuery = "SELECT COUNT(*) AS cnt FROM requesthistory " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            // Data query with pagination
            string query = "SELECT * FROM requesthistory " + whereClause +
                           " ORDER BY createdutc DESC LIMIT " + filter.PageSize + " OFFSET " + offset + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<RequestHistoryEntry> data = RequestHistoryEntry.FromDataTable(result) ?? new List<RequestHistoryEntry>();

            return new RequestHistorySearchResult
            {
                Data = data,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = totalCount
            };
        }

        /// <summary>
        /// Count request history entries matching the filter.
        /// </summary>
        public async Task<long> CountAsync(RequestHistorySearchFilter filter, CancellationToken token = default)
        {
            if (filter == null) filter = new RequestHistorySearchFilter();

            string whereClause = BuildWhereClause(filter);
            string query = "SELECT COUNT(*) AS cnt FROM requesthistory " + whereClause + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return 0;
            return Convert.ToInt64(result.Rows[0]["cnt"]);
        }

        /// <summary>
        /// Delete a request history entry by ID.
        /// </summary>
        public async Task DeleteByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM requesthistory WHERE id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete request history entries matching the filter.
        /// </summary>
        public async Task<long> DeleteBulkAsync(RequestHistorySearchFilter filter, CancellationToken token = default)
        {
            if (filter == null) filter = new RequestHistorySearchFilter();

            // First count how many will be deleted
            long count = await CountAsync(filter, token).ConfigureAwait(false);
            if (count == 0) return 0;

            string whereClause = BuildWhereClause(filter);
            string query = "DELETE FROM requesthistory " + whereClause + ";";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            return count;
        }

        /// <summary>
        /// Delete request history entries older than the specified cutoff.
        /// </summary>
        public async Task<long> DeleteExpiredAsync(DateTime cutoff, CancellationToken token = default)
        {
            string cutoffStr = _Driver.FormatDateTime(cutoff);

            // First count
            string countQuery = "SELECT COUNT(*) AS cnt FROM requesthistory WHERE createdutc < '" + cutoffStr + "';";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long count = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                count = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            if (count == 0) return 0;

            string query = "DELETE FROM requesthistory WHERE createdutc < '" + cutoffStr + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            return count;
        }

        /// <summary>
        /// Get object keys for expired request history entries.
        /// </summary>
        public async Task<List<string>> GetExpiredObjectKeysAsync(DateTime cutoff, CancellationToken token = default)
        {
            string cutoffStr = _Driver.FormatDateTime(cutoff);

            string query = "SELECT objectkey FROM requesthistory WHERE createdutc < '" + cutoffStr + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<string> keys = new List<string>();
            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    string key = row["objectkey"]?.ToString();
                    if (!String.IsNullOrEmpty(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            return keys;
        }

        private string BuildWhereClause(RequestHistorySearchFilter filter)
        {
            List<string> conditions = new List<string>();

            if (!String.IsNullOrEmpty(filter.TenantGuid))
            {
                conditions.Add("tenantguid = '" + _Driver.Sanitize(filter.TenantGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.VirtualModelRunnerGuid))
            {
                conditions.Add("virtualmodelrunnerguid = '" + _Driver.Sanitize(filter.VirtualModelRunnerGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.ModelEndpointGuid))
            {
                conditions.Add("modelendpointguid = '" + _Driver.Sanitize(filter.ModelEndpointGuid) + "'");
            }
            if (!String.IsNullOrEmpty(filter.RequestorSourceIp))
            {
                conditions.Add("requestorsourceip = '" + _Driver.Sanitize(filter.RequestorSourceIp) + "'");
            }
            if (filter.HttpStatus.HasValue)
            {
                conditions.Add("httpstatus = " + filter.HttpStatus.Value);
            }

            return conditions.Count > 0 ? "WHERE " + String.Join(" AND ", conditions) : "";
        }
    }
}
