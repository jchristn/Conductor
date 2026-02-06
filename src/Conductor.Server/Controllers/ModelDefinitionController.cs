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
    /// Model Definition API controller.
    /// </summary>
    public class ModelDefinitionController : BaseController
    {
        /// <summary>
        /// Instantiate the model definition controller.
        /// </summary>
        public ModelDefinitionController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging)
            : base(database, authService, serializer, logging)
        {
        }

        /// <summary>
        /// Create a model definition.
        /// </summary>
        public async Task<ModelDefinition> Create(string tenantId, ModelDefinition definition)
        {
            if (definition == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrEmpty(definition.Name))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Name is required");

            definition.Id = IdGenerator.NewModelDefinitionId();
            definition.TenantId = tenantId;
            definition = await Database.ModelDefinition.CreateAsync(definition);

            return definition;
        }

        /// <summary>
        /// Read a model definition by ID.
        /// </summary>
        public async Task<ModelDefinition> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            ModelDefinition definition;
            if (String.IsNullOrEmpty(tenantId))
                definition = await Database.ModelDefinition.ReadByIdAsync(id);
            else
                definition = await Database.ModelDefinition.ReadAsync(tenantId, id);

            if (definition == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            return definition;
        }

        /// <summary>
        /// Update a model definition.
        /// </summary>
        public async Task<ModelDefinition> Update(string tenantId, string id, ModelDefinition definition)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            ModelDefinition existing = await Database.ModelDefinition.ReadAsync(tenantId, id);
            if (existing == null)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            if (definition == null)
                throw new SwiftStackException(ApiResultEnum.BadRequest, "Invalid request body");

            definition.Id = id;
            definition.TenantId = tenantId;
            definition.CreatedUtc = existing.CreatedUtc;
            definition = await Database.ModelDefinition.UpdateAsync(definition);

            return definition;
        }

        /// <summary>
        /// Delete a model definition and remove references from VMRs.
        /// </summary>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new SwiftStackException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.ModelDefinition.ExistsAsync(tenantId, id);
            if (!exists)
                throw new SwiftStackException(ApiResultEnum.NotFound);

            // Remove references from virtual model runners
            EnumerationResult<VirtualModelRunner> vmrs = await Database.VirtualModelRunner.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 });
            foreach (VirtualModelRunner vmr in vmrs.Data)
            {
                if (vmr.ModelDefinitionIds != null && vmr.ModelDefinitionIds.Contains(id))
                {
                    vmr.ModelDefinitionIds.Remove(id);
                    await Database.VirtualModelRunner.UpdateAsync(vmr);
                }
            }

            await Database.ModelDefinition.DeleteAsync(tenantId, id);
        }

        /// <summary>
        /// Enumerate model definitions.
        /// </summary>
        public async Task<EnumerationResult<ModelDefinition>> Enumerate(
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

            return await Database.ModelDefinition.EnumerateAsync(tenantId, request);
        }
    }
}
