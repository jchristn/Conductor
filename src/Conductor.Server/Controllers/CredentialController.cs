namespace Conductor.Server.Controllers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;
    

    /// <summary>
    /// Credential API controller.
    /// </summary>
    public class CredentialController : BaseController
    {
        /// <summary>
        /// Instantiate the credential controller.
        /// </summary>
        public CredentialController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging)
            : base(database, authService, serializer, logging)
        {
        }

        /// <summary>
        /// Create a credential.
        /// </summary>
        /// <param name="tenantId">Tenant ID from auth.</param>
        /// <param name="userId">User ID from auth.</param>
        /// <param name="credential">Credential data.</param>
        /// <returns>Created credential.</returns>
        public async Task<Credential> Create(string tenantId, string userId, Credential credential)
        {
            if (credential == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            credential.Id = IdGenerator.NewCredentialId();
            credential.TenantId = tenantId;
            credential.UserId = userId;

            // Generate bearer token if not provided, ensuring uniqueness
            if (String.IsNullOrEmpty(credential.BearerToken))
            {
                credential.BearerToken = await GenerateUniqueBearerTokenAsync();
            }
            else
            {
                // Check if provided bearer token is unique
                Credential existing = await Database.Credential.ReadByBearerTokenAsync(credential.BearerToken);
                if (existing != null)
                    throw new WebserverException(ApiResultEnum.BadRequest, "API key already exists. Please use a unique API key.");
            }

            credential = await Database.Credential.CreateAsync(credential);

            return credential;
        }

        /// <summary>
        /// Resolve and validate the user a new credential belongs to. When requestedUserId is empty the caller's own user is
        /// used. A caller may name a different user only when it may assign owners (tenant admin, global admin or system
        /// administrator). Either way the owner must exist in the target tenant, so a credential can never be bound to a user
        /// outside the tenant it is created in.
        /// </summary>
        /// <param name="tenantId">Target tenant. Must not be null or empty.</param>
        /// <param name="callerUserId">The authenticated caller's user ID. Null for system administrators.</param>
        /// <param name="callerCanAssignOwner">True when the caller is a tenant admin, global admin or system administrator.</param>
        /// <param name="requestedUserId">UserId supplied in the request body. Null allowed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The owner user ID. Never null.</returns>
        /// <exception cref="WebserverException">Thrown with BadRequest when no owner can be determined or the owner is not in
        /// the target tenant, and with Forbidden when the caller may not create credentials for another user.</exception>
        public async Task<string> ResolveOwnerUserIdAsync(string tenantId, string callerUserId, bool callerCanAssignOwner, string requestedUserId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId))
                throw new WebserverException(ApiResultEnum.BadRequest, "TenantId is required.");

            string ownerUserId = !String.IsNullOrWhiteSpace(requestedUserId) ? requestedUserId : callerUserId;
            if (String.IsNullOrWhiteSpace(ownerUserId))
                throw new WebserverException(ApiResultEnum.BadRequest, "UserId is required: system administrator credentials have no user, so supply the UserId of a user in tenant '" + tenantId + "'.");

            if (!String.Equals(ownerUserId, callerUserId, StringComparison.Ordinal) && !callerCanAssignOwner)
                throw new WebserverException(ApiResultEnum.Forbidden, "Only tenant administrators and global administrators can create credentials for another user.");

            UserMaster owner = await Database.User.ReadAsync(tenantId, ownerUserId, token).ConfigureAwait(false);
            if (owner == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "UserId '" + ownerUserId + "' was not found in tenant '" + tenantId + "'. A credential must belong to a user in the tenant it is created in.");

            return owner.Id;
        }

        /// <summary>
        /// Read a credential by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID from auth.</param>
        /// <param name="id">Credential ID.</param>
        /// <returns>Credential.</returns>
        public async Task<Credential> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            Credential credential;
            if (String.IsNullOrEmpty(tenantId))
                credential = await Database.Credential.ReadByIdAsync(id);
            else
                credential = await Database.Credential.ReadAsync(tenantId, id);

            if (credential == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return credential;
        }

        /// <summary>
        /// Update a credential.
        /// </summary>
        /// <param name="tenantId">Tenant ID from auth.</param>
        /// <param name="id">Credential ID.</param>
        /// <param name="credential">Credential data.</param>
        /// <returns>Updated credential.</returns>
        public async Task<Credential> Update(string tenantId, string id, Credential credential)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            Credential existing = await Database.Credential.ReadAsync(tenantId, id);
            if (existing == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            if (credential == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            credential.Id = id;
            credential.TenantId = tenantId;
            credential.UserId = existing.UserId;
            credential.CreatedUtc = existing.CreatedUtc;

            // Keep existing bearer token if not provided
            if (String.IsNullOrEmpty(credential.BearerToken))
            {
                credential.BearerToken = existing.BearerToken;
            }
            else if (credential.BearerToken != existing.BearerToken)
            {
                // Check if new bearer token is unique
                Credential tokenCheck = await Database.Credential.ReadByBearerTokenAsync(credential.BearerToken);
                if (tokenCheck != null)
                    throw new WebserverException(ApiResultEnum.BadRequest, "API key already exists. Please use a unique API key.");
            }

            credential = await Database.Credential.UpdateAsync(credential);

            return credential;
        }

        /// <summary>
        /// Delete a credential.
        /// </summary>
        /// <param name="tenantId">Tenant ID from auth.</param>
        /// <param name="id">Credential ID.</param>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.Credential.ExistsAsync(tenantId, id);
            if (!exists)
                throw new WebserverException(ApiResultEnum.NotFound);

            await Database.Credential.DeleteAsync(tenantId, id);
        }

        /// <summary>
        /// Enumerate credentials.
        /// </summary>
        /// <param name="tenantId">Tenant ID from auth.</param>
        /// <param name="maxResults">Max results.</param>
        /// <param name="continuationToken">Continuation token.</param>
        /// <param name="activeFilter">Active filter.</param>
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult<Credential>> Enumerate(
            string tenantId,
            int? maxResults = null,
            string continuationToken = null,
            bool? activeFilter = null)
        {
            EnumerationRequest request = new EnumerationRequest();

            if (maxResults.HasValue)
                request.MaxResults = maxResults.Value;

            request.ContinuationToken = continuationToken;

            if (activeFilter.HasValue)
                request.ActiveFilter = activeFilter.Value;

            return await Database.Credential.EnumerateAsync(tenantId, request);
        }

        /// <summary>
        /// Generate a unique bearer token.
        /// </summary>
        private async Task<string> GenerateUniqueBearerTokenAsync()
        {
            const int maxAttempts = 10;
            for (int i = 0; i < maxAttempts; i++)
            {
                string token = IdGenerator.NewBearerToken();
                Credential existing = await Database.Credential.ReadByBearerTokenAsync(token);
                if (existing == null)
                {
                    return token;
                }
            }
            throw new InvalidOperationException("Failed to generate a unique bearer token after " + maxAttempts + " attempts");
        }
    }
}
