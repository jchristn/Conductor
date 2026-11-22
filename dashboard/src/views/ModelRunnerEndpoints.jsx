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
import LabelsTagsEditor, { labelsFromValue, labelsToPayload, tagsFromValue, tagsToPayload } from '../components/LabelsTagsEditor';
import {
  DEFAULT_ANTHROPIC_VERSION,
  HEALTH_CHECK_METHODS,
  getApiTypeBadgeClass,
  getEndpointDefaults,
  getHealthCheckTooltip,
  getProvider,
  getProviderLabel,
  hasCapability,
  isValidAnthropicVersion,
  listProviders
} from '../utils/providers';

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

function createEmptyEndpointForm() {
  return {
    TenantId: '',
    Name: '',
    Hostname: '',
    Port: 11434,
    ApiKey: '',
    ApiKeyStored: '',
    ApiKeyCleared: false,
    ApiType: 'Ollama',
    AnthropicVersion: '',
    UseSsl: false,
    TimeoutMs: 300000,
    Active: true,
    Labels: labelsFromValue([]),
    Tags: tagsFromValue({}),
    HealthCheckEnabled: true,
    HealthCheckUrl: '/',
    HealthCheckMethod: 'GET',
    HealthCheckRequestBody: '',
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
  };
}

function normalizeHealthCheckMethod(value) {
  if (value === 1 || value === 'HEAD') return 'HEAD';
  if (value === 2 || value === 'POST') return 'POST';
  return 'GET';
}

function endpointToForm(endpoint, overrides = {}) {
  return {
    ...createEmptyEndpointForm(),
    TenantId: endpoint.TenantId || '',
    Name: endpoint.Name || '',
    Hostname: endpoint.Hostname || '',
    Port: endpoint.Port || 11434,
    ApiKey: endpoint.ApiKey || '',
    ApiKeyStored: endpoint.ApiKey || '',
    ApiKeyCleared: false,
    ApiType: endpoint.ApiType || 'Ollama',
    AnthropicVersion: endpoint.AnthropicVersion || '',
    UseSsl: endpoint.UseSsl || false,
    TimeoutMs: endpoint.TimeoutMs || 300000,
    Active: endpoint.Active !== false,
    Labels: labelsFromValue(endpoint.Labels),
    Tags: tagsFromValue(endpoint.Tags),
    HealthCheckEnabled: endpoint.HealthCheckEnabled !== false,
    HealthCheckUrl: endpoint.HealthCheckUrl || '/',
    HealthCheckMethod: normalizeHealthCheckMethod(endpoint.HealthCheckMethod),
    HealthCheckRequestBody: endpoint.HealthCheckRequestBody || '',
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
    RigMonitorTelemetrySelectors: (endpoint.RigMonitor?.TelemetrySelectors || []).join(', '),
    ...overrides
  };
}

/**
 * Value to send as ApiKey. Contract: omitted, null or the masked value keeps the stored key,
 * an empty string clears it, any other value replaces it.
 * Returns undefined when the property should be omitted.
 */
export function resolveApiKeyForPayload(form) {
  if (form.ApiKeyCleared) return '';
  const value = form.ApiKey || '';
  if (!value) return undefined;
  return value;
}

/**
 * Client-side checks that block saving. Returns a list of messages (empty when valid).
 */
export function getEndpointFormErrors(form) {
  const errors = [];
  if (form.ApiType === 'Anthropic' && form.AnthropicVersion && !isValidAnthropicVersion(form.AnthropicVersion.trim())) {
    errors.push('Anthropic version must be a date in yyyy-MM-dd format, for example 2023-06-01.');
  }
  if (form.HealthCheckMethod === 'POST') {
    const body = (form.HealthCheckRequestBody || '').trim();
    if (!body) {
      errors.push('A POST health check needs a request body.');
    } else {
      try {
        JSON.parse(body);
      } catch (err) {
        errors.push(`Health check request body is not valid JSON: ${err.message}`);
      }
    }
  }
  return errors;
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
  const [formData, setFormData] = useState(createEmptyEndpointForm);
  const [formErrors, setFormErrors] = useState([]);
  const [apiKeyRevealError, setApiKeyRevealError] = useState('');

  // Defaults come only from the type the operator selects (provider registry). Nothing is
  // inferred from the hostname.
  const applyApiTypeDefaults = (currentFormData, apiType) => ({
    ...currentFormData,
    ...getEndpointDefaults(apiType),
    ApiType: apiType,
    AnthropicVersion: apiType === 'Anthropic' ? currentFormData.AnthropicVersion : ''
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

  // Handle hostname input changes. A pasted URL fills hostname, port and SSL from what was typed;
  // the type and every other default come only from the selected API type.
  const handleHostnameChange = (e) => {
    const value = e.target.value;
    const parsed = parseEndpointUrl(value);

    if (parsed) {
      const updates = {
        ...formData,
        Hostname: parsed.hostname,
        Port: parsed.port
      };
      if (parsed.useSsl !== null) {
        updates.UseSsl = parsed.useSsl;
      }
      setFormData(updates);
    } else {
      setFormData({ ...formData, Hostname: value });
    }
  };

  const handleCreate = () => {
    setEditMode(false);
    setValidationResult(null);
    setFormErrors([]);
    setApiKeyRevealError('');
    setFormData(createEmptyEndpointForm());
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
    setFormErrors([]);
    setApiKeyRevealError('');
    setFormData(endpointToForm(endpoint));
    setShowForm(true);
  };

  // Endpoint reads return a masked key, so Duplicate fetches the real key through the audited
  // reveal route. If that fails the copy starts without a key and the operator is told why.
  const handleDuplicate = async (endpoint) => {
    setEditMode(false);
    setSelectedEndpoint(null);
    setValidationResult(null);
    setFormErrors([]);
    setApiKeyRevealError('');

    let apiKey = '';
    if (endpoint.ApiKey) {
      try {
        const revealed = await api.revealModelRunnerEndpointApiKey(endpoint.Id, endpoint.TenantId);
        apiKey = revealed?.ApiKey || '';
      } catch (err) {
        setError('Could not copy the API key of the original endpoint: ' + err.message + '. Enter the key on the copy.');
      }
    }

    setFormData(endpointToForm(endpoint, {
      Name: endpoint.Name ? endpoint.Name + ' (Copy)' : '',
      ApiKey: apiKey,
      ApiKeyStored: ''
    }));
    setShowForm(true);
  };

  // Reveal control on the API key: replaces the masked value with the full key.
  const handleRevealApiKey = async () => {
    setApiKeyRevealError('');
    const showingStored = formData.ApiKey && formData.ApiKey === formData.ApiKeyStored;
    if (!editMode || !selectedEndpoint || !showingStored) {
      return true;
    }
    try {
      const revealed = await api.revealModelRunnerEndpointApiKey(selectedEndpoint.Id, selectedEndpoint.TenantId);
      const fullKey = revealed?.ApiKey || '';
      setFormData((current) => ({ ...current, ApiKey: fullKey, ApiKeyStored: fullKey }));
      return true;
    } catch (err) {
      setApiKeyRevealError('Could not reveal the API key: ' + err.message);
      return false;
    }
  };

  const handleClearApiKey = () => {
    setFormData((current) => ({ ...current, ApiKey: '', ApiKeyCleared: true }));
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
    const telemetrySelectors = parseTelemetrySelectors(formData.RigMonitorTelemetrySelectors);

    const payload = {
      TenantId: formData.TenantId || null,
      Name: formData.Name,
      Hostname: formData.Hostname,
      Port: parseInt(formData.Port),
      ApiType: formData.ApiType,
      AnthropicVersion: formData.ApiType === 'Anthropic' && formData.AnthropicVersion.trim() ? formData.AnthropicVersion.trim() : null,
      UseSsl: formData.UseSsl,
      TimeoutMs: parseInt(formData.TimeoutMs),
      Active: formData.Active,
      Labels: labelsToPayload(formData.Labels),
      Tags: tagsToPayload(formData.Tags),
      HealthCheckEnabled: formData.HealthCheckEnabled,
      HealthCheckUrl: formData.HealthCheckUrl,
      HealthCheckMethod: formData.HealthCheckMethod,
      HealthCheckRequestBody: formData.HealthCheckMethod === 'POST' ? formData.HealthCheckRequestBody : null,
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

    const apiKey = resolveApiKeyForPayload(formData);
    if (apiKey !== undefined) {
      payload.ApiKey = apiKey;
    }
    return payload;
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
    const errors = getEndpointFormErrors(formData);
    setFormErrors(errors);
    if (errors.length > 0) {
      return;
    }
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
      defaultHidden: true,
      label: 'ID',
      tooltip: 'Unique identifier for this endpoint',
      width: '340px',
      render: (item) => (
        <span className="model-runner-endpoint-id-cell">
          <CopyableId value={item.Id} />
        </span>
      )
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
      tooltip: 'Upstream API type the operator selected for this endpoint. It decides the wire API, authentication and defaults.',
      width: '130px',
      render: (item) => (
        <span className={getApiTypeBadgeClass(item.ApiType)} title={getProvider(item.ApiType)?.description || ''}>
          {getProviderLabel(item.ApiType)}
        </span>
      ),
      filterValue: (item) => `${item.ApiType || ''} ${getProviderLabel(item.ApiType)}`
    },
    {
      key: 'RigMonitor',
      defaultHidden: true,
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
        label: 'State',
        tooltip: 'Operator-managed routing state for this endpoint: normal, draining, or quarantined',
        width: '104px',
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
      width: '82px',
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
        if (item.HealthCheckEnabled === false) {
          return <span className="status-badge pending" title="Health checks are disabled for this endpoint, so Conductor sends no probe and treats it as healthy.">Checks Off</span>;
        }
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
        if (item.HealthCheckEnabled === false) return 'checks off';
        const h = healthData[item.Id];
        if (!h) return 'awaiting';
        return h.IsHealthy ? 'healthy' : 'unhealthy';
      }
    },
    {
      key: 'Uptime',
      defaultHidden: true,
      label: 'Uptime',
      tooltip: 'Percentage of time this endpoint has been healthy since monitoring started',
      width: '72px',
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
      width: '72px',
      sortable: false,
      filterable: false,
      isAction: true,
        render: (item) => (
          <ActionMenu
            actions={[
              { label: 'View Details', onClick: () => handleViewMetadata(item) },
              { label: 'Health Data', onClick: () => handleViewHealth(item) },
              { label: 'Load Model', onClick: () => handleOpenLoadModel(item) },
              ...(hasCapability(item.ApiType, 'modelManagement') ? [
                { label: 'Manage Models', onClick: () => handleOpenManageOllamaModels(item) }
              ] : []),
              { label: 'Edit', onClick: () => handleEdit(item) },
              { label: 'Duplicate', onClick: () => handleDuplicate(item) },
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

      <div className="model-runner-endpoints-table">
        <DataTable data={endpoints} columns={columns} loading={loading} onRowClick={handleEdit} />
      </div>

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Endpoint' : 'Create Endpoint'} wide>
        <form onSubmit={handleSubmit}>
          {formErrors.length > 0 && (
            <div className="validation-panel error" role="alert">
              <strong>Fix these fields before saving.</strong>
              <ul className="validation-list">
                {formErrors.map((message) => <li key={message}>{message}</li>)}
              </ul>
            </div>
          )}
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
              <label htmlFor="hostname" title="Paste a full URL to fill hostname, port, and SSL from it. Do not use localhost if running in Docker.">Hostname</label>
              <input
                type="text"
                id="hostname"
                value={formData.Hostname}
                onChange={handleHostnameChange}
                placeholder="hostname or https://hostname:port"
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
              <label htmlFor="apiType" title="Upstream API type. Selecting a type fills its defaults (host, port, SSL, health check). Conductor never infers the type from the hostname.">API Type</label>
              <select
                id="apiType"
                value={formData.ApiType}
                onChange={handleApiTypeChange}
                aria-describedby="apiTypeHelp"
              >
                {listProviders().map((provider) => (
                  <option key={provider.value} value={provider.value} title={provider.description}>
                    {provider.label}
                  </option>
                ))}
              </select>
              <small id="apiTypeHelp" className="form-help">{getProvider(formData.ApiType)?.description}</small>
            </div>
            <div className="form-group">
              <label htmlFor="timeoutMs" title="How long Conductor waits for this endpoint to start responding: model load, prompt evaluation and, for non-streaming requests, the whole generation. Streamed responses are not limited after the first token. The smaller of this and the VMR's value applies.">Time to first byte (ms)</label>
              <input
                type="number"
                id="timeoutMs"
                value={formData.TimeoutMs}
                onChange={(e) => setFormData({ ...formData, TimeoutMs: e.target.value })}
                min="1000"
                step="1000"
              />
              <small className="form-help">Must cover cold model loads, and should not exceed the runner's own load timeout (Ollama: OLLAMA_LOAD_TIMEOUT, default 5 minutes).</small>
            </div>
          </div>
          <div className="form-group">
            <label htmlFor="apiKey" title={`Upstream key for this endpoint, sent as ${getProvider(formData.ApiType)?.authLabel || 'Authorization: Bearer'}. Stored keys are shown masked; use the eye control to reveal the full value.`}>
              {getProvider(formData.ApiType)?.apiKeyRequired ? 'API Key (required)' : 'API Key (optional)'}
            </label>
            <div className="api-key-row">
              <SensitiveInput
                id="apiKey"
                value={formData.ApiKey}
                onChange={(e) => setFormData({ ...formData, ApiKey: e.target.value, ApiKeyCleared: false })}
                placeholder={formData.ApiKeyCleared ? 'Key will be cleared on save' : (editMode && formData.ApiKeyStored ? 'Leave blank to keep the stored key' : 'Leave blank if not required')}
                autoComplete="new-password"
                onReveal={handleRevealApiKey}
                revealLabel="Reveal API key"
                hideLabel="Hide API key"
                describedBy="apiKeyHelp"
              />
              {editMode && (formData.ApiKeyStored || formData.ApiKey) && !formData.ApiKeyCleared && (
                <button
                  type="button"
                  className="btn-secondary btn-small"
                  onClick={handleClearApiKey}
                  title="Remove the stored key from this endpoint when you save"
                >
                  Clear
                </button>
              )}
            </div>
            <small id="apiKeyHelp" className="form-help">
              {formData.ApiKeyCleared
                ? 'The stored key will be removed when you save. Type a new key to replace it instead.'
                : (editMode && formData.ApiKeyStored
                  ? 'Leaving the key unchanged or blank keeps the stored key. Use Clear to remove it.'
                  : `Sent upstream as ${getProvider(formData.ApiType)?.authLabel || 'Authorization: Bearer'}.`)}
            </small>
            {apiKeyRevealError && <small className="error-text">{apiKeyRevealError}</small>}
          </div>

          {formData.ApiType === 'Anthropic' && (
            <div className="form-group">
              <label htmlFor="anthropicVersion" title="anthropic-version header Conductor sends upstream when the client does not send one">Anthropic version</label>
              <input
                type="text"
                id="anthropicVersion"
                value={formData.AnthropicVersion}
                onChange={(e) => setFormData({ ...formData, AnthropicVersion: e.target.value })}
                placeholder={DEFAULT_ANTHROPIC_VERSION}
                aria-describedby="anthropicVersionHelp"
              />
              <small id="anthropicVersionHelp" className="form-help">
                Format yyyy-MM-dd. A client's own anthropic-version header always wins. Leave blank to use the server default ({DEFAULT_ANTHROPIC_VERSION}).
              </small>
              {formData.AnthropicVersion && !isValidAnthropicVersion(formData.AnthropicVersion.trim()) && (
                <small className="error-text">Use the yyyy-MM-dd format, for example {DEFAULT_ANTHROPIC_VERSION}.</small>
              )}
            </div>
          )}

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

          <div className="form-group checkbox-group">
            <label title={getHealthCheckTooltip(formData.ApiType)}>
              <input
                type="checkbox"
                checked={formData.HealthCheckEnabled}
                onChange={(e) => setFormData({ ...formData, HealthCheckEnabled: e.target.checked })}
                aria-describedby="healthCheckEnabledHelp"
              />
              Health checks enabled
            </label>
            <small id="healthCheckEnabledHelp" className="form-help">
              {formData.ApiType === 'TypeSafe'
                ? getHealthCheckTooltip('TypeSafe')
                : (formData.HealthCheckEnabled
                  ? 'Conductor probes this endpoint on the interval below.'
                  : 'No probes are sent and the endpoint is treated as healthy.')}
            </small>
          </div>

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
                {HEALTH_CHECK_METHODS.map((method) => (
                  <option key={method} value={method}>{method}</option>
                ))}
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

          {formData.HealthCheckMethod === 'POST' && (
            <div className="form-group">
              <label htmlFor="healthCheckRequestBody" title="JSON body sent with each POST health check, with Content-Type: application/json">Health Check Request Body</label>
              <textarea
                id="healthCheckRequestBody"
                className="code-input"
                rows={4}
                value={formData.HealthCheckRequestBody}
                onChange={(e) => setFormData({ ...formData, HealthCheckRequestBody: e.target.value })}
                placeholder='{"key": "value"}'
                spellCheck={false}
                aria-required="true"
              />
              {(() => {
                const bodyErrors = getEndpointFormErrors({ ...formData, ApiType: '' });
                return bodyErrors.length > 0
                  ? <small className="error-text">{bodyErrors[0]}</small>
                  : <small className="form-help">Required for POST. Sent as application/json.</small>;
              })()}
            </div>
          )}

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
            <label title={`Include the API key in health check requests, sent as ${getProvider(formData.ApiType)?.authLabel || 'Authorization: Bearer'}. Hosted providers reject unauthenticated health checks.`}>
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

          <LabelsTagsEditor
            labels={formData.Labels}
            tags={formData.Tags}
            onLabelsChange={(Labels) => setFormData({ ...formData, Labels })}
            onTagsChange={(Tags) => setFormData({ ...formData, Tags })}
            idPrefix="model-runner-endpoint"
          />

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
