namespace Test.Shared.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Database.PostgreSql;
    using Conductor.Core.Enums;
    using Conductor.Core.Settings;
    using FluentAssertions;
    using Npgsql;

    /// <summary>
    /// Tests for the configurable database command timeout: provider timeouts become
    /// <see cref="DatabaseTimeoutException"/>, other failures pass through unchanged, and
    /// migrations run without a timeout.
    /// </summary>
    public class DatabaseTimeoutTests
    {
        /// <summary>
        /// A provider error with a TimeoutException in its inner chain becomes DatabaseTimeoutException.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ExecuteQueryAsync_ProviderTimeout_ThrowsDatabaseTimeoutException()
        {
            FakeDriver driver = new FakeDriver(45);
            Exception providerError = new InvalidOperationException("Exception while reading from stream", new TimeoutException("Timeout during reading attempt"));
            driver.Throw = providerError;

            Func<Task> act = () => driver.ExecuteQueryAsync("SELECT 1;");

            DatabaseTimeoutException ex = (await act.Should().ThrowAsync<DatabaseTimeoutException>().ConfigureAwait(false)).Which;
            ex.TimeoutSeconds.Should().Be(45);
            ex.Message.Should().Contain("45 seconds");
            ex.InnerException.Should().BeSameAs(providerError);
        }

        /// <summary>
        /// The batch path translates timeouts the same way as the single-query path.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ExecuteQueriesAsync_ProviderTimeout_ThrowsDatabaseTimeoutException()
        {
            FakeDriver driver = new FakeDriver(30);
            driver.Throw = new TimeoutException("expired");

            Func<Task> act = () => driver.ExecuteQueriesAsync(new List<string> { "SELECT 1;" });

            await act.Should().ThrowAsync<DatabaseTimeoutException>().ConfigureAwait(false);
        }

        /// <summary>
        /// Non-timeout provider errors are not rewritten.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ExecuteQueryAsync_NonTimeoutError_PassesThroughUnchanged()
        {
            FakeDriver driver = new FakeDriver(30);
            InvalidOperationException providerError = new InvalidOperationException("syntax error");
            driver.Throw = providerError;

            Func<Task> act = () => driver.ExecuteQueryAsync("SELECT 1;");

            (await act.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false)).Which.Should().BeSameAs(providerError);
        }

        /// <summary>
        /// Caller cancellation is not reported as a timeout, even when a TimeoutException is attached.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ExecuteQueryAsync_Cancellation_IsNotReportedAsTimeout()
        {
            FakeDriver driver = new FakeDriver(30);
            driver.Throw = new OperationCanceledException("canceled", new TimeoutException("inner"));

            Func<Task> act = () => driver.ExecuteQueryAsync("SELECT 1;");

            await act.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
        }

        /// <summary>
        /// Normal queries run with the configured timeout.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ExecuteQueryAsync_UsesConfiguredTimeout()
        {
            FakeDriver driver = new FakeDriver(17);

            await driver.ExecuteQueryAsync("SELECT 1;").ConfigureAwait(false);

            driver.ObservedTimeouts.Should().Equal(17);
        }

        /// <summary>
        /// Migration queries run without a timeout, and the override does not leak into later queries.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ExecuteWithoutCommandTimeoutAsync_UsesNoLimit_AndDoesNotLeak()
        {
            FakeDriver driver = new FakeDriver(17);

            await driver.RunMigrationQueryAsync("CREATE INDEX x ON t(c);").ConfigureAwait(false);
            await driver.ExecuteQueryAsync("SELECT 1;").ConfigureAwait(false);

            driver.ObservedTimeouts.Should().Equal(0, 17);
        }

        /// <summary>
        /// A timeout during a migration query reports a zero (unlimited) timeout rather than the configured value.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ExecuteWithoutCommandTimeoutAsync_Timeout_ReportsZeroTimeout()
        {
            FakeDriver driver = new FakeDriver(17);
            driver.Throw = new TimeoutException("server-side timeout");

            Func<Task> act = () => driver.RunMigrationQueryAsync("CREATE INDEX x ON t(c);");

            DatabaseTimeoutException ex = (await act.Should().ThrowAsync<DatabaseTimeoutException>().ConfigureAwait(false)).Which;
            ex.TimeoutSeconds.Should().Be(0);
            ex.Message.Should().Be("The database query timed out.");
        }

        /// <summary>
        /// PostgreSQL recognizes both the Npgsql read timeout and a 57014 query_canceled error, and nothing else.
        /// </summary>
        public void PostgreSql_IsCommandTimeout_RecognizesNpgsqlTimeouts()
        {
            TestPostgreSqlDriver driver = new TestPostgreSqlDriver();

            driver.IsTimeout(new NpgsqlException("Exception while reading from stream", new TimeoutException())).Should().BeTrue();
            driver.IsTimeout(new PostgresException("canceling statement due to statement timeout", "ERROR", "ERROR", PostgresErrorCodes.QueryCanceled)).Should().BeTrue();
            driver.IsTimeout(new PostgresException("syntax error", "ERROR", "ERROR", PostgresErrorCodes.SyntaxError)).Should().BeFalse();
            driver.IsTimeout(new NpgsqlException("connection refused")).Should().BeFalse();
        }

        /// <summary>
        /// The PostgreSQL driver picks up the configured command timeout from settings.
        /// </summary>
        public void PostgreSql_Constructor_ReadsCommandTimeoutFromSettings()
        {
            TestPostgreSqlDriver driver = new TestPostgreSqlDriver(90);
            driver.ConfiguredTimeout.Should().Be(90);
        }

        private sealed class FakeDriver : DatabaseDriverBase
        {
            public Exception Throw { get; set; }

            public List<int> ObservedTimeouts { get; } = new List<int>();

            public FakeDriver(int timeoutSeconds)
            {
                CommandTimeoutSeconds = timeoutSeconds;
            }

            public Task<DataTable> RunMigrationQueryAsync(string query)
            {
                return ExecuteWithoutCommandTimeoutAsync(query);
            }

            public override Task InitializeAsync(CancellationToken token = default)
            {
                return Task.CompletedTask;
            }

            protected override string TelemetryDatabaseSystem => "fake";

            protected override Task<DataTable> ExecuteQueryCoreAsync(string query, bool isTransaction = false, CancellationToken token = default)
            {
                return RunAsync();
            }

            protected override Task<DataTable> ExecuteQueriesCoreAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
            {
                return RunAsync();
            }

            private async Task<DataTable> RunAsync()
            {
                await Task.Yield();
                ObservedTimeouts.Add(EffectiveCommandTimeoutSeconds);
                if (Throw != null) throw Throw;
                return new DataTable();
            }
        }

        private sealed class TestPostgreSqlDriver : PostgreSqlDatabaseDriver
        {
            public TestPostgreSqlDriver(int timeoutSeconds = 30)
                : base(new DatabaseSettings { Type = DatabaseTypeEnum.PostgreSql, CommandTimeoutSeconds = timeoutSeconds })
            {
            }

            public int ConfiguredTimeout => CommandTimeoutSeconds;

            public bool IsTimeout(Exception ex)
            {
                return IsCommandTimeout(ex);
            }
        }
    }
}
