import React, { useRef, useState } from 'react';
import ChartCopyButton from './ChartCopyButton';
import useClampedTooltip from '../hooks/useClampedTooltip';
import { chartSeries } from '../utils/analyticsSeries';
const colors = ['#2563eb', '#e87922', '#16a34a', '#9333ea'];
const formatValue = (value, label) => value == null ? 'Not reported' : /\(ms/.test(label) ? `${Math.round(value).toLocaleString()} ms` : value.toLocaleString();
export default function AnalyticsTrendChart({ data = [], mode = 'volume' }) {
  const svgRef = useRef(null);
  const containerRef = useRef(null);
  const tooltipRef = useRef(null);
  const [hovered, setHovered] = useState(null);
  const tooltipPos = useClampedTooltip(containerRef, tooltipRef, hovered);
  const handleHover = (index, event) => {
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect) return;
    setHovered({ index, relX: event.clientX - rect.left, relY: event.clientY - rect.top });
  };
  const series = chartSeries(data, mode);
  const title = mode === 'tokens' ? 'Token usage by type' : mode === 'latency' ? 'Response latency and time to first token' : 'Request volume and TTFT';
  const max = Math.max(1, ...series.filter(s => !s.axis).flatMap(s => s.values));
  const rightMax = Math.max(1, ...series.filter(s => s.axis).flatMap(s => s.values));
  const x = i => 65 + (i + 0.5) * 770 / Math.max(1, data.length);
  const y = (v, axis) => 260 - (v / (axis ? rightMax : max)) * 230;
  const band = 770 / Math.max(1, data.length);
  const hoveredBucket = hovered && data[hovered.index] ? data[hovered.index] : null;
  const counted = series.filter(s => !s.axis);
  return <div className="dashboard-section">
    <div className="request-history-chart-header"><h3>{title}</h3><ChartCopyButton svgRef={svgRef} getOptions={() => ({ title, xLabel: 'Bucket start (local time)', yLabel: mode === 'tokens' ? 'Tokens' : mode === 'latency' ? 'Milliseconds' : 'Requests; TTFT on right axis', legend: series.map((s, i) => ({ label: s.label, color: colors[i] })) })} /></div>
    {!data.length ? <p role="status">No analytics in this range.</p> : <>
      <div className="analytics-trend-chart-container" ref={containerRef} style={{ position: 'relative' }}>
      <svg ref={svgRef} viewBox="0 0 920 320" role="img" aria-label={title} style={{ width: '100%' }}>
        {[0, .25, .5, .75, 1].map(t => <g key={t}><line x1="60" x2="840" y1={260 - t * 230} y2={260 - t * 230} stroke="#94a3b8" opacity=".3"/><text x="55" y={264 - t * 230} textAnchor="end" fill="currentColor" fontSize="11">{Math.round(max * t)}</text>{mode === 'volume' && <text x="850" y={264 - t * 230} fill="currentColor" fontSize="11">{Math.round(rightMax * t)} ms</text>}</g>)}
        {series.map((s, si) => <g key={s.label}>{mode === 'tokens' || (mode === 'volume' && !s.axis) ? s.values.map((v, i) => v == null ? null : <rect key={i} x={x(i) + (si - series.length / 2) * Math.min(12, 120 / data.length)} y={y(v)} width={Math.max(1, Math.min(11, 110 / data.length))} height={260 - y(v)} fill={colors[si]}/>) : s.values.map((v, i) => v == null ? null : <g key={i}>{i > 0 && s.values[i - 1] != null && <line x1={x(i - 1)} y1={y(s.values[i - 1], s.axis)} x2={x(i)} y2={y(v, s.axis)} stroke={colors[si]} strokeWidth="2"/>}<circle cx={x(i)} cy={y(v, s.axis)} r="4" fill={colors[si]}/></g>)}</g>)}
        {data.map((b, i) => i % Math.max(1, Math.ceil(data.length / 6)) === 0 && <text key={i} x={x(i)} y="290" textAnchor="middle" fontSize="10" fill="currentColor">{new Date(b.TimestampUtc).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}</text>)}
        {hoveredBucket && <line x1={x(hovered.index)} x2={x(hovered.index)} y1="30" y2="260" stroke="#94a3b8" strokeDasharray="3 3" pointerEvents="none"/>}
        {data.map((b, i) => <rect key={`hover-${i}`} data-testid="analytics-hover-zone" x={65 + i * band} y="30" width={band} height="230" fill="transparent" onMouseEnter={event => handleHover(i, event)} onMouseMove={event => handleHover(i, event)} onMouseLeave={() => setHovered(null)}/>)}
      </svg>
      {hoveredBucket && <div ref={tooltipRef} className="request-history-chart-tooltip" role="tooltip" style={{ left: tooltipPos ? `${tooltipPos.left}px` : '-9999px', top: tooltipPos ? `${tooltipPos.top}px` : '-9999px', transform: 'none' }}>
        <div style={{ fontWeight: 600, marginBottom: 4 }}>{new Date(hoveredBucket.TimestampUtc).toLocaleString()}</div>
        {series.map((s, i) => <div key={s.label}><span style={{ color: colors[i] }}>{s.label}:</span> {formatValue(s.values[hovered.index], s.label)}</div>)}
        {mode !== 'latency' && <div style={{ marginTop: 4 }}>{mode === 'tokens' ? 'Total tokens' : 'Total requests'}: {counted.reduce((sum, s) => sum + (s.values[hovered.index] || 0), 0).toLocaleString()}</div>}
      </div>}
      </div>
      <div className="request-history-chart-legend">{series.map((s, i) => <span key={s.label} title={`${s.label}; each value describes a time bucket.`} style={{ color: colors[i], marginRight: 16 }}>{s.label}</span>)}</div>
      {mode === 'tokens' && <p className="text-muted">Cached input is separated when reported; otherwise the input series includes all reported input. Unreported usage is unknown, not zero. Multimodal counts may overlap input/output and are not added again.</p>}
    </>}
  </div>;
}
