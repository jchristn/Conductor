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
    /// User API controller.
    /// </summary>
    public class UserController : BaseController
    {
        /// <summary>
        /// Instantiate the user controller.
        /// </summary>
        public UserController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging)
            : base(database, authService, serializer, logging)
        {
        }

        /// <summary>
        /// Create a user.
        /// </summary>
        /// <param name="tenantId">Tenant ID from auth.</param>
        /// <param name="user">User data.</param>
        /// <returns>Created user.</returns>
        public async Task<UserMaster> Create(string tenantId, UserMaster user)
        {
            if (user == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(user.FirstName) || String.IsNullOrEmpty(user.LastName) ||
                String.IsNullOrEmpty(user.Email) || String.IsNullOrEmpty(user.Password))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "FirstName, LastName, Email, and Password are required");

            user.Id = IdGenerator.NewUserId();
            user.TenantId = tenantId;
            user = await Database.User.CreateAsync(user);

            return user;
        }

        /// <summary>
        /// Read a user by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID from auth.</param>
        /// <param name="id">User ID.</param>
        /// <returns>User.</returns>
        public async Task<UserMaster> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            UserMaster user;
            if (String.IsNullOrEmpty(tenantId))
                user = await Database.User.ReadByIdAsync(id);
            else
                user = await Database.User.ReadAsync(tenantId, id);

            if (user == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            return user;
        }

        /// <summary>
        /// Update a user.
        /// </summary>
        /// <param name="tenantId">Tenant ID from auth.</param>
        /// <param name="id">User ID.</param>
        /// <param name="user">User data.</param>
        /// <returns>Updated user.</returns>
        public async Task<UserMaster> Update(string tenantId, string id, UserMaster user)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            UserMaster existing = await Database.User.ReadAsync(tenantId, id);
            if (existing == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            if (user == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            user.Id = id;
            user.TenantId = tenantId;
            user.CreatedUtc = existing.CreatedUtc;
            user = await Database.User.UpdateAsync(user);

            return user;
        }

        /// <summary>
        /// Delete a user and associated credentials.
        /// </summary>
        /// <param name="tenantId">Tenant ID from auth.</param>
        /// <param name="id">User ID.</param>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.User.ExistsAsync(tenantId, id);
            if (!exists)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            // Delete all credentials associated with this user
            EnumerationResult<Credential> credentials = await Database.Credential.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (Credential credential in credentials.Data)
            {
                if (credential.UserId == id)
                {
                    await Database.Credential.DeleteAsync(tenantId, credential.Id);
                }
            }

            await Database.User.DeleteAsync(tenantId, id);
        }

        /// <summary>
        /// Enumerate users.
        /// </summary>
        /// <param name="tenantId">Tenant ID from auth.</param>
        /// <param name="maxResults">Max results.</param>
        /// <param name="continuationToken">Continuation token.</param>
        /// <param name="nameFilter">Name filter.</param>
        /// <param name="activeFilter">Active filter.</param>
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult<UserMaster>> Enumerate(
            string tenantId,
            int? maxResults = null,
            string continuationToken = null,
            string nameFilter = null,
            bool? activeFilter = null)
        {
            EnumerationRequest request = new EnumerationRequest();

            if (maxResults.HasValue)
                request.MaxResults = maxResults.Value;

            request.ContinuationToken = continuationToken;
            request.NameFilter = nameFilter;

            if (activeFilter.HasValue)
                request.ActiveFilter = activeFilter.Value;

            return await Database.User.EnumerateAsync(tenantId, request);
        }
    }
}
