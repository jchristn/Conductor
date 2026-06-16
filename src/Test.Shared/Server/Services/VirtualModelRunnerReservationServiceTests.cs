namespace Test.Shared.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server.Services;
    using FluentAssertions;
    using Test.Shared.Server.Controllers;

    /// <summary>
    /// Tests for reservation validation and admission decisions.
    /// </summary>
    public class VirtualModelRunnerReservationServiceTests : ControllerTestBase
    {
        private VirtualModelRunnerReservationService _Service;

        public async Task InitializeAsync()
        {
            await InitializeDatabaseAsync().ConfigureAwait(false);
            _Service = new VirtualModelRunnerReservationService(Database, Logging);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ValidateAsync_WithValidReservation_AllowsDraft()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Valid Reservation").ConfigureAwait(false);
            UserMaster user = await CreateUserAsync("valid").ConfigureAwait(false);
            VirtualModelRunnerReservation draft = CreateReservation(vmr.Id, DateTime.UtcNow.AddHours(1), ReservationSubjectTypeEnum.User, user.Id);

            ResourceValidationResult result = await _Service.ValidateAsync(TestTenantId, draft).ConfigureAwait(false);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        public async Task ValidateAsync_WithOverlappingReservation_ReturnsOverlapError()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Overlap Reservation").ConfigureAwait(false);
            UserMaster user = await CreateUserAsync("overlap").ConfigureAwait(false);
            DateTime start = DateTime.UtcNow.AddHours(2);
            await Database.VirtualModelRunnerReservation.CreateAsync(CreateReservation(vmr.Id, start, ReservationSubjectTypeEnum.User, user.Id)).ConfigureAwait(false);
            VirtualModelRunnerReservation overlapping = CreateReservation(vmr.Id, start.AddMinutes(15), ReservationSubjectTypeEnum.User, user.Id);

            ResourceValidationResult result = await _Service.ValidateAsync(TestTenantId, overlapping).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(item => item.Code == "ReservationOverlap");
        }

        public async Task ValidateAsync_WithDuplicateSubjects_ReturnsDuplicateError()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Duplicate Subject Reservation").ConfigureAwait(false);
            UserMaster user = await CreateUserAsync("duplicate").ConfigureAwait(false);
            VirtualModelRunnerReservation draft = CreateReservation(vmr.Id, DateTime.UtcNow.AddHours(4), ReservationSubjectTypeEnum.User, user.Id);
            draft.Subjects.Add(new VirtualModelRunnerReservationSubject
            {
                SubjectType = ReservationSubjectTypeEnum.User,
                SubjectId = user.Id
            });

            ResourceValidationResult result = await _Service.ValidateAsync(TestTenantId, draft).ConfigureAwait(false);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(item => item.Code == "DuplicateSubject");
        }

        public async Task EvaluateAsync_WithNoReservation_AllowsRequest()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("No Reservation").ConfigureAwait(false);

            ReservationEvaluationResult result = await _Service.EvaluateAsync(new ReservationEvaluationContext
            {
                TenantId = TestTenantId,
                VirtualModelRunnerId = vmr.Id
            }).ConfigureAwait(false);

            result.Allowed.Should().BeTrue();
            result.Decision.Should().Be(ReservationDecisionEnum.NoReservation);
            result.HasReservation.Should().BeFalse();
        }

        public async Task EvaluateAsync_WithCredentialParticipant_AllowsRequest()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Credential Reservation").ConfigureAwait(false);
            Credential credential = await CreateCredentialAsync("credential-participant").ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            VirtualModelRunnerReservation reservation = await Database.VirtualModelRunnerReservation.CreateAsync(CreateReservation(vmr.Id, now.AddMinutes(-5), ReservationSubjectTypeEnum.Credential, credential.Id)).ConfigureAwait(false);

            ReservationEvaluationResult result = await _Service.EvaluateAsync(new ReservationEvaluationContext
            {
                TenantId = TestTenantId,
                VirtualModelRunnerId = vmr.Id,
                CredentialId = credential.Id,
                AtUtc = now
            }).ConfigureAwait(false);

            result.Allowed.Should().BeTrue();
            result.Decision.Should().Be(ReservationDecisionEnum.Allowed);
            result.ReservationId.Should().Be(reservation.Id);
            result.MatchedSubjectType.Should().Be(ReservationSubjectTypeEnum.Credential);
            result.MatchedSubjectId.Should().Be(credential.Id);
        }

        public async Task EvaluateAsync_WithNonParticipant_DeniesRequest()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Denied Reservation").ConfigureAwait(false);
            UserMaster reservedUser = await CreateUserAsync("reserved").ConfigureAwait(false);
            Credential outsider = await CreateCredentialAsync("outsider").ConfigureAwait(false);
            DateTime now = DateTime.UtcNow;
            VirtualModelRunnerReservation reservation = await Database.VirtualModelRunnerReservation.CreateAsync(CreateReservation(vmr.Id, now.AddMinutes(-5), ReservationSubjectTypeEnum.User, reservedUser.Id)).ConfigureAwait(false);

            ReservationEvaluationResult result = await _Service.EvaluateAsync(new ReservationEvaluationContext
            {
                TenantId = TestTenantId,
                VirtualModelRunnerId = vmr.Id,
                CredentialId = outsider.Id,
                CredentialOwnerUserId = outsider.UserId,
                AtUtc = now
            }).ConfigureAwait(false);

            result.Allowed.Should().BeFalse();
            result.Decision.Should().Be(ReservationDecisionEnum.Denied);
            result.ReasonCode.Should().Be("ReservationDenied");
            result.ReservationId.Should().Be(reservation.Id);
        }

        public async Task EvaluateAsync_WithDrainWindowNonParticipant_UsesDrainReason()
        {
            VirtualModelRunner vmr = await CreateVmrAsync("Drain Reservation").ConfigureAwait(false);
            UserMaster reservedUser = await CreateUserAsync("drain-reserved").ConfigureAwait(false);
            Credential outsider = await CreateCredentialAsync("drain-outsider").ConfigureAwait(false);
            DateTime start = DateTime.UtcNow.AddMinutes(10);
            await Database.VirtualModelRunnerReservation.CreateAsync(CreateReservation(vmr.Id, start, ReservationSubjectTypeEnum.User, reservedUser.Id, 900000)).ConfigureAwait(false);

            ReservationEvaluationResult result = await _Service.EvaluateAsync(new ReservationEvaluationContext
            {
                TenantId = TestTenantId,
                VirtualModelRunnerId = vmr.Id,
                CredentialId = outsider.Id,
                CredentialOwnerUserId = outsider.UserId,
                AtUtc = start.AddMinutes(-5)
            }).ConfigureAwait(false);

            result.Allowed.Should().BeFalse();
            result.InDrainWindow.Should().BeTrue();
            result.ReasonCode.Should().Be("ReservationDrainDenied");
        }

        private async Task<VirtualModelRunner> CreateVmrAsync(string name)
        {
            return await Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = TestTenantId,
                Name = name,
                BasePath = "/reservation-service/" + Guid.NewGuid().ToString("N") + "/"
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

        private async Task<Credential> CreateCredentialAsync(string prefix)
        {
            UserMaster user = await CreateUserAsync(prefix).ConfigureAwait(false);
            return await Database.Credential.CreateAsync(new Credential
            {
                TenantId = TestTenantId,
                UserId = user.Id,
                Name = prefix + " credential"
            }).ConfigureAwait(false);
        }

        private VirtualModelRunnerReservation CreateReservation(
            string vmrId,
            DateTime startUtc,
            ReservationSubjectTypeEnum subjectType,
            string subjectId,
            int admissionDrainLeadMs = 0)
        {
            return new VirtualModelRunnerReservation
            {
                TenantId = TestTenantId,
                VirtualModelRunnerId = vmrId,
                Name = "Service Reservation " + Guid.NewGuid().ToString("N"),
                StartUtc = startUtc,
                EndUtc = startUtc.AddHours(1),
                AdmissionDrainLeadMs = admissionDrainLeadMs,
                Subjects = new List<VirtualModelRunnerReservationSubject>
                {
                    new VirtualModelRunnerReservationSubject
                    {
                        SubjectType = subjectType,
                        SubjectId = subjectId
                    }
                }
            };
        }
    }
}
