namespace Conductor.Core.Models
{
    using System;
    using Conductor.Core.Enums;

    /// <summary>
    /// Context used to evaluate reservation access.
    /// </summary>
    public class ReservationEvaluationContext
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Virtual model runner identifier.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Evaluation time.
        /// </summary>
        public DateTime AtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Request user identifier.
        /// </summary>
        public string UserId { get; set; } = null;

        /// <summary>
        /// Request credential identifier.
        /// </summary>
        public string CredentialId { get; set; } = null;

        /// <summary>
        /// Request credential owner user identifier.
        /// </summary>
        public string CredentialOwnerUserId { get; set; } = null;

        /// <summary>
        /// Request type.
        /// </summary>
        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;
    }
}
