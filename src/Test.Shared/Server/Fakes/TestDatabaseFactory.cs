namespace Test.Shared.Server.Fakes
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using Conductor.Core.Database;
    using Conductor.Core.Database.MySql;
    using Conductor.Core.Database.PostgreSql;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Database.SqlServer;
    using Conductor.Core.Enums;
    using Conductor.Core.Settings;
    using Microsoft.Data.SqlClient;
    using MySql.Data.MySqlClient;
    using Npgsql;

    /// <summary>
    /// Creates the database driver used by tests.
    /// By default every test gets its own SQLite file.
    /// Setting the environment variable <c>CONDUCTOR_TEST_DB_TYPE</c> to <c>PostgreSql</c>, <c>MySql</c> or <c>SqlServer</c>
    /// runs the same tests against that server instead, with a freshly created database per call so tests stay isolated.
    /// Connection details come from <c>CONDUCTOR_TEST_DB_HOST</c> (default 127.0.0.1), <c>CONDUCTOR_TEST_DB_PORT</c>,
    /// <c>CONDUCTOR_TEST_DB_USER</c> and <c>CONDUCTOR_TEST_DB_PASSWORD</c>. The user must be allowed to create databases.
    /// To keep the server small, databases created earlier in the run are dropped once more than
    /// <see cref="MaxLiveDatabases"/> exist, which assumes tests that share a process run one after another.
    /// This class is thread-safe.
    /// </summary>
    public static class TestDatabaseFactory
    {
        /// <summary>
        /// Name of the environment variable that selects the database provider. Unset or empty means SQLite.
        /// </summary>
        public static readonly string TypeVariable = "CONDUCTOR_TEST_DB_TYPE";

        /// <summary>
        /// Number of most recently created server databases to keep. Older ones are dropped. Default is 6, minimum is 1.
        /// </summary>
        public static int MaxLiveDatabases
        {
            get => _MaxLiveDatabases;
            set => _MaxLiveDatabases = (value < 1 ? 1 : value);
        }

        private static readonly object _Lock = new object();
        private static readonly Queue<string> _LiveDatabases = new Queue<string>();
        private static int _MaxLiveDatabases = 6;

        /// <summary>
        /// The database provider selected for this test run. Default is <see cref="DatabaseTypeEnum.Sqlite"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the environment variable names an unknown provider.</exception>
        public static DatabaseTypeEnum SelectedType
        {
            get
            {
                string value = Environment.GetEnvironmentVariable(TypeVariable);
                if (String.IsNullOrWhiteSpace(value)) return DatabaseTypeEnum.Sqlite;
                if (Enum.TryParse<DatabaseTypeEnum>(value, true, out DatabaseTypeEnum parsed)) return parsed;
                throw new InvalidOperationException("Unknown " + TypeVariable + " value '" + value + "'. Valid values: Sqlite, PostgreSql, MySql, SqlServer.");
            }
        }

        /// <summary>
        /// Create an uninitialized database driver for a test. The caller still calls InitializeAsync.
        /// </summary>
        /// <param name="sqliteFilename">SQLite file to use when SQLite is selected. Must not be null or empty in that case.</param>
        /// <returns>Database driver. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when SQLite is selected and <paramref name="sqliteFilename"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the selected provider is unknown or its connection settings are incomplete.</exception>
        public static DatabaseDriverBase Create(string sqliteFilename)
        {
            DatabaseTypeEnum type = SelectedType;

            if (type == DatabaseTypeEnum.Sqlite)
            {
                if (String.IsNullOrEmpty(sqliteFilename)) throw new ArgumentNullException(nameof(sqliteFilename));
                return new SqliteDatabaseDriver(new DatabaseSettings { Type = DatabaseTypeEnum.Sqlite, Filename = sqliteFilename, LogQueries = false });
            }

            string host = GetVariable("CONDUCTOR_TEST_DB_HOST", "127.0.0.1");
            string user = GetVariable("CONDUCTOR_TEST_DB_USER", null);
            string password = GetVariable("CONDUCTOR_TEST_DB_PASSWORD", null);
            string portText = GetVariable("CONDUCTOR_TEST_DB_PORT", null);
            if (!Int32.TryParse(portText, out int port)) throw new InvalidOperationException("CONDUCTOR_TEST_DB_PORT must be set to the server port when " + TypeVariable + " is " + type + ".");

            string databaseName = "ct_" + Guid.NewGuid().ToString("N").Substring(0, 24);

            DatabaseSettings settings = new DatabaseSettings
            {
                Type = type,
                Hostname = host,
                Port = port,
                DatabaseName = databaseName,
                Username = user,
                Password = password,
                LogQueries = false
            };

            List<string> expired = new List<string>();
            lock (_Lock)
            {
                _LiveDatabases.Enqueue(databaseName);
                while (_LiveDatabases.Count > _MaxLiveDatabases) expired.Add(_LiveDatabases.Dequeue());
            }

            foreach (string name in expired)
            {
                try
                {
                    ExecuteAdmin(OpenAdminConnection(type, host, port, user, password), BuildDropStatement(type, name));
                }
                catch (DbException)
                {
                    // Best effort: a database that cannot be dropped only costs server resources.
                }
            }

            ExecuteAdmin(OpenAdminConnection(type, host, port, user, password), "CREATE DATABASE " + databaseName);

            switch (type)
            {
                case DatabaseTypeEnum.PostgreSql:
                    return new PostgreSqlDatabaseDriver(settings);
                case DatabaseTypeEnum.MySql:
                    return new MySqlDatabaseDriver(settings);
                default:
                    return new SqlServerDatabaseDriver(settings);
            }
        }

        private static DbConnection OpenAdminConnection(DatabaseTypeEnum type, string host, int port, string user, string password)
        {
            switch (type)
            {
                case DatabaseTypeEnum.PostgreSql:
                    return new NpgsqlConnection("Host=" + host + ";Port=" + port + ";Database=postgres;Username=" + user + ";Password=" + password + ";Pooling=false");
                case DatabaseTypeEnum.MySql:
                    return new MySqlConnection("Server=" + host + ";Port=" + port + ";Uid=" + user + ";Pwd=" + password + ";Pooling=false");
                case DatabaseTypeEnum.SqlServer:
                    return new SqlConnection("Server=" + host + "," + port + ";Database=master;User Id=" + user + ";Password=" + password + ";TrustServerCertificate=True;Pooling=false");
                default:
                    throw new InvalidOperationException("Unsupported test database type: " + type);
            }
        }

        private static string BuildDropStatement(DatabaseTypeEnum type, string name)
        {
            switch (type)
            {
                case DatabaseTypeEnum.PostgreSql:
                    return "DROP DATABASE IF EXISTS " + name + " WITH (FORCE)";
                case DatabaseTypeEnum.SqlServer:
                    return "IF DB_ID('" + name + "') IS NOT NULL BEGIN ALTER DATABASE " + name + " SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE " + name + "; END";
                default:
                    return "DROP DATABASE IF EXISTS " + name;
            }
        }

        private static string GetVariable(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return String.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static void ExecuteAdmin(DbConnection connection, string sql)
        {
            using (connection)
            {
                connection.Open();
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
