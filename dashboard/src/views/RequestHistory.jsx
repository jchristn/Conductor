import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import CopyableId from '../components/CopyableId';
import CopyButton from '../components/CopyButton';
import { copyToClipboard } from '../utils/clipboard';

function CollapsibleSection({ title, meta, content, defaultExpanded = false, showFormatJson = false }) {
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
      <div className="collapsible-header" onClick={() => setExpanded(!expanded)}>
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
          <span className="collapsible-title">{title}</span>
          {meta && <span className="collapsible-meta">{meta}</span>}
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
        <div className="collapsible-content">
          <pre>{displayContent || '(empty)'}</pre>
        </div>
      )}
    </div>
  );
}

function formatFacetEntries(facets) {
  return Object.entries(facets || {})
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
  const [detailLoading, setDetailLoading] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [showBulkDeleteConfirm, setShowBulkDeleteConfirm] = useState(false);
  const [bulkDeleteLoading, setBulkDeleteLoading] = useState(false);
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
    modelName: '',
    mutationSummary: '',
    denialReasonCode: '',
    sessionAffinityOutcome: '',
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

  const handleViewDetail = async (entry) => {
    setSelectedEntry(entry);
    setShowDetail(true);
    setDetailLoading(true);
    try {
      const detail = await api.getRequestHistoryDetail(entry.Id);
      setDetailData(detail);
    } catch (err) {
      setError('Failed to fetch detail: ' + err.message);
      setDetailData(null);
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
      alert(`Deleted ${result.DeletedCount} entries`);
      fetchEntries();
    } catch (err) {
      setError('Failed to bulk delete: ' + err.message);
    } finally {
      setBulkDeleteLoading(false);
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
      modelName: '',
      mutationSummary: '',
      denialReasonCode: '',
      sessionAffinityOutcome: '',
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

  const getVmrName = (id) => {
    const vmr = virtualModelRunners.find(v => v.Id === id);
    return vmr ? vmr.Name : id || '-';
  };

  const getEndpointName = (id) => {
    const ep = endpoints.find(e => e.Id === id);
    return ep ? ep.Name : id || '-';
  };

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      tooltip: 'Unique identifier for this request history entry',
      width: '200px',
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
            <button className="btn-icon" onClick={fetchEntries} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
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
            <button className="btn-icon" onClick={fetchSummary} title="Refresh summary" disabled={summaryLoading}>
              <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
              </svg>
            </button>
          </div>
          {summary && (
            <div className="facet-grid">
              <div className="facet-card">
                <strong>Status Classes</strong>
                {formatFacetEntries(summary.StatusClassCounts).map(([key, value]) => (
                  <div className="facet-row" key={`status-class-${key}`}><span>{key}</span><span>{value}</span></div>
                ))}
              </div>
              <div className="facet-card">
                <strong>Denial Reasons</strong>
                {formatFacetEntries(summary.DenialReasonCounts).slice(0, 6).map(([key, value]) => (
                  <div className="facet-row" key={`denial-${key}`}><span>{key}</span><span>{value}</span></div>
                ))}
              </div>
              <div className="facet-card">
                <strong>Session Affinity</strong>
                {formatFacetEntries(summary.SessionAffinityOutcomeCounts).map(([key, value]) => (
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
            <label>Affinity:</label>
            <input
              type="text"
              value={filters.sessionAffinityOutcome}
              onChange={(e) => handleFilterChange('sessionAffinityOutcome', e.target.value)}
              placeholder="Hit, Miss, Created..."
            />
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

      <DataTable data={entries} columns={columns} loading={loading} hidePagination={true} onRowClick={handleViewDetail} />

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
        onClose={() => { setShowDetail(false); setDetailData(null); }}
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
                <div className="detail-id">
                  <label>ID:</label>
                  <code>{detailData.Id}</code>
                  <CopyButton value={detailData.Id} title="Copy ID" />
                </div>
              </div>
              <div className="detail-grid">
                <div className="detail-item">
                  <label>Time:</label>
                  <span>{formatDate(detailData.CreatedUtc)}</span>
                </div>
                <div className="detail-item">
                  <label>Completed:</label>
                  <span>{formatDate(detailData.CompletedUtc)}</span>
                </div>
                <div className="detail-item">
                  <label>Response Time:</label>
                  <span>{detailData.ResponseTimeMs != null ? `${detailData.ResponseTimeMs} ms` : '-'}</span>
                </div>
                <div className="detail-item">
                  <label>TTFT:</label>
                  <span>{detailData.FirstTokenTimeMs != null ? `${detailData.FirstTokenTimeMs} ms` : '-'}</span>
                </div>
                <div className="detail-item">
                  <label>Source IP:</label>
                  <span>{detailData.RequestorSourceIp}</span>
                </div>
                <div className="detail-item">
                  <label>HTTP Status:</label>
                  <span className={`http-status ${getStatusClass(detailData.HttpStatus)}`}>
                    {detailData.HttpStatus || '-'}
                  </span>
                </div>
                <div className="detail-item">
                  <label>Request Transfer:</label>
                  <span>{getTransferTypeLabel(detailData.RequestTransferType)}</span>
                </div>
                <div className="detail-item">
                  <label>Response Transfer:</label>
                  <span>{getTransferTypeLabel(detailData.ResponseTransferType)}</span>
                </div>
                <div className="detail-item">
                  <label>URL:</label>
                  <span><code>{detailData.HttpMethod} {detailData.HttpUrl}</code></span>
                </div>
              </div>
            </div>

              <div className="detail-section">
                <h3>Routing</h3>
                <div className="detail-grid">
                  <div className="detail-item">
                    <label>VMR:</label>
                  <span>{detailData.VirtualModelRunnerName || detailData.VirtualModelRunnerGuid || '-'}</span>
                </div>
                <div className="detail-item">
                  <label>Endpoint:</label>
                  <span>{detailData.ModelEndpointName || detailData.ModelEndpointGuid || '-'}</span>
                </div>
                <div className="detail-item">
                  <label>Model Definition:</label>
                  <span>{detailData.ModelDefinitionName || detailData.ModelDefinitionGuid || '-'}</span>
                </div>
                  <div className="detail-item">
                    <label>Endpoint URL:</label>
                    <span>{detailData.ModelEndpointUrl || '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Requested Model:</label>
                    <span>{detailData.RequestedModel || '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Effective Model:</label>
                    <span>{detailData.EffectiveModel || '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Policy:</label>
                    <span>{detailData.LoadBalancingPolicyName || detailData.LoadBalancingPolicyGuid || '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Request Type:</label>
                    <span>{detailData.RequestType || '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Outcome:</label>
                    <span>{detailData.RoutingOutcomeCode || '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Denied Because:</label>
                    <span>{detailData.DenialReasonCode || detailData.DenialReason || '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Session Affinity:</label>
                    <span>{detailData.SessionAffinityOutcome || '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Mutation Summary:</label>
                    <span>{detailData.MutationSummary || '-'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Explanation:</label>
                    <span>{detailData.ExplanationSummary || '-'}</span>
                  </div>
                </div>

                {detailData.RoutingDecision && (
                  <div className="explain-detail-panel">
                    <div className="summary-badge-row compact">
                      <span className={`service-state-badge ${detailData.RoutingDecision.Success ? 'success' : 'danger'}`}>
                        {detailData.RoutingDecision.Success ? 'Routed' : 'Denied'}
                      </span>
                      <span className="service-state-badge neutral">HTTP {detailData.RoutingDecision.HttpStatusCode}</span>
                      {detailData.RoutingDecision.PolicyFallbackUsed && <span className="service-state-badge warning">Policy Fallback</span>}
                    </div>

                    <div className="detail-table-container">
                      <table className="detail-table explanation-table">
                        <thead>
                          <tr>
                            <th>Title</th>
                            <th>Description</th>
                            <th>Status</th>
                          </tr>
                        </thead>
                        <tbody>
                          {(detailData.RoutingDecision.Timeline || []).length > 0 ? (
                            (detailData.RoutingDecision.Timeline || []).map((stage, index) => (
                              <tr key={`detail-stage-${index}`}>
                                <td className="detail-table-title-cell">{stage.Title || '-'}</td>
                                <td>{stage.Message || '-'}</td>
                                <td className="detail-table-status-cell">
                                  <span className={`service-state-badge ${getStageOutcomeTone(stage.Outcome)}`}>
                                    {stage.Outcome || 'Unknown'}
                                  </span>
                                </td>
                              </tr>
                            ))
                          ) : (
                            <tr>
                              <td colSpan="3" className="detail-table-empty-cell">No routing explanation steps recorded.</td>
                            </tr>
                          )}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}
              </div>

              <div className="detail-section">
                <h3>Request</h3>
                <CollapsibleSection
                  title="Headers"
                  meta={detailData.RequestHeadersRedacted ? 'Redacted' : null}
                  content={detailData.RequestHeaders || {}}
                  defaultExpanded={false}
                />
                <CollapsibleSection
                  title="Body"
                  meta={`${detailData.RequestBodyLength || 0} bytes · ${getRetentionLabel(detailData.RequestBodyRetained, detailData.RequestBodyRedacted, detailData.RequestBodyTruncated).label}`}
                  content={detailData.RequestBody}
                  defaultExpanded={false}
                  showFormatJson={true}
              />
            </div>

              <div className="detail-section">
                <h3>Response</h3>
                <CollapsibleSection
                  title="Headers"
                  meta={detailData.ResponseHeadersRedacted ? 'Redacted' : null}
                  content={detailData.ResponseHeaders || {}}
                  defaultExpanded={false}
                />
                <CollapsibleSection
                  title="Body"
                  meta={`${detailData.ResponseBodyLength || 0} bytes · ${getRetentionLabel(detailData.ResponseBodyRetained, detailData.ResponseBodyRedacted, detailData.ResponseBodyTruncated).label}`}
                  content={detailData.ResponseBody}
                  defaultExpanded={false}
                  showFormatJson={true}
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
    </div>
  );
}

export default RequestHistory;
