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

function RequestHistory() {
  const { api, setError } = useApp();
  const [entries, setEntries] = useState([]);
  const [virtualModelRunners, setVirtualModelRunners] = useState([]);
  const [endpoints, setEndpoints] = useState([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(50);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
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

  // Filter state
  const [filters, setFilters] = useState({
    vmrGuid: '',
    endpointGuid: '',
    sourceIp: '',
    httpStatus: ''
  });

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
      setEntries(result.Data || []);
      setTotalPages(result.TotalPages || 1);
      setTotalCount(result.TotalCount || 0);
    } catch (err) {
      setError('Failed to fetch request history: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError, page, pageSize, filters]);

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
    setPage(1); // Reset to first page when filtering
  };

  const handleClearFilters = () => {
    setFilters({
      vmrGuid: '',
      endpointGuid: '',
      sourceIp: '',
      httpStatus: ''
    });
    setPage(1);
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
      key: 'ResponseTimeMs',
      label: 'Time (ms)',
      tooltip: 'Response time in milliseconds',
      width: '100px',
      render: (item) => item.ResponseTimeMs != null ? `${item.ResponseTimeMs}` : '-'
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
        <h1>Request History</h1>
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
        {hasFilters && (
          <button className="btn-link" onClick={handleClearFilters}>
            Clear Filters
          </button>
        )}
      </div>

      <DataTable data={entries} columns={columns} loading={loading} hidePagination={true} />

      <div className="pagination">
        <span className="pagination-info">
          Showing {entries.length} of {totalCount} entries
        </span>
        <div className="pagination-controls">
          <button
            className="btn-secondary"
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page <= 1}
          >
            Previous
          </button>
          <span className="pagination-page">
            Page {page} of {totalPages}
          </span>
          <button
            className="btn-secondary"
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            disabled={page >= totalPages}
          >
            Next
          </button>
        </div>
      </div>

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
              </div>
            </div>

            <div className="detail-section">
              <h3>Request</h3>
              <CollapsibleSection
                title="Headers"
                content={detailData.RequestHeaders || {}}
                defaultExpanded={false}
              />
              <CollapsibleSection
                title="Body"
                meta={`${detailData.RequestBodyLength || 0} bytes${detailData.RequestBodyTruncated ? ' [TRUNCATED]' : ''}`}
                content={detailData.RequestBody}
                defaultExpanded={false}
                showFormatJson={true}
              />
            </div>

            <div className="detail-section">
              <h3>Response</h3>
              <CollapsibleSection
                title="Headers"
                content={detailData.ResponseHeaders || {}}
                defaultExpanded={false}
              />
              <CollapsibleSection
                title="Body"
                meta={`${detailData.ResponseBodyLength || 0} bytes${detailData.ResponseBodyTruncated ? ' [TRUNCATED]' : ''}`}
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
