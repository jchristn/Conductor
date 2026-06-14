namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Inputs used to evaluate model access for a request or simulation.
    /// </summary>
    public class ModelAccessEvaluationContext
    {
        /// <summary>
        /// Tenant identifier for the policy evaluation.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// User identifier associated with the request.
        /// </summary>
        public string UserId { get; set; } = null;

        /// <summary>
        /// Labels assigned to the user.
        /// </summary>
        public List<string> UserLabels
        {
            get => _UserLabels;
            set => _UserLabels = (value != null ? value : new List<string>());
        }

        /// <summary>
        /// Whether the authenticated user is a global administrator.
        /// </summary>
        public bool IsUserAdmin { get; set; } = false;

        /// <summary>
        /// Whether the authenticated user is a tenant administrator.
        /// </summary>
        public bool IsUserTenantAdmin { get; set; } = false;

        /// <summary>
        /// Credential identifier associated with the request.
        /// </summary>
        public string CredentialId { get; set; } = null;

        /// <summary>
        /// Labels assigned to the credential.
        /// </summary>
        public List<string> CredentialLabels
        {
            get => _CredentialLabels;
            set => _CredentialLabels = (value != null ? value : new List<string>());
        }

        /// <summary>
        /// Virtual model runner identifier handling the request.
        /// </summary>
        public string VirtualModelRunnerId { get; set; } = null;

        /// <summary>
        /// Attached model access policy identifier, when known.
        /// </summary>
        public string ModelAccessPolicyId { get; set; } = null;

        /// <summary>
        /// Model name requested by the caller before mutation.
        /// </summary>
        public string RequestedModel { get; set; } = null;

        /// <summary>
        /// Effective model name after mutation or model resolution.
        /// </summary>
        public string EffectiveModel { get; set; } = null;

        /// <summary>
        /// Resolved model definition identifier, when available.
        /// </summary>
        public string ModelDefinitionId { get; set; } = null;

        /// <summary>
        /// Resolved model definition name, when available.
        /// </summary>
        public string ModelDefinitionName { get; set; } = null;

        /// <summary>
        /// Labels assigned to the resolved model definition.
        /// </summary>
        public List<string> ModelLabels
        {
            get => _ModelLabels;
            set => _ModelLabels = (value != null ? value : new List<string>());
        }

        /// <summary>
        /// Normalized model access action.
        /// </summary>
        public ModelAccessActionEnum Action { get; set; } = ModelAccessActionEnum.Completions;

        /// <summary>
        /// Original request type.
        /// </summary>
        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;

        /// <summary>
        /// API type exposed by the virtual model runner.
        /// </summary>
        public ApiTypeEnum ApiType { get; set; } = ApiTypeEnum.Ollama;

        /// <summary>
        /// UTC timestamp for the evaluation.
        /// </summary>
        public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;

        private List<string> _UserLabels = new List<string>();
        private List<string> _CredentialLabels = new List<string>();
        private List<string> _ModelLabels = new List<string>();
    }
}
