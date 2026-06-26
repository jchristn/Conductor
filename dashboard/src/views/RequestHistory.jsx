import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import CopyableId from '../components/CopyableId';
import CopyButton from '../components/CopyButton';
import RefreshButton from '../components/RefreshButton';
import { copyToClipboard } from '../utils/clipboard';

function CollapsibleSection({ title, meta, content, defaultExpanded = false, showFormatJson = false, tooltip }) {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const [copied, setCopied] = useState(false);
  const [formatted, setFormatted] = useState(false);

  const handleCopy = async (e) => {
    e.stopPropagation();
    const text = typeof content === 'string' ? content : JSON.stringify(content, null, 2);
    const success = await copyToClipboard(text);
    if (success) {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    }
  };

  const handleFormat = (e) => {
    e.stopPropagation();
    setFormatted(!formatted);
  };

  const getDisplayContent = () => {
    if (typeof content !== 'string') {
      return JSON.stringify(content, null, 2);
    }
    if (formatted && content) {
      try {
        const parsed = JSON.parse(content);
        return JSON.stringify(parsed, null, 2);
      } catch {
        return content; // Not valid JSON, return as-is
      }
    }
    return content;
  };

  const displayContent = getDisplayContent();

  return (
    <div className="collapsible-section">
      <div className="collapsible-header" onClick={() => setExpanded(!expanded)} title={tooltip || 'Captured request history payload detail. Expand to inspect the retained value.'}>
        <div className="collapsible-header-left">
          <svg
            width="12"
            height="12"
            viewBox="0 0 20 20"
            fill="currentColor"
            className={expanded ? 'expanded' : ''}
          >
            <path fillRule="evenodd" d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z" clipRule="evenodd" />
          </svg>
          <span className="collapsible-title" title={tooltip || 'Captured request history payload detail. Expand to inspect the retained value.'}>{title}</span>
          {meta && <span className="collapsible-meta" title="Retention and redaction metadata for this captured value">{meta}</span>}
        </div>
        <div className="collapsible-actions">
          {showFormatJson && (
            <button
              className={`format-btn ${formatted ? 'active' : ''}`}
              onClick={handleFormat}
              title={formatted ? 'Show raw' : 'Format JSON'}
            >
              <svg width="14" height="14" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M12.316 3.051a1 1 0 01.633 1.265l-4 12a1 1 0 11-1.898-.632l4-12a1 1 0 011.265-.633zM5.707 6.293a1 1 0 010 1.414L3.414 10l2.293 2.293a1 1 0 11-1.414 1.414l-3-3a1 1 0 010-1.414l3-3a1 1 0 011.414 0zm8.586 0a1 1 0 011.414 0l3 3a1 1 0 010 1.414l-3 3a1 1 0 11-1.414-1.414L16.586 10l-2.293-2.293a1 1 0 010-1.414z" clipRule="evenodd" />
              </svg>
            </button>
          )}
          <button
            className={`copy-btn ${copied ? 'copied' : ''}`}
            onClick={handleCopy}
            title={copied ? 'Copied!' : 'Copy to clipboard'}
          >
            {copied ? (
              <svg width="14" height="14" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
              </svg>
            ) : (
              <svg width="14" height="14" viewBox="0 0 20 20" fill="currentColor">
                <path d="M8 3a1 1 0 011-1h2a1 1 0 110 2H9a1 1 0 01-1-1z" />
                <path d="M6 3a2 2 0 00-2 2v11a2 2 0 002 2h8a2 2 0 002-2V5a2 2 0 00-2-2 3 3 0 01-3 3H9a3 3 0 01-3-3z" />
              </svg>
            )}
          </button>
        </div>
      </div>
      {expanded && (
        <div className="collapsible-content" title={tooltip || 'Retained request history payload content for this section'}>
          <pre title={tooltip || 'Retained request history payload content for this section'}>{displayContent || '(empty)'}</pre>
        </div>
      )}
    </div>
  );
}

const DEFAULT_STATUS_CLASS_COUNTS = {
  NoStatus: 0,
  '1xx': 0,
  '2xx': 0,
  '3xx': 0,
  '4xx': 0,
  '5xx': 0
};

const DEFAULT_DENIAL_REASON_COUNTS = {
  None: 0
};

const DEFAULT_SESSION_AFFINITY_COUNTS = {
  None: 0
};

function formatFacetEntries(facets, defaults = {}) {
  return Object.entries({ ...defaults, ...(facets || {}) })
    .sort((left, right) => right[1] - left[1]);
}

function getRetentionLabel(retained, redacted, truncated) {
  if (!retained) return { label: 'Metadata Only', tone: 'neutral' };
  if (redacted && truncated) return { label: 'Redacted + Truncated', tone: 'warning' };
  if (redacted) return { label: 'Redacted', tone: 'warning' };
  if (truncated) return { label: 'Truncated', tone: 'warning' };
  return { label: 'Retained', tone: 'success' };
}

function getStageOutcomeTone(outcome) {
  switch (outcome) {
    case 'Passed':
      return 'success';
    case 'Denied':
      return 'danger';
    case 'Fallback':
      return 'warning';
    default:
      return 'neutral';
  }
}

function DetailItem({ label, tooltip, children }) {
  return (
    <div className="detail-item" title={tooltip}>
      <label title={tooltip}>{label}:</label>
      <span title={tooltip}>{children}</span>
    </div>
  );
}

function toLocalDateTimeValue(value) {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  const pad = (part) => String(part).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function RequestHistory() {
  const { api, setError } = useApp();
  const [entries, setEntries] = useState([]);
  const [virtualModelRunners, setVirtualModelRunners] = useState([]);
  const [endpoints, setEndpoints] = useState([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [pageInput, setPageInput] = useState('1');
  const [selectedEntry, setSelectedEntry] = useState(null);
  const [showDetail, setShowDetail] = useState(false);
  const [detailData, setDetailData] = useState(null);
  const [detailAnalytics, setDetailAnalytics] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [showBulkDeleteConfirm, setShowBulkDeleteConfirm] = useState(false);
  const [bulkDeleteLoading, setBulkDeleteLoading] = useState(false);
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [showSelectedDeleteConfirm, setShowSelectedDeleteConfirm] = useState(false);
  const [selectedDeleteLoading, setSelectedDeleteLoading] = useState(false);
  const [deleteResult, setDeleteResult] = useState(null);
  const [showJsonModal, setShowJsonModal] = useState(false);
  const [jsonModalData, setJsonModalData] = useState(null);
  const [requestHistoryIssue, setRequestHistoryIssue] = useState(null);
  const [summary, setSummary] = useState(null);
  const [summaryLoading, setSummaryLoading] = useState(false);

  // Filter state
  const [filters, setFilters] = useState({
    vmrGuid: '',
    endpointGuid: '',
    requestorUserGuid: '',
    credentialGuid: '',
    loadBalancingPolicyGuid: '',
    modelAccessPolicyGuid: '',
    modelAccessRuleGuid: '',
    modelAccessDecision: '',
    modelAccessWouldDeny: '',
    modelName: '',
    mutationSummary: '',
    denialReasonCode: '',
    reservationGuid: '',
    reservationDecision: '',
    reservationReasonCode: '',
    sessionAffinityOutcome: '',
    selectionStrategy: '',
    endpointGroupGuid: '',
    backoffReason: '',
    adaptiveSelection: '',
    policyFallbackUsed: '',
    statusClass: '',
    createdAfterUtc: '',
    createdBeforeUtc: '',
    sourceIp: '',
    httpStatus: ''
  });

  const showRequestHistoryUnavailableModal = useCallback((err) => {
    setError(null);
    setRequestHistoryIssue({
      endpoint: err?.endpoint || '/v1.0/requesthistory',
      status: err?.status || 404,
      message: err?.message || 'HTTP 404'
    });
  }, [setError]);

  const fetchEntries = useCallback(async () => {
    try {
      setLoading(true);
      const params = {
        page,
        pageSize,
        ...Object.fromEntries(
          Object.entries(filters).filter(([_, v]) => v !== '')
        )
      };
      const result = await api.searchRequestHistory(params);
      setRequestHistoryIssue(null);
      setEntries(result.Data || []);
      setTotalPages(result.TotalPages || 1);
      setTotalCount(result.TotalCount || 0);
    } catch (err) {
      if (err?.status === 404) {
        setEntries([]);
        setTotalPages(1);
        setTotalCount(0);
        showRequestHistoryUnavailableModal(err);
      } else {
        setError('Failed to fetch request history: ' + err.message);
      }
    } finally {
      setLoading(false);
    }
  }, [api, setError, page, pageSize, filters, showRequestHistoryUnavailableModal]);

  const fetchSummary = useCallback(async () => {
    try {
      setSummaryLoading(true);
      const endUtc = new Date().toISOString();
      const startUtc = filters.createdAfterUtc || new Date(Date.now() - (24 * 60 * 60 * 1000)).toISOString();
      const result = await api.getRequestHistorySummary({
        ...Object.fromEntries(Object.entries(filters).filter(([_, value]) => value !== '')),
        startUtc,
        endUtc: filters.createdBeforeUtc || endUtc,
        interval: 'hour'
      });
      setSummary(result);
    } catch {
      setSummary(null);
    } finally {
      setSummaryLoading(false);
    }
  }, [api, filters]);

  const fetchVMRs = useCallback(async () => {
    try {
      const result = await api.listVirtualModelRunners({ maxResults: 1000 });
      setVirtualModelRunners(result.Data || []);
    } catch (err) {
      // Non-critical, ignore
    }
  }, [api]);

  const fetchEndpoints = useCallback(async () => {
    try {
      const result = await api.listModelRunnerEndpoints({ maxResults: 1000 });
      setEndpoints(result.Data || []);
    } catch (err) {
      // Non-critical, ignore
    }
  }, [api]);

  useEffect(() => {
    fetchEntries();
  }, [fetchEntries]);

  useEffect(() => {
    fetchVMRs();
    fetchEndpoints();
  }, [fetchVMRs, fetchEndpoints]);

  useEffect(() => {
    fetchSummary();
  }, [fetchSummary]);

  useEffect(() => {
    setSelectedIds(new Set());
  }, [page, pageSize, filters]);

  const handleViewDetail = async (entry) => {
    setSelectedEntry(entry);
    setShowDetail(true);
    setDetailLoading(true);
    try {
      const [detail, analytics] = await Promise.all([
        api.getRequestHistoryDetail(entry.Id),
        api.getRequestHistoryAnalytics(entry.Id).catch(() => null)
      ]);
      setDetailData(detail);
      setDetailAnalytics(analytics);
    } catch (err) {
      setError('Failed to fetch detail: ' + err.message);
      setDetailData(null);
      setDetailAnalytics(null);
    } finally {
      setDetailLoading(false);
    }
  };

  const handleViewJson = async (entry) => {
    try {
      const detail = await api.getRequestHistoryDetail(entry.Id);
      setJsonModalData(detail);
      setShowJsonModal(true);
    } catch (err) {
      setError('Failed to fetch detail: ' + err.message);
    }
  };

  const handleDeleteClick = (entry) => {
    setSelectedEntry(entry);
    setShowDeleteConfirm(true);
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteRequestHistoryEntry(selectedEntry.Id);
      setShowDeleteConfirm(false);
      setSelectedEntry(null);
      fetchEntries();
    } catch (err) {
      setError('Failed to delete entry: ' + err.message);
    } finally {
      setDeleteLoading(false);
    }
  };

  const handleBulkDelete = async () => {
    try {
      setBulkDeleteLoading(true);
      const params = Object.fromEntries(
        Object.entries(filters).filter(([_, v]) => v !== '')
      );
      const result = await api.bulkDeleteRequestHistory(params);
      setShowBulkDeleteConfirm(false);
      setError(null);
      setDeleteResult({
        title: 'Request History Deleted',
        message: `Deleted ${result.DeletedCount} entries matching the active filters.`
      });
      fetchEntries();
    } catch (err) {
      setError('Failed to bulk delete: ' + err.message);
    } finally {
      setBulkDeleteLoading(false);
    }
  };

  const handleToggleSelect = (id, checked) => {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  };

  const handleToggleSelectPage = (checked) => {
    setSelectedIds((current) => {
      const next = new Set(current);
      entries.forEach((entry) => {
        if (checked) {
          next.add(entry.Id);
        } else {
          next.delete(entry.Id);
        }
      });
      return next;
    });
  };

  const handleDeleteSelected = async () => {
    try {
      setSelectedDeleteLoading(true);
      const ids = Array.from(selectedIds);
      const result = await api.deleteRequestHistoryEntries(ids);
      setShowSelectedDeleteConfirm(false);
      setSelectedIds(new Set());
      setError(null);
      setDeleteResult({
        title: 'Selected Entries Deleted',
        message: `Deleted ${result.DeletedCount} selected ${result.DeletedCount === 1 ? 'entry' : 'entries'}.`
      });
      fetchEntries();
      fetchSummary();
    } catch (err) {
      setError('Failed to delete selected entries: ' + err.message);
    } finally {
      setSelectedDeleteLoading(false);
    }
  };

  const handleFilterChange = (key, value) => {
    setFilters({ ...filters, [key]: value });
    setPage(1);
    setPageInput('1');
  };

  const handleClearFilters = () => {
    setFilters({
      vmrGuid: '',
      endpointGuid: '',
      requestorUserGuid: '',
      credentialGuid: '',
      loadBalancingPolicyGuid: '',
      modelAccessPolicyGuid: '',
      modelAccessRuleGuid: '',
      modelAccessDecision: '',
      modelAccessWouldDeny: '',
      modelName: '',
      mutationSummary: '',
      denialReasonCode: '',
      reservationGuid: '',
      reservationDecision: '',
      reservationReasonCode: '',
      sessionAffinityOutcome: '',
      selectionStrategy: '',
      endpointGroupGuid: '',
      backoffReason: '',
      adaptiveSelection: '',
      policyFallbackUsed: '',
      statusClass: '',
      createdAfterUtc: '',
      createdBeforeUtc: '',
      sourceIp: '',
      httpStatus: ''
    });
    setPage(1);
    setPageInput('1');
  };

  const formatDate = (dateString) => {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleString();
  };

  const getStatusClass = (status) => {
    if (!status) return '';
    if (status >= 200 && status < 300) return 'status-success';
    if (status >= 400 && status < 500) return 'status-warning';
    if (status >= 500) return 'status-error';
    return '';
  };

  const getTransferTypeLabel = (type) => {
    switch (type) {
      case 1: return 'Chunked';
      case 2: return 'Server-Sent Events';
      default: return 'Normal';
    }
  };

  const getTransferTypeBadge = (type) => {
    if (!type || type === 0) return null;
    const label = type === 1 ? 'Chunked' : 'SSE';
    return <span className="badge badge-info">{label}</span>;
  };

  const formatBoolean = (value) => {
    if (value === true) return 'Yes';
    if (value === false) return 'No';
    return '-';
  };

  const renderAnalyticsTimeline = () => {
    const events = detailAnalytics?.Events || [];
    if (!detailAnalytics && !detailData?.AnalyticsCaptured) {
      return null;
    }

    const maxDuration = Math.max(1, ...events.map(event => event.DurationMs || 0));

    return (
      <div className="detail-section">
        <h3 title="Request analytics captured across Conductor routing, upstream provider handling, and streaming lifecycle stages">Performance</h3>
        <div className="detail-grid detail-grid-two">
          <DetailItem label="Trace" tooltip="Trace identifier that correlates this request history entry with analytics events, logs, and provider metadata">
            {detailData.TraceId ? <CopyableId value={detailData.TraceId} /> : '-'}
          </DetailItem>
          <DetailItem label="Provider" tooltip="Normalized upstream provider family inferred from the selected endpoint or provider response">
            {detailData.ProviderName || '-'}
          </DetailItem>
          <DetailItem label="Provider Request" tooltip="Provider-native request identifier captured from safe response headers or response body when available">
            {detailData.ProviderRequestId ? <CopyableId value={detailData.ProviderRequestId} /> : '-'}
          </DetailItem>
          <DetailItem label="Tokens" tooltip="Total provider-reported prompt and completion token count when the provider returned usage metrics">
            {detailData.TotalTokens != null ? `${detailData.TotalTokens} total` : '-'}
          </DetailItem>
          <DetailItem label="TPS" tooltip="Generation tokens per second when available, otherwise overall tokens per second for the request">
            {detailData.TokensPerSecondGeneration ?? detailData.TokensPerSecondOverall ?? '-'}
          </DetailItem>
          <DetailItem label="Dominant Stage" tooltip="Longest captured analytics stage, or the analytics failure code if detailed capture was unavailable">
            {detailData.DominantStageKind || detailData.AnalyticsFailureCode || '-'}
          </DetailItem>
        </div>
        <div className="analytics-stage-list modal-list">
          {events.length < 1 ? (
            <div className="analytics-empty" title="Detailed analytics were enabled for this request, but no per-stage timing events were retained">No stage events were recorded for this request.</div>
          ) : events.map(event => (
            <div
              className="analytics-stage-row"
              key={event.Id || `${event.Sequence}-${event.StageKind}`}
              title="One captured request analytics stage. The bar shows this stage duration relative to the slowest recorded stage for this request."
            >
              <div className="analytics-stage-label" title="Stage name, stable stage kind, and success state captured during request processing">
                <strong title="Human-readable analytics stage name">{event.StageName || event.StageKind}</strong>
                <span title="Stable analytics stage kind and whether the stage completed successfully">{event.StageKind} / {event.Success ? 'success' : 'failed'}</span>
              </div>
              <div className="analytics-stage-track" title="Relative duration bar compared with the slowest captured stage in this request">
                <div className="analytics-stage-fill" style={{ width: `${Math.max(2, ((event.DurationMs || 0) / maxDuration) * 100)}%` }} />
              </div>
              <span className="analytics-stage-total" title="Measured duration for this individual analytics stage">{event.DurationMs != null ? `${event.DurationMs} ms` : '-'}</span>
            </div>
          ))}
        </div>
      </div>
    );
  };

  const getVmrName = (id) => {
    const vmr = virtualModelRunners.find(v => v.Id === id);
    return vmr ? vmr.Name : id || '-';
  };

  const getEndpointName = (id) => {
    const ep = endpoints.find(e => e.Id === id);
    return ep ? ep.Name : id || '-';
  };

  const selectedCount = selectedIds.size;
  const allPageSelected = entries.length > 0 && entries.every(entry => selectedIds.has(entry.Id));
  const somePageSelected = entries.some(entry => selectedIds.has(entry.Id));

  const columns = [
    {
      key: 'selection',
      label: '',
      tooltip: 'Select request history entries on this page',
      width: '44px',
      sortable: false,
      filterable: false,
      headerRender: () => (
        <input
          type="checkbox"
          checked={allPageSelected}
          ref={(input) => {
            if (input) {
              input.indeterminate = !allPageSelected && somePageSelected;
            }
          }}
          onChange={(event) => handleToggleSelectPage(event.target.checked)}
          title="Select all request history entries on this page"
        />
      ),
      render: (item) => (
        <input
          type="checkbox"
          checked={selectedIds.has(item.Id)}
          onChange={(event) => handleToggleSelect(item.Id, event.target.checked)}
          title={`Select request history entry ${item.Id}`}
        />
      )
    },
    {
      key: 'Id',
      label: 'ID',
      tooltip: 'Unique identifier for this request history entry',
      width: '340px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'CreatedUtc',
      label: 'Time',
      tooltip: 'When the request was received',
      width: '180px',
      render: (item) => formatDate(item.CreatedUtc)
    },
    {
      key: 'HttpMethod',
      label: 'Method',
      tooltip: 'HTTP method used',
      width: '80px'
    },
    {
      key: 'HttpStatus',
      label: 'Status',
      tooltip: 'HTTP response status code',
      width: '80px',
      render: (item) => (
        <span className={`http-status ${getStatusClass(item.HttpStatus)}`}>
          {item.HttpStatus || '-'}
        </span>
      )
    },
      {
        key: 'ResponseTransferType',
        label: 'Transfer',
        tooltip: 'Response transfer type (Normal, Chunked, or SSE)',
        width: '90px',
        render: (item) => getTransferTypeBadge(item.ResponseTransferType) || <span className="text-muted">Normal</span>
      },
    {
      key: 'ResponseTimeMs',
      label: 'Time (ms)',
      tooltip: 'Response time in milliseconds',
      width: '100px',
      render: (item) => item.ResponseTimeMs != null ? `${item.ResponseTimeMs}` : '-'
    },
    {
      key: 'FirstTokenTimeMs',
      label: 'TTFT (ms)',
      tooltip: 'Time to first token/byte in milliseconds. For non-streaming responses, this matches response time.',
      width: '100px',
      render: (item) => item.FirstTokenTimeMs != null ? `${item.FirstTokenTimeMs}` : '-'
    },
    {
      key: 'VirtualModelRunnerName',
      label: 'VMR',
      tooltip: 'Virtual Model Runner that handled this request',
      render: (item) => item.VirtualModelRunnerName || getVmrName(item.VirtualModelRunnerGuid)
    },
    {
      key: 'ModelEndpointName',
      label: 'Endpoint',
      tooltip: 'Model Runner Endpoint that processed this request',
      render: (item) => item.ModelEndpointName || getEndpointName(item.ModelEndpointGuid) || '-'
    },
    {
      key: 'SelectionStrategy',
      label: 'Routing',
      tooltip: 'Endpoint selection strategy, endpoint group, and adaptive routing evidence recorded for this request',
      width: '190px',
      render: (item) => (
        <div className="stacked-cell">
          <span>{item.SelectionStrategy || '-'}</span>
          {item.EndpointGroupGuid && <span className="text-muted">{item.EndpointGroupName || item.EndpointGroupGuid}</span>}
          <div className="summary-badge-row compact">
            {item.AdaptiveSelection && <span className="service-state-badge neutral" title="Adaptive scoring was used for this request">Adaptive</span>}
            {item.PolicyFallbackUsed && <span className="service-state-badge warning" title="Load-balancing policy fallback routing was used">Fallback</span>}
            {item.BackoffReason && <span className="service-state-badge warning" title="Backoff evidence captured during routing">{item.BackoffReason}</span>}
          </div>
        </div>
      ),
      filterValue: (item) => [
        item.SelectionStrategy,
        item.EndpointGroupName,
        item.EndpointGroupGuid,
        item.BackoffReason,
        item.AdaptiveSelection ? 'adaptive' : '',
        item.PolicyFallbackUsed ? 'fallback' : ''
      ].filter(Boolean).join(' ')
    },
    {
      key: 'ReservationName',
      label: 'Reservation',
      tooltip: 'VMR reservation gate that applied to this request, when one was active or in drain.',
      width: '180px',
      render: (item) => item.ReservationGuid
        ? (
          <div className="stacked-cell">
            <span>{item.ReservationName || item.ReservationGuid}</span>
            <span className={`service-state-badge ${item.ReservationDecision === 'Denied' ? 'danger' : 'neutral'}`}>
              {item.ReservationReasonCode || item.ReservationDecision || 'Applied'}
            </span>
          </div>
        )
        : <span className="text-muted">-</span>,
      filterValue: (item) => item.ReservationReasonCode || item.ReservationDecision || 'none'
    },
    {
      key: 'ModelAccessDecision',
      label: 'Access',
      tooltip: 'Model access policy decision recorded for this request',
      width: '110px',
      render: (item) => item.ModelAccessDecision
        ? (
          <span className={`service-state-badge ${item.ModelAccessDecision === 'Deny' ? 'warning' : 'success'}`}>
            {item.ModelAccessWouldDeny ? 'Would Deny' : item.ModelAccessDecision}
          </span>
        )
        : <span className="text-muted">-</span>,
      filterValue: (item) => item.ModelAccessWouldDeny ? 'would-deny' : item.ModelAccessDecision || 'none'
    },
    {
      key: 'RequestorSourceIp',
      label: 'Source IP',
      tooltip: 'IP address of the requestor',
      width: '130px'
    },
    {
      key: 'actions',
      label: 'Actions',
      width: '80px',
      sortable: false,
      filterable: false,
      isAction: true,
      render: (item) => (
        <ActionMenu
          actions={[
            { label: 'View Details', onClick: () => handleViewDetail(item) },
            { label: 'View JSON', onClick: () => handleViewJson(item) },
            { divider: true },
            { label: 'Delete', danger: true, onClick: () => handleDeleteClick(item) }
          ]}
        />
      )
    }
  ];

  const hasFilters = Object.values(filters).some(v => v !== '');

  return (
    <div className="view-container">
      <div className="view-header">
        <div>
          <h1>Request History</h1>
          <p className="view-subtitle">View and filter recent API requests including routing decisions, response details, and token usage.</p>
        </div>
        <div className="view-actions">
          <RefreshButton onClick={fetchEntries} title="Refresh request history" disabled={loading} />
          {hasFilters && (
            <button
              className="btn-secondary btn-danger"
              onClick={() => setShowBulkDeleteConfirm(true)}
              title="Delete all entries matching current filters"
            >
              Bulk Delete
            </button>
          )}
        </div>
        </div>

        <div className="dashboard-section compact">
          <div className="request-history-chart-header">
            <h2>Ledger Summary</h2>
            <RefreshButton onClick={fetchSummary} title="Refresh summary" disabled={summaryLoading} />
          </div>
          {summary && (
            <div className="facet-grid">
              <div className="facet-card">
                <strong>Status Classes</strong>
                {formatFacetEntries(summary.StatusClassCounts, DEFAULT_STATUS_CLASS_COUNTS).map(([key, value]) => (
                  <div className="facet-row" key={`status-class-${key}`}><span>{key}</span><span>{value}</span></div>
                ))}
              </div>
              <div className="facet-card">
                <strong>Denial Reasons</strong>
                {formatFacetEntries(summary.DenialReasonCounts, DEFAULT_DENIAL_REASON_COUNTS).slice(0, 6).map(([key, value]) => (
                  <div className="facet-row" key={`denial-${key}`}><span>{key}</span><span>{value}</span></div>
                ))}
              </div>
              <div className="facet-card">
                <strong>Session Affinity</strong>
                {formatFacetEntries(summary.SessionAffinityOutcomeCounts, DEFAULT_SESSION_AFFINITY_COUNTS).map(([key, value]) => (
                  <div className="facet-row" key={`session-${key}`}><span>{key}</span><span>{value}</span></div>
                ))}
              </div>
            </div>
          )}
        </div>

        <div className="filter-bar">
        <div className="filter-group">
          <label>VMR:</label>
          <select
            value={filters.vmrGuid}
            onChange={(e) => handleFilterChange('vmrGuid', e.target.value)}
          >
            <option value="">All</option>
            {virtualModelRunners.map(vmr => (
              <option key={vmr.Id} value={vmr.Id}>{vmr.Name}</option>
            ))}
          </select>
        </div>
        <div className="filter-group">
          <label>Endpoint:</label>
          <select
            value={filters.endpointGuid}
            onChange={(e) => handleFilterChange('endpointGuid', e.target.value)}
          >
            <option value="">All</option>
            {endpoints.map(ep => (
              <option key={ep.Id} value={ep.Id}>{ep.Name}</option>
            ))}
          </select>
        </div>
          <div className="filter-group">
            <label>Source IP:</label>
            <input
            type="text"
            value={filters.sourceIp}
            onChange={(e) => handleFilterChange('sourceIp', e.target.value)}
            placeholder="Filter by IP"
          />
        </div>
          <div className="filter-group">
            <label>User:</label>
            <input
              type="text"
              value={filters.requestorUserGuid}
              onChange={(e) => handleFilterChange('requestorUserGuid', e.target.value)}
              placeholder="User GUID"
            />
          </div>
          <div className="filter-group">
            <label>Credential:</label>
            <input
              type="text"
              value={filters.credentialGuid}
              onChange={(e) => handleFilterChange('credentialGuid', e.target.value)}
              placeholder="Credential GUID"
            />
          </div>
          <div className="filter-group">
            <label>Model:</label>
            <input
              type="text"
              value={filters.modelName}
              onChange={(e) => handleFilterChange('modelName', e.target.value)}
              placeholder="Requested/effective model"
            />
          </div>
          <div className="filter-group">
            <label>Mutation:</label>
            <input
              type="text"
              value={filters.mutationSummary}
              onChange={(e) => handleFilterChange('mutationSummary', e.target.value)}
              placeholder="temperature, max_tokens..."
            />
          </div>
          <div className="filter-group">
            <label>Status:</label>
            <select
              value={filters.httpStatus}
            onChange={(e) => handleFilterChange('httpStatus', e.target.value)}
            >
              <option value="">All</option>
              <option value="200">200</option>
            <option value="400">400</option>
            <option value="401">401</option>
            <option value="403">403</option>
            <option value="404">404</option>
            <option value="500">500</option>
              <option value="502">502</option>
            </select>
          </div>
          <div className="filter-group">
            <label>Class:</label>
            <select
              value={filters.statusClass}
              onChange={(e) => handleFilterChange('statusClass', e.target.value)}
            >
              <option value="">All</option>
              <option value="2xx">2xx</option>
              <option value="4xx">4xx</option>
              <option value="5xx">5xx</option>
              <option value="NoStatus">No Status</option>
            </select>
          </div>
          <div className="filter-group">
            <label>Denied:</label>
            <input
              type="text"
              value={filters.denialReasonCode}
              onChange={(e) => handleFilterChange('denialReasonCode', e.target.value)}
              placeholder="AllEndpointsAtCapacity"
            />
          </div>
          <div className="filter-group">
            <label>Reservation:</label>
            <input
              type="text"
              value={filters.reservationGuid}
              onChange={(e) => handleFilterChange('reservationGuid', e.target.value)}
              placeholder="vmrrsv_..."
            />
          </div>
          <div className="filter-group">
            <label>Res Decision:</label>
            <select
              value={filters.reservationDecision}
              onChange={(e) => handleFilterChange('reservationDecision', e.target.value)}
            >
              <option value="">All</option>
              <option value="Allowed">Allowed</option>
              <option value="Denied">Denied</option>
              <option value="NoReservation">No Reservation</option>
              <option value="Conflict">Conflict</option>
            </select>
          </div>
          <div className="filter-group">
            <label>Res Reason:</label>
            <input
              type="text"
              value={filters.reservationReasonCode}
              onChange={(e) => handleFilterChange('reservationReasonCode', e.target.value)}
              placeholder="ReservationDenied"
            />
          </div>
          <div className="filter-group">
            <label>Access Policy:</label>
            <input
              type="text"
              value={filters.modelAccessPolicyGuid}
              onChange={(e) => handleFilterChange('modelAccessPolicyGuid', e.target.value)}
              placeholder="map_..."
            />
          </div>
          <div className="filter-group">
            <label>Access Rule:</label>
            <input
              type="text"
              value={filters.modelAccessRuleGuid}
              onChange={(e) => handleFilterChange('modelAccessRuleGuid', e.target.value)}
              placeholder="mar_..."
            />
          </div>
          <div className="filter-group">
            <label>Access:</label>
            <select
              value={filters.modelAccessDecision}
              onChange={(e) => handleFilterChange('modelAccessDecision', e.target.value)}
            >
              <option value="">All</option>
              <option value="Permit">Permit</option>
              <option value="Deny">Deny</option>
            </select>
          </div>
          <div className="filter-group">
            <label>Would Deny:</label>
            <select
              value={filters.modelAccessWouldDeny}
              onChange={(e) => handleFilterChange('modelAccessWouldDeny', e.target.value)}
            >
              <option value="">All</option>
              <option value="true">Yes</option>
              <option value="false">No</option>
            </select>
          </div>
          <div className="filter-group">
            <label>Affinity:</label>
            <input
              type="text"
              value={filters.sessionAffinityOutcome}
              onChange={(e) => handleFilterChange('sessionAffinityOutcome', e.target.value)}
              placeholder="Hit, Miss, Created..."
            />
          </div>
          <div className="filter-group">
            <label>Strategy:</label>
            <input
              type="text"
              value={filters.selectionStrategy}
              onChange={(e) => handleFilterChange('selectionStrategy', e.target.value)}
              placeholder="Adaptive, LeastRecentlyUsed..."
            />
          </div>
          <div className="filter-group">
            <label>Group:</label>
            <input
              type="text"
              value={filters.endpointGroupGuid}
              onChange={(e) => handleFilterChange('endpointGroupGuid', e.target.value)}
              placeholder="Endpoint group GUID"
            />
          </div>
          <div className="filter-group">
            <label>Backoff:</label>
            <input
              type="text"
              value={filters.backoffReason}
              onChange={(e) => handleFilterChange('backoffReason', e.target.value)}
              placeholder="RateLimited, Failure..."
            />
          </div>
          <div className="filter-group">
            <label>Adaptive:</label>
            <select
              value={filters.adaptiveSelection}
              onChange={(e) => handleFilterChange('adaptiveSelection', e.target.value)}
            >
              <option value="">All</option>
              <option value="true">Yes</option>
              <option value="false">No</option>
            </select>
          </div>
          <div className="filter-group">
            <label>Fallback:</label>
            <select
              value={filters.policyFallbackUsed}
              onChange={(e) => handleFilterChange('policyFallbackUsed', e.target.value)}
            >
              <option value="">All</option>
              <option value="true">Yes</option>
              <option value="false">No</option>
            </select>
          </div>
          <div className="filter-group">
            <label>Created After:</label>
            <input
              type="datetime-local"
              value={toLocalDateTimeValue(filters.createdAfterUtc)}
              onChange={(e) => handleFilterChange('createdAfterUtc', e.target.value ? new Date(e.target.value).toISOString() : '')}
            />
          </div>
          <div className="filter-group">
            <label>Created Before:</label>
            <input
              type="datetime-local"
              value={toLocalDateTimeValue(filters.createdBeforeUtc)}
              onChange={(e) => handleFilterChange('createdBeforeUtc', e.target.value ? new Date(e.target.value).toISOString() : '')}
            />
          </div>
          {hasFilters && (
            <button className="btn-link" onClick={handleClearFilters}>
              Clear Filters
          </button>
        )}
      </div>

      <div className="pagination">
        <div className="pagination-info">
          Showing {entries.length === 0 ? 0 : ((page - 1) * pageSize) + 1} to{' '}
          {Math.min(page * pageSize, totalCount)} of {totalCount} entries
        </div>

        <div className="pagination-controls">
          {selectedCount > 0 && (
            <button
              className="btn-icon request-history-selected-delete"
              onClick={() => setShowSelectedDeleteConfirm(true)}
              title={`Delete ${selectedCount} selected request history ${selectedCount === 1 ? 'entry' : 'entries'}`}
            >
              <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2v10a2 2 0 002 2h8a2 2 0 002-2V6a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zM7 8a1 1 0 012 0v6a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z" clipRule="evenodd" />
              </svg>
            </button>
          )}
          <select
            value={pageSize}
            onChange={(e) => {
              setPageSize(Number(e.target.value));
              setPage(1);
              setPageInput('1');
            }}
          >
            <option value={10}>10</option>
            <option value={25}>25</option>
            <option value={50}>50</option>
            <option value={100}>100</option>
          </select>

          <button onClick={() => { setPage(1); setPageInput('1'); }} disabled={page <= 1}>
            First
          </button>
          <button onClick={() => { setPage(p => Math.max(1, p - 1)); setPageInput(String(Math.max(1, page - 1))); }} disabled={page <= 1}>
            Prev
          </button>

          <span className="page-input-container">
            Page{' '}
            <input
              type="text"
              value={pageInput}
              onChange={(e) => setPageInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  const pageNum = parseInt(pageInput, 10);
                  if (!isNaN(pageNum) && pageNum >= 1 && pageNum <= totalPages) {
                    setPage(pageNum);
                  } else {
                    setPageInput(String(page));
                  }
                }
              }}
              className="page-input"
            />{' '}
            of {totalPages}
          </span>

          <button onClick={() => { setPage(p => Math.min(totalPages, p + 1)); setPageInput(String(Math.min(totalPages, page + 1))); }} disabled={page >= totalPages}>
            Next
          </button>
          <button onClick={() => { setPage(totalPages); setPageInput(String(totalPages)); }} disabled={page >= totalPages}>
            Last
          </button>
        </div>
      </div>

      <DataTable data={entries} columns={columns} loading={loading} pageSize={pageSize} hidePagination={true} onRowClick={handleViewDetail} />

      <Modal
        isOpen={Boolean(requestHistoryIssue)}
        onClose={() => setRequestHistoryIssue(null)}
        title="Request History Unavailable"
      >
        <div className="request-history-issue-modal">
          <p>
            Conductor could not load request history because the server returned
            {' '}<strong>HTTP {requestHistoryIssue?.status || 404}</strong>{' '}
            for <code>{requestHistoryIssue?.endpoint || '/v1.0/requesthistory'}</code>.
          </p>
          <p>
            This usually means request history is disabled globally in the server settings,
            even if the Virtual Model Runner has request history enabled.
          </p>
          <div className="request-history-issue-callout">
            <code>"RequestHistory": &#123; "Enabled": true &#125;</code>
          </div>
          <p>
            Check <code>conductor.json</code>, enable <code>RequestHistory.Enabled</code>,
            restart the server, and retry this page. The per-VMR
            {' '}<code>RequestHistoryEnabled</code>{' '}setting must also remain enabled.
          </p>
          <div className="request-history-issue-actions">
            <button className="btn-secondary" onClick={() => setRequestHistoryIssue(null)}>
              Close
            </button>
            <button
              className="btn-primary"
              onClick={() => {
                setRequestHistoryIssue(null);
                fetchEntries();
              }}
            >
              Retry
            </button>
          </div>
        </div>
      </Modal>

      <Modal
        isOpen={showDetail}
        onClose={() => { setShowDetail(false); setDetailData(null); setDetailAnalytics(null); }}
        onDelete={detailData ? () => {
          setShowDetail(false);
          setSelectedEntry(detailData);
          setShowDeleteConfirm(true);
        } : undefined}
        title="Request History Detail"
        extraWide
      >
        {detailLoading ? (
          <div className="loading-spinner">Loading...</div>
        ) : detailData ? (
          <div className="detail-content">
            <div className="detail-section">
              <div className="detail-header-row">
                <div className="detail-id" title="Primary request history record ID used for support lookup, API queries, and correlation with retained request details">
                  <label>ID:</label>
                  <code>{detailData.Id}</code>
                  <CopyButton value={detailData.Id} title="Copy ID" />
                </div>
              </div>
              <div className="detail-grid detail-grid-three">
                <DetailItem label="Time" tooltip="When Conductor received the request">
                  {formatDate(detailData.CreatedUtc)}
                </DetailItem>
                <DetailItem label="Completed" tooltip="When Conductor finished processing the request">
                  {formatDate(detailData.CompletedUtc)}
                </DetailItem>
                <DetailItem label="Response Time" tooltip="Total elapsed request duration measured by Conductor">
                  {detailData.ResponseTimeMs != null ? `${detailData.ResponseTimeMs} ms` : '-'}
                </DetailItem>
                <DetailItem label="TTFT" tooltip="Time to first token or first response byte; for non-streaming responses this can match total response time">
                  {detailData.FirstTokenTimeMs != null ? `${detailData.FirstTokenTimeMs} ms` : '-'}
                </DetailItem>
                <DetailItem label="Source IP" tooltip="Client source IP address recorded for the request">
                  {detailData.RequestorSourceIp || '-'}
                </DetailItem>
                <DetailItem label="HTTP Status" tooltip="Final HTTP status code returned to the caller">
                  <span className={`http-status ${getStatusClass(detailData.HttpStatus)}`}>
                    {detailData.HttpStatus || '-'}
                  </span>
                </DetailItem>
                <DetailItem label="Request Transfer" tooltip="Request body transfer mode observed by Conductor">
                  {getTransferTypeLabel(detailData.RequestTransferType)}
                </DetailItem>
                <DetailItem label="Response Transfer" tooltip="Response transfer mode returned by Conductor, including streaming responses">
                  {getTransferTypeLabel(detailData.ResponseTransferType)}
                </DetailItem>
                <DetailItem label="URL" tooltip="HTTP method and request URL path that reached Conductor">
                  <code>{detailData.HttpMethod} {detailData.HttpUrl}</code>
                </DetailItem>
              </div>
            </div>

              {renderAnalyticsTimeline()}

              <div className="detail-section">
                <h3 title="Routing metadata showing how Conductor matched the request to a VMR, endpoint, model, and policy outcome">Routing</h3>
                <div className="detail-grid detail-grid-three">
                  <DetailItem label="VMR" tooltip="Virtual Model Runner that matched this request">
                    {detailData.VirtualModelRunnerName || detailData.VirtualModelRunnerGuid || '-'}
                  </DetailItem>
                  <DetailItem label="Endpoint" tooltip="Model Runner Endpoint selected by routing">
                    {detailData.ModelEndpointName || detailData.ModelEndpointGuid || '-'}
                  </DetailItem>
                  <DetailItem label="Model Definition" tooltip="Model definition associated with the routed model, when captured">
                    {detailData.ModelDefinitionName || detailData.ModelDefinitionGuid || '-'}
                  </DetailItem>
                  <DetailItem label="Endpoint URL" tooltip="Upstream endpoint URL selected for the request">
                    {detailData.ModelEndpointUrl || '-'}
                  </DetailItem>
                  <DetailItem label="Requested Model" tooltip="Model name requested by the caller before Conductor mapping or mutation">
                    {detailData.RequestedModel || '-'}
                  </DetailItem>
                  <DetailItem label="Effective Model" tooltip="Final upstream model name after VMR mapping and request mutation">
                    {detailData.EffectiveModel || '-'}
                  </DetailItem>
                  <DetailItem label="Policy" tooltip="Load-balancing policy used during routing, if one was attached">
                    {detailData.LoadBalancingPolicyName || detailData.LoadBalancingPolicyGuid || '-'}
                  </DetailItem>
                  <DetailItem label="Selection Strategy" tooltip="Endpoint selection strategy recorded for this request">
                    {detailData.SelectionStrategy || '-'}
                  </DetailItem>
                  <DetailItem label="Endpoint Group" tooltip="Endpoint group selected by weighted group routing, when one applied">
                    {detailData.EndpointGroupGuid
                      ? (
                        <>
                          {detailData.EndpointGroupName || 'Endpoint Group'} <CopyableId value={detailData.EndpointGroupGuid} />
                        </>
                      )
                      : '-'}
                  </DetailItem>
                  <DetailItem label="Adaptive Selection" tooltip="Whether adaptive scoring was used when this request selected an endpoint">
                    {formatBoolean(detailData.AdaptiveSelection)}
                  </DetailItem>
                  <DetailItem label="Policy Fallback" tooltip="Whether fallback routing was used after a load-balancing policy could not select an endpoint">
                    {formatBoolean(detailData.PolicyFallbackUsed)}
                  </DetailItem>
                  <DetailItem label="Backoff Reason" tooltip="Transient backoff reason captured by routing, when an endpoint was avoided or marked unhealthy">
                    {detailData.BackoffReason || '-'}
                  </DetailItem>
                  <DetailItem label="Access Policy" tooltip="Model access policy evaluated for this request, if one was attached">
                    {detailData.ModelAccessPolicyName || detailData.ModelAccessPolicyGuid || '-'}
                  </DetailItem>
                  <DetailItem label="Access Rule" tooltip="Model access rule that matched the request, or Default when the policy default decided it">
                    {detailData.ModelAccessRuleName || detailData.ModelAccessRuleGuid || (detailData.ModelAccessDecision ? 'Default' : '-')}
                  </DetailItem>
                  <DetailItem label="Access Decision" tooltip="Model access decision recorded by the routing engine">
                    {detailData.ModelAccessDecision
                      ? `${detailData.ModelAccessDecision}${detailData.ModelAccessWouldDeny ? ' (would deny)' : ''}`
                      : '-'}
                  </DetailItem>
                  <DetailItem label="Request Type" tooltip="Conductor request type resolved from the HTTP method and URL path">
                    {detailData.RequestType || '-'}
                  </DetailItem>
                  <DetailItem label="Outcome" tooltip="Stable routing outcome code recorded by the routing decision">
                    {detailData.RoutingOutcomeCode || '-'}
                  </DetailItem>
                  <DetailItem label="Denied Because" tooltip="Routing denial reason when Conductor could not select an endpoint">
                    {detailData.DenialReasonCode || detailData.DenialReason || '-'}
                  </DetailItem>
                  <DetailItem label="Reservation" tooltip="VMR reservation gate that applied to this request, when active or in drain">
                    {detailData.ReservationGuid
                      ? (
                        <>
                          {detailData.ReservationName || 'Reservation'} <CopyableId value={detailData.ReservationGuid} />
                        </>
                      )
                      : '-'}
                  </DetailItem>
                  <DetailItem label="Reservation Decision" tooltip="Reservation gate decision captured before model access and endpoint selection">
                    {detailData.ReservationDecision || '-'}
                  </DetailItem>
                  <DetailItem label="Reservation Reason" tooltip="Stable reservation reason code, such as ReservationDenied or ReservationDrainDenied">
                    {detailData.ReservationReasonCode || '-'}
                  </DetailItem>
                  <DetailItem label="Reservation Window" tooltip="UTC reservation window that was evaluated for this request">
                    {detailData.ReservationWindowStartUtc || detailData.ReservationWindowEndUtc
                      ? `${formatDate(detailData.ReservationWindowStartUtc)} to ${formatDate(detailData.ReservationWindowEndUtc)}`
                      : '-'}
                  </DetailItem>
                  <DetailItem label="Session Affinity" tooltip="Session affinity outcome such as hit, miss, created, expired, or disabled">
                    {detailData.SessionAffinityOutcome || '-'}
                  </DetailItem>
                  <DetailItem label="Mutation Summary" tooltip="Summary of request model or parameter mutations applied before proxying">
                    {detailData.MutationSummary || '-'}
                  </DetailItem>
                  <DetailItem label="Explanation" tooltip="Compact explanation of the routing decision">
                    {detailData.ExplanationSummary || '-'}
                  </DetailItem>
                </div>

                {detailData.RoutingDecision && (
                  <div className="explain-detail-panel">
                    <div className="summary-badge-row compact">
                      <span className={`service-state-badge ${detailData.RoutingDecision.Success ? 'success' : 'danger'}`} title="Overall routing result showing whether Conductor selected an endpoint or denied the request">
                        {detailData.RoutingDecision.Success ? 'Routed' : 'Denied'}
                      </span>
                      <span className="service-state-badge neutral" title="HTTP status code Conductor associated with the routing decision">HTTP {detailData.RoutingDecision.HttpStatusCode}</span>
                      {detailData.RoutingDecision.SelectionStrategy && <span className="service-state-badge neutral" title="Endpoint selection strategy recorded by this routing decision">{detailData.RoutingDecision.SelectionStrategy}</span>}
                      {detailData.RoutingDecision.SelectedAdaptiveScore != null && <span className="service-state-badge neutral" title="Adaptive score selected for the chosen endpoint">Score {detailData.RoutingDecision.SelectedAdaptiveScore}</span>}
                      {detailData.RoutingDecision.BackoffReason && <span className="service-state-badge warning" title="Transient backoff reason recorded by this routing decision">{detailData.RoutingDecision.BackoffReason}</span>}
                      {detailData.RoutingDecision.PolicyFallbackUsed && <span className="service-state-badge warning" title="The attached load-balancing policy could not select a route, so Conductor used fallback routing">Policy Fallback</span>}
                      {detailData.RoutingDecision.ModelAccessDecision && <span className={`service-state-badge ${detailData.RoutingDecision.ModelAccessDecision === 'Deny' ? 'warning' : 'success'}`} title="Model access policy decision recorded for this routing decision">Access {detailData.RoutingDecision.ModelAccessDecision}</span>}
                      {detailData.RoutingDecision.ModelAccessWouldDeny && <span className="service-state-badge warning" title="Monitor mode allowed the request but would deny it in enforce mode">Would Deny</span>}
                    </div>

                    <div className="detail-table-container">
                      <table className="detail-table explanation-table">
                        <thead>
                          <tr>
                            <th title="Name of the routing decision step that was evaluated">Title</th>
                            <th title="Explanation recorded by the routing engine for this decision step">Description</th>
                            <th title="Outcome of the routing decision step, such as passed, denied, fallback, or unknown">Status</th>
                          </tr>
                        </thead>
                        <tbody>
                          {(detailData.RoutingDecision.Timeline || []).length > 0 ? (
                            (detailData.RoutingDecision.Timeline || []).map((stage, index) => (
                              <tr key={`detail-stage-${index}`} title="One routing decision step recorded while Conductor evaluated endpoint eligibility and selected or denied a route">
                                <td className="detail-table-title-cell" title="Name of the routing decision step that was evaluated">{stage.Title || '-'}</td>
                                <td title="Explanation recorded by the routing engine for this decision step">{stage.Message || '-'}</td>
                                <td className="detail-table-status-cell" title="Outcome of this routing decision step">
                                  <span className={`service-state-badge ${getStageOutcomeTone(stage.Outcome)}`} title="Routing step outcome code recorded for this decision point">
                                    {stage.Outcome || 'Unknown'}
                                  </span>
                                </td>
                              </tr>
                            ))
                          ) : (
                            <tr>
                              <td colSpan="3" className="detail-table-empty-cell" title="No per-step routing timeline was captured for this request">No routing explanation steps recorded.</td>
                            </tr>
                          )}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}
              </div>

              <div className="detail-section">
                <h3 title="Captured caller request data after Conductor applies configured retention, truncation, and redaction rules">Request</h3>
                <CollapsibleSection
                  title="Headers"
                  meta={detailData.RequestHeadersRedacted ? 'Redacted' : null}
                  content={detailData.RequestHeaders || {}}
                  defaultExpanded={false}
                  tooltip="HTTP request headers captured from the caller after Conductor redaction. Sensitive headers may be masked or omitted."
                />
                <CollapsibleSection
                  title="Body"
                  meta={`${detailData.RequestBodyLength || 0} bytes · ${getRetentionLabel(detailData.RequestBodyRetained, detailData.RequestBodyRedacted, detailData.RequestBodyTruncated).label}`}
                  content={detailData.RequestBody}
                  defaultExpanded={false}
                  showFormatJson={true}
                  tooltip="HTTP request body captured from the caller when body retention is enabled. The metadata indicates retained, redacted, truncated, or metadata-only storage."
              />
            </div>

              <div className="detail-section">
                <h3 title="Captured response data after Conductor receives the upstream response and applies retention, truncation, and redaction rules">Response</h3>
                <CollapsibleSection
                  title="Headers"
                  meta={detailData.ResponseHeadersRedacted ? 'Redacted' : null}
                  content={detailData.ResponseHeaders || {}}
                  defaultExpanded={false}
                  tooltip="HTTP response headers captured from the upstream provider or Conductor response after redaction. Sensitive headers may be masked or omitted."
                />
                <CollapsibleSection
                  title="Body"
                  meta={`${detailData.ResponseBodyLength || 0} bytes · ${getRetentionLabel(detailData.ResponseBodyRetained, detailData.ResponseBodyRedacted, detailData.ResponseBodyTruncated).label}`}
                  content={detailData.ResponseBody}
                  defaultExpanded={false}
                  showFormatJson={true}
                  tooltip="HTTP response body captured when response retention and size limits allow it. The metadata indicates retained, redacted, truncated, or metadata-only storage."
              />
            </div>
          </div>
        ) : (
          <p>No detail available</p>
        )}
      </Modal>

      {showJsonModal && jsonModalData && (
        <ViewMetadataModal
          data={jsonModalData}
          title="Request History JSON"
          subtitle={jsonModalData.Id}
          onClose={() => { setShowJsonModal(false); setJsonModalData(null); }}
        />
      )}

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedEntry?.Id}
        entityType="request history entry"
        loading={deleteLoading}
      />

      <DeleteConfirmModal
        isOpen={showBulkDeleteConfirm}
        onClose={() => setShowBulkDeleteConfirm(false)}
        onConfirm={handleBulkDelete}
        entityName={`${totalCount} entries matching filters`}
        entityType="request history entries"
        loading={bulkDeleteLoading}
      />

      <DeleteConfirmModal
        isOpen={showSelectedDeleteConfirm}
        onClose={() => setShowSelectedDeleteConfirm(false)}
        onConfirm={handleDeleteSelected}
        entityName={`${selectedCount} selected ${selectedCount === 1 ? 'entry' : 'entries'}`}
        entityType="request history entries"
        loading={selectedDeleteLoading}
      />

      <Modal
        isOpen={Boolean(deleteResult)}
        onClose={() => setDeleteResult(null)}
        title={deleteResult?.title || 'Request History Deleted'}
      >
        <div className="request-history-delete-result-modal">
          <p>{deleteResult?.message}</p>
          <div className="request-history-issue-actions">
            <button className="btn-primary" onClick={() => setDeleteResult(null)}>
              Close
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

export default RequestHistory;
