namespace Test.Shared.Server.Integration
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Database.Sqlite;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using FluentAssertions;

    /// <summary>
    /// Integration tests for QoS profile and traffic-class persistence against a real SQLite database:
    /// aggregate round-trip, replace-on-update, cascade delete, default lookup, and the traffic-class
    /// catalog. Each behavior has a positive and a negative aspect.
    /// </summary>
    public class QosPersistenceTests : IDisposable
    {
        private readonly string _DatabaseFile;
        private DatabaseDriverBase _Database;
        private string _TenantId;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the tests with a unique temp database file.
        /// </summary>
        public QosPersistenceTests()
        {
            _DatabaseFile = Path.Combine(Path.GetTempPath(), "conductor_qos_" + Guid.NewGuid().ToString("N") + ".db");
        }

        /// <summary>
        /// Create the database and a test tenant.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            Conductor.Core.Settings.DatabaseSettings settings = new Conductor.Core.Settings.DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                Filename = _DatabaseFile,
                LogQueries = false
            };
            _Database = new SqliteDatabaseDriver(settings);
            await _Database.InitializeAsync().ConfigureAwait(false);

            TenantMetadata tenant = new TenantMetadata { Name = "QoS Test Tenant", Active = true };
            tenant = await _Database.Tenant.CreateAsync(tenant).ConfigureAwait(false);
            _TenantId = tenant.Id;
        }

        public async Task QosProfile_CreateRead_RoundTripsAggregate()
        {
            QosProfile created = await _Database.QosProfile.CreateAsync(BuildProfile("round-trip")).ConfigureAwait(false);

            QosProfile read = await _Database.QosProfile.ReadAsync(_TenantId, created.Id).ConfigureAwait(false);

            read.Should().NotBeNull();
            read.Name.Should().Be("round-trip");
            read.MaxQueueWaitMs.Should().Be(12345);
            read.Rules.Should().HaveCount(1);
            read.Rules[0].ClassName.Should().Be("gold");
            read.Nodes.Should().HaveCount(1);
            read.Nodes[0].Discipline.Should().Be(QosDisciplineEnum.Llq);
            read.Nodes[0].MaxDepth.Should().Be(100);
            read.Nodes[0].OverflowPolicy.Should().Be(QosOverflowPolicyEnum.DropOldest);
            read.Nodes[0].Classes.Should().HaveCount(2);
            QosQueueClass gold = read.Nodes[0].Classes.Find(c => c.ClassName == "gold");
            gold.Should().NotBeNull();
            gold.RatePerSecond.Should().Be(50.5);
            gold.Burst.Should().Be(100);
            read.Links.Should().HaveCount(1);
            read.IngressRoutes.Should().HaveCount(1);
        }

        public async Task QosProfile_ReadMissing_ReturnsNull()
        {
            QosProfile read = await _Database.QosProfile.ReadAsync(_TenantId, "qos_does_not_exist").ConfigureAwait(false);
            read.Should().BeNull();
        }

        public async Task QosProfile_Update_ReplacesChildRows()
        {
            QosProfile created = await _Database.QosProfile.CreateAsync(BuildProfile("upd")).ConfigureAwait(false);

            created.Rules.Clear();
            created.Nodes[0].Classes.Clear();
            created.Nodes[0].Classes.Add(new QosQueueClass { Ordinal = 0, Kind = QosQueueClassKindEnum.FairClass, ClassName = "default", Weight = 1 });
            created.Links.Clear();
            created.IngressRoutes.Clear();
            created.Name = "upd-renamed";

            await _Database.QosProfile.UpdateAsync(created).ConfigureAwait(false);

            QosProfile read = await _Database.QosProfile.ReadAsync(_TenantId, created.Id).ConfigureAwait(false);
            read.Name.Should().Be("upd-renamed");
            read.Rules.Should().BeEmpty();
            read.Nodes.Should().HaveCount(1);
            read.Nodes[0].Classes.Should().HaveCount(1);
            read.Nodes[0].Classes[0].ClassName.Should().Be("default");
            read.Links.Should().BeEmpty();
            read.IngressRoutes.Should().BeEmpty();
        }

        public async Task QosProfile_Delete_RemovesProfile()
        {
            QosProfile created = await _Database.QosProfile.CreateAsync(BuildProfile("del")).ConfigureAwait(false);

            await _Database.QosProfile.DeleteAsync(_TenantId, created.Id).ConfigureAwait(false);

            QosProfile read = await _Database.QosProfile.ReadAsync(_TenantId, created.Id).ConfigureAwait(false);
            read.Should().BeNull();
        }

        public async Task QosProfile_ReadDefault_FindsOnlyTheDefault()
        {
            // Negative: no default yet.
            (await _Database.QosProfile.ReadDefaultAsync(_TenantId).ConfigureAwait(false)).Should().BeNull();

            await _Database.QosProfile.CreateAsync(BuildProfile("non-default")).ConfigureAwait(false);
            QosProfile def = BuildProfile("the-default");
            def.IsDefault = true;
            await _Database.QosProfile.CreateAsync(def).ConfigureAwait(false);

            // Positive: the default is returned.
            QosProfile read = await _Database.QosProfile.ReadDefaultAsync(_TenantId).ConfigureAwait(false);
            read.Should().NotBeNull();
            read.IsDefault.Should().BeTrue();
            read.Name.Should().Be("the-default");
        }

        public async Task QosTrafficClass_Crud_And_ReadByName()
        {
            QosTrafficClass created = await _Database.QosTrafficClass.CreateAsync(new QosTrafficClass
            {
                TenantId = _TenantId,
                Name = "human-interactive",
                Description = "people",
                Tier = QosClassTierEnum.Interactive
            }).ConfigureAwait(false);

            // Positive: read by name finds it.
            QosTrafficClass byName = await _Database.QosTrafficClass.ReadByNameAsync(_TenantId, "human-interactive").ConfigureAwait(false);
            byName.Should().NotBeNull();
            byName.Tier.Should().Be(QosClassTierEnum.Interactive);

            // Negative: an unknown name returns null.
            (await _Database.QosTrafficClass.ReadByNameAsync(_TenantId, "nope").ConfigureAwait(false)).Should().BeNull();

            created.Description = "updated";
            await _Database.QosTrafficClass.UpdateAsync(created).ConfigureAwait(false);
            (await _Database.QosTrafficClass.ReadAsync(_TenantId, created.Id).ConfigureAwait(false)).Description.Should().Be("updated");

            await _Database.QosTrafficClass.DeleteAsync(_TenantId, created.Id).ConfigureAwait(false);
            (await _Database.QosTrafficClass.ExistsAsync(_TenantId, created.Id).ConfigureAwait(false)).Should().BeFalse();
        }

        private QosProfile BuildProfile(string name)
        {
            QosProfile profile = new QosProfile
            {
                TenantId = _TenantId,
                Name = name,
                DefaultClass = "default",
                IngressMode = QosIngressModeEnum.Single,
                IngressDefaultNode = "n",
                TailNode = "n",
                MaxQueueWaitMs = 12345,
                RejectionStatusCode = 429
            };
            profile.Rules.Add(new QosClassifierRule { Ordinal = 0, Source = QosClassifierSourceEnum.Header, MatchKey = "X-C", Operator = QosClassifierOperatorEnum.Equals, MatchValue = "gold", ClassName = "gold" });

            QosQueueNode node = new QosQueueNode { Name = "n", Discipline = QosDisciplineEnum.Llq, MaxDepth = 100, OverflowPolicy = QosOverflowPolicyEnum.DropOldest, DefaultWeight = 2 };
            node.Classes.Add(new QosQueueClass { Ordinal = 0, Kind = QosQueueClassKindEnum.PriorityClass, ClassName = "gold", RatePerSecond = 50.5, Burst = 100 });
            node.Classes.Add(new QosQueueClass { Ordinal = 1, Kind = QosQueueClassKindEnum.FairClass, ClassName = "default", Weight = 3 });
            profile.Nodes.Add(node);

            profile.Links.Add(new QosQueueLink { FromNode = "a", ToNode = "n" });
            profile.IngressRoutes.Add(new QosIngressRoute { Ordinal = 0, ClassName = "gold", Node = "n" });
            return profile;
        }

        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            try { if (File.Exists(_DatabaseFile)) File.Delete(_DatabaseFile); }
            catch { /* best effort */ }
        }
    }
}
