namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Security.Cryptography;
    using System.Text;
    using Conductor.Core.Helpers;

    /// <summary>
    /// Represents a global administrator with cross-tenant access.
    /// </summary>
    public class Administrator
    {
        /// <summary>
        /// Unique identifier for the administrator (admin_{prettyid}).
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewAdministratorId();

        /// <summary>
        /// Email address for the administrator (unique, stored lowercase).
        /// </summary>
        public string Email
        {
            get => _Email;
            set => _Email = String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Email)) : value.ToLowerInvariant();
        }

        /// <summary>
        /// SHA256 hash of the administrator's password.
        /// </summary>
        public string PasswordSha256
        {
            get => _PasswordSha256;
            set => _PasswordSha256 = String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(PasswordSha256)) : value;
        }

        /// <summary>
        /// First name of the administrator.
        /// </summary>
        public string FirstName { get; set; } = null;

        /// <summary>
        /// Last name of the administrator.
        /// </summary>
        public string LastName { get; set; } = null;

        /// <summary>
        /// Boolean indicating if the administrator is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Timestamp when the administrator was created (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the administrator was last updated (UTC).
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Email = null;
        private string _PasswordSha256 = null;

        /// <summary>
        /// Instantiate the administrator.
        /// </summary>
        public Administrator()
        {
        }

        /// <summary>
        /// Compute SHA256 hash of a password.
        /// </summary>
        /// <param name="password">Plain text password.</param>
        /// <returns>SHA256 hash as lowercase hex string.</returns>
        public static string ComputePasswordHash(string password)
        {
            if (String.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// Verify a password against the stored hash.
        /// </summary>
        /// <param name="password">Plain text password to verify.</param>
        /// <returns>True if the password matches.</returns>
        public bool VerifyPassword(string password)
        {
            if (String.IsNullOrEmpty(password)) return false;
            return ComputePasswordHash(password).Equals(_PasswordSha256, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance.</returns>
        public static Administrator FromDataRow(DataRow row)
        {
            if (row == null) return null;

            Administrator obj = new Administrator
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                Email = DataTableHelper.GetStringValue(row, "email"),
                PasswordSha256 = DataTableHelper.GetStringValue(row, "passwordsha256"),
                FirstName = DataTableHelper.GetStringValue(row, "firstname"),
                LastName = DataTableHelper.GetStringValue(row, "lastname"),
                Active = DataTableHelper.GetBooleanValue(row, "active"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                LastUpdateUtc = DataTableHelper.GetDateTimeValue(row, "lastupdateutc")
            };

            return obj;
        }

        /// <summary>
        /// Instantiate list from DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of instances.</returns>
        public static List<Administrator> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<Administrator>();

            List<Administrator> ret = new List<Administrator>();
            foreach (DataRow row in table.Rows)
            {
                Administrator obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
