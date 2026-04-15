namespace Conductor.Server
{
    using System.Collections.Generic;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Extension methods and helpers bridging Conductor's route registration style to Watson 7 OpenAPI metadata.
    /// </summary>
    public static class WatsonCompatExtensions
    {
        /// <summary>
        /// Set the OpenAPI summary for an operation.
        /// </summary>
        /// <param name="metadata">Route metadata; must not be null.</param>
        /// <param name="summary">Operation summary.</param>
        /// <returns>The same metadata instance for chaining.</returns>
        public static OpenApiRouteMetadata WithSummary(this OpenApiRouteMetadata metadata, string summary)
        {
            metadata.Summary = summary;
            return metadata;
        }

        /// <summary>
        /// Add a security scheme requirement to an operation.
        /// </summary>
        /// <param name="metadata">Route metadata; must not be null.</param>
        /// <param name="scheme">Security scheme name, e.g. "Bearer".</param>
        /// <returns>The same metadata instance for chaining.</returns>
        public static OpenApiRouteMetadata WithSecurity(this OpenApiRouteMetadata metadata, string scheme)
        {
            if (metadata.Security == null) metadata.Security = new List<string>();
            metadata.Security.Add(scheme);
            return metadata;
        }
    }

    /// <summary>
    /// Helper factories for Conductor's OpenAPI metadata.
    /// These mirror the generic helpers that were previously available on SwiftStack types and
    /// generate schema references by type name so Watson 7 can link them to registered components.
    /// </summary>
    public static class Api
    {
        /// <summary>
        /// Build a JSON response metadata referencing a schema by the type's name.
        /// </summary>
        /// <typeparam name="T">Response body type.</typeparam>
        /// <param name="description">Human-readable response description.</param>
        /// <returns>A response metadata instance.</returns>
        public static OpenApiResponseMetadata JsonResponse<T>(string description)
        {
            return OpenApiResponseMetadata.Json(description, OpenApiSchemaMetadata.CreateRef(typeof(T).Name));
        }

        /// <summary>
        /// Build a JSON request body metadata referencing a schema by the type's name.
        /// </summary>
        /// <typeparam name="T">Request body type.</typeparam>
        /// <param name="description">Human-readable body description.</param>
        /// <param name="required">True if the body is required (default: true).</param>
        /// <returns>A request body metadata instance.</returns>
        public static OpenApiRequestBodyMetadata JsonRequestBody<T>(string description, bool required = true)
        {
            return OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.CreateRef(typeof(T).Name), description, required);
        }
    }
}
