namespace Conductor.Core.Responses
{
    using System;

    /// <summary>
    /// Response body of the audited reveal route GET /v1.0/modelrunnerendpoints/{id}/apikey.
    /// </summary>
    public class ModelRunnerEndpointApiKeyResponse
    {
        /// <summary>
        /// The full, unmasked upstream API key. Null when the endpoint has no key.
        /// </summary>
        public string ApiKey { get; set; } = null;

        /// <summary>
        /// Instantiate the response.
        /// </summary>
        public ModelRunnerEndpointApiKeyResponse()
        {
        }
    }
}
