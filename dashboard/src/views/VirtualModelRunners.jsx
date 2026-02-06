import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';
import CopyButton from '../components/CopyButton';

function VirtualModelRunners() {
  const { api, setError, serverUrl } = useApp();
  const [vmrs, setVmrs] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [endpoints, setEndpoints] = useState([]);
  const [configurations, setConfigurations] = useState([]);
  const [definitions, setDefinitions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedVmr, setSelectedVmr] = useState(null);
  const [showMetadata, setShowMetadata] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [showHealth, setShowHealth] = useState(false);
  const [healthData, setHealthData] = useState(null);
  const [healthLoading, setHealthLoading] = useState(false);
  const [formData, setFormData] = useState({
    TenantId: '',
    Name: '',
    Hostname: '',
    BasePath: '',
    ApiType: 'OpenAI',
    LoadBalancingMode: 'RoundRobin',
    ModelRunnerEndpointIds: [],
    ModelConfigurationIds: [],
    ModelDefinitionIds: [],
    ModelConfigurationMappingsJson: '{}',
    TimeoutMs: 300000,
    AllowEmbeddings: true,
    AllowCompletions: true,
    AllowModelManagement: false,
    StrictMode: false,
    SessionAffinityMode: 'None',
    SessionAffinityHeader: '',
    SessionTimeoutMs: 600000,
    SessionMaxEntries: 10000,
    Active: true,
    LabelsJson: '[]',
    TagsJson: '{}'
  });

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [vmrResult, tenantsResult, endpointsResult, configurationsResult, definitionsResult] = await Promise.all([
        api.listVirtualModelRunners({ maxResults: 1000 }),
        api.listTenants({ maxResults: 1000 }),
        api.listModelRunnerEndpoints({ maxResults: 1000 }),
        api.listModelConfigurations({ maxResults: 1000 }),
        api.listModelDefinitions({ maxResults: 1000 })
      ]);
      setVmrs(vmrResult.Data || []);
      setTenants(tenantsResult.Data || []);
      setEndpoints(endpointsResult.Data || []);
      setConfigurations(configurationsResult.Data || []);
      setDefinitions(definitionsResult.Data || []);
    } catch (err) {
      setError('Failed to fetch data: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleCreate = () => {
    setEditMode(false);
    setFormData({
      TenantId: '',
      Name: '',
      Hostname: '',
      BasePath: '',
      ApiType: 'OpenAI',
      LoadBalancingMode: 'RoundRobin',
      ModelRunnerEndpointIds: [],
      ModelConfigurationIds: [],
      ModelDefinitionIds: [],
      ModelConfigurationMappingsJson: '{}',
      TimeoutMs: 300000,
      AllowEmbeddings: true,
      AllowCompletions: true,
      AllowModelManagement: false,
      StrictMode: false,
      SessionAffinityMode: 'None',
      SessionAffinityHeader: '',
      SessionTimeoutMs: 600000,
      SessionMaxEntries: 10000,
      Active: true,
      LabelsJson: '[]',
      TagsJson: '{}'
    });
    setShowForm(true);
  };

  const handleEdit = (vmr) => {
    setEditMode(true);
    setSelectedVmr(vmr);
    setFormData({
      TenantId: vmr.TenantId || '',
      Name: vmr.Name || '',
      Hostname: vmr.Hostname || '',
      BasePath: vmr.BasePath || '',
      ApiType: vmr.ApiType || 'OpenAI',
      LoadBalancingMode: vmr.LoadBalancingMode || 'RoundRobin',
      ModelRunnerEndpointIds: vmr.ModelRunnerEndpointIds || [],
      ModelConfigurationIds: vmr.ModelConfigurationIds || [],
      ModelDefinitionIds: vmr.ModelDefinitionIds || [],
      ModelConfigurationMappingsJson: JSON.stringify(vmr.ModelConfigurationMappings || {}, null, 2),
      TimeoutMs: vmr.TimeoutMs || 300000,
      AllowEmbeddings: vmr.AllowEmbeddings !== false,
      AllowCompletions: vmr.AllowCompletions !== false,
      AllowModelManagement: vmr.AllowModelManagement === true,
      StrictMode: vmr.StrictMode === true,
      SessionAffinityMode: vmr.SessionAffinityMode || 'None',
      SessionAffinityHeader: vmr.SessionAffinityHeader || '',
      SessionTimeoutMs: vmr.SessionTimeoutMs || 600000,
      SessionMaxEntries: vmr.SessionMaxEntries || 10000,
      Active: vmr.Active !== false,
      LabelsJson: JSON.stringify(vmr.Labels || [], null, 2),
      TagsJson: JSON.stringify(vmr.Tags || {}, null, 2)
    });
    setShowForm(true);
  };

  const handleViewMetadata = (vmr) => {
    setSelectedVmr(vmr);
    setShowMetadata(true);
  };

  const handleDeleteClick = (vmr) => {
    setSelectedVmr(vmr);
    setShowDeleteConfirm(true);
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteVirtualModelRunner(selectedVmr.Id, selectedVmr.TenantId);
      setShowDeleteConfirm(false);
      setSelectedVmr(null);
      fetchData();
    } catch (err) {
      setError('Failed to delete virtual model runner: ' + err.message);
    } finally {
      setDeleteLoading(false);
    }
  };

  const handleViewHealth = async (vmr) => {
    setSelectedVmr(vmr);
    setShowHealth(true);
    setHealthLoading(true);
    try {
      const health = await api.getVirtualModelRunnerHealth(vmr.Id, vmr.TenantId);
      setHealthData(health);
    } catch (err) {
      setError('Failed to fetch health status: ' + err.message);
    } finally {
      setHealthLoading(false);
    }
  };

  const refreshHealth = async () => {
    if (!selectedVmr) return;
    setHealthLoading(true);
    try {
      const health = await api.getVirtualModelRunnerHealth(selectedVmr.Id, selectedVmr.TenantId);
      setHealthData(health);
    } catch (err) {
      setError('Failed to refresh health status: ' + err.message);
    } finally {
      setHealthLoading(false);
    }
  };

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

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      let labels = [];
      let tags = {};
      let modelConfigurationMappings = {};

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

      try {
        modelConfigurationMappings = JSON.parse(formData.ModelConfigurationMappingsJson || '{}');
      } catch (err) {
        setError('Invalid JSON in Model Configuration Mappings');
        return;
      }

      const data = {
        TenantId: formData.TenantId || null,
        Name: formData.Name,
        Hostname: formData.Hostname || null,
        BasePath: formData.BasePath,
        ApiType: formData.ApiType,
        LoadBalancingMode: formData.LoadBalancingMode,
        ModelRunnerEndpointIds: formData.ModelRunnerEndpointIds,
        ModelConfigurationIds: formData.ModelConfigurationIds,
        ModelDefinitionIds: formData.ModelDefinitionIds,
        ModelConfigurationMappings: modelConfigurationMappings,
        TimeoutMs: parseInt(formData.TimeoutMs),
        AllowEmbeddings: formData.AllowEmbeddings,
        AllowCompletions: formData.AllowCompletions,
        AllowModelManagement: formData.AllowModelManagement,
        StrictMode: formData.StrictMode,
        SessionAffinityMode: formData.SessionAffinityMode,
        SessionAffinityHeader: formData.SessionAffinityHeader || null,
        SessionTimeoutMs: parseInt(formData.SessionTimeoutMs),
        SessionMaxEntries: parseInt(formData.SessionMaxEntries),
        Active: formData.Active,
        Labels: labels,
        Tags: tags
      };

      if (editMode) {
        await api.updateVirtualModelRunner(selectedVmr.Id, data);
      } else {
        await api.createVirtualModelRunner(data);
      }
      setShowForm(false);
      fetchData();
    } catch (err) {
      setError('Failed to save virtual model runner: ' + err.message);
    }
  };

  const handleEndpointToggle = (endpointId) => {
    const current = formData.ModelRunnerEndpointIds || [];
    if (current.includes(endpointId)) {
      setFormData({
        ...formData,
        ModelRunnerEndpointIds: current.filter((id) => id !== endpointId)
      });
    } else {
      setFormData({
        ...formData,
        ModelRunnerEndpointIds: [...current, endpointId]
      });
    }
  };

  const handleConfigurationToggle = (configId) => {
    const current = formData.ModelConfigurationIds || [];
    if (current.includes(configId)) {
      setFormData({
        ...formData,
        ModelConfigurationIds: current.filter((id) => id !== configId)
      });
    } else {
      setFormData({
        ...formData,
        ModelConfigurationIds: [...current, configId]
      });
    }
  };

  const handleDefinitionToggle = (defId) => {
    const current = formData.ModelDefinitionIds || [];
    if (current.includes(defId)) {
      setFormData({
        ...formData,
        ModelDefinitionIds: current.filter((id) => id !== defId)
      });
    } else {
      setFormData({
        ...formData,
        ModelDefinitionIds: [...current, defId]
      });
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
      tooltip: 'Unique identifier for this virtual model runner',
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Name',
      label: 'Name',
      tooltip: 'Display name for this virtual model runner'
    },
    {
      key: 'BasePath',
      label: 'Base Path',
      tooltip: 'URL path prefix used to route requests to this VMR',
      width: '180px',
      render: (item) => (
        <div className="token-cell">
          <code>{item.BasePath}</code>
          <CopyButton value={`${serverUrl}${item.BasePath}`} title="Copy full URL" />
        </div>
      )
    },
    {
      key: 'ApiType',
      label: 'API Type',
      tooltip: 'API format exposed by this VMR (OpenAI or Ollama compatible)',
      width: '100px'
    },
    {
      key: 'LoadBalancingMode',
      label: 'Load Balancing',
      tooltip: 'How requests are distributed across endpoints. Session affinity, if enabled, pins clients to specific endpoints.',
      width: '140px',
      render: (item) => (
        <span>
          {item.LoadBalancingMode}
          {item.SessionAffinityMode && item.SessionAffinityMode !== 'None' && (
            <small style={{ color: 'var(--text-secondary)', marginLeft: '4px' }}>(Pinned)</small>
          )}
        </span>
      )
    },
    {
      key: 'Endpoints',
      label: 'Endpoints',
      tooltip: 'Number of backend model runner endpoints attached to this VMR',
      width: '80px',
      render: (item) => (item.ModelRunnerEndpointIds || []).length,
      sortValue: (item) => (item.ModelRunnerEndpointIds || []).length
    },
    {
      key: 'Active',
      label: 'Status',
      tooltip: 'Whether this VMR is accepting requests',
      width: '120px',
      render: (item) => (
        <span title={item.Active ? 'VMR is active and accepting requests' : 'VMR is inactive and rejecting all requests'}>
          <StatusIndicator active={item.Active} />
        </span>
      ),
      filterValue: (item) => item.Active ? 'active' : 'inactive'
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
        <h1>Virtual Model Runners</h1>
        <div className="view-actions">
          <button className="btn-icon" onClick={fetchData} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
          <button className="btn-primary" onClick={handleCreate}>
            Create VMR
          </button>
        </div>
      </div>

      <DataTable data={vmrs} columns={columns} loading={loading} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Virtual Model Runner' : 'Create Virtual Model Runner'} wide>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="tenantId" title="Organizational unit that owns this VMR and its associated resources">Tenant</label>
            <select
              id="tenantId"
              value={formData.TenantId}
              onChange={(e) => setFormData({
                ...formData,
                TenantId: e.target.value,
                ModelRunnerEndpointIds: [],
                ModelConfigurationIds: [],
                ModelDefinitionIds: []
              })}
            >
              <option value="">-- No Tenant --</option>
              {tenants.map((tenant) => (
                <option key={tenant.Id} value={tenant.Id}>
                  {tenant.Name} ({tenant.Id})
                </option>
              ))}
            </select>
          </div>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="name" title="Display name for this virtual model runner">Name</label>
              <input
                type="text"
                id="name"
                value={formData.Name}
                onChange={(e) => setFormData({ ...formData, Name: e.target.value })}
                required
              />
            </div>
            <div className="form-group">
              <label htmlFor="hostname" title="Route requests based on the Host header instead of base path">Hostname (optional)</label>
              <input
                type="text"
                id="hostname"
                value={formData.Hostname}
                onChange={(e) => setFormData({ ...formData, Hostname: e.target.value })}
                placeholder="For host-based routing"
              />
            </div>
          </div>
          <div className="form-group">
            <label htmlFor="basePath" title="URL path prefix for routing requests to this VMR">Base Path</label>
            <input
              type="text"
              id="basePath"
              value={formData.BasePath}
              onChange={(e) => setFormData({ ...formData, BasePath: e.target.value })}
              placeholder="/v1.0/api/my-vmr/"
              required
            />
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="apiType" title="API format exposed by this VMR (OpenAI or Ollama compatible)">API Type</label>
              <select
                id="apiType"
                value={formData.ApiType}
                onChange={(e) => setFormData({ ...formData, ApiType: e.target.value })}
              >
                <option value="OpenAI">OpenAI</option>
                <option value="Ollama">Ollama</option>
              </select>
            </div>
            <div className="form-group">
              <label htmlFor="loadBalancingMode" title="How requests are distributed across endpoints">Load Balancing Mode</label>
              <select
                id="loadBalancingMode"
                value={formData.LoadBalancingMode}
                onChange={(e) => setFormData({ ...formData, LoadBalancingMode: e.target.value })}
              >
                <option value="RoundRobin">Round Robin</option>
                <option value="Random">Random</option>
                <option value="FirstAvailable">First Available</option>
              </select>
            </div>
            <div className="form-group">
              <label htmlFor="timeoutMs" title="Maximum time in milliseconds to wait for endpoint response">Timeout (ms)</label>
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

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="sessionAffinityMode" title="Pin subsequent requests from the same client to the same backend endpoint to minimize context drops">Session Affinity Mode</label>
              <select
                id="sessionAffinityMode"
                value={formData.SessionAffinityMode}
                onChange={(e) => setFormData({ ...formData, SessionAffinityMode: e.target.value })}
              >
                <option value="None">None</option>
                <option value="SourceIP">Source IP</option>
                <option value="ApiKey">API Key</option>
                <option value="Header">Header</option>
              </select>
            </div>
            {formData.SessionAffinityMode === 'Header' && (
              <div className="form-group">
                <label htmlFor="sessionAffinityHeader" title="Request header name whose value identifies the client session">Session Affinity Header</label>
                <input
                  type="text"
                  id="sessionAffinityHeader"
                  value={formData.SessionAffinityHeader}
                  onChange={(e) => setFormData({ ...formData, SessionAffinityHeader: e.target.value })}
                  placeholder="X-Session-Id"
                />
              </div>
            )}
            {formData.SessionAffinityMode !== 'None' && (
              <div className="form-group">
                <label htmlFor="sessionTimeoutMs" title="How long a session pin remains active since the client's last request (refreshed on each request)">Session Timeout (ms)</label>
                <input
                  type="number"
                  id="sessionTimeoutMs"
                  value={formData.SessionTimeoutMs}
                  onChange={(e) => setFormData({ ...formData, SessionTimeoutMs: e.target.value })}
                  min="60000"
                  step="60000"
                />
              </div>
            )}
            {formData.SessionAffinityMode !== 'None' && (
              <div className="form-group">
                <label htmlFor="sessionMaxEntries" title="Maximum number of concurrent session-to-endpoint pins for this VMR">Max Session Entries</label>
                <input
                  type="number"
                  id="sessionMaxEntries"
                  value={formData.SessionMaxEntries}
                  onChange={(e) => setFormData({ ...formData, SessionMaxEntries: e.target.value })}
                  min="100"
                  step="100"
                />
              </div>
            )}
          </div>

          <div className="form-group">
            <label title="Backend model runner endpoints that will handle requests for this VMR">Model Runner Endpoints</label>
            <div className="endpoint-list">
              {!formData.TenantId ? (
                <p className="no-items">Select a tenant first.</p>
              ) : endpoints.filter(e => e.TenantId === formData.TenantId).length === 0 ? (
                <p className="no-items">No endpoints available for this tenant.</p>
              ) : (
                endpoints.filter(e => e.TenantId === formData.TenantId).map((endpoint) => (
                  <label key={endpoint.Id} className="endpoint-item">
                    <input
                      type="checkbox"
                      checked={(formData.ModelRunnerEndpointIds || []).includes(endpoint.Id)}
                      onChange={() => handleEndpointToggle(endpoint.Id)}
                    />
                    <span className="endpoint-name">{endpoint.Name}</span>
                    <span className="endpoint-url">
                      {endpoint.UseSsl ? 'https' : 'http'}://{endpoint.Hostname}:{endpoint.Port}
                    </span>
                  </label>
                ))
              )}
            </div>
          </div>

          <div className="form-group">
            <label title="Model configurations that define inference parameters for requests">Model Configurations</label>
            <div className="endpoint-list">
              {!formData.TenantId ? (
                <p className="no-items">Select a tenant first.</p>
              ) : configurations.filter(c => c.TenantId === formData.TenantId).length === 0 ? (
                <p className="no-items">No configurations available for this tenant.</p>
              ) : (
                configurations.filter(c => c.TenantId === formData.TenantId).map((config) => (
                  <label key={config.Id} className="endpoint-item">
                    <input
                      type="checkbox"
                      checked={(formData.ModelConfigurationIds || []).includes(config.Id)}
                      onChange={() => handleConfigurationToggle(config.Id)}
                    />
                    <span className="endpoint-name">{config.Name}</span>
                  </label>
                ))
              )}
            </div>
          </div>

          <div className="form-group">
            <label title="Model definitions that specify which models are available through this VMR">Model Definitions</label>
            <div className="endpoint-list">
              {!formData.TenantId ? (
                <p className="no-items">Select a tenant first.</p>
              ) : definitions.filter(d => d.TenantId === formData.TenantId).length === 0 ? (
                <p className="no-items">No definitions available for this tenant.</p>
              ) : (
                definitions.filter(d => d.TenantId === formData.TenantId).map((def) => (
                  <label key={def.Id} className="endpoint-item">
                    <input
                      type="checkbox"
                      checked={(formData.ModelDefinitionIds || []).includes(def.Id)}
                      onChange={() => handleDefinitionToggle(def.Id)}
                    />
                    <span className="endpoint-name">{def.Name}</span>
                    <span className="endpoint-url">
                      {def.Family && `${def.Family} `}
                      {def.ParameterSize && `${def.ParameterSize}`}
                    </span>
                  </label>
                ))
              )}
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="modelConfigMappings" title="Maps model names to configuration IDs for pinned properties">Model Configuration Mappings (JSON)</label>
            <textarea
              id="modelConfigMappings"
              value={formData.ModelConfigurationMappingsJson}
              onChange={(e) => setFormData({ ...formData, ModelConfigurationMappingsJson: e.target.value })}
              rows={4}
              className="code-input"
              placeholder='{"model-name": "mc_xxx"}'
            />
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
            <label title="Enable embedding generation requests through this VMR">
              <input
                type="checkbox"
                checked={formData.AllowEmbeddings}
                onChange={(e) => setFormData({ ...formData, AllowEmbeddings: e.target.checked })}
              />
              Allow Embeddings
            </label>
          </div>

          <div className="form-group checkbox-group">
            <label title="Enable chat and text completion requests through this VMR">
              <input
                type="checkbox"
                checked={formData.AllowCompletions}
                onChange={(e) => setFormData({ ...formData, AllowCompletions: e.target.checked })}
              />
              Allow Completions
            </label>
          </div>

          <div className="form-group checkbox-group">
            <label title="Enable pull, push, and delete model operations (Ollama API only)">
              <input
                type="checkbox"
                checked={formData.AllowModelManagement}
                onChange={(e) => setFormData({ ...formData, AllowModelManagement: e.target.checked })}
              />
              Allow Model Management
            </label>
          </div>

          <div className="form-group checkbox-group">
            <label title="Only allow models explicitly defined by attached Model Definitions">
              <input
                type="checkbox"
                checked={formData.StrictMode}
                onChange={(e) => setFormData({ ...formData, StrictMode: e.target.checked })}
              />
              Strict Mode
            </label>
          </div>

          <div className="form-group checkbox-group">
            <label title="Inactive VMRs will not accept any requests">
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

      {showMetadata && selectedVmr && (
        <ViewMetadataModal
          data={selectedVmr}
          title="Virtual Model Runner Details"
          subtitle={selectedVmr.Id}
          onClose={() => setShowMetadata(false)}
        />
      )}

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedVmr?.Name}
        entityType="virtual model runner"
        loading={deleteLoading}
      />

      <Modal
        isOpen={showHealth}
        onClose={() => { setShowHealth(false); setHealthData(null); }}
        title="Endpoint Health Status"
        wide
      >
        <div className="health-modal">
          {selectedVmr && (
            <div className="health-header">
              <div className="health-summary">
                <h3>{selectedVmr.Name}</h3>
                {healthData && (
                  <span
                    className={`health-badge ${healthData.OverallHealthy ? 'healthy' : 'unhealthy'}`}
                    title={healthData.OverallHealthy
                      ? `All ${healthData.TotalEndpointCount} endpoint(s) are passing health checks`
                      : `${healthData.TotalEndpointCount - healthData.HealthyEndpointCount} of ${healthData.TotalEndpointCount} endpoint(s) are failing health checks`}
                  >
                    {healthData.OverallHealthy ? 'All Healthy' : 'Issues Detected'}
                  </span>
                )}
              </div>
              {healthData && selectedVmr.SessionAffinityMode && selectedVmr.SessionAffinityMode !== 'None' && (
                <span className="health-badge" style={{ marginLeft: '8px', fontSize: '0.85em' }} title="Active session affinity pins for this VMR">
                  Sessions: {healthData.ActiveSessionCount ?? 0} / {selectedVmr.SessionMaxEntries}
                </span>
              )}
              <button className="btn-icon" onClick={refreshHealth} disabled={healthLoading} title="Refresh">
                <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" className={healthLoading ? 'spinning' : ''}>
                  <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
                </svg>
              </button>
            </div>
          )}

          {healthLoading && !healthData ? (
            <div className="loading-spinner">Loading health data...</div>
          ) : healthData?.Endpoints?.length === 0 ? (
            <p className="no-items">No endpoints configured for this VMR.</p>
          ) : healthData?.Endpoints ? (
            <div className="health-endpoints">
              <table className="health-table">
                <thead>
                  <tr>
                    <th title="Backend model runner endpoint name and any error details">Endpoint</th>
                    <th title="Current health check status based on periodic monitoring">Status</th>
                    <th title="Active requests being proxied vs. configured maximum (0 = unlimited)">In-Flight</th>
                    <th title="Relative load balancing weight - higher values receive more traffic">Weight</th>
                    <th title="Percentage of time this endpoint has been healthy since monitoring started">Uptime</th>
                    <th title="When the most recent health check was performed">Last Check</th>
                  </tr>
                </thead>
                <tbody>
                  {healthData.Endpoints.map((ep) => (
                    <tr key={ep.EndpointId}>
                      <td>
                        <div className="endpoint-info">
                          <strong>{ep.EndpointName || ep.EndpointId}</strong>
                          {ep.LastError && <small className="error-text" title="Error from the most recent failed health check">{ep.LastError}</small>}
                        </div>
                      </td>
                      <td>
                        <span
                          className={`status-badge ${ep.IsHealthy ? 'healthy' : 'unhealthy'}`}
                          title={ep.IsHealthy
                            ? `Passing health checks since ${ep.LastHealthyUtc ? new Date(ep.LastHealthyUtc).toLocaleString() : 'unknown'}`
                            : `Failing health checks${ep.LastError ? ': ' + ep.LastError : ''}`}
                        >
                          {ep.IsHealthy ? 'Healthy' : 'Unhealthy'}
                        </span>
                      </td>
                      <td title={`${ep.InFlightRequests} active request(s) of ${ep.MaxParallelRequests === 0 ? 'unlimited' : ep.MaxParallelRequests} maximum`}>
                        {ep.InFlightRequests} / {ep.MaxParallelRequests === 0 ? '\u221E' : ep.MaxParallelRequests}
                      </td>
                      <td title="Relative weight for load balancing distribution">{ep.Weight}</td>
                      <td title={`${ep.UptimePercentage !== undefined ? ep.UptimePercentage.toFixed(2) : 0}% uptime - total healthy time: ${formatDuration(ep.TotalUptimeMs)}`}>
                        {ep.UptimePercentage !== undefined ? `${ep.UptimePercentage.toFixed(1)}%` : '-'}
                        <small style={{ color: 'var(--text-secondary)', marginLeft: '4px' }}>
                          ({formatDuration(ep.TotalUptimeMs)})
                        </small>
                      </td>
                      <td title={ep.LastCheckUtc ? `Full timestamp: ${new Date(ep.LastCheckUtc).toLocaleString()}` : 'No check performed yet'}>
                        {ep.LastCheckUtc ? new Date(ep.LastCheckUtc).toLocaleTimeString() : '-'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {healthData.CheckedUtc && (
                <p className="health-timestamp">
                  Status as of: {new Date(healthData.CheckedUtc).toLocaleString()}
                </p>
              )}
            </div>
          ) : null}
        </div>
      </Modal>
    </div>
  );
}

export default VirtualModelRunners;
