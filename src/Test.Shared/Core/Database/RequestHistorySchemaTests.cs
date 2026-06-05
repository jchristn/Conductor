namespace Test.Shared.Core.Database
{
    using System;
    using System.IO;
    using Conductor.Core.Database.MySql.Queries;
    using Conductor.Core.Database.PostgreSql.Queries;
    using Conductor.Core.Database.Sqlite.Queries;
    using FluentAssertions;
    using SqlServerTableQueries = Conductor.Core.Database.SqlServer.Queries.TableQueries;
    using MySqlTableQueries = Conductor.Core.Database.MySql.Queries.TableQueries;
    using PostgreSqlTableQueries = Conductor.Core.Database.PostgreSql.Queries.TableQueries;
    using SqliteTableQueries = Conductor.Core.Database.Sqlite.Queries.TableQueries;

    /// <summary>
    /// Guards schema definitions so additive request-history indexes stay in migrations.
    /// </summary>
    public class RequestHistorySchemaTests
    {
        private static readonly string[] _DeferredIndexNames =
        {
            "idx_requesthistory_requestoruserguid",
            "idx_requesthistory_credentialguid",
            "idx_requesthistory_loadbalancingpolicyguid",
            "idx_requesthistory_requestedmodel",
            "idx_requesthistory_effectivemodel",
            "idx_requesthistory_denialreasoncode",
            "idx_requesthistory_sessionaffinityoutcome",
            "idx_requesthistory_traceid",
            "idx_requesthistory_providerrequestid",
            "idx_requesthistory_providername",
            "idx_requesthistory_analyticscaptured",
            "idx_requesthistory_dominantstagekind",
            "idx_requesthistory_analyticsfailurecode"
        };

        private static readonly string[] _FreshSchemaColumns =
        {
            "requestoruserguid",
            "credentialguid",
            "loadbalancingpolicyguid",
            "requestedmodel",
            "effectivemodel",
            "denialreasoncode",
            "sessionaffinityoutcome",
            "traceid",
            "providerrequestid",
            "prompttokens",
            "completiontokens",
            "totaltokens",
            "analyticscaptured",
            "dominantstagekind"
        };

        private static readonly string[] _BaselineIndexNames =
        {
            "idx_requesthistory_tenantguid",
            "idx_requesthistory_vmrguid",
            "idx_requesthistory_createdutc",
            "idx_requesthistory_httpstatus",
            "idx_requesthistory_requestorsourceip"
        };

        private static readonly string[] _AnalyticsIndexNames =
        {
            "idx_requestanalyticsevents_tenant_created",
            "idx_requestanalyticsevents_requesthistoryid",
            "idx_requestanalyticsevents_traceid",
            "idx_requestanalyticsevents_stagekind",
            "idx_requestanalyticsevents_endpoint_created",
            "idx_requestanalyticsevents_vmr_created"
        };

        public void CreateRequestHistoryTable_AllSupportedDialects_DefersMigratedIndexes()
        {
            AssertRequestHistoryCreateSchema(SqliteTableQueries.CreateRequestHistoryTable);
            AssertRequestHistoryCreateSchema(PostgreSqlTableQueries.CreateRequestHistoryTable);
            AssertRequestHistoryCreateSchema(MySqlTableQueries.CreateRequestHistoryTable);
            AssertRequestHistoryCreateSchema(SqlServerTableQueries.CreateRequestHistoryTable);
        }

        public void CreateRequestAnalyticsEventsTable_AllSupportedDialects_ContainsRequiredIndexes()
        {
            AssertRequestAnalyticsCreateSchema(SqliteTableQueries.CreateRequestAnalyticsEventsTable);
            AssertRequestAnalyticsCreateSchema(PostgreSqlTableQueries.CreateRequestAnalyticsEventsTable);
            AssertRequestAnalyticsCreateSchema(MySqlTableQueries.CreateRequestAnalyticsEventsTable);
            AssertRequestAnalyticsCreateSchema(SqlServerTableQueries.CreateRequestAnalyticsEventsTable);
        }

        public void FactorySchema_DefersMigratedIndexes()
        {
            string schemaPath = FindRepositoryPath("docker", "factory", "schema.sql");
            string schemaSql = File.ReadAllText(schemaPath);

            AssertRequestHistoryCreateSchema(schemaSql);
            AssertRequestAnalyticsCreateSchema(schemaSql);
        }

        private static void AssertRequestHistoryCreateSchema(string schema)
        {
            schema.Should().NotBeNullOrWhiteSpace();

            foreach (string columnName in _FreshSchemaColumns)
            {
                schema.Should().Contain(columnName);
            }

            foreach (string indexName in _BaselineIndexNames)
            {
                schema.Should().Contain(indexName);
            }

            foreach (string indexName in _DeferredIndexNames)
            {
                schema.Should().NotContain(indexName);
            }
        }

        private static void AssertRequestAnalyticsCreateSchema(string schema)
        {
            schema.Should().NotBeNullOrWhiteSpace();
            schema.Should().Contain("requestanalyticsevents");
            schema.Should().Contain("requesthistoryid");
            schema.Should().Contain("stagekind");
            schema.Should().Contain("durationms");
            schema.Should().Contain("tokenspersecond");

            foreach (string indexName in _AnalyticsIndexNames)
            {
                schema.Should().Contain(indexName);
            }
        }

        private static string FindRepositoryPath(params string[] parts)
        {
            DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                string candidate = Path.Combine(current.FullName, Path.Combine(parts));
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            throw new FileNotFoundException("Unable to locate repository file: " + Path.Combine(parts));
        }
    }
}
