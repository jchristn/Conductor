namespace Conductor.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Full request history detail including headers and bodies.
    /// This data is persisted to the filesystem as JSON.
    /// </summary>
    public class RequestHistoryDetail : RequestHistoryEntry
    {
        /// <summary>
        /// Full structured routing decision when available.
        /// </summary>
        public RoutingDecision RoutingDecision { get; set; } = null;

        /// <summary>
        /// HTTP request headers.
        /// </summary>
        public Dictionary<string, string> RequestHeaders
        {
            get => _RequestHeaders;
            set => _RequestHeaders = (value != null ? value : new Dictionary<string, string>());
        }

        /// <summary>
        /// HTTP request body (truncated to MaxRequestBodyBytes).
        /// May be null if no request body was present.
        /// </summary>
        public string RequestBody { get; set; } = null;

        /// <summary>
        /// Indicates if the request body was truncated.
        /// </summary>
        public bool RequestBodyTruncated { get; set; } = false;

        /// <summary>
        /// HTTP response headers.
        /// </summary>
        public Dictionary<string, string> ResponseHeaders
        {
            get => _ResponseHeaders;
            set => _ResponseHeaders = (value != null ? value : new Dictionary<string, string>());
        }

        /// <summary>
        /// HTTP response body (truncated to MaxResponseBodyBytes).
        /// May be null if no response body was present or response not yet received.
        /// </summary>
        public string ResponseBody { get; set; } = null;

        /// <summary>
        /// Indicates if the response body was truncated.
        /// </summary>
        public bool ResponseBodyTruncated { get; set; } = false;

        private Dictionary<string, string> _RequestHeaders = new Dictionary<string, string>();
        private Dictionary<string, string> _ResponseHeaders = new Dictionary<string, string>();

        /// <summary>
        /// Instantiate the request history detail.
        /// </summary>
        public RequestHistoryDetail()
        {
        }

        /// <summary>
        /// Create a RequestHistoryDetail from a RequestHistoryEntry.
        /// </summary>
        /// <param name="entry">The source entry.</param>
        /// <returns>A new RequestHistoryDetail with copied properties, or null if entry is null.</returns>
        public static RequestHistoryDetail FromEntry(RequestHistoryEntry entry)
        {
            if (entry == null) return null;

            return new RequestHistoryDetail
            {
                Id = entry.Id,
                TenantGuid = entry.TenantGuid,
                VirtualModelRunnerGuid = entry.VirtualModelRunnerGuid,
                VirtualModelRunnerName = entry.VirtualModelRunnerName,
                RequestorUserGuid = entry.RequestorUserGuid,
                RequestorUserEmail = entry.RequestorUserEmail,
                CredentialGuid = entry.CredentialGuid,
                CredentialName = entry.CredentialName,
                LoadBalancingPolicyGuid = entry.LoadBalancingPolicyGuid,
                LoadBalancingPolicyName = entry.LoadBalancingPolicyName,
                ModelEndpointGuid = entry.ModelEndpointGuid,
                ModelEndpointName = entry.ModelEndpointName,
                ModelEndpointUrl = entry.ModelEndpointUrl,
                ModelDefinitionGuid = entry.ModelDefinitionGuid,
                ModelDefinitionName = entry.ModelDefinitionName,
                ModelConfigurationGuid = entry.ModelConfigurationGuid,
                RequestedModel = entry.RequestedModel,
                EffectiveModel = entry.EffectiveModel,
                RequestType = entry.RequestType,
                RoutingOutcomeCode = entry.RoutingOutcomeCode,
                DenialReasonCode = entry.DenialReasonCode,
                DenialReason = entry.DenialReason,
                SessionAffinityOutcome = entry.SessionAffinityOutcome,
                MutationSummary = entry.MutationSummary,
                ExplanationSummary = entry.ExplanationSummary,
                RequestBodyRetained = entry.RequestBodyRetained,
                RequestBodyRedacted = entry.RequestBodyRedacted,
                RequestHeadersRedacted = entry.RequestHeadersRedacted,
                ResponseBodyRetained = entry.ResponseBodyRetained,
                ResponseBodyRedacted = entry.ResponseBodyRedacted,
                ResponseHeadersRedacted = entry.ResponseHeadersRedacted,
                RequestorSourceIp = entry.RequestorSourceIp,
                HttpMethod = entry.HttpMethod,
                HttpUrl = entry.HttpUrl,
                RequestBodyLength = entry.RequestBodyLength,
                ResponseBodyLength = entry.ResponseBodyLength,
                HttpStatus = entry.HttpStatus,
                FirstTokenTimeMs = entry.FirstTokenTimeMs,
                ResponseTimeMs = entry.ResponseTimeMs,
                ObjectKey = entry.ObjectKey,
                RequestTransferType = entry.RequestTransferType,
                ResponseTransferType = entry.ResponseTransferType,
                CreatedUtc = entry.CreatedUtc,
                CompletedUtc = entry.CompletedUtc
            };
        }
    }
}
