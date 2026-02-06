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
    /// SQLite user methods implementation.
    /// </summary>
    public class UserMethods : IUserMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the user methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        public UserMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a user.
        /// </summary>
        public async Task<UserMaster> CreateAsync(UserMaster user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            user.CreatedUtc = DateTime.UtcNow;
            user.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO users (id, tenantid, firstname, lastname, email, password, active, isadmin, istenantadmin, createdutc, lastupdateutc, labels, tags, metadata) " +
                           "VALUES ('" + _Driver.Sanitize(user.Id) + "', " +
                           "'" + _Driver.Sanitize(user.TenantId) + "', " +
                           "'" + _Driver.Sanitize(user.FirstName) + "', " +
                           "'" + _Driver.Sanitize(user.LastName) + "', " +
                           "'" + _Driver.Sanitize(user.Email) + "', " +
                           "'" + _Driver.Sanitize(user.Password) + "', " +
                           _Driver.FormatBoolean(user.Active) + ", " +
                           _Driver.FormatBoolean(user.IsAdmin) + ", " +
                           _Driver.FormatBoolean(user.IsTenantAdmin) + ", " +
                           "'" + _Driver.FormatDateTime(user.CreatedUtc) + "', " +
                           "'" + _Driver.FormatDateTime(user.LastUpdateUtc) + "', " +
                           _Driver.FormatNullableString(user.LabelsJson) + ", " +
                           _Driver.FormatNullableString(user.TagsJson) + ", " +
                           _Driver.FormatNullableString(user.MetadataJson) + ");";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return user;
        }

        /// <summary>
        /// Read a user by ID.
        /// </summary>
        public async Task<UserMaster> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM users WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return UserMaster.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read a user by ID without tenant filtering (admin use only).
        /// </summary>
        public async Task<UserMaster> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM users WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return UserMaster.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read a user by email.
        /// </summary>
        public async Task<UserMaster> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));

            string query = "SELECT * FROM users WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND email = '" + _Driver.Sanitize(email) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return UserMaster.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update a user.
        /// </summary>
        public async Task<UserMaster> UpdateAsync(UserMaster user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            user.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE users SET " +
                           "firstname = '" + _Driver.Sanitize(user.FirstName) + "', " +
                           "lastname = '" + _Driver.Sanitize(user.LastName) + "', " +
                           "email = '" + _Driver.Sanitize(user.Email) + "', " +
                           "password = '" + _Driver.Sanitize(user.Password) + "', " +
                           "active = " + _Driver.FormatBoolean(user.Active) + ", " +
                           "isadmin = " + _Driver.FormatBoolean(user.IsAdmin) + ", " +
                           "istenantadmin = " + _Driver.FormatBoolean(user.IsTenantAdmin) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(user.LastUpdateUtc) + "', " +
                           "labels = " + _Driver.FormatNullableString(user.LabelsJson) + ", " +
                           "tags = " + _Driver.FormatNullableString(user.TagsJson) + ", " +
                           "metadata = " + _Driver.FormatNullableString(user.MetadataJson) + " " +
                           "WHERE tenantid = '" + _Driver.Sanitize(user.TenantId) + "' AND id = '" + _Driver.Sanitize(user.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return user;
        }

        /// <summary>
        /// Delete a user by ID.
        /// </summary>
        public async Task DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM users WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if a user exists by ID.
        /// </summary>
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM users WHERE tenantid = '" + _Driver.Sanitize(tenantId) + "' AND id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt32(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate users.
        /// </summary>
        public async Task<EnumerationResult<UserMaster>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            // If tenantId is null/empty, return all users (admin access)
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

            string countQuery = "SELECT COUNT(*) AS cnt FROM users " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            string query = "SELECT * FROM users " + whereClause + " " + orderBy +
                           " LIMIT " + (request.MaxResults + 1) + " OFFSET " + offset + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            System.Collections.Generic.List<UserMaster> data = UserMaster.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore)
            {
                data.RemoveAt(data.Count - 1);
            }

            return new EnumerationResult<UserMaster>
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
                    return "ORDER BY email ASC";
                case EnumerationOrderEnum.NameDescending:
                    return "ORDER BY email DESC";
                default:
                    return "ORDER BY createdutc DESC";
            }
        }
    }
}
