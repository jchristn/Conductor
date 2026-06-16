namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Virtual model runner reservation controller.
    /// </summary>
    public class VirtualModelRunnerReservationController : BaseController
    {
        private readonly VirtualModelRunnerReservationService _ReservationService;

        /// <summary>
        /// Instantiate the controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="reservationService">Reservation service.</param>
        public VirtualModelRunnerReservationController(
            DatabaseDriverBase database,
            AuthenticationService authService,
            Serializer serializer,
            LoggingModule logging,
            VirtualModelRunnerReservationService reservationService)
            : base(database, authService, serializer, logging)
        {
            _ReservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
        }

        /// <summary>
        /// Create a reservation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="reservation">Reservation.</param>
        /// <param name="auth">Authentication result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created reservation.</returns>
        public async Task<VirtualModelRunnerReservation> Create(
            string tenantId,
            VirtualModelRunnerReservation reservation,
            AuthenticationResult auth,
            CancellationToken token = default)
        {
            if (reservation == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(tenantId))
                throw new WebserverException(ApiResultEnum.BadRequest, "TenantId is required");

            reservation.Id = IdGenerator.NewVirtualModelRunnerReservationId();
            reservation.TenantId = tenantId;
            reservation.CreatedByUserId = auth?.User?.Id;
            reservation.CreatedByCredentialId = auth?.Credential?.Id;

            ResourceValidationResult validation = await Validate(tenantId, reservation, null, token).ConfigureAwait(false);
            ThrowIfInvalid(validation);

            VirtualModelRunnerReservation created = await Database.VirtualModelRunnerReservation.CreateAsync(reservation, token).ConfigureAwait(false);
            Logging.Info("[VirtualModelRunnerReservationController] created reservation tenant=" + tenantId + " vmr=" + created.VirtualModelRunnerId + " reservation=" + created.Id);
            return created;
        }

        /// <summary>
        /// Read a reservation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Reservation identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Reservation.</returns>
        public async Task<VirtualModelRunnerReservation> Read(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId))
                throw new WebserverException(ApiResultEnum.BadRequest, "TenantId is required");
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            VirtualModelRunnerReservation reservation = await Database.VirtualModelRunnerReservation.ReadAsync(tenantId, id, token).ConfigureAwait(false);
            if (reservation == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return reservation;
        }

        /// <summary>
        /// Update a reservation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Reservation identifier.</param>
        /// <param name="reservation">Reservation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated reservation.</returns>
        public async Task<VirtualModelRunnerReservation> Update(
            string tenantId,
            string id,
            VirtualModelRunnerReservation reservation,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId))
                throw new WebserverException(ApiResultEnum.BadRequest, "TenantId is required");
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");
            if (reservation == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            VirtualModelRunnerReservation existing = await Database.VirtualModelRunnerReservation.ReadAsync(tenantId, id, token).ConfigureAwait(false);
            if (existing == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            reservation.Id = id;
            reservation.TenantId = tenantId;
            reservation.CreatedUtc = existing.CreatedUtc;
            if (String.IsNullOrEmpty(reservation.CreatedByUserId))
            {
                reservation.CreatedByUserId = existing.CreatedByUserId;
            }
            if (String.IsNullOrEmpty(reservation.CreatedByCredentialId))
            {
                reservation.CreatedByCredentialId = existing.CreatedByCredentialId;
            }

            ResourceValidationResult validation = await Validate(tenantId, reservation, id, token).ConfigureAwait(false);
            ThrowIfInvalid(validation);

            VirtualModelRunnerReservation updated = await Database.VirtualModelRunnerReservation.UpdateAsync(reservation, token).ConfigureAwait(false);
            Logging.Info("[VirtualModelRunnerReservationController] updated reservation tenant=" + tenantId + " vmr=" + updated.VirtualModelRunnerId + " reservation=" + updated.Id);
            return updated;
        }

        /// <summary>
        /// Deactivate a reservation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Reservation identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task Delete(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId))
                throw new WebserverException(ApiResultEnum.BadRequest, "TenantId is required");
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            VirtualModelRunnerReservation existing = await Database.VirtualModelRunnerReservation.ReadAsync(tenantId, id, token).ConfigureAwait(false);
            if (existing == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            await Database.VirtualModelRunnerReservation.DeactivateAsync(tenantId, id, token).ConfigureAwait(false);
            Logging.Info("[VirtualModelRunnerReservationController] deactivated reservation tenant=" + tenantId + " vmr=" + existing.VirtualModelRunnerId + " reservation=" + id);
        }

        /// <summary>
        /// List reservations.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Reservation result.</returns>
        public async Task<EnumerationResult<VirtualModelRunnerReservation>> Enumerate(VirtualModelRunnerReservationFilter filter, CancellationToken token = default)
        {
            if (filter == null) filter = new VirtualModelRunnerReservationFilter();

            return await Database.VirtualModelRunnerReservation.EnumerateAsync(filter, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Validate a reservation draft.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="reservation">Reservation.</param>
        /// <param name="existingId">Existing reservation identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Validation result.</returns>
        public async Task<ResourceValidationResult> Validate(
            string tenantId,
            VirtualModelRunnerReservation reservation,
            string existingId = null,
            CancellationToken token = default)
        {
            return await _ReservationService.ValidateAsync(tenantId, reservation, existingId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluate reservation access for a VMR and candidate identity.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="virtualModelRunnerId">Virtual model runner identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="credentialId">Credential identifier.</param>
        /// <param name="atUtc">Evaluation timestamp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Evaluation result.</returns>
        public async Task<ReservationEvaluationResult> EvaluateEffectiveAsync(
            string tenantId,
            string virtualModelRunnerId,
            string userId,
            string credentialId,
            DateTime? atUtc,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId))
                throw new WebserverException(ApiResultEnum.BadRequest, "TenantId is required");
            if (String.IsNullOrEmpty(virtualModelRunnerId))
                throw new WebserverException(ApiResultEnum.BadRequest, "VirtualModelRunnerId is required");

            string credentialOwnerUserId = null;
            if (!String.IsNullOrEmpty(credentialId))
            {
                Credential credential = await Database.Credential.ReadAsync(tenantId, credentialId, token).ConfigureAwait(false);
                if (credential == null)
                    throw new WebserverException(ApiResultEnum.BadRequest, "CredentialId must reference a credential in the same tenant.");
                credentialOwnerUserId = credential.UserId;
            }

            if (!String.IsNullOrEmpty(userId))
            {
                UserMaster user = await Database.User.ReadAsync(tenantId, userId, token).ConfigureAwait(false);
                if (user == null)
                    throw new WebserverException(ApiResultEnum.BadRequest, "UserId must reference a user in the same tenant.");
            }

            return await _ReservationService.EvaluateAsync(new ReservationEvaluationContext
            {
                TenantId = tenantId,
                VirtualModelRunnerId = virtualModelRunnerId,
                UserId = userId,
                CredentialId = credentialId,
                CredentialOwnerUserId = credentialOwnerUserId,
                AtUtc = atUtc ?? DateTime.UtcNow
            }, token).ConfigureAwait(false);
        }

        private static void ThrowIfInvalid(ResourceValidationResult validation)
        {
            if (validation == null || validation.IsValid)
            {
                return;
            }

            List<string> messages = new List<string>();
            foreach (ResourceValidationIssue issue in validation.Errors)
            {
                messages.Add(issue.Message);
            }

            throw new WebserverException(ApiResultEnum.BadRequest, String.Join(" ", messages));
        }
    }
}
