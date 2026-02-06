namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
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
            _Logging.Info(_Header + "starting (interval: " + _Settings.CleanupIntervalMinutes + " minutes, retention: " + _Settings.RetentionDays + " days)");

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
                DateTime cutoff = DateTime.UtcNow.AddDays(-_Settings.RetentionDays);
                _Logging.Debug(_Header + "cleaning up entries older than " + cutoff.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");

                // Get object keys for files to delete
                List<string> objectKeys = await _Database.RequestHistory.GetExpiredObjectKeysAsync(cutoff, token).ConfigureAwait(false);

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
                long entriesDeleted = await _Database.RequestHistory.DeleteExpiredAsync(cutoff, token).ConfigureAwait(false);

                if (entriesDeleted > 0 || filesDeleted > 0)
                {
                    _Logging.Info(_Header + "cleaned up " + entriesDeleted + " entries, " + filesDeleted + " files");
                }
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "cleanup error: " + ex.Message);
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
