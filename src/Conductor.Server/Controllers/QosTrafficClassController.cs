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
    using WatsonWebserver.Core;

    /// <summary>
    /// QoS traffic class catalog API controller.
    /// </summary>
    public class QosTrafficClassController : BaseController
    {
        /// <summary>
        /// Instantiate the QoS traffic class controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        public QosTrafficClassController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging)
            : base(database, authService, serializer, logging)
        {
        }

        /// <summary>
        /// Create a traffic class.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="trafficClass">Traffic class to create.</param>
        /// <returns>The created traffic class.</returns>
        public async Task<QosTrafficClass> Create(string tenantId, QosTrafficClass trafficClass)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new WebserverException(ApiResultEnum.BadRequest, "Tenant id is required");
            if (trafficClass == null) throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");
            if (String.IsNullOrEmpty(trafficClass.Name)) throw new WebserverException(ApiResultEnum.BadRequest, "Name is required");

            QosTrafficClass conflict = await Database.QosTrafficClass.ReadByNameAsync(tenantId, trafficClass.Name).ConfigureAwait(false);
            if (conflict != null) throw new WebserverException(ApiResultEnum.BadRequest, "A traffic class with that name already exists");

            trafficClass.Id = IdGenerator.NewQosTrafficClassId();
            trafficClass.TenantId = tenantId;
            trafficClass.IsSystem = false;

            return await Database.QosTrafficClass.CreateAsync(trafficClass).ConfigureAwait(false);
        }

        /// <summary>
        /// Read a traffic class by id.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Traffic class id.</param>
        /// <returns>The traffic class.</returns>
        public async Task<QosTrafficClass> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id)) throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            QosTrafficClass trafficClass = await Database.QosTrafficClass.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (trafficClass == null) throw new WebserverException(ApiResultEnum.NotFound);
            return trafficClass;
        }

        /// <summary>
        /// Update a traffic class.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Traffic class id.</param>
        /// <param name="trafficClass">Updated traffic class.</param>
        /// <returns>The updated traffic class.</returns>
        public async Task<QosTrafficClass> Update(string tenantId, string id, QosTrafficClass trafficClass)
        {
            if (String.IsNullOrEmpty(id)) throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");
            if (trafficClass == null) throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            QosTrafficClass existing = await Database.QosTrafficClass.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (existing == null) throw new WebserverException(ApiResultEnum.NotFound);

            trafficClass.Id = id;
            trafficClass.TenantId = tenantId;
            trafficClass.IsSystem = existing.IsSystem;
            trafficClass.CreatedUtc = existing.CreatedUtc;

            return await Database.QosTrafficClass.UpdateAsync(trafficClass).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a traffic class.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Traffic class id.</param>
        /// <returns>Task.</returns>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id)) throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.QosTrafficClass.ExistsAsync(tenantId, id).ConfigureAwait(false);
            if (!exists) throw new WebserverException(ApiResultEnum.NotFound);

            await Database.QosTrafficClass.DeleteAsync(tenantId, id).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate traffic classes.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="maxResults">Maximum results.</param>
        /// <param name="continuationToken">Pagination token.</param>
        /// <param name="nameFilter">Optional name filter.</param>
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult<QosTrafficClass>> Enumerate(string tenantId, int? maxResults = null, string continuationToken = null, string nameFilter = null)
        {
            EnumerationRequest request = new EnumerationRequest();
            if (maxResults.HasValue) request.MaxResults = maxResults.Value;
            request.ContinuationToken = continuationToken;
            request.NameFilter = nameFilter;

            return await Database.QosTrafficClass.EnumerateAsync(tenantId, request).ConfigureAwait(false);
        }
    }
}
