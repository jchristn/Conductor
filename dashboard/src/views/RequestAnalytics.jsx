import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useApp } from '../context/AppContext';
import Modal from '../components/Modal';
import CopyableId from '../components/CopyableId';

const RANGE_OPTIONS = [
  { label: 'Last Hour', value: 'lastHour', tooltip: 'Filter every analytics panel to requests created during the last hour.' },
  { label: 'Last Day', value: 'lastDay', tooltip: 'Filter every analytics panel to requests created during the last day.' },
  { label: 'Last Week', value: 'lastWeek', tooltip: 'Filter every analytics panel to requests created during the last week.' },
  { label: 'Last Month', value: 'lastMonth', tooltip: 'Filter every analytics panel to requests created during the last month.' },
  { label: 'Custom', value: 'custom', tooltip: 'Choose a custom start and end time within the retained 30-day analytics window.' }
];

const GRANULARITY_OPTIONS = [
  { label: 'Auto', value: '', tooltip: 'Let the server choose the safest bucket size for the selected range.' },
  { label: '1m', value: '60', tooltip: 'Group analytics into one-minute buckets.' },
  { label: '15m', value: '900', tooltip: 'Group analytics into fifteen-minute buckets.' },
  { label: '1h', value: '3600', tooltip: 'Group analytics into one-hour buckets.' },
  { label: '6h', value: '21600', tooltip: 'Group analytics into six-hour buckets.' },
  { label: '1d', value: '86400', tooltip: 'Group analytics into one-day buckets.' }
];

const ANALYTICS_TABS = [
  { label: 'Overview', value: 'overview', tooltip: 'Summary, volume, TTFT, endpoint, user, and credential analytics.' },
  { label: 'Latency/TTFT', value: 'latency', tooltip: 'Time-to-first-token percentiles, coverage, stage breakdown, and slow request drill-down.' },
  { label: 'Tokens', value: 'tokens', tooltip: 'Prompt, completion, total, unknown usage, and estimate-only cost analytics.' },
  { label: 'Users', value: 'users', tooltip: 'User and credential analytics breakdowns.' },
  { label: 'Reliability/Access', value: 'reliability', tooltip: 'Failed, denied, rate-limited, and access-oriented analytics.' },
  { label: 'Reports', value: 'reports', tooltip: 'Saved Analytics workspace reports.' },
  { label: 'Exports', value: 'exports', tooltip: 'Export status, unavailable export formats, and active filter metadata.' }
];

const EXPORT_FORMATS = [
  { label: 'CSV', value: 'csv', tooltip: 'CSV export is deferred until export jobs and audit metadata are implemented.' },
  { label: 'JSON', value: 'json', tooltip: 'JSON export is deferred until export jobs and audit metadata are implemented.' },
  { label: 'Parquet', value: 'parquet', tooltip: 'Parquet export is deferred until export jobs and format-specific behavior are implemented.' },
  { label: 'PDF', value: 'pdf', tooltip: 'PDF export is deferred until export rendering and file retention are implemented.' },
  { label: 'Dashboard Link', value: 'dashboardLink', tooltip: 'Export-specific dashboard links are deferred; saved-report links are available in the Reports controls.' }
];

const PROVIDERS = ['', 'OpenAI', 'vLLM', 'Gemini', 'Ollama'];
const PAGE_TOOLTIP = 'Analytics workspace for time-to-first-token, token usage, estimated cost, users, endpoints, and request outcomes.';
const FILTER_SECTION_TOOLTIP = 'Filters applied to every metric, chart, table, and analytics detail on this page. Analytics data is retained for 30 days.';
const CHART_TOOLTIP = 'Combined time-series chart. Stacked bars show successful and failed request volume per bucket, and the line shows average time-to-first-token.';
const STAGE_BREAKDOWN_TOOLTIP = 'Aggregated analytics stages for the active filters. Each row compares total time spent in that stage across captured events.';
const SLOWEST_REQUESTS_TOOLTIP = 'Slowest individual requests matching the active filters. Click a row to inspect its per-stage analytics timeline.';
const ENDPOINT_SUMMARY_TOOLTIP = 'Endpoint-level analytics grouped by routed model endpoint for the active filters.';
const USER_BREAKDOWN_TOOLTIP = 'User-level analytics for the active filters, including TTFT, token usage, estimate-only cost, failures, denials, coverage, and last seen time.';
const CREDENTIAL_BREAKDOWN_TOOLTIP = 'Credential-level analytics for the active filters, including TTFT, token usage, estimate-only cost, failures, denials, coverage, and last seen time.';

function formatNumber(value) {
  if (value == null) return '-';
  return Number(value).toLocaleString();
}

function formatMs(value) {
  if (value == null) return '-';
  return `${Number(value).toLocaleString()} ms`;
}

function formatPercent(value) {
  if (value == null) return '-';
  return `${Number(value).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`;
}

function formatCost(value, currency) {
  if (value == null) return '-';
  const formatted = Number(value).toLocaleString(undefined, { maximumFractionDigits: 8 });
  return currency ? `${formatted} ${currency}` : formatted;
}

function formatDateTime(value) {
  if (!value) return '-';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '-' : date.toLocaleString();
}

function calculateWeightedCoverage(rows) {
  const visibleRows = rows || [];
  let weighted = 0;
  let total = 0;

  visibleRows.forEach(row => {
    const successCount = row.SuccessfulCompletionCount || 0;
    if (successCount > 0 && row.TimeToFirstTokenCoveragePercent != null) {
      weighted += successCount * Number(row.TimeToFirstTokenCoveragePercent);
      total += successCount;
    }
  });

  return total > 0 ? weighted / total : null;
}

function getInitialQueryParam(name, fallback = '') {
  if (typeof window === 'undefined') return fallback;
  const params = new URLSearchParams(window.location.search);
  return params.get(name) || fallback;
}

function getInitialRange() {
  const value = getInitialQueryParam('range', 'lastDay');
  return RANGE_OPTIONS.some(option => option.value === value) ? value : 'lastDay';
}

function getInitialTab() {
  const value = getInitialQueryParam('tab', 'overview');
  return ANALYTICS_TABS.some(option => option.value === value) ? value : 'overview';
}

function toDateTimeLocalInput(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (part) => String(part).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function dateTimeLocalToIso(value) {
  if (!value) return '';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toISOString();
}

function normalizeAnalyticsResult(result) {
  if (!result) return null;

  const timeSeries = (result.TimeSeries || []).map(bucket => ({
    ...bucket,
    SuccessCount: bucket.SuccessfulCompletionCount ?? bucket.SuccessCount ?? 0,
    FailureCount: bucket.FailedRequestCount ?? bucket.FailureCount ?? 0,
    P95DurationMs: bucket.AverageTimeToFirstTokenMs ?? bucket.P95DurationMs ?? null
  }));

  const endpointSummaries = (result.Groups || []).map(group => ({
    ModelEndpointGuid: group.Value,
    ModelEndpointName: group.Label || group.Value,
    ProviderName: '',
    RequestCount: group.RequestCount || 0,
    SuccessCount: group.SuccessfulCompletionCount || 0,
    FailureCount: Math.max(0, (group.RequestCount || 0) - (group.SuccessfulCompletionCount || 0)),
    AverageDurationMs: group.AverageTimeToFirstTokenMs,
    P95DurationMs: group.P95TimeToFirstTokenMs,
    TotalTokens: group.TotalTokens || 0,
    EstimatedCost: group.EstimatedCost,
    AverageTokensPerSecond: null
  }));

  return {
    ...result,
    SuccessCount: result.SuccessfulCompletionCount ?? result.SuccessCount ?? 0,
    FailureCount: result.FailedRequestCount ?? result.FailureCount ?? 0,
    P50DurationMs: result.P50TimeToFirstTokenMs ?? result.P50DurationMs ?? null,
    P95DurationMs: result.P95TimeToFirstTokenMs ?? result.P95DurationMs ?? null,
    P99DurationMs: result.P99TimeToFirstTokenMs ?? result.P99DurationMs ?? null,
    TimeSeries: timeSeries,
    EndpointSummaries: endpointSummaries,
    StageBreakdown: result.StageBreakdown || [],
    SlowestRequests: result.SlowestRequests || []
  };
}

function statusTone(status) {
  if (!status) return 'neutral';
  if (status >= 200 && status < 300) return 'success';
  if (status >= 400 && status < 500) return 'warning';
  if (status >= 500) return 'danger';
  return 'neutral';
}

function MetricCard({ label, value, sublabel, tooltip, valueTooltip, sublabelTooltip }) {
  const cardTooltip = tooltip || 'Summary metric calculated from request analytics matching the active filters.';

  return (
    <div className="analytics-metric-card" title={cardTooltip}>
      <span className="analytics-metric-label" title={cardTooltip}>{label}</span>
      <strong title={valueTooltip || cardTooltip}>{value}</strong>
      {sublabel && <span className="analytics-metric-sub" title={sublabelTooltip || cardTooltip}>{sublabel}</span>}
    </div>
  );
}

function TimeSeriesChart({ data }) {
  const [hovered, setHovered] = useState(null);
  const chartRef = useRef(null);
  const [chartWidth, setChartWidth] = useState(920);
  const buckets = data || [];
  const width = chartWidth;
  const height = 390;
  const padLeft = 52;
  const padRight = 18;
  const padTop = 24;
  const padBottom = 44;
  const innerWidth = width - padLeft - padRight;
  const innerHeight = height - padTop - padBottom;
  const maxRequests = Math.max(1, ...buckets.map(item => item.RequestCount || 0));
  const maxLatency = Math.max(1, ...buckets.map(item => item.P95DurationMs || 0));
  const requestAxisTicks = maxRequests <= 1
    ? [{ ratio: 0, label: 0 }, { ratio: 1, label: 1 }]
    : [0, 0.25, 0.5, 0.75, 1].map(tick => ({
      ratio: tick,
      label: Math.round(maxRequests * tick)
    }));
  const groupWidth = buckets.length > 0 ? innerWidth / buckets.length : innerWidth;
  const barWidth = Math.max(2, Math.min(24, groupWidth * 0.62));

  useEffect(() => {
    const updateWidth = () => {
      if (chartRef.current) {
        setChartWidth(Math.max(640, Math.round(chartRef.current.clientWidth || 920)));
      }
    };

    updateWidth();

    if (typeof ResizeObserver !== 'undefined') {
      const observer = new ResizeObserver(updateWidth);
      if (chartRef.current) {
        observer.observe(chartRef.current);
      }
      return () => observer.disconnect();
    }

    window.addEventListener('resize', updateWidth);
    return () => window.removeEventListener('resize', updateWidth);
  }, []);

  const points = buckets.map((bucket, index) => {
    const x = padLeft + index * groupWidth + groupWidth / 2;
    const y = padTop + innerHeight - ((bucket.P95DurationMs || 0) / maxLatency) * innerHeight;
    return `${x},${y}`;
  }).join(' ');

  return (
    <div className="analytics-chart-wrap" ref={chartRef} title={CHART_TOOLTIP}>
      {buckets.length < 1 ? (
        <div className="analytics-empty" title="No request analytics time buckets matched the active range and filters.">No analytics buckets in this range.</div>
      ) : (
        <>
          <svg className="analytics-chart" viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="xMidYMid meet" role="img" aria-label={CHART_TOOLTIP}>
            <title>{CHART_TOOLTIP}</title>
            {requestAxisTicks.map(tick => {
              const y = padTop + innerHeight - tick.ratio * innerHeight;
              return (
                <g key={tick.ratio}>
                  <title>Request-count grid line used to read stacked bar height against the highest-volume bucket in the active range.</title>
                  <line x1={padLeft} x2={width - padRight} y1={y} y2={y} />
                  <text x={padLeft - 8} y={y + 4} textAnchor="end">{tick.label}</text>
                </g>
              );
            })}
            {buckets.map((bucket, index) => {
              const totalHeight = ((bucket.RequestCount || 0) / maxRequests) * innerHeight;
              const failureHeight = ((bucket.FailureCount || 0) / maxRequests) * innerHeight;
              const successHeight = Math.max(0, totalHeight - failureHeight);
              const x = padLeft + index * groupWidth + (groupWidth - barWidth) / 2;
              const ySuccess = padTop + innerHeight - totalHeight;
              const yFailure = padTop + innerHeight - failureHeight;
              const showLabel = index % Math.max(1, Math.ceil(buckets.length / 8)) === 0;
              return (
                <g key={bucket.TimestampUtc || index} onMouseEnter={() => setHovered(index)} onMouseLeave={() => setHovered(null)}>
                  <title>One time bucket in the active range. Bars show successful and failed request volume, and the line shows P95 latency for this bucket.</title>
                  <rect x={padLeft + index * groupWidth} y={padTop} width={groupWidth} height={innerHeight + padBottom} fill="transparent" />
                  {successHeight > 0 && (
                    <rect x={x} y={ySuccess} width={barWidth} height={successHeight} rx="2" className="analytics-bar-success">
                      <title>Successful requests in this time bucket. Height is scaled against the highest request-count bucket in the chart.</title>
                    </rect>
                  )}
                  {failureHeight > 0 && (
                    <rect x={x} y={yFailure} width={barWidth} height={failureHeight} rx="2" className="analytics-bar-failure">
                      <title>Failed requests in this time bucket, including missing or 4xx/5xx HTTP status results.</title>
                    </rect>
                  )}
                  {showLabel && (
                    <text x={padLeft + index * groupWidth + groupWidth / 2} y={height - 10} textAnchor="middle">
                      <title>Bucket date label for the time-series x-axis.</title>
                      {new Date(bucket.TimestampUtc).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}
                    </text>
                  )}
                </g>
              );
            })}
            {points && (
              <polyline points={points} className="analytics-latency-line">
                <title>P95 latency trend across the displayed time buckets.</title>
              </polyline>
            )}
          </svg>
          {hovered != null && buckets[hovered] && (
            <div className="analytics-tooltip" style={{ left: `${((hovered + 0.5) / buckets.length) * 100}%` }} title="Point-in-time values for the bucket under the cursor: request count, failed requests, and P95 latency.">
              <strong title="Timestamp for the hovered analytics bucket">{new Date(buckets[hovered].TimestampUtc).toLocaleString()}</strong>
              <span title="Total requests recorded in the hovered analytics bucket">Requests: {formatNumber(buckets[hovered].RequestCount)}</span>
              <span title="Requests in the hovered bucket with missing or 4xx/5xx HTTP status results">Failed: {formatNumber(buckets[hovered].FailureCount)}</span>
              <span title="Average time-to-first-token for the hovered analytics bucket">Avg TTFT: {formatMs(buckets[hovered].P95DurationMs)}</span>
            </div>
          )}
        </>
      )}
    </div>
  );
}

function StageBreakdown({ stages }) {
  const max = Math.max(1, ...(stages || []).map(item => item.TotalDurationMs || 0));
  return (
    <div className="analytics-stage-list" title={STAGE_BREAKDOWN_TOOLTIP}>
      {(stages || []).length < 1 && <div className="analytics-empty" title="No per-stage request analytics events matched the active range and filters.">No stage events captured for this range.</div>}
      {(stages || []).map(stage => (
        <div className="analytics-stage-row" key={stage.StageKind} title="One analytics stage aggregated across filtered requests. The bar compares total captured duration for this stage against the slowest aggregate stage.">
          <div className="analytics-stage-label" title="Stage identity and aggregate event count for this request-processing phase.">
            <strong title="Human-readable name for the analytics stage.">{stage.StageName || stage.StageKind}</strong>
            <span title="Number of captured events for this stage and its 95th percentile duration.">{stage.Count} events / p95 {formatMs(stage.P95DurationMs)}</span>
          </div>
          <div className="analytics-stage-track" title="Relative total duration for this stage compared with the stage that consumed the most time in the active result set.">
            <div className="analytics-stage-fill" style={{ width: `${Math.max(2, ((stage.TotalDurationMs || 0) / max) * 100)}%` }} title="Filled bar showing this stage's share of the maximum aggregate stage duration." />
          </div>
          <span className="analytics-stage-total" title="Total captured duration for this stage across all matching analytics events.">{formatMs(stage.TotalDurationMs)}</span>
        </div>
      ))}
    </div>
  );
}

function AnalyticsStateBanner({ tone = 'info', title, children, onRetry }) {
  return (
    <div className={`analytics-state-banner ${tone}`} title={title || 'Analytics workspace state.'}>
      <span title={title || 'Analytics workspace state.'}>{children}</span>
      {onRetry && (
        <button className="btn-secondary" onClick={onRetry} title="Retry the analytics query with the active filters.">Retry</button>
      )}
    </div>
  );
}

function AnalyticsTabNav({ activeTab, onChange }) {
  return (
    <div className="analytics-workspace-tabs" role="tablist" aria-label="Analytics workspace sections" title="Analytics workspace section tabs.">
      {ANALYTICS_TABS.map(tab => (
        <button
          key={tab.value}
          type="button"
          role="tab"
          aria-selected={activeTab === tab.value}
          className={activeTab === tab.value ? 'active' : ''}
          onClick={() => onChange(tab.value)}
          title={tab.tooltip}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}

function SavedReportsPanel({ reports, selectedReportId, onSelect }) {
  return (
    <section className="dashboard-section" title="Saved Analytics workspace reports visible in the active scope.">
      <div className="request-history-chart-header" title="Saved reports panel header.">
        <h2 title="Saved Analytics workspace reports.">Saved Reports</h2>
      </div>
      <div className="detail-table-container" title="Saved Analytics workspace reports table.">
        <table className="detail-table" title="Saved Analytics workspace reports table.">
          <thead>
            <tr>
              <th title="Saved report name.">Name</th>
              <th title="Saved report scope.">Scope</th>
              <th title="Saved report range.">Range</th>
              <th title="Saved report grouping dimensions.">Group By</th>
              <th title="Saved report last update time.">Updated</th>
            </tr>
          </thead>
          <tbody>
            {(reports || []).length < 1 ? (
              <tr><td colSpan="5" className="detail-table-empty-cell" title="No saved Analytics reports are visible in the active scope.">No saved reports in this scope.</td></tr>
            ) : reports.map(report => {
              const query = report.Query || {};
              const selected = report.Id === selectedReportId;
              return (
                <tr key={report.Id} className="clickable-row" onClick={() => onSelect(report.Id)} title="Load this saved Analytics report into the workspace filters.">
                  <td title="Saved report name.">{selected ? `${report.Name} (selected)` : report.Name}</td>
                  <td title="Saved report global or tenant scope.">{report.Scope || (report.TenantId ? 'Tenant' : 'Global')}</td>
                  <td title="Saved report named range.">{query.Range || '-'}</td>
                  <td title="Saved report grouping dimensions.">{(query.GroupBy || []).join(', ') || '-'}</td>
                  <td title="Saved report last update timestamp.">{formatDateTime(report.LastUpdateUtc || report.CreatedUtc)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function ExportStatusPanel({ filters, overview, selectedReportId }) {
  const activeFilters = [
    filters.tenantId ? `tenant ${filters.tenantId}` : 'current scope',
    filters.range,
    filters.bucketSeconds ? `${filters.bucketSeconds}s buckets` : 'auto buckets',
    filters.vmrGuid ? `VMR ${filters.vmrGuid}` : null,
    filters.endpointGuid ? `endpoint ${filters.endpointGuid}` : null,
    filters.providerName ? `provider ${filters.providerName}` : null,
    filters.modelName ? `model ${filters.modelName}` : null,
    filters.requestorUserGuid ? `user ${filters.requestorUserGuid}` : null,
    filters.credentialGuid ? `credential ${filters.credentialGuid}` : null
  ].filter(Boolean);

  return (
    <section className="dashboard-section" title="Analytics export status and active filter metadata.">
      <div className="request-history-chart-header" title="Export status panel header.">
        <h2 title="Analytics export status.">Exports</h2>
      </div>
      <AnalyticsStateBanner tone="info" title="Export APIs are deferred for the first Analytics workspace release.">
        CSV, JSON, Parquet, PDF, and export-specific dashboard links are unavailable in this release. Saved-report dashboard links remain available after a report is saved.
      </AnalyticsStateBanner>
      <div className="analytics-export-format-grid" title="Unavailable export formats.">
        {EXPORT_FORMATS.map(format => (
          <button key={format.value} type="button" className="btn-secondary" disabled title={format.tooltip}>{format.label}</button>
        ))}
      </div>
      <div className="analytics-export-metadata" title="Metadata that future export jobs should persist for audit and reproducibility.">
        <span title="Applied query time range.">Range: {filters.range === 'custom' ? `${filters.startUtc || '-'} to ${filters.endUtc || '-'}` : filters.range}</span>
        <span title="Server-applied retention window.">Retention: {overview?.RetentionDays || 30} days</span>
        <span title="Returned analytics result scope.">Scope: {overview?.IsGlobalScope ? 'Global' : overview?.TenantId || 'Tenant'}</span>
        <span title="Filtered request count in the active analytics result.">Rows represented: {formatNumber(overview?.TotalRequests || 0)}</span>
        <span title="Current saved-report link state.">Dashboard link: {selectedReportId ? 'available from saved report controls' : 'save a report first'}</span>
        <span title="Active filter summary.">Filters: {activeFilters.join(' / ')}</span>
      </div>
    </section>
  );
}

function BreakdownIdentity({ row, fallback }) {
  const value = row?.Value != null ? String(row.Value) : fallback;
  const label = row?.Label != null ? String(row.Label) : value || fallback;
  const canCopy = value && !value.startsWith('(');

  return (
    <div className="analytics-breakdown-identity" title="Analytics group label and identifier.">
      <strong title="Analytics group display label.">{label || fallback}</strong>
      {canCopy ? (
        <CopyableId value={value} />
      ) : (
        <span className="text-muted" title="No concrete identifier was captured for this analytics group.">{value || fallback}</span>
      )}
    </div>
  );
}

function AnalyticsBreakdownTable({ title, tooltip, rows, loading, emptyMessage, identityFallback, costCurrency }) {
  const visibleRows = rows || [];

  return (
    <section className="dashboard-section" title={tooltip}>
      <div className="request-history-chart-header" title={`Header for ${title}.`}>
        <h2 title={tooltip}>{title}</h2>
        {loading && <span className="text-muted" title={`${title} is loading with the active filters.`}>Loading...</span>}
      </div>
      <div className="detail-table-container" title={tooltip}>
        <table className="detail-table analytics-breakdown-table" title={tooltip}>
          <thead>
            <tr>
              <th title="User or credential identity for this analytics group.">Identity</th>
              <th title="Total requests matching the active filters for this group.">Requests</th>
              <th title="Successful completion percentage and count for this group.">Success</th>
              <th title="Failed, denied, and rate-limited request counts for this group.">Fail / Deny / 429</th>
              <th title="Average time-to-first-token for successful completions in this group.">Avg TTFT</th>
              <th title="95th percentile time-to-first-token for successful completions in this group.">P95 TTFT</th>
              <th title="Provider-reported total tokens for successful completions in this group.">Tokens</th>
              <th title="Successful completions in this group without usable provider token metrics.">Unknown Usage</th>
              <th title="Estimate-only cost for this group using the supplied per-token unit cost.">Est. Cost</th>
              <th title="Percentage of successful completions in this group with measured time-to-first-token.">Coverage</th>
              <th title="Most recent request timestamp in this group.">Last Seen</th>
            </tr>
          </thead>
          <tbody>
            {visibleRows.length < 1 ? (
              <tr><td colSpan="11" className="detail-table-empty-cell" title={emptyMessage}>{emptyMessage}</td></tr>
            ) : visibleRows.map((row, index) => {
              const requestCount = row.RequestCount || 0;
              const successCount = row.SuccessfulCompletionCount || 0;
              const successRate = requestCount > 0 ? (successCount * 100) / requestCount : null;
              const failedCount = row.FailedRequestCount ?? Math.max(0, requestCount - successCount);
              const deniedCount = row.DeniedRequestCount || 0;
              const rateLimitedCount = row.RateLimitedRequestCount || 0;

              return (
                <tr key={`${row.Dimension || title}:${row.Value || index}`} title="One analytics group matching the active filters.">
                  <td title="User or credential label and identifier."><BreakdownIdentity row={row} fallback={identityFallback} /></td>
                  <td title="Total filtered requests for this group.">{formatNumber(requestCount)}</td>
                  <td title="Successful completions and success rate for this group.">{formatNumber(successCount)} / {formatPercent(successRate)}</td>
                  <td title="Failed, denied, and rate-limited request counts for this group.">{formatNumber(failedCount)} / {formatNumber(deniedCount)} / {formatNumber(rateLimitedCount)}</td>
                  <td title="Average time-to-first-token for successful completions in this group.">{formatMs(row.AverageTimeToFirstTokenMs)}</td>
                  <td title="95th percentile time-to-first-token for successful completions in this group.">{formatMs(row.P95TimeToFirstTokenMs)}</td>
                  <td title="Total provider-reported tokens for successful completions in this group.">{formatNumber(row.TotalTokens || 0)}</td>
                  <td title="Successful completions in this group where provider token usage was missing and is treated as unknown.">{formatNumber(row.UnknownTokenUsageCount || 0)}</td>
                  <td title="Estimate-only cost for this group.">{formatCost(row.EstimatedCost, costCurrency)}</td>
                  <td title="Time-to-first-token coverage for successful completions in this group.">{formatPercent(row.TimeToFirstTokenCoveragePercent)}</td>
                  <td title="Most recent request timestamp in this group.">{formatDateTime(row.LastSeenUtc)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function TimelineModal({ analytics, onClose }) {
  const events = analytics?.Events || [];
  const total = Math.max(1, ...events.map(item => item.DurationMs || 0));

  return (
    <Modal
      isOpen={Boolean(analytics)}
      onClose={onClose}
      title="Request Analytics"
      titleTooltip="Per-request analytics detail for the selected slow request."
      closeTooltip="Close request analytics detail and return to the overview."
      extraWide
    >
      <div className="analytics-modal" title="Per-request analytics detail for the selected slow request.">
        <table className="analytics-summary-table" title="Request identifiers and analytics coverage state for the selected request.">
          <tbody>
            <tr title="Request history entry that owns these analytics events">
              <th title="Request history record identifier.">ID</th>
              <td title="Request history record identifier. Use this to correlate with Request History detail and support logs."><CopyableId value={analytics?.RequestHistoryId} /></td>
            </tr>
            <tr title="Trace identifier that correlates this request with analytics events, logs, and provider metadata">
              <th title="Trace identifier for cross-system correlation.">Trace</th>
              <td title="Trace identifier that links this request to analytics events, logs, and provider metadata.">{analytics?.TraceId ? <CopyableId value={analytics.TraceId} /> : '-'}</td>
            </tr>
            <tr title="Whether detailed request analytics were captured for this request history entry">
              <th title="Analytics capture state for this request.">Coverage</th>
              <td title="Shows whether detailed analytics were captured, or the capture failure code when detailed analytics were unavailable.">{analytics?.AnalyticsCaptured ? 'Captured' : analytics?.AnalyticsFailureCode || 'Not captured'}</td>
            </tr>
          </tbody>
        </table>
        <div className="analytics-stage-list modal-list" title="Per-stage timeline for the selected request, ordered by the captured analytics event sequence.">
          {events.length < 1 && <div className="analytics-empty" title="No per-stage analytics events were retained for this selected request.">No analytics events were recorded for this request.</div>}
          {events.map(event => (
            <div
              className="analytics-stage-row"
              key={event.Id || `${event.Sequence}-${event.StageKind}`}
              title="One per-request analytics event. The bar compares this stage duration with the longest captured stage in the selected request."
            >
              <div className="analytics-stage-label" title="Stage identity, stable stage kind, and success state for this request-processing event.">
                <strong title="Human-readable name for this request analytics stage.">{event.StageName || event.StageKind}</strong>
                <span title="Stable analytics stage kind and whether this stage completed successfully.">{event.StageKind} / {event.Success ? 'success' : 'failed'}</span>
              </div>
              <div className="analytics-stage-track" title="Relative duration for this stage compared with the longest captured stage in this request.">
                <div className="analytics-stage-fill" style={{ width: `${Math.max(2, ((event.DurationMs || 0) / total) * 100)}%` }} title="Filled bar showing this stage duration relative to the request's longest stage." />
              </div>
              <span className="analytics-stage-total" title="Measured duration for this individual analytics stage.">{formatMs(event.DurationMs)}</span>
            </div>
          ))}
        </div>
      </div>
    </Modal>
  );
}

function RequestAnalytics() {
  const { api, setError, isAdmin, currentUser } = useApp();
  const hasGlobalAnalyticsScope = Boolean(isAdmin || currentUser?.IsAdmin);
  const loadedReportFromUrlRef = useRef(false);
  const overviewAbortRef = useRef(null);
  const overviewDebounceRef = useRef(null);
  const [range, setRange] = useState(getInitialRange);
  const [activeTab, setActiveTab] = useState(getInitialTab);
  const [customStartUtc, setCustomStartUtc] = useState(() => toDateTimeLocalInput(getInitialQueryParam('startUtc')));
  const [customEndUtc, setCustomEndUtc] = useState(() => toDateTimeLocalInput(getInitialQueryParam('endUtc')));
  const [tenantId, setTenantId] = useState(() => getInitialQueryParam('tenantId'));
  const [bucketSeconds, setBucketSeconds] = useState(() => getInitialQueryParam('bucketSeconds'));
  const [vmrGuid, setVmrGuid] = useState(() => getInitialQueryParam('vmrGuid'));
  const [endpointGuid, setEndpointGuid] = useState(() => getInitialQueryParam('endpointGuid'));
  const [providerName, setProviderName] = useState(() => getInitialQueryParam('providerName'));
  const [modelName, setModelName] = useState(() => getInitialQueryParam('modelName'));
  const [requestorUserGuid, setRequestorUserGuid] = useState(() => getInitialQueryParam('requestorUserGuid') || getInitialQueryParam('userId'));
  const [credentialGuid, setCredentialGuid] = useState(() => getInitialQueryParam('credentialGuid') || getInitialQueryParam('credentialId'));
  const [tokenUnitCost, setTokenUnitCost] = useState(() => getInitialQueryParam('tokenUnitCost'));
  const [savedReports, setSavedReports] = useState([]);
  const [selectedSavedReportId, setSelectedSavedReportId] = useState(() => getInitialQueryParam('analyticsReport'));
  const [savedReportName, setSavedReportName] = useState('');
  const [savedReportStatus, setSavedReportStatus] = useState('');
  const [savingReport, setSavingReport] = useState(false);
  const [tenants, setTenants] = useState([]);
  const [vmrs, setVmrs] = useState([]);
  const [endpoints, setEndpoints] = useState([]);
  const [overview, setOverview] = useState(null);
  const [userBreakdown, setUserBreakdown] = useState([]);
  const [credentialBreakdown, setCredentialBreakdown] = useState([]);
  const [analyticsNotice, setAnalyticsNotice] = useState(null);
  const [loading, setLoading] = useState(false);
  const [selectedAnalytics, setSelectedAnalytics] = useState(null);

  const customStartIso = useMemo(() => dateTimeLocalToIso(customStartUtc), [customStartUtc]);
  const customEndIso = useMemo(() => dateTimeLocalToIso(customEndUtc), [customEndUtc]);
  const filters = useMemo(() => ({
    tenantId: hasGlobalAnalyticsScope ? tenantId : '',
    range,
    startUtc: range === 'custom' ? customStartIso : '',
    endUtc: range === 'custom' ? customEndIso : '',
    bucketSeconds,
    vmrGuid,
    endpointGuid,
    providerName,
    modelName,
    requestorUserGuid,
    credentialGuid,
    tokenUnitCost,
    costCurrency: tokenUnitCost ? 'estimate' : '',
    groupBy: 'ModelRunnerEndpointId',
    limit: 10000
  }), [hasGlobalAnalyticsScope, tenantId, range, customStartIso, customEndIso, bucketSeconds, vmrGuid, endpointGuid, providerName, modelName, requestorUserGuid, credentialGuid, tokenUnitCost]);

  const fetchOverview = useCallback(async () => {
    if (overviewAbortRef.current) {
      overviewAbortRef.current.abort();
      overviewAbortRef.current = null;
    }

    if (range === 'custom' && (!filters.startUtc || !filters.endUtc)) {
      setOverview(null);
      setUserBreakdown([]);
      setCredentialBreakdown([]);
      setAnalyticsNotice(null);
      setLoading(false);
      return;
    }

    if (range === 'custom' && new Date(filters.startUtc) >= new Date(filters.endUtc)) {
      setOverview(null);
      setUserBreakdown([]);
      setCredentialBreakdown([]);
      setAnalyticsNotice({
        tone: 'warning',
        message: 'Choose a custom end time after the custom start time.'
      });
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    overviewAbortRef.current = controller;

    try {
      setLoading(true);
      setAnalyticsNotice(null);
      const [overviewResult, userResult, credentialResult] = await Promise.all([
        api.getAnalyticsSummary(filters, { signal: controller.signal }),
        api.getAnalyticsSummary({ ...filters, groupBy: 'RequestorUserId' }, { signal: controller.signal }),
        api.getAnalyticsSummary({ ...filters, groupBy: 'CredentialId' }, { signal: controller.signal })
      ]);
      setOverview(normalizeAnalyticsResult(overviewResult));
      setUserBreakdown((userResult?.Groups || []).slice(0, 100));
      setCredentialBreakdown((credentialResult?.Groups || []).slice(0, 100));
    } catch (err) {
      if (err?.name === 'AbortError') {
        return;
      }

      setError('Failed to fetch analytics: ' + err.message);
      setOverview(null);
      setUserBreakdown([]);
      setCredentialBreakdown([]);
      setAnalyticsNotice({
        tone: err?.status === 401 || err?.status === 403 ? 'warning' : 'danger',
        message: err?.status === 401 || err?.status === 403
          ? 'You do not have permission to view Analytics for this scope.'
          : `Analytics query failed: ${err.message}`
      });
    } finally {
      if (overviewAbortRef.current === controller) {
        overviewAbortRef.current = null;
        setLoading(false);
      }
    }
  }, [api, filters, range, setError]);

  useEffect(() => {
    if (overviewDebounceRef.current) {
      clearTimeout(overviewDebounceRef.current);
    }

    overviewDebounceRef.current = setTimeout(() => {
      fetchOverview();
    }, 250);

    return () => {
      if (overviewDebounceRef.current) {
        clearTimeout(overviewDebounceRef.current);
      }

      if (overviewAbortRef.current) {
        overviewAbortRef.current.abort();
        overviewAbortRef.current = null;
      }
    };
  }, [fetchOverview]);

  const handleRefreshOverview = useCallback(() => {
    if (overviewDebounceRef.current) {
      clearTimeout(overviewDebounceRef.current);
      overviewDebounceRef.current = null;
    }

    fetchOverview();
  }, [fetchOverview]);

  useEffect(() => {
    if (typeof window === 'undefined') return;

    const params = new URLSearchParams(window.location.search);
    const setOrDelete = (name, value) => {
      if (value) params.set(name, value);
      else params.delete(name);
    };

    setOrDelete('analyticsReport', selectedSavedReportId);
    setOrDelete('tab', activeTab === 'overview' ? '' : activeTab);
    setOrDelete('tenantId', hasGlobalAnalyticsScope ? tenantId : '');
    setOrDelete('range', range === 'lastDay' ? '' : range);
    setOrDelete('startUtc', range === 'custom' ? filters.startUtc : '');
    setOrDelete('endUtc', range === 'custom' ? filters.endUtc : '');
    setOrDelete('bucketSeconds', bucketSeconds);
    setOrDelete('vmrGuid', vmrGuid);
    setOrDelete('endpointGuid', endpointGuid);
    setOrDelete('providerName', providerName);
    setOrDelete('modelName', modelName);
    setOrDelete('requestorUserGuid', requestorUserGuid);
    setOrDelete('credentialGuid', credentialGuid);
    setOrDelete('tokenUnitCost', tokenUnitCost);

    const queryString = params.toString();
    const nextUrl = `${window.location.pathname}${queryString ? `?${queryString}` : ''}`;
    const currentUrl = `${window.location.pathname}${window.location.search}`;
    if (nextUrl !== currentUrl) {
      window.history.replaceState(null, '', nextUrl);
    }
  }, [hasGlobalAnalyticsScope, tenantId, range, filters.startUtc, filters.endUtc, bucketSeconds, vmrGuid, endpointGuid, providerName, modelName, requestorUserGuid, credentialGuid, tokenUnitCost, selectedSavedReportId, activeTab]);

  useEffect(() => {
    api.listVirtualModelRunners({ maxResults: 1000 }).then(result => setVmrs(result.Data || [])).catch(() => {});
    api.listModelRunnerEndpoints({ maxResults: 1000 }).then(result => setEndpoints(result.Data || [])).catch(() => {});
  }, [api]);

  useEffect(() => {
    if (!hasGlobalAnalyticsScope) {
      setTenants([]);
      setTenantId('');
      return;
    }

    api.listTenants({ maxResults: 1000 })
      .then(result => setTenants(result.Data || []))
      .catch((err) => setError('Failed to fetch tenants for analytics: ' + err.message));
  }, [api, hasGlobalAnalyticsScope, setError]);

  const fetchSavedReports = useCallback(async () => {
    try {
      const result = await api.listAnalyticsSavedReports({
        maxResults: 100,
        tenantId: hasGlobalAnalyticsScope ? tenantId : ''
      });
      setSavedReports(result.Data || []);
    } catch (err) {
      setError('Failed to fetch analytics saved reports: ' + err.message);
    }
  }, [api, hasGlobalAnalyticsScope, tenantId, setError]);

  useEffect(() => {
    fetchSavedReports();
  }, [fetchSavedReports]);

  const applySavedReport = useCallback((report) => {
    if (!report) return;

    const query = report.Query || {};
    const reportFilters = query.Filters || {};
    if (hasGlobalAnalyticsScope) {
      setTenantId(report.TenantId || query.TenantId || '');
    }
    setRange(query.Range || 'lastDay');
    setCustomStartUtc(toDateTimeLocalInput(query.StartUtc));
    setCustomEndUtc(toDateTimeLocalInput(query.EndUtc));
    setBucketSeconds(query.BucketSeconds ? String(query.BucketSeconds) : '');
    setTokenUnitCost(query.TokenUnitCost != null ? String(query.TokenUnitCost) : '');
    setVmrGuid((reportFilters.VirtualModelRunnerIds || [])[0] || '');
    setEndpointGuid((reportFilters.ModelRunnerEndpointIds || [])[0] || '');
    setProviderName((reportFilters.ProviderNames || [])[0] || '');
    setModelName((reportFilters.ModelNames || [])[0] || '');
    setRequestorUserGuid((reportFilters.RequestorUserIds || [])[0] || '');
    setCredentialGuid((reportFilters.CredentialIds || [])[0] || '');
    setSelectedSavedReportId(report.Id || '');
    setSavedReportName(report.Name || '');
    setSavedReportStatus(report.Name ? `Loaded ${report.Name}` : 'Loaded saved report');
  }, [hasGlobalAnalyticsScope]);

  useEffect(() => {
    if (loadedReportFromUrlRef.current) return;

    const params = new URLSearchParams(window.location.search);
    const reportId = params.get('analyticsReport');
    if (!reportId) {
      loadedReportFromUrlRef.current = true;
      return;
    }

    loadedReportFromUrlRef.current = true;
    api.getAnalyticsSavedReport(reportId, hasGlobalAnalyticsScope ? tenantId || null : null)
      .then(applySavedReport)
      .catch((err) => setError('Failed to load analytics saved report: ' + err.message));
  }, [api, applySavedReport, hasGlobalAnalyticsScope, tenantId, setError]);

  const buildSavedReportPayload = useCallback((name) => ({
    TenantId: hasGlobalAnalyticsScope ? tenantId || null : null,
    Name: name || 'Analytics report',
    Query: {
      TenantId: hasGlobalAnalyticsScope ? tenantId || null : null,
      Range: range,
      StartUtc: range === 'custom' ? customStartIso || null : null,
      EndUtc: range === 'custom' ? customEndIso || null : null,
      BucketSeconds: bucketSeconds ? Number(bucketSeconds) : null,
      TokenUnitCost: tokenUnitCost ? Number(tokenUnitCost) : null,
      CostCurrency: tokenUnitCost ? 'estimate' : null,
      GroupBy: ['ModelRunnerEndpointId'],
      Filters: {
        VirtualModelRunnerIds: vmrGuid ? [vmrGuid] : [],
        ModelRunnerEndpointIds: endpointGuid ? [endpointGuid] : [],
        ProviderNames: providerName ? [providerName] : [],
        ModelNames: modelName ? [modelName] : [],
        RequestorUserIds: requestorUserGuid ? [requestorUserGuid] : [],
        CredentialIds: credentialGuid ? [credentialGuid] : [],
        SuccessfulCompletionsOnly: true
      },
      Limit: 10000
    },
    DisplayState: {
      workspace: 'Analytics',
      chart: 'VolumeAndTtft'
    },
    Labels: ['analytics'],
    Tags: {
      range
    }
  }), [hasGlobalAnalyticsScope, tenantId, range, customStartIso, customEndIso, bucketSeconds, tokenUnitCost, vmrGuid, endpointGuid, providerName, modelName, requestorUserGuid, credentialGuid]);

  const handleSavedReportSelect = async (reportId) => {
    setSelectedSavedReportId(reportId);
    if (!reportId) {
      setSavedReportName('');
      setSavedReportStatus('');
      return;
    }

    const cached = savedReports.find(report => report.Id === reportId);
    if (cached) {
      applySavedReport(cached);
      return;
    }

    try {
      const report = await api.getAnalyticsSavedReport(reportId, hasGlobalAnalyticsScope ? tenantId || null : null);
      applySavedReport(report);
    } catch (err) {
      setError('Failed to load analytics saved report: ' + err.message);
    }
  };

  const handleSaveReport = async () => {
    const name = savedReportName.trim();
    if (!name) {
      setError('Saved report name is required.');
      return;
    }

    try {
      setSavingReport(true);
      const payload = buildSavedReportPayload(name);
      const saved = selectedSavedReportId
        ? await api.updateAnalyticsSavedReport(selectedSavedReportId, { ...payload, Id: selectedSavedReportId })
        : await api.createAnalyticsSavedReport(payload);
      setSelectedSavedReportId(saved.Id || selectedSavedReportId);
      setSavedReportName(saved.Name || name);
      setSavedReportStatus(saved.Name ? `Saved ${saved.Name}` : 'Saved report');
      await fetchSavedReports();
    } catch (err) {
      setError('Failed to save analytics report: ' + err.message);
    } finally {
      setSavingReport(false);
    }
  };

  const handleNewReport = () => {
    setSelectedSavedReportId('');
    setSavedReportName('');
    setSavedReportStatus('Ready to save a new report');
  };

  const handleDeleteReport = async () => {
    if (!selectedSavedReportId) return;

    try {
      setSavingReport(true);
      await api.deleteAnalyticsSavedReport(selectedSavedReportId, hasGlobalAnalyticsScope ? tenantId || null : null);
      setSelectedSavedReportId('');
      setSavedReportName('');
      setSavedReportStatus('Deleted saved report');
      await fetchSavedReports();
    } catch (err) {
      setError('Failed to delete analytics report: ' + err.message);
    } finally {
      setSavingReport(false);
    }
  };

  const handleCopyReportLink = async () => {
    if (!selectedSavedReportId) return;

    const params = new URLSearchParams(window.location.search);
    params.set('analyticsReport', selectedSavedReportId);
    if (hasGlobalAnalyticsScope && tenantId) {
      params.set('tenantId', tenantId);
    }
    const url = `${window.location.origin}${window.location.pathname}?${params.toString()}`;
    try {
      await navigator.clipboard.writeText(url);
      setSavedReportStatus('Copied dashboard link');
    } catch (err) {
      setError('Failed to copy analytics report link: ' + err.message);
    }
  };

  const successRate = overview?.TotalRequests > 0
    ? (overview.SuccessCount * 100) / overview.TotalRequests
    : 0;
  const failedRate = overview?.TotalRequests > 0
    ? ((overview.FailureCount || 0) * 100) / overview.TotalRequests
    : 0;
  const deniedRate = overview?.TotalRequests > 0
    ? ((overview.DeniedRequestCount || 0) * 100) / overview.TotalRequests
    : 0;
  const rateLimitedRate = overview?.TotalRequests > 0
    ? ((overview.RateLimitedRequestCount || 0) * 100) / overview.TotalRequests
    : 0;
  const ttftCoverage = calculateWeightedCoverage(userBreakdown);
  const customRangeIncomplete = range === 'custom' && (!filters.startUtc || !filters.endUtc);
  const overviewStartMs = overview?.StartUtc ? new Date(overview.StartUtc).getTime() : null;
  const requestedStartMs = filters.startUtc ? new Date(filters.startUtc).getTime() : null;
  const customRangeClamped = range === 'custom'
    && overviewStartMs != null
    && requestedStartMs != null
    && overviewStartMs > requestedStartMs + 1000;
  const emptyResult = !loading && !analyticsNotice && overview && (overview.TotalRequests || 0) < 1;
  const partialTokenUsage = !loading && !analyticsNotice && overview && (overview.UnknownTokenUsageCount || 0) > 0;
  const selectedVmr = vmrGuid ? vmrs.find(vmr => vmr.Id === vmrGuid) : null;
  const selectedVmrHistoryDisabled = Boolean(selectedVmr && selectedVmr.RequestHistoryEnabled !== true);
  const noVmrHasRequestHistory = vmrs.length > 0 && vmrs.every(vmr => vmr.RequestHistoryEnabled !== true);

  const handleOpenTimeline = async (requestHistoryId) => {
    try {
      const result = await api.getRequestHistoryAnalytics(requestHistoryId);
      setSelectedAnalytics(result);
    } catch (err) {
      setError('Failed to fetch request analytics detail: ' + err.message);
    }
  };

  return (
    <div className="view-container analytics-view" title={PAGE_TOOLTIP}>
      <div className="view-header" title="Analytics page header and refresh control.">
        <div>
          <h1 title={PAGE_TOOLTIP}>Analytics</h1>
          <p className="view-subtitle" title="This page uses 30 days of retained analytics data and estimates cost only from the token unit cost you supply.">Answer TTFT, token usage, and estimate-only cost questions across users, models, VMRs, and endpoints.</p>
        </div>
        <div className="view-actions">
          <button className="btn-icon" onClick={handleRefreshOverview} title="Refresh analytics using the active range and filters." disabled={loading}>
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <title>Refresh request analytics data.</title>
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
        </div>
      </div>

      <section className="dashboard-section analytics-filter-section" title={FILTER_SECTION_TOOLTIP}>
        <div className="analytics-range-tabs" title="Time range selector applied to every request analytics query on this page.">
          {RANGE_OPTIONS.map(option => (
            <button key={option.value} className={range === option.value ? 'active' : ''} onClick={() => setRange(option.value)} title={option.tooltip}>
              {option.label}
            </button>
          ))}
        </div>
        {range === 'custom' && (
          <div className="filter-bar analytics-custom-range" title="Custom analytics time range within the retained 30-day window.">
            <div className="filter-group" title="Custom analytics start time.">
              <label title="Custom start timestamp.">Start:</label>
              <input type="datetime-local" value={customStartUtc} onChange={(e) => setCustomStartUtc(e.target.value)} title="Custom start time. The server evaluates the timestamp in UTC and clamps to the retained 30-day window." />
            </div>
            <div className="filter-group" title="Custom analytics end time.">
              <label title="Custom end timestamp.">End:</label>
              <input type="datetime-local" value={customEndUtc} onChange={(e) => setCustomEndUtc(e.target.value)} title="Custom end time. Choose a value after the start time." />
            </div>
          </div>
        )}
        <div className="analytics-saved-report-bar" title="Save, load, update, delete, or copy a link to analytics report filters.">
          <div className="filter-group analytics-report-select" title="Load a saved Analytics workspace report.">
            <label title="Saved report selector.">Saved Report:</label>
            <select value={selectedSavedReportId} onChange={(e) => handleSavedReportSelect(e.target.value)} title="Choose a saved report to load its filters and display state.">
              <option value="" title="No saved report selected.">Unsaved</option>
              {savedReports.map(report => <option key={report.Id} value={report.Id} title="Load this saved Analytics workspace report.">{report.Name}</option>)}
            </select>
          </div>
          <div className="filter-group analytics-report-name" title="Name for a saved Analytics workspace report.">
            <label title="Saved report name.">Name:</label>
            <input value={savedReportName} onChange={(e) => setSavedReportName(e.target.value)} placeholder="Daily user cost" title="Saved report name used in the selector and API." />
          </div>
          <div className="analytics-report-actions" title="Saved report actions.">
            <button className="btn-secondary" onClick={handleNewReport} disabled={savingReport} title="Clear the selected report so the next save creates a new report.">New</button>
            <button className="btn-primary" onClick={handleSaveReport} disabled={savingReport} title="Save the active analytics filters as a report, or update the selected report.">{selectedSavedReportId ? 'Update' : 'Save'}</button>
            <button className="btn-secondary" onClick={handleDeleteReport} disabled={!selectedSavedReportId || savingReport} title="Delete the selected saved report.">Delete</button>
            <button className="btn-secondary" onClick={handleCopyReportLink} disabled={!selectedSavedReportId || savingReport} title="Copy a dashboard link that loads the selected saved report.">Copy Link</button>
          </div>
          {savedReportStatus && <span className="analytics-report-status" title="Latest saved report action status.">{savedReportStatus}</span>}
        </div>
        <div className="filter-bar analytics-filters" title="Request analytics filter controls.">
          {hasGlobalAnalyticsScope && (
            <div className="filter-group" title="System administrators can query global analytics or restrict analytics to one tenant.">
              <label title="Tenant scope filter for system administrators.">Tenant:</label>
              <select value={tenantId} onChange={(e) => setTenantId(e.target.value)} title="Choose Global to query all tenants, or select one tenant to restrict analytics and saved reports.">
                <option value="" title="Run analytics in global system-admin scope.">Global</option>
                {tenants.map(tenant => <option key={tenant.Id} value={tenant.Id} title="Restrict analytics to this tenant.">{tenant.Name || tenant.Id}</option>)}
              </select>
            </div>
          )}
          <div className="filter-group" title="Choose the time-series bucket size.">
            <label title="Granularity applied to all time-series analytics panels.">Granularity:</label>
            <select value={bucketSeconds} onChange={(e) => setBucketSeconds(e.target.value)} title="Choose bucket granularity, or Auto to let the server choose.">
              {GRANULARITY_OPTIONS.map(option => <option key={option.value || 'auto'} value={option.value} title={option.tooltip}>{option.label}</option>)}
            </select>
          </div>
          <div className="filter-group" title="Limit analytics to requests routed through one Virtual Model Runner.">
            <label title="Virtual Model Runner filter applied to all analytics panels.">VMR:</label>
            <select value={vmrGuid} onChange={(e) => setVmrGuid(e.target.value)} title="Choose a Virtual Model Runner, or All to include every VMR visible to the tenant.">
              <option value="" title="Include requests for every Virtual Model Runner.">All</option>
              {vmrs.map(vmr => <option key={vmr.Id} value={vmr.Id} title="Limit analytics to this Virtual Model Runner.">{vmr.Name}</option>)}
            </select>
          </div>
          <div className="filter-group" title="Limit analytics to requests routed to one model runner endpoint.">
            <label title="Model Runner Endpoint filter applied to all analytics panels.">Endpoint:</label>
            <select value={endpointGuid} onChange={(e) => setEndpointGuid(e.target.value)} title="Choose a model runner endpoint, or All to include every endpoint.">
              <option value="" title="Include requests for every endpoint.">All</option>
              {endpoints.map(endpoint => <option key={endpoint.Id} value={endpoint.Id} title="Limit analytics to this model runner endpoint.">{endpoint.Name}</option>)}
            </select>
          </div>
          <div className="filter-group" title="Limit analytics to one normalized upstream provider family.">
            <label title="Provider filter applied to all analytics panels.">Provider:</label>
            <select value={providerName} onChange={(e) => setProviderName(e.target.value)} title="Choose a provider family, or All to include every provider.">
              {PROVIDERS.map(provider => <option key={provider || 'all'} value={provider} title={provider ? `Limit analytics to ${provider} requests.` : 'Include requests for every provider.'}>{provider || 'All'}</option>)}
            </select>
          </div>
          <div className="filter-group" title="Limit analytics to requests whose effective upstream model matches this text.">
            <label title="Effective model text filter applied to all analytics panels.">Model:</label>
            <input value={modelName} onChange={(e) => setModelName(e.target.value)} placeholder="Effective model" title="Filter by effective model name after Conductor mapping and mutation." />
          </div>
          <div className="filter-group" title="Limit analytics to a requestor user ID.">
            <label title="Requestor user filter applied to all analytics panels.">User:</label>
            <input value={requestorUserGuid} onChange={(e) => setRequestorUserGuid(e.target.value)} placeholder="usr_..." title="Filter by requestor user ID to answer per-user TTFT, token usage, and cost estimate questions." />
          </div>
          <div className="filter-group" title="Limit analytics to a credential ID.">
            <label title="Credential filter applied to all analytics panels.">Credential:</label>
            <input value={credentialGuid} onChange={(e) => setCredentialGuid(e.target.value)} placeholder="cred_..." title="Filter by credential ID to answer per-credential TTFT, token usage, and cost estimate questions." />
          </div>
          <div className="filter-group" title="Optional estimate-only token unit cost.">
            <label title="Per-token unit cost used only for estimate-only cost output.">Unit Cost:</label>
            <input type="number" min="0" step="0.00000001" value={tokenUnitCost} onChange={(e) => setTokenUnitCost(e.target.value)} placeholder="0.000001" title="Optional per-token unit cost. Cost output is an estimate, not billing reconciliation." />
          </div>
        </div>
      </section>

      {customRangeIncomplete && (
        <AnalyticsStateBanner tone="warning" title="Custom analytics range is missing a start or end time.">
          Choose both a custom start and end time to run Analytics. Data is retained for 30 days.
        </AnalyticsStateBanner>
      )}
      {analyticsNotice && (
        <AnalyticsStateBanner tone={analyticsNotice.tone} title="Analytics query state." onRetry={handleRefreshOverview}>
          {analyticsNotice.message}
        </AnalyticsStateBanner>
      )}
      {customRangeClamped && (
        <AnalyticsStateBanner tone="info" title="The query start was adjusted to the retained analytics window.">
          The custom range starts before retained Analytics data. Results begin at {formatDateTime(overview?.StartUtc)}.
        </AnalyticsStateBanner>
      )}
      {selectedVmrHistoryDisabled && (
        <AnalyticsStateBanner tone="warning" title="Request History is disabled for the selected VMR.">
          Enable Request History on {selectedVmr?.Name || 'this Virtual Model Runner'} and send new requests before expecting rows in Analytics.
        </AnalyticsStateBanner>
      )}
      {emptyResult && (
        <AnalyticsStateBanner tone="info" title="No analytics rows matched the active filters.">
          {noVmrHasRequestHistory
            ? 'No visible Virtual Model Runner has Request History enabled. Enable it on the VMR and send new requests to populate Analytics.'
            : 'No Analytics data matched the active range and filters. Check the time range, tenant scope, VMR filter, and whether Request History was enabled before the requests were sent.'}
        </AnalyticsStateBanner>
      )}
      {partialTokenUsage && (
        <AnalyticsStateBanner tone="warning" title="Some successful completions are missing provider token metrics.">
          {formatNumber(overview.UnknownTokenUsageCount)} successful completions have unknown token usage, so token and cost totals are partial.
        </AnalyticsStateBanner>
      )}

      <AnalyticsTabNav activeTab={activeTab} onChange={setActiveTab} />

      {activeTab === 'overview' && (
        <>
      <div className="analytics-metric-grid" title="High-level request analytics metrics calculated from the active range and filters.">
        <MetricCard
          label="Requests"
          value={formatNumber(overview?.TotalRequests || 0)}
          sublabel={`${formatNumber(overview?.FailureCount || 0)} failed`}
          tooltip="Total requests matching the active filters. The sublabel shows requests with missing or 4xx/5xx HTTP status results."
          valueTooltip="Total request history entries included in the active analytics result set."
          sublabelTooltip="Filtered requests counted as failures because they have no HTTP status or returned 4xx/5xx."
        />
        <MetricCard
          label="Success Rate"
          value={formatPercent(successRate)}
          sublabel={`${formatNumber(overview?.SuccessCount || 0)} successful`}
          tooltip="Percentage of filtered requests with HTTP status from 100 through 399."
          valueTooltip="Successful request percentage calculated from successful requests divided by total requests."
          sublabelTooltip="Filtered requests counted as successful based on HTTP status from 100 through 399."
        />
        <MetricCard
          label="P95 TTFT"
          value={formatMs(overview?.P95DurationMs)}
          sublabel={`P50 ${formatMs(overview?.P50DurationMs)}`}
          tooltip="95th percentile time-to-first-token for successful completions. TTFT is measured from Conductor request received to first token received."
          valueTooltip="TTFT below which 95 percent of successful filtered completions produced a first token."
          sublabelTooltip="Median TTFT for successful completions in the active filters."
        />
        <MetricCard
          label="Unknown Tokens"
          value={formatNumber(overview?.UnknownTokenUsageCount || 0)}
          sublabel="unknown, not zero"
          tooltip="Successful completions with missing provider token usage. Missing token usage is unknown, not zero."
          valueTooltip="Successful completions where Conductor did not receive usable provider token metrics."
          sublabelTooltip="Analytics keeps missing usage separate from real zero-token values."
        />
        <MetricCard
          label="Tokens"
          value={formatNumber(overview?.TotalTokens || 0)}
          sublabel={`${formatNumber(overview?.PromptTokens || 0)} prompt / ${formatNumber(overview?.CompletionTokens || 0)} completion`}
          tooltip="Total provider-reported tokens across successful completions matching the active filters."
          valueTooltip="Sum of provider-reported total tokens for successful completions."
          sublabelTooltip="Prompt and completion token split for successful completions."
        />
        <MetricCard
          label="Estimated Cost"
          value={formatCost(overview?.EstimatedCost, overview?.CostCurrency)}
          sublabel={overview?.TokenUnitCost != null ? `${overview.TokenUnitCost} per token` : 'unit cost not set'}
          tooltip="Estimate-only cost from successful reported tokens multiplied by your supplied token unit cost."
          valueTooltip="Estimated cost is not chargeback, billback, provider billing reconciliation, or accounting-grade reporting."
          sublabelTooltip="Per-token unit cost supplied in the workspace filter."
        />
      </div>

      <section className="dashboard-section analytics-main-chart" title={CHART_TOOLTIP}>
        <div className="request-history-chart-header" title="Header for the combined request volume and latency chart.">
          <h2 title={CHART_TOOLTIP}>Volume and TTFT</h2>
          {loading && <span className="text-muted" title="Request analytics overview is loading with the active filters.">Loading...</span>}
        </div>
        <TimeSeriesChart data={overview?.TimeSeries || []} />
        <div className="request-history-chart-legend" title="Legend for the request volume and latency chart.">
          <span className="request-history-legend-item" title="Green stacked bar segment represents successful requests in each time bucket."><span className="request-history-legend-color success" title="Success bar color." />Success</span>
          <span className="request-history-legend-item" title="Red stacked bar segment represents failed requests in each time bucket."><span className="request-history-legend-color danger" title="Failure bar color." />Failed</span>
          <span className="request-history-legend-item" title="Line represents average time-to-first-token in each time bucket."><span className="analytics-line-key" title="Average TTFT line color." />Avg TTFT</span>
        </div>
      </section>

      <div className="analytics-grid-two" title="Secondary analytics panels for stage timing and slow individual request inspection.">
        <section className="dashboard-section" title={STAGE_BREAKDOWN_TOOLTIP}>
          <div className="request-history-chart-header" title="Header for the aggregate stage timing panel.">
            <h2 title={STAGE_BREAKDOWN_TOOLTIP}>Stage Breakdown</h2>
          </div>
          <StageBreakdown stages={overview?.StageBreakdown || []} />
        </section>

        <section className="dashboard-section" title={SLOWEST_REQUESTS_TOOLTIP}>
          <div className="request-history-chart-header" title="Header for the slowest individual requests table.">
            <h2 title={SLOWEST_REQUESTS_TOOLTIP}>Slowest Requests</h2>
          </div>
          <div className="detail-table-container" title={SLOWEST_REQUESTS_TOOLTIP}>
            <table className="detail-table" title={SLOWEST_REQUESTS_TOOLTIP}>
              <thead>
                <tr>
                  <th title="Request history identifier for a slow request. Click a row to open per-stage analytics.">Request</th>
                  <th title="Final HTTP status returned for the slow request.">Status</th>
                  <th title="Total response time recorded for the slow request.">Latency</th>
                  <th title="The captured analytics stage that contributed the most time to the request, when available.">Dominant Stage</th>
                </tr>
              </thead>
              <tbody>
                {(overview?.SlowestRequests || []).length < 1 ? (
                  <tr><td colSpan="4" className="detail-table-empty-cell" title="No individual slow request rows matched the active range and filters.">No slow requests in this range.</td></tr>
                ) : (overview.SlowestRequests || []).map(item => (
                  <tr key={item.RequestHistoryId} onClick={() => handleOpenTimeline(item.RequestHistoryId)} className="clickable-row" title="Open the per-stage request analytics timeline for this slow request.">
                    <td title="Request history identifier for this slow request."><CopyableId value={item.RequestHistoryId} /></td>
                    <td title="Final HTTP status returned to the caller for this slow request."><span className={`service-state-badge ${statusTone(item.HttpStatus)}`} title="HTTP status category for this slow request.">{item.HttpStatus || '-'}</span></td>
                    <td title="Total elapsed request duration measured by Conductor.">{formatMs(item.ResponseTimeMs)}</td>
                    <td title="Longest captured analytics stage, or empty when no dominant stage was captured.">{item.DominantStageKind || '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <div className="analytics-breakdown-grid" title="User and credential analytics breakdowns for the active filters.">
        <AnalyticsBreakdownTable
          title="User Breakdown"
          tooltip={USER_BREAKDOWN_TOOLTIP}
          rows={userBreakdown}
          loading={loading}
          emptyMessage="No user analytics in this range."
          identityFallback="(anonymous)"
          costCurrency={overview?.CostCurrency}
        />
        <AnalyticsBreakdownTable
          title="Credential Breakdown"
          tooltip={CREDENTIAL_BREAKDOWN_TOOLTIP}
          rows={credentialBreakdown}
          loading={loading}
          emptyMessage="No credential analytics in this range."
          identityFallback="(none)"
          costCurrency={overview?.CostCurrency}
        />
      </div>

      <section className="dashboard-section" title={ENDPOINT_SUMMARY_TOOLTIP}>
        <div className="request-history-chart-header" title="Header for endpoint-level request analytics.">
          <h2 title={ENDPOINT_SUMMARY_TOOLTIP}>Endpoint Summary</h2>
        </div>
        <div className="detail-table-container" title={ENDPOINT_SUMMARY_TOOLTIP}>
          <table className="detail-table" title={ENDPOINT_SUMMARY_TOOLTIP}>
            <thead>
              <tr>
                <th title="Model runner endpoint selected by routing for this analytics group, or Unrouted when no endpoint was selected.">Endpoint</th>
                <th title="Normalized provider family associated with this endpoint or request group.">Provider</th>
                <th title="Total filtered requests routed to this endpoint group.">Requests</th>
                <th title="Filtered requests in this endpoint group with missing or 4xx/5xx HTTP status results.">Failures</th>
                <th title="95th percentile time-to-first-token for this endpoint group.">P95 TTFT</th>
                <th title="Provider-reported prompt and completion tokens for this endpoint group.">Tokens</th>
                <th title="Average overall tokens per second for requests in this endpoint group when usage metrics were available.">Avg TPS</th>
              </tr>
            </thead>
            <tbody>
              {(overview?.EndpointSummaries || []).length < 1 ? (
                <tr><td colSpan="7" className="detail-table-empty-cell" title="No endpoint-level analytics rows matched the active range and filters.">No endpoint analytics in this range.</td></tr>
              ) : (overview.EndpointSummaries || []).map(item => (
                <tr key={item.ModelEndpointGuid || item.ModelEndpointName} title="One endpoint analytics group for the active filters.">
                  <td title="Endpoint name or endpoint identifier selected by routing. Unrouted means Conductor did not select an endpoint.">{item.ModelEndpointName || item.ModelEndpointGuid || 'Unrouted'}</td>
                  <td title="Normalized upstream provider family for this endpoint group.">{item.ProviderName || '-'}</td>
                  <td title="Total filtered requests routed to this endpoint group.">{formatNumber(item.RequestCount)}</td>
                  <td title="Filtered requests in this endpoint group counted as failures.">{formatNumber(item.FailureCount)}</td>
                  <td title="95th percentile time-to-first-token for this endpoint group.">{formatMs(item.P95DurationMs)}</td>
                  <td title="Total provider-reported tokens for this endpoint group.">{formatNumber(item.TotalTokens)}</td>
                  <td title="Average overall tokens per second for this endpoint group.">{item.AverageTokensPerSecond ?? '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
        </>
      )}

      {activeTab === 'latency' && (
        <>
          <div className="analytics-metric-grid" title="Latency and time-to-first-token metrics for successful completions.">
            <MetricCard
              label="Avg TTFT"
              value={formatMs(overview?.AverageTimeToFirstTokenMs)}
              sublabel="request received to first token"
              tooltip="Average time-to-first-token for successful completions matching the active filters."
            />
            <MetricCard
              label="P50 TTFT"
              value={formatMs(overview?.P50DurationMs)}
              sublabel={`P95 ${formatMs(overview?.P95DurationMs)}`}
              tooltip="Median and 95th percentile time-to-first-token for successful completions."
            />
            <MetricCard
              label="P99 TTFT"
              value={formatMs(overview?.P99DurationMs)}
              sublabel={`${formatNumber(overview?.SuccessCount || 0)} successful`}
              tooltip="99th percentile time-to-first-token for successful completions."
            />
            <MetricCard
              label="TTFT Coverage"
              value={formatPercent(ttftCoverage)}
              sublabel="successful completions"
              tooltip="Weighted percentage of successful completions with captured time-to-first-token, based on grouped user analytics."
            />
          </div>
          <section className="dashboard-section analytics-main-chart" title={CHART_TOOLTIP}>
            <div className="request-history-chart-header" title="Latency chart header.">
              <h2 title={CHART_TOOLTIP}>TTFT Trend</h2>
              {loading && <span className="text-muted" title="Latency analytics are loading with the active filters.">Loading...</span>}
            </div>
            <TimeSeriesChart data={overview?.TimeSeries || []} />
          </section>
          <div className="analytics-grid-two" title="Latency detail panels.">
            <section className="dashboard-section" title={STAGE_BREAKDOWN_TOOLTIP}>
              <div className="request-history-chart-header" title="Stage breakdown header.">
                <h2 title={STAGE_BREAKDOWN_TOOLTIP}>Stage Breakdown</h2>
              </div>
              <StageBreakdown stages={overview?.StageBreakdown || []} />
            </section>
            <section className="dashboard-section" title={SLOWEST_REQUESTS_TOOLTIP}>
              <div className="request-history-chart-header" title="Slowest requests header.">
                <h2 title={SLOWEST_REQUESTS_TOOLTIP}>Slowest Requests</h2>
              </div>
              <div className="detail-table-container" title={SLOWEST_REQUESTS_TOOLTIP}>
                <table className="detail-table" title={SLOWEST_REQUESTS_TOOLTIP}>
                  <thead>
                    <tr>
                      <th title="Request history identifier for a slow request.">Request</th>
                      <th title="HTTP status for a slow request.">Status</th>
                      <th title="Total response latency.">Latency</th>
                      <th title="Dominant captured analytics stage.">Dominant Stage</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(overview?.SlowestRequests || []).length < 1 ? (
                      <tr><td colSpan="4" className="detail-table-empty-cell" title="No slow request rows matched the active filters.">No slow requests in this range.</td></tr>
                    ) : (overview.SlowestRequests || []).map(item => (
                      <tr key={item.RequestHistoryId} onClick={() => handleOpenTimeline(item.RequestHistoryId)} className="clickable-row" title="Open the per-stage request analytics timeline for this slow request.">
                        <td title="Request history identifier for this slow request."><CopyableId value={item.RequestHistoryId} /></td>
                        <td title="Final HTTP status returned to the caller."><span className={`service-state-badge ${statusTone(item.HttpStatus)}`} title="HTTP status category.">{item.HttpStatus || '-'}</span></td>
                        <td title="Total elapsed request duration measured by Conductor.">{formatMs(item.ResponseTimeMs)}</td>
                        <td title="Longest captured analytics stage.">{item.DominantStageKind || '-'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          </div>
          <div className="analytics-breakdown-grid" title="Latency drill-down by user and credential.">
            <AnalyticsBreakdownTable title="User TTFT Breakdown" tooltip={USER_BREAKDOWN_TOOLTIP} rows={userBreakdown} loading={loading} emptyMessage="No user latency analytics in this range." identityFallback="(anonymous)" costCurrency={overview?.CostCurrency} />
            <AnalyticsBreakdownTable title="Credential TTFT Breakdown" tooltip={CREDENTIAL_BREAKDOWN_TOOLTIP} rows={credentialBreakdown} loading={loading} emptyMessage="No credential latency analytics in this range." identityFallback="(none)" costCurrency={overview?.CostCurrency} />
          </div>
        </>
      )}

      {activeTab === 'tokens' && (
        <>
          <div className="analytics-metric-grid" title="Token usage and estimate-only cost metrics.">
            <MetricCard label="Prompt Tokens" value={formatNumber(overview?.PromptTokens || 0)} sublabel="successful completions" tooltip="Provider-reported prompt tokens for successful completions." />
            <MetricCard label="Completion Tokens" value={formatNumber(overview?.CompletionTokens || 0)} sublabel="successful completions" tooltip="Provider-reported completion tokens for successful completions." />
            <MetricCard label="Total Tokens" value={formatNumber(overview?.TotalTokens || 0)} sublabel={`${formatNumber(overview?.UnknownTokenUsageCount || 0)} unknown usage`} tooltip="Provider-reported total tokens for successful completions." />
            <MetricCard label="Cached Tokens" value={formatNumber(overview?.CachedTokens)} sublabel="nullable provider field" tooltip="Cached-token counts are nullable until provider parsers persist this category." />
            <MetricCard label="Multimodal Tokens" value={formatNumber(overview?.MultimodalTokens)} sublabel="nullable provider field" tooltip="Multimodal-token counts are nullable until provider parsers persist this category." />
            <MetricCard label="Estimated Cost" value={formatCost(overview?.EstimatedCost, overview?.CostCurrency)} sublabel={overview?.TokenUnitCost != null ? `${overview.TokenUnitCost} per token` : 'unit cost not set'} tooltip="Estimate-only cost from successful reported tokens and the supplied token unit cost." />
          </div>
          <section className="dashboard-section analytics-main-chart" title="Token usage over time with request volume context.">
            <div className="request-history-chart-header" title="Token trend header.">
              <h2 title="Token usage over time.">Token Usage Trend</h2>
              {loading && <span className="text-muted" title="Token analytics are loading with the active filters.">Loading...</span>}
            </div>
            <TimeSeriesChart data={overview?.TimeSeries || []} />
          </section>
          <div className="analytics-breakdown-grid" title="Token usage by user and credential.">
            <AnalyticsBreakdownTable title="User Token Breakdown" tooltip={USER_BREAKDOWN_TOOLTIP} rows={userBreakdown} loading={loading} emptyMessage="No user token analytics in this range." identityFallback="(anonymous)" costCurrency={overview?.CostCurrency} />
            <AnalyticsBreakdownTable title="Credential Token Breakdown" tooltip={CREDENTIAL_BREAKDOWN_TOOLTIP} rows={credentialBreakdown} loading={loading} emptyMessage="No credential token analytics in this range." identityFallback="(none)" costCurrency={overview?.CostCurrency} />
          </div>
        </>
      )}

      {activeTab === 'users' && (
        <div className="analytics-breakdown-grid" title="User and credential analytics breakdowns.">
          <AnalyticsBreakdownTable title="User Breakdown" tooltip={USER_BREAKDOWN_TOOLTIP} rows={userBreakdown} loading={loading} emptyMessage="No user analytics in this range." identityFallback="(anonymous)" costCurrency={overview?.CostCurrency} />
          <AnalyticsBreakdownTable title="Credential Breakdown" tooltip={CREDENTIAL_BREAKDOWN_TOOLTIP} rows={credentialBreakdown} loading={loading} emptyMessage="No credential analytics in this range." identityFallback="(none)" costCurrency={overview?.CostCurrency} />
        </div>
      )}

      {activeTab === 'reliability' && (
        <>
          <div className="analytics-metric-grid" title="Reliability and access outcome metrics.">
            <MetricCard label="Failed Requests" value={formatNumber(overview?.FailureCount || 0)} sublabel={formatPercent(failedRate)} tooltip="Requests with missing status or HTTP 4xx/5xx outcomes." />
            <MetricCard label="Denied Requests" value={formatNumber(overview?.DeniedRequestCount || 0)} sublabel={formatPercent(deniedRate)} tooltip="Requests denied by model access or routing decisions." />
            <MetricCard label="Rate Limited" value={formatNumber(overview?.RateLimitedRequestCount || 0)} sublabel={formatPercent(rateLimitedRate)} tooltip="Requests that returned HTTP 429." />
            <MetricCard label="Successful" value={formatNumber(overview?.SuccessCount || 0)} sublabel={formatPercent(successRate)} tooltip="Requests with successful completion status in the active filters." />
          </div>
          <div className="analytics-breakdown-grid" title="Reliability and access breakdowns by user and credential.">
            <AnalyticsBreakdownTable title="User Access Breakdown" tooltip={USER_BREAKDOWN_TOOLTIP} rows={userBreakdown} loading={loading} emptyMessage="No user access analytics in this range." identityFallback="(anonymous)" costCurrency={overview?.CostCurrency} />
            <AnalyticsBreakdownTable title="Credential Access Breakdown" tooltip={CREDENTIAL_BREAKDOWN_TOOLTIP} rows={credentialBreakdown} loading={loading} emptyMessage="No credential access analytics in this range." identityFallback="(none)" costCurrency={overview?.CostCurrency} />
          </div>
        </>
      )}

      {activeTab === 'reports' && (
        <SavedReportsPanel reports={savedReports} selectedReportId={selectedSavedReportId} onSelect={handleSavedReportSelect} />
      )}

      {activeTab === 'exports' && (
        <ExportStatusPanel filters={filters} overview={overview} selectedReportId={selectedSavedReportId} />
      )}

      <TimelineModal analytics={selectedAnalytics} onClose={() => setSelectedAnalytics(null)} />
    </div>
  );
}

export default RequestAnalytics;
