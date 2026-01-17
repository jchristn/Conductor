namespace Conductor.Server.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using SwiftStack;
    using SwiftStack.Rest;

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
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

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
                    throw new SwiftStackException(ApiResultEnum.BadRequest, "API key already exists. Please use a unique API key.");
            }

            credential = await Database.Credential.CreateAsync(credential);

            return credential;
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
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            Credential credential = await Database.Credential.ReadAsync(tenantId, id);
            if (credential == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

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
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            Credential existing = await Database.Credential.ReadAsync(tenantId, id);
            if (existing == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            if (credential == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

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
                    throw new SwiftStackException(ApiResultEnum.BadRequest, "API key already exists. Please use a unique API key.");
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
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.Credential.ExistsAsync(tenantId, id);
            if (!exists)
                throw new SwiftStackException(ApiResultEnum.NotFound);

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
