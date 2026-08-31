namespace Conductor.Core.Models
{
    /// <summary>
    /// The confirmation body for a tenant purge (nuke). The caller must echo the tenant id to confirm
    /// the destructive operation.
    /// </summary>
    public class TenantPurgeRequest
    {
        /// <summary>
        /// The tenant id the caller is confirming for deletion. Must equal the path tenant id. Nullable.
        /// </summary>
        public string ConfirmTenantId { get; set; } = null;
    }
}
