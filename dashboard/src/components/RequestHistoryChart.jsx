import React, { useState, useEffect } from 'react';
import { useApp } from '../context/AppContext';

const TIME_RANGES = [
  { label: 'Last Hour', value: 'hour', interval: 'minute', hours: 1, stepMs: 60000 },
  { label: 'Last Day', value: 'day', interval: '15minute', hours: 24, stepMs: 900000 },
  { label: 'Last Week', value: 'week', interval: 'hour', hours: 168, stepMs: 3600000 },
  { label: 'Last Month', value: 'month', interval: '6hour', hours: 720, stepMs: 21600000 }
];

function floorToStep(ts, stepMs) {
  return Math.floor(ts / stepMs) * stepMs;
}

function generateAllBuckets(startMs, endMs, stepMs) {
  const buckets = [];
  const flooredStart = floorToStep(startMs, stepMs);
  for (let t = flooredStart; t < endMs; t += stepMs) {
    buckets.push({
      TimestampUtc: new Date(t).toISOString(),
      SuccessCount: 0,
      FailureCount: 0,
      _key: t
    });
  }
  return buckets;
}

function mergeBuckets(allBuckets, apiData, stepMs) {
  const dataMap = new Map();
  for (const d of apiData) {
    const key = floorToStep(new Date(d.TimestampUtc).getTime(), stepMs);
    dataMap.set(key, d);
  }
  return allBuckets.map(b => {
    const match = dataMap.get(b._key);
    if (match) {
      return {
        ...b,
        SuccessCount: match.SuccessCount || 0,
        FailureCount: match.FailureCount || 0
      };
    }
    return b;
  });
}

function RequestHistoryChart() {
  const { api } = useApp();
  const [timeRange, setTimeRange] = useState('day');
  const [vmrGuid, setVmrGuid] = useState('');
  const [vmrList, setVmrList] = useState([]);
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [hoveredBar, setHoveredBar] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    api.listVirtualModelRunners({ maxResults: 100 })
      .then(result => {
        setVmrList(result.Data || []);
      })
      .catch(() => {});
  }, [api]);

  useEffect(() => {
    let cancelled = false;
    setSummary(null);
    setLoading(true);
    setError(null);
    setHoveredBar(null);

    const range = TIME_RANGES.find(r => r.value === timeRange);
    const endUtc = new Date().toISOString();
    const startUtc = new Date(Date.now() - range.hours * 3600000).toISOString();

    const params = {
      startUtc,
      endUtc,
      interval: range.interval
    };
    if (vmrGuid) params.vmrGuid = vmrGuid;

    api.getRequestHistorySummary(params)
      .then(result => {
        if (!cancelled) setSummary(result);
      })
      .catch(() => {
        if (!cancelled) {
          setError('Failed to load request history summary');
          setSummary(null);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [api, timeRange, vmrGuid, refreshKey]);

  const range = TIME_RANGES.find(r => r.value === timeRange);

  const buckets = (() => {
    const endMs = Date.now();
    const startMs = endMs - range.hours * 3600000;
    const allBuckets = generateAllBuckets(startMs, endMs, range.stepMs);
    const apiData = summary?.Data || [];
    return mergeBuckets(allBuckets, apiData, range.stepMs);
  })();

  const maxCount = Math.max(1, ...buckets.map(b => (b.SuccessCount || 0) + (b.FailureCount || 0)));

  const chartHeight = 200;
  const chartPaddingTop = 20;
  const chartPaddingBottom = 40;
  const chartPaddingLeft = 50;
  const chartPaddingRight = 16;
  const barAreaHeight = chartHeight - chartPaddingTop - chartPaddingBottom;

  const yTicks = computeYTicks(maxCount);

  function computeYTicks(max) {
    if (max <= 0) return [0];
    const step = Math.max(1, Math.ceil(max / 4));
    const ticks = [];
    for (let i = 0; i <= max; i += step) {
      ticks.push(i);
    }
    if (ticks[ticks.length - 1] < max) {
      ticks.push(ticks[ticks.length - 1] + step);
    }
    return ticks;
  }

  function formatBucketLabel(timestamp) {
    const d = new Date(timestamp);
    switch (range.interval) {
      case 'minute':
      case '15minute':
        return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
      case 'hour':
      case '6hour':
        if (range.hours > 48) {
          return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }) + ' ' +
                 d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
        }
        return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
      case 'day':
        return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
      default:
        return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
    }
  }

  function formatTooltipTime(timestamp) {
    const d = new Date(timestamp);
    switch (range.interval) {
      case 'day':
        return d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' });
      default:
        return d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
    }
  }

  const totalSuccess = summary?.TotalSuccess || 0;
  const totalFailure = summary?.TotalFailure || 0;
  const totalRequests = summary?.TotalRequests || 0;

  return (
    <div className="dashboard-section request-history-chart-section">
      <div className="request-history-chart-header">
        <h2>Request History</h2>
        <div className="request-history-chart-controls">
          <select
            value={vmrGuid}
            onChange={e => setVmrGuid(e.target.value)}
            className="request-history-select"
          >
            <option value="">All Virtual Model Runners</option>
            {vmrList.map(vmr => (
              <option key={vmr.Id} value={vmr.Id}>{vmr.Name}</option>
            ))}
          </select>
          <div className="request-history-time-tabs">
            {TIME_RANGES.map(r => (
              <button
                key={r.value}
                className={'request-history-time-tab' + (timeRange === r.value ? ' active' : '')}
                onClick={() => setTimeRange(r.value)}
              >
                {r.label}
              </button>
            ))}
          </div>
          <button
            className="request-history-refresh-btn"
            onClick={() => setRefreshKey(k => k + 1)}
            title="Refresh"
            disabled={loading}
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={loading ? { animation: 'spin 1s linear infinite' } : undefined}>
              <polyline points="23 4 23 10 17 10" />
              <polyline points="1 20 1 14 7 14" />
              <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15" />
            </svg>
          </button>
        </div>
      </div>

      <div className="request-history-chart-stats">
        <div className="request-history-stat">
          <span className="request-history-stat-value">{totalRequests.toLocaleString()}</span>
          <span className="request-history-stat-label">Total</span>
        </div>
        <div className="request-history-stat">
          <span className="request-history-stat-value" style={{ color: 'var(--success-color)' }}>{totalSuccess.toLocaleString()}</span>
          <span className="request-history-stat-label">Success</span>
        </div>
        <div className="request-history-stat">
          <span className="request-history-stat-value" style={{ color: 'var(--danger-color)' }}>{totalFailure.toLocaleString()}</span>
          <span className="request-history-stat-label">Failed</span>
        </div>
      </div>

      {loading && !summary && (
        <div className="request-history-chart-empty">Loading...</div>
      )}

      {error && (
        <div className="request-history-chart-empty" style={{ color: 'var(--danger-color)' }}>{error}</div>
      )}

      {!loading && !error && buckets.length === 0 && (
        <div className="request-history-chart-empty">No request data for this time range</div>
      )}

      {buckets.length > 0 && (
        <div className="request-history-chart-container" style={{ position: 'relative' }}>
          <svg
            width="100%"
            viewBox={`0 0 800 ${chartHeight}`}
            preserveAspectRatio="xMidYMid meet"
            style={{ display: 'block' }}
          >
            {/* Y-axis grid lines and labels */}
            {yTicks.map(tick => {
              const yMax = yTicks[yTicks.length - 1] || 1;
              const y = chartPaddingTop + barAreaHeight - (tick / yMax) * barAreaHeight;
              return (
                <g key={tick}>
                  <line
                    x1={chartPaddingLeft}
                    y1={y}
                    x2={800 - chartPaddingRight}
                    y2={y}
                    stroke="var(--border-color)"
                    strokeDasharray={tick === 0 ? 'none' : '4,4'}
                    strokeWidth={0.5}
                  />
                  <text
                    x={chartPaddingLeft - 8}
                    y={y + 4}
                    textAnchor="end"
                    fontSize="11"
                    fill="var(--text-secondary)"
                  >
                    {tick}
                  </text>
                </g>
              );
            })}

            {/* Bars */}
            {(() => {
              const barAreaWidth = 800 - chartPaddingLeft - chartPaddingRight;
              const barGroupWidth = barAreaWidth / buckets.length;
              const barWidth = Math.max(2, Math.min(40, barGroupWidth * 0.7));
              const yMax = yTicks[yTicks.length - 1] || 1;

              return buckets.map((bucket, i) => {
                const success = bucket.SuccessCount || 0;
                const failure = bucket.FailureCount || 0;
                const total = success + failure;
                const successHeight = (success / yMax) * barAreaHeight;
                const failureHeight = (failure / yMax) * barAreaHeight;
                const x = chartPaddingLeft + i * barGroupWidth + (barGroupWidth - barWidth) / 2;
                const successY = chartPaddingTop + barAreaHeight - successHeight - failureHeight;
                const failureY = chartPaddingTop + barAreaHeight - failureHeight;

                const labelCharWidth = 6;
                const labelPadding = 10;
                const isCompoundLabel = (range.interval === 'hour' || range.interval === '6hour') && range.hours > 48;
                const estLabelChars = isCompoundLabel ? 14 : 6;
                const estLabelPx = estLabelChars * labelCharWidth + labelPadding;
                const maxLabels = Math.max(1, Math.floor(barAreaWidth / estLabelPx));
                const labelInterval = Math.max(1, Math.ceil(buckets.length / maxLabels));
                const showLabel = i % labelInterval === 0;

                return (
                  <g
                    key={i}
                    onMouseEnter={() => setHoveredBar(i)}
                    onMouseLeave={() => setHoveredBar(null)}
                    style={{ cursor: 'default' }}
                  >
                    {/* Invisible hit area for hover */}
                    <rect
                      x={chartPaddingLeft + i * barGroupWidth}
                      y={chartPaddingTop}
                      width={barGroupWidth}
                      height={barAreaHeight + chartPaddingBottom}
                      fill="transparent"
                    />
                    {/* Success bar */}
                    {success > 0 && (
                      <rect
                        x={x}
                        y={successY}
                        width={barWidth}
                        height={successHeight}
                        rx={2}
                        fill="var(--success-color)"
                        opacity={hoveredBar === i ? 1 : 0.85}
                      />
                    )}
                    {/* Failure bar */}
                    {failure > 0 && (
                      <rect
                        x={x}
                        y={failureY}
                        width={barWidth}
                        height={failureHeight}
                        rx={2}
                        fill="var(--danger-color)"
                        opacity={hoveredBar === i ? 1 : 0.85}
                      />
                    )}
                    {/* X-axis label */}
                    {showLabel && (
                      <text
                        x={chartPaddingLeft + i * barGroupWidth + barGroupWidth / 2}
                        y={chartHeight - 6}
                        textAnchor="middle"
                        fontSize="10"
                        fill="var(--text-secondary)"
                      >
                        {formatBucketLabel(bucket.TimestampUtc)}
                      </text>
                    )}
                  </g>
                );
              });
            })()}
          </svg>

          {/* Tooltip */}
          {hoveredBar !== null && buckets[hoveredBar] && (
            <div
              className="request-history-chart-tooltip"
              style={{
                left: `${((hoveredBar + 0.5) / buckets.length) * 100}%`
              }}
            >
              <div style={{ fontWeight: 600, marginBottom: 4 }}>{formatTooltipTime(buckets[hoveredBar].TimestampUtc)}</div>
              <div><span style={{ color: 'var(--success-color)' }}>Success:</span> {(buckets[hoveredBar].SuccessCount || 0).toLocaleString()}</div>
              <div><span style={{ color: 'var(--danger-color)' }}>Failed:</span> {(buckets[hoveredBar].FailureCount || 0).toLocaleString()}</div>
              <div>Total: {((buckets[hoveredBar].SuccessCount || 0) + (buckets[hoveredBar].FailureCount || 0)).toLocaleString()}</div>
            </div>
          )}
        </div>
      )}

      <div className="request-history-chart-legend">
        <span className="request-history-legend-item">
          <span className="request-history-legend-color" style={{ backgroundColor: 'var(--success-color)' }} />
          Success (1xx-3xx)
        </span>
        <span className="request-history-legend-item">
          <span className="request-history-legend-color" style={{ backgroundColor: 'var(--danger-color)' }} />
          Failed (4xx-5xx)
        </span>
      </div>
    </div>
  );
}

export default RequestHistoryChart;
