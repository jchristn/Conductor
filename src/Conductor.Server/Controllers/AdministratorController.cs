namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
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
    /// Administrator API controller.
    /// </summary>
    public class AdministratorController : BaseController
    {
        /// <summary>
        /// Instantiate the administrator controller.
        /// </summary>
        public AdministratorController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging)
            : base(database, authService, serializer, logging)
        {
        }

        /// <summary>
        /// Create an administrator.
        /// </summary>
        public async Task<AdministratorResponse> Create(AdministratorCreateRequest request)
        {
            if (request == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(request.Email) || String.IsNullOrEmpty(request.Password))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Email and Password are required");

            // Check if email already exists
            bool exists = await Database.Administrator.ExistsByEmailAsync(request.Email);
            if (exists)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "An administrator with this email already exists");

            Administrator admin = new Administrator
            {
                Id = IdGenerator.NewAdministratorId(),
                Email = request.Email,
                PasswordSha256 = Administrator.ComputePasswordHash(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Active = request.Active ?? true
            };

            admin = await Database.Administrator.CreateAsync(admin);

            return ToResponse(admin);
        }

        /// <summary>
        /// Read an administrator by ID.
        /// </summary>
        public async Task<AdministratorResponse> Read(string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            Administrator admin = await Database.Administrator.ReadAsync(id);
            if (admin == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            return ToResponse(admin);
        }

        /// <summary>
        /// Update an administrator.
        /// </summary>
        public async Task<AdministratorResponse> Update(string id, AdministratorUpdateRequest request)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            Administrator existing = await Database.Administrator.ReadAsync(id);
            if (existing == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            if (request == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            // Check if changing email to one that already exists
            if (!String.IsNullOrEmpty(request.Email) &&
                !request.Email.Equals(existing.Email, StringComparison.OrdinalIgnoreCase))
            {
                bool emailExists = await Database.Administrator.ExistsByEmailAsync(request.Email);
                if (emailExists)
                    throw new SwiftStackException(ApiResultEnum.BadRequest, "An administrator with this email already exists");
                existing.Email = request.Email;
            }

            // Update password if provided
            if (!String.IsNullOrEmpty(request.Password))
            {
                existing.PasswordSha256 = Administrator.ComputePasswordHash(request.Password);
            }

            // Update other fields
            if (request.FirstName != null) existing.FirstName = request.FirstName;
            if (request.LastName != null) existing.LastName = request.LastName;
            if (request.Active.HasValue) existing.Active = request.Active.Value;

            existing = await Database.Administrator.UpdateAsync(existing);

            return ToResponse(existing);
        }

        /// <summary>
        /// Delete an administrator.
        /// </summary>
        /// <param name="currentAdminId">ID of the currently authenticated admin (to prevent self-deletion).</param>
        /// <param name="id">ID of the administrator to delete.</param>
        public async Task Delete(string currentAdminId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            // Prevent deleting yourself
            if (id.Equals(currentAdminId, StringComparison.OrdinalIgnoreCase))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Cannot delete your own administrator account");

            bool exists = await Database.Administrator.ExistsAsync(id);
            if (!exists)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            await Database.Administrator.DeleteAsync(id);
        }

        /// <summary>
        /// Enumerate administrators.
        /// </summary>
        public async Task<object> Enumerate(
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

            EnumerationResult<Administrator> result = await Database.Administrator.EnumerateAsync(request);

            // Convert to response objects (without password hash)
            List<AdministratorResponse> responseData = new List<AdministratorResponse>();
            foreach (Administrator admin in result.Data)
            {
                responseData.Add(ToResponse(admin));
            }

            return new
            {
                Data = responseData,
                result.TotalCount,
                result.HasMore,
                result.ContinuationToken
            };
        }

        /// <summary>
        /// Convert administrator to response object (without password hash).
        /// </summary>
        private static AdministratorResponse ToResponse(Administrator admin)
        {
            return new AdministratorResponse
            {
                Id = admin.Id,
                Email = admin.Email,
                FirstName = admin.FirstName,
                LastName = admin.LastName,
                Active = admin.Active,
                CreatedUtc = admin.CreatedUtc,
                LastUpdateUtc = admin.LastUpdateUtc
            };
        }
    }

    /// <summary>
    /// Request to create an administrator.
    /// </summary>
    public class AdministratorCreateRequest
    {
        /// <summary>
        /// Email address for the administrator.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Password for the administrator.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// First name of the administrator.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Last name of the administrator.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Whether the administrator is active.
        /// </summary>
        public bool? Active { get; set; }
    }

    /// <summary>
    /// Request to update an administrator.
    /// </summary>
    public class AdministratorUpdateRequest
    {
        /// <summary>
        /// Email address for the administrator.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Password for the administrator.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// First name of the administrator.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Last name of the administrator.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Whether the administrator is active.
        /// </summary>
        public bool? Active { get; set; }
    }

    /// <summary>
    /// Administrator response object (without password hash).
    /// </summary>
    public class AdministratorResponse
    {
        /// <summary>
        /// Unique identifier for the administrator.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Email address of the administrator.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// First name of the administrator.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Last name of the administrator.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Whether the administrator is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Timestamp when the administrator was created (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Timestamp when the administrator was last updated (UTC).
        /// </summary>
        public DateTime LastUpdateUtc { get; set; }
    }
}
