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
            "idx_requesthistory_modelaccesspolicyguid",
            "idx_requesthistory_modelaccessruleguid",
            "idx_requesthistory_modelaccessdecision",
            "idx_requesthistory_modelaccesswoulddeny",
            "idx_requesthistory_requestedmodel",
            "idx_requesthistory_effectivemodel",
            "idx_requesthistory_denialreasoncode",
            "idx_requesthistory_reservationguid",
            "idx_requesthistory_reservationreasoncode",
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
            "modelaccesspolicyguid",
            "modelaccesspolicyname",
            "modelaccessruleguid",
            "modelaccessrulename",
            "modelaccessdecision",
            "modelaccesswoulddeny",
            "requestedmodel",
            "effectivemodel",
            "denialreasoncode",
            "reservationguid",
            "reservationreasoncode",
            "reservationwindowstartutc",
            "reservationwindowendutc",
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
            "idx_requestanalyticsevents_vmr_created",
            "idx_requestanalyticsevents_reservation_created"
        };

        private static readonly string[] _AnalyticsSavedReportIndexNames =
        {
            "idx_asr_tenantid",
            "idx_asr_owneruserid",
            "idx_asr_scope",
            "idx_asr_name",
            "idx_asr_lastupdateutc"
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

        public void CreateAnalyticsSavedReportsTable_AllSupportedDialects_ContainsRequiredIndexes()
        {
            AssertAnalyticsSavedReportCreateSchema(SqliteTableQueries.CreateAnalyticsSavedReportsTable);
            AssertAnalyticsSavedReportCreateSchema(PostgreSqlTableQueries.CreateAnalyticsSavedReportsTable);
            AssertAnalyticsSavedReportCreateSchema(MySqlTableQueries.CreateAnalyticsSavedReportsTable);
            AssertAnalyticsSavedReportCreateSchema(SqlServerTableQueries.CreateAnalyticsSavedReportsTable);
        }

        public void CreateVirtualModelRunnersTable_AllSupportedDialects_DefaultsRequestHistoryEnabled()
        {
            SqliteTableQueries.CreateVirtualModelRunnersTable.Should().Contain("requesthistoryenabled INTEGER NOT NULL DEFAULT 1");
            PostgreSqlTableQueries.CreateVirtualModelRunnersTable.Should().Contain("requesthistoryenabled BOOLEAN NOT NULL DEFAULT TRUE");
            MySqlTableQueries.CreateVirtualModelRunnersTable.Should().Contain("requesthistoryenabled TINYINT(1) NOT NULL DEFAULT 1");
            SqlServerTableQueries.CreateVirtualModelRunnersTable.Should().Contain("requesthistoryenabled BIT NOT NULL DEFAULT 1");

            SqliteTableQueries.AddRequestHistoryEnabledColumn.Should().Contain("DEFAULT 1");
            PostgreSqlTableQueries.AddRequestHistoryEnabledColumn.Should().Contain("DEFAULT TRUE");
            MySqlTableQueries.AddRequestHistoryEnabledColumn.Should().Contain("DEFAULT 1");
            SqlServerTableQueries.AddRequestHistoryEnabledColumn.Should().Contain("DEFAULT 1");
        }

        public void FactorySchema_DefersMigratedIndexes()
        {
            string schemaPath = FindRepositoryPath("docker", "factory", "schema.sql");
            string schemaSql = File.ReadAllText(schemaPath);

            AssertRequestHistoryCreateSchema(schemaSql);
            AssertRequestAnalyticsCreateSchema(schemaSql);
            AssertAnalyticsSavedReportCreateSchema(schemaSql);
            schemaSql.Should().Contain("requesthistoryenabled INTEGER NOT NULL DEFAULT 1");
        }

        public void DockerPostgresSchema_DefaultsRequestHistoryEnabled()
        {
            string schemaPath = FindRepositoryPath("docker", "postgres", "init.sql");
            string schemaSql = File.ReadAllText(schemaPath);

            schemaSql.Should().Contain("requesthistoryenabled BOOLEAN NOT NULL DEFAULT TRUE");
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
            schema.Should().Contain("reservationguid");
            schema.Should().Contain("reservationreasoncode");

            foreach (string indexName in _AnalyticsIndexNames)
            {
                schema.Should().Contain(indexName);
            }
        }

        private static void AssertAnalyticsSavedReportCreateSchema(string schema)
        {
            schema.Should().NotBeNullOrWhiteSpace();
            schema.Should().Contain("analyticssavedreports");
            schema.Should().Contain("tenantid");
            schema.Should().Contain("owneruserid");
            schema.Should().Contain("queryjson");
            schema.Should().Contain("displaystatejson");

            foreach (string indexName in _AnalyticsSavedReportIndexNames)
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
