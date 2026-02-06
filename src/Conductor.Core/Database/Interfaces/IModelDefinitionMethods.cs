namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for model definition database methods.
    /// </summary>
    public interface IModelDefinitionMethods
    {
        /// <summary>
        /// Create a model definition.
        /// </summary>
        /// <param name="definition">Definition to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created definition.</returns>
        Task<ModelDefinition> CreateAsync(ModelDefinition definition, CancellationToken token = default);

        /// <summary>
        /// Read a model definition by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Definition ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Definition or null.</returns>
        Task<ModelDefinition> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a model definition by ID without tenant filtering (admin use only).
        /// </summary>
        /// <param name="id">Definition ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Definition or null if not found.</returns>
        Task<ModelDefinition> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update a model definition.
        /// </summary>
        /// <param name="definition">Definition to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated definition.</returns>
        Task<ModelDefinition> UpdateAsync(ModelDefinition definition, CancellationToken token = default);

        /// <summary>
        /// Delete a model definition by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Definition ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check if a model definition exists by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Definition ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate model definitions.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ModelDefinition>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);
    }
}
