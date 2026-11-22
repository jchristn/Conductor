namespace Conductor.Server.Services
{
    using System;
    using WatsonWebserver.Core;

    /// <summary>
    /// Resolves the tenant a REST request acts on from the caller's authentication and the TenantId the request supplies
    /// (in the body or the tenantId query parameter). All members are static, side-effect free and thread safe.
    /// </summary>
    /// <remarks>
    /// Rules:
    /// <list type="bullet">
    /// <item>A normal or tenant admin user always acts on its own tenant; a supplied TenantId is ignored.</item>
    /// <item>A global admin user (IsAdmin=true) acts on the supplied TenantId. On reads, an omitted TenantId means all tenants
    /// (null). On writes (create, update, delete, validate, service state changes), an omitted TenantId defaults to the
    /// global admin user's own tenant.</item>
    /// <item>A system administrator (x-admin-apikey or admin headers) has no tenant. On reads, an omitted TenantId means all
    /// tenants. On writes, TenantId is required and an omitted value is rejected with 400 and TenantRequiredMessage.</item>
    /// </list>
    /// </remarks>
    public static class RequestTenantResolver
    {
        /// <summary>
        /// Message returned with 400 when a system administrator write omits TenantId.
        /// </summary>
        public const string TenantRequiredMessage = "TenantId is required: system administrator credentials have no tenant, so supply TenantId in the request body (create and update) or the tenantId query parameter (delete and other routes).";

        /// <summary>
        /// Resolve the tenant for a read or list request.
        /// </summary>
        /// <param name="metadata">The authenticated caller (AuthenticationResult or AdminAuthenticationResult). Null allowed.</param>
        /// <param name="requestedTenantId">TenantId supplied by the request. Null allowed.</param>
        /// <returns>The tenant to act on, or null for all tenants (cross-tenant callers only) or an unauthenticated caller.</returns>
        public static string ResolveReadTenantId(object metadata, string requestedTenantId)
        {
            if (metadata is AuthenticationResult userAuth)
            {
                if (userAuth.IsAdmin)
                {
                    return !String.IsNullOrEmpty(requestedTenantId) ? requestedTenantId : null;
                }

                return userAuth.Tenant?.Id;
            }

            if (metadata is AdminAuthenticationResult)
            {
                return requestedTenantId;
            }

            return null;
        }

        /// <summary>
        /// Resolve the tenant for a write request (create, update, delete, validate or a service state change). Never
        /// returns null for an authenticated caller.
        /// </summary>
        /// <param name="metadata">The authenticated caller (AuthenticationResult or AdminAuthenticationResult).</param>
        /// <param name="requestedTenantId">TenantId supplied by the request. Null allowed.</param>
        /// <returns>The tenant to act on. Never null or empty.</returns>
        /// <exception cref="WebserverException">Thrown with BadRequest when a system administrator omits TenantId, or with
        /// NotAuthorized when the caller is not authenticated or has no tenant.</exception>
        public static string ResolveWriteTenantId(object metadata, string requestedTenantId)
        {
            if (metadata is AuthenticationResult userAuth)
            {
                string tenantId = userAuth.IsAdmin && !String.IsNullOrEmpty(requestedTenantId)
                    ? requestedTenantId
                    : userAuth.Tenant?.Id;
                if (String.IsNullOrEmpty(tenantId))
                {
                    throw new WebserverException(ApiResultEnum.NotAuthorized, "The authenticated user has no tenant.");
                }

                return tenantId;
            }

            if (metadata is AdminAuthenticationResult)
            {
                if (String.IsNullOrEmpty(requestedTenantId))
                {
                    throw new WebserverException(ApiResultEnum.BadRequest, TenantRequiredMessage);
                }

                return requestedTenantId;
            }

            throw new WebserverException(ApiResultEnum.NotAuthorized);
        }
    }
}
