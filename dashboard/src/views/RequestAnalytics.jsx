import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useApp } from '../context/AppContext';
import Modal from '../components/Modal';
import CopyableId from '../components/CopyableId';

const RANGE_OPTIONS = [
  { label: 'Last Hour', value: 'lastHour', tooltip: 'Filter every analytics panel to requests created during the last hour.' },
  { label: 'Last Day', value: 'lastDay', tooltip: 'Filter every analytics panel to requests created during the last day.' },
  { label: 'Last Week', value: 'lastWeek', tooltip: 'Filter every analytics panel to requests created during the last week.' },
  { label: 'Last Month', value: 'lastMonth', tooltip: 'Filter every analytics panel to requests created during the last month.' }
];

const PROVIDERS = ['', 'OpenAI', 'vLLM', 'Gemini', 'Ollama'];
const PAGE_TOOLTIP = 'Request analytics dashboard for filtered request volume, latency, token throughput, endpoint behavior, and per-stage timing.';
const FILTER_SECTION_TOOLTIP = 'Filters applied to every metric, chart, table, and request analytics detail on this page.';
const CHART_TOOLTIP = 'Combined time-series chart. Stacked bars show successful and failed request volume per bucket, and the line shows P95 latency.';
const STAGE_BREAKDOWN_TOOLTIP = 'Aggregated analytics stages for the active filters. Each row compares total time spent in that stage across captured events.';
const SLOWEST_REQUESTS_TOOLTIP = 'Slowest individual requests matching the active filters. Click a row to inspect its per-stage analytics timeline.';
const ENDPOINT_SUMMARY_TOOLTIP = 'Endpoint-level analytics grouped by routed model endpoint for the active filters.';

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
            {[0, 0.25, 0.5, 0.75, 1].map(tick => {
              const y = padTop + innerHeight - tick * innerHeight;
              return (
                <g key={tick}>
                  <title>Request-count grid line used to read stacked bar height against the highest-volume bucket in the active range.</title>
                  <line x1={padLeft} x2={width - padRight} y1={y} y2={y} />
                  <text x={padLeft - 8} y={y + 4} textAnchor="end">{Math.round(maxRequests * tick)}</text>
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
              <span title="95th percentile request duration for the hovered analytics bucket">P95: {formatMs(buckets[hovered].P95DurationMs)}</span>
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
  const { api, setError } = useApp();
  const [range, setRange] = useState('lastDay');
  const [vmrGuid, setVmrGuid] = useState('');
  const [endpointGuid, setEndpointGuid] = useState('');
  const [providerName, setProviderName] = useState('');
  const [modelName, setModelName] = useState('');
  const [vmrs, setVmrs] = useState([]);
  const [endpoints, setEndpoints] = useState([]);
  const [overview, setOverview] = useState(null);
  const [loading, setLoading] = useState(false);
  const [selectedAnalytics, setSelectedAnalytics] = useState(null);

  const filters = useMemo(() => ({
    range,
    vmrGuid,
    endpointGuid,
    providerName,
    modelName,
    limit: 10000
  }), [range, vmrGuid, endpointGuid, providerName, modelName]);

  const fetchOverview = useCallback(async () => {
    try {
      setLoading(true);
      const result = await api.getRequestAnalyticsOverview(filters);
      setOverview(result);
    } catch (err) {
      setError('Failed to fetch request analytics: ' + err.message);
      setOverview(null);
    } finally {
      setLoading(false);
    }
  }, [api, filters, setError]);

  useEffect(() => {
    fetchOverview();
  }, [fetchOverview]);

  useEffect(() => {
    api.listVirtualModelRunners({ maxResults: 1000 }).then(result => setVmrs(result.Data || [])).catch(() => {});
    api.listModelRunnerEndpoints({ maxResults: 1000 }).then(result => setEndpoints(result.Data || [])).catch(() => {});
  }, [api]);

  const successRate = overview?.TotalRequests > 0
    ? (overview.SuccessCount * 100) / overview.TotalRequests
    : 0;

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
      <div className="view-header" title="Request analytics page header and refresh control.">
        <div>
          <h1 title={PAGE_TOOLTIP}>Request Analytics</h1>
          <p className="view-subtitle" title="This page combines request history and per-stage analytics to diagnose traffic, latency, throughput, slow requests, and endpoint behavior.">Diagnose request volume, latency, provider timing, token throughput, slow stages, and endpoint behavior.</p>
        </div>
        <div className="view-actions">
          <button className="btn-icon" onClick={fetchOverview} title="Refresh request analytics using the active range and filters." disabled={loading}>
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
        <div className="filter-bar analytics-filters" title="Request analytics filter controls.">
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
        </div>
      </section>

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
          label="P95 Latency"
          value={formatMs(overview?.P95DurationMs)}
          sublabel={`P50 ${formatMs(overview?.P50DurationMs)}`}
          tooltip="95th percentile total request duration for filtered requests. The sublabel shows median latency."
          valueTooltip="Request duration below which 95 percent of filtered requests completed."
          sublabelTooltip="Median total request duration for the active filters."
        />
        <MetricCard
          label="Analytics Coverage"
          value={formatPercent(overview?.AnalyticsCoveragePercent || 0)}
          sublabel={`${formatNumber(overview?.AnalyticsCapturedCount || 0)} captured`}
          tooltip="Share of filtered requests that have detailed per-stage analytics captured."
          valueTooltip="Detailed analytics capture percentage for the active request result set."
          sublabelTooltip="Number of filtered requests with detailed analytics events or captured analytics metadata."
        />
        <MetricCard
          label="Tokens"
          value={formatNumber(overview?.TotalTokens || 0)}
          sublabel={`Avg TPS ${overview?.AverageTokensPerSecond ?? '-'}`}
          tooltip="Total provider-reported tokens across filtered requests. The sublabel shows average overall token throughput."
          valueTooltip="Sum of provider-reported prompt and completion tokens for filtered requests."
          sublabelTooltip="Average overall tokens per second for requests where provider usage metrics were available."
        />
      </div>

      <section className="dashboard-section analytics-main-chart" title={CHART_TOOLTIP}>
        <div className="request-history-chart-header" title="Header for the combined request volume and latency chart.">
          <h2 title={CHART_TOOLTIP}>Volume and Latency</h2>
          {loading && <span className="text-muted" title="Request analytics overview is loading with the active filters.">Loading...</span>}
        </div>
        <TimeSeriesChart data={overview?.TimeSeries || []} />
        <div className="request-history-chart-legend" title="Legend for the request volume and latency chart.">
          <span className="request-history-legend-item" title="Green stacked bar segment represents successful requests in each time bucket."><span className="request-history-legend-color success" title="Success bar color." />Success</span>
          <span className="request-history-legend-item" title="Red stacked bar segment represents failed requests in each time bucket."><span className="request-history-legend-color danger" title="Failure bar color." />Failed</span>
          <span className="request-history-legend-item" title="Line represents P95 request latency in each time bucket."><span className="analytics-line-key" title="P95 latency line color." />P95 latency</span>
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
                <th title="95th percentile total request duration for this endpoint group.">P95</th>
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
                  <td title="95th percentile request duration for this endpoint group.">{formatMs(item.P95DurationMs)}</td>
                  <td title="Total provider-reported tokens for this endpoint group.">{formatNumber(item.TotalTokens)}</td>
                  <td title="Average overall tokens per second for this endpoint group.">{item.AverageTokensPerSecond ?? '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <TimelineModal analytics={selectedAnalytics} onClose={() => setSelectedAnalytics(null)} />
    </div>
  );
}

export default RequestAnalytics;
