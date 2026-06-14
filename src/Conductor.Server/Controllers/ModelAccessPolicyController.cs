namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
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
    /// Model access policy API controller.
    /// </summary>
    public class ModelAccessPolicyController : BaseController
    {
        private readonly IModelAccessControlService _ModelAccessControlService;

        /// <summary>
        /// Instantiate the model access policy controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="modelAccessControlService">Model access control service.</param>
        public ModelAccessPolicyController(
            DatabaseDriverBase database,
            AuthenticationService authService,
            Serializer serializer,
            LoggingModule logging,
            IModelAccessControlService modelAccessControlService = null)
            : base(database, authService, serializer, logging)
        {
            _ModelAccessControlService = modelAccessControlService;
        }

        /// <summary>
        /// Create a model access policy.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="policy">Policy to create.</param>
        /// <returns>Created policy with rules.</returns>
        public async Task<ModelAccessPolicy> Create(string tenantId, ModelAccessPolicy policy)
        {
            if (policy == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            policy.Id = IdGenerator.NewModelAccessPolicyId();
            policy.TenantId = tenantId;
            NormalizeRules(policy);
            await ValidateAsync(tenantId, policy, null).ConfigureAwait(false);

            await Database.ModelAccessPolicy.CreateAsync(policy).ConfigureAwait(false);
            foreach (ModelAccessRule rule in policy.Rules)
            {
                await Database.ModelAccessPolicy.CreateRuleAsync(rule).ConfigureAwait(false);
            }

            _ModelAccessControlService?.InvalidateCache(tenantId, policy.Id);
            return await Read(tenantId, policy.Id).ConfigureAwait(false);
        }

        /// <summary>
        /// Read a model access policy by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <returns>Policy with rules.</returns>
        public async Task<ModelAccessPolicy> Read(string tenantId, string id)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            ModelAccessPolicy policy = String.IsNullOrEmpty(tenantId)
                ? await Database.ModelAccessPolicy.ReadByIdAsync(id).ConfigureAwait(false)
                : await Database.ModelAccessPolicy.ReadAsync(tenantId, id).ConfigureAwait(false);

            if (policy == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            await PopulateRulesAsync(policy).ConfigureAwait(false);
            return policy;
        }

        /// <summary>
        /// Update a model access policy and replace its rules.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <param name="policy">Updated policy.</param>
        /// <returns>Updated policy with rules.</returns>
        public async Task<ModelAccessPolicy> Update(string tenantId, string id, ModelAccessPolicy policy)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            ModelAccessPolicy existing = await Database.ModelAccessPolicy.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (existing == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            if (policy == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Invalid request body");

            policy.Id = id;
            policy.TenantId = tenantId;
            policy.CreatedUtc = existing.CreatedUtc;
            NormalizeRules(policy);
            await ValidateAsync(tenantId, policy, id).ConfigureAwait(false);

            await Database.ModelAccessPolicy.UpdateAsync(policy).ConfigureAwait(false);
            await Database.ModelAccessPolicy.DeleteRulesByPolicyAsync(tenantId, id).ConfigureAwait(false);
            foreach (ModelAccessRule rule in policy.Rules)
            {
                await Database.ModelAccessPolicy.CreateRuleAsync(rule).ConfigureAwait(false);
            }

            _ModelAccessControlService?.InvalidateCache(tenantId, id);
            return await Read(tenantId, id).ConfigureAwait(false);
        }

        /// <summary>
        /// Validate a model access policy draft.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="policy">Policy draft.</param>
        /// <param name="existingId">Existing policy ID during update validation.</param>
        /// <returns>Validation result.</returns>
        public async Task<ResourceValidationResult> Validate(string tenantId, ModelAccessPolicy policy, string existingId = null)
        {
            if (_ModelAccessControlService == null)
            {
                return new ResourceValidationResult
                {
                    ResourceType = "ModelAccessPolicy",
                    IsValid = true
                };
            }

            if (policy != null)
            {
                if (!String.IsNullOrWhiteSpace(tenantId))
                {
                    policy.TenantId = tenantId;
                }
                NormalizeRules(policy);
            }

            return await _ModelAccessControlService.ValidatePolicyAsync(tenantId, policy, existingId).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a model access policy, optionally detaching referencing virtual model runners.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <param name="forceDetach">True to detach active virtual model runners before deleting.</param>
        /// <returns>Task.</returns>
        public async Task Delete(string tenantId, string id, bool forceDetach = false)
        {
            if (String.IsNullOrEmpty(id))
                throw new WebserverException(ApiResultEnum.BadRequest, "ID is required");

            bool exists = await Database.ModelAccessPolicy.ExistsAsync(tenantId, id).ConfigureAwait(false);
            if (!exists)
                throw new WebserverException(ApiResultEnum.NotFound);

            EnumerationResult<VirtualModelRunner> vmrs = await Database.VirtualModelRunner.EnumerateAsync(tenantId, new EnumerationRequest { MaxResults = 10000 }).ConfigureAwait(false);
            List<VirtualModelRunner> attachedVmrs = new List<VirtualModelRunner>();
            foreach (VirtualModelRunner vmr in vmrs.Data ?? new List<VirtualModelRunner>())
            {
                if (String.Equals(vmr.ModelAccessPolicyId, id, StringComparison.Ordinal))
                {
                    attachedVmrs.Add(vmr);
                }
            }

            if (attachedVmrs.Count > 0 && !forceDetach)
            {
                throw new WebserverException(ApiResultEnum.Conflict, "Model access policy is attached to one or more virtual model runners.");
            }

            foreach (VirtualModelRunner vmr in attachedVmrs)
            {
                vmr.ModelAccessPolicyId = null;
                await Database.VirtualModelRunner.UpdateAsync(vmr).ConfigureAwait(false);
            }

            await Database.ModelAccessPolicy.DeleteAsync(tenantId, id).ConfigureAwait(false);
            _ModelAccessControlService?.InvalidateCache(tenantId, id);
        }

        /// <summary>
        /// Enumerate model access policies.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="maxResults">Maximum results.</param>
        /// <param name="continuationToken">Continuation token.</param>
        /// <param name="nameFilter">Name filter.</param>
        /// <param name="activeFilter">Active-state filter.</param>
        /// <returns>Enumeration result.</returns>
        public async Task<EnumerationResult<ModelAccessPolicy>> Enumerate(string tenantId, int? maxResults = null, string continuationToken = null, string nameFilter = null, bool? activeFilter = null)
        {
            EnumerationRequest request = new EnumerationRequest();
            if (maxResults.HasValue) request.MaxResults = maxResults.Value;
            request.ContinuationToken = continuationToken;
            request.NameFilter = nameFilter;
            if (activeFilter.HasValue) request.ActiveFilter = activeFilter.Value;

            return await Database.ModelAccessPolicy.EnumerateAsync(tenantId, request).ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluate a request context against a specific policy.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Policy ID.</param>
        /// <param name="context">Evaluation context.</param>
        /// <returns>Evaluation result.</returns>
        public async Task<ModelAccessEvaluationResult> Evaluate(string tenantId, string id, ModelAccessEvaluationContext context)
        {
            if (_ModelAccessControlService == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Model access evaluation is not available.");

            ModelAccessPolicy policy = await Database.ModelAccessPolicy.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (policy == null)
                throw new WebserverException(ApiResultEnum.NotFound);

            context ??= new ModelAccessEvaluationContext();
            context.TenantId = tenantId;
            context.ModelAccessPolicyId = id;
            await EnrichContextAsync(context).ConfigureAwait(false);
            return await _ModelAccessControlService.ExplainAsync(context).ConfigureAwait(false);
        }

        /// <summary>
        /// Evaluate effective access from query parameters and attached policy references.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="credentialId">Credential ID.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="vmrId">Virtual model runner ID.</param>
        /// <param name="modelDefinitionId">Model definition ID.</param>
        /// <param name="modelName">Model name.</param>
        /// <param name="action">Model access action.</param>
        /// <returns>Evaluation result.</returns>
        public async Task<ModelAccessEvaluationResult> GetEffective(
            string tenantId,
            string credentialId = null,
            string userId = null,
            string vmrId = null,
            string modelDefinitionId = null,
            string modelName = null,
            ModelAccessActionEnum? action = null)
        {
            if (_ModelAccessControlService == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Model access evaluation is not available.");

            ModelAccessEvaluationContext context = new ModelAccessEvaluationContext
            {
                TenantId = tenantId,
                CredentialId = credentialId,
                UserId = userId,
                VirtualModelRunnerId = vmrId,
                ModelDefinitionId = modelDefinitionId,
                RequestedModel = modelName,
                EffectiveModel = modelName,
                Action = action ?? ModelAccessActionEnum.Completions
            };

            if (!String.IsNullOrWhiteSpace(vmrId))
            {
                VirtualModelRunner vmr = await Database.VirtualModelRunner.ReadAsync(tenantId, vmrId).ConfigureAwait(false);
                if (vmr == null)
                    throw new WebserverException(ApiResultEnum.NotFound);

                context.ModelAccessPolicyId = vmr.ModelAccessPolicyId;
                context.ApiType = vmr.ApiType;
            }

            await EnrichContextAsync(context).ConfigureAwait(false);
            return await _ModelAccessControlService.ExplainAsync(context).ConfigureAwait(false);
        }

        private void NormalizeRules(ModelAccessPolicy policy)
        {
            if (policy == null) return;

            foreach (ModelAccessRule rule in policy.Rules ?? new List<ModelAccessRule>())
            {
                if (String.IsNullOrWhiteSpace(rule.Id))
                {
                    rule.Id = IdGenerator.NewModelAccessRuleId();
                }

                rule.TenantId = policy.TenantId;
                rule.PolicyId = policy.Id;
            }
        }

        private async Task PopulateRulesAsync(ModelAccessPolicy policy)
        {
            EnumerationResult<ModelAccessRule> rules = await Database.ModelAccessPolicy.EnumerateRulesAsync(
                policy.TenantId,
                policy.Id,
                new EnumerationRequest { MaxResults = 10000 }).ConfigureAwait(false);
            policy.Rules = rules.Data ?? new List<ModelAccessRule>();
        }

        private async Task EnrichContextAsync(ModelAccessEvaluationContext context)
        {
            if (!String.IsNullOrWhiteSpace(context.CredentialId))
            {
                Credential credential = await Database.Credential.ReadAsync(context.TenantId, context.CredentialId).ConfigureAwait(false);
                if (credential != null)
                {
                    context.CredentialLabels = credential.Labels;
                    if (String.IsNullOrWhiteSpace(context.UserId))
                    {
                        context.UserId = credential.UserId;
                    }
                }
            }

            if (!String.IsNullOrWhiteSpace(context.UserId))
            {
                UserMaster user = await Database.User.ReadAsync(context.TenantId, context.UserId).ConfigureAwait(false);
                if (user != null)
                {
                    context.UserLabels = user.Labels;
                }
            }

            if (!String.IsNullOrWhiteSpace(context.ModelDefinitionId))
            {
                ModelDefinition definition = await Database.ModelDefinition.ReadAsync(context.TenantId, context.ModelDefinitionId).ConfigureAwait(false);
                if (definition != null)
                {
                    context.ModelDefinitionName = definition.Name;
                    context.ModelLabels = definition.Labels;
                    context.EffectiveModel ??= definition.Name;
                    context.RequestedModel ??= definition.Name;
                }
            }
        }

        private async Task ValidateAsync(string tenantId, ModelAccessPolicy policy, string existingId)
        {
            ResourceValidationResult validation = await Validate(tenantId, policy, existingId).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                throw new WebserverException(ApiResultEnum.BadRequest, String.Join(" ", validation.Errors.ConvertAll(item => item.Message)));
            }
        }
    }
}
