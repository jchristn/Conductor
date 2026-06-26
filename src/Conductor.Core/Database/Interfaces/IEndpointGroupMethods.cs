namespace Conductor.Core.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Models;

    /// <summary>
    /// Interface for endpoint group database methods.
    /// </summary>
    public interface IEndpointGroupMethods
    {
        /// <summary>
        /// Create an endpoint group.
        /// </summary>
        Task<EndpointGroup> CreateAsync(EndpointGroup group, CancellationToken token = default);

        /// <summary>
        /// Read an endpoint group by tenant and ID.
        /// </summary>
        Task<EndpointGroup> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read an endpoint group by ID without tenant filtering.
        /// </summary>
        Task<EndpointGroup> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Update an endpoint group.
        /// </summary>
        Task<EndpointGroup> UpdateAsync(EndpointGroup group, CancellationToken token = default);

        /// <summary>
        /// Delete an endpoint group.
        /// </summary>
        Task DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Check if an endpoint group exists.
        /// </summary>
        Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate endpoint groups.
        /// </summary>
        Task<EnumerationResult<EndpointGroup>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);
    }
}
