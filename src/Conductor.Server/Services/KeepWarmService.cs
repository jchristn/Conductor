namespace Conductor.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Responses;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Background service that keeps models warm for virtual model runners with <see cref="KeepWarmSettings.Enabled"/> set.
    /// On each tick it finds runners whose interval has elapsed and, for each of their models, checks residency on every
    /// eligible Ollama endpoint and loads the model where it is not resident. Each load takes one of the endpoint's
    /// MaxParallelRequests slots. Passes run one at a time across the server so warm-up loads never stack up on a GPU.
    /// Public methods are thread-safe.
    /// </summary>
    public class KeepWarmService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// How often, in milliseconds, the service looks for runners that are due. Minimum: 1000. Default: 15000.
        /// Values below the minimum are clamped. Takes effect after the current tick.
        /// </summary>
        public int TickIntervalMs
        {
            get => _TickIntervalMs;
            set => _TickIntervalMs = (value < 1000 ? 1000 : value);
        }

        #endregion

        #region Private-Members

        private static readonly string _Header = "[KeepWarmService] ";
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly ModelLoadService _ModelLoadService;
        private readonly ConcurrentDictionary<string, KeepWarmStatus> _Statuses = new ConcurrentDictionary<string, KeepWarmStatus>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, DateTime> _NextRunUtc = new ConcurrentDictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, bool> _Running = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        private readonly SemaphoreSlim _PassLock = new SemaphoreSlim(1, 1);
        private int _TickIntervalMs = 15000;
        private CancellationTokenSource _TokenSource;
        private Task _LoopTask;
        private bool _Disposed;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the keep-warm service.
        /// </summary>
        /// <param name="database">Database driver. Must not be null.</param>
        /// <param name="logging">Logging module. Must not be null.</param>
        /// <param name="modelLoadService">Model load service used to check residency and load models. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public KeepWarmService(DatabaseDriverBase database, LoggingModule logging, ModelLoadService modelLoadService)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _ModelLoadService = modelLoadService ?? throw new ArgumentNullException(nameof(modelLoadService));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the background loop. Returns immediately; the first check runs inside the loop.
        /// </summary>
        /// <param name="token">Cancellation token that stops the loop.</param>
        /// <returns>Completed task.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the service is disposed.</exception>
        public Task StartAsync(CancellationToken token = default)
        {
            ObjectDisposedException.ThrowIf(_Disposed, this);
            if (_LoopTask != null) return Task.CompletedTask;

            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _LoopTask = Task.Run(() => LoopAsync(_TokenSource.Token), _TokenSource.Token);
            _Logging.Info(_Header + "started (tick " + _TickIntervalMs + " ms)");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the background loop and wait for the current pass to end.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StopAsync()
        {
            if (_TokenSource == null) return;

            _TokenSource.Cancel();
            if (_LoopTask != null)
            {
                try
                {
                    await _LoopTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            _Logging.Info(_Header + "stopped");
        }

        /// <summary>
        /// Run one scheduling check: start a pass for every active virtual model runner whose keep-warm interval has
        /// elapsed. Runners are processed one at a time.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of passes run.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="token"/> is cancelled.</exception>
        public async Task<int> RunDueAsync(CancellationToken token = default)
        {
            List<VirtualModelRunner> vmrs = await _Database.VirtualModelRunner.GetAllActiveAsync(token).ConfigureAwait(false) ?? new List<VirtualModelRunner>();
            HashSet<string> enabledIds = new HashSet<string>(
                vmrs.Where(item => item?.KeepWarm != null && item.KeepWarm.Enabled).Select(item => item.Id),
                StringComparer.Ordinal);

            foreach (string id in _NextRunUtc.Keys.Where(id => !enabledIds.Contains(id)).ToList())
            {
                _NextRunUtc.TryRemove(id, out _);
                _Statuses.TryRemove(id, out _);
            }

            int passes = 0;
            foreach (VirtualModelRunner vmr in vmrs.Where(item => item != null && enabledIds.Contains(item.Id)))
            {
                token.ThrowIfCancellationRequested();

                DateTime now = DateTime.UtcNow;
                if (_NextRunUtc.TryGetValue(vmr.Id, out DateTime nextRun) && nextRun > now) continue;

                await RunPassAsync(vmr, token).ConfigureAwait(false);
                passes++;
            }

            return passes;
        }

        /// <summary>
        /// Run a keep-warm pass for one virtual model runner now, regardless of its schedule or Enabled flag, and
        /// reschedule its next pass one interval from now. Waits for any pass already running.
        /// </summary>
        /// <param name="vmr">Virtual model runner. Must not be null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status after the pass. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="vmr"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="token"/> is cancelled.</exception>
        public async Task<KeepWarmStatus> RunNowAsync(VirtualModelRunner vmr, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(vmr);
            await RunPassAsync(vmr, token).ConfigureAwait(false);
            return GetStatus(vmr);
        }

        /// <summary>
        /// Get a snapshot of keep-warm state for a virtual model runner.
        /// </summary>
        /// <param name="vmr">Virtual model runner. Must not be null.</param>
        /// <returns>Status snapshot. Never null; before the first pass it carries only the configuration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="vmr"/> is null.</exception>
        public KeepWarmStatus GetStatus(VirtualModelRunner vmr)
        {
            ArgumentNullException.ThrowIfNull(vmr);

            KeepWarmSettings settings = vmr.KeepWarm ?? new KeepWarmSettings();
            KeepWarmStatus snapshot = new KeepWarmStatus
            {
                VirtualModelRunnerId = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                Enabled = settings.Enabled && vmr.Active,
                IntervalMs = settings.IntervalMs,
                Running = _Running.ContainsKey(vmr.Id)
            };

            if (_Statuses.TryGetValue(vmr.Id, out KeepWarmStatus last))
            {
                snapshot.Models = new List<string>(last.Models);
                snapshot.LastRunStartedUtc = last.LastRunStartedUtc;
                snapshot.LastRunCompletedUtc = last.LastRunCompletedUtc;
                snapshot.LastRunSucceeded = last.LastRunSucceeded;
                snapshot.LastError = last.LastError;
                snapshot.LastResults = new List<ModelLoadResponse>(last.LastResults);
            }

            if (snapshot.Enabled)
            {
                snapshot.NextRunUtc = _NextRunUtc.TryGetValue(vmr.Id, out DateTime next) ? next : DateTime.UtcNow;
            }

            return snapshot;
        }

        /// <summary>
        /// Dispose the service, stopping the background loop.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">True when disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                try { _TokenSource?.Cancel(); } catch (ObjectDisposedException) { }
                _TokenSource?.Dispose();
                _PassLock.Dispose();
            }

            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await RunDueAsync(token).ConfigureAwait(false);
                    await Task.Delay(_TickIntervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "loop error: " + ex.Message);
                    try
                    {
                        await Task.Delay(_TickIntervalMs, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task RunPassAsync(VirtualModelRunner vmr, CancellationToken token)
        {
            await _PassLock.WaitAsync(token).ConfigureAwait(false);
            _Running[vmr.Id] = true;

            KeepWarmSettings settings = vmr.KeepWarm ?? new KeepWarmSettings();
            KeepWarmStatus status = new KeepWarmStatus
            {
                VirtualModelRunnerId = vmr.Id,
                VirtualModelRunnerName = vmr.Name,
                Enabled = settings.Enabled && vmr.Active,
                IntervalMs = settings.IntervalMs,
                LastRunStartedUtc = DateTime.UtcNow
            };

            List<string> errors = new List<string>();

            try
            {
                status.Models = await ResolveModelsAsync(vmr, settings, token).ConfigureAwait(false);
                if (status.Models.Count < 1)
                {
                    errors.Add("No models to keep warm: set KeepWarm.Models or attach an active model definition.");
                }

                foreach (string model in status.Models)
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        ModelLoadResponse response = await _ModelLoadService.KeepWarmVirtualModelRunnerAsync(vmr, model, settings, token).ConfigureAwait(false);
                        status.LastResults.Add(response);

                        foreach (ModelLoadEndpointResult result in response.EndpointResults.Where(item => !item.Success && item.OutcomeCode != ModelLoadOutcomeEnum.Skipped))
                        {
                            errors.Add(model + " on " + (result.EndpointName ?? result.EndpointId) + ": " + result.OutcomeCode
                                + (String.IsNullOrWhiteSpace(result.ErrorMessage) ? "" : " (" + result.ErrorMessage + ")"));
                        }

                        LogResponse(vmr, model, response);
                    }
                    catch (WebserverException ex)
                    {
                        errors.Add(model + ": " + ex.Message);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _Logging.Warn(_Header + "vmr " + vmr.Name + " model " + model + " failed: " + ex.Message);
                        errors.Add(model + ": " + ex.Message);
                    }
                }
            }
            finally
            {
                status.LastRunCompletedUtc = DateTime.UtcNow;
                status.LastError = errors.Count > 0 ? String.Join("; ", errors) : null;
                status.LastRunSucceeded = errors.Count < 1;
                _Statuses[vmr.Id] = status;
                _NextRunUtc[vmr.Id] = DateTime.UtcNow.AddMilliseconds(settings.IntervalMs);
                _Running.TryRemove(vmr.Id, out _);
                _PassLock.Release();
            }
        }

        private async Task<List<string>> ResolveModelsAsync(VirtualModelRunner vmr, KeepWarmSettings settings, CancellationToken token)
        {
            if (settings.Models != null && settings.Models.Count > 0)
            {
                return new List<string>(settings.Models);
            }

            List<string> models = new List<string>();
            foreach (string definitionId in vmr.ModelDefinitionIds ?? new List<string>())
            {
                ModelDefinition definition = await _Database.ModelDefinition.ReadAsync(vmr.TenantId, definitionId, token).ConfigureAwait(false);
                if (definition != null && definition.Active && !String.IsNullOrWhiteSpace(definition.Name)
                    && !models.Contains(definition.Name, StringComparer.OrdinalIgnoreCase))
                {
                    models.Add(definition.Name);
                }
            }

            return models;
        }

        private void LogResponse(VirtualModelRunner vmr, string model, ModelLoadResponse response)
        {
            foreach (ModelLoadEndpointResult result in response.EndpointResults)
            {
                string line = _Header + "vmr " + vmr.Name + " model " + model + " endpoint " + (result.EndpointName ?? result.EndpointId)
                    + ": " + result.OutcomeCode + " (" + result.DurationMs + " ms)"
                    + (String.IsNullOrWhiteSpace(result.ErrorMessage) ? "" : " " + result.ErrorMessage);

                if (result.OutcomeCode == ModelLoadOutcomeEnum.Loaded) _Logging.Info(line);
                else if (result.Success || result.OutcomeCode == ModelLoadOutcomeEnum.Skipped) _Logging.Debug(line);
                else _Logging.Warn(line);
            }
        }

        #endregion
    }
}
