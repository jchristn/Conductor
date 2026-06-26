namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using FluentAssertions;
    using Microsoft.Data.Sqlite;

    /// <summary>
    /// Regression tests for database schema upgrades.
    /// </summary>
    public class DatabaseMigrationTests : IDisposable
    {
        private readonly string _DatabaseFile;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate the migration tests.
        /// </summary>
        public DatabaseMigrationTests()
        {
            _DatabaseFile = Path.Combine(Path.GetTempPath(), $"conductor_migration_{Guid.NewGuid():N}.db");
        }

        public async Task Sqlite_Initialize_UpgradesLegacyRequestHistorySchema()
        {
            await CreateLegacyRequestHistorySchemaAsync().ConfigureAwait(false);

            SqliteDatabaseDriver database = new SqliteDatabaseDriver(new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                Filename = _DatabaseFile,
                LogQueries = false
            });

            Func<Task> act = async () => await database.InitializeAsync().ConfigureAwait(false);
            await act.Should().NotThrowAsync().ConfigureAwait(false);

            RequestHistoryEntry entry = await database.RequestHistory.ReadByIdAsync("rh_legacy").ConfigureAwait(false);

            entry.Should().NotBeNull();
            entry.RequestorUserGuid.Should().BeNull();
            entry.CredentialGuid.Should().BeNull();
            entry.LoadBalancingPolicyGuid.Should().BeNull();
            entry.ModelAccessPolicyGuid.Should().BeNull();
            entry.ModelAccessRuleGuid.Should().BeNull();
            entry.ModelAccessDecision.Should().BeNull();
            entry.ModelAccessWouldDeny.Should().BeFalse();
            entry.RequestedModel.Should().BeNull();
            entry.SessionAffinityOutcome.Should().BeNull();
            entry.SelectionStrategy.Should().BeNull();
            entry.EndpointGroupGuid.Should().BeNull();
            entry.BackoffReason.Should().BeNull();
            entry.AdaptiveSelection.Should().BeFalse();
            entry.PolicyFallbackUsed.Should().BeFalse();
            entry.RequestBodyRetained.Should().BeFalse();
            entry.ResponseBodyRetained.Should().BeFalse();

            await AssertColumnExistsAsync(database, "requestoruserguid").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "credentialguid").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "loadbalancingpolicyguid").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "modelaccesspolicyguid").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "modelaccesspolicyname").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "modelaccessruleguid").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "modelaccessrulename").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "modelaccessdecision").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "modelaccesswoulddeny").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "requestedmodel").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "effectivemodel").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "requesttype").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "routingoutcomecode").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "selectionstrategy").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "endpointgroupguid").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "endpointgroupname").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "backoffreason").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "adaptiveselection").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "policyfallbackused").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "denialreasoncode").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "reservationguid").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "reservationreasoncode").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "reservationwindowstartutc").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "reservationwindowendutc").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "sessionaffinityoutcome").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "requestbodyretained").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "responsebodyretained").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "traceid").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "providerrequestid").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "prompttokens").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "completiontokens").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "totaltokens").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "analyticscaptured").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "dominantstagekind").ConfigureAwait(false);

            await AssertIndexExistsAsync(database, "idx_requesthistory_requestoruserguid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_credentialguid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_loadbalancingpolicyguid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_modelaccesspolicyguid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_modelaccessruleguid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_modelaccessdecision").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_modelaccesswoulddeny").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_requestedmodel").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_effectivemodel").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_selectionstrategy").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_endpointgroupguid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_backoffreason").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_adaptiveselection").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_policyfallbackused").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_denialreasoncode").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_reservationguid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_reservationreasoncode").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_sessionaffinityoutcome").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_traceid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_providerrequestid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_providername").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_analyticscaptured").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_dominantstagekind").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requesthistory_analyticsfailurecode").ConfigureAwait(false);
            await AssertTableExistsAsync(database, "requestanalyticsevents").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "requestanalyticsevents", "reservationguid").ConfigureAwait(false);
            await AssertColumnExistsAsync(database, "requestanalyticsevents", "reservationreasoncode").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requestanalyticsevents_tenant_created").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requestanalyticsevents_requesthistoryid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requestanalyticsevents_traceid").ConfigureAwait(false);
            await AssertIndexExistsAsync(database, "idx_requestanalyticsevents_reservation_created").ConfigureAwait(false);
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing && File.Exists(_DatabaseFile))
            {
                try
                {
                    File.Delete(_DatabaseFile);
                }
                catch
                {
                }
            }

            _Disposed = true;
        }

        private async Task CreateLegacyRequestHistorySchemaAsync()
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + _DatabaseFile))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                string legacySchema = @"
                    CREATE TABLE requesthistory (
                        id TEXT PRIMARY KEY,
                        tenantguid TEXT NOT NULL,
                        virtualmodelrunnerguid TEXT NOT NULL,
                        virtualmodelrunnername TEXT NOT NULL,
                        modelendpointguid TEXT,
                        modelendpointname TEXT,
                        modelendpointurl TEXT,
                        modeldefinitionguid TEXT,
                        modeldefinitionname TEXT,
                        modelconfigurationguid TEXT,
                        requestorsourceip TEXT NOT NULL,
                        httpmethod TEXT NOT NULL,
                        httpurl TEXT NOT NULL,
                        requestbodylength INTEGER NOT NULL,
                        responsebodylength INTEGER,
                        httpstatus INTEGER,
                        responsetimems INTEGER,
                        objectkey TEXT NOT NULL,
                        createdutc TEXT NOT NULL,
                        requesttransfertype INTEGER NOT NULL DEFAULT 0,
                        responsetransfertype INTEGER NOT NULL DEFAULT 0,
                        completedutc TEXT
                    );
                    CREATE INDEX idx_requesthistory_tenantguid ON requesthistory(tenantguid);
                    CREATE INDEX idx_requesthistory_vmrguid ON requesthistory(virtualmodelrunnerguid);
                    CREATE INDEX idx_requesthistory_createdutc ON requesthistory(createdutc);
                    CREATE INDEX idx_requesthistory_httpstatus ON requesthistory(httpstatus);
                    CREATE INDEX idx_requesthistory_requestorsourceip ON requesthistory(requestorsourceip);
                    INSERT INTO requesthistory (
                        id,
                        tenantguid,
                        virtualmodelrunnerguid,
                        virtualmodelrunnername,
                        modelendpointguid,
                        modelendpointname,
                        modelendpointurl,
                        modeldefinitionguid,
                        modeldefinitionname,
                        modelconfigurationguid,
                        requestorsourceip,
                        httpmethod,
                        httpurl,
                        requestbodylength,
                        responsebodylength,
                        httpstatus,
                        responsetimems,
                        objectkey,
                        createdutc,
                        requesttransfertype,
                        responsetransfertype,
                        completedutc
                    ) VALUES (
                        'rh_legacy',
                        'ten_legacy',
                        'vmr_legacy',
                        'Legacy VMR',
                        'mre_legacy',
                        'Legacy Endpoint',
                        'http://legacy.local',
                        'md_legacy',
                        'Legacy Model',
                        'mc_legacy',
                        '203.0.113.10',
                        'POST',
                        '/v1/chat/completions',
                        128,
                        256,
                        200,
                        42,
                        'legacy.json',
                        '2026-05-26 00:00:00',
                        0,
                        0,
                        '2026-05-26 00:00:01'
                    );";

                using (SqliteCommand command = new SqliteCommand(legacySchema, connection))
                {
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        private static async Task AssertColumnExistsAsync(SqliteDatabaseDriver database, string columnName)
        {
            await AssertColumnExistsAsync(database, "requesthistory", columnName).ConfigureAwait(false);
        }

        private static async Task AssertColumnExistsAsync(SqliteDatabaseDriver database, string tableName, string columnName)
        {
            string query = "SELECT COUNT(*) AS cnt FROM pragma_table_info('" + tableName + "') WHERE name = '" + columnName + "';";
            long count = await ReadCountAsync(database, query).ConfigureAwait(false);
            count.Should().Be(1);
        }

        private static async Task AssertIndexExistsAsync(SqliteDatabaseDriver database, string indexName)
        {
            string query = "SELECT COUNT(*) AS cnt FROM sqlite_master WHERE type = 'index' AND name = '" + indexName + "';";
            long count = await ReadCountAsync(database, query).ConfigureAwait(false);
            count.Should().Be(1);
        }

        private static async Task AssertTableExistsAsync(SqliteDatabaseDriver database, string tableName)
        {
            string query = "SELECT COUNT(*) AS cnt FROM sqlite_master WHERE type = 'table' AND name = '" + tableName + "';";
            long count = await ReadCountAsync(database, query).ConfigureAwait(false);
            count.Should().Be(1);
        }

        private static async Task<long> ReadCountAsync(SqliteDatabaseDriver database, string query)
        {
            return Convert.ToInt64((await database.ExecuteQueryAsync(query).ConfigureAwait(false)).Rows[0]["cnt"]);
        }
    }
}
