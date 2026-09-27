import React, { useRef, useState } from 'react';
import ChartCopyButton from './ChartCopyButton';
import useClampedTooltip from '../hooks/useClampedTooltip';
import { computeXLabelIndices, computeYTicks, cssColor } from '../utils/chartHelpers';

// Chart geometry (SVG viewBox units). The SVG scales to fill its container width.
const CHART_WIDTH = 800;
const CHART_HEIGHT = 240;
const PADDING_LEFT = 48;
const PADDING_RIGHT = 20;
const PADDING_TOP = 16;
const PADDING_BOTTOM = 40;
const INNER_WIDTH = CHART_WIDTH - PADDING_LEFT - PADDING_RIGHT;
const INNER_HEIGHT = CHART_HEIGHT - PADDING_TOP - PADDING_BOTTOM;

// Stacked from the baseline up. "Admitted" excludes requests that later hit an endpoint slot timeout,
// which are drawn as their own segment, so the stack height equals the admission decisions in the bucket.
export const QOS_OUTCOME_SERIES = [
  { key: 'served', label: 'Admitted', color: 'var(--success-color)', cssVar: '--success-color', fallback: '#10b981' },
  { key: 'endpointSlotTimeouts', label: 'Admitted, then endpoint slot timeout', color: 'var(--qos-slot-timeout-color)', cssVar: '--qos-slot-timeout-color', fallback: '#8b5cf6' },
  { key: 'rejected', label: 'Rejected', color: 'var(--danger-color)', cssVar: '--danger-color', fallback: '#ef4444' },
  { key: 'timedOut', label: 'Timed out in queue', color: 'var(--warning-color)', cssVar: '--warning-color', fallback: '#f59e0b' },
  { key: 'aborted', label: 'Aborted by client', color: 'var(--qos-aborted-color)', cssVar: '--qos-aborted-color', fallback: '#94a3b8' }
];

function formatChartLabel(timestamp, stepMs) {
  const date = new Date(timestamp);
  if (stepMs >= 900_000) {
    return date.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }
  return date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

function formatTooltipTimestamp(timestamp) {
  return new Date(timestamp).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

/**
 * Stacked bar chart of QoS admission outcomes per time bucket.
 * @param {{ buckets: Array<{timestampMs:number, admitted:number, rejected:number, timedOut:number, aborted:number, endpointSlotTimeouts:number}>,
 *   range: {label:string, stepMs:number}, runnerName?: string, error?: string|null }} props
 */
function QosAdmissionsChart({ buckets, range, runnerName, error }) {
  const [hovered, setHovered] = useState(null);
  const containerRef = useRef(null);
  const tooltipRef = useRef(null);
  const svgRef = useRef(null);
  const tooltipPos = useClampedTooltip(containerRef, tooltipRef, hovered);

  const rows = (buckets || []).map((bucket) => ({
    ...bucket,
    served: Math.max(0, bucket.admitted - bucket.endpointSlotTimeouts)
  }));

  const stackTotal = (row) => QOS_OUTCOME_SERIES.reduce((sum, series) => sum + (row[series.key] || 0), 0);
  const maxCount = Math.max(1, ...rows.map(stackTotal));
  const yTicks = computeYTicks(maxCount);
  const yMax = yTicks[yTicks.length - 1] || 1;
  const xLabelIndices = computeXLabelIndices(rows.length);
  const barGroupWidth = INNER_WIDTH / Math.max(rows.length, 1);
  const barWidth = Math.max(2, Math.min(40, barGroupWidth * 0.72));

  const totals = rows.reduce((acc, row) => {
    acc.admitted += row.admitted;
    acc.rejected += row.rejected;
    acc.timedOut += row.timedOut;
    acc.aborted += row.aborted;
    acc.endpointSlotTimeouts += row.endpointSlotTimeouts;
    return acc;
  }, { admitted: 0, rejected: 0, timedOut: 0, aborted: 0, endpointSlotTimeouts: 0 });

  const handleHover = (index, event) => {
    const containerRect = containerRef.current?.getBoundingClientRect();
    if (!containerRect) return;
    setHovered({ index, relX: event.clientX - containerRect.left, relY: event.clientY - containerRect.top });
  };

  const getCopyOptions = () => ({
    title: `QoS Admissions${runnerName ? ` - ${runnerName}` : ''} - ${range.label}`,
    xLabel: 'Time',
    yLabel: 'Requests',
    legend: QOS_OUTCOME_SERIES.map((series) => ({ label: series.label, color: cssColor(series.cssVar, series.fallback) }))
  });

  const hoveredRow = hovered ? rows[hovered.index] : null;

  return (
    <div className="dashboard-section request-history-chart-section">
      <div className="request-history-chart-header">
        <h2>Admissions Over Time</h2>
        <div className="request-history-chart-controls">
          <ChartCopyButton svgRef={svgRef} getOptions={getCopyOptions} />
        </div>
      </div>

      <div className="request-history-chart-stats qos-monitor-chart-stats">
        <div className="request-history-stat">
          <span className="request-history-stat-value" style={{ color: 'var(--success-color)' }}>{totals.admitted.toLocaleString()}</span>
          <span className="request-history-stat-label">Admitted</span>
        </div>
        <div className="request-history-stat">
          <span className="request-history-stat-value" style={{ color: 'var(--danger-color)' }}>{totals.rejected.toLocaleString()}</span>
          <span className="request-history-stat-label">Rejected</span>
        </div>
        <div className="request-history-stat">
          <span className="request-history-stat-value" style={{ color: 'var(--warning-color)' }}>{totals.timedOut.toLocaleString()}</span>
          <span className="request-history-stat-label">Timed Out</span>
        </div>
        <div className="request-history-stat">
          <span className="request-history-stat-value">{totals.aborted.toLocaleString()}</span>
          <span className="request-history-stat-label">Aborted</span>
        </div>
        <div className="request-history-stat">
          <span className="request-history-stat-value" style={{ color: 'var(--qos-slot-timeout-color)' }}>{totals.endpointSlotTimeouts.toLocaleString()}</span>
          <span className="request-history-stat-label">Slot Timeouts</span>
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
            role="img"
            aria-label={`QoS admission outcomes per bucket for the ${range.label.toLowerCase()}: ${totals.admitted} admitted, ${totals.rejected} rejected, ${totals.timedOut} timed out, ${totals.aborted} aborted, ${totals.endpointSlotTimeouts} endpoint slot timeouts`}
          >
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
                  <text x={PADDING_LEFT - 10} y={y + 3} textAnchor="end" fontSize="9" fill="var(--text-secondary)">
                    {tick}
                  </text>
                </g>
              );
            })}

            {rows.map((row, index) => {
              const x = PADDING_LEFT + index * barGroupWidth + (barGroupWidth - barWidth) / 2;
              const isHovered = hovered?.index === index;
              let cursorY = PADDING_TOP + INNER_HEIGHT;
              return (
                <g
                  key={row.timestampMs}
                  onMouseEnter={(event) => handleHover(index, event)}
                  onMouseMove={(event) => handleHover(index, event)}
                  onMouseLeave={() => setHovered(null)}
                >
                  <rect
                    x={PADDING_LEFT + index * barGroupWidth}
                    y={PADDING_TOP}
                    width={barGroupWidth}
                    height={INNER_HEIGHT}
                    fill="transparent"
                  />
                  {QOS_OUTCOME_SERIES.map((series) => {
                    const value = row[series.key] || 0;
                    if (value <= 0) return null;
                    const height = (value / yMax) * INNER_HEIGHT;
                    cursorY -= height;
                    return (
                      <rect
                        key={series.key}
                        x={x}
                        y={cursorY}
                        width={barWidth}
                        height={height}
                        rx={2}
                        fill={series.color}
                        stroke="var(--card-bg)"
                        strokeWidth={height > 3 ? 1 : 0}
                        opacity={isHovered ? 1 : 0.85}
                      />
                    );
                  })}
                </g>
              );
            })}

            {xLabelIndices.map((index) => {
              const anchor = index === 0 ? 'start' : index === rows.length - 1 ? 'end' : 'middle';
              let x = PADDING_LEFT + index * barGroupWidth + barGroupWidth / 2;
              if (anchor === 'start') x = PADDING_LEFT;
              if (anchor === 'end') x = CHART_WIDTH - PADDING_RIGHT;
              return (
                <text key={index} x={x} y={CHART_HEIGHT - 14} textAnchor={anchor} fontSize="9" fill="var(--text-secondary)">
                  {formatChartLabel(rows[index].timestampMs, range.stepMs)}
                </text>
              );
            })}
          </svg>

          {hoveredRow && (
            <div
              ref={tooltipRef}
              className="request-history-chart-tooltip"
              style={{
                left: tooltipPos ? `${tooltipPos.left}px` : '-9999px',
                top: tooltipPos ? `${tooltipPos.top}px` : '-9999px',
                transform: 'none'
              }}
            >
              <div style={{ fontWeight: 600, marginBottom: 4 }}>{formatTooltipTimestamp(hoveredRow.timestampMs)}</div>
              <div><span style={{ color: 'var(--success-color)' }}>Admitted:</span> {hoveredRow.admitted.toLocaleString()}</div>
              <div><span style={{ color: 'var(--qos-slot-timeout-color)' }}>Endpoint slot timeouts:</span> {hoveredRow.endpointSlotTimeouts.toLocaleString()}</div>
              <div><span style={{ color: 'var(--danger-color)' }}>Rejected:</span> {hoveredRow.rejected.toLocaleString()}</div>
              <div><span style={{ color: 'var(--warning-color)' }}>Timed out:</span> {hoveredRow.timedOut.toLocaleString()}</div>
              <div><span style={{ color: 'var(--qos-aborted-color)' }}>Aborted:</span> {hoveredRow.aborted.toLocaleString()}</div>
              <div>Total decisions: {(hoveredRow.admitted + hoveredRow.rejected + hoveredRow.timedOut + hoveredRow.aborted).toLocaleString()}</div>
            </div>
          )}
        </div>
      )}

      <div className="request-history-chart-legend qos-monitor-legend">
        {QOS_OUTCOME_SERIES.map((series) => (
          <span className="request-history-legend-item" key={series.key}>
            <span className="request-history-legend-color" style={{ backgroundColor: series.color }} />
            {series.label}
          </span>
        ))}
      </div>
    </div>
  );
}

export default QosAdmissionsChart;
