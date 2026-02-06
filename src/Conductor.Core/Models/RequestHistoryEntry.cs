namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data;
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
        /// Response time in milliseconds. May be null if response not yet received.
        /// </summary>
        public int? ResponseTimeMs { get; set; } = null;

        /// <summary>
        /// Filesystem object key for the full request/response data.
        /// </summary>
        public string ObjectKey
        {
            get => _ObjectKey;
            set => _ObjectKey = (String.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(ObjectKey)) : value);
        }

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
                ModelEndpointGuid = DataTableHelper.GetStringValue(row, "modelendpointguid"),
                ModelEndpointName = DataTableHelper.GetStringValue(row, "modelendpointname"),
                ModelEndpointUrl = DataTableHelper.GetStringValue(row, "modelendpointurl"),
                ModelDefinitionGuid = DataTableHelper.GetStringValue(row, "modeldefinitionguid"),
                ModelDefinitionName = DataTableHelper.GetStringValue(row, "modeldefinitionname"),
                ModelConfigurationGuid = DataTableHelper.GetStringValue(row, "modelconfigurationguid"),
                RequestorSourceIp = DataTableHelper.GetStringValue(row, "requestorsourceip"),
                HttpMethod = DataTableHelper.GetStringValue(row, "httpmethod"),
                HttpUrl = DataTableHelper.GetStringValue(row, "httpurl"),
                RequestBodyLength = DataTableHelper.GetLongValue(row, "requestbodylength"),
                ResponseBodyLength = DataTableHelper.GetNullableLongValue(row, "responsebodylength"),
                HttpStatus = DataTableHelper.GetNullableIntValue(row, "httpstatus"),
                ResponseTimeMs = DataTableHelper.GetNullableIntValue(row, "responsetimems"),
                ObjectKey = DataTableHelper.GetStringValue(row, "objectkey"),
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
