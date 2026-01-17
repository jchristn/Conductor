namespace Conductor.Core.Database.MySql.Implementations
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
    /// MySQL virtual model runner methods implementation.
    /// </summary>
    public class VirtualModelRunnerMethods : IVirtualModelRunnerMethods
    {
        private readonly MySqlDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the virtual model runner methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        public VirtualModelRunnerMethods(MySqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a virtual model runner.
        /// </summary>
        public async Task<VirtualModelRunner> CreateAsync(VirtualModelRunner vmr, CancellationToken token = default)
        {
            if (vmr == null) throw new ArgumentNullException(nameof(vmr));

            vmr.CreatedUtc = DateTime.UtcNow;
            vmr.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO virtualmodelrunners (id, tenantid, name, hostname, basepath, apitype, loadbalancingmode, modelrunnerendpointids, modelconfigurationids, modeldefinitionids, timeoutms, allowembeddings, allowcompletions, allowmodelmanagement, strictmode, active, createdutc, lastupdateutc, labels, tags, metadata) " +
                           "VALUES ('" + _Driver.Sanitize(vmr.Id) + "', " +
                           "'" + _Driver.Sanitize(vmr.TenantId) + "', " +
                           "'" + _Driver.Sanitize(vmr.Name) + "', " +
                           _Driver.FormatNullableString(vmr.Hostname) + ", " +
                           "'" + _Driver.Sanitize(vmr.BasePath) + "', " +
                           (int)vmr.ApiType + ", " +
                           (int)vmr.LoadBalancingMode + ", " +
                           _Driver.FormatNullableString(vmr.ModelRunnerEndpointIdsJson) + ", " +
                           _Driver.FormatNullableString(vmr.ModelConfigurationIdsJson) + ", " +
                           _Driver.FormatNullableString(vmr.ModelDefinitionIdsJson) + ", " +
                           vmr.TimeoutMs + ", " +
                           _Driver.FormatBoolean(vmr.AllowEmbeddings) + ", " +
                           _Driver.FormatBoolean(vmr.AllowCompletions) + ", " +
                           _Driver.FormatBoolean(vmr.AllowModelManagement) + ", " +
                           _Driver.FormatBoolean(vmr.StrictMode) + ", " +
                           _Driver.FormatBoolean(vmr.Active) + ", " +
                           "'" + _Driver.FormatDateTime(vmr.CreatedUtc) + "', " +
                           "'" + _Driver.FormatDateTime(vmr.LastUpdateUtc) + "', " +
                           _Driver.FormatNullableString(vmr.LabelsJson) + ", " +
                           _Driver.FormatNullableString(vmr.TagsJson) + ", " +
                           _Driver.FormatNullableString(vmr.MetadataJson) + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return vmr;
        }

        /// <summary>
        /// Read a virtual model runner by ID.
        /// </summary>
        public async Task<VirtualModelRunner> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM virtualmodelrunners WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return VirtualModelRunner.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read a virtual model runner by base path.
        /// </summary>
        public async Task<VirtualModelRunner> ReadByBasePathAsync(string basePath, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(basePath)) throw new ArgumentNullException(nameof(basePath));

            string query = "SELECT * FROM virtualmodelrunners WHERE basepath = '" + _Driver.Sanitize(basePath) + "' AND active = 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return VirtualModelRunner.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update a virtual model runner.
        /// </summary>
        public async Task<VirtualModelRunner> UpdateAsync(VirtualModelRunner vmr, CancellationToken token = default)
        {
            if (vmr == null) throw new ArgumentNullException(nameof(vmr));

            vmr.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE virtualmodelrunners SET " +
                           "name = '" + _Driver.Sanitize(vmr.Name) + "', " +
                           "hostname = " + _Driver.FormatNullableString(vmr.Hostname) + ", " +
                           "basepath = '" + _Driver.Sanitize(vmr.BasePath) + "', " +
                           "apitype = " + (int)vmr.ApiType + ", " +
                           "loadbalancingmode = " + (int)vmr.LoadBalancingMode + ", " +
                           "modelrunnerendpointids = " + _Driver.FormatNullableString(vmr.ModelRunnerEndpointIdsJson) + ", " +
                           "modelconfigurationids = " + _Driver.FormatNullableString(vmr.ModelConfigurationIdsJson) + ", " +
                           "modeldefinitionids = " + _Driver.FormatNullableString(vmr.ModelDefinitionIdsJson) + ", " +
                           "timeoutms = " + vmr.TimeoutMs + ", " +
                           "allowembeddings = " + _Driver.FormatBoolean(vmr.AllowEmbeddings) + ", " +
                           "allowcompletions = " + _Driver.FormatBoolean(vmr.AllowCompletions) + ", " +
                           "allowmodelmanagement = " + _Driver.FormatBoolean(vmr.AllowModelManagement) + ", " +
                           "strictmode = " + _Driver.FormatBoolean(vmr.StrictMode) + ", " +
                           "active = " + _Driver.FormatBoolean(vmr.Active) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(vmr.LastUpdateUtc) + "', " +
                           "labels = " + _Driver.FormatNullableString(vmr.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(vmr.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(vmr.MetadataJson) + " " +
                           "WHERE tenantid = '" + _Driver.Sanitize(vmr.TenantId) + "' AND id = '" + _Driver.Sanitize(vmr.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return vmr;
        }

        /// <summary>
        /// Delete a virtual model runner by ID.
        /// </summary>
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM virtualmodelrunners WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a virtual model runner exists by ID.
        /// </summary>
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM virtualmodelrunners WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt32(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate virtual model runners.
        /// </summary>
        public async Task<EnumerationResult<VirtualModelRunner>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            // If tenantId is null/empty, return all VMRs (admin access)
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

            string countQuery = "SELECT COUNT(*) AS cnt FROM virtualmodelrunners " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            string query = "SELECT * FROM virtualmodelrunners " + whereClause + " " + orderBy +
                           " LIMIT " + (request.MaxResults + 1) + " OFFSET " + offset + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<VirtualModelRunner> data = VirtualModelRunner.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore)
            {
                data.RemoveAt(data.Count - 1);
            }

            return new EnumerationResult<VirtualModelRunner>
            {
                Data = data,
                TotalCount = totalCount,
                HasMore = hasMore,
                ContinuationToken = hasMore ? (offset + request.MaxResults).ToString() : null
            };
        }

        /// <summary>
        /// Get all active virtual model runners.
        /// </summary>
        public async Task<List<VirtualModelRunner>> GetAllActiveAsync(CancellationToken token = default)
        {
            string query = "SELECT * FROM virtualmodelrunners WHERE active = 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            return VirtualModelRunner.FromDataTable(result);
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
