namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Helpers;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Service for capturing and persisting request/response history.
    /// </summary>
    public class RequestHistoryService
    {
        private static readonly string _Header = "[RequestHistoryService] ";

        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly RequestHistorySettings _Settings;
        private readonly Serializer _Serializer;
        private readonly string _Directory;

        /// <summary>
        /// Instantiate the request history service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="settings">Request history settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when database, logging, or settings is null.</exception>
        public RequestHistoryService(DatabaseDriverBase database, LoggingModule logging, RequestHistorySettings settings)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Serializer = new Serializer();
            _Directory = Path.GetFullPath(settings.Directory);

            // Ensure directory exists
            if (!System.IO.Directory.Exists(_Directory))
            {
                System.IO.Directory.CreateDirectory(_Directory);
                _Logging.Info(_Header + "created request history directory: " + _Directory);
            }
        }

        /// <summary>
        /// Check if request history is enabled globally.
        /// </summary>
        public bool IsEnabled => _Settings.Enabled;

        /// <summary>
        /// Maximum request body bytes to capture.
        /// </summary>
        public int MaxRequestBodyBytes => _Settings.MaxRequestBodyBytes;

        /// <summary>
        /// Maximum response body bytes to capture.
        /// </summary>
        public int MaxResponseBodyBytes => _Settings.MaxResponseBodyBytes;

        /// <summary>
        /// Create a new request history entry at the start of a request.
        /// </summary>
        /// <param name="vmr">Virtual model runner handling the request.</param>
        /// <param name="sourceIp">Source IP address of the requestor.</param>
        /// <param name="httpMethod">HTTP method.</param>
        /// <param name="httpUrl">HTTP URL.</param>
        /// <param name="requestHeaders">Request headers.</param>
        /// <param name="requestBody">Request body (will be truncated if too large).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>RequestHistoryDetail with the entry data, or null if creation fails.</returns>
        public async Task<RequestHistoryDetail> CreateEntryAsync(
            VirtualModelRunner vmr,
            string sourceIp,
            string httpMethod,
            string httpUrl,
            Dictionary<string, string> requestHeaders,
            string requestBody,
            CancellationToken token = default)
        {
            if (vmr == null) throw new ArgumentNullException(nameof(vmr));
            if (String.IsNullOrEmpty(sourceIp)) throw new ArgumentNullException(nameof(sourceIp));
            if (String.IsNullOrEmpty(httpMethod)) throw new ArgumentNullException(nameof(httpMethod));
            if (String.IsNullOrEmpty(httpUrl)) throw new ArgumentNullException(nameof(httpUrl));

            try
            {
                string id = IdGenerator.NewRequestHistoryId();
                string objectKey = GenerateObjectKey(id);

                // Truncate request body if needed
                bool requestBodyTruncated = false;
                string truncatedRequestBody = requestBody;
                if (!String.IsNullOrEmpty(requestBody) && requestBody.Length > _Settings.MaxRequestBodyBytes)
                {
                    truncatedRequestBody = requestBody.Substring(0, _Settings.MaxRequestBodyBytes);
                    requestBodyTruncated = true;
                }

                RequestHistoryDetail detail = new RequestHistoryDetail
                {
                    Id = id,
                    TenantGuid = vmr.TenantId,
                    VirtualModelRunnerGuid = vmr.Id,
                    VirtualModelRunnerName = vmr.Name,
                    RequestorSourceIp = sourceIp,
                    HttpMethod = httpMethod,
                    HttpUrl = httpUrl,
                    RequestBodyLength = requestBody?.Length ?? 0,
                    ObjectKey = objectKey,
                    CreatedUtc = DateTime.UtcNow,
                    RequestHeaders = requestHeaders ?? new Dictionary<string, string>(),
                    RequestBody = truncatedRequestBody,
                    RequestBodyTruncated = requestBodyTruncated
                };

                // Create database entry
                RequestHistoryEntry entry = await _Database.RequestHistory.CreateAsync(detail, token).ConfigureAwait(false);

                _Logging.Debug(_Header + "created request history entry " + id + " for VMR " + vmr.Name);

                return detail;
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to create request history entry: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Update a request history entry with response data.
        /// </summary>
        /// <param name="detail">Request history detail to update.</param>
        /// <param name="endpoint">Model endpoint that handled the request (may be null).</param>
        /// <param name="modelDefinition">Model definition used (may be null).</param>
        /// <param name="modelConfiguration">Model configuration used (may be null).</param>
        /// <param name="httpStatus">HTTP response status code.</param>
        /// <param name="responseHeaders">Response headers.</param>
        /// <param name="responseBody">Response body (will be truncated if too large).</param>
        /// <param name="stopwatch">Stopwatch started at request beginning.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task UpdateWithResponseAsync(
            RequestHistoryDetail detail,
            ModelRunnerEndpoint endpoint,
            ModelDefinition modelDefinition,
            ModelConfiguration modelConfiguration,
            int httpStatus,
            Dictionary<string, string> responseHeaders,
            string responseBody,
            Stopwatch stopwatch,
            CancellationToken token = default)
        {
            if (detail == null) return;

            try
            {
                // Update with endpoint/model info
                if (endpoint != null)
                {
                    detail.ModelEndpointGuid = endpoint.Id;
                    detail.ModelEndpointName = endpoint.Name;
                    detail.ModelEndpointUrl = endpoint.GetBaseUrl();
                }

                if (modelDefinition != null)
                {
                    detail.ModelDefinitionGuid = modelDefinition.Id;
                    detail.ModelDefinitionName = modelDefinition.Name;
                }

                if (modelConfiguration != null)
                {
                    detail.ModelConfigurationGuid = modelConfiguration.Id;
                }

                // Update response data
                detail.HttpStatus = httpStatus;
                detail.ResponseBodyLength = responseBody?.Length ?? 0;
                detail.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;
                detail.CompletedUtc = DateTime.UtcNow;
                detail.ResponseHeaders = responseHeaders ?? new Dictionary<string, string>();

                // Truncate response body if needed
                bool responseBodyTruncated = false;
                string truncatedResponseBody = responseBody;
                if (!String.IsNullOrEmpty(responseBody) && responseBody.Length > _Settings.MaxResponseBodyBytes)
                {
                    truncatedResponseBody = responseBody.Substring(0, _Settings.MaxResponseBodyBytes);
                    responseBodyTruncated = true;
                }
                detail.ResponseBody = truncatedResponseBody;
                detail.ResponseBodyTruncated = responseBodyTruncated;

                // Update database entry
                await _Database.RequestHistory.UpdateAsync(detail, token).ConfigureAwait(false);

                // Persist full detail to filesystem
                await PersistDetailAsync(detail, token).ConfigureAwait(false);

                _Logging.Debug(_Header + "updated request history entry " + detail.Id + " status=" + httpStatus + " time=" + detail.ResponseTimeMs + "ms");
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to update request history entry " + detail.Id + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Get a request history entry by ID.
        /// </summary>
        /// <param name="id">Entry ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>RequestHistoryEntry or null if not found.</returns>
        public async Task<RequestHistoryEntry> GetEntryAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) return null;
            return await _Database.RequestHistory.ReadByIdAsync(id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the full request history detail including headers and bodies.
        /// </summary>
        /// <param name="id">Entry ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>RequestHistoryDetail or null if not found.</returns>
        public async Task<RequestHistoryDetail> GetDetailAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) return null;

            RequestHistoryEntry entry = await _Database.RequestHistory.ReadByIdAsync(id, token).ConfigureAwait(false);
            if (entry == null) return null;

            return await LoadDetailAsync(entry, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Search request history entries.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Search result with pagination info.</returns>
        public async Task<RequestHistorySearchResult> SearchAsync(RequestHistorySearchFilter filter, CancellationToken token = default)
        {
            return await _Database.RequestHistory.SearchAsync(filter, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a request history entry and its associated file.
        /// </summary>
        /// <param name="id">Entry ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) return;

            RequestHistoryEntry entry = await _Database.RequestHistory.ReadByIdAsync(id, token).ConfigureAwait(false);
            if (entry == null) return;

            // Delete file
            DeleteFile(entry.ObjectKey);

            // Delete database entry
            await _Database.RequestHistory.DeleteByIdAsync(id, token).ConfigureAwait(false);

            _Logging.Debug(_Header + "deleted request history entry " + id);
        }

        /// <summary>
        /// Delete request history entries matching a filter.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of deleted entries.</returns>
        public async Task<long> DeleteBulkAsync(RequestHistorySearchFilter filter, CancellationToken token = default)
        {
            // Get all entries matching filter to delete their files
            RequestHistorySearchResult result = await _Database.RequestHistory.SearchAsync(
                new RequestHistorySearchFilter
                {
                    TenantGuid = filter.TenantGuid,
                    VirtualModelRunnerGuid = filter.VirtualModelRunnerGuid,
                    ModelEndpointGuid = filter.ModelEndpointGuid,
                    RequestorSourceIp = filter.RequestorSourceIp,
                    HttpStatus = filter.HttpStatus,
                    Page = 1,
                    PageSize = 100
                }, token).ConfigureAwait(false);

            // Delete files for all matching entries
            long totalDeleted = 0;
            while (result.Data.Count > 0)
            {
                foreach (RequestHistoryEntry entry in result.Data)
                {
                    DeleteFile(entry.ObjectKey);
                }
                totalDeleted += result.Data.Count;

                if (result.Data.Count < 100) break;

                result = await _Database.RequestHistory.SearchAsync(
                    new RequestHistorySearchFilter
                    {
                        TenantGuid = filter.TenantGuid,
                        VirtualModelRunnerGuid = filter.VirtualModelRunnerGuid,
                        ModelEndpointGuid = filter.ModelEndpointGuid,
                        RequestorSourceIp = filter.RequestorSourceIp,
                        HttpStatus = filter.HttpStatus,
                        Page = 1,
                        PageSize = 100
                    }, token).ConfigureAwait(false);
            }

            // Delete database entries
            await _Database.RequestHistory.DeleteBulkAsync(filter, token).ConfigureAwait(false);

            _Logging.Info(_Header + "bulk deleted " + totalDeleted + " request history entries");
            return totalDeleted;
        }

        private string GenerateObjectKey(string id)
        {
            // Use a simple flat file structure with the ID as filename
            return id + ".json";
        }

        private string GetFilePath(string objectKey)
        {
            return Path.Combine(_Directory, objectKey);
        }

        private async Task PersistDetailAsync(RequestHistoryDetail detail, CancellationToken token = default)
        {
            try
            {
                string filePath = GetFilePath(detail.ObjectKey);
                string json = _Serializer.SerializeJson(detail, true);
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to persist detail file for " + detail.Id + ": " + ex.Message);
            }
        }

        private async Task<RequestHistoryDetail> LoadDetailAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            try
            {
                string filePath = GetFilePath(entry.ObjectKey);
                if (!File.Exists(filePath))
                {
                    // Return basic detail from entry if file doesn't exist
                    return RequestHistoryDetail.FromEntry(entry);
                }

                string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, token).ConfigureAwait(false);
                return _Serializer.DeserializeJson<RequestHistoryDetail>(json);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to load detail file for " + entry.Id + ": " + ex.Message);
                return RequestHistoryDetail.FromEntry(entry);
            }
        }

        private void DeleteFile(string objectKey)
        {
            try
            {
                string filePath = GetFilePath(objectKey);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to delete file " + objectKey + ": " + ex.Message);
            }
        }
    }
}
