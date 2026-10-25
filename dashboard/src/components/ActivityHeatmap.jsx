import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import ChartCopyButton from './ChartCopyButton';
import {
  HEATMAP_METRICS,
  HEATMAP_TIMEFRAMES,
  buildBuckets,
  cellLabel,
  heatLevel,
  layoutHeatmap,
  metricPeak,
  metricValue,
  normalizeMetric,
  normalizeTimeframe,
  summarize
} from '../utils/heatmap';
import './ActivityHeatmap.css';

const CELL = 14;
const GAP = 3;
const PITCH = CELL + GAP;
const TRUNCATION_LIMIT = 10000;
const pad2 = n => String(n).padStart(2, '0');

// Column header text for row layouts: clock time when rows start at UTC midnight, else a minute offset.
function columnLabel(layout, column) {
  const offsetSeconds = column * layout.bucketSeconds;
  if (layout.timeframe === 'week' || layout.timeframe === 'month') {
    return `${pad2(Math.floor(offsetSeconds / 3600))}:${pad2(Math.floor((offsetSeconds % 3600) / 60))}`;
  }
  return `+${offsetSeconds / 60}m`;
}

/**
 * UTC activity heat map for the Dashboard chart tabs. The timeframe (?heatmapRange=) and metric
 * (?heatmapMetric=) live in the query string. A timeframe change re-queries POST /v1.0/analytics/query;
 * a metric change re-renders from the cached response. Superseded responses are ignored.
 */
function ActivityHeatmap() {
  const { api } = useApp();
  const [searchParams, setSearchParams] = useSearchParams();
  const timeframeKey = normalizeTimeframe(searchParams.get('heatmapRange'));
  const metricKey = normalizeMetric(searchParams.get('heatmapMetric'));
  const timeframe = HEATMAP_TIMEFRAMES[timeframeKey];
  const metric = HEATMAP_METRICS[metricKey];

  // The result remembers which timeframe it belongs to and is reset whenever a query starts, so a new
  // timeframe shows loading (and drops the previous error) until its own response arrives.
  const [result, setResult] = useState({ timeframe: null, response: null, error: null });
  useEffect(() => {
    let cancelled = false;
    const { range, bucketSeconds } = HEATMAP_TIMEFRAMES[timeframeKey];
    setResult({ timeframe: null, response: null, error: null });
    api.queryAnalytics({ Range: range, BucketSeconds: bucketSeconds })
      .then(response => { if (!cancelled) setResult({ timeframe: timeframeKey, response: response || {}, error: null }); })
      .catch(error => { if (!cancelled) setResult({ timeframe: timeframeKey, response: null, error: error?.message || String(error) }); });
    return () => { cancelled = true; };
  }, [api, timeframeKey]);

  const current = result.timeframe === timeframeKey ? result : null;
  const response = current?.response || null;

  const buckets = useMemo(() => (response ? buildBuckets(response) : []), [response]);
  const layout = useMemo(() => layoutHeatmap(buckets, timeframeKey, response?.BucketSeconds), [buckets, timeframeKey, response]);
  const levels = useMemo(() => {
    const peak = metricPeak(buckets, metricKey);
    return new Map(buckets.map(bucket => [bucket.startMs, heatLevel(metricValue(bucket, metricKey), peak)]));
  }, [buckets, metricKey]);
  const summary = useMemo(() => summarize(buckets, metricKey), [buckets, metricKey]);
  const isEmpty = buckets.every(bucket => {
    const value = metricValue(bucket, metricKey);
    return value == null || value === 0;
  });

  // Real cells by grid position, for keyboard navigation that skips padding.
  const grid = useMemo(() => layout.rows.map(row => row.cells.map(cell => (cell.padding ? null : cell.key))), [layout]);
  const firstKey = useMemo(() => grid.flat().find(Boolean) || null, [grid]);
  const [activeKey, setActiveKey] = useState(null);
  const tabStopKey = activeKey && grid.some(row => row.includes(activeKey)) ? activeKey : firstKey;
  const [hoverKey, setHoverKey] = useState(null);
  const [focusKey, setFocusKey] = useState(null);
  const cellRefs = useRef(new Map());
  const svgRef = useRef(null);

  const setParam = (name, value) => {
    const next = new URLSearchParams(searchParams);
    next.set(name, value);
    setSearchParams(next);
  };

  const moveFocus = (event, key) => {
    // Escape dismisses the tooltip without moving focus (WCAG 1.4.13); the next hover or focus shows it again.
    if (event.key === 'Escape') {
      setHoverKey(null);
      setFocusKey(null);
      return;
    }
    const r = grid.findIndex(row => row.includes(key));
    if (r < 0) return;
    const c = grid[r].indexOf(key);
    let target = null;
    const scan = (dr, dc) => {
      for (let rr = r + dr, cc = c + dc; rr >= 0 && rr < grid.length && cc >= 0 && cc < grid[rr].length; rr += dr, cc += dc) {
        if (grid[rr][cc]) return grid[rr][cc];
      }
      return null;
    };
    if (event.key === 'ArrowRight') target = scan(0, 1);
    else if (event.key === 'ArrowLeft') target = scan(0, -1);
    else if (event.key === 'ArrowDown') target = scan(1, 0);
    else if (event.key === 'ArrowUp') target = scan(-1, 0);
    else if (event.key === 'Home') target = (event.ctrlKey ? grid.flat() : grid[r]).find(Boolean) || null;
    else if (event.key === 'End') target = (event.ctrlKey ? grid.flat() : grid[r]).filter(Boolean).pop() || null;
    else return;
    event.preventDefault();
    if (!target) return;
    setActiveKey(target);
    const el = cellRefs.current.get(target);
    if (el) el.focus();
  };

  const isCalendar = layout.kind === 'calendar';
  const left = isCalendar ? 36 : 80;
  const top = 18;
  const width = left + Math.max(1, layout.cellsPerRow) * PITCH;
  const height = top + Math.max(1, layout.rows.length) * PITCH;
  const columnStep = layout.cellsPerRow > 12 ? 3 : 1;

  const cellByKey = useMemo(() => {
    const map = new Map();
    layout.rows.forEach((row, r) => row.cells.forEach((cell, c) => { if (!cell.padding) map.set(cell.key, { cell, r, c }); }));
    return map;
  }, [layout]);
  const tipKey = hoverKey && cellByKey.has(hoverKey) ? hoverKey : focusKey && cellByKey.has(focusKey) ? focusKey : null;
  const tip = tipKey ? cellByKey.get(tipKey) : null;

  const gridLabel = `Activity heatmap, ${metric.label}, ${timeframe.label}`;
  const truncated = Number(response?.TotalRequests) >= TRUNCATION_LIMIT;

  return (
    <div className="dashboard-section activity-heatmap">
      <div className="request-history-chart-header">
        <h3>Activity heatmap</h3>
        {current?.response && !isEmpty && (
          <ChartCopyButton
            svgRef={svgRef}
            getOptions={() => ({ title: `Activity heatmap: ${metric.label}, ${timeframe.label} (UTC)`, xLabel: 'Bucket start (UTC)', yLabel: metric.label })}
          />
        )}
      </div>
      <div className="hm-controls">
        <label className="hm-control">
          <span>Timeframe</span>
          <select value={timeframeKey} onChange={event => setParam('heatmapRange', event.target.value)}>
            {Object.entries(HEATMAP_TIMEFRAMES).map(([key, value]) => <option key={key} value={key}>{value.label}</option>)}
          </select>
        </label>
        <label className="hm-control">
          <span>Metric</span>
          <select value={metricKey} onChange={event => setParam('heatmapMetric', event.target.value)}>
            {Object.entries(HEATMAP_METRICS).map(([key, value]) => <option key={key} value={key}>{value.label}</option>)}
          </select>
        </label>
      </div>

      {!current && <p role="status">Loading analytics…</p>}
      {current?.error != null && <p role="alert">Analytics unavailable: {current.error}</p>}
      {current?.response && (
        <>
          <div className="hm-summary">
            <div className="hm-tile">
              <span className="hm-tile-label">{summary.primary.label}</span>
              <span className="hm-tile-value">{summary.primary.text}</span>
            </div>
            <div className="hm-tile">
              <span className="hm-tile-label">Peak</span>
              <span className="hm-tile-value">{summary.peak.text}</span>
              {summary.peak.timeText && <span className="hm-tile-detail">at {summary.peak.timeText}</span>}
            </div>
            <div className="hm-tile">
              <span className="hm-tile-label">Active buckets</span>
              <span className="hm-tile-value">{summary.active.text}</span>
            </div>
          </div>
          {truncated && <p className="text-muted hm-note">Results may be truncated at 10,000 requests.</p>}
          {isEmpty ? (
            <p className="hm-empty" role="status">No activity for this time range</p>
          ) : (
            <>
              <p className="text-muted hm-note">All times are UTC.</p>
              <div className="hm-chart">
                <svg
                  ref={svgRef}
                  className="hm-svg"
                  viewBox={`0 0 ${width} ${height}`}
                  width={width}
                  height={height}
                  role="grid"
                  aria-label={gridLabel}
                >
                  <g aria-hidden="true">
                    {isCalendar
                      ? layout.columns.map((column, c) => (
                        <text key={column.key} className="hm-axis-label" x={left + c * PITCH} y={12}>{c % 2 === 0 ? column.label : ''}</text>
                      ))
                      : Array.from({ length: layout.cellsPerRow }, (_, c) => (c % columnStep === 0 ? (
                        <text key={`c${c}`} className="hm-axis-label" x={left + c * PITCH} y={12}>{columnLabel(layout, c)}</text>
                      ) : null))}
                  </g>
                  {layout.rows.map((row, r) => (
                    <g key={row.key} role="row">
                      <text className="hm-axis-label" aria-hidden="true" x={left - 6} y={top + r * PITCH + CELL - 3} textAnchor="end">{row.label}</text>
                      {row.cells.map((cell, c) => {
                        const x = left + c * PITCH;
                        const y = top + r * PITCH;
                        if (cell.padding) {
                          return <rect key={cell.key} className="hm-cell hm-padding" x={x} y={y} width={CELL} height={CELL} rx={2} aria-hidden="true" />;
                        }
                        const label = cellLabel(cell.bucket, metricKey, layout.bucketSeconds);
                        return (
                          <rect
                            key={cell.key}
                            ref={el => { if (el) cellRefs.current.set(cell.key, el); else cellRefs.current.delete(cell.key); }}
                            className={`hm-cell hm-level-${levels.get(cell.startMs) || 0}`}
                            x={x}
                            y={y}
                            width={CELL}
                            height={CELL}
                            rx={2}
                            role="gridcell"
                            aria-label={label}
                            tabIndex={cell.key === tabStopKey ? 0 : -1}
                            onMouseEnter={() => setHoverKey(cell.key)}
                            onMouseLeave={() => setHoverKey(null)}
                            onFocus={() => { setFocusKey(cell.key); setActiveKey(cell.key); }}
                            onBlur={() => setFocusKey(null)}
                            onKeyDown={event => moveFocus(event, cell.key)}
                          />
                        );
                      })}
                    </g>
                  ))}
                </svg>
                {tip && (
                  <div
                    className="hm-tooltip"
                    role="tooltip"
                    style={{ left: `${((left + tip.c * PITCH + CELL / 2) / width) * 100}%`, top: `${((top + tip.r * PITCH) / height) * 100}%` }}
                  >
                    {cellLabel(tip.cell.bucket, metricKey, layout.bucketSeconds)}
                  </div>
                )}
              </div>
              <div className="hm-legend">
                <span>Less</span>
                {[0, 1, 2, 3, 4].map(level => (
                  <svg key={level} className="hm-legend-swatch" width={CELL} height={CELL} viewBox={`0 0 ${CELL} ${CELL}`} aria-hidden="true">
                    <rect className={`hm-cell hm-level-${level}`} x={0.5} y={0.5} width={CELL - 1} height={CELL - 1} rx={2} />
                  </svg>
                ))}
                <span>More</span>
              </div>
            </>
          )}
        </>
      )}
    </div>
  );
}

export default ActivityHeatmap;
