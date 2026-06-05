namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Conductor.Core.Helpers;

    /// <summary>
    /// Normalized performance event attached to a request history entry.
    /// </summary>
    public class RequestAnalyticsEvent
    {
        /// <summary>
        /// K-sortable unique identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewRequestAnalyticsEventId();

        /// <summary>
        /// Tenant GUID.
        /// </summary>
        public string TenantGuid { get; set; } = null;

        /// <summary>
        /// Parent request history ID.
        /// </summary>
        public string RequestHistoryId { get; set; } = null;

        /// <summary>
        /// Request trace ID.
        /// </summary>
        public string TraceId { get; set; } = null;

        /// <summary>
        /// Virtual Model Runner GUID.
        /// </summary>
        public string VirtualModelRunnerGuid { get; set; } = null;

        /// <summary>
        /// Virtual Model Runner name at capture time.
        /// </summary>
        public string VirtualModelRunnerName { get; set; } = null;

        /// <summary>
        /// Model endpoint GUID.
        /// </summary>
        public string ModelEndpointGuid { get; set; } = null;

        /// <summary>
        /// Model endpoint name at capture time.
        /// </summary>
        public string ModelEndpointName { get; set; } = null;

        /// <summary>
        /// Model endpoint URL at capture time.
        /// </summary>
        public string ModelEndpointUrl { get; set; } = null;

        /// <summary>
        /// Provider family, such as OpenAI, Gemini, Ollama, or vLLM.
        /// </summary>
        public string ProviderName { get; set; } = null;

        /// <summary>
        /// API format observed for the request.
        /// </summary>
        public string ApiFormat { get; set; } = null;

        /// <summary>
        /// Effective upstream model name.
        /// </summary>
        public string ModelName { get; set; } = null;

        /// <summary>
        /// Stage ordering within the request.
        /// </summary>
        public int Sequence { get; set; } = 0;

        /// <summary>
        /// Stable stage kind.
        /// </summary>
        public string StageKind { get; set; } = null;

        /// <summary>
        /// Optional phase within a stage.
        /// </summary>
        public string Phase { get; set; } = null;

        /// <summary>
        /// Human-readable stage name.
        /// </summary>
        public string StageName { get; set; } = null;

        /// <summary>
        /// Stage start timestamp.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Stage completion timestamp.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Stage duration in milliseconds.
        /// </summary>
        public int? DurationMs { get; set; } = null;

        /// <summary>
        /// Whether the stage completed successfully.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// HTTP status associated with this event.
        /// </summary>
        public int? HttpStatus { get; set; } = null;

        /// <summary>
        /// Stable error category.
        /// </summary>
        public string ErrorType { get; set; } = null;

        /// <summary>
        /// Redacted error message.
        /// </summary>
        public string ErrorMessage { get; set; } = null;

        /// <summary>
        /// Endpoint limiter wait in milliseconds.
        /// </summary>
        public int? EndpointLimiterWaitMs { get; set; } = null;

        /// <summary>
        /// Time from upstream dispatch to response headers.
        /// </summary>
        public int? RequestToHeadersMs { get; set; } = null;

        /// <summary>
        /// Time from upstream headers to first token or byte.
        /// </summary>
        public int? HeadersToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Time from first token to final token or byte.
        /// </summary>
        public int? FirstTokenToLastTokenMs { get; set; } = null;

        /// <summary>
        /// Total client-observed duration.
        /// </summary>
        public int? ClientTotalMs { get; set; } = null;

        /// <summary>
        /// Prompt or input token count.
        /// </summary>
        public int? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion or output token count.
        /// </summary>
        public int? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Total token count.
        /// </summary>
        public int? TotalTokens { get; set; } = null;

        /// <summary>
        /// Request bytes associated with this event.
        /// </summary>
        public long? RequestBytes { get; set; } = null;

        /// <summary>
        /// Response bytes associated with this event.
        /// </summary>
        public long? ResponseBytes { get; set; } = null;

        /// <summary>
        /// Token throughput associated with this event.
        /// </summary>
        public decimal? TokensPerSecond { get; set; } = null;

        /// <summary>
        /// Redacted provider metrics JSON.
        /// </summary>
        public string RawProviderMetrics { get; set; } = null;

        /// <summary>
        /// Event creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Instantiate from a DataRow.
        /// </summary>
        /// <param name="row">Data row.</param>
        /// <returns>Request analytics event or null.</returns>
        public static RequestAnalyticsEvent FromDataRow(DataRow row)
        {
            if (row == null) return null;

            return new RequestAnalyticsEvent
            {
                Id = DataTableHelper.GetStringValue(row, "id"),
                TenantGuid = DataTableHelper.GetStringValue(row, "tenantguid"),
                RequestHistoryId = DataTableHelper.GetStringValue(row, "requesthistoryid"),
                TraceId = DataTableHelper.GetStringValue(row, "traceid"),
                VirtualModelRunnerGuid = DataTableHelper.GetStringValue(row, "virtualmodelrunnerguid"),
                VirtualModelRunnerName = DataTableHelper.GetStringValue(row, "virtualmodelrunnername"),
                ModelEndpointGuid = DataTableHelper.GetStringValue(row, "modelendpointguid"),
                ModelEndpointName = DataTableHelper.GetStringValue(row, "modelendpointname"),
                ModelEndpointUrl = DataTableHelper.GetStringValue(row, "modelendpointurl"),
                ProviderName = DataTableHelper.GetStringValue(row, "providername"),
                ApiFormat = DataTableHelper.GetStringValue(row, "apiformat"),
                ModelName = DataTableHelper.GetStringValue(row, "modelname"),
                Sequence = DataTableHelper.GetIntValue(row, "sequence"),
                StageKind = DataTableHelper.GetStringValue(row, "stagekind"),
                Phase = DataTableHelper.GetStringValue(row, "phase"),
                StageName = DataTableHelper.GetStringValue(row, "stagename"),
                StartedUtc = DataTableHelper.GetDateTimeValue(row, "startedutc"),
                CompletedUtc = DataTableHelper.GetNullableDateTimeValue(row, "completedutc"),
                DurationMs = DataTableHelper.GetNullableIntValue(row, "durationms"),
                Success = DataTableHelper.GetBooleanValue(row, "success"),
                HttpStatus = DataTableHelper.GetNullableIntValue(row, "httpstatus"),
                ErrorType = DataTableHelper.GetStringValue(row, "errortype"),
                ErrorMessage = DataTableHelper.GetStringValue(row, "errormessage"),
                EndpointLimiterWaitMs = DataTableHelper.GetNullableIntValue(row, "endpointlimiterwaitms"),
                RequestToHeadersMs = DataTableHelper.GetNullableIntValue(row, "requesttoheadersms"),
                HeadersToFirstTokenMs = DataTableHelper.GetNullableIntValue(row, "headerstofirsttokenms"),
                FirstTokenToLastTokenMs = DataTableHelper.GetNullableIntValue(row, "firsttokentolasttokenms"),
                ClientTotalMs = DataTableHelper.GetNullableIntValue(row, "clienttotalms"),
                PromptTokens = DataTableHelper.GetNullableIntValue(row, "prompttokens"),
                CompletionTokens = DataTableHelper.GetNullableIntValue(row, "completiontokens"),
                TotalTokens = DataTableHelper.GetNullableIntValue(row, "totaltokens"),
                RequestBytes = DataTableHelper.GetNullableLongValue(row, "requestbytes"),
                ResponseBytes = DataTableHelper.GetNullableLongValue(row, "responsebytes"),
                TokensPerSecond = DataTableHelper.GetNullableDecimalValue(row, "tokenspersecond"),
                RawProviderMetrics = DataTableHelper.GetStringValue(row, "rawprovidermetrics"),
                CreatedUtc = DataTableHelper.GetDateTimeValue(row, "createdutc")
            };
        }

        /// <summary>
        /// Instantiate a list from a DataTable.
        /// </summary>
        /// <param name="table">Data table.</param>
        /// <returns>List of request analytics events.</returns>
        public static List<RequestAnalyticsEvent> FromDataTable(DataTable table)
        {
            if (table == null) return null;
            if (table.Rows.Count < 1) return new List<RequestAnalyticsEvent>();

            List<RequestAnalyticsEvent> ret = new List<RequestAnalyticsEvent>();
            foreach (DataRow row in table.Rows)
            {
                RequestAnalyticsEvent obj = FromDataRow(row);
                if (obj != null) ret.Add(obj);
            }

            return ret;
        }
    }
}
