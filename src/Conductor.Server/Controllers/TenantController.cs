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
    /// Tenant API controller.
    /// </summary>
    public class TenantController : BaseController
    {
        private string _Header = "[TenantController] ";

        /// <summary>
        /// Instantiate the tenant controller.
        /// </summary>
        public TenantController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging)
            : base(database, authService, serializer, logging)
        {
        }

        /// <summary>
        /// Create a tenant.
        /// </summary>
        /// <param name="tenant">Tenant data.</param>
        /// <returns>Created tenant.</returns>
        public async Task<TenantMetadata> Create(TenantMetadata tenant)
        {
            if (tenant == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(tenant.Name))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Name is required");

            tenant.Id = IdGenerator.NewTenantId();
            tenant = await Database.Tenant.CreateAsync(tenant);

            return tenant;
        }

        /// <summary>
        /// Read a tenant by ID.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <returns>Tenant or null.</returns>
        public async Task<TenantMetadata> Read(string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            TenantMetadata tenant = await Database.Tenant.ReadAsync(id);
            if (tenant == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            return tenant;
        }

        /// <summary>
        /// Update a tenant.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        /// <param name="tenant">Tenant data.</param>
        /// <returns>Updated tenant.</returns>
        public async Task<TenantMetadata> Update(string id, TenantMetadata tenant)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            TenantMetadata existing = await Database.Tenant.ReadAsync(id);
            if (existing == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            if (tenant == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            tenant.Id = id;
            tenant.CreatedUtc = existing.CreatedUtc;
            tenant = await Database.Tenant.UpdateAsync(tenant);

            return tenant;
        }

        /// <summary>
        /// Delete a tenant and all associated data.
        /// </summary>
        /// <param name="id">Tenant ID.</param>
        public async Task Delete(string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.Tenant.ExistsAsync(id);
            if (!exists)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            // Delete all associated data (cascade delete)
            await DeleteAssociatedDataAsync(id);

            await Database.Tenant.DeleteAsync(id);
        }

        /// <summary>
        /// Delete all data associated with a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        private async Task DeleteAssociatedDataAsync(string tenantId)
        {
            Logging.Info(_Header + "starting deletion of subordinate data for tenant " + tenantId);

            // Delete virtual model runners
            EnumerationResult<VirtualModelRunner> vmrs = await Database.VirtualModelRunner.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (VirtualModelRunner vmr in vmrs.Data)
            {
                await Database.VirtualModelRunner.DeleteAsync(tenantId, vmr.Id);
            }

            // Delete model configurations
            EnumerationResult<ModelConfiguration> configs = await Database.ModelConfiguration.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (ModelConfiguration config in configs.Data)
            {
                await Database.ModelConfiguration.DeleteAsync(tenantId, config.Id);
            }

            // Delete model definitions
            EnumerationResult<ModelDefinition> definitions = await Database.ModelDefinition.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (ModelDefinition definition in definitions.Data)
            {
                await Database.ModelDefinition.DeleteAsync(tenantId, definition.Id);
            }

            // Delete model runner endpoints
            EnumerationResult<ModelRunnerEndpoint> endpoints = await Database.ModelRunnerEndpoint.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (ModelRunnerEndpoint endpoint in endpoints.Data)
            {
                await Database.ModelRunnerEndpoint.DeleteAsync(tenantId, endpoint.Id);
            }

            // Delete credentials
            EnumerationResult<Credential> credentials = await Database.Credential.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (Credential credential in credentials.Data)
            {
                await Database.Credential.DeleteAsync(tenantId, credential.Id);
            }

            // Delete users
            EnumerationResult<UserMaster> users = await Database.User.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (UserMaster user in users.Data)
            {
                await Database.User.DeleteAsync(tenantId, user.Id);
            }

            Logging.Info(_Header + "completed deletion of subordinate data for tenant " + tenantId);
        }

        /// <summary>
        /// Enumerate tenants.
        /// </summary>
        /// <param name="maxResults">Max results.</param>
        /// <param name="continuationToken">Continuation token.</param>
        /// <param name="nameFilter">Name filter.</param>
        /// <param name="activeFilter">Active filter.</param>
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult<TenantMetadata>> Enumerate(
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

            return await Database.Tenant.EnumerateAsync(request);
        }
    }
}
