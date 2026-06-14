namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Default implementation of model access policy validation and evaluation.
    /// </summary>
    public class ModelAccessControlService : IModelAccessControlService
    {
        private static readonly HashSet<string> _AllowedSelectorKeys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
        {
            "label",
            "labels",
            "value",
            "equals",
            "prefix",
            "contains"
        };

        private readonly object _CacheLock = new object();
        private readonly Dictionary<string, CachedPolicy> _PolicyCache = new Dictionary<string, CachedPolicy>(StringComparer.Ordinal);
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly ModelAccessControlSettings _Settings;

        /// <summary>
        /// Instantiate the model access control service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="settings">Model access control settings.</param>
        public ModelAccessControlService(DatabaseDriverBase database, LoggingModule logging, ModelAccessControlSettings settings = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging;
            _Settings = settings ?? new ModelAccessControlSettings();
        }

        /// <inheritdoc />
        public async Task<ModelAccessEvaluationResult> EvaluateAsync(ModelAccessEvaluationContext context, CancellationToken token = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            ModelAccessEnforcementModeEnum mode = GetEffectiveMode();
            if (mode == ModelAccessEnforcementModeEnum.Disabled)
            {
                return BuildResult(
                    ModelAccessDefaultDecisionEnum.Permit,
                    mode,
                    "Disabled",
                    null,
                    null,
                    null,
                    "ModelAccessDisabled",
                    "Model access control is disabled.");
            }

            if (String.IsNullOrWhiteSpace(context.TenantId))
            {
                return BuildResult(
                    ModelAccessDefaultDecisionEnum.Deny,
                    mode,
                    null,
                    null,
                    null,
                    null,
                    "TenantMissing",
                    "TenantId is required for model access evaluation.");
            }

            if (ShouldBypassForAdministrator(context))
            {
                return BuildResult(
                    ModelAccessDefaultDecisionEnum.Permit,
                    mode,
                    "AdministratorBypass",
                    null,
                    null,
                    null,
                    "AdministratorBypass",
                    "The authenticated administrator is allowed to bypass model access policy checks.");
            }

            ModelAccessActionEnum action = NormalizeAction(context);
            if (IsUnknownModelDenied(context, action))
            {
                string reasonCode = _Settings.UnknownModelBehavior == ModelAccessUnknownModelBehaviorEnum.RequireStrictVirtualModelRunnerResolution
                    ? "UnknownModelRequiresStrictResolution"
                    : "UnknownModelDenied";
                return BuildResult(
                    ModelAccessDefaultDecisionEnum.Deny,
                    mode,
                    "Global",
                    null,
                    null,
                    null,
                    reasonCode,
                    "The request model could not be resolved before model access evaluation.");
            }

            if (String.IsNullOrWhiteSpace(context.ModelAccessPolicyId))
            {
                return BuildGlobalDefaultResult(mode, "NoModelAccessPolicy", "No model access policy is attached.");
            }

            CachedPolicy cached = await LoadPolicyAsync(context.TenantId, context.ModelAccessPolicyId, token).ConfigureAwait(false);
            if (cached == null || cached.Policy == null)
            {
                return BuildGlobalDefaultResult(mode, "ModelAccessPolicyMissing", "The attached model access policy was not found.");
            }

            ModelAccessPolicy policy = cached.Policy;
            if (!policy.Active)
            {
                return BuildGlobalDefaultResult(mode, "ModelAccessPolicyInactive", "The attached model access policy is inactive.", policy);
            }

            List<ModelAccessRule> matches = (cached.Rules ?? new List<ModelAccessRule>())
                .Where(rule => RuleMatches(rule, context, action))
                .OrderByDescending(rule => rule.Priority)
                .ThenBy(rule => rule.Effect == ModelAccessRuleEffectEnum.Deny ? 0 : 1)
                .ThenBy(rule => rule.CreatedUtc)
                .ThenBy(rule => rule.Id, StringComparer.Ordinal)
                .ToList();

            if (matches.Count > 0)
            {
                ModelAccessRule selected = matches[0];
                ModelAccessDefaultDecisionEnum decision = selected.Effect == ModelAccessRuleEffectEnum.Allow
                    ? ModelAccessDefaultDecisionEnum.Permit
                    : ModelAccessDefaultDecisionEnum.Deny;

                return BuildResult(
                    decision,
                    mode,
                    null,
                    policy,
                    selected,
                    selected.Effect,
                    selected.Effect == ModelAccessRuleEffectEnum.Allow ? "MatchedAllowRule" : "MatchedDenyRule",
                    "Matched model access rule '" + selected.Name + "'.");
            }

            return BuildResult(
                policy.DefaultDecision,
                mode,
                "Policy",
                policy,
                null,
                null,
                policy.DefaultDecision == ModelAccessDefaultDecisionEnum.Permit ? "PolicyDefaultPermit" : "PolicyDefaultDeny",
                "No active model access rule matched; using the policy default decision.");
        }

        /// <inheritdoc />
        public Task<ModelAccessEvaluationResult> ExplainAsync(ModelAccessEvaluationContext context, CancellationToken token = default)
        {
            return EvaluateAsync(context, token);
        }

        /// <inheritdoc />
        public async Task<ResourceValidationResult> ValidatePolicyAsync(string tenantId, ModelAccessPolicy policy, string existingId = null, CancellationToken token = default)
        {
            ResourceValidationResult result = CreateResult();
            if (policy == null)
            {
                AddError(result, "InvalidBody", null, "A model access policy payload is required.");
                return Finalize(result);
            }

            string effectiveTenantId = !String.IsNullOrWhiteSpace(tenantId) ? tenantId : policy.TenantId;
            if (String.IsNullOrWhiteSpace(effectiveTenantId))
            {
                AddError(result, "TenantIdRequired", "TenantId", "TenantId is required.");
            }

            if (String.IsNullOrWhiteSpace(policy.Name))
            {
                AddError(result, "NameRequired", "Name", "Name is required.");
            }

            if (!Enum.IsDefined(typeof(ModelAccessDefaultDecisionEnum), policy.DefaultDecision))
            {
                AddError(result, "DefaultDecisionInvalid", "DefaultDecision", "DefaultDecision is invalid.");
            }

            HashSet<string> ruleIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<int, List<ModelAccessRule>> rulesByPriority = new Dictionary<int, List<ModelAccessRule>>();
            foreach (ModelAccessRule rule in policy.Rules ?? new List<ModelAccessRule>())
            {
                token.ThrowIfCancellationRequested();
                ValidateRuleShape(result, rule, ruleIds, rulesByPriority);
                if (!String.IsNullOrWhiteSpace(effectiveTenantId))
                {
                    await ValidateRuleReferencesAsync(result, effectiveTenantId, rule, token).ConfigureAwait(false);
                }
            }

            foreach (KeyValuePair<int, List<ModelAccessRule>> pair in rulesByPriority)
            {
                if (pair.Value.Count > 1)
                {
                    AddWarning(result, "DuplicatePriority", "Rules", "Multiple rules use priority " + pair.Key + "; deny rules will win over allow rules at the same priority.");
                }
            }

            return Finalize(result);
        }

        /// <inheritdoc />
        public void InvalidateCache(string tenantId = null, string policyId = null)
        {
            lock (_CacheLock)
            {
                if (String.IsNullOrWhiteSpace(tenantId) && String.IsNullOrWhiteSpace(policyId))
                {
                    _PolicyCache.Clear();
                    return;
                }

                List<string> keys = _PolicyCache.Keys
                    .Where(key =>
                        (String.IsNullOrWhiteSpace(tenantId) || key.StartsWith(tenantId + "|", StringComparison.Ordinal)) &&
                        (String.IsNullOrWhiteSpace(policyId) || key.EndsWith("|" + policyId, StringComparison.Ordinal)))
                    .ToList();

                foreach (string key in keys)
                {
                    _PolicyCache.Remove(key);
                }
            }
        }

        /// <summary>
        /// Normalize a request type to a model access action.
        /// </summary>
        /// <param name="requestType">Request type.</param>
        /// <param name="fallback">Fallback action.</param>
        /// <returns>Model access action.</returns>
        public static ModelAccessActionEnum MapAction(RequestTypeEnum requestType, ModelAccessActionEnum fallback = ModelAccessActionEnum.Completions)
        {
            switch (requestType)
            {
                case RequestTypeEnum.OpenAIEmbeddings:
                case RequestTypeEnum.GeminiEmbedContent:
                case RequestTypeEnum.OllamaEmbeddings:
                    return ModelAccessActionEnum.Embeddings;
                case RequestTypeEnum.OpenAIListModels:
                case RequestTypeEnum.GeminiListModels:
                case RequestTypeEnum.OllamaListTags:
                case RequestTypeEnum.OllamaListRunningModels:
                    return ModelAccessActionEnum.ListModels;
                case RequestTypeEnum.OllamaShowModelInfo:
                    return ModelAccessActionEnum.ShowModel;
                case RequestTypeEnum.LoadModelRunnerEndpointModel:
                case RequestTypeEnum.LoadVirtualModelRunnerModel:
                case RequestTypeEnum.OllamaPullModel:
                    return ModelAccessActionEnum.LoadModel;
                case RequestTypeEnum.OllamaDeleteModel:
                    return ModelAccessActionEnum.UnloadModel;
                case RequestTypeEnum.OpenAIChatCompletions:
                case RequestTypeEnum.OpenAICompletions:
                case RequestTypeEnum.GeminiGenerateContent:
                case RequestTypeEnum.GeminiStreamGenerateContent:
                case RequestTypeEnum.OllamaGenerate:
                case RequestTypeEnum.OllamaChat:
                    return ModelAccessActionEnum.Completions;
                default:
                    return fallback;
            }
        }

        private ModelAccessEnforcementModeEnum GetEffectiveMode()
        {
            if (!_Settings.Enabled) return ModelAccessEnforcementModeEnum.Disabled;
            return _Settings.Mode;
        }

        private async Task<CachedPolicy> LoadPolicyAsync(string tenantId, string policyId, CancellationToken token)
        {
            string key = GetCacheKey(tenantId, policyId);
            if (_Settings.CacheTtlMs > 0)
            {
                lock (_CacheLock)
                {
                    if (_PolicyCache.TryGetValue(key, out CachedPolicy cached)
                        && (DateTime.UtcNow - cached.CachedUtc).TotalMilliseconds <= _Settings.CacheTtlMs)
                    {
                        return cached;
                    }
                }
            }

            ModelAccessPolicy policy = await _Database.ModelAccessPolicy.ReadAsync(tenantId, policyId, token).ConfigureAwait(false);
            if (policy == null)
            {
                return null;
            }

            EnumerationResult<ModelAccessRule> rules = await _Database.ModelAccessPolicy.EnumerateRulesAsync(
                tenantId,
                policyId,
                new EnumerationRequest { MaxResults = 10000 },
                token).ConfigureAwait(false);

            CachedPolicy snapshot = new CachedPolicy
            {
                Policy = policy,
                Rules = rules.Data ?? new List<ModelAccessRule>(),
                PolicyLastUpdateUtc = policy.LastUpdateUtc,
                CachedUtc = DateTime.UtcNow
            };

            if (_Settings.CacheTtlMs > 0 && policy.Active)
            {
                lock (_CacheLock)
                {
                    _PolicyCache[key] = snapshot;
                }
            }

            return snapshot;
        }

        private static string GetCacheKey(string tenantId, string policyId)
        {
            return tenantId + "|" + policyId;
        }

        private static ModelAccessActionEnum NormalizeAction(ModelAccessEvaluationContext context)
        {
            return MapAction(context.RequestType, context.Action);
        }

        private bool IsUnknownModelDenied(ModelAccessEvaluationContext context, ModelAccessActionEnum action)
        {
            if (!IsModelScopedAction(action)) return false;
            bool modelKnown = !String.IsNullOrWhiteSpace(context.ModelDefinitionId)
                || !String.IsNullOrWhiteSpace(context.RequestedModel)
                || !String.IsNullOrWhiteSpace(context.EffectiveModel);
            if (modelKnown) return false;
            return _Settings.UnknownModelBehavior == ModelAccessUnknownModelBehaviorEnum.Deny
                || _Settings.UnknownModelBehavior == ModelAccessUnknownModelBehaviorEnum.RequireStrictVirtualModelRunnerResolution;
        }

        private static bool IsModelScopedAction(ModelAccessActionEnum action)
        {
            return action == ModelAccessActionEnum.Completions
                || action == ModelAccessActionEnum.Embeddings
                || action == ModelAccessActionEnum.ShowModel
                || action == ModelAccessActionEnum.LoadModel
                || action == ModelAccessActionEnum.UnloadModel;
        }

        private ModelAccessEvaluationResult BuildGlobalDefaultResult(ModelAccessEnforcementModeEnum mode, string reasonCode, string reasonText, ModelAccessPolicy policy = null)
        {
            return BuildResult(
                _Settings.DefaultDecision,
                mode,
                "Global",
                policy,
                null,
                null,
                reasonCode,
                reasonText);
        }

        private bool ShouldBypassForAdministrator(ModelAccessEvaluationContext context)
        {
            if (context == null)
            {
                return false;
            }

            if (_Settings.AllowGlobalAdministratorBypass && context.IsUserAdmin)
            {
                return true;
            }

            return _Settings.AllowAdministratorBypass && (context.IsUserAdmin || context.IsUserTenantAdmin);
        }

        private static ModelAccessEvaluationResult BuildResult(
            ModelAccessDefaultDecisionEnum decision,
            ModelAccessEnforcementModeEnum mode,
            string defaultSource,
            ModelAccessPolicy policy,
            ModelAccessRule rule,
            ModelAccessRuleEffectEnum? effect,
            string reasonCode,
            string reasonText)
        {
            return new ModelAccessEvaluationResult
            {
                Decision = decision,
                Effect = effect,
                Mode = mode,
                DefaultSource = defaultSource,
                PolicyId = policy?.Id,
                PolicyName = policy?.Name,
                RuleId = rule?.Id,
                RuleName = rule?.Name,
                ReasonCode = reasonCode,
                ReasonText = reasonText,
                WouldDeny = mode == ModelAccessEnforcementModeEnum.Monitor && decision == ModelAccessDefaultDecisionEnum.Deny,
                EvaluatedUtc = DateTime.UtcNow
            };
        }

        private static bool RuleMatches(ModelAccessRule rule, ModelAccessEvaluationContext context, ModelAccessActionEnum action)
        {
            if (rule == null || !rule.Active) return false;
            if (rule.Actions == null || rule.Actions.Count < 1) return false;
            if (!rule.Actions.Contains(action)) return false;
            if (!String.IsNullOrWhiteSpace(rule.VirtualModelRunnerId)
                && !String.Equals(rule.VirtualModelRunnerId, context.VirtualModelRunnerId, StringComparison.Ordinal))
            {
                return false;
            }

            return SubjectMatches(rule, context) && ResourceMatches(rule, context);
        }

        private static bool SubjectMatches(ModelAccessRule rule, ModelAccessEvaluationContext context)
        {
            switch (rule.SubjectType)
            {
                case ModelAccessSubjectTypeEnum.Credential:
                    return StringEquals(rule.SubjectId, context.CredentialId);
                case ModelAccessSubjectTypeEnum.CredentialLabel:
                    return LabelSelectorMatches(rule.SubjectId, rule.SubjectSelector, context.CredentialLabels);
                case ModelAccessSubjectTypeEnum.User:
                    return StringEquals(rule.SubjectId, context.UserId);
                case ModelAccessSubjectTypeEnum.UserLabel:
                    return LabelSelectorMatches(rule.SubjectId, rule.SubjectSelector, context.UserLabels);
                case ModelAccessSubjectTypeEnum.Tenant:
                    return String.IsNullOrWhiteSpace(rule.SubjectId) || StringEquals(rule.SubjectId, context.TenantId);
                case ModelAccessSubjectTypeEnum.Any:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ResourceMatches(ModelAccessRule rule, ModelAccessEvaluationContext context)
        {
            switch (rule.ResourceType)
            {
                case ModelAccessResourceTypeEnum.ModelDefinition:
                    return StringEquals(rule.ResourceId, context.ModelDefinitionId);
                case ModelAccessResourceTypeEnum.ModelName:
                    return ModelNameMatches(rule, context);
                case ModelAccessResourceTypeEnum.ModelLabel:
                    return LabelSelectorMatches(rule.ResourceId, rule.ResourceSelector, context.ModelLabels);
                case ModelAccessResourceTypeEnum.VirtualModelRunner:
                    return StringEquals(rule.ResourceId, context.VirtualModelRunnerId)
                        || StringEquals(rule.VirtualModelRunnerId, context.VirtualModelRunnerId);
                case ModelAccessResourceTypeEnum.Any:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ModelNameMatches(ModelAccessRule rule, ModelAccessEvaluationContext context)
        {
            if (!String.IsNullOrWhiteSpace(rule.ResourceId)
                && (StringEquals(rule.ResourceId, context.RequestedModel) || StringEquals(rule.ResourceId, context.EffectiveModel)))
            {
                return true;
            }

            if (rule.ResourceSelector == null || rule.ResourceSelector.Count < 1) return false;
            return TextSelectorMatches(rule.ResourceSelector, context.RequestedModel)
                || TextSelectorMatches(rule.ResourceSelector, context.EffectiveModel);
        }

        private static bool LabelSelectorMatches(string idValue, Dictionary<string, string> selector, List<string> labels)
        {
            if (labels == null) return false;
            if (!String.IsNullOrWhiteSpace(idValue) && labels.Contains(idValue, StringComparer.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (selector == null || selector.Count < 1) return false;
            if (selector.TryGetValue("label", out string label) && labels.Contains(label, StringComparer.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (selector.TryGetValue("labels", out string labelsValue))
            {
                string[] expectedLabels = labelsValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                return expectedLabels.Any(item => labels.Contains(item.Trim(), StringComparer.InvariantCultureIgnoreCase));
            }

            return false;
        }

        private static bool TextSelectorMatches(Dictionary<string, string> selector, string value)
        {
            if (String.IsNullOrWhiteSpace(value) || selector == null) return false;

            if (selector.TryGetValue("value", out string valueSelector) && StringEquals(valueSelector, value)) return true;
            if (selector.TryGetValue("equals", out string equalsSelector) && StringEquals(equalsSelector, value)) return true;
            if (selector.TryGetValue("prefix", out string prefixSelector) && value.StartsWith(prefixSelector, StringComparison.OrdinalIgnoreCase)) return true;
            if (selector.TryGetValue("contains", out string containsSelector) && value.IndexOf(containsSelector, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        private static bool StringEquals(string left, string right)
        {
            return !String.IsNullOrWhiteSpace(left)
                && !String.IsNullOrWhiteSpace(right)
                && String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateRuleShape(
            ResourceValidationResult result,
            ModelAccessRule rule,
            HashSet<string> ruleIds,
            Dictionary<int, List<ModelAccessRule>> rulesByPriority)
        {
            if (rule == null)
            {
                AddError(result, "RuleInvalid", "Rules", "Rule payload is required.");
                return;
            }

            if (String.IsNullOrWhiteSpace(rule.Id))
            {
                AddError(result, "RuleIdRequired", "Rules", "Rule Id is required.");
            }
            else if (!ruleIds.Add(rule.Id))
            {
                AddError(result, "DuplicateRuleId", "Rules", "Rule Id '" + rule.Id + "' is duplicated.");
            }

            if (String.IsNullOrWhiteSpace(rule.Name))
            {
                AddError(result, "RuleNameRequired", "Rules", "Rule name is required.");
            }

            if (!Enum.IsDefined(typeof(ModelAccessRuleEffectEnum), rule.Effect))
            {
                AddError(result, "RuleEffectInvalid", "Rules", "Rule effect is invalid.");
            }

            if (!Enum.IsDefined(typeof(ModelAccessSubjectTypeEnum), rule.SubjectType))
            {
                AddError(result, "SubjectTypeInvalid", "Rules", "Rule subject type is invalid.");
            }

            if (!Enum.IsDefined(typeof(ModelAccessResourceTypeEnum), rule.ResourceType))
            {
                AddError(result, "ResourceTypeInvalid", "Rules", "Rule resource type is invalid.");
            }

            if (rule.Actions == null || rule.Actions.Count < 1)
            {
                AddError(result, "ActionsRequired", "Rules", "Each active rule must include at least one action.");
            }
            else if (rule.Actions.Any(action => !Enum.IsDefined(typeof(ModelAccessActionEnum), action)))
            {
                AddError(result, "ActionInvalid", "Rules", "Rule action set contains an invalid action.");
            }

            ValidateSelector(result, "SubjectSelector", rule.SubjectSelector);
            ValidateSelector(result, "ResourceSelector", rule.ResourceSelector);

            if (!rulesByPriority.TryGetValue(rule.Priority, out List<ModelAccessRule> priorityRules))
            {
                priorityRules = new List<ModelAccessRule>();
                rulesByPriority[rule.Priority] = priorityRules;
            }
            priorityRules.Add(rule);
        }

        private async Task ValidateRuleReferencesAsync(ResourceValidationResult result, string tenantId, ModelAccessRule rule, CancellationToken token)
        {
            if (rule == null) return;

            if (!String.IsNullOrWhiteSpace(rule.TenantId) && !String.Equals(rule.TenantId, tenantId, StringComparison.Ordinal))
            {
                AddError(result, "RuleTenantMismatch", "Rules", "Rule '" + rule.Name + "' belongs to a different tenant.");
            }

            await ValidateSubjectReferenceAsync(result, tenantId, rule, token).ConfigureAwait(false);
            await ValidateResourceReferenceAsync(result, tenantId, rule, token).ConfigureAwait(false);

            if (!String.IsNullOrWhiteSpace(rule.VirtualModelRunnerId))
            {
                await ValidateVirtualModelRunnerReferenceAsync(result, tenantId, rule.VirtualModelRunnerId, "VirtualModelRunnerId", token).ConfigureAwait(false);
            }
        }

        private async Task ValidateSubjectReferenceAsync(ResourceValidationResult result, string tenantId, ModelAccessRule rule, CancellationToken token)
        {
            switch (rule.SubjectType)
            {
                case ModelAccessSubjectTypeEnum.Credential:
                    if (String.IsNullOrWhiteSpace(rule.SubjectId))
                    {
                        AddError(result, "SubjectIdRequired", "SubjectId", "Credential subject rules require SubjectId.");
                        return;
                    }
                    await ValidateCredentialReferenceAsync(result, tenantId, rule.SubjectId, token).ConfigureAwait(false);
                    break;
                case ModelAccessSubjectTypeEnum.User:
                    if (String.IsNullOrWhiteSpace(rule.SubjectId))
                    {
                        AddError(result, "SubjectIdRequired", "SubjectId", "User subject rules require SubjectId.");
                        return;
                    }
                    await ValidateUserReferenceAsync(result, tenantId, rule.SubjectId, token).ConfigureAwait(false);
                    break;
                case ModelAccessSubjectTypeEnum.CredentialLabel:
                case ModelAccessSubjectTypeEnum.UserLabel:
                    if (String.IsNullOrWhiteSpace(rule.SubjectId) && (rule.SubjectSelector == null || rule.SubjectSelector.Count < 1))
                    {
                        AddError(result, "SubjectSelectorRequired", "SubjectSelector", "Label subject rules require SubjectId or SubjectSelector.");
                    }
                    break;
                case ModelAccessSubjectTypeEnum.Tenant:
                    if (!String.IsNullOrWhiteSpace(rule.SubjectId) && !String.Equals(rule.SubjectId, tenantId, StringComparison.Ordinal))
                    {
                        AddError(result, "CrossTenantSubject", "SubjectId", "Tenant subject rules cannot reference another tenant.");
                    }
                    break;
            }
        }

        private async Task ValidateResourceReferenceAsync(ResourceValidationResult result, string tenantId, ModelAccessRule rule, CancellationToken token)
        {
            switch (rule.ResourceType)
            {
                case ModelAccessResourceTypeEnum.ModelDefinition:
                    if (String.IsNullOrWhiteSpace(rule.ResourceId))
                    {
                        AddError(result, "ResourceIdRequired", "ResourceId", "ModelDefinition resource rules require ResourceId.");
                        return;
                    }
                    await ValidateModelDefinitionReferenceAsync(result, tenantId, rule.ResourceId, token).ConfigureAwait(false);
                    break;
                case ModelAccessResourceTypeEnum.VirtualModelRunner:
                    string vmrId = !String.IsNullOrWhiteSpace(rule.ResourceId) ? rule.ResourceId : rule.VirtualModelRunnerId;
                    if (String.IsNullOrWhiteSpace(vmrId))
                    {
                        AddError(result, "ResourceIdRequired", "ResourceId", "VirtualModelRunner resource rules require ResourceId or VirtualModelRunnerId.");
                        return;
                    }
                    await ValidateVirtualModelRunnerReferenceAsync(result, tenantId, vmrId, "ResourceId", token).ConfigureAwait(false);
                    break;
                case ModelAccessResourceTypeEnum.ModelName:
                    if (String.IsNullOrWhiteSpace(rule.ResourceId) && (rule.ResourceSelector == null || rule.ResourceSelector.Count < 1))
                    {
                        AddError(result, "ResourceSelectorRequired", "ResourceSelector", "ModelName resource rules require ResourceId or ResourceSelector.");
                    }
                    break;
                case ModelAccessResourceTypeEnum.ModelLabel:
                    if (String.IsNullOrWhiteSpace(rule.ResourceId) && (rule.ResourceSelector == null || rule.ResourceSelector.Count < 1))
                    {
                        AddError(result, "ResourceSelectorRequired", "ResourceSelector", "ModelLabel resource rules require ResourceId or ResourceSelector.");
                    }
                    break;
            }
        }

        private async Task ValidateCredentialReferenceAsync(ResourceValidationResult result, string tenantId, string credentialId, CancellationToken token)
        {
            Credential credential = await _Database.Credential.ReadAsync(tenantId, credentialId, token).ConfigureAwait(false);
            if (credential != null)
            {
                if (!credential.Active) AddError(result, "CredentialInactive", "SubjectId", "Referenced credential '" + credentialId + "' is inactive.");
                return;
            }

            Credential anyTenantCredential = await _Database.Credential.ReadByIdAsync(credentialId, token).ConfigureAwait(false);
            AddError(result, anyTenantCredential == null ? "CredentialMissing" : "CrossTenantCredential", "SubjectId", "Referenced credential '" + credentialId + "' was not found in the same tenant.");
        }

        private async Task ValidateUserReferenceAsync(ResourceValidationResult result, string tenantId, string userId, CancellationToken token)
        {
            UserMaster user = await _Database.User.ReadAsync(tenantId, userId, token).ConfigureAwait(false);
            if (user != null)
            {
                if (!user.Active) AddError(result, "UserInactive", "SubjectId", "Referenced user '" + userId + "' is inactive.");
                return;
            }

            UserMaster anyTenantUser = await _Database.User.ReadByIdAsync(userId, token).ConfigureAwait(false);
            AddError(result, anyTenantUser == null ? "UserMissing" : "CrossTenantUser", "SubjectId", "Referenced user '" + userId + "' was not found in the same tenant.");
        }

        private async Task ValidateModelDefinitionReferenceAsync(ResourceValidationResult result, string tenantId, string modelDefinitionId, CancellationToken token)
        {
            ModelDefinition definition = await _Database.ModelDefinition.ReadAsync(tenantId, modelDefinitionId, token).ConfigureAwait(false);
            if (definition != null)
            {
                if (!definition.Active) AddError(result, "ModelDefinitionInactive", "ResourceId", "Referenced model definition '" + modelDefinitionId + "' is inactive.");
                return;
            }

            ModelDefinition anyTenantDefinition = await _Database.ModelDefinition.ReadByIdAsync(modelDefinitionId, token).ConfigureAwait(false);
            AddError(result, anyTenantDefinition == null ? "ModelDefinitionMissing" : "CrossTenantModelDefinition", "ResourceId", "Referenced model definition '" + modelDefinitionId + "' was not found in the same tenant.");
        }

        private async Task ValidateVirtualModelRunnerReferenceAsync(ResourceValidationResult result, string tenantId, string vmrId, string field, CancellationToken token)
        {
            VirtualModelRunner vmr = await _Database.VirtualModelRunner.ReadAsync(tenantId, vmrId, token).ConfigureAwait(false);
            if (vmr != null)
            {
                if (!vmr.Active) AddError(result, "VirtualModelRunnerInactive", field, "Referenced virtual model runner '" + vmrId + "' is inactive.");
                return;
            }

            VirtualModelRunner anyTenantVmr = await _Database.VirtualModelRunner.ReadByIdAsync(vmrId, token).ConfigureAwait(false);
            AddError(result, anyTenantVmr == null ? "VirtualModelRunnerMissing" : "CrossTenantVirtualModelRunner", field, "Referenced virtual model runner '" + vmrId + "' was not found in the same tenant.");
        }

        private static void ValidateSelector(ResourceValidationResult result, string field, Dictionary<string, string> selector)
        {
            if (selector == null || selector.Count < 1) return;

            foreach (KeyValuePair<string, string> pair in selector)
            {
                if (!_AllowedSelectorKeys.Contains(pair.Key))
                {
                    AddError(result, "SelectorInvalid", field, "Selector key '" + pair.Key + "' is not supported.");
                }

                if (String.IsNullOrWhiteSpace(pair.Value))
                {
                    AddError(result, "SelectorValueRequired", field, "Selector key '" + pair.Key + "' requires a non-empty value.");
                }
            }
        }

        private static ResourceValidationResult CreateResult()
        {
            return new ResourceValidationResult
            {
                ResourceType = "ModelAccessPolicy"
            };
        }

        private static ResourceValidationResult Finalize(ResourceValidationResult result)
        {
            result.IsValid = result.Errors.Count < 1;
            return result;
        }

        private static void AddError(ResourceValidationResult result, string code, string field, string message)
        {
            result.Errors.Add(new ResourceValidationIssue
            {
                Code = code,
                Field = field,
                Message = message
            });
        }

        private static void AddWarning(ResourceValidationResult result, string code, string field, string message)
        {
            result.Warnings.Add(new ResourceValidationIssue
            {
                Code = code,
                Field = field,
                Message = message
            });
        }

        private sealed class CachedPolicy
        {
            public ModelAccessPolicy Policy { get; set; }
            public List<ModelAccessRule> Rules { get; set; } = new List<ModelAccessRule>();
            public DateTime PolicyLastUpdateUtc { get; set; }
            public DateTime CachedUtc { get; set; }
        }
    }
}
