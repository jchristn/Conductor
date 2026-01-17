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
    /// Model Configuration API controller.
    /// </summary>
    public class ModelConfigurationController : BaseController
    {
        /// <summary>
        /// Instantiate the model configuration controller.
        /// </summary>
        public ModelConfigurationController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging)
            : base(database, authService, serializer, logging)
        {
        }

        /// <summary>
        /// Create a model configuration.
        /// </summary>
        public async Task<ModelConfiguration> Create(string tenantId, ModelConfiguration configuration)
        {
            if (configuration == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(configuration.Name))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Name is required");

            configuration.Id = IdGenerator.NewModelConfigurationId();
            configuration.TenantId = tenantId;
            configuration = await Database.ModelConfiguration.CreateAsync(configuration);

            return configuration;
        }

        /// <summary>
        /// Read a model configuration by ID.
        /// </summary>
        public async Task<ModelConfiguration> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            ModelConfiguration configuration = await Database.ModelConfiguration.ReadAsync(tenantId, id);
            if (configuration == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            return configuration;
        }

        /// <summary>
        /// Update a model configuration.
        /// </summary>
        public async Task<ModelConfiguration> Update(string tenantId, string id, ModelConfiguration configuration)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            ModelConfiguration existing = await Database.ModelConfiguration.ReadAsync(tenantId, id);
            if (existing == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            if (configuration == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            configuration.Id = id;
            configuration.TenantId = tenantId;
            configuration.CreatedUtc = existing.CreatedUtc;
            configuration = await Database.ModelConfiguration.UpdateAsync(configuration);

            return configuration;
        }

        /// <summary>
        /// Delete a model configuration and remove references from VMRs.
        /// </summary>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.ModelConfiguration.ExistsAsync(tenantId, id);
            if (!exists)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            // Remove references from virtual model runners
            EnumerationResult<VirtualModelRunner> vmrs = await Database.VirtualModelRunner.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (VirtualModelRunner vmr in vmrs.Data)
            {
                if (vmr.ModelConfigurationIds != null && vmr.ModelConfigurationIds.Contains(id))
                {
                    vmr.ModelConfigurationIds.Remove(id);
                    await Database.VirtualModelRunner.UpdateAsync(vmr);
                }
            }

            await Database.ModelConfiguration.DeleteAsync(tenantId, id);
        }

        /// <summary>
        /// Enumerate model configurations.
        /// </summary>
        public async Task<EnumerationResult<ModelConfiguration>> Enumerate(
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

            return await Database.ModelConfiguration.EnumerateAsync(tenantId, request);
        }
    }
}
