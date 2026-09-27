import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useApp } from '../context/AppContext';
import ErrorBanner from '../components/ErrorBanner';
import RefreshButton from '../components/RefreshButton';
import QosAdmissionsChart from '../components/QosAdmissionsChart';
import QosWaitDepthChart, { formatWaitMs, qosClassColor } from '../components/QosWaitDepthChart';
import { parseUtcMs } from '../utils/chartHelpers';
import {
  DEFAULT_QOS_TIME_RANGE,
  QOS_TIME_RANGES,
  floorToStep,
  getQosTimeRange,
  getRangeWindow
} from '../utils/qosMonitorTimeRanges';

const AUTO_REFRESH_MS = 5000;
const SELECTED_RUNNER_STORAGE_KEY = 'conductor_qos_monitor_vmr';
const TIME_RANGE_STORAGE_KEY = 'conductor_qos_monitor_range';

function readStorage(key) {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStorage(key, value) {
  try {
    if (value) window.localStorage.setItem(key, value);
    else window.localStorage.removeItem(key);
  } catch {
    /* storage unavailable (private mode, blocked site data) - selection just isn't remembered */
  }
}

function formatNumber(value) {
  return (value || 0).toLocaleString();
}

function formatUtcDate(value) {
  const ms = parseUtcMs(value);
  if (Number.isNaN(ms)) return '-';
  return new Date(ms).toLocaleString();
}

function sumClasses(snapshot, key) {
  return (snapshot?.Classes || []).reduce((sum, cls) => sum + (cls[key] || 0), 0);
}

function maxClasses(snapshot, key) {
  return (snapshot?.Classes || []).reduce((max, cls) => Math.max(max, cls[key] || 0), 0);
}

function failedCount(item) {
  return (item.Rejected || 0) + (item.TimedOut || 0) + (item.Aborted || 0);
}

function formatCapacity(snapshot) {
  if (!snapshot) return '-';
  return snapshot.Capacity > 0 ? `${formatNumber(snapshot.InUse)} / ${formatNumber(snapshot.Capacity)}` : `${formatNumber(snapshot.InUse)} / unbounded`;
}

function SchedulerStateBadge({ snapshot }) {
  const state = snapshot?.SchedulerState || 'Idle';
  const faults = snapshot?.SchedulerFaultCount || 0;
  const lastError = snapshot?.LastSchedulerError
    ? `Last scheduler error${snapshot.LastSchedulerErrorUtc ? ` (${formatUtcDate(snapshot.LastSchedulerErrorUtc)})` : ''}: ${snapshot.LastSchedulerError}`
    : '';

  let tone = 'neutral';
  let label = state;
  let title = '';
  if (state === 'Running') {
    tone = faults > 0 ? 'warning' : 'success';
    label = faults > 0 ? `Running (${faults} ${faults === 1 ? 'fault' : 'faults'})` : 'Running';
    title = faults > 0
      ? `The scheduler is running and has recovered from ${faults} ${faults === 1 ? 'fault' : 'faults'} since the server started. ${lastError}`
      : 'The QoS scheduler is running and queueing requests for this runner.';
  } else if (state === 'Recovering') {
    tone = 'danger';
    label = 'Recovering';
    title = `The scheduler faulted and is restarting. ${lastError}`;
  } else if (state === 'PassThrough') {
    tone = 'warning';
    label = 'Pass-through';
    title = 'No active QoS profile, or the profile failed to compile, so requests are admitted without queueing.';
  } else if (state === 'Idle') {
    title = 'No request has been admitted since the server started, so no QoS runtime has been built yet.';
  }

  return (
    <span className={`service-state-badge ${tone}`} title={title.trim()}>
      {(tone === 'danger' || tone === 'warning') && (
        <svg width="12" height="12" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
          <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
        </svg>
      )}
      {label}
    </span>
  );
}

function MetricCard({ label, value, sublabel, tooltip, tone }) {
  return (
    <div className={`analytics-metric-card${tone ? ` qos-monitor-card-${tone}` : ''}`} title={tooltip}>
      <span className="analytics-metric-label">{label}</span>
      <strong>{value}</strong>
      {sublabel && <span className="analytics-metric-sub">{sublabel}</span>}
    </div>
  );
}

// Fill the bucket grid for the selected range from the sparse history (only buckets with activity
// are returned). Bucket keys are aligned to the interval in UTC on both sides.
function buildBuckets(history, range, startMs) {
  const byTimestamp = new Map();
  (history?.Buckets || []).forEach((bucket) => {
    const ts = floorToStep(parseUtcMs(bucket.TimestampUtc), range.stepMs);
    if (Number.isNaN(ts)) return;
    if (!byTimestamp.has(ts)) byTimestamp.set(ts, []);
    byTimestamp.get(ts).push(bucket);
  });

  return Array.from({ length: range.bucketCount }, (_, index) => {
    const timestampMs = startMs + index * range.stepMs;
    const row = { timestampMs, admitted: 0, rejected: 0, timedOut: 0, aborted: 0, endpointSlotTimeouts: 0, classes: {} };
    (byTimestamp.get(timestampMs) || []).forEach((bucket) => {
      row.admitted += bucket.Admitted || 0;
      row.rejected += bucket.Rejected || 0;
      row.timedOut += bucket.TimedOut || 0;
      row.aborted += bucket.Aborted || 0;
      row.endpointSlotTimeouts += bucket.EndpointSlotTimeouts || 0;
      const name = bucket.ClassName || 'default';
      row.classes[name] = {
        admitted: bucket.Admitted || 0,
        averageWaitMs: bucket.AverageWaitMs || 0,
        maxWaitMs: bucket.MaxWaitMs || 0,
        peakWaiting: bucket.PeakWaiting || 0
      };
    });
    return row;
  });
}

function QosMonitor() {
  const { api } = useApp();
  const [runners, setRunners] = useState(null);
  const [history, setHistory] = useState(null);
  const [historyError, setHistoryError] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);
  const [lastUpdatedMs, setLastUpdatedMs] = useState(null);
  const [selectedId, setSelectedId] = useState(() => readStorage(SELECTED_RUNNER_STORAGE_KEY));
  const [timeRange, setTimeRange] = useState(() => {
    const stored = readStorage(TIME_RANGE_STORAGE_KEY);
    return QOS_TIME_RANGES.some((entry) => entry.value === stored) ? stored : DEFAULT_QOS_TIME_RANGE;
  });

  const range = getQosTimeRange(timeRange);

  // Refs so the polling loop always reads the latest selection without being recreated.
  const selectedIdRef = useRef(selectedId);
  const timeRangeRef = useRef(timeRange);
  const inFlightRef = useRef(false);
  const pendingRef = useRef(false);
  const mountedRef = useRef(true);
  const dismissedErrorRef = useRef(null);

  selectedIdRef.current = selectedId;
  timeRangeRef.current = timeRange;

  const fetchAll = useCallback(async () => {
    // Never overlap requests: if one is running, remember to run again when it finishes.
    if (inFlightRef.current) {
      pendingRef.current = true;
      return;
    }
    inFlightRef.current = true;
    setLoading(true);
    try {
      do {
        pendingRef.current = false;
        const requestedRange = getQosTimeRange(timeRangeRef.current);
        let list;
        try {
          list = await api.listQosRuntime();
        } catch (e) {
          if (!mountedRef.current) return;
          const message = e?.message || 'Failed to load QoS runtime state';
          if (dismissedErrorRef.current !== message) setError(message);
          continue;
        }
        if (!mountedRef.current) return;
        const snapshots = Array.isArray(list) ? list : [];
        setRunners(snapshots);
        setError(null);
        dismissedErrorRef.current = null;
        setLastUpdatedMs(Date.now());

        let targetId = selectedIdRef.current;
        if (!snapshots.some((item) => item.VirtualModelRunnerId === targetId)) {
          // Default to the first runner with QoS activity, falling back to the first runner.
          const active = snapshots.find((item) => item.SchedulerState && item.SchedulerState !== 'Idle');
          targetId = (active || snapshots[0])?.VirtualModelRunnerId || null;
          if (targetId !== selectedIdRef.current) {
            selectedIdRef.current = targetId;
            setSelectedId(targetId);
          }
        }

        if (!targetId) {
          setHistory(null);
          continue;
        }

        const historyWindow = getRangeWindow(requestedRange, Date.now());
        try {
          const result = await api.getQosRuntimeHistory(targetId, {
            startUtc: new Date(historyWindow.startMs).toISOString(),
            endUtc: new Date(historyWindow.endExclusiveMs).toISOString(),
            interval: requestedRange.interval
          });
          if (!mountedRef.current) return;
          // Drop the result if the selection changed while it was loading; the pending rerun fetches the new one.
          if (targetId === selectedIdRef.current && requestedRange.value === timeRangeRef.current) {
            setHistory({ vmrId: targetId, range: requestedRange.value, data: result });
            setHistoryError(null);
          }
        } catch (e) {
          if (!mountedRef.current) return;
          if (targetId === selectedIdRef.current) {
            setHistoryError(e?.message || 'Failed to load QoS history');
          }
        }
      } while (pendingRef.current && mountedRef.current);
    } finally {
      inFlightRef.current = false;
      if (mountedRef.current) setLoading(false);
    }
  }, [api]);

  // Initial load, selection/range changes, and auto-refresh while the tab is visible.
  useEffect(() => {
    fetchAll();
  }, [fetchAll, selectedId, timeRange]);

  useEffect(() => {
    mountedRef.current = true;
    const timer = setInterval(() => {
      if (!document.hidden) fetchAll();
    }, AUTO_REFRESH_MS);
    const handleVisibility = () => {
      if (!document.hidden) fetchAll();
    };
    document.addEventListener('visibilitychange', handleVisibility);
    return () => {
      mountedRef.current = false;
      clearInterval(timer);
      document.removeEventListener('visibilitychange', handleVisibility);
    };
  }, [fetchAll]);

  const handleSelect = (id) => {
    if (!id || id === selectedIdRef.current) return;
    writeStorage(SELECTED_RUNNER_STORAGE_KEY, id);
    setHistory(null);
    setHistoryError(null);
    setSelectedId(id);
  };

  const handleTimeRangeChange = (value) => {
    if (value === timeRange) return;
    writeStorage(TIME_RANGE_STORAGE_KEY, value);
    setTimeRange(value);
  };

  const handleDismissError = () => {
    dismissedErrorRef.current = error;
    setError(null);
  };

  const snapshots = runners || [];
  const selected = snapshots.find((item) => item.VirtualModelRunnerId === selectedId) || null;
  const showTenant = new Set(snapshots.map((item) => item.TenantId)).size > 1;
  const currentHistory = history && history.vmrId === selectedId && history.range === timeRange ? history.data : null;

  const rangeWindow = getRangeWindow(range, lastUpdatedMs || Date.now());
  const buckets = useMemo(
    () => buildBuckets(currentHistory, range, rangeWindow.startMs),
    [currentHistory, range, rangeWindow.startMs]
  );

  // Stable, sorted class list (live snapshot classes plus any class seen in history) so each class
  // keeps its color regardless of which classes are hidden or active in the window.
  const classNames = useMemo(() => {
    const names = new Set();
    (selected?.Classes || []).forEach((cls) => names.add(cls.ClassName));
    (currentHistory?.Classes || []).forEach((name) => names.add(name));
    return Array.from(names).filter(Boolean).sort((a, b) => a.localeCompare(b));
  }, [selected, currentHistory]);

  const rangeTotals = buckets.reduce((acc, row) => {
    acc.admitted += row.admitted;
    acc.failed += row.rejected + row.timedOut + row.aborted;
    acc.slotTimeouts += row.endpointSlotTimeouts;
    return acc;
  }, { admitted: 0, failed: 0, slotTimeouts: 0 });

  const p95Class = (selected?.Classes || []).reduce(
    (best, cls) => (!best || (cls.P95WaitMs || 0) > (best.P95WaitMs || 0) ? cls : best),
    null
  );

  const renderOverview = () => (
    <section className="dashboard-section" aria-labelledby="qos-monitor-overview-title">
      <div className="request-history-chart-header">
        <h2 id="qos-monitor-overview-title">All Runners</h2>
        <span className="text-muted qos-monitor-updated">
          {lastUpdatedMs ? `Updated ${new Date(lastUpdatedMs).toLocaleTimeString()} · refreshes every ${AUTO_REFRESH_MS / 1000}s` : ''}
        </span>
      </div>
      <div className="detail-table-container">
        <table className="detail-table qos-monitor-table">
          <thead>
            <tr>
              <th title="Virtual model runner">Runner</th>
              {showTenant && <th title="Tenant that owns the runner">Tenant</th>}
              <th title="QoS profile linked to the runner">Profile</th>
              <th title="QoS scheduler state. Hover a badge for details and the last scheduler error.">Scheduler</th>
              <th title="Admission permits in use / capacity (the sum of the endpoints' MaxParallelRequests)">In Use / Capacity</th>
              <th title="Requests waiting in the QoS queue now">Waiting</th>
              <th title="Requests admitted since the server started">Admitted</th>
              <th title="Requests rejected, timed out in the queue, or aborted by the client since the server started">Rejected / Timed Out / Aborted</th>
              <th title="Highest per-class 95th percentile queue wait since the server started">P95 Wait</th>
            </tr>
          </thead>
          <tbody>
            {snapshots.map((item) => {
              const isSelected = item.VirtualModelRunnerId === selectedId;
              const failed = (item.Classes || []).reduce((sum, cls) => sum + failedCount(cls), 0);
              return (
                <tr
                  key={item.VirtualModelRunnerId}
                  className={'qos-monitor-row' + (isSelected ? ' selected' : '')}
                  onClick={() => handleSelect(item.VirtualModelRunnerId)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                      event.preventDefault();
                      handleSelect(item.VirtualModelRunnerId);
                    }
                  }}
                  tabIndex={0}
                  aria-current={isSelected ? 'true' : undefined}
                  title={`Show QoS details for ${item.VirtualModelRunnerName || item.VirtualModelRunnerId}`}
                >
                  <td className="detail-table-title-cell">{item.VirtualModelRunnerName || item.VirtualModelRunnerId}</td>
                  {showTenant && <td>{item.TenantId || '-'}</td>}
                  <td>{item.QosProfileName || <span className="text-muted">None</span>}</td>
                  <td><SchedulerStateBadge snapshot={item} /></td>
                  <td>{formatCapacity(item)}</td>
                  <td className={item.Waiting > 0 ? 'qos-monitor-emphasis' : ''}>{formatNumber(item.Waiting)}</td>
                  <td>{formatNumber(sumClasses(item, 'Admitted'))}</td>
                  <td className={failed > 0 ? 'qos-monitor-failed' : ''}>{formatNumber(failed)}</td>
                  <td>{(item.Classes || []).length > 0 ? formatWaitMs(maxClasses(item, 'P95WaitMs')) : '-'}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );

  const renderClassTable = () => (
    <section className="dashboard-section" aria-labelledby="qos-monitor-classes-title">
      <div className="request-history-chart-header">
        <h2 id="qos-monitor-classes-title">Traffic Classes</h2>
        <span className="text-muted qos-monitor-updated">Since server start</span>
      </div>
      <div className="detail-table-container">
        <table className="detail-table qos-monitor-table">
          <thead>
            <tr>
              <th>Class</th>
              <th title="Requests of this class waiting in the queue now">Waiting</th>
              <th title="Requests admitted">Admitted</th>
              <th title="Requests rejected because the queue was full">Rejected</th>
              <th title="Requests that waited longer than MaxQueueWaitMs">Timed Out</th>
              <th title="Requests the client abandoned while waiting">Aborted</th>
              <th title="Admitted requests that then timed out waiting for a free endpoint slot">Slot Timeouts</th>
              <th title="Average / 95th percentile / maximum queue wait">Avg / P95 / Max Wait</th>
              <th title="Most recent admission">Last Admitted</th>
              <th title="Most recent rejection">Last Rejected</th>
            </tr>
          </thead>
          <tbody>
            {(selected?.Classes || []).length === 0 ? (
              <tr><td colSpan="10" className="detail-table-empty-cell">No traffic classes have seen requests since the server started.</td></tr>
            ) : selected.Classes.map((cls) => (
              <tr key={cls.ClassName}>
                <td className="detail-table-title-cell">
                  <span className="qos-monitor-class-name">
                    <span className="request-history-legend-color" style={{ backgroundColor: qosClassColor(classNames.indexOf(cls.ClassName)) }} />
                    {cls.ClassName}
                  </span>
                </td>
                <td className={cls.Waiting > 0 ? 'qos-monitor-emphasis' : ''}>{formatNumber(cls.Waiting)}</td>
                <td>{formatNumber(cls.Admitted)}</td>
                <td className={cls.Rejected > 0 ? 'qos-monitor-failed' : ''}>{formatNumber(cls.Rejected)}</td>
                <td className={cls.TimedOut > 0 ? 'qos-monitor-failed' : ''}>{formatNumber(cls.TimedOut)}</td>
                <td>{formatNumber(cls.Aborted)}</td>
                <td className={cls.EndpointSlotTimeouts > 0 ? 'qos-monitor-failed' : ''}>{formatNumber(cls.EndpointSlotTimeouts)}</td>
                <td className="qos-monitor-nowrap">{formatWaitMs(cls.AverageWaitMs)} / {formatWaitMs(cls.P95WaitMs)} / {formatWaitMs(cls.MaxWaitMs)}</td>
                <td className="qos-monitor-nowrap">{formatUtcDate(cls.LastAdmittedUtc)}</td>
                <td className="qos-monitor-nowrap">{formatUtcDate(cls.LastRejectedUtc)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );

  const renderEndpointTable = () => (
    <section className="dashboard-section" aria-labelledby="qos-monitor-endpoints-title">
      <div className="request-history-chart-header">
        <h2 id="qos-monitor-endpoints-title">Endpoint Slots</h2>
        <span className="text-muted qos-monitor-updated">Live</span>
      </div>
      <div className="detail-table-container">
        <table className="detail-table qos-monitor-table">
          <thead>
            <tr>
              <th>Endpoint</th>
              <th title="Whether the endpoint is active">Active</th>
              <th title="Latest health check result">Health</th>
              <th title="Requests in flight / MaxParallelRequests (0 = unlimited)">In Flight / Max</th>
            </tr>
          </thead>
          <tbody>
            {(selected?.Endpoints || []).length === 0 ? (
              <tr><td colSpan="4" className="detail-table-empty-cell">No endpoints are attached to this runner.</td></tr>
            ) : selected.Endpoints.map((endpoint) => {
              const limited = endpoint.MaxParallelRequests > 0;
              const saturated = limited && endpoint.InFlight >= endpoint.MaxParallelRequests;
              const percent = limited ? Math.min(100, (endpoint.InFlight / endpoint.MaxParallelRequests) * 100) : 0;
              return (
                <tr key={endpoint.EndpointId} className={saturated ? 'qos-monitor-saturated' : ''}>
                  <td className="detail-table-title-cell">
                    {endpoint.EndpointName || endpoint.EndpointId}
                    {saturated && (
                      <span className="service-state-badge warning qos-monitor-inline-badge" title="Every slot on this endpoint is in use; new admissions wait for a free slot">Saturated</span>
                    )}
                  </td>
                  <td>
                    <span className={`service-state-badge ${endpoint.Active ? 'success' : 'neutral'}`}>{endpoint.Active ? 'Active' : 'Inactive'}</span>
                  </td>
                  <td>
                    <span className={`service-state-badge ${endpoint.IsHealthy ? 'success' : 'danger'}`}>{endpoint.IsHealthy ? 'Healthy' : 'Unhealthy'}</span>
                  </td>
                  <td>
                    <div className="qos-monitor-slot-cell">
                      <span className="qos-monitor-nowrap">
                        {formatNumber(endpoint.InFlight)} / {limited ? formatNumber(endpoint.MaxParallelRequests) : 'unlimited'}
                      </span>
                      {limited && (
                        <div
                          className="qos-monitor-slot-track"
                          role="meter"
                          aria-valuemin={0}
                          aria-valuemax={endpoint.MaxParallelRequests}
                          aria-valuenow={endpoint.InFlight}
                          aria-label={`${endpoint.InFlight} of ${endpoint.MaxParallelRequests} slots in use`}
                        >
                          <div className={'qos-monitor-slot-fill' + (saturated ? ' saturated' : '')} style={{ width: `${percent}%` }} />
                        </div>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );

  const renderSelected = () => {
    if (!selected) return null;
    const runnerName = selected.VirtualModelRunnerName || selected.VirtualModelRunnerId;

    if (selected.SchedulerState === 'Idle') {
      return (
        <>
          <section className="dashboard-section">
            <div className="qos-monitor-empty">
              <strong>No QoS activity for {runnerName} since the server started</strong>
              <p>
                The QoS runtime for a runner is built when its first request is admitted. Statistics are kept in memory,
                so they start empty after every server restart. Send traffic through this runner and this page fills in
                within a few seconds.
              </p>
            </div>
          </section>
          {renderEndpointTable()}
        </>
      );
    }

    return (
      <>
        {selected.SchedulerState === 'Recovering' && (
          <div className="qos-monitor-alert" role="alert">
            <strong>The QoS scheduler for {runnerName} faulted and is recovering.</strong>
            {selected.LastSchedulerError && (
              <span> Last error{selected.LastSchedulerErrorUtc ? ` at ${formatUtcDate(selected.LastSchedulerErrorUtc)}` : ''}: {selected.LastSchedulerError}</span>
            )}
          </div>
        )}

        <div className="analytics-metric-grid">
          <MetricCard
            label="Waiting Now"
            value={formatNumber(selected.Waiting)}
            sublabel={selected.MaxQueueWaitMs > 0 ? `max queue wait ${formatWaitMs(selected.MaxQueueWaitMs)}` : 'no queue wait deadline'}
            tooltip="Requests waiting in the QoS queue right now, across all classes"
            tone={selected.Waiting > 0 ? 'warning' : null}
          />
          <MetricCard
            label="In Use / Capacity"
            value={formatCapacity(selected)}
            sublabel={selected.Capacity > 0 ? `${Math.round((selected.InUse / selected.Capacity) * 100)}% of admission capacity` : 'endpoints set no parallel limit'}
            tooltip="Admission permits held by admitted requests / the sum of the endpoints' MaxParallelRequests"
          />
          <MetricCard
            label="Admitted"
            value={formatNumber(rangeTotals.admitted)}
            sublabel={range.label.toLowerCase()}
            tooltip="Requests admitted by QoS in the selected time range"
          />
          <MetricCard
            label="Rejected / Timed Out / Aborted"
            value={formatNumber(rangeTotals.failed)}
            sublabel={range.label.toLowerCase()}
            tooltip="Requests rejected because the queue was full, timed out waiting in the queue, or abandoned by the client, in the selected time range"
            tone={rangeTotals.failed > 0 ? 'danger' : null}
          />
          <MetricCard
            label="P95 Wait"
            value={formatWaitMs(p95Class?.P95WaitMs || 0)}
            sublabel={p95Class ? `${p95Class.ClassName}, since server start` : 'since server start'}
            tooltip="Highest per-class 95th percentile queue wait since the server started"
          />
          <MetricCard
            label="Endpoint Slot Timeouts"
            value={formatNumber(rangeTotals.slotTimeouts)}
            sublabel={`${formatNumber(sumClasses(selected, 'EndpointSlotTimeouts'))} since server start`}
            tooltip="Admitted requests that then timed out waiting for a free endpoint slot, in the selected time range"
            tone={rangeTotals.slotTimeouts > 0 ? 'danger' : null}
          />
        </div>

        <QosAdmissionsChart buckets={buckets} range={range} runnerName={runnerName} error={historyError} />
        <QosWaitDepthChart buckets={buckets} classNames={classNames} range={range} runnerName={runnerName} error={historyError} />
        {renderClassTable()}
        {renderEndpointTable()}
      </>
    );
  };

  return (
    <div className="view-container qos-monitor-view">
      <div className="view-header">
        <div>
          <h1>QoS Monitor</h1>
          <p className="view-subtitle">
            Live queueing and admission for each virtual model runner. Statistics are kept in memory and reset when the server restarts.
          </p>
        </div>
        <div className="view-actions">
          <RefreshButton onClick={fetchAll} title="Refresh QoS runtime state" disabled={loading && runners === null} />
        </div>
      </div>

      <ErrorBanner message={error} onDismiss={handleDismissError} />

      {runners === null && !error && (
        <div className="data-table-loading">
          <div className="spinner"></div>
          <span>Loading...</span>
        </div>
      )}

      {runners !== null && snapshots.length === 0 && (
        <section className="dashboard-section">
          <div className="qos-monitor-empty">
            <strong>No virtual model runners</strong>
            <p>QoS runtime state appears here for each virtual model runner. Create a virtual model runner to start monitoring its queue.</p>
          </div>
        </section>
      )}

      {snapshots.length > 0 && (
        <>
          <div className="qos-monitor-toolbar">
            <div className="filter-group">
              <label htmlFor="qos-monitor-runner">Runner:</label>
              <select
                id="qos-monitor-runner"
                value={selectedId || ''}
                onChange={(event) => handleSelect(event.target.value)}
              >
                {snapshots.map((item) => (
                  <option key={item.VirtualModelRunnerId} value={item.VirtualModelRunnerId}>
                    {item.VirtualModelRunnerName || item.VirtualModelRunnerId}{showTenant ? ` (${item.TenantId})` : ''}
                  </option>
                ))}
              </select>
            </div>
            <div className="request-history-time-range-bar qos-monitor-time-range">
              <span className="request-history-time-range-label" id="qos-monitor-range-label">Time range</span>
              <div className="request-history-time-tabs" role="group" aria-labelledby="qos-monitor-range-label">
                {QOS_TIME_RANGES.map((entry) => (
                  <button
                    key={entry.value}
                    type="button"
                    className={'request-history-time-tab' + (timeRange === entry.value ? ' active' : '')}
                    aria-pressed={timeRange === entry.value}
                    onClick={() => handleTimeRangeChange(entry.value)}
                    title={`Show QoS history for the ${entry.label.toLowerCase()}`}
                  >
                    {entry.label}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {renderOverview()}
          {renderSelected()}
        </>
      )}
    </div>
  );
}

export default QosMonitor;
