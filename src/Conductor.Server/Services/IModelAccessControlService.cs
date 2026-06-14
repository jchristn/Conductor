namespace Conductor.Server.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Service for validating and evaluating model access policies.
    /// </summary>
    public interface IModelAccessControlService
    {
        /// <summary>
        /// Evaluate model access for a request or simulation context.
        /// </summary>
        /// <param name="context">Evaluation context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Evaluation result.</returns>
        Task<ModelAccessEvaluationResult> EvaluateAsync(ModelAccessEvaluationContext context, CancellationToken token = default);

        /// <summary>
        /// Evaluate model access and return an explainable result for dashboard or SDK simulation.
        /// </summary>
        /// <param name="context">Evaluation context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Evaluation result.</returns>
        Task<ModelAccessEvaluationResult> ExplainAsync(ModelAccessEvaluationContext context, CancellationToken token = default);

        /// <summary>
        /// Validate a model access policy draft and its rules.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="policy">Policy draft.</param>
        /// <param name="existingId">Existing policy identifier during updates.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Validation result.</returns>
        Task<ResourceValidationResult> ValidatePolicyAsync(string tenantId, ModelAccessPolicy policy, string existingId = null, CancellationToken token = default);

        /// <summary>
        /// Invalidate cached model access policy data.
        /// </summary>
        /// <param name="tenantId">Optional tenant identifier.</param>
        /// <param name="policyId">Optional policy identifier.</param>
        void InvalidateCache(string tenantId = null, string policyId = null);
    }
}
