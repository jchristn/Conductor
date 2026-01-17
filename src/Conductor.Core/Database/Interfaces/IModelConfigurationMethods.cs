namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for model configuration database methods.
    /// </summary>
    public interface IModelConfigurationMethods
    {
        /// <summary>
        /// Create a model configuration.
        /// </summary>
        /// <param name="configuration">Configuration to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created configuration.</returns>
        Task<ModelConfiguration> CreateAsync(ModelConfiguration configuration, CancellationToken token = default);

        /// <summary>
        /// Read a model configuration by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Configuration ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Configuration or null.</returns>
        Task<ModelConfiguration> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Update a model configuration.
        /// </summary>
        /// <param name="configuration">Configuration to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated configuration.</returns>
        Task<ModelConfiguration> UpdateAsync(ModelConfiguration configuration, CancellationToken token = default);

        /// <summary>
        /// Delete a model configuration by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Configuration ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check if a model configuration exists by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Configuration ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate model configurations.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ModelConfiguration>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);
    }
}
