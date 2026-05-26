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
            "idx_requesthistory_sessionaffinityoutcome"
        };

        private static readonly string[] _FreshSchemaColumns =
        {
            "requestoruserguid",
            "credentialguid",
            "loadbalancingpolicyguid",
            "requestedmodel",
            "effectivemodel",
            "denialreasoncode",
            "sessionaffinityoutcome"
        };

        private static readonly string[] _BaselineIndexNames =
        {
            "idx_requesthistory_tenantguid",
            "idx_requesthistory_vmrguid",
            "idx_requesthistory_createdutc",
            "idx_requesthistory_httpstatus",
            "idx_requesthistory_requestorsourceip"
        };

        public void CreateRequestHistoryTable_AllSupportedDialects_DefersMigratedIndexes()
        {
            AssertRequestHistoryCreateSchema(SqliteTableQueries.CreateRequestHistoryTable);
            AssertRequestHistoryCreateSchema(PostgreSqlTableQueries.CreateRequestHistoryTable);
            AssertRequestHistoryCreateSchema(MySqlTableQueries.CreateRequestHistoryTable);
            AssertRequestHistoryCreateSchema(SqlServerTableQueries.CreateRequestHistoryTable);
        }

        public void FactorySchema_DefersMigratedIndexes()
        {
            string schemaPath = FindRepositoryPath("docker", "factory", "schema.sql");
            string schemaSql = File.ReadAllText(schemaPath);

            AssertRequestHistoryCreateSchema(schemaSql);
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
