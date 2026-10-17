import React, { useRef } from 'react';
import ChartCopyButton from './ChartCopyButton';
import { chartSeries } from '../utils/analyticsSeries';
const colors = ['#2563eb', '#e87922', '#16a34a', '#9333ea'];
export default function AnalyticsTrendChart({ data = [], mode = 'volume' }) {
  const svgRef = useRef(null);
  const series = chartSeries(data, mode);
  const title = mode === 'tokens' ? 'Token usage by type' : mode === 'latency' ? 'Response latency and time to first token' : 'Request volume and TTFT';
  const max = Math.max(1, ...series.filter(s => !s.axis).flatMap(s => s.values));
  const rightMax = Math.max(1, ...series.filter(s => s.axis).flatMap(s => s.values));
  const x = i => 65 + (i + 0.5) * 770 / Math.max(1, data.length);
  const y = (v, axis) => 260 - (v / (axis ? rightMax : max)) * 230;
  return <div className="dashboard-section">
    <div className="request-history-chart-header"><h3>{title}</h3><ChartCopyButton svgRef={svgRef} getOptions={() => ({ title, xLabel: 'Bucket start (local time)', yLabel: mode === 'tokens' ? 'Tokens' : mode === 'latency' ? 'Milliseconds' : 'Requests; TTFT on right axis', legend: series.map((s, i) => ({ label: s.label, color: colors[i] })) })} /></div>
    {!data.length ? <p role="status">No analytics in this range.</p> : <>
      <svg ref={svgRef} viewBox="0 0 920 320" role="img" aria-label={title} style={{ width: '100%' }}>
        <title>{title}</title>
        {[0, .25, .5, .75, 1].map(t => <g key={t}><line x1="60" x2="840" y1={260 - t * 230} y2={260 - t * 230} stroke="#94a3b8" opacity=".3"/><text x="55" y={264 - t * 230} textAnchor="end" fill="currentColor" fontSize="11">{Math.round(max * t)}</text>{mode === 'volume' && <text x="850" y={264 - t * 230} fill="currentColor" fontSize="11">{Math.round(rightMax * t)} ms</text>}</g>)}
        {series.map((s, si) => <g key={s.label}>{mode === 'tokens' || (mode === 'volume' && !s.axis) ? s.values.map((v, i) => v == null ? null : <rect key={i} x={x(i) + (si - series.length / 2) * Math.min(12, 120 / data.length)} y={y(v)} width={Math.max(1, Math.min(11, 110 / data.length))} height={260 - y(v)} fill={colors[si]}><title>{`${new Date(data[i].TimestampUtc).toLocaleString()}: ${s.label}: ${v.toLocaleString()}`}</title></rect>) : s.values.map((v, i) => v == null ? null : <g key={i}>{i > 0 && s.values[i - 1] != null && <line x1={x(i - 1)} y1={y(s.values[i - 1], s.axis)} x2={x(i)} y2={y(v, s.axis)} stroke={colors[si]} strokeWidth="2"/>}<circle cx={x(i)} cy={y(v, s.axis)} r="4" fill={colors[si]}><title>{`${new Date(data[i].TimestampUtc).toLocaleString()}: ${s.label}: ${v}`}</title></circle></g>)}</g>)}
        {data.map((b, i) => i % Math.max(1, Math.ceil(data.length / 6)) === 0 && <text key={i} x={x(i)} y="290" textAnchor="middle" fontSize="10" fill="currentColor">{new Date(b.TimestampUtc).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}</text>)}
      </svg>
      <div className="request-history-chart-legend">{series.map((s, i) => <span key={s.label} title={`${s.label}; each value describes a time bucket.`} style={{ color: colors[i], marginRight: 16 }}>{s.label}</span>)}</div>
      {mode === 'tokens' && <p className="text-muted">Cached input is separated when reported; otherwise the input series includes all reported input. Unreported usage is unknown, not zero. Multimodal counts may overlap input/output and are not added again.</p>}
    </>}
  </div>;
}
