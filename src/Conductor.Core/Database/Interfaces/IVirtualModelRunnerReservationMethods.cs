namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for virtual model runner reservation database methods.
    /// </summary>
    public interface IVirtualModelRunnerReservationMethods
    {
        /// <summary>
        /// Create a reservation and its subjects.
        /// </summary>
        /// <param name="reservation">Reservation to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created reservation.</returns>
        Task<VirtualModelRunnerReservation> CreateAsync(VirtualModelRunnerReservation reservation, CancellationToken token = default);

        /// <summary>
        /// Read a reservation by tenant and identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Reservation identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Reservation or null.</returns>
        Task<VirtualModelRunnerReservation> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Update a reservation and replace its subjects.
        /// </summary>
        /// <param name="reservation">Reservation to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated reservation.</returns>
        Task<VirtualModelRunnerReservation> UpdateAsync(VirtualModelRunnerReservation reservation, CancellationToken token = default);

        /// <summary>
        /// Deactivate a reservation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Reservation identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeactivateAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate reservations by filter.
        /// </summary>
        /// <param name="filter">Reservation filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Reservation enumeration result.</returns>
        Task<EnumerationResult<VirtualModelRunnerReservation>> EnumerateAsync(VirtualModelRunnerReservationFilter filter, CancellationToken token = default);

        /// <summary>
        /// List subjects for a reservation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="reservationId">Reservation identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Subjects.</returns>
        Task<List<VirtualModelRunnerReservationSubject>> ListSubjectsAsync(string tenantId, string reservationId, CancellationToken token = default);

        /// <summary>
        /// Replace subjects for a reservation.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="reservationId">Reservation identifier.</param>
        /// <param name="subjects">Replacement subjects.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceSubjectsAsync(string tenantId, string reservationId, List<VirtualModelRunnerReservationSubject> subjects, CancellationToken token = default);

        /// <summary>
        /// List reservations active at a given time for a VMR.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="virtualModelRunnerId">Virtual model runner identifier.</param>
        /// <param name="atUtc">Evaluation timestamp.</param>
        /// <param name="includeDrainWindow">Whether pre-start drain windows should be included.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matching reservations.</returns>
        Task<List<VirtualModelRunnerReservation>> ListActiveForVirtualModelRunnerAsync(
            string tenantId,
            string virtualModelRunnerId,
            DateTime atUtc,
            bool includeDrainWindow,
            CancellationToken token = default);

        /// <summary>
        /// Count active overlapping reservations.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="virtualModelRunnerId">Virtual model runner identifier.</param>
        /// <param name="startUtc">Start timestamp.</param>
        /// <param name="endUtc">End timestamp.</param>
        /// <param name="excludeReservationId">Reservation to exclude.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of overlaps.</returns>
        Task<int> CountOverlapsAsync(
            string tenantId,
            string virtualModelRunnerId,
            DateTime startUtc,
            DateTime endUtc,
            string excludeReservationId = null,
            CancellationToken token = default);
    }
}
