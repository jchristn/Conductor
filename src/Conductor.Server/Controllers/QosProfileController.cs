namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// QoS profile API controller.
    /// </summary>
    public class QosProfileController : BaseController
    {
        private readonly QosProfileCompiler _Compiler;
        private readonly QosAdmissionService _AdmissionService;

        /// <summary>
        /// Instantiate the QoS profile controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="admissionService">Admission service to invalidate on change. Nullable.</param>
        public QosProfileController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging, QosAdmissionService admissionService = null)
            : base(database, authService, serializer, logging)
        {
            _Compiler = new QosProfileCompiler();
            _AdmissionService = admissionService;
        }

        /// <summary>
        /// Create a QoS profile.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="profile">Profile to create.</param>
        /// <returns>The created profile.</returns>
        public async Task<QosProfile> Create(string tenantId, QosProfile profile)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new WebserverException(ApiResultEnum.BadRequest, "Tenant id is required");
            if (profile == null) throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            profile.Id = IdGenerator.NewQosProfileId();
            profile.TenantId = tenantId;
            profile.IsDefault = false;

            ThrowIfInvalid(profile);

            return await Database.QosProfile.CreateAsync(profile).ConfigureAwait(false);
        }

        /// <summary>
        /// Read a QoS profile by id.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Profile id.</param>
        /// <returns>The profile.</returns>
        public async Task<QosProfile> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id)) throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            QosProfile profile = String.IsNullOrEmpty(tenantId)
                ? await Database.QosProfile.ReadByIdAsync(id).ConfigureAwait(false)
                : await Database.QosProfile.ReadAsync(tenantId, id).ConfigureAwait(false);

            if (profile == null) throw new WebserverException(ApiResultEnum.NotFound);
            return profile;
        }

        /// <summary>
        /// Update a QoS profile.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Profile id.</param>
        /// <param name="profile">Updated profile.</param>
        /// <returns>The updated profile.</returns>
        public async Task<QosProfile> Update(string tenantId, string id, QosProfile profile)
        {
            if (String.IsNullOrEmpty(id)) throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");
            if (profile == null) throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            QosProfile existing = await Database.QosProfile.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (existing == null) throw new WebserverException(ApiResultEnum.NotFound);

            profile.Id = id;
            profile.TenantId = tenantId;
            profile.CreatedUtc = existing.CreatedUtc;
            profile.IsDefault = existing.IsDefault;

            ThrowIfInvalid(profile);

            QosProfile updated = await Database.QosProfile.UpdateAsync(profile).ConfigureAwait(false);
            _AdmissionService?.Invalidate(id);
            return updated;
        }

        /// <summary>
        /// Validate a QoS profile draft without persisting it.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="profile">Profile draft.</param>
        /// <returns>Validation result.</returns>
        public ResourceValidationResult Validate(string tenantId, QosProfile profile)
        {
            ResourceValidationResult result = new ResourceValidationResult { ResourceType = "QosProfile", IsValid = true };
            if (profile == null)
            {
                result.IsValid = false;
                result.Errors.Add(new ResourceValidationIssue { Code = "empty", Message = "Profile body is required." });
                return result;
            }

            try
            {
                _Compiler.Compile(profile);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add(new ResourceValidationIssue { Code = "compile", Message = ex.Message });
            }

            return result;
        }

        /// <summary>
        /// Delete a QoS profile. The default profile cannot be deleted; referencing runners are
        /// reassigned to the tenant default.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="id">Profile id.</param>
        /// <returns>Task.</returns>
        public async Task Delete(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id)) throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            QosProfile existing = await Database.QosProfile.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (existing == null) throw new WebserverException(ApiResultEnum.NotFound);
            if (existing.IsDefault) throw new WebserverException(ApiResultEnum.BadRequest, "Cannot delete the default QoS profile");

            QosProfile defaultProfile = await Database.QosProfile.ReadDefaultAsync(tenantId).ConfigureAwait(false);
            string reassignTo = defaultProfile?.Id;

            EnumerationResult<VirtualModelRunner> vmrs = await Database.VirtualModelRunner.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 }).ConfigureAwait(false);
            foreach (VirtualModelRunner vmr in vmrs.Data.Where(v => String.Equals(v.QosProfileId, id, StringComparison.Ordinal)))
            {
                vmr.QosProfileId = reassignTo;
                await Database.VirtualModelRunner.UpdateAsync(vmr).ConfigureAwait(false);
            }

            await Database.QosProfile.DeleteAsync(tenantId, id).ConfigureAwait(false);
            _AdmissionService?.Invalidate(id);
        }

        /// <summary>
        /// Enumerate QoS profiles.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <param name="maxResults">Maximum results.</param>
        /// <param name="continuationToken">Pagination token.</param>
        /// <param name="nameFilter">Optional name filter.</param>
        /// <param name="activeFilter">Optional active-state filter.</param>
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult<QosProfile>> Enumerate(string tenantId, int? maxResults = null, string continuationToken = null, string nameFilter = null, bool? activeFilter = null)
        {
            EnumerationRequest request = new EnumerationRequest();
            if (maxResults.HasValue) request.MaxResults = maxResults.Value;
            request.ContinuationToken = continuationToken;
            request.NameFilter = nameFilter;
            if (activeFilter.HasValue) request.ActiveFilter = activeFilter.Value;

            return await Database.QosProfile.EnumerateAsync(tenantId, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the classifier catalog: available sources, operators, disciplines, and the tenant's classes.
        /// </summary>
        /// <param name="tenantId">Tenant id.</param>
        /// <returns>Catalog object.</returns>
        public async Task<Dictionary<string, object>> GetClassifierCatalog(string tenantId)
        {
            List<QosTrafficClass> classes = new List<QosTrafficClass>();
            if (!String.IsNullOrEmpty(tenantId))
            {
                EnumerationResult<QosTrafficClass> result = await Database.QosTrafficClass.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 1000 }).ConfigureAwait(false);
                classes = result.Data;
            }

            return new Dictionary<string, object>
            {
                { "sources", Enum.GetNames(typeof(QosClassifierSourceEnum)) },
                { "operators", Enum.GetNames(typeof(QosClassifierOperatorEnum)) },
                { "disciplines", Enum.GetNames(typeof(QosDisciplineEnum)) },
                { "overflowPolicies", Enum.GetNames(typeof(QosOverflowPolicyEnum)) },
                { "classes", classes }
            };
        }

        private void ThrowIfInvalid(QosProfile profile)
        {
            ResourceValidationResult validation = Validate(profile.TenantId, profile);
            if (!validation.IsValid)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, String.Join(" ", validation.Errors.ConvertAll(item => item.Message)));
            }
        }
    }
}
