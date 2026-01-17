namespace Conductor.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for model runner endpoint database methods.
    /// </summary>
    public interface IModelRunnerEndpointMethods
    {
        /// <summary>
        /// Create a model runner endpoint.
        /// </summary>
        /// <param name="endpoint">Endpoint to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created endpoint.</returns>
        Task<ModelRunnerEndpoint> CreateAsync(ModelRunnerEndpoint endpoint, CancellationToken token = default);

        /// <summary>
        /// Read a model runner endpoint by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Endpoint ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Endpoint or null.</returns>
        Task<ModelRunnerEndpoint> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Update a model runner endpoint.
        /// </summary>
        /// <param name="endpoint">Endpoint to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated endpoint.</returns>
        Task<ModelRunnerEndpoint> UpdateAsync(ModelRunnerEndpoint endpoint, CancellationToken token = default);

        /// <summary>
        /// Delete a model runner endpoint by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Endpoint ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check if a model runner endpoint exists by ID.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="id">Endpoint ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if exists.</returns>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate model runner endpoints.
        /// </summary>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ModelRunnerEndpoint>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);
    }
}
