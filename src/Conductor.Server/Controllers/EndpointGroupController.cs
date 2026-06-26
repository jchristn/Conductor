namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Responses;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Endpoint group API controller.
    /// </summary>
    public class EndpointGroupController : BaseController
    {
        private readonly ConfigurationValidationService _ValidationService;

        /// <summary>
        /// Instantiate the endpoint group controller.
        /// </summary>
        public EndpointGroupController(
            DatabaseDriverBase database,
            AuthenticationService authService,
            Serializer serializer,
            LoggingModule logging,
            ConfigurationValidationService validationService = null)
            : base(database, authService, serializer, logging)
        {
            _ValidationService = validationService;
        }

        /// <summary>
        /// Create an endpoint group.
        /// </summary>
        public async Task<EndpointGroup> Create(string tenantId, EndpointGroup group)
        {
            if (group == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            if (String.IsNullOrWhiteSpace(group.Name))
                throw new WebserverException(ApiResultEnum.BadRequest, "Name is required");

            group.Id = IdGenerator.NewEndpointGroupId();
            group.TenantId = tenantId;
            await ValidateAsync(tenantId, group, null).ConfigureAwait(false);
            return await Database.EndpointGroup.CreateAsync(group).ConfigureAwait(false);
        }

        /// <summary>
        /// Read an endpoint group.
        /// </summary>
        public async Task<EndpointGroup> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            EndpointGroup group = String.IsNullOrEmpty(tenantId)
                ? await Database.EndpointGroup.ReadByIdAsync(id).ConfigureAwait(false)
                : await Database.EndpointGroup.ReadAsync(tenantId, id).ConfigureAwait(false);

            if (group == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            return group;
        }

        /// <summary>
        /// Update an endpoint group.
        /// </summary>
        public async Task<EndpointGroup> Update(string tenantId, string id, EndpointGroup group)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            EndpointGroup existing = await Database.EndpointGroup.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (existing == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            if (group == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            group.Id = id;
            group.TenantId = tenantId;
            group.CreatedUtc = existing.CreatedUtc;
            await ValidateAsync(tenantId, group, id).ConfigureAwait(false);
            return await Database.EndpointGroup.UpdateAsync(group).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete an endpoint group and detach it from virtual model runners.
        /// </summary>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.EndpointGroup.ExistsAsync(tenantId, id).ConfigureAwait(false);
            if (!exists)
                throw new WebserverException(ApiResultEnum.NotFound);

            EnumerationResult<VirtualModelRunner> vmrs = await Database.VirtualModelRunner
                .EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 })
                .ConfigureAwait(false);

            foreach (VirtualModelRunner vmr in vmrs.Data ?? new List<VirtualModelRunner>())
            {
                if (vmr.EndpointGroupIds == null || !vmr.EndpointGroupIds.Contains(id))
                {
                    continue;
                }

                vmr.EndpointGroupIds = vmr.EndpointGroupIds
                    .Where(groupId => !String.Equals(groupId, id, StringComparison.Ordinal))
                    .ToList();
                await Database.VirtualModelRunner.UpdateAsync(vmr).ConfigureAwait(false);
            }

            await Database.EndpointGroup.DeleteAsync(tenantId, id).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate endpoint groups.
        /// </summary>
        public async Task<EnumerationResult<EndpointGroup>> Enumerate(
            string tenantId,
            int? maxResults = null,
            string continuationToken = null,
            string nameFilter = null,
            bool? activeFilter = null)
        {
            EnumerationRequest request = new EnumerationRequest();
            if (maxResults.HasValue) request.MaxResults = maxResults.Value;
            request.ContinuationToken = continuationToken;
            request.NameFilter = nameFilter;
            if (activeFilter.HasValue) request.ActiveFilter = activeFilter.Value;

            return await Database.EndpointGroup.EnumerateAsync(tenantId, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Validate an endpoint group draft.
        /// </summary>
        public async Task<ResourceValidationResult> Validate(string tenantId, EndpointGroup group, string existingId = null)
        {
            if (_ValidationService == null)
            {
                return new ResourceValidationResult
                {
                    ResourceType = "EndpointGroup",
                    IsValid = true
                };
            }

            return await _ValidationService.ValidateEndpointGroupAsync(tenantId, group, existingId).ConfigureAwait(false);
        }

        private async Task ValidateAsync(string tenantId, EndpointGroup group, string existingId)
        {
            ResourceValidationResult validation = await Validate(tenantId, group, existingId).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, String.Join(" ", validation.Errors.ConvertAll(item => item.Message)));
            }
        }
    }
}
