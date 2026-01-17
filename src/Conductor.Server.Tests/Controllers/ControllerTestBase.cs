namespace Conductor.Server.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Core.Settings;
    using Conductor.Server.Services;
    using SyslogLogging;

    /// <summary>
    /// Base class for controller tests providing common test infrastructure.
    /// Uses in-memory SQLite database for isolation.
    /// </summary>
    public abstract class ControllerTestBase : IDisposable
    {
        private readonly string _DatabaseFile;
        private bool _Disposed = false;

        /// <summary>
        /// Database driver for testing.
        /// </summary>
        protected DatabaseDriverBase Database { get; private set; }

        /// <summary>
        /// Authentication service for testing.
        /// </summary>
        protected AuthenticationService AuthService { get; private set; }

        /// <summary>
        /// Serializer for testing.
        /// </summary>
        protected Serializer Serializer { get; private set; }

        /// <summary>
        /// Logging module for testing.
        /// </summary>
        protected LoggingModule Logging { get; private set; }

        /// <summary>
        /// Default tenant ID for testing.
        /// </summary>
        protected string TestTenantId { get; private set; }

        /// <summary>
        /// Default user ID for testing.
        /// </summary>
        protected string TestUserId { get; private set; }

        /// <summary>
        /// Instantiate the base test class.
        /// </summary>
        protected ControllerTestBase()
        {
            // Create unique database file for test isolation
            _DatabaseFile = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"conductor_test_{Guid.NewGuid():N}.db");

            DatabaseSettings dbSettings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                Filename = _DatabaseFile,
                LogQueries = false
            };

            Database = new SqliteDatabaseDriver(dbSettings);
            Serializer = new Serializer();
            Logging = new LoggingModule();

            AuthService = new AuthenticationService(Database, Logging, new List<string> { "test-admin-key" });
        }

        /// <summary>
        /// Initialize the database and create test data.
        /// </summary>
        /// <returns>Task.</returns>
        protected async Task InitializeDatabaseAsync()
        {
            await Database.InitializeAsync().ConfigureAwait(false);
            await CreateTestTenantAndUserAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Create a test tenant and user for testing.
        /// </summary>
        /// <returns>Task.</returns>
        private async Task CreateTestTenantAndUserAsync()
        {
            TenantMetadata tenant = new TenantMetadata
            {
                Name = "Test Tenant",
                Active = true
            };
            tenant = await Database.Tenant.CreateAsync(tenant).ConfigureAwait(false);
            TestTenantId = tenant.Id;

            UserMaster user = new UserMaster
            {
                TenantId = TestTenantId,
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                Password = "password123",
                Active = true
            };
            user = await Database.User.CreateAsync(user).ConfigureAwait(false);
            TestUserId = user.Id;
        }

        /// <summary>
        /// Dispose of test resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of test resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                Logging?.Dispose();

                // Clean up test database file
                if (System.IO.File.Exists(_DatabaseFile))
                {
                    try
                    {
                        System.IO.File.Delete(_DatabaseFile);
                    }
                    catch
                    {
                        // Ignore cleanup failures
                    }
                }
            }

            _Disposed = true;
        }
    }
}
