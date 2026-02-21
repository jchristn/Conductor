import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { useApp } from '../context/AppContext';
import { useOnboarding } from '../context/OnboardingContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';

function ModelRunnerEndpoints() {
  const { api, setError } = useApp();
  const { pendingCreate, clearPendingCreate, onEntityCreated } = useOnboarding();
  const [endpoints, setEndpoints] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedEndpoint, setSelectedEndpoint] = useState(null);
  const [showMetadata, setShowMetadata] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [healthData, setHealthData] = useState({});
  const [showHealthDetail, setShowHealthDetail] = useState(false);
  const [healthDetailEndpoint, setHealthDetailEndpoint] = useState(null);
  const [formData, setFormData] = useState({
    TenantId: '',
    Name: '',
    Hostname: '',
    Port: 11434,
    ApiKey: '',
    ApiType: 'Ollama',
    UseSsl: false,
    TimeoutMs: 300000,
    Active: true,
    LabelsJson: '[]',
    TagsJson: '{}',
    HealthCheckUrl: '/',
    HealthCheckMethod: 'GET',
    HealthCheckIntervalMs: 5000,
    HealthCheckTimeoutMs: 5000,
    HealthCheckExpectedStatusCode: 200,
    UnhealthyThreshold: 2,
    HealthyThreshold: 2,
    HealthCheckUseAuth: false,
    MaxParallelRequests: 4,
    Weight: 1
  });

  const fetchEndpoints = useCallback(async () => {
    try {
      setLoading(true);
      const result = await api.listModelRunnerEndpoints({ maxResults: 1000 });
      setEndpoints(result.Data || []);
    } catch (err) {
      setError('Failed to fetch endpoints: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError]);

  const fetchTenants = useCallback(async () => {
    try {
      const result = await api.listTenants({ maxResults: 1000 });
      setTenants(result.Data || []);
    } catch (err) {
      setError('Failed to fetch tenants: ' + err.message);
    }
  }, [api, setError]);

  const fetchHealth = useCallback(async () => {
    try {
      const result = await api.getModelRunnerEndpointsHealth();
      const healthMap = {};

      // Handle both raw array and wrapped response formats
      let healthList = null;
      if (Array.isArray(result)) {
        healthList = result;
      } else if (result && Array.isArray(result.Data)) {
        healthList = result.Data;
      } else if (result && Array.isArray(result.Endpoints)) {
        healthList = result.Endpoints;
      }

      if (healthList) {
        healthList.forEach(h => {
          const id = h.EndpointId || h.endpointId;
          if (id) healthMap[id] = h;
        });
      }

      setHealthData(healthMap);
    } catch {
      // Health data is supplementary; silently ignore errors
    }
  }, [api]);

  useEffect(() => {
    fetchEndpoints();
    fetchTenants();
  }, [fetchEndpoints, fetchTenants]);

  useEffect(() => {
    if (pendingCreate === 'endpoint') {
      clearPendingCreate();
      handleCreate();
    }
  }, [pendingCreate]);

  useEffect(() => {
    fetchHealth();
    const interval = setInterval(fetchHealth, 15000);
    return () => clearInterval(interval);
  }, [fetchHealth]);

  const formatDuration = (ms) => {
    if (!ms) return '-';
    const seconds = Math.floor(ms / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    if (days > 0) return `${days}d ${hours % 24}h`;
    if (hours > 0) return `${hours}h ${minutes % 60}m`;
    if (minutes > 0) return `${minutes}m ${seconds % 60}s`;
    return `${seconds}s`;
  };

  const getUptimeColor = (pct) => {
    if (pct >= 99) return 'var(--success-color)';
    if (pct >= 95) return 'var(--warning-color)';
    return 'var(--danger-color)';
  };

  const healthSummary = useMemo(() => {
    const activeEndpoints = endpoints.filter(e => e.Active);
    let healthy = 0, unhealthy = 0, pending = 0;
    activeEndpoints.forEach(ep => {
      const h = healthData[ep.Id];
      if (!h) { pending++; }
      else if (h.IsHealthy) { healthy++; }
      else { unhealthy++; }
    });
    return { healthy, unhealthy, pending, total: activeEndpoints.length };
  }, [endpoints, healthData]);

  const handleViewHealth = (endpoint) => {
    setHealthDetailEndpoint(endpoint);
    setShowHealthDetail(true);
  };

  // Parse a URL or hostname string and extract components
  const parseEndpointUrl = (input) => {
    if (!input) return null;

    const trimmed = input.trim();

    // Check if it looks like a URL (contains :// or starts with a protocol)
    const urlMatch = trimmed.match(/^(https?):\/\/([^/:]+)(?::(\d+))?(\/.*)?$/i);
    if (urlMatch) {
      const protocol = urlMatch[1].toLowerCase();
      const hostname = urlMatch[2];
      const port = urlMatch[3] ? parseInt(urlMatch[3]) : (protocol === 'https' ? 443 : 80);
      const useSsl = protocol === 'https';
      return { hostname, port, useSsl };
    }

    // Check for hostname:port format (no protocol)
    const hostPortMatch = trimmed.match(/^([^/:]+):(\d+)$/);
    if (hostPortMatch) {
      return {
        hostname: hostPortMatch[1],
        port: parseInt(hostPortMatch[2]),
        useSsl: null // Keep existing SSL setting
      };
    }

    return null;
  };

  // Handle hostname input changes with auto-parsing
  const handleHostnameChange = (e) => {
    const value = e.target.value;
    const parsed = parseEndpointUrl(value);

    if (parsed) {
      // User entered a URL, auto-populate fields
      const updates = {
        ...formData,
        Hostname: parsed.hostname,
        Port: parsed.port
      };
      if (parsed.useSsl !== null) {
        updates.UseSsl = parsed.useSsl;
      }
      // Set defaults for api.openai.com
      if (parsed.hostname.toLowerCase() === 'api.openai.com') {
        updates.HealthCheckUseAuth = true;
        updates.HealthCheckUrl = '/v1/models';
      }
      setFormData(updates);
    } else {
      // Just a plain hostname, update as-is
      const updates = { ...formData, Hostname: value };
      // Set defaults for api.openai.com
      if (value.toLowerCase() === 'api.openai.com') {
        updates.HealthCheckUseAuth = true;
        updates.HealthCheckUrl = '/v1/models';
      }
      setFormData(updates);
    }
  };

  const handleCreate = () => {
    setEditMode(false);
    setFormData({
      TenantId: '',
      Name: '',
      Hostname: '',
      Port: 11434,
      ApiKey: '',
      ApiType: 'Ollama',
      UseSsl: false,
      TimeoutMs: 300000,
      Active: true,
      LabelsJson: '[]',
      TagsJson: '{}',
      HealthCheckUrl: '/',
      HealthCheckMethod: 'GET',
      HealthCheckIntervalMs: 5000,
      HealthCheckTimeoutMs: 5000,
      HealthCheckExpectedStatusCode: 200,
      UnhealthyThreshold: 2,
      HealthyThreshold: 2,
      HealthCheckUseAuth: false,
      MaxParallelRequests: 4,
      Weight: 1
    });
    setShowForm(true);
  };

  const handleEdit = (endpoint) => {
    setEditMode(true);
    setSelectedEndpoint(endpoint);
    setFormData({
      TenantId: endpoint.TenantId || '',
      Name: endpoint.Name || '',
      Hostname: endpoint.Hostname || '',
      Port: endpoint.Port || 11434,
      ApiKey: endpoint.ApiKey || '',
      ApiType: endpoint.ApiType || 'Ollama',
      UseSsl: endpoint.UseSsl || false,
      TimeoutMs: endpoint.TimeoutMs || 300000,
      Active: endpoint.Active !== false,
      LabelsJson: JSON.stringify(endpoint.Labels || [], null, 2),
      TagsJson: JSON.stringify(endpoint.Tags || {}, null, 2),
      HealthCheckUrl: endpoint.HealthCheckUrl || '/',
      HealthCheckMethod: endpoint.HealthCheckMethod || 'GET',
      HealthCheckIntervalMs: endpoint.HealthCheckIntervalMs || 5000,
      HealthCheckTimeoutMs: endpoint.HealthCheckTimeoutMs || 5000,
      HealthCheckExpectedStatusCode: endpoint.HealthCheckExpectedStatusCode || 200,
      UnhealthyThreshold: endpoint.UnhealthyThreshold || 2,
      HealthyThreshold: endpoint.HealthyThreshold || 2,
      HealthCheckUseAuth: endpoint.HealthCheckUseAuth || false,
      MaxParallelRequests: endpoint.MaxParallelRequests ?? 4,
      Weight: endpoint.Weight || 1
    });
    setShowForm(true);
  };

  const handleViewMetadata = (endpoint) => {
    setSelectedEndpoint(endpoint);
    setShowMetadata(true);
  };

  const handleDeleteClick = (endpoint) => {
    setSelectedEndpoint(endpoint);
    setShowDeleteConfirm(true);
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteModelRunnerEndpoint(selectedEndpoint.Id, selectedEndpoint.TenantId);
      setShowDeleteConfirm(false);
      setSelectedEndpoint(null);
      fetchEndpoints();
    } catch (err) {
      setError('Failed to delete endpoint: ' + err.message);
    } finally {
      setDeleteLoading(false);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      let labels = [];
      let tags = {};

      try {
        labels = JSON.parse(formData.LabelsJson || '[]');
      } catch (err) {
        setError('Invalid JSON in Labels');
        return;
      }

      try {
        tags = JSON.parse(formData.TagsJson || '{}');
      } catch (err) {
        setError('Invalid JSON in Tags');
        return;
      }

      const data = {
        TenantId: formData.TenantId || null,
        Name: formData.Name,
        Hostname: formData.Hostname,
        Port: parseInt(formData.Port),
        ApiKey: formData.ApiKey,
        ApiType: formData.ApiType,
        UseSsl: formData.UseSsl,
        TimeoutMs: parseInt(formData.TimeoutMs),
        Active: formData.Active,
        Labels: labels,
        Tags: tags,
        HealthCheckUrl: formData.HealthCheckUrl,
        HealthCheckMethod: formData.HealthCheckMethod,
        HealthCheckIntervalMs: parseInt(formData.HealthCheckIntervalMs),
        HealthCheckTimeoutMs: parseInt(formData.HealthCheckTimeoutMs),
        HealthCheckExpectedStatusCode: parseInt(formData.HealthCheckExpectedStatusCode),
        UnhealthyThreshold: parseInt(formData.UnhealthyThreshold),
        HealthyThreshold: parseInt(formData.HealthyThreshold),
        HealthCheckUseAuth: formData.HealthCheckUseAuth,
        MaxParallelRequests: parseInt(formData.MaxParallelRequests),
        Weight: parseInt(formData.Weight)
      };

      if (editMode) {
        await api.updateModelRunnerEndpoint(selectedEndpoint.Id, data);
      } else {
        await api.createModelRunnerEndpoint(data);
        onEntityCreated('endpoint');
      }
      setShowForm(false);
      fetchEndpoints();
    } catch (err) {
      setError('Failed to save endpoint: ' + err.message);
    }
  };

  const formatDate = (dateString) => {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleString();
  };

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      tooltip: 'Unique identifier for this endpoint',
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Name',
      label: 'Name',
      tooltip: 'Display name for this endpoint'
    },
    {
      key: 'Endpoint',
      label: 'Endpoint',
      tooltip: 'URL of the model runner backend',
      render: (item) => `${item.UseSsl ? 'https' : 'http'}://${item.Hostname}:${item.Port}`,
      filterValue: (item) => `${item.Hostname}:${item.Port}`
    },
    {
      key: 'ApiType',
      label: 'API Type',
      tooltip: 'API format used by this endpoint (Ollama or OpenAI compatible)',
      width: '100px'
    },
    {
      key: 'Active',
      label: 'Status',
      tooltip: 'Whether this endpoint is enabled for use in load balancing',
      width: '100px',
      render: (item) => (
        <span title={item.Active ? 'Endpoint is enabled and available for routing' : 'Endpoint is disabled and excluded from routing'}>
          <StatusIndicator active={item.Active} />
        </span>
      ),
      filterValue: (item) => item.Active ? 'active' : 'inactive'
    },
    {
      key: 'Health',
      label: 'Health',
      tooltip: 'Live health check result from periodic monitoring',
      width: '110px',
      render: (item) => {
        if (!item.Active) return <span className="status-badge pending" title="Inactive endpoints are not health-checked">Inactive</span>;
        const h = healthData[item.Id];
        if (!h) return <span className="status-badge pending" title="Awaiting first health check result">Awaiting Check</span>;
        return h.IsHealthy
          ? <span className="status-badge healthy" title={`Healthy since ${h.LastHealthyUtc ? new Date(h.LastHealthyUtc).toLocaleString() : 'unknown'}`}>Healthy</span>
          : <span className="status-badge unhealthy" title={h.LastError || `Unhealthy since ${h.LastUnhealthyUtc ? new Date(h.LastUnhealthyUtc).toLocaleString() : 'unknown'}`}>Unhealthy</span>;
      },
      filterValue: (item) => {
        if (!item.Active) return 'inactive';
        const h = healthData[item.Id];
        if (!h) return 'awaiting';
        return h.IsHealthy ? 'healthy' : 'unhealthy';
      }
    },
    {
      key: 'Uptime',
      label: 'Uptime',
      tooltip: 'Percentage of time this endpoint has been healthy since monitoring started',
      width: '90px',
      render: (item) => {
        const h = healthData[item.Id];
        if (!h || !item.Active) return <span style={{ color: 'var(--text-secondary)' }} title="No uptime data available">-</span>;
        const pct = h.UptimePercentage;
        return (
          <span
            style={{ color: getUptimeColor(pct), fontWeight: 500 }}
            title={`${pct.toFixed(2)}% uptime (${formatDuration(h.TotalUptimeMs)} up / ${formatDuration(h.TotalDowntimeMs)} down)`}
          >
            {pct.toFixed(1)}%
          </span>
        );
      },
      sortValue: (item) => {
        const h = healthData[item.Id];
        return h ? h.UptimePercentage : -1;
      },
      filterValue: (item) => {
        const h = healthData[item.Id];
        return h ? `${h.UptimePercentage.toFixed(1)}%` : '';
      }
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
            { label: 'View Details', onClick: () => handleViewMetadata(item) },
            { label: 'View Health', onClick: () => handleViewHealth(item) },
            { label: 'Edit', onClick: () => handleEdit(item) },
            { divider: true },
            { label: 'Delete', danger: true, onClick: () => handleDeleteClick(item) }
          ]}
        />
      )
    }
  ];

  return (
    <div className="view-container">
      <div className="view-header">
        <h1>Model Runner Endpoints</h1>
        <div className="view-actions">
          <button className="btn-icon" onClick={() => { fetchEndpoints(); fetchHealth(); }} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
          <button className="btn-primary" onClick={handleCreate}>
            Create Endpoint
          </button>
        </div>
      </div>

      {!loading && endpoints.length > 0 && (
        <div className="health-summary-banner" title="Aggregate health status of all active endpoints based on periodic health checks">
          <div className="health-summary-counts">
            <span className="health-count healthy" title="Endpoints currently passing health checks">
              <span className="health-count-dot healthy"></span>
              {healthSummary.healthy} Healthy
            </span>
            <span className="health-count unhealthy" title="Endpoints currently failing health checks">
              <span className="health-count-dot unhealthy"></span>
              {healthSummary.unhealthy} Unhealthy
            </span>
            {healthSummary.pending > 0 && (
              <span className="health-count pending" title="Active endpoints awaiting their first health check result">
                <span className="health-count-dot pending"></span>
                {healthSummary.pending} Awaiting Check
              </span>
            )}
          </div>
          <span style={{ fontSize: '13px', color: 'var(--text-secondary)' }} title="Total number of endpoints with Active status enabled">
            {healthSummary.total} active endpoint{healthSummary.total !== 1 ? 's' : ''}
          </span>
        </div>
      )}

      <DataTable data={endpoints} columns={columns} loading={loading} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Endpoint' : 'Create Endpoint'} wide>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="tenantId" title="Organizational unit that owns this endpoint">Tenant</label>
            <select
              id="tenantId"
              value={formData.TenantId}
              onChange={(e) => setFormData({ ...formData, TenantId: e.target.value })}
            >
              <option value="">-- No Tenant --</option>
              {tenants.map((tenant) => (
                <option key={tenant.Id} value={tenant.Id}>
                  {tenant.Name} ({tenant.Id})
                </option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="name" title="Display name for this endpoint">Name</label>
            <input
              type="text"
              id="name"
              value={formData.Name}
              onChange={(e) => setFormData({ ...formData, Name: e.target.value })}
              required
            />
          </div>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="hostname" title="Paste a full URL to auto-fill hostname, port, and SSL. Do not use localhost if running in Docker.">Hostname</label>
              <input
                type="text"
                id="hostname"
                value={formData.Hostname}
                onChange={handleHostnameChange}
                placeholder="api.openai.com or https://api.openai.com"
                required
              />
            </div>
            <div className="form-group" style={{ maxWidth: '120px' }}>
              <label htmlFor="port" title="TCP port number (1-65535)">Port</label>
              <input
                type="number"
                id="port"
                value={formData.Port}
                onChange={(e) => setFormData({ ...formData, Port: e.target.value })}
                min="1"
                max="65535"
                required
              />
            </div>
          </div>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="apiType" title="API format used by this endpoint (Ollama or OpenAI compatible)">API Type</label>
              <select
                id="apiType"
                value={formData.ApiType}
                onChange={(e) => setFormData({ ...formData, ApiType: e.target.value })}
              >
                <option value="Ollama">Ollama</option>
                <option value="OpenAI">OpenAI</option>
              </select>
            </div>
            <div className="form-group">
              <label htmlFor="timeoutMs" title="Maximum time in milliseconds to wait for response">Timeout (ms)</label>
              <input
                type="number"
                id="timeoutMs"
                value={formData.TimeoutMs}
                onChange={(e) => setFormData({ ...formData, TimeoutMs: e.target.value })}
                min="1000"
                step="1000"
              />
            </div>
          </div>
          <div className="form-group">
            <label htmlFor="apiKey" title="Bearer token for authenticated endpoints">API Key (optional)</label>
            <input
              type="password"
              id="apiKey"
              value={formData.ApiKey}
              onChange={(e) => setFormData({ ...formData, ApiKey: e.target.value })}
              placeholder="Leave blank if not required"
            />
          </div>

          <h3 style={{ marginTop: '24px', marginBottom: '16px', borderBottom: '1px solid var(--border-color)', paddingBottom: '8px' }}>Health Check Configuration</h3>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="healthCheckUrl" title="Path to check endpoint health (e.g., / or /v1/models)">Health Check URL</label>
              <input
                type="text"
                id="healthCheckUrl"
                value={formData.HealthCheckUrl}
                onChange={(e) => setFormData({ ...formData, HealthCheckUrl: e.target.value })}
                placeholder="/"
              />
            </div>
            <div className="form-group" style={{ maxWidth: '120px' }}>
              <label htmlFor="healthCheckMethod" title="HTTP method for health check requests">Method</label>
              <select
                id="healthCheckMethod"
                value={formData.HealthCheckMethod}
                onChange={(e) => setFormData({ ...formData, HealthCheckMethod: e.target.value })}
              >
                <option value="GET">GET</option>
                <option value="HEAD">HEAD</option>
              </select>
            </div>
            <div className="form-group" style={{ maxWidth: '100px' }}>
              <label htmlFor="healthCheckExpectedStatusCode" title="Expected HTTP status code for a healthy response">Status</label>
              <input
                type="number"
                id="healthCheckExpectedStatusCode"
                value={formData.HealthCheckExpectedStatusCode}
                onChange={(e) => setFormData({ ...formData, HealthCheckExpectedStatusCode: e.target.value })}
                min="100"
                max="599"
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="healthCheckIntervalMs" title="Time in milliseconds between health checks">Interval (ms)</label>
              <input
                type="number"
                id="healthCheckIntervalMs"
                value={formData.HealthCheckIntervalMs}
                onChange={(e) => setFormData({ ...formData, HealthCheckIntervalMs: e.target.value })}
                min="1000"
                step="1000"
              />
            </div>
            <div className="form-group">
              <label htmlFor="healthCheckTimeoutMs" title="Maximum time to wait for health check response">Timeout (ms)</label>
              <input
                type="number"
                id="healthCheckTimeoutMs"
                value={formData.HealthCheckTimeoutMs}
                onChange={(e) => setFormData({ ...formData, HealthCheckTimeoutMs: e.target.value })}
                min="1000"
                step="1000"
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="healthyThreshold" title="Number of consecutive successful checks to mark endpoint as healthy">Healthy Threshold</label>
              <input
                type="number"
                id="healthyThreshold"
                value={formData.HealthyThreshold}
                onChange={(e) => setFormData({ ...formData, HealthyThreshold: e.target.value })}
                min="1"
              />
            </div>
            <div className="form-group">
              <label htmlFor="unhealthyThreshold" title="Number of consecutive failed checks to mark endpoint as unhealthy">Unhealthy Threshold</label>
              <input
                type="number"
                id="unhealthyThreshold"
                value={formData.UnhealthyThreshold}
                onChange={(e) => setFormData({ ...formData, UnhealthyThreshold: e.target.value })}
                min="1"
              />
            </div>
          </div>

          <div className="form-group checkbox-group">
            <label title="Include API key in health check requests (required for OpenAI API)">
              <input
                type="checkbox"
                checked={formData.HealthCheckUseAuth}
                onChange={(e) => setFormData({ ...formData, HealthCheckUseAuth: e.target.checked })}
              />
              Use Authentication for Health Checks
            </label>
          </div>

          <h3 style={{ marginTop: '24px', marginBottom: '16px', borderBottom: '1px solid var(--border-color)', paddingBottom: '8px' }}>Rate Limiting & Load Balancing</h3>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="maxParallelRequests" title="Maximum concurrent requests to this endpoint (0 = unlimited)">Max Parallel Requests</label>
              <input
                type="number"
                id="maxParallelRequests"
                value={formData.MaxParallelRequests}
                onChange={(e) => setFormData({ ...formData, MaxParallelRequests: e.target.value })}
                min="0"
              />
            </div>
            <div className="form-group">
              <label htmlFor="weight" title="Relative weight for load balancing - higher values receive proportionally more traffic">Weight (1-1000)</label>
              <input
                type="number"
                id="weight"
                value={formData.Weight}
                onChange={(e) => setFormData({ ...formData, Weight: e.target.value })}
                min="1"
                max="1000"
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="labels" title="String array for categorization and filtering">Labels (JSON)</label>
            <textarea
              id="labels"
              value={formData.LabelsJson}
              onChange={(e) => setFormData({ ...formData, LabelsJson: e.target.value })}
              rows={4}
              className="code-input"
              placeholder="[]"
            />
          </div>

          <div className="form-group">
            <label htmlFor="tags" title="Key-value pairs for custom metadata">Tags (JSON)</label>
            <textarea
              id="tags"
              value={formData.TagsJson}
              onChange={(e) => setFormData({ ...formData, TagsJson: e.target.value })}
              rows={4}
              className="code-input"
              placeholder="{}"
            />
          </div>

          <div className="form-group checkbox-group">
            <label title="Connect using HTTPS instead of HTTP">
              <input
                type="checkbox"
                checked={formData.UseSsl}
                onChange={(e) => setFormData({ ...formData, UseSsl: e.target.checked })}
              />
              Use SSL
            </label>
          </div>

          <div className="form-group checkbox-group">
            <label title="Inactive endpoints are excluded from load balancing">
              <input
                type="checkbox"
                checked={formData.Active}
                onChange={(e) => setFormData({ ...formData, Active: e.target.checked })}
              />
              Active
            </label>
          </div>
          <div className="form-actions">
            <button type="button" className="btn-secondary" onClick={() => setShowForm(false)}>
              Cancel
            </button>
            <button type="submit" className="btn-primary">
              {editMode ? 'Update' : 'Create'}
            </button>
          </div>
        </form>
      </Modal>

      {showMetadata && selectedEndpoint && (
        <ViewMetadataModal
          data={selectedEndpoint}
          title="Endpoint Details"
          subtitle={selectedEndpoint.Id}
          onClose={() => setShowMetadata(false)}
        />
      )}

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedEndpoint?.Name}
        entityType="endpoint"
        loading={deleteLoading}
      />

      <Modal
        isOpen={showHealthDetail}
        onClose={() => { setShowHealthDetail(false); setHealthDetailEndpoint(null); }}
        title="Endpoint Health Details"
        wide
      >
        {healthDetailEndpoint && (() => {
          const h = healthData[healthDetailEndpoint.Id];
          const isActive = healthDetailEndpoint.Active;
          return (
            <div className="health-modal">
              <div className="health-header">
                <div className="health-summary">
                  <div>
                    <h3>{healthDetailEndpoint.Name}</h3>
                    <div style={{ fontSize: '13px', color: 'var(--text-secondary)', fontFamily: "'Monaco', 'Menlo', monospace" }}>
                      {healthDetailEndpoint.UseSsl ? 'https' : 'http'}://{healthDetailEndpoint.Hostname}:{healthDetailEndpoint.Port}
                    </div>
                  </div>
                  {h ? (
                    <span
                      className={`health-badge ${h.IsHealthy ? 'healthy' : 'unhealthy'}`}
                      title={h.IsHealthy
                        ? `Passing health checks since ${h.LastHealthyUtc ? new Date(h.LastHealthyUtc).toLocaleString() : 'unknown'}`
                        : `Failing health checks since ${h.LastUnhealthyUtc ? new Date(h.LastUnhealthyUtc).toLocaleString() : 'unknown'}`}
                    >
                      {h.IsHealthy ? 'Healthy' : 'Unhealthy'}
                    </span>
                  ) : isActive ? (
                    <span className="health-badge pending" title="This endpoint is active and being monitored, but no health check results have been received yet">
                      Awaiting First Check
                    </span>
                  ) : (
                    <span className="health-badge pending" title="Inactive endpoints are excluded from health monitoring">
                      Not Monitored
                    </span>
                  )}
                </div>
                <button className="btn-icon" onClick={fetchHealth} title="Refresh health data">
                  <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
                    <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
                  </svg>
                </button>
              </div>

              {!h ? (
                isActive ? (
                  <p className="no-items">Awaiting first health check result. The server performs periodic checks based on the configured interval.</p>
                ) : (
                  <p className="no-items">This endpoint is inactive and is not being health-checked. Enable it to begin monitoring.</p>
                )
              ) : (
                <>
                  <div className="metrics-grid" style={{ marginBottom: '16px' }}>
                    <div className="metric" title="Percentage of time this endpoint has been healthy since monitoring started">
                      <span className="metric-label">Uptime</span>
                      <span className="metric-value" style={{ color: getUptimeColor(h.UptimePercentage) }}>
                        {h.UptimePercentage.toFixed(1)}%
                      </span>
                    </div>
                    <div className="metric" title="Active requests currently being proxied to this endpoint vs. the configured maximum (0 = unlimited)">
                      <span className="metric-label">In-Flight</span>
                      <span className="metric-value">
                        {h.InFlightRequests} / {h.MaxParallelRequests === 0 ? '\u221E' : h.MaxParallelRequests}
                      </span>
                    </div>
                    <div className="metric" title="Relative load balancing weight - higher values receive proportionally more traffic">
                      <span className="metric-label">Weight</span>
                      <span className="metric-value">{h.Weight}</span>
                    </div>
                    <div className="metric" title={`Consecutive successful health checks - endpoint becomes healthy after reaching the configured threshold`}>
                      <span className="metric-label">Consecutive OK</span>
                      <span className="metric-value" style={{ color: h.ConsecutiveSuccesses > 0 ? 'var(--success-color)' : 'var(--text-primary)' }}>
                        {h.ConsecutiveSuccesses}
                      </span>
                    </div>
                    <div className="metric" title={`Consecutive failed health checks - endpoint becomes unhealthy after reaching the configured threshold`}>
                      <span className="metric-label">Consecutive Fail</span>
                      <span className="metric-value" style={{ color: h.ConsecutiveFailures > 0 ? 'var(--danger-color)' : 'var(--text-primary)' }}>
                        {h.ConsecutiveFailures}
                      </span>
                    </div>
                  </div>

                  <table className="health-table">
                    <tbody>
                      <tr title="When the most recent health check was performed">
                        <td style={{ fontWeight: 500, color: 'var(--text-secondary)', width: '160px' }}>Last Check</td>
                        <td>{h.LastCheckUtc ? new Date(h.LastCheckUtc).toLocaleString() : '-'}</td>
                      </tr>
                      <tr title="When the endpoint last transitioned between healthy and unhealthy states">
                        <td style={{ fontWeight: 500, color: 'var(--text-secondary)' }}>Last State Change</td>
                        <td>{h.LastStateChangeUtc ? new Date(h.LastStateChangeUtc).toLocaleString() : '-'}</td>
                      </tr>
                      <tr title="When the endpoint most recently transitioned to healthy status">
                        <td style={{ fontWeight: 500, color: 'var(--text-secondary)' }}>Last Healthy</td>
                        <td>{h.LastHealthyUtc ? new Date(h.LastHealthyUtc).toLocaleString() : '-'}</td>
                      </tr>
                      <tr title="When the endpoint most recently transitioned to unhealthy status">
                        <td style={{ fontWeight: 500, color: 'var(--text-secondary)' }}>Last Unhealthy</td>
                        <td>{h.LastUnhealthyUtc ? new Date(h.LastUnhealthyUtc).toLocaleString() : '-'}</td>
                      </tr>
                      <tr title="When health monitoring began for this endpoint">
                        <td style={{ fontWeight: 500, color: 'var(--text-secondary)' }}>Monitoring Since</td>
                        <td>{h.FirstCheckUtc ? new Date(h.FirstCheckUtc).toLocaleString() : '-'}</td>
                      </tr>
                      <tr title="Cumulative time the endpoint has been in a healthy state">
                        <td style={{ fontWeight: 500, color: 'var(--text-secondary)' }}>Total Uptime</td>
                        <td>{formatDuration(h.TotalUptimeMs)}</td>
                      </tr>
                      <tr title="Cumulative time the endpoint has been in an unhealthy state">
                        <td style={{ fontWeight: 500, color: 'var(--text-secondary)' }}>Total Downtime</td>
                        <td>{formatDuration(h.TotalDowntimeMs)}</td>
                      </tr>
                    </tbody>
                  </table>

                  {h.LastError && (
                    <div
                      style={{ marginTop: '16px', padding: '12px', background: 'rgba(239, 68, 68, 0.1)', borderRadius: '6px', border: '1px solid rgba(239, 68, 68, 0.3)' }}
                      title="The error message from the most recent failed health check"
                    >
                      <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--danger-color)', marginBottom: '4px' }}>Last Error</div>
                      <div style={{ fontSize: '13px', color: 'var(--danger-color)' }}>{h.LastError}</div>
                    </div>
                  )}
                </>
              )}
            </div>
          );
        })()}
      </Modal>
    </div>
  );
}

export default ModelRunnerEndpoints;
