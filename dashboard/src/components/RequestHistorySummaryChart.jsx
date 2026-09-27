import React, { useEffect, useRef, useState } from 'react';
import { useApp } from '../context/AppContext';
import ChartCopyButton from './ChartCopyButton';
import useClampedTooltip from '../hooks/useClampedTooltip';
import { computeXLabelIndices, computeYTicks, cssColor, parseUtcMs } from '../utils/chartHelpers';
import { floorToStep, getRangeWindow, getTimeRange } from '../utils/requestHistoryTimeRanges';

// Chart geometry (SVG viewBox units). The SVG scales to fill its container width.
const CHART_WIDTH = 800;
const CHART_HEIGHT = 240;
const PADDING_LEFT = 48;
const PADDING_RIGHT = 20;
const PADDING_TOP = 16;
const PADDING_BOTTOM = 40;
const INNER_WIDTH = CHART_WIDTH - PADDING_LEFT - PADDING_RIGHT;
const INNER_HEIGHT = CHART_HEIGHT - PADDING_TOP - PADDING_BOTTOM;

// Filters that describe a time window are ignored by the chart because the selected time range owns the window.
const TIME_FILTER_KEYS = new Set(['createdAfterUtc', 'createdBeforeUtc']);

function buildBuckets(summary, range, startMs) {
  const apiBuckets = new Map(
    (summary?.Data || []).map((bucket) => [
      floorToStep(parseUtcMs(bucket.TimestampUtc), range.stepMs),
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

function RequestHistorySummaryChart({ filters, timeRange, refreshToken }) {
  const { api } = useApp();
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [hovered, setHovered] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const containerRef = useRef(null);
  const tooltipRef = useRef(null);
  const svgRef = useRef(null);

  const range = getTimeRange(timeRange);

  // Only the non-time filters flow into the chart query; the selected time range owns the window.
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
  }, [api, timeRange, scopedFilterKey, refreshKey, refreshToken]);

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

  // Keep the tooltip fully inside the chart container.
  const tooltipPos = useClampedTooltip(containerRef, tooltipRef, hovered);

  const handleBarHover = (index, event) => {
    const containerRect = containerRef.current?.getBoundingClientRect();
    if (!containerRect) return;
    setHovered({
      index,
      relX: event.clientX - containerRect.left,
      relY: event.clientY - containerRect.top
    });
  };

  const getCopyOptions = () => ({
    title: `Request History - ${range.label}`,
    xLabel: 'Time',
    yLabel: 'Requests',
    legend: [
      { label: 'Success (1xx-3xx)', color: cssColor('--success-color', '#10b981') },
      { label: 'Failed (4xx-5xx)', color: cssColor('--danger-color', '#ef4444') }
    ]
  });

  const hoveredBucket = hovered ? buckets[hovered.index] : null;

  return (
    <div className="dashboard-section request-history-chart-section">
      <div className="request-history-chart-header">
        <h2>API Requests Over Time</h2>
        <div className="request-history-chart-controls">
          <ChartCopyButton svgRef={svgRef} getOptions={getCopyOptions} />
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
                    y={y + 3}
                    textAnchor="end"
                    fontSize="9"
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
                  fontSize="9"
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

export default RequestHistorySummaryChart;
