namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// Service for validating and evaluating VMR reservations.
    /// </summary>
    public class VirtualModelRunnerReservationService
    {
        private const string _Header = "[VirtualModelRunnerReservationService] ";
        private const int _MaximumAdmissionDrainLeadMs = 86400000;

        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public VirtualModelRunnerReservationService(DatabaseDriverBase database, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Validate a reservation draft.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="reservation">Reservation draft.</param>
        /// <param name="existingId">Existing reservation identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Validation result.</returns>
        public async Task<ResourceValidationResult> ValidateAsync(
            string tenantId,
            VirtualModelRunnerReservation reservation,
            string existingId = null,
            CancellationToken token = default)
        {
            ResourceValidationResult result = new ResourceValidationResult
            {
                ResourceType = "VirtualModelRunnerReservation",
                IsValid = true
            };

            if (String.IsNullOrEmpty(tenantId))
            {
                AddError(result, "TenantRequired", "TenantId", "TenantId is required.");
                return Finalize(result);
            }

            if (reservation == null)
            {
                AddError(result, "ReservationRequired", "Reservation", "Reservation body is required.");
                return Finalize(result);
            }

            reservation.TenantId = tenantId;
            ValidateRequiredFields(result, reservation);
            await ValidateReferencesAsync(result, tenantId, reservation, token).ConfigureAwait(false);
            await ValidateSubjectsAsync(result, tenantId, reservation, token).ConfigureAwait(false);
            await ValidateOverlapAsync(result, tenantId, reservation, existingId, token).ConfigureAwait(false);

            return Finalize(result);
        }

        /// <summary>
        /// Evaluate a request against active reservations.
        /// </summary>
        /// <param name="context">Evaluation context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Evaluation result.</returns>
        public async Task<ReservationEvaluationResult> EvaluateAsync(ReservationEvaluationContext context, CancellationToken token = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (String.IsNullOrEmpty(context.TenantId)) throw new ArgumentNullException(nameof(context.TenantId));
            if (String.IsNullOrEmpty(context.VirtualModelRunnerId)) throw new ArgumentNullException(nameof(context.VirtualModelRunnerId));

            List<VirtualModelRunnerReservation> reservations = await _Database.VirtualModelRunnerReservation
                .ListActiveForVirtualModelRunnerAsync(context.TenantId, context.VirtualModelRunnerId, context.AtUtc, true, token)
                .ConfigureAwait(false);

            if (reservations == null || reservations.Count < 1)
            {
                return new ReservationEvaluationResult
                {
                    HasReservation = false,
                    Allowed = true,
                    Decision = ReservationDecisionEnum.NoReservation,
                    ReasonCode = "NoReservation",
                    ReasonText = "No active reservation applies."
                };
            }

            if (reservations.Count > 1)
            {
                ReservationEvaluationResult conflict = CreateResult(reservations[0], context.AtUtc);
                conflict.Allowed = false;
                conflict.Decision = ReservationDecisionEnum.Conflict;
                conflict.ReasonCode = "ReservationConflict";
                conflict.ReasonText = "Multiple active reservations apply to the virtual model runner.";
                _Logging.Warn(_Header + "reservation conflict tenant=" + context.TenantId + " vmr=" + context.VirtualModelRunnerId + " count=" + reservations.Count);
                return conflict;
            }

            VirtualModelRunnerReservation reservation = reservations[0];
            ReservationEvaluationResult result = CreateResult(reservation, context.AtUtc);

            if (String.IsNullOrEmpty(context.UserId)
                && String.IsNullOrEmpty(context.CredentialId)
                && String.IsNullOrEmpty(context.CredentialOwnerUserId))
            {
                result.Allowed = false;
                result.Decision = ReservationDecisionEnum.AuthenticationRequired;
                result.ReasonCode = "ReservationAuthenticationRequired";
                result.ReasonText = "An authenticated user or credential is required because this virtual model runner is reserved.";
                return result;
            }

            VirtualModelRunnerReservationSubject matched = FindMatchingSubject(reservation, context);
            if (matched != null)
            {
                result.Allowed = true;
                result.Decision = ReservationDecisionEnum.Allowed;
                result.ReasonCode = "ReservationParticipant";
                result.ReasonText = "The request identity is included in the reservation.";
                result.MatchedSubjectType = matched.SubjectType;
                result.MatchedSubjectId = matched.SubjectId;
                return result;
            }

            result.Allowed = false;
            result.Decision = ReservationDecisionEnum.Denied;
            result.ReasonCode = result.InDrainWindow ? "ReservationDrainDenied" : "ReservationDenied";
            result.ReasonText = result.InDrainWindow
                ? "The virtual model runner is entering a reserved window and the request identity is not included in the reservation."
                : "The virtual model runner has an active reservation and the request identity is not included in the reservation.";

            _Logging.Warn(
                _Header
                + "reservation denied tenant=" + context.TenantId
                + " vmr=" + context.VirtualModelRunnerId
                + " reservation=" + reservation.Id
                + " user=" + (context.UserId ?? String.Empty)
                + " credential=" + (context.CredentialId ?? String.Empty)
                + " startUtc=" + reservation.StartUtc.ToString("O")
                + " endUtc=" + reservation.EndUtc.ToString("O")
                + " reason=" + result.ReasonCode);

            return result;
        }

        private static ReservationEvaluationResult CreateResult(VirtualModelRunnerReservation reservation, DateTime atUtc)
        {
            bool inActiveWindow = reservation.StartUtc <= atUtc && atUtc < reservation.EndUtc;
            bool inDrainWindow = reservation.AdmissionDrainLeadMs > 0
                && reservation.StartUtc.AddMilliseconds(-reservation.AdmissionDrainLeadMs) <= atUtc
                && atUtc < reservation.StartUtc;

            return new ReservationEvaluationResult
            {
                HasReservation = true,
                InActiveWindow = inActiveWindow,
                InDrainWindow = inDrainWindow,
                Allowed = false,
                Decision = ReservationDecisionEnum.Denied,
                ReservationId = reservation.Id,
                ReservationName = reservation.Name,
                StartUtc = reservation.StartUtc,
                EndUtc = reservation.EndUtc
            };
        }

        private static VirtualModelRunnerReservationSubject FindMatchingSubject(
            VirtualModelRunnerReservation reservation,
            ReservationEvaluationContext context)
        {
            if (reservation.Subjects == null) return null;

            foreach (VirtualModelRunnerReservationSubject subject in reservation.Subjects)
            {
                if (subject == null) continue;

                if (subject.SubjectType == ReservationSubjectTypeEnum.Credential
                    && !String.IsNullOrEmpty(context.CredentialId)
                    && String.Equals(subject.SubjectId, context.CredentialId, StringComparison.Ordinal))
                {
                    return subject;
                }

                if (subject.SubjectType == ReservationSubjectTypeEnum.User)
                {
                    if (!String.IsNullOrEmpty(context.UserId)
                        && String.Equals(subject.SubjectId, context.UserId, StringComparison.Ordinal))
                    {
                        return subject;
                    }

                    if (!String.IsNullOrEmpty(context.CredentialOwnerUserId)
                        && String.Equals(subject.SubjectId, context.CredentialOwnerUserId, StringComparison.Ordinal))
                    {
                        return subject;
                    }
                }
            }

            return null;
        }

        private static void ValidateRequiredFields(ResourceValidationResult result, VirtualModelRunnerReservation reservation)
        {
            if (String.IsNullOrWhiteSpace(reservation.Name))
            {
                AddError(result, "NameRequired", "Name", "Name is required.");
            }

            if (String.IsNullOrWhiteSpace(reservation.VirtualModelRunnerId))
            {
                AddError(result, "VirtualModelRunnerRequired", "VirtualModelRunnerId", "VirtualModelRunnerId is required.");
            }

            if (reservation.StartUtc >= reservation.EndUtc)
            {
                AddError(result, "InvalidWindow", "StartUtc", "StartUtc must be earlier than EndUtc.");
            }

            if (reservation.AdmissionDrainLeadMs > _MaximumAdmissionDrainLeadMs)
            {
                AddError(result, "AdmissionDrainLeadTooLarge", "AdmissionDrainLeadMs", "AdmissionDrainLeadMs must not exceed 86400000.");
            }

            if (reservation.Subjects == null || reservation.Subjects.Count < 1)
            {
                AddError(result, "SubjectsRequired", "Subjects", "At least one user or credential subject is required.");
            }
        }

        private async Task ValidateReferencesAsync(ResourceValidationResult result, string tenantId, VirtualModelRunnerReservation reservation, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(reservation.VirtualModelRunnerId))
            {
                return;
            }

            VirtualModelRunner vmr = await _Database.VirtualModelRunner.ReadAsync(tenantId, reservation.VirtualModelRunnerId, token).ConfigureAwait(false);
            if (vmr == null)
            {
                AddError(result, "VirtualModelRunnerNotFound", "VirtualModelRunnerId", "VirtualModelRunnerId must reference a VMR in the same tenant.");
            }
            else if (!vmr.Active)
            {
                AddError(result, "VirtualModelRunnerInactive", "VirtualModelRunnerId", "VirtualModelRunnerId must reference an active VMR.");
            }
        }

        private async Task ValidateSubjectsAsync(ResourceValidationResult result, string tenantId, VirtualModelRunnerReservation reservation, CancellationToken token)
        {
            if (reservation.Subjects == null) return;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (VirtualModelRunnerReservationSubject subject in reservation.Subjects)
            {
                if (subject == null)
                {
                    AddError(result, "SubjectRequired", "Subjects", "Subject entries cannot be null.");
                    continue;
                }

                string key = ((int)subject.SubjectType).ToString() + ":" + (subject.SubjectId ?? String.Empty);
                if (!seen.Add(key))
                {
                    AddError(result, "DuplicateSubject", "Subjects", "Reservation subjects cannot contain duplicates.");
                    continue;
                }

                if (String.IsNullOrWhiteSpace(subject.SubjectId))
                {
                    AddError(result, "SubjectIdRequired", "Subjects", "SubjectId is required.");
                    continue;
                }

                if (subject.SubjectType == ReservationSubjectTypeEnum.User)
                {
                    UserMaster user = await _Database.User.ReadAsync(tenantId, subject.SubjectId, token).ConfigureAwait(false);
                    if (user == null)
                    {
                        AddError(result, "UserSubjectNotFound", "Subjects", "User subject must exist in the same tenant.");
                    }
                    else if (!user.Active)
                    {
                        AddError(result, "UserSubjectInactive", "Subjects", "User subject must be active.");
                    }
                }
                else if (subject.SubjectType == ReservationSubjectTypeEnum.Credential)
                {
                    Credential credential = await _Database.Credential.ReadAsync(tenantId, subject.SubjectId, token).ConfigureAwait(false);
                    if (credential == null)
                    {
                        AddError(result, "CredentialSubjectNotFound", "Subjects", "Credential subject must exist in the same tenant.");
                    }
                    else if (!credential.Active)
                    {
                        AddError(result, "CredentialSubjectInactive", "Subjects", "Credential subject must be active.");
                    }
                }
                else
                {
                    AddError(result, "SubjectTypeInvalid", "Subjects", "SubjectType must be User or Credential.");
                }
            }
        }

        private async Task ValidateOverlapAsync(ResourceValidationResult result, string tenantId, VirtualModelRunnerReservation reservation, string existingId, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(reservation.VirtualModelRunnerId) || reservation.StartUtc >= reservation.EndUtc)
            {
                return;
            }

            int overlaps = await _Database.VirtualModelRunnerReservation
                .CountOverlapsAsync(tenantId, reservation.VirtualModelRunnerId, reservation.StartUtc, reservation.EndUtc, existingId, token)
                .ConfigureAwait(false);
            if (overlaps > 0)
            {
                AddError(result, "ReservationOverlap", "StartUtc", "Active reservations for the same VMR cannot overlap.");
            }
        }

        private static ResourceValidationResult Finalize(ResourceValidationResult result)
        {
            result.IsValid = result.Errors == null || result.Errors.Count < 1;
            return result;
        }

        private static void AddError(ResourceValidationResult result, string code, string field, string message)
        {
            result.Errors.Add(new ResourceValidationIssue
            {
                Code = code,
                Field = field,
                Message = message
            });
        }
    }
}
