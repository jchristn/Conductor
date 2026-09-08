import React, { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useApp } from '../context/AppContext';
import { copyChartPng } from '../utils/chartExport';

// Time ranges use the same bucket sizes as the Verbex, Lattice, and Pneuma dashboards:
// last hour -> 1 minute buckets, last day -> 15 minute buckets, last week -> 1 hour buckets,
// last month -> 6 hour buckets. The selected range fully drives the chart window.
const TIME_RANGES = [
  { label: 'Last Hour', value: 'hour', interval: 'minute', stepMs: 60_000, bucketCount: 60 },
  { label: 'Last Day', value: 'day', interval: '15minute', stepMs: 900_000, bucketCount: 96 },
  { label: 'Last Week', value: 'week', interval: 'hour', stepMs: 3_600_000, bucketCount: 24 * 7 },
  { label: 'Last Month', value: 'month', interval: '6hour', stepMs: 21_600_000, bucketCount: 4 * 30 }
];

const MAX_X_AXIS_LABELS = 8;

// Chart geometry (SVG viewBox units). The SVG scales to fill its container width.
const CHART_WIDTH = 800;
const CHART_HEIGHT = 240;
const PADDING_LEFT = 48;
const PADDING_RIGHT = 20;
const PADDING_TOP = 16;
const PADDING_BOTTOM = 40;
const INNER_WIDTH = CHART_WIDTH - PADDING_LEFT - PADDING_RIGHT;
const INNER_HEIGHT = CHART_HEIGHT - PADDING_TOP - PADDING_BOTTOM;

// Filters that describe a time window are ignored by the chart because the range tabs own the window.
const TIME_FILTER_KEYS = new Set(['createdAfterUtc', 'createdBeforeUtc']);

function floorToStep(timestamp, stepMs) {
  return Math.floor(timestamp / stepMs) * stepMs;
}

function getRangeWindow(range, nowMs) {
  const endExclusiveMs = floorToStep(nowMs, range.stepMs) + range.stepMs;
  const startMs = endExclusiveMs - range.bucketCount * range.stepMs;
  return { startMs, endExclusiveMs };
}

function buildBuckets(summary, range, startMs) {
  const apiBuckets = new Map(
    (summary?.Data || []).map((bucket) => [
      floorToStep(new Date(bucket.TimestampUtc).getTime(), range.stepMs),
      bucket
    ])
  );

  return Array.from({ length: range.bucketCount }, (_, index) => {
    const timestamp = startMs + index * range.stepMs;
    const apiBucket = apiBuckets.get(timestamp);
    return {
      timestampUtc: new Date(timestamp).toISOString(),
      successCount: apiBucket?.SuccessCount || 0,
      failureCount: apiBucket?.FailureCount || 0
    };
  });
}

// Evenly spaced integer Y ticks. Always at least 2 labels; the max is rounded up so every tick is a
// whole number and the ticks stay evenly spaced by both value and position.
function computeYTicks(maxCount) {
  const segments = 4;
  const niceMax = Math.max(segments, Math.ceil(Math.max(maxCount, 1) / segments) * segments);
  const step = niceMax / segments;
  const ticks = [];
  for (let i = 0; i <= segments; i++) {
    ticks.push(Math.round(step * i));
  }
  return ticks;
}

// At most MAX_X_AXIS_LABELS labels, evenly spaced across the buckets (including the first and last).
function computeXLabelIndices(bucketCount) {
  if (bucketCount <= 0) return [];
  const labelCount = Math.min(MAX_X_AXIS_LABELS, bucketCount);
  if (labelCount === 1) return [0];
  const indices = new Set();
  for (let i = 0; i < labelCount; i++) {
    indices.add(Math.round((i * (bucketCount - 1)) / (labelCount - 1)));
  }
  return Array.from(indices).sort((a, b) => a - b);
}

function formatChartLabel(timestamp, interval) {
  const date = new Date(timestamp);
  if (interval === 'day') {
    return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }
  if (interval === 'hour' || interval === '6hour') {
    return date.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }
  return date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

function formatTooltipTimestamp(timestamp, interval) {
  const date = new Date(timestamp);
  if (interval === 'day') {
    return date.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });
  }
  return date.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

function RequestHistorySummaryChart({ filters }) {
  const { api } = useApp();
  const [timeRange, setTimeRange] = useState('day');
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [hovered, setHovered] = useState(null);
  const [tooltipPos, setTooltipPos] = useState(null);
  const [copyState, setCopyState] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const containerRef = useRef(null);
  const tooltipRef = useRef(null);
  const svgRef = useRef(null);

  const range = TIME_RANGES.find((entry) => entry.value === timeRange) || TIME_RANGES[1];

  // Only the non-time filters flow into the chart query; the range tabs own the time window.
  const scopedFilters = Object.fromEntries(
    Object.entries(filters || {}).filter(([key, value]) => value !== '' && value != null && !TIME_FILTER_KEYS.has(key))
  );
  const scopedFilterKey = JSON.stringify(scopedFilters);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    setHovered(null);

    const rangeWindow = getRangeWindow(range, Date.now());
    const params = {
      ...scopedFilters,
      startUtc: new Date(rangeWindow.startMs).toISOString(),
      endUtc: new Date(rangeWindow.endExclusiveMs).toISOString(),
      interval: range.interval
    };

    api.getRequestHistorySummary(params)
      .then((result) => {
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, timeRange, scopedFilterKey, refreshKey]);

  const rangeWindow = getRangeWindow(range, Date.now());
  const buckets = buildBuckets(summary, range, rangeWindow.startMs);
  const maxCount = Math.max(1, ...buckets.map((bucket) => bucket.successCount + bucket.failureCount));
  const yTicks = computeYTicks(maxCount);
  const yMax = yTicks[yTicks.length - 1] || 1;
  const xLabelIndices = computeXLabelIndices(buckets.length);

  const barGroupWidth = INNER_WIDTH / Math.max(buckets.length, 1);
  const barWidth = Math.max(2, Math.min(40, barGroupWidth * 0.72));

  const totalRequests = summary?.TotalRequests || 0;
  const totalSuccess = summary?.TotalSuccess || 0;
  const totalFailure = summary?.TotalFailure || 0;

  // Keep the tooltip fully inside the chart container: clamp its top/left against the container and
  // the tooltip's own measured size so it never renders outside the chart bounds.
  useLayoutEffect(() => {
    if (!hovered || !containerRef.current || !tooltipRef.current) {
      return;
    }
    const containerRect = containerRef.current.getBoundingClientRect();
    const tooltipRect = tooltipRef.current.getBoundingClientRect();
    const margin = 8;
    const offset = 14;

    let left = hovered.relX + offset;
    if (left + tooltipRect.width + margin > containerRect.width) {
      left = hovered.relX - tooltipRect.width - offset;
    }
    left = Math.max(margin, Math.min(left, containerRect.width - tooltipRect.width - margin));

    let top = hovered.relY + offset;
    if (top + tooltipRect.height + margin > containerRect.height) {
      top = hovered.relY - tooltipRect.height - offset;
    }
    top = Math.max(margin, Math.min(top, containerRect.height - tooltipRect.height - margin));

    setTooltipPos((current) => {
      if (current && current.left === left && current.top === top) return current;
      return { left, top };
    });
  }, [hovered]);

  const handleBarHover = (index, event) => {
    const containerRect = containerRef.current?.getBoundingClientRect();
    if (!containerRect) return;
    setHovered({
      index,
      relX: event.clientX - containerRect.left,
      relY: event.clientY - containerRect.top
    });
  };

  const handleCopy = async () => {
    const result = await copyChartPng(svgRef.current, {
      title: `Request History - ${range.label}`,
      xLabel: 'Time',
      yLabel: 'Requests',
      legend: [
        { label: 'Success (1xx-3xx)', color: cssColor('--success-color', '#10b981') },
        { label: 'Failed (4xx-5xx)', color: cssColor('--danger-color', '#ef4444') }
      ]
    });
    setCopyState(result);
    setTimeout(() => setCopyState(null), 1800);
  };

  const copyTitle = copyState === 'copied'
    ? 'Copied chart image to clipboard'
    : copyState === 'downloaded'
      ? 'Clipboard unavailable - downloaded chart image instead'
      : 'Copy chart as an image';

  const hoveredBucket = hovered ? buckets[hovered.index] : null;

  return (
    <div className="dashboard-section request-history-chart-section">
      <div className="request-history-chart-header">
        <h2>API Requests Over Time</h2>
        <div className="request-history-chart-controls">
          <div className="request-history-time-tabs">
            {TIME_RANGES.map((entry) => (
              <button
                key={entry.value}
                type="button"
                className={'request-history-time-tab' + (timeRange === entry.value ? ' active' : '')}
                onClick={() => setTimeRange(entry.value)}
                title={`Show request traffic for the ${entry.label.toLowerCase()}`}
              >
                {entry.label}
              </button>
            ))}
          </div>
          <button
            type="button"
            className="request-history-refresh-btn"
            onClick={handleCopy}
            title={copyTitle}
          >
            {copyState === 'copied' || copyState === 'downloaded' ? (
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="20 6 9 17 4 12" />
              </svg>
            ) : (
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
              </svg>
            )}
          </button>
          <button
            type="button"
            className="request-history-refresh-btn"
            onClick={() => setRefreshKey((key) => key + 1)}
            title="Refresh chart"
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

      {error && (
        <div className="request-history-chart-empty" style={{ color: 'var(--danger-color)' }}>{error}</div>
      )}

      {!error && (
        <div className="request-history-chart-container" ref={containerRef} style={{ position: 'relative' }}>
          <svg
            ref={svgRef}
            width="100%"
            viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`}
            preserveAspectRatio="xMidYMid meet"
            style={{ display: 'block' }}
          >
            {/* Y-axis grid lines and labels */}
            {yTicks.map((tick) => {
              const y = PADDING_TOP + INNER_HEIGHT - (tick / yMax) * INNER_HEIGHT;
              return (
                <g key={tick}>
                  <line
                    x1={PADDING_LEFT}
                    y1={y}
                    x2={CHART_WIDTH - PADDING_RIGHT}
                    y2={y}
                    stroke="var(--border-color)"
                    strokeDasharray={tick === 0 ? 'none' : '4,4'}
                    strokeWidth={0.75}
                  />
                  <text
                    x={PADDING_LEFT - 10}
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
            {buckets.map((bucket, index) => {
              const success = bucket.successCount;
              const failure = bucket.failureCount;
              const successHeight = (success / yMax) * INNER_HEIGHT;
              const failureHeight = (failure / yMax) * INNER_HEIGHT;
              const x = PADDING_LEFT + index * barGroupWidth + (barGroupWidth - barWidth) / 2;
              const failureY = PADDING_TOP + INNER_HEIGHT - failureHeight;
              const successY = failureY - successHeight;
              const isHovered = hovered?.index === index;

              return (
                <g
                  key={bucket.timestampUtc}
                  onMouseEnter={(event) => handleBarHover(index, event)}
                  onMouseMove={(event) => handleBarHover(index, event)}
                  onMouseLeave={() => setHovered(null)}
                >
                  {/* Invisible full-height hit area so hovering anywhere in the column shows the tooltip */}
                  <rect
                    x={PADDING_LEFT + index * barGroupWidth}
                    y={PADDING_TOP}
                    width={barGroupWidth}
                    height={INNER_HEIGHT}
                    fill="transparent"
                  />
                  {success > 0 && (
                    <rect
                      x={x}
                      y={successY}
                      width={barWidth}
                      height={successHeight}
                      rx={2}
                      fill="var(--success-color)"
                      opacity={isHovered ? 1 : 0.85}
                    />
                  )}
                  {failure > 0 && (
                    <rect
                      x={x}
                      y={failureY}
                      width={barWidth}
                      height={failureHeight}
                      rx={2}
                      fill="var(--danger-color)"
                      opacity={isHovered ? 1 : 0.85}
                    />
                  )}
                </g>
              );
            })}

            {/* X-axis labels (evenly spaced, at most 8) */}
            {xLabelIndices.map((index) => {
              const anchor = index === 0 ? 'start' : index === buckets.length - 1 ? 'end' : 'middle';
              let x = PADDING_LEFT + index * barGroupWidth + barGroupWidth / 2;
              if (anchor === 'start') x = PADDING_LEFT;
              if (anchor === 'end') x = CHART_WIDTH - PADDING_RIGHT;
              return (
                <text
                  key={index}
                  x={x}
                  y={CHART_HEIGHT - 14}
                  textAnchor={anchor}
                  fontSize="11"
                  fill="var(--text-secondary)"
                >
                  {formatChartLabel(buckets[index].timestampUtc, range.interval)}
                </text>
              );
            })}
          </svg>

          {hoveredBucket && (
            <div
              ref={tooltipRef}
              className="request-history-chart-tooltip"
              style={{
                left: tooltipPos ? `${tooltipPos.left}px` : '-9999px',
                top: tooltipPos ? `${tooltipPos.top}px` : '-9999px',
                transform: 'none'
              }}
            >
              <div style={{ fontWeight: 600, marginBottom: 4 }}>{formatTooltipTimestamp(hoveredBucket.timestampUtc, range.interval)}</div>
              <div><span style={{ color: 'var(--success-color)' }}>Success:</span> {hoveredBucket.successCount.toLocaleString()}</div>
              <div><span style={{ color: 'var(--danger-color)' }}>Failed:</span> {hoveredBucket.failureCount.toLocaleString()}</div>
              <div>Total: {(hoveredBucket.successCount + hoveredBucket.failureCount).toLocaleString()}</div>
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

function cssColor(name, fallback) {
  if (typeof window === 'undefined') return fallback;
  const value = getComputedStyle(document.documentElement).getPropertyValue(name);
  return (value && value.trim()) || fallback;
}

export default RequestHistorySummaryChart;
