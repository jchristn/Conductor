namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Enums;
    using Conductor.Core.Helpers;

    /// <summary>
    /// Represents a request history entry stored in the database.
    /// </summary>
    public class RequestHistoryEntry
    {
        /// <summary>
        /// K-sortable unique identifier (e.g., req_...).
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewRequestHistoryId();

        /// <summary>
        /// Tenant GUID (inherited from Virtual Model Runner).
        /// </summary>
        public string TenantGuid
        {
            get => _TenantGuid;
            set => _TenantGuid = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(TenantGuid)) : value);
        }

        /// <summary>
        /// Virtual Model Runner GUID.
        /// </summary>
        public string VirtualModelRunnerGuid
        {
            get => _VirtualModelRunnerGuid;
            set => _VirtualModelRunnerGuid = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(VirtualModelRunnerGuid)) : value);
        }

        /// <summary>
        /// Virtual Model Runner name at time of request.
        /// </summary>
        public string VirtualModelRunnerName
        {
            get => _VirtualModelRunnerName;
            set => _VirtualModelRunnerName = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(VirtualModelRunnerName)) : value);
        }

        /// <summary>
        /// Requestor user GUID, if authenticated. May be null.
        /// </summary>
        public string RequestorUserGuid { get; set; } = null;

        /// <summary>
        /// Requestor user email, if authenticated. May be null.
        /// </summary>
        public string RequestorUserEmail { get; set; } = null;

        /// <summary>
        /// Credential GUID used to authenticate the request, if available. May be null.
        /// </summary>
        public string CredentialGuid { get; set; } = null;

        /// <summary>
        /// Credential name used to authenticate the request, if available. May be null.
        /// </summary>
        public string CredentialName { get; set; } = null;

        /// <summary>
        /// Attached load-balancing policy GUID, if any. May be null.
        /// </summary>
        public string LoadBalancingPolicyGuid { get; set; } = null;

        /// <summary>
        /// Attached load-balancing policy name, if any. May be null.
        /// </summary>
        public string LoadBalancingPolicyName { get; set; } = null;

        /// <summary>
        /// Attached model access policy GUID, if evaluated. May be null.
        /// </summary>
        public string ModelAccessPolicyGuid { get; set; } = null;

        /// <summary>
        /// Attached model access policy name, if evaluated. May be null.
        /// </summary>
        public string ModelAccessPolicyName { get; set; } = null;

        /// <summary>
        /// Matched model access rule GUID, if any. May be null.
        /// </summary>
        public string ModelAccessRuleGuid { get; set; } = null;

        /// <summary>
        /// Matched model access rule name, if any. May be null.
        /// </summary>
        public string ModelAccessRuleName { get; set; } = null;

        /// <summary>
        /// Model access decision, if evaluated. May be null.
        /// </summary>
        public string ModelAccessDecision { get; set; } = null;

        /// <summary>
        /// Whether monitor mode observed that model access would deny the request.
        /// </summary>
        public bool ModelAccessWouldDeny { get; set; } = false;

        /// <summary>
        /// Model Endpoint GUID, if routed. May be null.
        /// </summary>
        public string ModelEndpointGuid { get; set; } = null;

        /// <summary>
        /// Model Endpoint name at time of request. May be null.
        /// </summary>
        public string ModelEndpointName { get; set; } = null;

        /// <summary>
        /// Model Endpoint URL at time of request. May be null.
        /// </summary>
        public string ModelEndpointUrl { get; set; } = null;

        /// <summary>
        /// Model Definition GUID. May be null.
        /// </summary>
        public string ModelDefinitionGuid { get; set; } = null;

        /// <summary>
        /// Model name from the model definition. May be null.
        /// </summary>
        public string ModelDefinitionName { get; set; } = null;

        /// <summary>
        /// Model Configuration GUID. May be null.
        /// </summary>
        public string ModelConfigurationGuid { get; set; } = null;

        /// <summary>
        /// Requested model name before mutation. May be null.
        /// </summary>
        public string RequestedModel { get; set; } = null;

        /// <summary>
        /// Effective model name after mutation. May be null.
        /// </summary>
        public string EffectiveModel { get; set; } = null;

        /// <summary>
        /// Request type at the time of capture. May be null.
        /// </summary>
        public string RequestType { get; set; } = null;

        /// <summary>
        /// High-level routing outcome code. May be null.
        /// </summary>
        public string RoutingOutcomeCode { get; set; } = null;

        /// <summary>
        /// Denial reason code for non-routed requests. May be null.
        /// </summary>
        public string DenialReasonCode { get; set; } = null;

        /// <summary>
        /// Denial reason detail for non-routed requests. May be null.
        /// </summary>
        public string DenialReason { get; set; } = null;

        /// <summary>
        /// Session-affinity outcome for this request. May be null.
        /// </summary>
        public string SessionAffinityOutcome { get; set; } = null;

        /// <summary>
        /// Compact mutation summary for search and ledger views. May be null.
        /// </summary>
        public string MutationSummary { get; set; } = null;

        /// <summary>
        /// Compact routing explanation summary for search and ledger views. May be null.
        /// </summary>
        public string ExplanationSummary { get; set; } = null;

        /// <summary>
        /// Whether the request body was retained in detail storage.
        /// </summary>
        public bool RequestBodyRetained { get; set; } = false;

        /// <summary>
        /// Whether the request body was redacted before persistence.
        /// </summary>
        public bool RequestBodyRedacted { get; set; } = false;

        /// <summary>
        /// Whether request headers were redacted before persistence.
        /// </summary>
        public bool RequestHeadersRedacted { get; set; } = false;

        /// <summary>
        /// Whether the response body was retained in detail storage.
        /// </summary>
        public bool ResponseBodyRetained { get; set; } = false;

        /// <summary>
        /// Whether the response body was redacted before persistence.
        /// </summary>
        public bool ResponseBodyRedacted { get; set; } = false;

        /// <summary>
        /// Whether response headers were redacted before persistence.
        /// </summary>
        public bool ResponseHeadersRedacted { get; set; } = false;

        /// <summary>
        /// Requestor's source IP address.
        /// </summary>
        public string RequestorSourceIp
        {
            get => _RequestorSourceIp;
            set => _RequestorSourceIp = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(RequestorSourceIp)) : value);
        }

        /// <summary>
        /// HTTP method (GET, POST, etc.).
        /// </summary>
        public string HttpMethod
        {
            get => _HttpMethod;
            set => _HttpMethod = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(HttpMethod)) : value);
        }

        /// <summary>
        /// HTTP request URL.
        /// </summary>
        public string HttpUrl
        {
            get => _HttpUrl;
            set => _HttpUrl = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(HttpUrl)) : value);
        }

        /// <summary>
        /// Request body length in bytes.
        /// </summary>
        public long RequestBodyLength { get; set; } = 0;

        /// <summary>
        /// Response body length in bytes. May be null if response not yet received.
        /// </summary>
        public long? ResponseBodyLength { get; set; } = null;

        /// <summary>
        /// HTTP response status code. May be null if response not yet received.
        /// </summary>
        public int? HttpStatus { get; set; } = null;

        /// <summary>
        /// Time to first token/byte in milliseconds. For non-streaming responses, this matches ResponseTimeMs.
        /// May be null if response not yet received.
        /// </summary>
        public int? FirstTokenTimeMs { get; set; } = null;

        /// <summary>
        /// Response time in milliseconds. May be null if response not yet received.
        /// </summary>
        public int? ResponseTimeMs { get; set; } = null;

        /// <summary>
        /// Trace ID linking request history, analytics events, logs, and provider IDs.
        /// </summary>
        public string TraceId { get; set; } = IdGenerator.NewTraceId();

        /// <summary>
        /// Provider request ID when returned by the upstream service.
        /// </summary>
        public string ProviderRequestId { get; set; } = null;

        /// <summary>
        /// Provider family, such as OpenAI, Gemini, Ollama, or vLLM.
        /// </summary>
        public string ProviderName { get; set; } = null;

        /// <summary>
        /// Prompt/input token count.
        /// </summary>
        public int? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion/output token count.
        /// </summary>
        public int? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Total token count.
        /// </summary>
        public int? TotalTokens { get; set; } = null;

        /// <summary>
        /// Overall token throughput.
        /// </summary>
        public decimal? TokensPerSecondOverall { get; set; } = null;

        /// <summary>
        /// Generation token throughput.
        /// </summary>
        public decimal? TokensPerSecondGeneration { get; set; } = null;

        /// <summary>
        /// Whether detailed request analytics events were captured.
        /// </summary>
        public bool AnalyticsCaptured { get; set; } = false;

        /// <summary>
        /// Request analytics schema version.
        /// </summary>
        public int AnalyticsVersion { get; set; } = 1;

        /// <summary>
        /// Longest measured stage kind.
        /// </summary>
        public string DominantStageKind { get; set; } = null;

        /// <summary>
        /// Longest measured stage duration.
        /// </summary>
        public int? DominantStageDurationMs { get; set; } = null;

        /// <summary>
        /// Stable code describing why analytics were not captured.
        /// </summary>
        public string AnalyticsFailureCode { get; set; } = null;

        /// <summary>
        /// Filesystem object key for the full request/response data.
        /// </summary>
        public string ObjectKey
        {
            get => _ObjectKey;
            set => _ObjectKey = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(ObjectKey)) : value);
        }

        /// <summary>
        /// Transfer type used for the request (Normal, Chunked, or ServerSentEvents).
        /// Defaults to Normal.
        /// </summary>
        public TransferTypeEnum RequestTransferType { get; set; } = TransferTypeEnum.Normal;

        /// <summary>
        /// Transfer type used for the response (Normal, Chunked, or ServerSentEvents).
        /// Defaults to Normal.
        /// </summary>
        public TransferTypeEnum ResponseTransferType { get; set; } = TransferTypeEnum.Normal;

        /// <summary>
        /// Record creation timestamp (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Response completion timestamp (UTC). May be null if response not yet received.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        private string _TenantGuid = null;
        private string _VirtualModelRunnerGuid = null;
        private string _VirtualModelRunnerName = null;
        private string _RequestorSourceIp = null;
        private string _HttpMethod = null;
        private string _HttpUrl = null;
        private string _ObjectKey = null;

        /// <summary>
        /// Instantiate the request history entry.
        /// </summary>
        public RequestHistoryEntry()
        {
        }

        /// <summary>
        /// Instantiate from DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <returns>Instance, or null if row is null.</returns>
        public static RequestHistoryEntry FromDataRow(DataRow row)
        {
            if (row == null) return null;

            RequestHistoryEntry obj = new RequestHistoryEntry
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantGuid = DataTableHelper.GetStringValue(row, "tenantguid"),
                VirtualModelRunnerGuid = DataTableHelper.GetStringValue(row, "virtualmodelrunnerguid"),
                VirtualModelRunnerName = DataTableHelper.GetStringValue(row, "virtualmodelrunnername"),
                RequestorUserGuid = DataTableHelper.GetStringValue(row, "requestoruserguid"),
                RequestorUserEmail = DataTableHelper.GetStringValue(row, "requestoruseremail"),
                CredentialGuid = DataTableHelper.GetStringValue(row, "credentialguid"),
                CredentialName = DataTableHelper.GetStringValue(row, "credentialname"),
                LoadBalancingPolicyGuid = DataTableHelper.GetStringValue(row, "loadbalancingpolicyguid"),
                LoadBalancingPolicyName = DataTableHelper.GetStringValue(row, "loadbalancingpolicyname"),
                ModelAccessPolicyGuid = DataTableHelper.GetStringValue(row, "modelaccesspolicyguid"),
                ModelAccessPolicyName = DataTableHelper.GetStringValue(row, "modelaccesspolicyname"),
                ModelAccessRuleGuid = DataTableHelper.GetStringValue(row, "modelaccessruleguid"),
                ModelAccessRuleName = DataTableHelper.GetStringValue(row, "modelaccessrulename"),
                ModelAccessDecision = DataTableHelper.GetStringValue(row, "modelaccessdecision"),
                ModelAccessWouldDeny = DataTableHelper.GetBooleanValue(row, "modelaccesswoulddeny"),
                ModelEndpointGuid = DataTableHelper.GetStringValue(row, "modelendpointguid"),
                ModelEndpointName = DataTableHelper.GetStringValue(row, "modelendpointname"),
                ModelEndpointUrl = DataTableHelper.GetStringValue(row, "modelendpointurl"),
                ModelDefinitionGuid = DataTableHelper.GetStringValue(row, "modeldefinitionguid"),
                ModelDefinitionName = DataTableHelper.GetStringValue(row, "modeldefinitionname"),
                ModelConfigurationGuid = DataTableHelper.GetStringValue(row, "modelconfigurationguid"),
                RequestedModel = DataTableHelper.GetStringValue(row, "requestedmodel"),
                EffectiveModel = DataTableHelper.GetStringValue(row, "effectivemodel"),
                RequestType = DataTableHelper.GetStringValue(row, "requesttype"),
                RoutingOutcomeCode = DataTableHelper.GetStringValue(row, "routingoutcomecode"),
                DenialReasonCode = DataTableHelper.GetStringValue(row, "denialreasoncode"),
                DenialReason = DataTableHelper.GetStringValue(row, "denialreason"),
                SessionAffinityOutcome = DataTableHelper.GetStringValue(row, "sessionaffinityoutcome"),
                MutationSummary = DataTableHelper.GetStringValue(row, "mutationsummary"),
                ExplanationSummary = DataTableHelper.GetStringValue(row, "explanationsummary"),
                RequestBodyRetained = DataTableHelper.GetBooleanValue(row, "requestbodyretained"),
                RequestBodyRedacted = DataTableHelper.GetBooleanValue(row, "requestbodyredacted"),
                RequestHeadersRedacted = DataTableHelper.GetBooleanValue(row, "requestheadersredacted"),
                ResponseBodyRetained = DataTableHelper.GetBooleanValue(row, "responsebodyretained"),
                ResponseBodyRedacted = DataTableHelper.GetBooleanValue(row, "responsebodyredacted"),
                ResponseHeadersRedacted = DataTableHelper.GetBooleanValue(row, "responseheadersredacted"),
                RequestorSourceIp = DataTableHelper.GetStringValue(row, "requestorsourceip"),
                HttpMethod = DataTableHelper.GetStringValue(row, "httpmethod"),
                HttpUrl = DataTableHelper.GetStringValue(row, "httpurl"),
                RequestBodyLength = DataTableHelper.GetLongValue(row, "requestbodylength"),
                ResponseBodyLength = DataTableHelper.GetNullableLongValue(row, "responsebodylength"),
                HttpStatus = DataTableHelper.GetNullableIntValue(row, "httpstatus"),
                FirstTokenTimeMs = DataTableHelper.GetNullableIntValue(row, "firsttokentimems"),
                ResponseTimeMs = DataTableHelper.GetNullableIntValue(row, "responsetimems"),
                TraceId = DataTableHelper.GetStringValue(row, "traceid") ?? IdGenerator.NewTraceId(),
                ProviderRequestId = DataTableHelper.GetStringValue(row, "providerrequestid"),
                ProviderName = DataTableHelper.GetStringValue(row, "providername"),
                PromptTokens = DataTableHelper.GetNullableIntValue(row, "prompttokens"),
                CompletionTokens = DataTableHelper.GetNullableIntValue(row, "completiontokens"),
                TotalTokens = DataTableHelper.GetNullableIntValue(row, "totaltokens"),
                TokensPerSecondOverall = DataTableHelper.GetNullableDecimalValue(row, "tokenspersecondoverall"),
                TokensPerSecondGeneration = DataTableHelper.GetNullableDecimalValue(row, "tokenspersecondgeneration"),
                AnalyticsCaptured = DataTableHelper.GetBooleanValue(row, "analyticscaptured"),
                AnalyticsVersion = DataTableHelper.GetIntValue(row, "analyticsversion") > 0 ? DataTableHelper.GetIntValue(row, "analyticsversion") : 1,
                DominantStageKind = DataTableHelper.GetStringValue(row, "dominantstagekind"),
                DominantStageDurationMs = DataTableHelper.GetNullableIntValue(row, "dominantstagedurationms"),
                AnalyticsFailureCode = DataTableHelper.GetStringValue(row, "analyticsfailurecode"),
                ObjectKey = DataTableHelper.GetStringValue(row, "objectkey"),
                RequestTransferType = DataTableHelper.GetEnumValue<TransferTypeEnum>(row, "requesttransfertype", TransferTypeEnum.Normal),
                ResponseTransferType = DataTableHelper.GetEnumValue<TransferTypeEnum>(row, "responsetransfertype", TransferTypeEnum.Normal),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc"),
                CompletedUtc = DataTableHelper.GetNullableDateTimeValue(row, "completedutc")
            };

            return obj;
        }

        /// <summary>
        /// Instantiate list from DataTable.
        /// </summary>
        /// <param name="table">DataTable.</param>
        /// <returns>List of instances, or null if table is null.</returns>
        public static List<RequestHistoryEntry> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<RequestHistoryEntry>();

            List<RequestHistoryEntry> ret = new List<RequestHistoryEntry>();
            foreach (DataRow row in table.Rows)
            {
                RequestHistoryEntry obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }
            return ret;
        }
    }
}
