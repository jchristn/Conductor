namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Background service for cleaning up expired request history entries.
    /// </summary>
    public class RequestHistoryCleanupService : IDisposable
    {
        private static readonly string _Header = "[RequestHistoryCleanupService] ";

        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly RequestHistorySettings _Settings;
        private readonly string _Directory;
        private readonly Serializer _Serializer = new Serializer();
        private CancellationTokenSource _CancellationTokenSource;
        private Task _CleanupTask;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the request history cleanup service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="settings">Request history settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when database, logging, or settings is null.</exception>
        public RequestHistoryCleanupService(DatabaseDriverBase database, LoggingModule logging, RequestHistorySettings settings)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Directory = Path.GetFullPath(settings.Directory);
        }

        /// <summary>
        /// Start the cleanup service.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task StartAsync(CancellationToken token = default)
        {
            if (!_Settings.Enabled)
            {
                _Logging.Info(_Header + "request history is disabled, cleanup service not started");
                return;
            }

            _CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            int metadataRetentionDays = _Settings.MetadataRetentionDays > 0 ? _Settings.MetadataRetentionDays : _Settings.RetentionDays;
            int bodyRetentionDays = _Settings.BodyRetentionDays > 0 ? _Settings.BodyRetentionDays : metadataRetentionDays;
            _Logging.Info(_Header
                + "starting (interval: " + _Settings.CleanupIntervalMinutes
                + " minutes, metadata retention: " + metadataRetentionDays
                + " days, body retention: " + bodyRetentionDays + " days)");

            // Run cleanup immediately on startup
            await CleanupExpiredAsync(_CancellationTokenSource.Token).ConfigureAwait(false);

            // Start background task
            _CleanupTask = Task.Run(async () => await CleanupLoop(_CancellationTokenSource.Token).ConfigureAwait(false), _CancellationTokenSource.Token);
        }

        /// <summary>
        /// Stop the cleanup service.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StopAsync()
        {
            _Logging.Info(_Header + "stopping");

            if (_CancellationTokenSource != null)
            {
                _CancellationTokenSource.Cancel();

                if (_CleanupTask != null)
                {
                    try
                    {
                        await _CleanupTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                }
            }

            _Logging.Info(_Header + "stopped");
        }

        private async Task CleanupLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Wait for the interval
                    await Task.Delay(TimeSpan.FromMinutes(_Settings.CleanupIntervalMinutes), token).ConfigureAwait(false);

                    if (token.IsCancellationRequested) break;

                    // Perform cleanup
                    await CleanupExpiredAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "cleanup loop error: " + ex.Message);
                }
            }
        }

        private async Task CleanupExpiredAsync(CancellationToken token)
        {
            try
            {
                int metadataRetentionDays = _Settings.MetadataRetentionDays > 0 ? _Settings.MetadataRetentionDays : _Settings.RetentionDays;
                int bodyRetentionDays = _Settings.BodyRetentionDays > 0 ? _Settings.BodyRetentionDays : metadataRetentionDays;
                DateTime metadataCutoff = DateTime.UtcNow.AddDays(-metadataRetentionDays);
                DateTime bodyCutoff = DateTime.UtcNow.AddDays(-bodyRetentionDays);

                _Logging.Debug(_Header
                    + "cleaning up request history (metadata cutoff: "
                    + metadataCutoff.ToString("yyyy-MM-dd HH:mm:ss")
                    + " UTC, body cutoff: "
                    + bodyCutoff.ToString("yyyy-MM-dd HH:mm:ss")
                    + " UTC)");

                int bodiesScrubbed = await ScrubExpiredBodiesAsync(bodyCutoff, metadataCutoff, token).ConfigureAwait(false);

                // Get object keys for files to delete
                List<string> objectKeys = await _Database.RequestHistory.GetExpiredObjectKeysAsync(metadataCutoff, token).ConfigureAwait(false);

                // Delete files
                int filesDeleted = 0;
                foreach (string objectKey in objectKeys)
                {
                    if (token.IsCancellationRequested) break;

                    try
                    {
                        string filePath = Path.Combine(_Directory, objectKey);
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            filesDeleted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logging.Warn(_Header + "failed to delete file " + objectKey + ": " + ex.Message);
                    }
                }

                // Delete database entries
                long analyticsEventsDeleted = 0;
                if (_Database.RequestAnalytics != null)
                {
                    analyticsEventsDeleted = await _Database.RequestAnalytics.DeleteExpiredAsync(metadataCutoff, token).ConfigureAwait(false);
                }

                long entriesDeleted = await _Database.RequestHistory.DeleteExpiredAsync(metadataCutoff, token).ConfigureAwait(false);

                if (entriesDeleted > 0 || analyticsEventsDeleted > 0 || filesDeleted > 0 || bodiesScrubbed > 0)
                {
                    _Logging.Info(_Header + "cleaned up " + entriesDeleted + " entries, " + analyticsEventsDeleted + " analytics events, " + filesDeleted + " files, scrubbed " + bodiesScrubbed + " retained body snapshots");
                }
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "cleanup error: " + ex.Message);
            }
        }

        private async Task<int> ScrubExpiredBodiesAsync(DateTime bodyCutoff, DateTime metadataCutoff, CancellationToken token)
        {
            if (bodyCutoff <= metadataCutoff)
            {
                return 0;
            }

            int scrubbed = 0;
            RequestHistorySearchFilter filter = new RequestHistorySearchFilter
            {
                CreatedAfterUtc = metadataCutoff,
                CreatedBeforeUtc = bodyCutoff,
                Page = 1,
                PageSize = 100
            };

            while (!token.IsCancellationRequested)
            {
                RequestHistorySearchResult result = await _Database.RequestHistory.SearchAsync(filter, token).ConfigureAwait(false);
                if (result == null || result.Data == null || result.Data.Count < 1)
                {
                    break;
                }

                foreach (RequestHistoryEntry entry in result.Data)
                {
                    token.ThrowIfCancellationRequested();

                    if (!entry.RequestBodyRetained && !entry.ResponseBodyRetained)
                    {
                        continue;
                    }

                    if (await ScrubDetailBodiesAsync(entry, token).ConfigureAwait(false))
                    {
                        scrubbed++;
                    }
                }

                if (result.Data.Count < filter.PageSize)
                {
                    break;
                }

                filter.Page++;
            }

            return scrubbed;
        }

        private async Task<bool> ScrubDetailBodiesAsync(RequestHistoryEntry entry, CancellationToken token)
        {
            if (entry == null)
            {
                return false;
            }

            RequestHistoryDetail detail = await LoadDetailAsync(entry, token).ConfigureAwait(false);
            if (detail == null)
            {
                return false;
            }

            bool changed = detail.RequestBodyRetained
                || detail.ResponseBodyRetained
                || !String.IsNullOrEmpty(detail.RequestBody)
                || !String.IsNullOrEmpty(detail.ResponseBody);

            if (!changed)
            {
                return false;
            }

            detail.RequestBody = null;
            detail.ResponseBody = null;
            detail.RequestBodyRetained = false;
            detail.ResponseBodyRetained = false;
            detail.RequestBodyTruncated = false;
            detail.ResponseBodyTruncated = false;

            await _Database.RequestHistory.UpdateAsync(detail, token).ConfigureAwait(false);
            await PersistDetailAsync(detail, token).ConfigureAwait(false);
            return true;
        }

        private async Task<RequestHistoryDetail> LoadDetailAsync(RequestHistoryEntry entry, CancellationToken token)
        {
            string filePath = Path.Combine(_Directory, entry.ObjectKey);
            if (!File.Exists(filePath))
            {
                return RequestHistoryDetail.FromEntry(entry);
            }

            try
            {
                string json = await File.ReadAllTextAsync(filePath, token).ConfigureAwait(false);
                return _Serializer.DeserializeJson<RequestHistoryDetail>(json) ?? RequestHistoryDetail.FromEntry(entry);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to read detail file " + entry.ObjectKey + " for body scrubbing: " + ex.Message);
                return RequestHistoryDetail.FromEntry(entry);
            }
        }

        private async Task PersistDetailAsync(RequestHistoryDetail detail, CancellationToken token)
        {
            if (detail == null || String.IsNullOrWhiteSpace(detail.ObjectKey))
            {
                return;
            }

            try
            {
                string filePath = Path.Combine(_Directory, detail.ObjectKey);
                string json = _Serializer.SerializeJson(detail, true);
                await File.WriteAllTextAsync(filePath, json, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to persist scrubbed detail file " + detail.ObjectKey + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Disposes of managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                if (_CancellationTokenSource != null)
                {
                    _CancellationTokenSource.Cancel();
                    _CancellationTokenSource.Dispose();
                    _CancellationTokenSource = null;
                }
            }

            _Disposed = true;
        }
    }
}
