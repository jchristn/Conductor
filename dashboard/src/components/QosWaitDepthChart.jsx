import React, { useRef, useState } from 'react';
import ChartCopyButton from './ChartCopyButton';
import useClampedTooltip from '../hooks/useClampedTooltip';
import { computeNiceTicks, computeXLabelIndices, cssColor } from '../utils/chartHelpers';

// Two panels share one time axis (small multiples, one Y scale each): queue wait per class as lines on
// top, and peak queue depth per class as stacked bars below.
const CHART_WIDTH = 800;
const CHART_HEIGHT = 380;
const PADDING_LEFT = 56;
const PADDING_RIGHT = 20;
const WAIT_TOP = 26;
const WAIT_HEIGHT = 170;
const DEPTH_TOP = 238;
const DEPTH_HEIGHT = 96;
const X_LABEL_Y = CHART_HEIGHT - 14;
const INNER_WIDTH = CHART_WIDTH - PADDING_LEFT - PADDING_RIGHT;

// Categorical class colors, assigned by the class's position in the stable, sorted class list so a
// class keeps its color when others are hidden. Values are validated for both themes (see index.css).
const CLASS_COLOR_COUNT = 8;
const CLASS_COLOR_FALLBACKS = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#4a3aa7', '#e34948'];

/**
 * CSS color for the class at a position in the stable class list.
 * @param {number} index - position of the class in the sorted class list.
 * @returns {string} a CSS var() reference.
 */
export function qosClassColor(index) {
  return `var(--qos-series-${(index % CLASS_COLOR_COUNT) + 1})`;
}

function resolvedClassColor(index) {
  const slot = index % CLASS_COLOR_COUNT;
  return cssColor(`--qos-series-${slot + 1}`, CLASS_COLOR_FALLBACKS[slot]);
}

/**
 * Format a millisecond duration for axes and tooltips.
 * @param {number|null|undefined} ms - duration in milliseconds.
 * @returns {string} e.g. "0 ms", "850 ms", "1.2 s", "2.5 min", or "-" when missing.
 */
export function formatWaitMs(ms) {
  if (ms == null || Number.isNaN(ms)) return '-';
  if (ms < 1000) return `${Math.round(ms)} ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(ms < 10_000 ? 1 : 0)} s`;
  return `${(ms / 60_000).toFixed(1)} min`;
}

function formatChartLabel(timestamp, stepMs) {
  const date = new Date(timestamp);
  if (stepMs >= 900_000) {
    return date.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }
  return date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

function buildLinePath(points) {
  // Break the line where a bucket had no activity for the class, rather than drawing it through zero.
  let path = '';
  let drawing = false;
  points.forEach((point) => {
    if (!point) {
      drawing = false;
      return;
    }
    path += `${drawing ? 'L' : 'M'}${point.x.toFixed(1)},${point.y.toFixed(1)} `;
    drawing = true;
  });
  return path.trim();
}

/**
 * Queue wait and depth per traffic class over time.
 * @param {{ buckets: Array<{timestampMs:number, classes:Object<string,{averageWaitMs:number,maxWaitMs:number,peakWaiting:number,admitted:number}>}>,
 *   classNames: string[], range: {label:string, stepMs:number}, runnerName?: string, error?: string|null }} props
 */
function QosWaitDepthChart({ buckets, classNames, range, runnerName, error }) {
  const [hovered, setHovered] = useState(null);
  const [metric, setMetric] = useState('average');
  const [hiddenClasses, setHiddenClasses] = useState(() => new Set());
  const containerRef = useRef(null);
  const tooltipRef = useRef(null);
  const svgRef = useRef(null);
  const tooltipPos = useClampedTooltip(containerRef, tooltipRef, hovered);

  const rows = buckets || [];
  const allClasses = classNames || [];
  const visibleClasses = allClasses.filter((name) => !hiddenClasses.has(name));
  const waitKey = metric === 'max' ? 'maxWaitMs' : 'averageWaitMs';

  const maxWait = Math.max(0, ...rows.flatMap((row) => visibleClasses.map((name) => row.classes[name]?.[waitKey] || 0)));
  const waitTicks = computeNiceTicks(maxWait, 4);
  const waitMax = waitTicks[waitTicks.length - 1] || 1;

  const maxDepth = Math.max(0, ...rows.map((row) => visibleClasses.reduce((sum, name) => sum + (row.classes[name]?.peakWaiting || 0), 0)));
  const depthTicks = computeNiceTicks(maxDepth, 2);
  const depthMax = depthTicks[depthTicks.length - 1] || 1;

  const xLabelIndices = computeXLabelIndices(rows.length);
  const groupWidth = INNER_WIDTH / Math.max(rows.length, 1);
  const barWidth = Math.max(2, Math.min(40, groupWidth * 0.72));
  const centerX = (index) => PADDING_LEFT + index * groupWidth + groupWidth / 2;

  const toggleClass = (name) => {
    setHiddenClasses((current) => {
      const next = new Set(current);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  };

  const handleHover = (index, event) => {
    const containerRect = containerRef.current?.getBoundingClientRect();
    if (!containerRect) return;
    setHovered({ index, relX: event.clientX - containerRect.left, relY: event.clientY - containerRect.top });
  };

  const getCopyOptions = () => ({
    title: `QoS ${metric === 'max' ? 'Max' : 'Average'} Wait and Peak Depth by Class${runnerName ? ` - ${runnerName}` : ''} - ${range.label}`,
    xLabel: 'Time',
    yLabel: `${metric === 'max' ? 'Max' : 'Average'} wait (top) / peak waiting (bottom)`,
    legend: visibleClasses.map((name) => ({ label: name, color: resolvedClassColor(allClasses.indexOf(name)) }))
  });

  const hoveredRow = hovered ? rows[hovered.index] : null;
  const hasAnyData = rows.some((row) => Object.keys(row.classes).length > 0);

  return (
    <div className="dashboard-section request-history-chart-section">
      <div className="request-history-chart-header">
        <h2>Queue Wait and Depth by Class</h2>
        <div className="request-history-chart-controls">
          <div className="request-history-time-tabs" role="group" aria-label="Wait metric">
            <button
              type="button"
              className={'request-history-time-tab' + (metric === 'average' ? ' active' : '')}
              aria-pressed={metric === 'average'}
              onClick={() => setMetric('average')}
              title="Plot the average queue wait per class in each bucket"
            >
              Average wait
            </button>
            <button
              type="button"
              className={'request-history-time-tab' + (metric === 'max' ? ' active' : '')}
              aria-pressed={metric === 'max'}
              onClick={() => setMetric('max')}
              title="Plot the longest queue wait per class in each bucket"
            >
              Max wait
            </button>
          </div>
          <ChartCopyButton svgRef={svgRef} getOptions={getCopyOptions} />
        </div>
      </div>

      {allClasses.length > 0 && (
        <div className="qos-monitor-class-toggles" role="group" aria-label="Show or hide traffic classes">
          {allClasses.map((name, index) => {
            const visible = !hiddenClasses.has(name);
            return (
              <button
                key={name}
                type="button"
                className={'qos-monitor-class-toggle' + (visible ? '' : ' off')}
                aria-pressed={visible}
                onClick={() => toggleClass(name)}
                title={visible ? `Hide class ${name}` : `Show class ${name}`}
              >
                <span className="request-history-legend-color" style={{ backgroundColor: qosClassColor(index) }} />
                {name}
              </button>
            );
          })}
        </div>
      )}

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
            aria-label={`${metric === 'max' ? 'Max' : 'Average'} queue wait and peak queue depth per traffic class for the ${range.label.toLowerCase()}`}
          >
            <text x={PADDING_LEFT} y={WAIT_TOP - 12} fontSize="10" fontWeight="600" fill="var(--text-secondary)">
              {metric === 'max' ? 'Max wait' : 'Average wait'}
            </text>
            {waitTicks.map((tick) => {
              const y = WAIT_TOP + WAIT_HEIGHT - (tick / waitMax) * WAIT_HEIGHT;
              return (
                <g key={`wait-${tick}`}>
                  <line x1={PADDING_LEFT} y1={y} x2={CHART_WIDTH - PADDING_RIGHT} y2={y} stroke="var(--border-color)" strokeDasharray={tick === 0 ? 'none' : '4,4'} strokeWidth={0.75} />
                  <text x={PADDING_LEFT - 10} y={y + 3} textAnchor="end" fontSize="9" fill="var(--text-secondary)">{formatWaitMs(tick)}</text>
                </g>
              );
            })}

            <text x={PADDING_LEFT} y={DEPTH_TOP - 12} fontSize="10" fontWeight="600" fill="var(--text-secondary)">
              Peak waiting
            </text>
            {depthTicks.map((tick) => {
              const y = DEPTH_TOP + DEPTH_HEIGHT - (tick / depthMax) * DEPTH_HEIGHT;
              return (
                <g key={`depth-${tick}`}>
                  <line x1={PADDING_LEFT} y1={y} x2={CHART_WIDTH - PADDING_RIGHT} y2={y} stroke="var(--border-color)" strokeDasharray={tick === 0 ? 'none' : '4,4'} strokeWidth={0.75} />
                  <text x={PADDING_LEFT - 10} y={y + 3} textAnchor="end" fontSize="9" fill="var(--text-secondary)">{tick}</text>
                </g>
              );
            })}

            {hovered && (
              <line
                x1={centerX(hovered.index)}
                x2={centerX(hovered.index)}
                y1={WAIT_TOP}
                y2={DEPTH_TOP + DEPTH_HEIGHT}
                stroke="var(--text-secondary)"
                strokeWidth={0.75}
                strokeDasharray="2,3"
              />
            )}

            {/* Peak depth: stacked bars per class */}
            {rows.map((row, index) => {
              const x = PADDING_LEFT + index * groupWidth + (groupWidth - barWidth) / 2;
              let cursorY = DEPTH_TOP + DEPTH_HEIGHT;
              return (
                <g key={`depth-${row.timestampMs}`}>
                  {visibleClasses.map((name) => {
                    const value = row.classes[name]?.peakWaiting || 0;
                    if (value <= 0) return null;
                    const height = (value / depthMax) * DEPTH_HEIGHT;
                    cursorY -= height;
                    return (
                      <rect
                        key={name}
                        x={x}
                        y={cursorY}
                        width={barWidth}
                        height={height}
                        rx={2}
                        fill={qosClassColor(allClasses.indexOf(name))}
                        stroke="var(--card-bg)"
                        strokeWidth={height > 3 ? 1 : 0}
                        opacity={hovered?.index === index ? 1 : 0.85}
                      />
                    );
                  })}
                </g>
              );
            })}

            {/* Wait: one line per class, broken where the class had no activity */}
            {visibleClasses.map((name) => {
              const color = qosClassColor(allClasses.indexOf(name));
              const points = rows.map((row, index) => {
                const bucket = row.classes[name];
                if (!bucket) return null;
                const value = bucket[waitKey] || 0;
                return { x: centerX(index), y: WAIT_TOP + WAIT_HEIGHT - (value / waitMax) * WAIT_HEIGHT };
              });
              const isolated = points.filter((point, index) => point && !points[index - 1] && !points[index + 1]);
              const hoveredPoint = hovered ? points[hovered.index] : null;
              return (
                <g key={`wait-${name}`}>
                  <path d={buildLinePath(points)} fill="none" stroke={color} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
                  {isolated.map((point) => (
                    <circle key={`${point.x}`} cx={point.x} cy={point.y} r={3} fill={color} />
                  ))}
                  {hoveredPoint && (
                    <circle cx={hoveredPoint.x} cy={hoveredPoint.y} r={4} fill={color} stroke="var(--card-bg)" strokeWidth={2} />
                  )}
                </g>
              );
            })}

            {/* Hit areas across both panels */}
            {rows.map((row, index) => (
              <rect
                key={`hit-${row.timestampMs}`}
                x={PADDING_LEFT + index * groupWidth}
                y={WAIT_TOP}
                width={groupWidth}
                height={DEPTH_TOP + DEPTH_HEIGHT - WAIT_TOP}
                fill="transparent"
                onMouseEnter={(event) => handleHover(index, event)}
                onMouseMove={(event) => handleHover(index, event)}
                onMouseLeave={() => setHovered(null)}
              />
            ))}

            {xLabelIndices.map((index) => {
              const anchor = index === 0 ? 'start' : index === rows.length - 1 ? 'end' : 'middle';
              let x = centerX(index);
              if (anchor === 'start') x = PADDING_LEFT;
              if (anchor === 'end') x = CHART_WIDTH - PADDING_RIGHT;
              return (
                <text key={index} x={x} y={X_LABEL_Y} textAnchor={anchor} fontSize="9" fill="var(--text-secondary)">
                  {formatChartLabel(rows[index].timestampMs, range.stepMs)}
                </text>
              );
            })}

            {!hasAnyData && (
              <text x={PADDING_LEFT + INNER_WIDTH / 2} y={WAIT_TOP + WAIT_HEIGHT / 2} textAnchor="middle" fontSize="12" fill="var(--text-secondary)">
                No queue activity in this range
              </text>
            )}
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
              <div style={{ fontWeight: 600, marginBottom: 4 }}>
                {new Date(hoveredRow.timestampMs).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
              </div>
              {visibleClasses.filter((name) => hoveredRow.classes[name]).length === 0 && (
                <div className="text-muted">No activity</div>
              )}
              {visibleClasses.filter((name) => hoveredRow.classes[name]).map((name) => {
                const bucket = hoveredRow.classes[name];
                return (
                  <div key={name} className="qos-monitor-tooltip-row">
                    <span className="request-history-legend-color" style={{ backgroundColor: qosClassColor(allClasses.indexOf(name)) }} />
                    <span className="qos-monitor-tooltip-class">{name}</span>
                    <span>avg {formatWaitMs(bucket.averageWaitMs)} · max {formatWaitMs(bucket.maxWaitMs)} · peak {bucket.peakWaiting}</span>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default QosWaitDepthChart;
