namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;

    /// <summary>
    /// SQLite integration tests for VMR reservation persistence.
    /// </summary>
    public class VirtualModelRunnerReservationDatabaseTests : ControllerTestBase
    {
        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task Reservation_CreateAndRead_RoundTripsSubjectsAndMetadata()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Reservations DB Round Trip").ConfigureAwait(false);
            UserMaster user = await CreateUserAsync("db-round-trip").ConfigureAwait(false);
            DateTime start = DateTime.UtcNow.AddHours(1);

            VirtualModelRunnerReservation created = await Database.VirtualModelRunnerReservation.CreateAsync(new VirtualModelRunnerReservation
            {
                TenantId = TestTenantId,
                VirtualModelRunnerId = vmr.Id,
                Name = "Round Trip Reservation",
                Description = "Dedicated run",
                StartUtc = start,
                EndUtc = start.AddHours(2),
                AdmissionDrainLeadMs = 120000,
                Labels = new List<string> { "benchmark" },
                Tags = new Dictionary<string, string> { ["purpose"] = "test" },
                Subjects = new List<VirtualModelRunnerReservationSubject>
                {
                    new VirtualModelRunnerReservationSubject
                    {
                        SubjectType = ReservationSubjectTypeEnum.User,
                        SubjectId = user.Id
                    }
                }
            }).ConfigureAwait(false);

            VirtualModelRunnerReservation read = await Database.VirtualModelRunnerReservation.ReadAsync(TestTenantId, created.Id).ConfigureAwait(false);

            read.Should().NotBeNull();
            read.Id.Should().Be(created.Id);
            read.VirtualModelRunnerId.Should().Be(vmr.Id);
            read.Name.Should().Be("Round Trip Reservation");
            read.Description.Should().Be("Dedicated run");
            read.AdmissionDrainLeadMs.Should().Be(120000);
            read.Labels.Should().Contain("benchmark");
            read.Tags.Should().ContainKey("purpose").WhoseValue.Should().Be("test");
            read.Subjects.Should().ContainSingle(item => item.SubjectType == ReservationSubjectTypeEnum.User && item.SubjectId == user.Id);
        }

        public async Task Reservation_Enumerate_WithSubjectFilter_ReturnsMatchingReservation()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Reservations Subject Filter").ConfigureAwait(false);
            UserMaster included = await CreateUserAsync("included-subject").ConfigureAwait(false);
            UserMaster excluded = await CreateUserAsync("excluded-subject").ConfigureAwait(false);
            DateTime start = DateTime.UtcNow.AddHours(3);

            await CreateReservationAsync(vmr.Id, "Included", included.Id, start).ConfigureAwait(false);
            await CreateReservationAsync(vmr.Id, "Excluded", excluded.Id, start.AddHours(2)).ConfigureAwait(false);

            EnumerationResult<VirtualModelRunnerReservation> result = await Database.VirtualModelRunnerReservation.EnumerateAsync(new VirtualModelRunnerReservationFilter
            {
                TenantId = TestTenantId,
                VirtualModelRunnerId = vmr.Id,
                SubjectType = ReservationSubjectTypeEnum.User,
                SubjectId = included.Id,
                MaxResults = 10
            }).ConfigureAwait(false);

            result.TotalCount.Should().Be(1);
            result.Data.Should().ContainSingle(item => item.Name == "Included");
            result.Data[0].Subjects.Should().ContainSingle(item => item.SubjectId == included.Id);
        }

        public async Task Reservation_Enumerate_WithoutTenantFilter_ReturnsAllTenantReservations()
        {
            VirtualModelRunner firstTenantVmr = await CreateVmrAsync("Reservations Global First Tenant").ConfigureAwait(false);
            UserMaster firstTenantUser = await CreateUserAsync("global-first-subject").ConfigureAwait(false);
            VirtualModelRunnerReservation firstTenantReservation = await CreateReservationAsync(
                firstTenantVmr.Id,
                "First Tenant Reservation",
                firstTenantUser.Id,
                DateTime.UtcNow.AddHours(3)).ConfigureAwait(false);

            TenantMetadata secondTenant = await Database.Tenant.CreateAsync(new TenantMetadata
            {
                Name = "Second Reservation Tenant",
                Active = true
            }).ConfigureAwait(false);

            VirtualModelRunner secondTenantVmr = await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = secondTenant.Id,
                Name = "Reservations Global Second Tenant",
                BasePath = "/reservations/" + Guid.NewGuid().ToString("N") + "/"
            }).ConfigureAwait(false);
            UserMaster secondTenantUser = await Database.User.CreateAsync(new UserMaster
            {
                TenantId = secondTenant.Id,
                FirstName = "Reservation",
                LastName = "User",
                Email = "global-second-" + Guid.NewGuid().ToString("N") + "@example.com",
                Password = "password",
                Active = true
            }).ConfigureAwait(false);
            VirtualModelRunnerReservation secondTenantReservation = await Database.VirtualModelRunnerReservation.CreateAsync(new VirtualModelRunnerReservation
            {
                TenantId = secondTenant.Id,
                VirtualModelRunnerId = secondTenantVmr.Id,
                Name = "Second Tenant Reservation",
                StartUtc = DateTime.UtcNow.AddHours(5),
                EndUtc = DateTime.UtcNow.AddHours(6),
                Subjects = new List<VirtualModelRunnerReservationSubject>
                {
                    new VirtualModelRunnerReservationSubject
                    {
                        SubjectType = ReservationSubjectTypeEnum.User,
                        SubjectId = secondTenantUser.Id
                    }
                }
            }).ConfigureAwait(false);

            EnumerationResult<VirtualModelRunnerReservation> result = await Database.VirtualModelRunnerReservation.EnumerateAsync(new VirtualModelRunnerReservationFilter
            {
                MaxResults = 10
            }).ConfigureAwait(false);

            result.Data.Should().Contain(item => item.Id == firstTenantReservation.Id && item.TenantId == TestTenantId);
            result.Data.Should().Contain(item => item.Id == secondTenantReservation.Id && item.TenantId == secondTenant.Id);
            result.Data.Single(item => item.Id == secondTenantReservation.Id).Subjects.Should().ContainSingle(item => item.SubjectId == secondTenantUser.Id);
        }

        public async Task Reservation_CountOverlaps_ExcludesInactiveAndExcludedReservation()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Reservations Overlap").ConfigureAwait(false);
            UserMaster user = await CreateUserAsync("overlap-subject").ConfigureAwait(false);
            DateTime start = DateTime.UtcNow.AddHours(6);

            VirtualModelRunnerReservation active = await CreateReservationAsync(vmr.Id, "Active", user.Id, start).ConfigureAwait(false);
            VirtualModelRunnerReservation inactive = await CreateReservationAsync(vmr.Id, "Inactive", user.Id, start.AddMinutes(15)).ConfigureAwait(false);
            await Database.VirtualModelRunnerReservation.DeactivateAsync(TestTenantId, inactive.Id).ConfigureAwait(false);

            int overlap = await Database.VirtualModelRunnerReservation.CountOverlapsAsync(TestTenantId, vmr.Id, start.AddMinutes(30), start.AddMinutes(45)).ConfigureAwait(false);
            int excluded = await Database.VirtualModelRunnerReservation.CountOverlapsAsync(TestTenantId, vmr.Id, start.AddMinutes(30), start.AddMinutes(45), active.Id).ConfigureAwait(false);

            overlap.Should().Be(1);
            excluded.Should().Be(0);
        }

        public async Task Reservation_ListActive_IncludesDrainWindowWhenRequested()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Reservations Drain").ConfigureAwait(false);
            UserMaster user = await CreateUserAsync("drain-subject").ConfigureAwait(false);
            DateTime start = DateTime.UtcNow.AddMinutes(30);
            VirtualModelRunnerReservation reservation = await CreateReservationAsync(vmr.Id, "Drain", user.Id, start, 600000).ConfigureAwait(false);

            List<VirtualModelRunnerReservation> withoutDrain = await Database.VirtualModelRunnerReservation.ListActiveForVirtualModelRunnerAsync(
                TestTenantId,
                vmr.Id,
                start.AddMinutes(-5),
                false).ConfigureAwait(false);
            List<VirtualModelRunnerReservation> withDrain = await Database.VirtualModelRunnerReservation.ListActiveForVirtualModelRunnerAsync(
                TestTenantId,
                vmr.Id,
                start.AddMinutes(-5),
                true).ConfigureAwait(false);

            withoutDrain.Should().BeEmpty();
            withDrain.Should().ContainSingle(item => item.Id == reservation.Id);
            withDrain[0].Subjects.Should().ContainSingle(item => item.SubjectId == user.Id);
        }

        private async Task<VirtualModelRunner> CreateVmrAsync(string name)
        {
            return await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = name,
                BasePath = "/reservations/" + Guid.NewGuid().ToString("N") + "/"
            }).ConfigureAwait(false);
        }

        private async Task<UserMaster> CreateUserAsync(string prefix)
        {
            return await Database.User.CreateAsync(new UserMaster
            {
                TenantId = TestTenantId,
                FirstName = "Reservation",
                LastName = "User",
                Email = prefix + "-" + Guid.NewGuid().ToString("N") + "@example.com",
                Password = "password",
                Active = true
            }).ConfigureAwait(false);
        }

        private async Task<VirtualModelRunnerReservation> CreateReservationAsync(
            string vmrId,
            string name,
            string userId,
            DateTime startUtc,
            int admissionDrainLeadMs = 0)
        {
            return await Database.VirtualModelRunnerReservation.CreateAsync(new VirtualModelRunnerReservation
            {
                TenantId = TestTenantId,
                VirtualModelRunnerId = vmrId,
                Name = name,
                StartUtc = startUtc,
                EndUtc = startUtc.AddHours(1),
                AdmissionDrainLeadMs = admissionDrainLeadMs,
                Subjects = new List<VirtualModelRunnerReservationSubject>
                {
                    new VirtualModelRunnerReservationSubject
                    {
                        SubjectType = ReservationSubjectTypeEnum.User,
                        SubjectId = userId
                    }
                }
            }).ConfigureAwait(false);
        }
    }
}
