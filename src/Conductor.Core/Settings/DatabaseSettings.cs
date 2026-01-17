namespace Conductor.Core.Settings
{
    using System;
    using Conductor.Core.Enums;

    /// <summary>
    /// Database settings.
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>
        /// Database type.
        /// </summary>
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Sqlite;

        /// <summary>
        /// Database filename (for SQLite).
        /// </summary>
        public string Filename
        {
            get => _Filename;
            set => _Filename = (String.IsNullOrEmpty(value) ? "./conductor.db" : value);
        }

        /// <summary>
        /// Database hostname (for PostgreSQL, SQL Server, MySQL).
        /// </summary>
        public string Hostname
        {
            get => _Hostname;
            set => _Hostname = (String.IsNullOrEmpty(value) ? "localhost" : value);
        }

        /// <summary>
        /// Database port.
        /// </summary>
        public int Port
        {
            get => _Port;
            set => _Port = (value < 1 || value > 65535 ? GetDefaultPort() : value);
        }

        /// <summary>
        /// Database name.
        /// </summary>
        public string DatabaseName
        {
            get => _DatabaseName;
            set => _DatabaseName = (String.IsNullOrEmpty(value) ? "conductor" : value);
        }

        /// <summary>
        /// Database username.
        /// </summary>
        public string Username { get; set; } = null;

        /// <summary>
        /// Database password.
        /// </summary>
        public string Password { get; set; } = null;

        /// <summary>
        /// Require encryption for database connections.
        /// </summary>
        public bool RequireEncryption { get; set; } = false;

        /// <summary>
        /// Log database queries.
        /// </summary>
        public bool LogQueries { get; set; } = false;

        private string _Filename = "./conductor.db";
        private string _Hostname = "localhost";
        private int _Port = 5432;
        private string _DatabaseName = "conductor";

        /// <summary>
        /// Instantiate the database settings.
        /// </summary>
        public DatabaseSettings()
        {
        }

        /// <summary>
        /// Get the connection string based on settings.
        /// </summary>
        /// <returns>Connection string.</returns>
        public string GetConnectionString()
        {
            switch (Type)
            {
                case DatabaseTypeEnum.Sqlite:
                    return "Data Source=" + Filename;

                case DatabaseTypeEnum.PostgreSql:
                    string pgConn = "Host=" + Hostname + ";Port=" + Port + ";Database=" + DatabaseName;
                    if (!String.IsNullOrEmpty(Username)) pgConn += ";Username=" + Username;
                    if (!String.IsNullOrEmpty(Password)) pgConn += ";Password=" + Password;
                    if (RequireEncryption) pgConn += ";SSL Mode=Require";
                    return pgConn;

                case DatabaseTypeEnum.SqlServer:
                    string sqlConn = "Server=" + Hostname + "," + Port + ";Database=" + DatabaseName;
                    if (!String.IsNullOrEmpty(Username) && !String.IsNullOrEmpty(Password))
                    {
                        sqlConn += ";User Id=" + Username + ";Password=" + Password;
                    }
                    else
                    {
                        sqlConn += ";Integrated Security=True";
                    }
                    if (RequireEncryption) sqlConn += ";Encrypt=True";
                    else sqlConn += ";TrustServerCertificate=True";
                    return sqlConn;

                case DatabaseTypeEnum.MySql:
                    string myConn = "Server=" + Hostname + ";Port=" + Port + ";Database=" + DatabaseName;
                    if (!String.IsNullOrEmpty(Username)) myConn += ";Uid=" + Username;
                    if (!String.IsNullOrEmpty(Password)) myConn += ";Pwd=" + Password;
                    if (RequireEncryption) myConn += ";SslMode=Required";
                    return myConn;

                default:
                    throw new InvalidOperationException("Unknown database type: " + Type);
            }
        }

        private int GetDefaultPort()
        {
            switch (Type)
            {
                case DatabaseTypeEnum.PostgreSql:
                    return 5432;
                case DatabaseTypeEnum.SqlServer:
                    return 1433;
                case DatabaseTypeEnum.MySql:
                    return 3306;
                default:
                    return 0;
            }
        }
    }
}
