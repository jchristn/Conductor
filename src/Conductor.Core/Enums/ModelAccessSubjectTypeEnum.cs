namespace Conductor.Core.Enums
{
    /// <summary>
    /// Subject type matched by a model access policy rule.
    /// </summary>
    public enum ModelAccessSubjectTypeEnum
    {
        /// <summary>
        /// Match a credential identifier.
        /// </summary>
        Credential = 0,

        /// <summary>
        /// Match labels assigned to a credential.
        /// </summary>
        CredentialLabel = 1,

        /// <summary>
        /// Match a user identifier.
        /// </summary>
        User = 2,

        /// <summary>
        /// Match labels assigned to a user.
        /// </summary>
        UserLabel = 3,

        /// <summary>
        /// Match a tenant identifier.
        /// </summary>
        Tenant = 4,

        /// <summary>
        /// Match any subject within the policy tenant.
        /// </summary>
        Any = 5
    }
}
