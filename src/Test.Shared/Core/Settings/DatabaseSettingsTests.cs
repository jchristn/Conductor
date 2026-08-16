namespace Test.Shared.Core.Settings
{
    using System;
    using Conductor.Core.Enums;
    using Conductor.Core.Settings;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for DatabaseSettings, including default values, guarded property
    /// normalization, port clamping, and connection-string construction for every
    /// supported database provider.
    /// </summary>
    public class DatabaseSettingsTests
    {
        #region Default-Value-Tests
        public void Type_DefaultsToSqlite()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Type.Should().Be(DatabaseTypeEnum.Sqlite);
        }
        public void Filename_DefaultsToLocalConductorDb()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Filename.Should().Be("./conductor.db");
        }
        public void Hostname_DefaultsToLocalhost()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Hostname.Should().Be("localhost");
        }
        public void Port_DefaultsTo5432()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Port.Should().Be(5432);
        }
        public void DatabaseName_DefaultsToConductor()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.DatabaseName.Should().Be("conductor");
        }
        public void Username_And_Password_DefaultToNull()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Username.Should().BeNull();
            settings.Password.Should().BeNull();
        }
        public void RequireEncryption_And_LogQueries_DefaultToFalse()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.RequireEncryption.Should().BeFalse();
            settings.LogQueries.Should().BeFalse();
        }

        #endregion

        #region Guarded-Property-Tests
        public void Filename_WhenSetToNull_FallsBackToDefault()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Filename = null;
            settings.Filename.Should().Be("./conductor.db");
        }
        public void Filename_WhenSetToEmpty_FallsBackToDefault()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Filename = "";
            settings.Filename.Should().Be("./conductor.db");
        }
        public void Filename_WhenSetToValue_UsesProvidedValue()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Filename = "/data/custom.db";
            settings.Filename.Should().Be("/data/custom.db");
        }
        public void Hostname_WhenSetToNull_FallsBackToDefault()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Hostname = null;
            settings.Hostname.Should().Be("localhost");
        }
        public void Hostname_WhenSetToValue_UsesProvidedValue()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Hostname = "db.internal";
            settings.Hostname.Should().Be("db.internal");
        }
        public void DatabaseName_WhenSetToEmpty_FallsBackToDefault()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.DatabaseName = "";
            settings.DatabaseName.Should().Be("conductor");
        }
        public void DatabaseName_WhenSetToValue_UsesProvidedValue()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.DatabaseName = "analytics";
            settings.DatabaseName.Should().Be("analytics");
        }

        #endregion

        #region Port-Clamping-Tests
        public void Port_WhenValid_UsesProvidedValue()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Port = 6543;
            settings.Port.Should().Be(6543);
        }
        public void Port_WhenBelowMinimum_FallsBackToProviderDefault()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Type = DatabaseTypeEnum.PostgreSql;
            settings.Port = 0;
            settings.Port.Should().Be(5432);
        }
        public void Port_WhenAboveMaximum_FallsBackToProviderDefault()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Type = DatabaseTypeEnum.MySql;
            settings.Port = 70000;
            settings.Port.Should().Be(3306);
        }
        public void Port_ForSqlServer_FallsBackTo1433()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Type = DatabaseTypeEnum.SqlServer;
            settings.Port = -1;
            settings.Port.Should().Be(1433);
        }
        public void Port_AtBoundaries_AreAccepted()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Port = 1;
            settings.Port.Should().Be(1);
            settings.Port = 65535;
            settings.Port.Should().Be(65535);
        }

        #endregion

        #region ConnectionString-Sqlite-Tests
        public void GetConnectionString_ForSqlite_UsesDataSource()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                Filename = "/tmp/test.db"
            };
            settings.GetConnectionString().Should().Be("Data Source=/tmp/test.db");
        }

        #endregion

        #region ConnectionString-PostgreSql-Tests
        public void GetConnectionString_ForPostgreSql_IncludesHostPortDatabase()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.PostgreSql,
                Hostname = "pg.host",
                Port = 5432,
                DatabaseName = "conductor"
            };

            string connectionString = settings.GetConnectionString();

            connectionString.Should().Contain("Host=pg.host");
            connectionString.Should().Contain("Port=5432");
            connectionString.Should().Contain("Database=conductor");
        }
        public void GetConnectionString_ForPostgreSql_WithCredentials_IncludesThem()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.PostgreSql,
                Username = "pguser",
                Password = "pgpass"
            };

            string connectionString = settings.GetConnectionString();

            connectionString.Should().Contain("Username=pguser");
            connectionString.Should().Contain("Password=pgpass");
        }
        public void GetConnectionString_ForPostgreSql_WithoutCredentials_OmitsThem()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.PostgreSql
            };

            string connectionString = settings.GetConnectionString();

            connectionString.Should().NotContain("Username=");
            connectionString.Should().NotContain("Password=");
        }
        public void GetConnectionString_ForPostgreSql_WithEncryption_RequiresSsl()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.PostgreSql,
                RequireEncryption = true
            };

            settings.GetConnectionString().Should().Contain("SSL Mode=Require");
        }

        #endregion

        #region ConnectionString-SqlServer-Tests
        public void GetConnectionString_ForSqlServer_WithCredentials_UsesUserIdAndPassword()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.SqlServer,
                Hostname = "sql.host",
                Port = 1433,
                DatabaseName = "conductor",
                Username = "sa",
                Password = "secret"
            };

            string connectionString = settings.GetConnectionString();

            connectionString.Should().Contain("Server=sql.host,1433");
            connectionString.Should().Contain("Database=conductor");
            connectionString.Should().Contain("User Id=sa");
            connectionString.Should().Contain("Password=secret");
            connectionString.Should().NotContain("Integrated Security");
        }
        public void GetConnectionString_ForSqlServer_WithoutCredentials_UsesIntegratedSecurity()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.SqlServer
            };

            settings.GetConnectionString().Should().Contain("Integrated Security=True");
        }
        public void GetConnectionString_ForSqlServer_WithoutEncryption_TrustsServerCertificate()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.SqlServer,
                RequireEncryption = false
            };

            string connectionString = settings.GetConnectionString();

            connectionString.Should().Contain("TrustServerCertificate=True");
            connectionString.Should().NotContain("Encrypt=True");
        }
        public void GetConnectionString_ForSqlServer_WithEncryption_Encrypts()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.SqlServer,
                RequireEncryption = true
            };

            settings.GetConnectionString().Should().Contain("Encrypt=True");
        }

        #endregion

        #region ConnectionString-MySql-Tests
        public void GetConnectionString_ForMySql_IncludesServerPortDatabase()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.MySql,
                Hostname = "mysql.host",
                Port = 3306,
                DatabaseName = "conductor"
            };

            string connectionString = settings.GetConnectionString();

            connectionString.Should().Contain("Server=mysql.host");
            connectionString.Should().Contain("Port=3306");
            connectionString.Should().Contain("Database=conductor");
        }
        public void GetConnectionString_ForMySql_WithCredentials_UsesUidAndPwd()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.MySql,
                Username = "root",
                Password = "toor"
            };

            string connectionString = settings.GetConnectionString();

            connectionString.Should().Contain("Uid=root");
            connectionString.Should().Contain("Pwd=toor");
        }
        public void GetConnectionString_ForMySql_WithEncryption_RequiresSsl()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.MySql,
                RequireEncryption = true
            };

            settings.GetConnectionString().Should().Contain("SslMode=Required");
        }

        #endregion

        #region ConnectionString-Negative-Tests
        public void GetConnectionString_ForUnknownType_ThrowsInvalidOperationException()
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = (DatabaseTypeEnum)999
            };

            Action act = () => settings.GetConnectionString();
            act.Should().Throw<InvalidOperationException>();
        }

        #endregion
    }
}
