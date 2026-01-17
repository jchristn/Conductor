namespace Conductor.Core.Database.MySql.Implementations
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Interfaces;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// MySQL administrator methods implementation.
    /// </summary>
    public class AdministratorMethods : IAdministratorMethods
    {
        private readonly MySqlDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the administrator methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        public AdministratorMethods(MySqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create an administrator.
        /// </summary>
        public async Task<Administrator> CreateAsync(Administrator admin, CancellationToken token = default)
        {
            if (admin == null) throw new ArgumentNullException(nameof(admin));

            admin.CreatedUtc = DateTime.UtcNow;
            admin.LastUpdateUtc = DateTime.UtcNow;

            string query = "INSERT INTO administrators (id, email, passwordsha256, firstname, lastname, active, createdutc, lastupdateutc) " +
                           "VALUES ('" + _Driver.Sanitize(admin.Id) + "', " +
                           "'" + _Driver.Sanitize(admin.Email) + "', " +
                           "'" + _Driver.Sanitize(admin.PasswordSha256) + "', " +
                           _Driver.FormatNullableString(admin.FirstName) + ", " +
                           _Driver.FormatNullableString(admin.LastName) + ", " +
                           _Driver.FormatBoolean(admin.Active) + ", " +
                           "'" + _Driver.FormatDateTime(admin.CreatedUtc) + "', " +
                           "'" + _Driver.FormatDateTime(admin.LastUpdateUtc) + "');";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return admin;
        }

        /// <summary>
        /// Read an administrator by ID.
        /// </summary>
        public async Task<Administrator> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT * FROM administrators WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return Administrator.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Read an administrator by email.
        /// </summary>
        public async Task<Administrator> ReadByEmailAsync(string email, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));

            string query = "SELECT * FROM administrators WHERE email = '" + _Driver.Sanitize(email.ToLowerInvariant()) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return null;
            return Administrator.FromDataRow(result.Rows[0]);
        }

        /// <summary>
        /// Update an administrator.
        /// </summary>
        public async Task<Administrator> UpdateAsync(Administrator admin, CancellationToken token = default)
        {
            if (admin == null) throw new ArgumentNullException(nameof(admin));

            admin.LastUpdateUtc = DateTime.UtcNow;

            string query = "UPDATE administrators SET " +
                           "email = '" + _Driver.Sanitize(admin.Email) + "', " +
                           "passwordsha256 = '" + _Driver.Sanitize(admin.PasswordSha256) + "', " +
                           "firstname = " + _Driver.FormatNullableString(admin.FirstName) + ", " +
                           "lastname = " + _Driver.FormatNullableString(admin.LastName) + ", " +
                           "active = " + _Driver.FormatBoolean(admin.Active) + ", " +
                           "lastupdateutc = '" + _Driver.FormatDateTime(admin.LastUpdateUtc) + "' " +
                           "WHERE id = '" + _Driver.Sanitize(admin.Id) + "';";

            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return admin;
        }

        /// <summary>
        /// Delete an administrator by ID.
        /// </summary>
        public async Task DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "DELETE FROM administrators WHERE id = '" + _Driver.Sanitize(id) + "';";
            await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if an administrator exists by ID.
        /// </summary>
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string query = "SELECT COUNT(*) AS cnt FROM administrators WHERE id = '" + _Driver.Sanitize(id) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Check if an administrator exists by email.
        /// </summary>
        public async Task<bool> ExistsByEmailAsync(string email, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));

            string query = "SELECT COUNT(*) AS cnt FROM administrators WHERE email = '" + _Driver.Sanitize(email.ToLowerInvariant()) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count < 1) return false;
            return Convert.ToInt64(result.Rows[0]["cnt"]) > 0;
        }

        /// <summary>
        /// Enumerate administrators.
        /// </summary>
        public async Task<EnumerationResult<Administrator>> EnumerateAsync(EnumerationRequest request, CancellationToken token = default)
        {
            if (request == null) request = new EnumerationRequest();

            string whereClause = "WHERE 1=1";
            if (request.ActiveFilter.HasValue)
            {
                whereClause += " AND active = " + _Driver.FormatBoolean(request.ActiveFilter.Value);
            }

            string orderBy = GetOrderBy(request.Order);
            int offset = 0;
            if (!String.IsNullOrEmpty(request.ContinuationToken))
            {
                Int32.TryParse(request.ContinuationToken, out offset);
            }

            string countQuery = "SELECT COUNT(*) AS cnt FROM administrators " + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            long totalCount = 0;
            if (countResult != null && countResult.Rows.Count > 0)
            {
                totalCount = Convert.ToInt64(countResult.Rows[0]["cnt"]);
            }

            string query = "SELECT * FROM administrators " + whereClause + " " + orderBy +
                           " LIMIT " + (request.MaxResults + 1) + " OFFSET " + offset + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            System.Collections.Generic.List<Administrator> data = Administrator.FromDataTable(result);
            bool hasMore = data.Count > request.MaxResults;
            if (hasMore)
            {
                data.RemoveAt(data.Count - 1);
            }

            return new EnumerationResult<Administrator>
            {
                Data = data,
                TotalCount = totalCount,
                HasMore = hasMore,
                ContinuationToken = hasMore ? (offset + request.MaxResults).ToString() : null
            };
        }

        /// <summary>
        /// Get the ORDER BY clause for the given order.
        /// </summary>
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
