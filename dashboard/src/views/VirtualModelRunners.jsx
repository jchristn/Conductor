import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import { useOnboarding } from '../context/OnboardingContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';
import CopyButton from '../components/CopyButton';
import LoadModelModal from '../components/LoadModelModal';
import LabelsTagsEditor, { labelsFromValue, labelsToPayload, tagsFromValue, tagsToPayload } from '../components/LabelsTagsEditor';

const VMR_BASE_PATH_PREFIX = '/v1.0/api/';

function getVmrBasePathValidationError(basePath) {
  const trimmed = (basePath || '').trim();
  if (!trimmed) return 'Base path is required.';
  if (!trimmed.toLowerCase().startsWith(VMR_BASE_PATH_PREFIX)) {
    return 'Base path must start with /v1.0/api/.';
  }

  const suffix = trimmed.slice(VMR_BASE_PATH_PREFIX.length).replace(/^\/+|\/+$/g, '');
  if (!suffix || suffix.includes('/')) {
    return 'Base path must use exactly one segment after /v1.0/api/.';
  }

  return '';
}

function normalizeVmrBasePath(basePath) {
  const error = getVmrBasePathValidationError(basePath);
  if (error) throw new Error(error);

  const trimmed = basePath.trim();
  const suffix = trimmed.slice(VMR_BASE_PATH_PREFIX.length).replace(/^\/+|\/+$/g, '');
  return `${VMR_BASE_PATH_PREFIX}${suffix}/`;
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
      return { label: 'Draining', tone: 'warning' };
    case 'Quarantined':
      return { label: 'Quarantined', tone: 'danger' };
    default:
      return { label: 'Normal', tone: 'success' };
  }
}

function getReservationStateForVmr(vmr, reservations) {
  const vmrReservations = (reservations || []).filter((reservation) =>
    reservation.VirtualModelRunnerId === vmr.Id && reservation.Active !== false
  );
  const now = Date.now();
  const active = vmrReservations.filter((reservation) => {
    const start = new Date(reservation.StartUtc).getTime();
    const end = new Date(reservation.EndUtc).getTime();
    return !Number.isNaN(start) && !Number.isNaN(end) && start <= now && now < end;
  });

  if (active.length > 1) {
    return { label: 'Conflict', tone: 'danger', title: `${active.length} active reservations overlap this VMR.` };
  }
  if (active.length === 1) {
    return { label: 'Reserved', tone: 'warning', title: `${active[0].Name || active[0].Id} is active until ${new Date(active[0].EndUtc).toISOString()}.` };
  }

  const drain = vmrReservations.find((reservation) => {
    const start = new Date(reservation.StartUtc).getTime();
    const lead = Number(reservation.AdmissionDrainLeadMs || 0);
    return lead > 0 && !Number.isNaN(start) && now >= start - lead && now < start;
  });
  if (drain) {
    return { label: 'Drain Soon', tone: 'warning', title: `${drain.Name || drain.Id} starts at ${new Date(drain.StartUtc).toISOString()}.` };
  }

  const upcoming = vmrReservations
    .filter((reservation) => new Date(reservation.StartUtc).getTime() > now)
    .sort((a, b) => new Date(a.StartUtc).getTime() - new Date(b.StartUtc).getTime())[0];
  if (upcoming) {
    return { label: 'Upcoming', tone: 'neutral', title: `${upcoming.Name || upcoming.Id} starts at ${new Date(upcoming.StartUtc).toISOString()}.` };
  }

  return { label: 'Open', tone: 'success', title: 'No active or upcoming reservation applies to this VMR.' };
}

function getDefaultExplainRequest(apiType) {
  switch (apiType) {
    case 'Gemini':
      return {
        Method: 'POST',
        RelativePath: '/v1beta/models/gemini-1.5-flash:generateContent',
        SourceIp: '127.0.0.1',
        HeadersJson: '{}',
        Body: '{\n  "contents": [{ "parts": [{ "text": "hello" }] }]\n}'
      };
    case 'Ollama':
      return {
        Method: 'POST',
        RelativePath: '/api/chat',
        SourceIp: '127.0.0.1',
        HeadersJson: '{}',
        Body: '{\n  "model": "llama3.1",\n  "messages": [{ "role": "user", "content": "hello" }]\n}'
      };
    default:
      return {
        Method: 'POST',
        RelativePath: '/v1/chat/completions',
        SourceIp: '127.0.0.1',
        HeadersJson: '{}',
        Body: '{\n  "model": "gpt-4o-mini",\n  "messages": [{ "role": "user", "content": "hello" }]\n}'
      };
  }
}

function createDefaultAdaptiveLoadBalancing() {
  return {
    SampleCount: 2,
    ColdStartScore: 60,
    EwmaAlpha: 0.2,
    BackoffBaseMs: 30000,
    BackoffMaxMs: 300000,
    FailureThreshold: 3,
    ExcludeBackoffEndpoints: true,
    BackoffBreaksSessionAffinity: true,
    Weights: {
      Success: 35,
      Latency: 25,
      TimeToFirstToken: 15,
      Pending: 15,
      EndpointWeight: 10
    }
  };
}

function normalizeAdaptiveLoadBalancing(value) {
  return {
    ...createDefaultAdaptiveLoadBalancing(),
    ...(value || {}),
    Weights: {
      ...createDefaultAdaptiveLoadBalancing().Weights,
      ...((value || {}).Weights || {})
    }
  };
}

function VirtualModelRunners() {
  const { api, setError, serverUrl } = useApp();
  const { pendingCreate, clearPendingCreate, onEntityCreated } = useOnboarding();
  const navigate = useNavigate();
  const [vmrs, setVmrs] = useState([]);
  const [reservations, setReservations] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [endpoints, setEndpoints] = useState([]);
  const [endpointGroups, setEndpointGroups] = useState([]);
  const [configurations, setConfigurations] = useState([]);
  const [definitions, setDefinitions] = useState([]);
  const [loadBalancingPolicies, setLoadBalancingPolicies] = useState([]);
  const [modelAccessPolicies, setModelAccessPolicies] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedVmr, setSelectedVmr] = useState(null);
  const [showMetadata, setShowMetadata] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [showLoadModel, setShowLoadModel] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [showHealth, setShowHealth] = useState(false);
  const [healthData, setHealthData] = useState(null);
  const [healthLoading, setHealthLoading] = useState(false);
  const [showRuntimeStats, setShowRuntimeStats] = useState(false);
  const [runtimeStatsData, setRuntimeStatsData] = useState(null);
  const [runtimeStatsLoading, setRuntimeStatsLoading] = useState(false);
  const [validationResult, setValidationResult] = useState(null);
  const [validationLoading, setValidationLoading] = useState(false);
  const [showEffective, setShowEffective] = useState(false);
  const [effectiveConfig, setEffectiveConfig] = useState(null);
  const [effectiveLoading, setEffectiveLoading] = useState(false);
  const [showExplain, setShowExplain] = useState(false);
  const [explainLoading, setExplainLoading] = useState(false);
  const [routingExplanation, setRoutingExplanation] = useState(null);
  const [explainRequest, setExplainRequest] = useState(getDefaultExplainRequest('OpenAI'));
  const [formData, setFormData] = useState({
    TenantId: '',
    Name: '',
    Hostname: '',
    BasePath: '',
    ApiType: 'OpenAI',
    LoadBalancingMode: 'RoundRobin',
    LoadBalancingPolicyId: '',
    ModelAccessPolicyId: '',
    ModelRunnerEndpointIds: [],
    AdaptiveLoadBalancing: createDefaultAdaptiveLoadBalancing(),
    EndpointGroupIds: [],
    ModelConfigurationIds: [],
    ModelDefinitionIds: [],
    ModelConfigurationMappingsJson: '{}',
    TimeoutMs: 300000,
    AllowEmbeddings: true,
    AllowCompletions: true,
    AllowModelManagement: false,
    StrictMode: false,
    RequestHistoryEnabled: true,
    SessionAffinityMode: 'None',
    SessionAffinityHeader: '',
    SessionTimeoutMs: 600000,
    SessionMaxEntries: 10000,
    Active: true,
    Labels: labelsFromValue([]),
    Tags: tagsFromValue({})
  });

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [vmrResult, reservationResult, tenantsResult, endpointsResult, endpointGroupsResult, configurationsResult, definitionsResult, policiesResult, modelAccessPolicyResult] = await Promise.all([
        api.listVirtualModelRunners({ maxResults: 1000 }),
        api.listVirtualModelRunnerReservations({ maxResults: 1000 }),
        api.listTenants({ maxResults: 1000 }),
        api.listModelRunnerEndpoints({ maxResults: 1000 }),
        api.listEndpointGroups({ maxResults: 1000 }),
        api.listModelConfigurations({ maxResults: 1000 }),
        api.listModelDefinitions({ maxResults: 1000 }),
        api.listLoadBalancingPolicies({ maxResults: 1000 }),
        api.listModelAccessPolicies({ maxResults: 1000 })
      ]);
      setVmrs(vmrResult.Data || []);
      setReservations(reservationResult.Data || []);
      setTenants(tenantsResult.Data || []);
      setEndpoints(endpointsResult.Data || []);
      setEndpointGroups(endpointGroupsResult.Data || []);
      setConfigurations(configurationsResult.Data || []);
      setDefinitions(definitionsResult.Data || []);
      setLoadBalancingPolicies(policiesResult.Data || []);
      setModelAccessPolicies(modelAccessPolicyResult.Data || []);
    } catch (err) {
      setError('Failed to fetch data: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    if (pendingCreate === 'vmr') {
      clearPendingCreate();
      handleCreate();
    }
  }, [pendingCreate]);

  const handleCreate = () => {
    setEditMode(false);
    setValidationResult(null);
    setFormData({
      TenantId: '',
      Name: '',
      Hostname: '',
      BasePath: '',
      ApiType: 'OpenAI',
      LoadBalancingMode: 'RoundRobin',
      LoadBalancingPolicyId: '',
      ModelAccessPolicyId: '',
      ModelRunnerEndpointIds: [],
      AdaptiveLoadBalancing: createDefaultAdaptiveLoadBalancing(),
      EndpointGroupIds: [],
      ModelConfigurationIds: [],
      ModelDefinitionIds: [],
      ModelConfigurationMappingsJson: '{}',
      TimeoutMs: 300000,
      AllowEmbeddings: true,
      AllowCompletions: true,
      AllowModelManagement: false,
      StrictMode: false,
      RequestHistoryEnabled: true,
      SessionAffinityMode: 'None',
      SessionAffinityHeader: '',
      SessionTimeoutMs: 600000,
      SessionMaxEntries: 10000,
      Active: true,
      Labels: labelsFromValue([]),
      Tags: tagsFromValue({})
    });
    setShowForm(true);
  };

  const handleEdit = (vmr) => {
    setEditMode(true);
    setSelectedVmr(vmr);
    setValidationResult(null);
    setFormData({
      TenantId: vmr.TenantId || '',
      Name: vmr.Name || '',
      Hostname: vmr.Hostname || '',
      BasePath: vmr.BasePath || '',
      ApiType: vmr.ApiType || 'OpenAI',
      LoadBalancingMode: vmr.LoadBalancingMode || 'RoundRobin',
      LoadBalancingPolicyId: vmr.LoadBalancingPolicyId || '',
      ModelAccessPolicyId: vmr.ModelAccessPolicyId || '',
      ModelRunnerEndpointIds: vmr.ModelRunnerEndpointIds || [],
      AdaptiveLoadBalancing: normalizeAdaptiveLoadBalancing(vmr.AdaptiveLoadBalancing),
      EndpointGroupIds: vmr.EndpointGroupIds || [],
      ModelConfigurationIds: vmr.ModelConfigurationIds || [],
      ModelDefinitionIds: vmr.ModelDefinitionIds || [],
      ModelConfigurationMappingsJson: JSON.stringify(vmr.ModelConfigurationMappings || {}, null, 2),
      TimeoutMs: vmr.TimeoutMs || 300000,
      AllowEmbeddings: vmr.AllowEmbeddings !== false,
      AllowCompletions: vmr.AllowCompletions !== false,
      AllowModelManagement: vmr.AllowModelManagement === true,
      StrictMode: vmr.StrictMode === true,
      RequestHistoryEnabled: vmr.RequestHistoryEnabled === true,
      SessionAffinityMode: vmr.SessionAffinityMode || 'None',
      SessionAffinityHeader: vmr.SessionAffinityHeader || '',
      SessionTimeoutMs: vmr.SessionTimeoutMs || 600000,
      SessionMaxEntries: vmr.SessionMaxEntries || 10000,
      Active: vmr.Active !== false,
      Labels: labelsFromValue(vmr.Labels),
      Tags: tagsFromValue(vmr.Tags)
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

  const handleOpenLoadModel = (vmr) => {
    setSelectedVmr(vmr);
    setShowLoadModel(true);
  };

  const handleViewReservations = (vmr) => {
    navigate(`/reservations?vmrId=${encodeURIComponent(vmr.Id)}`);
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

  const buildVmrPayload = () => {
    let modelConfigurationMappings = {};

    try {
      modelConfigurationMappings = JSON.parse(formData.ModelConfigurationMappingsJson || '{}');
    } catch (err) {
      throw new Error('Invalid JSON in Model Configuration Mappings');
    }

    return {
      TenantId: formData.TenantId || null,
      Name: formData.Name,
      Hostname: formData.Hostname || null,
      BasePath: normalizeVmrBasePath(formData.BasePath),
      ApiType: formData.ApiType,
      LoadBalancingMode: formData.LoadBalancingMode,
      LoadBalancingPolicyId: formData.LoadBalancingPolicyId || null,
      ModelAccessPolicyId: formData.ModelAccessPolicyId || null,
      ModelRunnerEndpointIds: formData.ModelRunnerEndpointIds,
      AdaptiveLoadBalancing: {
        ...formData.AdaptiveLoadBalancing,
        SampleCount: parseInt(formData.AdaptiveLoadBalancing.SampleCount),
        ColdStartScore: parseFloat(formData.AdaptiveLoadBalancing.ColdStartScore),
        EwmaAlpha: parseFloat(formData.AdaptiveLoadBalancing.EwmaAlpha),
        BackoffBaseMs: parseInt(formData.AdaptiveLoadBalancing.BackoffBaseMs),
        BackoffMaxMs: parseInt(formData.AdaptiveLoadBalancing.BackoffMaxMs),
        FailureThreshold: parseInt(formData.AdaptiveLoadBalancing.FailureThreshold),
        Weights: {
          Success: parseFloat(formData.AdaptiveLoadBalancing.Weights.Success),
          Latency: parseFloat(formData.AdaptiveLoadBalancing.Weights.Latency),
          TimeToFirstToken: parseFloat(formData.AdaptiveLoadBalancing.Weights.TimeToFirstToken),
          Pending: parseFloat(formData.AdaptiveLoadBalancing.Weights.Pending),
          EndpointWeight: parseFloat(formData.AdaptiveLoadBalancing.Weights.EndpointWeight)
        }
      },
      EndpointGroupIds: formData.EndpointGroupIds || [],
      EndpointGroups: [],
      ModelConfigurationIds: formData.ModelConfigurationIds,
      ModelDefinitionIds: formData.ModelDefinitionIds,
      ModelConfigurationMappings: modelConfigurationMappings,
      TimeoutMs: parseInt(formData.TimeoutMs),
      AllowEmbeddings: formData.AllowEmbeddings,
      AllowCompletions: formData.AllowCompletions,
      AllowModelManagement: formData.AllowModelManagement,
      StrictMode: formData.StrictMode,
      RequestHistoryEnabled: formData.RequestHistoryEnabled,
      SessionAffinityMode: formData.SessionAffinityMode,
      SessionAffinityHeader: formData.SessionAffinityHeader || null,
      SessionTimeoutMs: parseInt(formData.SessionTimeoutMs),
      SessionMaxEntries: parseInt(formData.SessionMaxEntries),
      Active: formData.Active,
      Labels: labelsToPayload(formData.Labels),
      Tags: tagsToPayload(formData.Tags)
    };
  };

  const validateDraft = async () => {
    setValidationLoading(true);
    try {
      const payload = buildVmrPayload();
      const result = await api.validateVirtualModelRunner(payload, editMode ? selectedVmr?.Id : null);
      setValidationResult(result);
      return { payload, result };
    } catch (err) {
      if (err.message?.startsWith('Invalid JSON') || err.message?.startsWith('Base path')) {
        setError(err.message);
      } else {
        setError('Failed to validate virtual model runner: ' + err.message);
      }
      throw err;
    } finally {
      setValidationLoading(false);
    }
  };

  const handleViewEffectiveConfiguration = async (vmr) => {
    setSelectedVmr(vmr);
    setShowEffective(true);
    setEffectiveLoading(true);
    try {
      const result = await api.getVirtualModelRunnerEffectiveConfiguration(vmr.Id, vmr.TenantId);
      setEffectiveConfig(result);
    } catch (err) {
      setError('Failed to fetch effective configuration: ' + err.message);
      setEffectiveConfig(null);
    } finally {
      setEffectiveLoading(false);
    }
  };

  const handleOpenExplainRouting = (vmr) => {
    setSelectedVmr(vmr);
    setRoutingExplanation(null);
    setExplainRequest(getDefaultExplainRequest(vmr?.ApiType || 'OpenAI'));
    setShowExplain(true);
  };

  const handleExplainRouting = async (e) => {
    e.preventDefault();
    if (!selectedVmr) {
      return;
    }

    setExplainLoading(true);
    try {
      let headers = {};
      try {
        headers = JSON.parse(explainRequest.HeadersJson || '{}');
      } catch (err) {
        setError('Invalid JSON in Explain Routing headers');
        return;
      }

      const result = await api.explainVirtualModelRunnerRouting(selectedVmr.Id, {
        Method: explainRequest.Method,
        RelativePath: explainRequest.RelativePath,
        SourceIp: explainRequest.SourceIp,
        Headers: headers,
        Body: explainRequest.Body
      }, selectedVmr.TenantId);
      setRoutingExplanation(result);
    } catch (err) {
      setError('Failed to explain routing: ' + err.message);
      setRoutingExplanation(null);
    } finally {
      setExplainLoading(false);
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

  const loadRuntimeStats = async (vmr) => {
    setRuntimeStatsLoading(true);
    try {
      const result = await api.getVirtualModelRunnerRuntimeStats(vmr.Id, { tenantId: vmr.TenantId });
      setRuntimeStatsData(result);
    } catch (err) {
      setError('Failed to fetch runtime stats: ' + err.message);
      setRuntimeStatsData(null);
    } finally {
      setRuntimeStatsLoading(false);
    }
  };

  const handleViewRuntimeStats = async (vmr) => {
    setSelectedVmr(vmr);
    setShowRuntimeStats(true);
    await loadRuntimeStats(vmr);
  };

  const refreshRuntimeStats = async () => {
    if (!selectedVmr) return;
    await loadRuntimeStats(selectedVmr);
  };

  const resetRuntimeStats = async () => {
    if (!selectedVmr) return;
    if (!window.confirm('Reset runtime statistics for this virtual model runner?')) return;
    setRuntimeStatsLoading(true);
    try {
      const result = await api.resetVirtualModelRunnerRuntimeStats(selectedVmr.Id, { tenantId: selectedVmr.TenantId });
      setRuntimeStatsData(result);
    } catch (err) {
      setError('Failed to reset runtime stats: ' + err.message);
    } finally {
      setRuntimeStatsLoading(false);
    }
  };

  const clearRuntimeBackoff = async () => {
    if (!selectedVmr) return;
    if (!window.confirm('Clear transient backoff state for this virtual model runner?')) return;
    setRuntimeStatsLoading(true);
    try {
      const result = await api.clearVirtualModelRunnerRuntimeBackoff(selectedVmr.Id, { tenantId: selectedVmr.TenantId });
      setRuntimeStatsData(result);
    } catch (err) {
      setError('Failed to clear runtime backoff: ' + err.message);
    } finally {
      setRuntimeStatsLoading(false);
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
      const { payload, result } = await validateDraft();
      if (!result?.IsValid) {
        setError('Resolve the blocking validation issues before saving this virtual model runner.');
        return;
      }

      if (editMode) {
        await api.updateVirtualModelRunner(selectedVmr.Id, payload);
      } else {
        await api.createVirtualModelRunner(payload);
        onEntityCreated('vmr');
      }
      setShowForm(false);
      setValidationResult(null);
      fetchData();
    } catch (err) {
      if (!err.message?.startsWith('Invalid JSON') && !err.message?.startsWith('Base path')) {
        setError('Failed to save virtual model runner: ' + err.message);
      }
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

  const updateAdaptiveSetting = (field, value) => {
    setFormData({
      ...formData,
      AdaptiveLoadBalancing: {
        ...formData.AdaptiveLoadBalancing,
        [field]: value
      }
    });
  };

  const updateAdaptiveWeight = (field, value) => {
    setFormData({
      ...formData,
      AdaptiveLoadBalancing: {
        ...formData.AdaptiveLoadBalancing,
        Weights: {
          ...formData.AdaptiveLoadBalancing.Weights,
          [field]: value
        }
      }
    });
  };

  const handleEndpointGroupToggle = (group) => {
    const current = formData.EndpointGroupIds || [];
    const selected = current.includes(group.Id)
      ? current.filter((id) => id !== group.Id)
      : [...current, group.Id];
    const endpointIdSet = new Set(formData.ModelRunnerEndpointIds || []);
    if (!current.includes(group.Id)) {
      (group.EndpointIds || []).forEach((endpointId) => endpointIdSet.add(endpointId));
    }
    setFormData({
      ...formData,
      EndpointGroupIds: selected,
      ModelRunnerEndpointIds: Array.from(endpointIdSet)
    });
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

  const basePathValidationError = getVmrBasePathValidationError(formData.BasePath);

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
          {getVmrBasePathValidationError(item.BasePath) && (
            <small className="error-text" title="This VMR base path does not match the server's routable pattern.">
              Invalid route
            </small>
          )}
        </div>
      )
    },
    {
      key: 'ApiType',
      label: 'API Type',
      tooltip: 'API format exposed by this VMR (OpenAI, vLLM, Gemini, or Ollama)',
      width: '100px'
    },
    {
      key: 'LoadBalancingMode',
      label: 'Load Balancing',
      tooltip: 'How requests are distributed across endpoints. Session affinity, if enabled, pins clients to specific endpoints.',
      width: '220px',
      render: (item) => (
        <span>
          {item.LoadBalancingPolicyId
            ? (loadBalancingPolicies.find((policy) => policy.Id === item.LoadBalancingPolicyId)?.Name || item.LoadBalancingPolicyId)
            : item.LoadBalancingMode}
          {item.LoadBalancingPolicyId && (
            <small style={{ color: 'var(--text-secondary)', marginLeft: '4px' }}>(Policy)</small>
          )}
          {item.SessionAffinityMode && item.SessionAffinityMode !== 'None' && (
            <small style={{ color: 'var(--text-secondary)', marginLeft: '4px' }}>(Pinned)</small>
          )}
        </span>
      )
    },
    {
      key: 'ModelAccessPolicyId',
      label: 'Model Access',
      tooltip: 'Model access policy attached to this VMR',
      width: '180px',
      render: (item) => item.ModelAccessPolicyId
        ? (modelAccessPolicies.find((policy) => policy.Id === item.ModelAccessPolicyId)?.Name || item.ModelAccessPolicyId)
        : <span className="text-muted">None</span>,
      filterValue: (item) => item.ModelAccessPolicyId
        ? (modelAccessPolicies.find((policy) => policy.Id === item.ModelAccessPolicyId)?.Name || item.ModelAccessPolicyId)
        : 'none'
    },
    {
      key: 'ReservationState',
      label: 'Reservation',
      tooltip: 'Current reservation state for this VMR',
      width: '130px',
      render: (item) => {
        const state = getReservationStateForVmr(item, reservations);
        return <span className={`service-state-badge ${state.tone}`} title={state.title}>{state.label}</span>;
      },
      filterValue: (item) => getReservationStateForVmr(item, reservations).label
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
        key: 'RequestHistoryEnabled',
        label: 'History',
        tooltip: 'Whether request history capture is enabled for this route',
        width: '100px',
        render: (item) => (
          <span className={`service-state-badge ${item.RequestHistoryEnabled ? 'success' : 'neutral'}`}>
            {item.RequestHistoryEnabled ? 'Enabled' : 'Off'}
          </span>
        ),
        filterValue: (item) => item.RequestHistoryEnabled ? 'enabled' : 'disabled'
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
              { label: 'Health Data', onClick: () => handleViewHealth(item) },
              { label: 'Runtime Stats', onClick: () => handleViewRuntimeStats(item) },
              { label: 'Effective Config', onClick: () => handleViewEffectiveConfiguration(item) },
              { label: 'Explain Routing', onClick: () => handleOpenExplainRouting(item) },
              { label: 'Reservations', onClick: () => handleViewReservations(item) },
              { label: 'Load Model', onClick: () => handleOpenLoadModel(item) },
              { label: 'Edit', onClick: () => handleEdit(item) },
              { divider: true },
              { label: 'Delete', danger: true, onClick: () => handleDeleteClick(item) }
          ]}
        />
      )
    }
  ];

  const selectedVmrReservations = selectedVmr
    ? reservations
      .filter((reservation) => reservation.VirtualModelRunnerId === selectedVmr.Id)
      .sort((a, b) => new Date(a.StartUtc).getTime() - new Date(b.StartUtc).getTime())
    : [];

  return (
    <div className="view-container">
      <div className="view-header">
        <div>
          <h1>Virtual Model Runners</h1>
          <p className="view-subtitle">Configure virtual model runners that expose model configurations to the network via OpenAI, vLLM, Gemini, or Ollama compatible APIs.</p>
        </div>
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

      <DataTable data={vmrs} columns={columns} loading={loading} onRowClick={handleEdit} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Virtual Model Runner' : 'Create Virtual Model Runner'} wide>
        <form onSubmit={handleSubmit}>
          {validationResult && (
            <div className={`validation-panel ${validationResult.IsValid ? 'success' : 'error'}`}>
              <strong>{validationResult.IsValid ? 'Draft looks internally consistent.' : 'Resolve the blocking validation issues before saving.'}</strong>
              {validationResult.Errors?.length > 0 && (
                <ul className="validation-list">
                  {validationResult.Errors.map((issue, index) => (
                    <li key={`vmr-error-${index}`}>{issue.Message}</li>
                  ))}
                </ul>
              )}
              {validationResult.Warnings?.length > 0 && (
                <ul className="validation-list warning">
                  {validationResult.Warnings.map((issue, index) => (
                    <li key={`vmr-warning-${index}`}>{issue.Message}</li>
                  ))}
                </ul>
              )}
            </div>
          )}

          <div className="form-group">
            <label htmlFor="tenantId" title="Organizational unit that owns this VMR and its associated resources">Tenant</label>
            <select
              id="tenantId"
              value={formData.TenantId}
              onChange={(e) => setFormData({
                ...formData,
                TenantId: e.target.value,
                LoadBalancingPolicyId: '',
                ModelAccessPolicyId: '',
                ModelRunnerEndpointIds: [],
                EndpointGroupIds: [],
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
            <p className="form-help">
              Use <code>/v1.0/api/&lt;name&gt;/</code>. Example request URL:
              <code> {serverUrl}/v1.0/api/my-vmr/v1/chat/completions</code>
            </p>
            {basePathValidationError && (
              <small className="error-text">{basePathValidationError}</small>
            )}
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="apiType" title="API format exposed by this VMR (OpenAI, vLLM, Gemini, or Ollama)">API Type</label>
              <select
                id="apiType"
                value={formData.ApiType}
                onChange={(e) => setFormData({ ...formData, ApiType: e.target.value })}
              >
                <option value="OpenAI">OpenAI</option>
                <option value="vLLM">vLLM</option>
                <option value="Gemini">Gemini</option>
                <option value="Ollama">Ollama</option>
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
              <label htmlFor="loadBalancingPolicyId" title="Attach a reusable load-balancing policy that can use cached health and RigMonitor telemetry">Load Balancing Policy</label>
              <select
                id="loadBalancingPolicyId"
                value={formData.LoadBalancingPolicyId}
                onChange={(e) => setFormData({ ...formData, LoadBalancingPolicyId: e.target.value })}
              >
                <option value="">None</option>
                {loadBalancingPolicies
                  .filter((policy) => !formData.TenantId || policy.TenantId === formData.TenantId)
                  .map((policy) => (
                    <option key={policy.Id} value={policy.Id}>
                      {policy.Name} ({policy.Id})
                    </option>
                  ))}
              </select>
            </div>
            <div className="form-group">
              <label htmlFor="modelAccessPolicyId" title="Attach a model access policy that controls which credentials and users can use models through this VMR">Model Access Policy</label>
              <select
                id="modelAccessPolicyId"
                value={formData.ModelAccessPolicyId}
                onChange={(e) => setFormData({ ...formData, ModelAccessPolicyId: e.target.value })}
              >
                <option value="">None</option>
                {modelAccessPolicies
                  .filter((policy) => !formData.TenantId || policy.TenantId === formData.TenantId)
                  .map((policy) => (
                    <option key={policy.Id} value={policy.Id}>
                      {policy.Name} ({policy.Id})
                    </option>
                  ))}
              </select>
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="loadBalancingMode" title="Selection mode used when no policy is attached and when an attached policy is configured to fall back">Load Balancing Mode</label>
              <select
                id="loadBalancingMode"
                value={formData.LoadBalancingMode}
                onChange={(e) => setFormData({ ...formData, LoadBalancingMode: e.target.value })}
              >
                <option value="RoundRobin">Round Robin</option>
                <option value="Random">Random</option>
                <option value="FirstAvailable">First Available</option>
                <option value="LeastRecentlyUsed">Least Recently Used</option>
                <option value="Adaptive">Adaptive</option>
              </select>
            </div>
          </div>

          {formData.LoadBalancingMode === 'Adaptive' && (
            <div className="detail-section">
              <h3>Adaptive Selection</h3>
              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="adaptiveSampleCount" title="Number of eligible endpoints scored on each routing decision">Sample Count</label>
                  <input id="adaptiveSampleCount" type="number" min="1" max="8" value={formData.AdaptiveLoadBalancing.SampleCount} onChange={(e) => updateAdaptiveSetting('SampleCount', e.target.value)} />
                </div>
                <div className="form-group">
                  <label htmlFor="adaptiveColdStartScore" title="Initial score for endpoints without runtime history">Cold Start Score</label>
                  <input id="adaptiveColdStartScore" type="number" min="0" max="100" value={formData.AdaptiveLoadBalancing.ColdStartScore} onChange={(e) => updateAdaptiveSetting('ColdStartScore', e.target.value)} />
                </div>
                <div className="form-group">
                  <label htmlFor="adaptiveEwmaAlpha" title="EWMA smoothing factor for runtime measurements">EWMA Alpha</label>
                  <input id="adaptiveEwmaAlpha" type="number" min="0.01" max="1" step="0.01" value={formData.AdaptiveLoadBalancing.EwmaAlpha} onChange={(e) => updateAdaptiveSetting('EwmaAlpha', e.target.value)} />
                </div>
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="adaptiveBackoffBase" title="Base transient backoff duration after rate limits or failures">Backoff Base (ms)</label>
                  <input id="adaptiveBackoffBase" type="number" min="1000" step="1000" value={formData.AdaptiveLoadBalancing.BackoffBaseMs} onChange={(e) => updateAdaptiveSetting('BackoffBaseMs', e.target.value)} />
                </div>
                <div className="form-group">
                  <label htmlFor="adaptiveBackoffMax" title="Maximum transient backoff duration">Backoff Max (ms)</label>
                  <input id="adaptiveBackoffMax" type="number" min="1000" step="1000" value={formData.AdaptiveLoadBalancing.BackoffMaxMs} onChange={(e) => updateAdaptiveSetting('BackoffMaxMs', e.target.value)} />
                </div>
                <div className="form-group">
                  <label htmlFor="adaptiveFailureThreshold" title="Consecutive failures before repeated 5xx responses trigger backoff">Failure Threshold</label>
                  <input id="adaptiveFailureThreshold" type="number" min="1" value={formData.AdaptiveLoadBalancing.FailureThreshold} onChange={(e) => updateAdaptiveSetting('FailureThreshold', e.target.value)} />
                </div>
              </div>
              <div className="checkbox-group">
                <label>
                  <input type="checkbox" checked={formData.AdaptiveLoadBalancing.ExcludeBackoffEndpoints} onChange={(e) => updateAdaptiveSetting('ExcludeBackoffEndpoints', e.target.checked)} />
                  Exclude endpoints in transient backoff
                </label>
                <label>
                  <input type="checkbox" checked={formData.AdaptiveLoadBalancing.BackoffBreaksSessionAffinity} onChange={(e) => updateAdaptiveSetting('BackoffBreaksSessionAffinity', e.target.checked)} />
                  Remove session pins during transient backoff
                </label>
              </div>
              <div className="form-row adaptive-weight-row">
                <div className="form-group">
                  <label htmlFor="adaptiveWeightSuccess">Success Weight</label>
                  <input id="adaptiveWeightSuccess" type="number" min="0" value={formData.AdaptiveLoadBalancing.Weights.Success} onChange={(e) => updateAdaptiveWeight('Success', e.target.value)} />
                </div>
                <div className="form-group">
                  <label htmlFor="adaptiveWeightLatency">Latency Weight</label>
                  <input id="adaptiveWeightLatency" type="number" min="0" value={formData.AdaptiveLoadBalancing.Weights.Latency} onChange={(e) => updateAdaptiveWeight('Latency', e.target.value)} />
                </div>
                <div className="form-group">
                  <label htmlFor="adaptiveWeightTtft">TTFT Weight</label>
                  <input id="adaptiveWeightTtft" type="number" min="0" value={formData.AdaptiveLoadBalancing.Weights.TimeToFirstToken} onChange={(e) => updateAdaptiveWeight('TimeToFirstToken', e.target.value)} />
                </div>
                <div className="form-group">
                  <label htmlFor="adaptiveWeightPending">Pending Weight</label>
                  <input id="adaptiveWeightPending" type="number" min="0" value={formData.AdaptiveLoadBalancing.Weights.Pending} onChange={(e) => updateAdaptiveWeight('Pending', e.target.value)} />
                </div>
                <div className="form-group">
                  <label htmlFor="adaptiveWeightEndpoint">Endpoint Weight</label>
                  <input id="adaptiveWeightEndpoint" type="number" min="0" value={formData.AdaptiveLoadBalancing.Weights.EndpointWeight} onChange={(e) => updateAdaptiveWeight('EndpointWeight', e.target.value)} />
                </div>
              </div>
            </div>
          )}

          {formData.LoadBalancingPolicyId && (
            <p className="form-help" style={{ marginTop: '-8px', marginBottom: '16px' }}>
              The attached policy drives endpoint eligibility. Adaptive mode scores the policy survivors; other modes use the policy ranking result unless the policy falls back.
            </p>
          )}

          <div className="form-row vmr-session-affinity-row">
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

          <div className="detail-section">
            <div className="section-header">
              <h3>Endpoint Groups</h3>
              <button type="button" className="btn-secondary" onClick={() => navigate('/endpoint-groups')}>Manage Groups</button>
            </div>
            {!formData.TenantId ? (
              <p className="no-items">Select a tenant first.</p>
            ) : endpointGroups.filter((group) => group.TenantId === formData.TenantId).length < 1 ? (
              <p className="no-items">No endpoint groups are available for this tenant.</p>
            ) : (
              <div className="endpoint-list">
                {endpointGroups
                  .filter((group) => group.TenantId === formData.TenantId)
                  .map((group) => {
                    const selected = (formData.EndpointGroupIds || []).includes(group.Id);
                    const groupEndpoints = (group.EndpointIds || [])
                      .map((endpointId) => endpoints.find((endpoint) => endpoint.Id === endpointId)?.Name || endpointId)
                      .join(', ');
                    return (
                      <label className="endpoint-item endpoint-group-item" key={group.Id}>
                        <input type="checkbox" checked={selected} onChange={() => handleEndpointGroupToggle(group)} />
                        <span className="endpoint-name">{group.Name || group.Id}</span>
                        <span className="endpoint-url">{groupEndpoints || 'No endpoints assigned.'}</span>
                        <span className="endpoint-group-summary">
                          Priority {group.Priority ?? 0}
                          <span>Weight {group.TrafficWeight ?? 0}</span>
                          <span>{(group.EndpointIds || []).length} endpoint(s)</span>
                          <span className={`service-state-badge ${group.Active === false ? 'danger' : 'success'}`}>
                            {group.Active === false ? 'Inactive' : 'Active'}
                          </span>
                        </span>
                      </label>
                    );
                  })}
              </div>
            )}
            {(formData.EndpointGroupIds || []).length < 1 && (
              <p className="form-help">No groups selected. The selected endpoint list is used directly.</p>
            )}
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

          <LabelsTagsEditor
            labels={formData.Labels}
            tags={formData.Tags}
            onLabelsChange={(Labels) => setFormData({ ...formData, Labels })}
            onTagsChange={(Tags) => setFormData({ ...formData, Tags })}
            idPrefix="virtual-model-runner"
          />

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
            <label title="Enable pull, push, and delete model operations (Ollama API only in the current implementation)">
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
            <label title="Capture request/response data for Request History and Analytics. Analytics is empty for this VMR when this is off.">
              <input
                type="checkbox"
                checked={formData.RequestHistoryEnabled}
                onChange={(e) => setFormData({ ...formData, RequestHistoryEnabled: e.target.checked })}
              />
              Request History and Analytics
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
              <button type="button" className="btn-secondary" onClick={() => validateDraft().catch(() => {})} disabled={validationLoading}>
                {validationLoading ? 'Validating...' : 'Validate'}
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

      <LoadModelModal
        isOpen={showLoadModel}
        onClose={() => { setShowLoadModel(false); setSelectedVmr(null); }}
        target={selectedVmr}
        targetType="vmr"
        api={api}
        definitions={definitions}
        endpoints={endpoints}
        onComplete={() => { fetchData(); }}
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
                    <div className="summary-badge-row">
                      <span
                        className={`health-badge ${healthData.OverallHealthy ? 'healthy' : 'unhealthy'}`}
                        title={healthData.OverallHealthy
                          ? `All ${healthData.TotalEndpointCount} endpoint(s) are passing health checks`
                          : `${healthData.TotalEndpointCount - healthData.HealthyEndpointCount} of ${healthData.TotalEndpointCount} endpoint(s) are failing health checks`}
                      >
                        {healthData.OverallHealthy ? 'All Healthy' : 'Issues Detected'}
                      </span>
                      {healthData.DrainingEndpointCount > 0 && (
                        <span className="service-state-badge warning" title="Endpoints intentionally draining new work">
                          {healthData.DrainingEndpointCount} Draining
                        </span>
                      )}
                      {healthData.QuarantinedEndpointCount > 0 && (
                        <span className="service-state-badge danger" title="Endpoints quarantined from routing">
                          {healthData.QuarantinedEndpointCount} Quarantined
                        </span>
                      )}
                    </div>
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
                      <th title="Operator-managed routing state for this endpoint">Service State</th>
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
                        <td>
                          <span className={`service-state-badge ${getServiceStatePresentation(ep.ServiceState).tone}`}>
                            {getServiceStatePresentation(ep.ServiceState).label}
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

        <Modal
          isOpen={showRuntimeStats}
          onClose={() => { setShowRuntimeStats(false); setRuntimeStatsData(null); }}
          title={`Runtime Stats: ${selectedVmr?.Name || 'VMR'}`}
          extraWide
        >
          <div className="detail-content">
            <div className="section-header">
              <div>
                <h3>{selectedVmr?.Name}</h3>
                {runtimeStatsData?.SnapshotUtc && (
                  <p className="health-timestamp">Snapshot: {new Date(runtimeStatsData.SnapshotUtc).toLocaleString()}</p>
                )}
              </div>
              <div className="view-actions">
                <button type="button" className="btn-secondary" onClick={clearRuntimeBackoff} disabled={runtimeStatsLoading}>Clear Backoff</button>
                <button type="button" className="btn-secondary" onClick={resetRuntimeStats} disabled={runtimeStatsLoading}>Reset Stats</button>
                <button type="button" className="btn-icon" onClick={refreshRuntimeStats} disabled={runtimeStatsLoading} title="Refresh runtime stats">
                  <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" className={runtimeStatsLoading ? 'spinning' : ''}>
                    <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
                  </svg>
                </button>
              </div>
            </div>
            {runtimeStatsLoading && !runtimeStatsData ? (
              <div className="loading-spinner">Loading runtime stats...</div>
            ) : runtimeStatsData?.Endpoints?.length ? (
              <div className="health-endpoints">
                <table className="health-table">
                  <thead>
                    <tr>
                      <th>Endpoint</th>
                      <th>Pending</th>
                      <th>Completed</th>
                      <th>Success</th>
                      <th>Error</th>
                      <th>Latency</th>
                      <th>TTFT</th>
                      <th>Backoff</th>
                      <th>Last Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {runtimeStatsData.Endpoints.map((endpoint) => (
                      <tr key={endpoint.EndpointId}>
                        <td>
                          <div className="endpoint-info">
                            <strong>{endpoint.EndpointName || endpoint.EndpointId}</strong>
                            <CopyableId value={endpoint.EndpointId} />
                          </div>
                        </td>
                        <td>{endpoint.Pending ?? 0} / {endpoint.InFlight ?? 0}</td>
                        <td>{endpoint.CompletedCount ?? 0}</td>
                        <td>{endpoint.SuccessEwma !== null && endpoint.SuccessEwma !== undefined ? endpoint.SuccessEwma.toFixed(3) : '-'}</td>
                        <td>{endpoint.ErrorEwma !== null && endpoint.ErrorEwma !== undefined ? endpoint.ErrorEwma.toFixed(3) : '-'}</td>
                        <td>{endpoint.LatencyEwmaMs !== null && endpoint.LatencyEwmaMs !== undefined ? `${endpoint.LatencyEwmaMs.toFixed(0)} ms` : '-'}</td>
                        <td>{endpoint.TimeToFirstTokenEwmaMs !== null && endpoint.TimeToFirstTokenEwmaMs !== undefined ? `${endpoint.TimeToFirstTokenEwmaMs.toFixed(0)} ms` : '-'}</td>
                        <td>
                          <span className={`service-state-badge ${endpoint.BackoffActive ? 'warning' : 'success'}`}>
                            {endpoint.BackoffActive ? (endpoint.BackoffReason || 'Backoff') : 'Clear'}
                          </span>
                          {endpoint.BackoffUntilUtc && <small className="form-help">{new Date(endpoint.BackoffUntilUtc).toLocaleTimeString()}</small>}
                        </td>
                        <td>{endpoint.LastStatusCode ?? endpoint.LastErrorCode ?? '-'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="no-items">No runtime statistics are available yet.</p>
            )}
          </div>
        </Modal>

        <Modal
          isOpen={showEffective}
          onClose={() => { setShowEffective(false); setEffectiveConfig(null); }}
          title={`Effective Configuration: ${selectedVmr?.Name || 'VMR'}`}
          extraWide
        >
          {effectiveLoading ? (
            <div className="loading-spinner">Loading effective configuration...</div>
          ) : effectiveConfig ? (
            <div className="detail-content">
              <div className="detail-section">
                <div className="detail-grid">
                  <div className="detail-item"><label>Base Path</label><code>{effectiveConfig.BasePath}</code></div>
                  <div className="detail-item"><label>API Type</label><span>{effectiveConfig.ApiType}</span></div>
                  <div className="detail-item"><label>Load Balancing</label><span>{effectiveConfig.Policy?.Name || effectiveConfig.LoadBalancingMode}</span></div>
                  <div className="detail-item"><label>Model Access</label><span>{effectiveConfig.ModelAccessPolicy?.PolicyName || effectiveConfig.ModelAccessPolicy?.PolicyId || 'None'}</span></div>
                  <div className="detail-item"><label>Access Mode</label><span>{effectiveConfig.ModelAccessPolicy?.Mode || 'Disabled'}</span></div>
                  <div className="detail-item"><label>Strict Mode</label><span>{effectiveConfig.StrictMode ? 'Enabled' : 'Disabled'}</span></div>
                  <div className="detail-item"><label>Request History</label><span>{effectiveConfig.RequestHistoryEnabled ? 'Enabled' : 'Disabled'}</span></div>
                  <div className="detail-item"><label>Session Affinity</label><span>{effectiveConfig.SessionAffinity?.Mode || 'None'}</span></div>
                </div>
              </div>

              <div className="detail-section">
                <h3>Reservations</h3>
                {selectedVmrReservations.length < 1 ? (
                  <p className="no-items">No reservations are scheduled for this VMR.</p>
                ) : (
                  <div className="summary-badge-row">
                    {selectedVmrReservations.slice(0, 6).map((reservation) => {
                      const state = getReservationStateForVmr(selectedVmr, [reservation]);
                      return (
                        <span className={`service-state-badge ${state.tone}`} key={reservation.Id} title={`${reservation.Name || reservation.Id}: ${new Date(reservation.StartUtc).toISOString()} to ${new Date(reservation.EndUtc).toISOString()}`}>
                          {reservation.Name || reservation.Id}
                        </span>
                      );
                    })}
                  </div>
                )}
              </div>

              <div className="detail-section">
                <h3>Permissions</h3>
                <div className="summary-badge-row">
                  <span className={`service-state-badge ${effectiveConfig.Permissions?.AllowEmbeddings ? 'success' : 'neutral'}`}>Embeddings {effectiveConfig.Permissions?.AllowEmbeddings ? 'On' : 'Off'}</span>
                  <span className={`service-state-badge ${effectiveConfig.Permissions?.AllowCompletions ? 'success' : 'neutral'}`}>Completions {effectiveConfig.Permissions?.AllowCompletions ? 'On' : 'Off'}</span>
                  <span className={`service-state-badge ${effectiveConfig.Permissions?.AllowModelManagement ? 'success' : 'neutral'}`}>Model Mgmt {effectiveConfig.Permissions?.AllowModelManagement ? 'On' : 'Off'}</span>
                </div>
              </div>

              <div className="detail-section">
                <h3>Resolved Endpoints</h3>
                <div className="explain-candidate-list">
                  {(effectiveConfig.Endpoints || []).map((endpoint) => (
                    <div className="explain-candidate-card" key={endpoint.Id}>
                      <div className="explain-candidate-header">
                        <strong>{endpoint.Name}</strong>
                        <span className={`service-state-badge ${getServiceStatePresentation(endpoint.ServiceState).tone}`}>{getServiceStatePresentation(endpoint.ServiceState).label}</span>
                      </div>
                      <div className="detail-grid">
                        <div className="detail-item"><label>URL</label><code>{endpoint.Url}</code></div>
                        <div className="detail-item"><label>Weight</label><span>{endpoint.Weight}</span></div>
                        <div className="detail-item"><label>Max Parallel</label><span>{endpoint.MaxParallelRequests}</span></div>
                        <div className="detail-item"><label>Active</label><span>{endpoint.Active ? 'Yes' : 'No'}</span></div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div className="detail-section">
                <h3>Model Resolution</h3>
                <div className="detail-grid">
                  <div className="detail-item">
                    <label>Definitions</label>
                    <span>{(effectiveConfig.ModelDefinitions || []).map((item) => item.Name).join(', ') || 'None attached'}</span>
                  </div>
                  <div className="detail-item">
                    <label>Configurations</label>
                    <span>{(effectiveConfig.ModelConfigurations || []).map((item) => item.Name).join(', ') || 'None attached'}</span>
                  </div>
                </div>
                <div className="detail-item" style={{ marginTop: '12px' }}>
                  <label>Model Configuration Mappings</label>
                  <div className="code-block">
                    {JSON.stringify(effectiveConfig.ModelConfigurationMappings || {}, null, 2)}
                  </div>
                </div>
              </div>
            </div>
          ) : (
            <p className="no-items">No effective configuration available.</p>
          )}
        </Modal>

        <Modal
          isOpen={showExplain}
          onClose={() => { setShowExplain(false); setRoutingExplanation(null); }}
          title={`Explain Routing: ${selectedVmr?.Name || 'VMR'}`}
          extraWide
        >
          <form onSubmit={handleExplainRouting}>
            <div className="form-row">
              <div className="form-group">
                <label htmlFor="explainMethod">Method</label>
                <select id="explainMethod" value={explainRequest.Method} onChange={(e) => setExplainRequest({ ...explainRequest, Method: e.target.value })}>
                  <option value="POST">POST</option>
                  <option value="GET">GET</option>
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="explainSourceIp">Source IP</label>
                <input id="explainSourceIp" value={explainRequest.SourceIp} onChange={(e) => setExplainRequest({ ...explainRequest, SourceIp: e.target.value })} />
              </div>
            </div>

            <div className="form-group">
              <label htmlFor="explainRelativePath">Relative Path</label>
              <input id="explainRelativePath" value={explainRequest.RelativePath} onChange={(e) => setExplainRequest({ ...explainRequest, RelativePath: e.target.value })} />
            </div>

            <div className="form-group">
              <label htmlFor="explainHeaders">Headers JSON</label>
              <textarea id="explainHeaders" rows="4" className="code-input" value={explainRequest.HeadersJson} onChange={(e) => setExplainRequest({ ...explainRequest, HeadersJson: e.target.value })} />
            </div>

            <div className="form-group">
              <label htmlFor="explainBody">Body</label>
              <textarea id="explainBody" rows="8" className="code-input" value={explainRequest.Body} onChange={(e) => setExplainRequest({ ...explainRequest, Body: e.target.value })} />
            </div>

            <div className="form-actions">
              <button type="button" className="btn-secondary" onClick={() => setShowExplain(false)}>Close</button>
              <button type="submit" className="btn-primary" disabled={explainLoading}>
                {explainLoading ? 'Explaining...' : 'Run Explanation'}
              </button>
            </div>
          </form>

          {routingExplanation && (
            <div className="detail-content">
              <div className="detail-section">
                <div className="summary-badge-row">
                  <span className={`service-state-badge ${routingExplanation.Success ? 'success' : 'danger'}`}>{routingExplanation.Success ? 'Routed' : 'Denied'}</span>
                  <span className="service-state-badge neutral">HTTP {routingExplanation.HttpStatusCode}</span>
                  {routingExplanation.SessionAffinityOutcome && <span className="service-state-badge neutral">Session {routingExplanation.SessionAffinityOutcome}</span>}
                  {routingExplanation.PolicyFallbackUsed && <span className="service-state-badge warning">Policy Fallback</span>}
                  {routingExplanation.ModelAccessDecision && <span className={`service-state-badge ${routingExplanation.ModelAccessDecision === 'Deny' ? 'warning' : 'success'}`}>Access {routingExplanation.ModelAccessDecision}</span>}
                  {routingExplanation.ModelAccessWouldDeny && <span className="service-state-badge warning">Would Deny</span>}
                  {routingExplanation.AdaptiveModeUsed && <span className="service-state-badge neutral">Adaptive {routingExplanation.AdaptiveSampleCount || 0}</span>}
                  {routingExplanation.SelectedEndpointGroupName && <span className="service-state-badge neutral">Group {routingExplanation.SelectedEndpointGroupName}</span>}
                </div>
                <p className="section-description" style={{ marginTop: '12px', marginBottom: 0 }}>{routingExplanation.Message}</p>
                {(routingExplanation.ModelAccessPolicyId || routingExplanation.ModelAccessRuleId) && (
                  <div className="detail-grid detail-grid-two" style={{ marginTop: '12px' }}>
                    <div className="detail-item"><label>Access Policy</label><span>{routingExplanation.ModelAccessPolicyName || routingExplanation.ModelAccessPolicyId || '-'}</span></div>
                    <div className="detail-item"><label>Access Rule</label><span>{routingExplanation.ModelAccessRuleName || routingExplanation.ModelAccessRuleId || 'Default'}</span></div>
                  </div>
                )}
              </div>

              <div className="detail-section">
                <h3>Timeline</h3>
                <div className="timeline-list">
                  {(routingExplanation.Timeline || []).map((stage, index) => (
                    <div className="timeline-item" key={`${stage.Code}-${index}`}>
                      <div className="timeline-item-header">
                        <strong>{stage.Title}</strong>
                        <span className={`service-state-badge ${stage.Outcome === 'Passed' ? 'success' : stage.Outcome === 'Denied' ? 'danger' : stage.Outcome === 'Fallback' ? 'warning' : 'neutral'}`}>{stage.Outcome}</span>
                      </div>
                      <p>{stage.Message}</p>
                    </div>
                  ))}
                </div>
              </div>

              <div className="detail-section">
                <h3>Candidates</h3>
                <div className="explain-candidate-list">
                  {(routingExplanation.Candidates || []).map((candidate) => (
                    <div className="explain-candidate-card" key={candidate.EndpointId}>
                      <div className="explain-candidate-header">
                        <strong>{candidate.EndpointName || candidate.EndpointId}</strong>
                        <div className="summary-badge-row">
                          <span className={`service-state-badge ${candidate.Selected ? 'success' : candidate.Included ? 'neutral' : 'danger'}`}>{candidate.Selected ? 'Selected' : candidate.Included ? 'Eligible' : 'Excluded'}</span>
                          <span className={`service-state-badge ${getServiceStatePresentation(candidate.ServiceState).tone}`}>{getServiceStatePresentation(candidate.ServiceState).label}</span>
                        </div>
                      </div>
                      <div className="detail-grid">
                        <div className="detail-item"><label>URL</label><code>{candidate.EndpointUrl}</code></div>
                        <div className="detail-item"><label>Healthy</label><span>{candidate.IsHealthy ? 'Yes' : 'No'}</span></div>
                        <div className="detail-item"><label>Capacity</label><span>{candidate.HasCapacity ? 'Available' : 'Full'}</span></div>
                        <div className="detail-item"><label>Policy Score</label><span>{candidate.PolicyScore ?? '-'}</span></div>
                        <div className="detail-item"><label>Adaptive Score</label><span>{candidate.AdaptiveScore?.Score !== undefined ? candidate.AdaptiveScore.Score.toFixed(2) : '-'}</span></div>
                        <div className="detail-item"><label>Pending</label><span>{candidate.RuntimeStats?.Pending ?? '-'}</span></div>
                        <div className="detail-item"><label>Latency EWMA</label><span>{candidate.RuntimeStats?.LatencyEwmaMs !== null && candidate.RuntimeStats?.LatencyEwmaMs !== undefined ? `${candidate.RuntimeStats.LatencyEwmaMs.toFixed(0)} ms` : '-'}</span></div>
                        <div className="detail-item"><label>Backoff</label><span>{candidate.RuntimeStats?.BackoffActive ? (candidate.RuntimeStats.BackoffReason || 'Active') : 'Clear'}</span></div>
                      </div>
                      {candidate.AdaptiveScore?.Components && (
                        <div className="code-block">
                          {JSON.stringify(candidate.AdaptiveScore.Components, null, 2)}
                        </div>
                      )}
                      {candidate.ExclusionReason && <p className="form-help">{candidate.ExclusionReason}</p>}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}
        </Modal>
      </div>
    );
  }

export default VirtualModelRunners;
