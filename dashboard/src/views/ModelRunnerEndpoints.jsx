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
import HealthHistogram from '../components/HealthHistogram';
import SensitiveInput from '../components/SensitiveInput';
import LoadModelModal from '../components/LoadModelModal';
import OllamaModelManagerModal from '../components/OllamaModelManagerModal';

function formatBytes(value) {
  if (value === null || value === undefined || Number.isNaN(value)) return '-';
  if (value === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const exponent = Math.min(Math.floor(Math.log(value) / Math.log(1024)), units.length - 1);
  const size = value / (1024 ** exponent);
  return `${size.toFixed(size >= 10 || exponent === 0 ? 0 : 1)} ${units[exponent]}`;
}

function formatTelemetryAge(timestampUtc) {
  if (!timestampUtc) return 'Not collected';
  const ageMs = Date.now() - new Date(timestampUtc).getTime();
  if (ageMs < 1000) return 'Just now';
  if (ageMs < 60000) return `${Math.floor(ageMs / 1000)}s ago`;
  if (ageMs < 3600000) return `${Math.floor(ageMs / 60000)}m ago`;
  return `${Math.floor(ageMs / 3600000)}h ago`;
}

function formatDateTime(timestampUtc) {
  if (!timestampUtc) return '-';
  return new Date(timestampUtc).toLocaleString();
}

function parseTelemetrySelectors(value) {
  return (value || '')
    .split(/[\n,]+/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function normalizeServiceState(value) {
  if (value === 1 || value === 'Draining') return 'Draining';
  if (value === 2 || value === 'Quarantined') return 'Quarantined';
  return 'Normal';
}

function getServiceStatePresentation(value) {
  const serviceState = normalizeServiceState(value);

  switch (serviceState) {
    case 'Draining':
      return {
        serviceState,
        tone: 'warning',
        label: 'Draining',
        description: 'Stops new routing while preserving in-flight or pinned work.'
      };
    case 'Quarantined':
      return {
        serviceState,
        tone: 'danger',
        label: 'Quarantined',
        description: 'Excluded from all routing but still health-checked for diagnostics.'
      };
    default:
      return {
        serviceState: 'Normal',
        tone: 'success',
        label: 'Normal',
        description: 'Eligible for new routing when active and healthy.'
      };
  }
}

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
  const [showLoadModel, setShowLoadModel] = useState(false);
  const [showManageOllamaModels, setShowManageOllamaModels] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [healthData, setHealthData] = useState({});
  const [showHealthDetail, setShowHealthDetail] = useState(false);
  const [healthDetailEndpoint, setHealthDetailEndpoint] = useState(null);
  const [validationResult, setValidationResult] = useState(null);
  const [validationLoading, setValidationLoading] = useState(false);
  const [serviceStateActionLoading, setServiceStateActionLoading] = useState('');
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
    Weight: 1,
    RigMonitorEnabled: false,
    RigMonitorHostnameOverride: '',
    RigMonitorPort: 9990,
    RigMonitorUseSsl: false,
    RigMonitorTimeoutMs: 5000,
    RigMonitorCollectDuringHealthCheck: true,
    RigMonitorRequireReadyz: true,
    RigMonitorHealthAffectedByRigMonitor: false,
    RigMonitorMaxTelemetryAgeMs: 30000,
    RigMonitorCapabilitiesRefreshIntervalMs: 60000,
    RigMonitorTelemetryProfile: 'Full',
    RigMonitorTelemetrySelectors: ''
  });

  const getApiTypeDefaults = (apiType) => {
    switch (apiType) {
      case 'OpenAI':
        return {
          Hostname: 'api.openai.com',
          Port: 443,
          UseSsl: true,
          TimeoutMs: 300000,
          HealthCheckUrl: '/v1/models',
          HealthCheckMethod: 'GET',
          HealthCheckExpectedStatusCode: 200,
          HealthCheckUseAuth: true
        };
      case 'Gemini':
        return {
          Hostname: 'generativelanguage.googleapis.com',
          Port: 443,
          UseSsl: true,
          TimeoutMs: 300000,
          HealthCheckUrl: '/v1beta/models',
          HealthCheckMethod: 'GET',
          HealthCheckExpectedStatusCode: 200,
          HealthCheckUseAuth: true
        };
      case 'Ollama':
        return {
          Hostname: 'localhost',
          Port: 11434,
          UseSsl: false,
          TimeoutMs: 300000,
          HealthCheckUrl: '/',
          HealthCheckMethod: 'GET',
          HealthCheckExpectedStatusCode: 200,
          HealthCheckUseAuth: false
        };
      default:
        return null;
    }
  };

  const applyApiTypeDefaults = (currentFormData, apiType) => {
    const defaults = getApiTypeDefaults(apiType);
    if (!defaults) {
      return { ...currentFormData, ApiType: apiType };
    }

    return {
      ...currentFormData,
      ...defaults,
      ApiType: apiType
    };
  };

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

  const [healthDetailData, setHealthDetailData] = useState(null);
  const [healthDetailLoading, setHealthDetailLoading] = useState(false);

  const handleViewHealth = async (endpoint) => {
    setHealthDetailEndpoint(endpoint);
    setShowHealthDetail(true);
    setHealthDetailLoading(true);
    setHealthDetailData(null);
    try {
      const [healthResult, rigResult] = await Promise.allSettled([
        api.getModelRunnerEndpointHealth(endpoint.Id, endpoint.TenantId),
        api.getModelRunnerEndpointRigMonitor(endpoint.Id, endpoint.TenantId)
      ]);

      const health = healthResult.status === 'fulfilled' ? healthResult.value : (healthData[endpoint.Id] || null);
      const rig = rigResult.status === 'fulfilled' ? rigResult.value : (health?.RigMonitor || null);
      setHealthDetailData(health
        ? { ...health, RigMonitor: rig }
        : (rig
            ? {
                IsHealthy: false,
                UptimePercentage: 0,
                ConsecutiveSuccesses: 0,
                ConsecutiveFailures: 0,
                History: [],
                RigMonitor: rig
              }
            : null));
    } catch {
      // Fall back to the bulk health data
      setHealthDetailData(healthData[endpoint.Id] || null);
    } finally {
      setHealthDetailLoading(false);
    }
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
      } else if (parsed.hostname.toLowerCase() === 'generativelanguage.googleapis.com') {
        updates.HealthCheckUseAuth = true;
        updates.HealthCheckUrl = '/v1beta/models';
      }
      setFormData(updates);
    } else {
      // Just a plain hostname, update as-is
      const updates = { ...formData, Hostname: value };
      // Set defaults for api.openai.com
      if (value.toLowerCase() === 'api.openai.com') {
        updates.HealthCheckUseAuth = true;
        updates.HealthCheckUrl = '/v1/models';
      } else if (value.toLowerCase() === 'generativelanguage.googleapis.com') {
        updates.HealthCheckUseAuth = true;
        updates.HealthCheckUrl = '/v1beta/models';
      }
      setFormData(updates);
    }
  };

  const handleCreate = () => {
    setEditMode(false);
    setValidationResult(null);
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
      Weight: 1,
      RigMonitorEnabled: false,
      RigMonitorHostnameOverride: '',
      RigMonitorPort: 9990,
      RigMonitorUseSsl: false,
      RigMonitorTimeoutMs: 5000,
      RigMonitorCollectDuringHealthCheck: true,
      RigMonitorRequireReadyz: true,
      RigMonitorHealthAffectedByRigMonitor: false,
      RigMonitorMaxTelemetryAgeMs: 30000,
      RigMonitorCapabilitiesRefreshIntervalMs: 60000,
      RigMonitorTelemetryProfile: 'Full',
      RigMonitorTelemetrySelectors: ''
    });
    setShowForm(true);
  };

  const handleApiTypeChange = (e) => {
    const apiType = e.target.value;
    setFormData((current) => applyApiTypeDefaults(current, apiType));
  };

  const handleEdit = (endpoint) => {
    setEditMode(true);
    setSelectedEndpoint(endpoint);
    setValidationResult(null);
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
      Weight: endpoint.Weight || 1,
      RigMonitorEnabled: endpoint.RigMonitor?.Enabled === true,
      RigMonitorHostnameOverride: endpoint.RigMonitor?.HostnameOverride || '',
      RigMonitorPort: endpoint.RigMonitor?.Port || 9990,
      RigMonitorUseSsl: endpoint.RigMonitor?.UseSsl === true,
      RigMonitorTimeoutMs: endpoint.RigMonitor?.TimeoutMs || 5000,
      RigMonitorCollectDuringHealthCheck: endpoint.RigMonitor?.CollectDuringHealthCheck !== false,
      RigMonitorRequireReadyz: endpoint.RigMonitor?.RequireReadyz !== false,
      RigMonitorHealthAffectedByRigMonitor: endpoint.RigMonitor?.HealthAffectedByRigMonitor === true,
      RigMonitorMaxTelemetryAgeMs: endpoint.RigMonitor?.MaxTelemetryAgeMs || 30000,
      RigMonitorCapabilitiesRefreshIntervalMs: endpoint.RigMonitor?.CapabilitiesRefreshIntervalMs || 60000,
      RigMonitorTelemetryProfile: endpoint.RigMonitor?.TelemetryProfile || 'Full',
      RigMonitorTelemetrySelectors: (endpoint.RigMonitor?.TelemetrySelectors || []).join(', ')
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

  const handleOpenLoadModel = (endpoint) => {
    setSelectedEndpoint(endpoint);
    setShowLoadModel(true);
  };

  const handleOpenManageOllamaModels = (endpoint) => {
    setSelectedEndpoint(endpoint);
    setShowManageOllamaModels(true);
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

  const getEffectiveServiceState = (endpoint) => {
    return normalizeServiceState(healthData[endpoint?.Id]?.ServiceState ?? endpoint?.ServiceState);
  };

  const buildEndpointPayload = () => {
    let labels = [];
    let tags = {};
    const telemetrySelectors = parseTelemetrySelectors(formData.RigMonitorTelemetrySelectors);

    try {
      labels = JSON.parse(formData.LabelsJson || '[]');
    } catch (err) {
      throw new Error('Invalid JSON in Labels');
    }

    try {
      tags = JSON.parse(formData.TagsJson || '{}');
    } catch (err) {
      throw new Error('Invalid JSON in Tags');
    }

    return {
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
      Weight: parseInt(formData.Weight),
      RigMonitor: {
        Enabled: formData.RigMonitorEnabled,
        HostnameOverride: formData.RigMonitorHostnameOverride || null,
        Port: parseInt(formData.RigMonitorPort, 10),
        UseSsl: formData.RigMonitorUseSsl,
        TimeoutMs: parseInt(formData.RigMonitorTimeoutMs, 10),
        CollectDuringHealthCheck: formData.RigMonitorCollectDuringHealthCheck,
        RequireReadyz: formData.RigMonitorRequireReadyz,
        HealthAffectedByRigMonitor: formData.RigMonitorHealthAffectedByRigMonitor,
        MaxTelemetryAgeMs: parseInt(formData.RigMonitorMaxTelemetryAgeMs, 10),
        CapabilitiesRefreshIntervalMs: parseInt(formData.RigMonitorCapabilitiesRefreshIntervalMs, 10),
        TelemetryProfile: formData.RigMonitorTelemetryProfile,
        TelemetrySelectors: telemetrySelectors
      }
    };
  };

  const validateDraft = async () => {
    setValidationLoading(true);
    try {
      const payload = buildEndpointPayload();
      const result = await api.validateModelRunnerEndpoint(payload, editMode ? selectedEndpoint?.Id : null);
      setValidationResult(result);
      return { payload, result };
    } catch (err) {
      if (err.message?.startsWith('Invalid JSON')) {
        setError(err.message);
      } else {
        setError('Failed to validate endpoint: ' + err.message);
      }
      throw err;
    } finally {
      setValidationLoading(false);
    }
  };

  const handleServiceStateAction = async (endpoint, action) => {
    if (!endpoint?.Id) {
      return;
    }

    setServiceStateActionLoading(`${action}:${endpoint.Id}`);
    try {
      if (action === 'drain') {
        await api.drainModelRunnerEndpoint(endpoint.Id, endpoint.TenantId);
      } else if (action === 'resume') {
        await api.resumeModelRunnerEndpoint(endpoint.Id, endpoint.TenantId);
      } else if (action === 'quarantine') {
        await api.quarantineModelRunnerEndpoint(endpoint.Id, endpoint.TenantId);
      }

      await fetchEndpoints();
      await fetchHealth();
      if (healthDetailEndpoint?.Id === endpoint.Id) {
        await handleViewHealth(endpoint);
      }
    } catch (err) {
      setError('Failed to update endpoint service state: ' + err.message);
    } finally {
      setServiceStateActionLoading('');
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      const { payload, result } = await validateDraft();
      if (!result?.IsValid) {
        setError('Resolve the blocking validation issues before saving this endpoint.');
        return;
      }

      if (editMode) {
        await api.updateModelRunnerEndpoint(selectedEndpoint.Id, payload);
      } else {
        await api.createModelRunnerEndpoint(payload);
        onEntityCreated('endpoint');
      }
      setShowForm(false);
      setValidationResult(null);
      fetchEndpoints();
    } catch (err) {
      if (!err.message?.startsWith('Invalid JSON')) {
        setError('Failed to save endpoint: ' + err.message);
      }
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
      tooltip: 'API format used by this endpoint (Ollama, OpenAI, vLLM, or Gemini)',
      width: '100px'
    },
    {
      key: 'RigMonitor',
      label: 'RigMonitor',
      tooltip: 'Whether this endpoint is configured to collect RigMonitor telemetry during health checks',
      width: '120px',
      render: (item) => {
        const rig = healthData[item.Id]?.RigMonitor;
        if (item.RigMonitor?.Enabled !== true) {
          return <span style={{ color: 'var(--text-secondary)' }}>Off</span>;
        }

        return (
          <span className={`status-badge ${rig?.Ready === false ? 'unhealthy' : 'healthy'}`}>
            {rig?.Ready === false ? 'Issue' : 'Enabled'}
          </span>
        );
      },
      filterValue: (item) => item.RigMonitor?.Enabled === true ? 'enabled' : 'disabled'
    },
      {
        key: 'ServiceState',
        label: 'Service State',
        tooltip: 'Operator-managed routing state for this endpoint: normal, draining, or quarantined',
        width: '130px',
        render: (item) => {
          const presentation = getServiceStatePresentation(getEffectiveServiceState(item));
          return (
            <span className={`service-state-badge ${presentation.tone}`} title={presentation.description}>
              {presentation.label}
            </span>
          );
        },
        filterValue: (item) => normalizeServiceState(getEffectiveServiceState(item)).toLowerCase()
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
      tooltip: 'Live health check result from periodic monitoring (click for details)',
      width: '200px',
      sortable: false,
      render: (item) => {
        if (!item.Active) return <span className="status-badge pending" title="Inactive endpoints are not health-checked">Inactive</span>;
        const h = healthData[item.Id];
        if (!h) return <span className="status-badge pending" title="Awaiting first health check result">Awaiting Check</span>;
        return (
          <div
            style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}
            data-row-click-ignore="true"
            onClick={(event) => {
              event.stopPropagation();
              handleViewHealth(item);
            }}
          >
            <span
              className={`status-badge ${h.IsHealthy ? 'healthy' : 'unhealthy'}`}
              title={h.IsHealthy
                ? `Healthy since ${h.LastHealthyUtc ? new Date(h.LastHealthyUtc).toLocaleString() : 'unknown'}`
                : (h.LastError || `Unhealthy since ${h.LastUnhealthyUtc ? new Date(h.LastUnhealthyUtc).toLocaleString() : 'unknown'}`)}
            >
              {h.IsHealthy ? 'Healthy' : 'Unhealthy'}
            </span>
            <HealthHistogram history={h.History || []} width={80} height={18} />
          </div>
        );
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
              { label: 'Health Data', onClick: () => handleViewHealth(item) },
              { label: 'Load Model', onClick: () => handleOpenLoadModel(item) },
              ...(item.ApiType === 'Ollama' ? [
                { label: 'Manage Models', onClick: () => handleOpenManageOllamaModels(item) }
              ] : []),
              { label: 'Edit', onClick: () => handleEdit(item) },
              { divider: true },
              {
                label: serviceStateActionLoading === `drain:${item.Id}` ? 'Draining...' : 'Drain',
                disabled: normalizeServiceState(getEffectiveServiceState(item)) === 'Draining' || serviceStateActionLoading.length > 0,
                onClick: () => handleServiceStateAction(item, 'drain')
              },
              {
                label: serviceStateActionLoading === `resume:${item.Id}` ? 'Resuming...' : 'Resume',
                disabled: normalizeServiceState(getEffectiveServiceState(item)) === 'Normal' || serviceStateActionLoading.length > 0,
                onClick: () => handleServiceStateAction(item, 'resume')
              },
              {
                label: serviceStateActionLoading === `quarantine:${item.Id}` ? 'Quarantining...' : 'Quarantine',
                disabled: normalizeServiceState(getEffectiveServiceState(item)) === 'Quarantined' || serviceStateActionLoading.length > 0,
                onClick: () => handleServiceStateAction(item, 'quarantine')
              },
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
        <div>
          <h1>Model Runner Endpoints</h1>
          <p className="view-subtitle">Register and monitor the backend model runner endpoints that serve inference requests.</p>
        </div>
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

      <DataTable data={endpoints} columns={columns} loading={loading} onRowClick={handleEdit} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Endpoint' : 'Create Endpoint'} wide>
        <form onSubmit={handleSubmit}>
          {validationResult && (
            <div className={`validation-panel ${validationResult.IsValid ? 'success' : 'error'}`}>
              <strong>{validationResult.IsValid ? 'Draft looks routable.' : 'Resolve the blocking issues before saving.'}</strong>
              {validationResult.Errors?.length > 0 && (
                <ul className="validation-list">
                  {validationResult.Errors.map((issue, index) => (
                    <li key={`endpoint-error-${index}`}>{issue.Message}</li>
                  ))}
                </ul>
              )}
              {validationResult.Warnings?.length > 0 && (
                <ul className="validation-list warning">
                  {validationResult.Warnings.map((issue, index) => (
                    <li key={`endpoint-warning-${index}`}>{issue.Message}</li>
                  ))}
                </ul>
              )}
            </div>
          )}

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
              <label htmlFor="apiType" title="API format used by this endpoint (Ollama, OpenAI, vLLM, or Gemini)">API Type</label>
              <select
                id="apiType"
                value={formData.ApiType}
                onChange={handleApiTypeChange}
              >
                <option value="Ollama">Ollama</option>
                <option value="OpenAI">OpenAI</option>
                <option value="vLLM">vLLM</option>
                <option value="Gemini">Gemini</option>
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
            <SensitiveInput
              id="apiKey"
              value={formData.ApiKey}
              onChange={(e) => setFormData({ ...formData, ApiKey: e.target.value })}
              placeholder="Leave blank if not required"
              autoComplete="new-password"
            />
          </div>

          <h3 style={{ marginTop: '24px', marginBottom: '16px', borderBottom: '1px solid var(--border-color)', paddingBottom: '8px' }}>RigMonitor</h3>

          <div className="form-group checkbox-group">
            <label title="Enable cached RigMonitor host telemetry collection for this endpoint during the health-check loop">
              <input
                type="checkbox"
                checked={formData.RigMonitorEnabled}
                onChange={(e) => setFormData({ ...formData, RigMonitorEnabled: e.target.checked })}
              />
              Enable RigMonitor
            </label>
          </div>

          {formData.RigMonitorEnabled && (
            <>
              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="rigMonitorHostnameOverride" title="Optional RigMonitor hostname override. Leave blank to reuse the endpoint hostname.">RigMonitor Hostname Override</label>
                  <input
                    type="text"
                    id="rigMonitorHostnameOverride"
                    value={formData.RigMonitorHostnameOverride}
                    onChange={(e) => setFormData({ ...formData, RigMonitorHostnameOverride: e.target.value })}
                    placeholder="Leave blank to reuse endpoint hostname"
                  />
                </div>
                <div className="form-group" style={{ maxWidth: '120px' }}>
                  <label htmlFor="rigMonitorPort" title="RigMonitor REST API port">RigMonitor Port</label>
                  <input
                    type="number"
                    id="rigMonitorPort"
                    value={formData.RigMonitorPort}
                    onChange={(e) => setFormData({ ...formData, RigMonitorPort: e.target.value })}
                    min="1"
                    max="65535"
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="rigMonitorTimeoutMs" title="HTTP timeout for RigMonitor REST calls">RigMonitor Timeout (ms)</label>
                  <input
                    type="number"
                    id="rigMonitorTimeoutMs"
                    value={formData.RigMonitorTimeoutMs}
                    onChange={(e) => setFormData({ ...formData, RigMonitorTimeoutMs: e.target.value })}
                    min="1000"
                    step="1000"
                  />
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="rigMonitorTelemetryProfile" title="Predefined selector profile used when fetching RigMonitor telemetry">Telemetry Profile</label>
                  <select
                    id="rigMonitorTelemetryProfile"
                    value={formData.RigMonitorTelemetryProfile}
                    onChange={(e) => setFormData({ ...formData, RigMonitorTelemetryProfile: e.target.value })}
                  >
                    <option value="Basic">Basic</option>
                    <option value="GpuPlacement">GPU Placement</option>
                    <option value="OllamaPlacement">Ollama Placement</option>
                    <option value="Full">Full</option>
                    <option value="Custom">Custom</option>
                  </select>
                </div>
                <div className="form-group">
                  <label htmlFor="rigMonitorMaxTelemetryAgeMs" title="Telemetry older than this is treated as stale for policy evaluation">Max Telemetry Age (ms)</label>
                  <input
                    type="number"
                    id="rigMonitorMaxTelemetryAgeMs"
                    value={formData.RigMonitorMaxTelemetryAgeMs}
                    onChange={(e) => setFormData({ ...formData, RigMonitorMaxTelemetryAgeMs: e.target.value })}
                    min="1000"
                    step="1000"
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="rigMonitorCapabilitiesRefreshIntervalMs" title="How often Conductor refreshes RigMonitor capabilities">Capabilities Refresh (ms)</label>
                  <input
                    type="number"
                    id="rigMonitorCapabilitiesRefreshIntervalMs"
                    value={formData.RigMonitorCapabilitiesRefreshIntervalMs}
                    onChange={(e) => setFormData({ ...formData, RigMonitorCapabilitiesRefreshIntervalMs: e.target.value })}
                    min="1000"
                    step="1000"
                  />
                </div>
              </div>

              {formData.RigMonitorTelemetryProfile === 'Custom' && (
                <div className="form-group">
                  <label htmlFor="rigMonitorTelemetrySelectors" title="Comma or newline separated RigMonitor selectors such as cpu, memory, gpu, and ollama">Telemetry Selectors</label>
                  <textarea
                    id="rigMonitorTelemetrySelectors"
                    value={formData.RigMonitorTelemetrySelectors}
                    onChange={(e) => setFormData({ ...formData, RigMonitorTelemetrySelectors: e.target.value })}
                    rows={3}
                    className="code-input"
                    placeholder="cpu, memory, gpu"
                  />
                </div>
              )}

              <div className="form-group checkbox-group">
                <label title="Use HTTPS for RigMonitor instead of HTTP">
                  <input
                    type="checkbox"
                    checked={formData.RigMonitorUseSsl}
                    onChange={(e) => setFormData({ ...formData, RigMonitorUseSsl: e.target.checked })}
                  />
                  Use SSL for RigMonitor
                </label>
              </div>

              <div className="form-group checkbox-group">
                <label title="Collect RigMonitor telemetry during the normal health-check loop">
                  <input
                    type="checkbox"
                    checked={formData.RigMonitorCollectDuringHealthCheck}
                    onChange={(e) => setFormData({ ...formData, RigMonitorCollectDuringHealthCheck: e.target.checked })}
                  />
                  Collect Telemetry During Health Checks
                </label>
              </div>

              <div className="form-group checkbox-group">
                <label title="Require RigMonitor /readyz to report ready before telemetry is considered current">
                  <input
                    type="checkbox"
                    checked={formData.RigMonitorRequireReadyz}
                    onChange={(e) => setFormData({ ...formData, RigMonitorRequireReadyz: e.target.checked })}
                  />
                  Require /readyz
                </label>
              </div>

              <div className="form-group checkbox-group">
                <label title="Allow RigMonitor readiness or telemetry failures to affect endpoint health">
                  <input
                    type="checkbox"
                    checked={formData.RigMonitorHealthAffectedByRigMonitor}
                    onChange={(e) => setFormData({ ...formData, RigMonitorHealthAffectedByRigMonitor: e.target.checked })}
                  />
                  RigMonitor Affects Endpoint Health
                </label>
              </div>

              {editMode && selectedEndpoint && (
                <div className="form-actions" style={{ justifyContent: 'flex-start', marginTop: '8px' }}>
                  <button type="button" className="btn-secondary" onClick={() => handleViewHealth(selectedEndpoint)}>
                    Test RigMonitor
                  </button>
                </div>
              )}
            </>
          )}

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
            <label title="Include API key in health check requests (required for OpenAI-compatible APIs and Gemini API)">
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
            <button type="button" className="btn-secondary" onClick={() => validateDraft().catch(() => {})} disabled={validationLoading}>
              {validationLoading ? 'Validating...' : 'Validate'}
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

      <LoadModelModal
        isOpen={showLoadModel}
        onClose={() => { setShowLoadModel(false); setSelectedEndpoint(null); }}
        target={selectedEndpoint}
        targetType="endpoint"
        api={api}
        onComplete={() => { fetchHealth(); }}
      />

      <OllamaModelManagerModal
        isOpen={showManageOllamaModels}
        onClose={() => { setShowManageOllamaModels(false); setSelectedEndpoint(null); }}
        endpoint={selectedEndpoint}
        api={api}
        onChanged={() => { fetchHealth(); }}
      />

      <Modal
        isOpen={showHealthDetail}
        onClose={() => { setShowHealthDetail(false); setHealthDetailEndpoint(null); setHealthDetailData(null); }}
        title={`Health: ${healthDetailEndpoint?.Name || 'Endpoint'}`}
        wide
        className="modal-health"
      >
        {healthDetailEndpoint && (() => {
          const h = healthDetailData || healthData[healthDetailEndpoint.Id];
          const isActive = healthDetailEndpoint.Active;
          const history = h?.History || [];
          const historySpanMs = history.length > 0
            ? new Date() - new Date([...history].sort((a, b) => new Date(a.TimestampUtc) - new Date(b.TimestampUtc))[0].TimestampUtc)
            : 0;
          const historySpanStr = historySpanMs > 0 ? formatDuration(historySpanMs) : 'No data';
          const rigMonitorStatusMessage = h?.RigMonitor?.LastError || h?.RigMonitor?.ReadyMessage || '';
          const rigMonitorStatusTone = h?.RigMonitor?.LastError
            ? 'error'
            : (h?.RigMonitor?.Ready === true
              ? 'success'
              : (h?.RigMonitor?.Ready === false ? 'warning' : 'neutral'));
          const loadedOllamaModels = h?.RigMonitor?.Telemetry?.Ollama?.LoadedModels || [];
          const showLoadedOllamaModels = loadedOllamaModels.length > 0;

          return (
            <div className="health-modal">
              {healthDetailLoading && !h && (
                <div className="loading-spinner">Loading health data...</div>
              )}

              {!h && !healthDetailLoading ? (
                isActive ? (
                  <p className="no-items">Awaiting first health check result. The server performs periodic checks based on the configured interval.</p>
                ) : (
                  <p className="no-items">This endpoint is inactive and is not being health-checked. Enable it to begin monitoring.</p>
                )
              ) : h ? (
                <>
                  <div className="health-stats-row">
                    <div className="health-stat-card" title="Current health check status">
                      <div className="health-stat-label">Status</div>
                      <div className="health-stat-value">
                        <span className={`status-badge ${h.IsHealthy ? 'healthy' : 'unhealthy'}`}>
                          {h.IsHealthy ? 'Healthy' : 'Unhealthy'}
                        </span>
                      </div>
                    </div>
                    <div className="health-stat-card" title="Operator-managed routing state for this endpoint">
                      <div className="health-stat-label">Service State</div>
                      <div className="health-stat-value">
                        <span className={`service-state-badge ${getServiceStatePresentation(h.ServiceState ?? healthDetailEndpoint.ServiceState).tone}`}>
                          {getServiceStatePresentation(h.ServiceState ?? healthDetailEndpoint.ServiceState).label}
                        </span>
                      </div>
                    </div>
                    <div className="health-stat-card" title="Percentage of time this endpoint has been healthy since monitoring started">
                      <div className="health-stat-label">Uptime</div>
                      <div className="health-stat-value" style={{ color: getUptimeColor(h.UptimePercentage) }}>
                        {h.UptimePercentage.toFixed(2)}%
                      </div>
                    </div>
                    <div className="health-stat-card" title="Time span covered by health check history">
                      <div className="health-stat-label">History Span</div>
                      <div className="health-stat-value">{historySpanStr}</div>
                    </div>
                    <div className="health-stat-card" title="Consecutive successful health checks">
                      <div className="health-stat-label">Consecutive OK</div>
                      <div className="health-stat-value health-stat-success">{h.ConsecutiveSuccesses}</div>
                    </div>
                    <div className="health-stat-card" title="Consecutive failed health checks">
                      <div className="health-stat-label">Consecutive Fail</div>
                      <div className="health-stat-value health-stat-danger">{h.ConsecutiveFailures}</div>
                    </div>
                  </div>

                  {h.LastError && (
                    <div className="health-error-box" title="The error message from the most recent failed health check">
                      <div className="health-error-label">Last Error</div>
                      <div className="health-error-message">{h.LastError}</div>
                    </div>
                  )}

                  {history.length > 0 && (
                    <div className="health-histogram-section">
                      <div className="health-section-label">Health History</div>
                      <div className="health-histogram-container">
                        <HealthHistogram history={history} width={840} height={36} fill />
                      </div>
                    </div>
                  )}

                  <div className="health-timestamps">
                    <div className="health-timestamp-item" title="When health monitoring began for this endpoint">
                      <span className="health-timestamp-label">First check</span>
                      <span className="health-timestamp-value">{h.FirstCheckUtc ? new Date(h.FirstCheckUtc).toLocaleString() : 'N/A'}</span>
                    </div>
                    <div className="health-timestamp-item" title="When the most recent health check was performed">
                      <span className="health-timestamp-label">Last check</span>
                      <span className="health-timestamp-value">{h.LastCheckUtc ? new Date(h.LastCheckUtc).toLocaleString() : 'N/A'}</span>
                    </div>
                    <div className="health-timestamp-item" title="When the endpoint most recently transitioned to healthy status">
                      <span className="health-timestamp-label">Last healthy</span>
                      <span className="health-timestamp-value">{h.LastHealthyUtc ? new Date(h.LastHealthyUtc).toLocaleString() : 'N/A'}</span>
                    </div>
                    <div className="health-timestamp-item" title="When the endpoint most recently transitioned to unhealthy status">
                      <span className="health-timestamp-label">Last unhealthy</span>
                      <span className="health-timestamp-value">{h.LastUnhealthyUtc ? new Date(h.LastUnhealthyUtc).toLocaleString() : 'N/A'}</span>
                    </div>
                  </div>

                  {h.RigMonitor?.Enabled && (
                    <>
                      <div className="health-section-label" style={{ marginTop: '20px' }}>RigMonitor</div>
                      <div className="health-stats-row health-stats-row-four">
                        <div className="health-stat-card" title="Cached RigMonitor readiness">
                          <div className="health-stat-label">Ready</div>
                          <div className="health-stat-value">
                            <span className={`status-badge ${h.RigMonitor.Ready === false ? 'unhealthy' : 'healthy'}`}>
                              {h.RigMonitor.Ready === false ? 'Not Ready' : (h.RigMonitor.Ready === true ? 'Ready' : 'Unknown')}
                            </span>
                          </div>
                        </div>
                        <div className="health-stat-card" title="RigMonitor base URL Conductor is polling">
                          <div className="health-stat-label">Base URL</div>
                          <div className="health-stat-value" style={{ fontSize: '13px' }}>{h.RigMonitor.BaseUrl || 'N/A'}</div>
                        </div>
                        <div className="health-stat-card" title="Age of the latest cached telemetry snapshot">
                          <div className="health-stat-label">Telemetry Age</div>
                          <div className="health-stat-value">{formatTelemetryAge(h.RigMonitor.LastTelemetryUtc)}</div>
                        </div>
                        <div className="health-stat-card" title="Most recent telemetry collection time">
                          <div className="health-stat-label">Last Telemetry</div>
                          <div className="health-stat-value">{h.RigMonitor.LastTelemetryUtc ? new Date(h.RigMonitor.LastTelemetryUtc).toLocaleString() : 'N/A'}</div>
                        </div>
                      </div>

                      {rigMonitorStatusMessage && (
                        <div className={`health-status-box ${rigMonitorStatusTone}`} title="Latest cached RigMonitor status message or error">
                          <div className="health-status-label">RigMonitor Status</div>
                          <div className="health-status-message">{rigMonitorStatusMessage}</div>
                        </div>
                      )}

                      <div className="health-timestamps">
                        <div className="health-timestamp-item" title="The last time RigMonitor /readyz was checked">
                          <span className="health-timestamp-label">Last /readyz</span>
                          <span className="health-timestamp-value">{h.RigMonitor.LastReadyzUtc ? new Date(h.RigMonitor.LastReadyzUtc).toLocaleString() : 'N/A'}</span>
                        </div>
                        <div className="health-timestamp-item" title="The last time RigMonitor capabilities were refreshed">
                          <span className="health-timestamp-label">Last capabilities</span>
                          <span className="health-timestamp-value">{h.RigMonitor.LastCapabilitiesUtc ? new Date(h.RigMonitor.LastCapabilitiesUtc).toLocaleString() : 'N/A'}</span>
                        </div>
                        <div className="health-timestamp-item" title="Whether RigMonitor reports warmed telemetry">
                          <span className="health-timestamp-label">Telemetry warm</span>
                          <span className="health-timestamp-value">{h.RigMonitor.Capabilities?.TelemetryWarm === true ? 'Yes' : 'No'}</span>
                        </div>
                        <div className="health-timestamp-item" title="Whether RigMonitor reports Nvidia GPU availability">
                          <span className="health-timestamp-label">GPU available</span>
                          <span className="health-timestamp-value">{h.RigMonitor.Capabilities?.NvidiaAvailable === true ? 'Yes' : 'No'}</span>
                        </div>
                      </div>

                      {h.RigMonitor.Telemetry && (
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '12px', marginTop: '16px' }}>
                          <div className="health-stat-card" title="Cached CPU utilization from RigMonitor">
                            <div className="health-stat-label">CPU Utilization</div>
                            <div className="health-stat-value">
                              {h.RigMonitor.Telemetry.Cpu?.UtilizationPercent !== null && h.RigMonitor.Telemetry.Cpu?.UtilizationPercent !== undefined
                                ? `${h.RigMonitor.Telemetry.Cpu.UtilizationPercent.toFixed(1)}%`
                                : '-'}
                            </div>
                          </div>
                          <div className="health-stat-card" title="Cached available memory from RigMonitor">
                            <div className="health-stat-label">Memory Free</div>
                            <div className="health-stat-value">{formatBytes(h.RigMonitor.Telemetry.Memory?.AvailableBytes)}</div>
                          </div>
                          <div className="health-stat-card" title="Cached aggregate network receive throughput">
                            <div className="health-stat-label">Network RX/s</div>
                            <div className="health-stat-value">{formatBytes(h.RigMonitor.Telemetry.Network?.TotalReceiveBytesPerSecond)}</div>
                          </div>
                          <div className="health-stat-card" title="Maximum disk utilization across cached volume telemetry">
                            <div className="health-stat-label">Disk Utilization</div>
                            <div className="health-stat-value">
                              {h.RigMonitor.Telemetry.Disk?.Volumes?.length
                                ? `${Math.max(...h.RigMonitor.Telemetry.Disk.Volumes.map((volume) => volume.UtilizationPercent || 0)).toFixed(1)}%`
                                : '-'}
                            </div>
                          </div>
                          <div className="health-stat-card" title="Average cached GPU utilization across devices">
                            <div className="health-stat-label">GPU Utilization</div>
                            <div className="health-stat-value">
                              {h.RigMonitor.Telemetry.Gpu?.Devices?.length
                                ? `${(h.RigMonitor.Telemetry.Gpu.Devices
                                    .map((device) => device.Metrics?.GpuUtilizationPercent || 0)
                                    .reduce((sum, value) => sum + value, 0) / h.RigMonitor.Telemetry.Gpu.Devices.length).toFixed(1)}%`
                                : '-'}
                            </div>
                          </div>
                          <div className="health-stat-card" title="Cached Ollama loaded model count, if available">
                            <div className="health-stat-label">Loaded Models</div>
                            <div className="health-stat-value">{h.RigMonitor.Telemetry.Ollama?.LoadedModelCount ?? '-'}</div>
                          </div>
                        </div>
                      )}

                      {showLoadedOllamaModels && (
                        <div className="health-table-section">
                          <div className="health-section-label">Running Ollama Models</div>
                          <div className="health-table-container">
                            <table className="health-table">
                              <thead>
                                <tr>
                                  <th title="Model name or tag currently loaded in Ollama">Model</th>
                                  <th title="Parameter size reported by Ollama telemetry">Parameters</th>
                                  <th title="Resident model size in bytes">Size</th>
                                  <th title="VRAM footprint reported by Ollama telemetry">VRAM</th>
                                  <th title="When Ollama plans to evict the model from memory">Expires</th>
                                </tr>
                              </thead>
                              <tbody>
                                {loadedOllamaModels.map((model, index) => (
                                  <tr key={`${model.Name || model.Model || 'model'}-${index}`}>
                                    <td title={model.Model || model.Name || ''}>
                                      {model.Name || model.Model || '-'}
                                    </td>
                                    <td>{model.ParameterSize || '-'}</td>
                                    <td>{formatBytes(model.SizeBytes)}</td>
                                    <td>{formatBytes(model.SizeVramBytes)}</td>
                                    <td>{formatDateTime(model.ExpiresAtUtc)}</td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        </div>
                      )}
                    </>
                  )}
                </>
              ) : null}
            </div>
          );
        })()}
      </Modal>
    </div>
  );
}

export default ModelRunnerEndpoints;
