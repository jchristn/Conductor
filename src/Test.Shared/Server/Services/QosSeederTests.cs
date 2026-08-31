namespace Test.Shared.Server.Services
{
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;

    /// <summary>
    /// Tests for <see cref="QosSeeder"/>: seeding, idempotent no-resurrection, runner backfill, and the
    /// always-ensured default profile.
    /// </summary>
    public class QosSeederTests : ControllerTestBase
    {
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task EnsureTenant_SeedsDefaultClassesAndStandardProfile()
        {
            TenantMetadata tenant = await Database.Tenant.ReadAsync(TestTenantId).ConfigureAwait(false);
            await new QosSeeder(Database).EnsureTenantAsync(tenant).ConfigureAwait(false);

            (await Database.QosProfile.ReadDefaultAsync(TestTenantId).ConfigureAwait(false)).Should().NotBeNull();
            (await Database.QosTrafficClass.ReadByNameAsync(TestTenantId, "realtime").ConfigureAwait(false)).Should().NotBeNull();

            EnumerationResult<QosProfile> profiles = await Database.QosProfile.EnumerateAsync(TestTenantId, new EnumerationRequest { MaxResults = 100 }).ConfigureAwait(false);
            profiles.Data.Should().Contain(p => p.Name == QosProfileFactory.StandardProfileName);
        }

        public async Task EnsureTenant_IsIdempotent_NoResurrection()
        {
            QosSeeder seeder = new QosSeeder(Database);
            await seeder.EnsureTenantAsync(await Database.Tenant.ReadAsync(TestTenantId).ConfigureAwait(false)).ConfigureAwait(false);

            // Delete a seeded standard class and the standard profile.
            QosTrafficClass realtime = await Database.QosTrafficClass.ReadByNameAsync(TestTenantId, "realtime").ConfigureAwait(false);
            await Database.QosTrafficClass.DeleteAsync(TestTenantId, realtime.Id).ConfigureAwait(false);

            EnumerationResult<QosProfile> before = await Database.QosProfile.EnumerateAsync(TestTenantId, new EnumerationRequest { MaxResults = 100 }).ConfigureAwait(false);
            QosProfile standard = before.Data.Find(p => p.Name == QosProfileFactory.StandardProfileName);
            await Database.QosProfile.DeleteAsync(TestTenantId, standard.Id).ConfigureAwait(false);

            // Re-run with the persisted marker: nothing is resurrected.
            await seeder.EnsureTenantAsync(await Database.Tenant.ReadAsync(TestTenantId).ConfigureAwait(false)).ConfigureAwait(false);

            (await Database.QosTrafficClass.ReadByNameAsync(TestTenantId, "realtime").ConfigureAwait(false)).Should().BeNull();
            EnumerationResult<QosProfile> after = await Database.QosProfile.EnumerateAsync(TestTenantId, new EnumerationRequest { MaxResults = 100 }).ConfigureAwait(false);
            after.Data.Should().NotContain(p => p.Name == QosProfileFactory.StandardProfileName);
        }

        public async Task EnsureTenant_BackfillsRunnersWithoutProfile()
        {
            VirtualModelRunner vmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner { TenantId = TestTenantId, Name = "seed-vmr", BasePath = "/v1.0/api/seed-backfill/" }).ConfigureAwait(false);

            QosProfile def = await new QosSeeder(Database).EnsureTenantAsync(await Database.Tenant.ReadAsync(TestTenantId).ConfigureAwait(false)).ConfigureAwait(false);

            VirtualModelRunner reread = await Database.VirtualModelRunner.ReadByIdAsync(vmr.Id).ConfigureAwait(false);
            reread.QosProfileId.Should().Be(def.Id);
        }

        public async Task EnsureTenant_AlwaysRecreatesDeletedDefault()
        {
            QosSeeder seeder = new QosSeeder(Database);
            await seeder.EnsureTenantAsync(await Database.Tenant.ReadAsync(TestTenantId).ConfigureAwait(false)).ConfigureAwait(false);

            QosProfile def = await Database.QosProfile.ReadDefaultAsync(TestTenantId).ConfigureAwait(false);
            await Database.QosProfile.DeleteAsync(TestTenantId, def.Id).ConfigureAwait(false);
            (await Database.QosProfile.ReadDefaultAsync(TestTenantId).ConfigureAwait(false)).Should().BeNull();

            // The default is ensured on every call even after the marker is set.
            await seeder.EnsureTenantAsync(await Database.Tenant.ReadAsync(TestTenantId).ConfigureAwait(false)).ConfigureAwait(false);
            (await Database.QosProfile.ReadDefaultAsync(TestTenantId).ConfigureAwait(false)).Should().NotBeNull();
        }
    }
}
