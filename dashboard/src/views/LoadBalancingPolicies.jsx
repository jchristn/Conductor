import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';

const POLICY_TEMPLATES = {
  LowestCpu: {
    Name: 'Lowest CPU Utilization',
    Description: 'Prefer the healthiest endpoint with the lowest CPU utilization.',
    Filters: [
      { Metric: 'health.isHealthy', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' },
      { Metric: 'health.hasCapacity', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' }
    ],
    Ranking: [
      { Metric: 'rig.cpu.utilizationPercent', Direction: 'Ascending', Weight: 1 }
    ]
  },
  LowestMemory: {
    Name: 'Lowest Memory Utilization',
    Description: 'Prefer the endpoint using the least memory.',
    Filters: [
      { Metric: 'health.isHealthy', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' },
      { Metric: 'health.hasCapacity', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' }
    ],
    Ranking: [
      { Metric: 'rig.memory.utilizationPercent', Direction: 'Ascending', Weight: 1 }
    ]
  },
  LowestGpu: {
    Name: 'Lowest GPU Utilization',
    Description: 'Prefer GPU-capable hosts with the lowest average GPU utilization.',
    Filters: [
      { Metric: 'health.isHealthy', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' },
      { Metric: 'health.hasCapacity', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' },
      { Metric: 'rig.gpu.available', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' }
    ],
    Ranking: [
      { Metric: 'rig.gpu.avgUtilizationPercent', Direction: 'Ascending', Weight: 1 }
    ]
  },
  MostFreeVram: {
    Name: 'Most Free VRAM',
    Description: 'Prefer GPU-capable hosts with the largest minimum free VRAM headroom.',
    Filters: [
      { Metric: 'health.isHealthy', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' },
      { Metric: 'health.hasCapacity', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' },
      { Metric: 'rig.gpu.available', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' }
    ],
    Ranking: [
      { Metric: 'rig.gpu.minFreeMemoryMegabytes', Direction: 'Descending', Weight: 1 }
    ]
  },
  LeastInFlight: {
    Name: 'Least In-Flight Requests',
    Description: 'Prefer the endpoint currently handling the fewest concurrent requests.',
    Filters: [
      { Metric: 'health.isHealthy', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' },
      { Metric: 'health.hasCapacity', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' }
    ],
    Ranking: [
      { Metric: 'health.inFlightRequests', Direction: 'Ascending', Weight: 1 }
    ]
  },
  BalancedGpu: {
    Name: 'Balanced CPU + GPU + In-Flight',
    Description: 'Blend CPU, GPU, and current queue depth into one placement decision.',
    Filters: [
      { Metric: 'health.isHealthy', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' },
      { Metric: 'health.hasCapacity', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' },
      { Metric: 'rig.gpu.available', Operator: 'Equal', ValueType: 'Boolean', Value: 'true' }
    ],
    Ranking: [
      { Metric: 'rig.gpu.avgUtilizationPercent', Direction: 'Ascending', Weight: 0.5 },
      { Metric: 'rig.cpu.utilizationPercent', Direction: 'Ascending', Weight: 0.3 },
      { Metric: 'health.inFlightRequests', Direction: 'Ascending', Weight: 0.2 }
    ]
  }
};

function defaultFormData() {
  return {
    TenantId: '',
    Name: '',
    Description: '',
    MaxTelemetryAgeMs: 30000,
    FallbackMode: 'UseLegacyLoadBalancingMode',
    TieBreaker: 'RoundRobin',
    Active: true,
    FiltersJson: JSON.stringify(POLICY_TEMPLATES.LowestCpu.Filters, null, 2),
    RankingJson: JSON.stringify(POLICY_TEMPLATES.LowestCpu.Ranking, null, 2),
    LabelsJson: '[]',
    TagsJson: '{}',
    MetadataJson: 'null'
  };
}

function formatJson(value, fallback) {
  if (value === null || value === undefined) return fallback;
  return JSON.stringify(value, null, 2);
}

function normalizeHealthMap(result) {
  const map = {};
  let healthList = null;

  if (Array.isArray(result)) {
    healthList = result;
  } else if (result && Array.isArray(result.Data)) {
    healthList = result.Data;
  } else if (result && Array.isArray(result.Endpoints)) {
    healthList = result.Endpoints;
  }

  if (healthList) {
    healthList.forEach((entry) => {
      const id = entry.EndpointId || entry.endpointId;
      if (id) map[id] = entry;
    });
  }

  return map;
}

function collectMetricIds(policy) {
  const ids = new Set();
  (policy?.Filters || []).forEach((filter) => {
    if (filter?.Metric) ids.add(filter.Metric);
  });
  (policy?.Ranking || []).forEach((rule) => {
    if (rule?.Metric) ids.add(rule.Metric);
  });
  return Array.from(ids);
}

function isFreshTelemetry(rigMonitor, maxAgeMs) {
  if (!rigMonitor?.LastTelemetryUtc) return false;
  return (Date.now() - new Date(rigMonitor.LastTelemetryUtc).getTime()) <= maxAgeMs;
}

function parseJsonInput(raw, fallbackValue) {
  try {
    return { value: JSON.parse(raw), error: null };
  } catch (err) {
    return { value: fallbackValue, error: err.message };
  }
}

function buildPolicyDiagnostics(policy, metricsById, vmrs, endpoints, endpointHealthMap) {
  if (!policy) return null;

  const metricIds = collectMetricIds(policy);
  const metricDefinitions = metricIds.map((id) => metricsById[id] || {
    Id: id,
    Name: id,
    Description: 'This metric is not present in the current catalog.',
    Source: 'Unknown'
  });
  const attachedVmrs = vmrs.filter((vmr) => vmr.LoadBalancingPolicyId === policy.Id);
  const scopedEndpoints = (policy.TenantId
    ? endpoints.filter((endpoint) => endpoint.TenantId === policy.TenantId)
    : endpoints);
  const rigEnabledEndpoints = scopedEndpoints.filter((endpoint) => endpoint.RigMonitor?.Enabled === true);
  const readyRigEndpoints = rigEnabledEndpoints.filter((endpoint) => endpointHealthMap[endpoint.Id]?.RigMonitor?.Ready === true);
  const freshRigEndpoints = rigEnabledEndpoints.filter((endpoint) => isFreshTelemetry(endpointHealthMap[endpoint.Id]?.RigMonitor, policy.MaxTelemetryAgeMs || 30000));
  const gpuReadyEndpoints = readyRigEndpoints.filter((endpoint) => endpointHealthMap[endpoint.Id]?.RigMonitor?.Capabilities?.NvidiaAvailable === true);
  const ollamaReadyEndpoints = readyRigEndpoints.filter((endpoint) => endpointHealthMap[endpoint.Id]?.RigMonitor?.Capabilities?.OllamaAvailable === true);
  const warnings = [];
  const notes = [];

  if ((policy.Filters || []).length < 1) {
    warnings.push('No explicit filters are defined. The proxy still enforces endpoint active, health, and capacity gates before ranking.');
  }

  if ((policy.Ranking || []).length < 1) {
    warnings.push('No ranking rules are defined. Eligible endpoints will fall back entirely to the configured tie-breaker.');
  }

  const usesRigMetrics = metricIds.some((id) => id.startsWith('rig.'));
  const usesGpuMetrics = metricIds.some((id) => id.startsWith('rig.gpu.'));
  const usesOllamaMetrics = metricIds.some((id) => id.startsWith('rig.ollama.'));

  if (usesRigMetrics && rigEnabledEndpoints.length < 1) {
    warnings.push('This policy references RigMonitor metrics, but no endpoints in the selected tenant currently have RigMonitor enabled.');
  } else if (usesRigMetrics && readyRigEndpoints.length < 1) {
    warnings.push('This policy references RigMonitor metrics, but no RigMonitor-enabled endpoints currently report a ready state.');
  } else if (usesRigMetrics && freshRigEndpoints.length < 1) {
    warnings.push('This policy references RigMonitor metrics, but no endpoints currently have fresh telemetry inside the configured max age.');
  }

  if (usesGpuMetrics && gpuReadyEndpoints.length < 1) {
    warnings.push('This policy references GPU metrics, but no endpoints currently report Nvidia GPU capability through RigMonitor.');
  }

  if (usesOllamaMetrics && ollamaReadyEndpoints.length < 1) {
    warnings.push('This policy references Ollama metrics, but no endpoints currently report Ollama availability through RigMonitor.');
  }

  if (attachedVmrs.length < 1) {
    notes.push('This policy is not currently attached to any virtual model runners.');
  }

  if (policy.FallbackMode === 'FailClosed') {
    notes.push('Fail-closed is enabled. If no endpoint satisfies this policy, the request path will return an error instead of using legacy load balancing.');
  }

  if (policy.TieBreaker === 'FirstAvailable' && (policy.Ranking || []).length > 1) {
    notes.push('FirstAvailable tie-breaking can make equally ranked candidates deterministic by endpoint ordering rather than spreading traffic.');
  }

  return {
    metricDefinitions,
    attachedVmrs,
    scopedEndpoints,
    rigEnabledEndpoints,
    readyRigEndpoints,
    freshRigEndpoints,
    gpuReadyEndpoints,
    ollamaReadyEndpoints,
    warnings,
    notes
  };
}

function summarizeFilter(filter) {
  if (!filter) return 'Invalid filter';
  return `${filter.Metric} ${filter.Operator} ${filter.Value}`;
}

function summarizeRanking(rule) {
  if (!rule) return 'Invalid ranking rule';
  return `${rule.Metric} ${rule.Direction} (${rule.Weight})`;
}

function LoadBalancingPolicies() {
  const { api, setError } = useApp();
  const [policies, setPolicies] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [vmrs, setVmrs] = useState([]);
  const [endpoints, setEndpoints] = useState([]);
  const [metrics, setMetrics] = useState([]);
  const [endpointHealthMap, setEndpointHealthMap] = useState({});
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedPolicy, setSelectedPolicy] = useState(null);
  const [showMetadata, setShowMetadata] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [showDiagnostics, setShowDiagnostics] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [formData, setFormData] = useState(defaultFormData());

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [policyResult, tenantResult, vmrResult, metricResult, endpointResult] = await Promise.all([
        api.listLoadBalancingPolicies({ maxResults: 1000 }),
        api.listTenants({ maxResults: 1000 }),
        api.listVirtualModelRunners({ maxResults: 1000 }),
        api.getLoadBalancingPolicyMetrics(),
        api.listModelRunnerEndpoints({ maxResults: 1000 })
      ]);

      setPolicies(policyResult.Data || []);
      setTenants(tenantResult.Data || []);
      setVmrs(vmrResult.Data || []);
      setMetrics(metricResult?.Metrics || []);
      setEndpoints(endpointResult.Data || []);

      try {
        const healthResult = await api.getModelRunnerEndpointsHealth();
        setEndpointHealthMap(normalizeHealthMap(healthResult));
      } catch {
        setEndpointHealthMap({});
      }
    } catch (err) {
      setError('Failed to fetch load-balancing policies: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const metricsById = useMemo(() => {
    const map = {};
    metrics.forEach((metric) => {
      map[metric.Id] = metric;
    });
    return map;
  }, [metrics]);

  const vmrAttachmentCount = useMemo(() => {
    const counts = {};
    vmrs.forEach((vmr) => {
      if (!vmr.LoadBalancingPolicyId) return;
      counts[vmr.LoadBalancingPolicyId] = (counts[vmr.LoadBalancingPolicyId] || 0) + 1;
    });
    return counts;
  }, [vmrs]);

  const selectedPolicyDiagnostics = useMemo(
    () => buildPolicyDiagnostics(selectedPolicy, metricsById, vmrs, endpoints, endpointHealthMap),
    [selectedPolicy, metricsById, vmrs, endpoints, endpointHealthMap]
  );

  const draftFilters = useMemo(() => parseJsonInput(formData.FiltersJson || '[]', []), [formData.FiltersJson]);
  const draftRanking = useMemo(() => parseJsonInput(formData.RankingJson || '[]', []), [formData.RankingJson]);
  const draftPolicyDiagnostics = useMemo(() => {
    if (draftFilters.error || draftRanking.error) return null;

    return buildPolicyDiagnostics({
      Id: selectedPolicy?.Id || null,
      TenantId: formData.TenantId || null,
      Filters: draftFilters.value,
      Ranking: draftRanking.value,
      MaxTelemetryAgeMs: parseInt(formData.MaxTelemetryAgeMs, 10) || 30000,
      FallbackMode: formData.FallbackMode,
      TieBreaker: formData.TieBreaker
    }, metricsById, vmrs, endpoints, endpointHealthMap);
  }, [
    draftFilters,
    draftRanking,
    endpointHealthMap,
    endpoints,
    formData.FallbackMode,
    formData.MaxTelemetryAgeMs,
    formData.TenantId,
    formData.TieBreaker,
    metricsById,
    selectedPolicy,
    vmrs
  ]);

  const handleCreate = () => {
    setEditMode(false);
    setSelectedPolicy(null);
    setFormData(defaultFormData());
    setShowForm(true);
  };

  const handleEdit = (policy) => {
    setEditMode(true);
    setSelectedPolicy(policy);
    setFormData({
      TenantId: policy.TenantId || '',
      Name: policy.Name || '',
      Description: policy.Description || '',
      MaxTelemetryAgeMs: policy.MaxTelemetryAgeMs || 30000,
      FallbackMode: policy.FallbackMode || 'UseLegacyLoadBalancingMode',
      TieBreaker: policy.TieBreaker || 'RoundRobin',
      Active: policy.Active !== false,
      FiltersJson: formatJson(policy.Filters, '[]'),
      RankingJson: formatJson(policy.Ranking, '[]'),
      LabelsJson: formatJson(policy.Labels, '[]'),
      TagsJson: formatJson(policy.Tags, '{}'),
      MetadataJson: formatJson(policy.Metadata, 'null')
    });
    setShowForm(true);
  };

  const handleClone = (policy) => {
    setEditMode(false);
    setSelectedPolicy(null);
    setFormData({
      TenantId: policy.TenantId || '',
      Name: `${policy.Name || 'Policy'} (Copy)`,
      Description: policy.Description || '',
      MaxTelemetryAgeMs: policy.MaxTelemetryAgeMs || 30000,
      FallbackMode: policy.FallbackMode || 'UseLegacyLoadBalancingMode',
      TieBreaker: policy.TieBreaker || 'RoundRobin',
      Active: policy.Active !== false,
      FiltersJson: formatJson(policy.Filters, '[]'),
      RankingJson: formatJson(policy.Ranking, '[]'),
      LabelsJson: formatJson(policy.Labels, '[]'),
      TagsJson: formatJson(policy.Tags, '{}'),
      MetadataJson: formatJson(policy.Metadata, 'null')
    });
    setShowForm(true);
  };

  const handleViewMetadata = (policy) => {
    setSelectedPolicy(policy);
    setShowMetadata(true);
  };

  const handleOpenDiagnostics = (policy) => {
    setSelectedPolicy(policy);
    setShowDiagnostics(true);
  };

  const handleDeleteClick = (policy) => {
    setSelectedPolicy(policy);
    setShowDeleteConfirm(true);
  };

  const applyTemplate = (templateKey) => {
    const template = POLICY_TEMPLATES[templateKey];
    if (!template) return;

    setFormData((current) => ({
      ...current,
      Name: editMode && current.Name ? current.Name : template.Name,
      Description: editMode && current.Description ? current.Description : template.Description,
      FiltersJson: JSON.stringify(template.Filters, null, 2),
      RankingJson: JSON.stringify(template.Ranking, null, 2)
    }));
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteLoadBalancingPolicy(selectedPolicy.Id, selectedPolicy.TenantId);
      setShowDeleteConfirm(false);
      setSelectedPolicy(null);
      fetchData();
    } catch (err) {
      setError('Failed to delete load-balancing policy: ' + err.message);
    } finally {
      setDeleteLoading(false);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    try {
      let filters = [];
      let ranking = [];
      let labels = [];
      let tags = {};
      let metadata = null;

      try {
        filters = JSON.parse(formData.FiltersJson || '[]');
      } catch {
        setError('Invalid JSON in Filters');
        return;
      }

      try {
        ranking = JSON.parse(formData.RankingJson || '[]');
      } catch {
        setError('Invalid JSON in Ranking');
        return;
      }

      try {
        labels = JSON.parse(formData.LabelsJson || '[]');
      } catch {
        setError('Invalid JSON in Labels');
        return;
      }

      try {
        tags = JSON.parse(formData.TagsJson || '{}');
      } catch {
        setError('Invalid JSON in Tags');
        return;
      }

      try {
        metadata = JSON.parse(formData.MetadataJson || 'null');
      } catch {
        setError('Invalid JSON in Metadata');
        return;
      }

      const data = {
        TenantId: formData.TenantId || null,
        Name: formData.Name,
        Description: formData.Description || null,
        MaxTelemetryAgeMs: parseInt(formData.MaxTelemetryAgeMs, 10),
        FallbackMode: formData.FallbackMode,
        TieBreaker: formData.TieBreaker,
        Active: formData.Active,
        Filters: filters,
        Ranking: ranking,
        Labels: labels,
        Tags: tags,
        Metadata: metadata
      };

      if (editMode) {
        await api.updateLoadBalancingPolicy(selectedPolicy.Id, data);
      } else {
        await api.createLoadBalancingPolicy(data);
      }

      setShowForm(false);
      fetchData();
    } catch (err) {
      setError('Failed to save load-balancing policy: ' + err.message);
    }
  };

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      tooltip: 'Unique identifier for this load-balancing policy',
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Name',
      label: 'Name',
      tooltip: 'Display name for this policy'
    },
    {
      key: 'TenantId',
      label: 'Tenant',
      tooltip: 'Tenant that owns this policy',
      width: '180px',
      render: (item) => {
        const tenant = tenants.find((candidate) => candidate.Id === item.TenantId);
        return tenant ? tenant.Name : item.TenantId || '-';
      }
    },
    {
      key: 'Rules',
      label: 'Rules',
      tooltip: 'Number of filters and ranking rules in this policy',
      width: '140px',
      render: (item) => `${(item.Filters || []).length} filters / ${(item.Ranking || []).length} ranks`,
      sortValue: (item) => ((item.Filters || []).length * 1000) + (item.Ranking || []).length
    },
    {
      key: 'Telemetry',
      label: 'Telemetry',
      tooltip: 'Whether this policy references RigMonitor metrics and how much tenant coverage currently exists',
      width: '160px',
      render: (item) => {
        const diagnostics = buildPolicyDiagnostics(item, metricsById, vmrs, endpoints, endpointHealthMap);
        const usesRigMetrics = diagnostics?.metricDefinitions?.some((metric) => metric.Id.startsWith('rig.'));
        if (!usesRigMetrics) {
          return <span style={{ color: 'var(--text-secondary)' }}>Not required</span>;
        }

        return (
          <span title={`${diagnostics.freshRigEndpoints.length}/${diagnostics.rigEnabledEndpoints.length || 0} RigMonitor-enabled endpoints currently have fresh telemetry.`}>
            {diagnostics.freshRigEndpoints.length}/{diagnostics.rigEnabledEndpoints.length || 0} fresh
          </span>
        );
      },
      sortValue: (item) => {
        const diagnostics = buildPolicyDiagnostics(item, metricsById, vmrs, endpoints, endpointHealthMap);
        return diagnostics?.freshRigEndpoints.length || 0;
      }
    },
    {
      key: 'Attachments',
      label: 'VMRs',
      tooltip: 'Number of virtual model runners attached to this policy',
      width: '80px',
      render: (item) => vmrAttachmentCount[item.Id] || 0,
      sortValue: (item) => vmrAttachmentCount[item.Id] || 0
    },
    {
      key: 'Active',
      label: 'Status',
      tooltip: 'Whether this policy is available for routing',
      width: '100px',
      render: (item) => <StatusIndicator active={item.Active !== false} />,
      filterValue: (item) => item.Active !== false ? 'active' : 'inactive'
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
            { label: 'Diagnostics', onClick: () => handleOpenDiagnostics(item) },
            { label: 'View Details', onClick: () => handleViewMetadata(item) },
            { label: 'Clone', onClick: () => handleClone(item) },
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
        <div>
          <h1>Load Balancing Policies</h1>
          <p className="view-subtitle">Create reusable routing policies that combine endpoint health, capacity, and cached RigMonitor telemetry.</p>
        </div>
        <div className="view-actions">
          <button className="btn-icon" onClick={fetchData} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
          <button className="btn-primary" onClick={handleCreate}>
            Create Policy
          </button>
        </div>
      </div>

      <div
        className="health-summary-banner"
        style={{ marginBottom: '20px', display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: '16px', alignItems: 'start' }}
      >
        <div>
          <div style={{ fontWeight: 600, marginBottom: '6px' }}>Metrics Catalog</div>
          <div style={{ color: 'var(--text-secondary)', fontSize: '13px', marginBottom: '10px' }}>
            Policies reference stable Conductor metric IDs instead of raw RigMonitor JSON paths.
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
            {metrics.slice(0, 12).map((metric) => (
              <span key={metric.Id} className="health-badge" title={metric.Description || metric.Id}>
                {metric.Id}
              </span>
            ))}
            {metrics.length > 12 && (
              <span className="health-badge">{metrics.length - 12} more</span>
            )}
          </div>
        </div>
        <div>
          <div style={{ fontWeight: 600, marginBottom: '6px' }}>Quick Start Templates</div>
          <div style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
            Seed a policy with a common routing strategy, clone it, and refine the JSON rules.
          </div>
        </div>
        <div>
          <div style={{ fontWeight: 600, marginBottom: '6px' }}>Fleet Coverage</div>
          <div style={{ color: 'var(--text-secondary)', fontSize: '13px', marginBottom: '10px' }}>
            {endpoints.filter((endpoint) => endpoint.RigMonitor?.Enabled === true).length} of {endpoints.length} endpoints currently have RigMonitor enabled.
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
            <span className="health-badge" title="Policies currently configured in Conductor">
              {policies.length} policies
            </span>
            <span className="health-badge" title="Virtual model runners currently attached to a policy">
              {vmrs.filter((vmr) => vmr.LoadBalancingPolicyId).length} attached VMRs
            </span>
          </div>
        </div>
      </div>

      <DataTable data={policies} columns={columns} loading={loading} onRowClick={handleEdit} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Load Balancing Policy' : 'Create Load Balancing Policy'} wide>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="tenantId" title="Tenant that owns this policy">Tenant</label>
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

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="name" title="Display name for this policy">Name</label>
              <input
                id="name"
                type="text"
                value={formData.Name}
                onChange={(e) => setFormData({ ...formData, Name: e.target.value })}
                required
              />
            </div>
            <div className="form-group">
              <label htmlFor="maxTelemetryAgeMs" title="Maximum allowed RigMonitor telemetry age in milliseconds before telemetry-backed metrics are treated as unavailable">Max Telemetry Age (ms)</label>
              <input
                id="maxTelemetryAgeMs"
                type="number"
                value={formData.MaxTelemetryAgeMs}
                onChange={(e) => setFormData({ ...formData, MaxTelemetryAgeMs: e.target.value })}
                min="1000"
                step="1000"
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="description" title="Operational intent for this policy">Description</label>
            <textarea
              id="description"
              value={formData.Description}
              onChange={(e) => setFormData({ ...formData, Description: e.target.value })}
              rows={3}
            />
          </div>

          <div style={{ marginBottom: '18px' }}>
            <div style={{ fontWeight: 600, marginBottom: '8px' }}>Templates</div>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
              <button type="button" className="btn-secondary" onClick={() => applyTemplate('LowestCpu')}>Lowest CPU</button>
              <button type="button" className="btn-secondary" onClick={() => applyTemplate('LowestMemory')}>Lowest Memory</button>
              <button type="button" className="btn-secondary" onClick={() => applyTemplate('LowestGpu')}>Lowest GPU</button>
              <button type="button" className="btn-secondary" onClick={() => applyTemplate('MostFreeVram')}>Most Free VRAM</button>
              <button type="button" className="btn-secondary" onClick={() => applyTemplate('LeastInFlight')}>Least In-Flight</button>
              <button type="button" className="btn-secondary" onClick={() => applyTemplate('BalancedGpu')}>Blend CPU + GPU</button>
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="fallbackMode" title="What to do when the policy cannot select an endpoint">Fallback Mode</label>
              <select
                id="fallbackMode"
                value={formData.FallbackMode}
                onChange={(e) => setFormData({ ...formData, FallbackMode: e.target.value })}
              >
                <option value="UseLegacyLoadBalancingMode">Use Legacy Load Balancing Mode</option>
                <option value="FailClosed">Fail Closed</option>
              </select>
            </div>
            <div className="form-group">
              <label htmlFor="tieBreaker" title="How to choose between equally ranked endpoints">Tie Breaker</label>
              <select
                id="tieBreaker"
                value={formData.TieBreaker}
                onChange={(e) => setFormData({ ...formData, TieBreaker: e.target.value })}
              >
                <option value="RoundRobin">Round Robin</option>
                <option value="Random">Random</option>
                <option value="FirstAvailable">First Available</option>
              </select>
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="filters" title="Filter rules that endpoints must satisfy before ranking">Filters (JSON)</label>
            <textarea
              id="filters"
              value={formData.FiltersJson}
              onChange={(e) => setFormData({ ...formData, FiltersJson: e.target.value })}
              rows={10}
              className="code-input"
            />
            {draftFilters.error && <small className="error-text">Filters JSON error: {draftFilters.error}</small>}
          </div>

          <div className="form-group">
            <label htmlFor="ranking" title="Ranking rules applied after filtering to score eligible endpoints">Ranking (JSON)</label>
            <textarea
              id="ranking"
              value={formData.RankingJson}
              onChange={(e) => setFormData({ ...formData, RankingJson: e.target.value })}
              rows={10}
              className="code-input"
            />
            {draftRanking.error && <small className="error-text">Ranking JSON error: {draftRanking.error}</small>}
          </div>

          {draftPolicyDiagnostics && (
            <div style={{ marginBottom: '18px', border: '1px solid var(--border-color)', borderRadius: '8px', padding: '16px', background: 'var(--surface-secondary)' }}>
              <div style={{ fontWeight: 600, marginBottom: '10px' }}>Draft Diagnostics</div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '12px', marginBottom: '14px' }}>
                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>Referenced Metrics</div>
                  <div style={{ fontWeight: 600 }}>{draftPolicyDiagnostics.metricDefinitions.length}</div>
                </div>
                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>Tenant Endpoints</div>
                  <div style={{ fontWeight: 600 }}>{draftPolicyDiagnostics.scopedEndpoints.length}</div>
                </div>
                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>RigMonitor Enabled</div>
                  <div style={{ fontWeight: 600 }}>{draftPolicyDiagnostics.rigEnabledEndpoints.length}</div>
                </div>
                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>Fresh Telemetry</div>
                  <div style={{ fontWeight: 600 }}>{draftPolicyDiagnostics.freshRigEndpoints.length}</div>
                </div>
              </div>

              <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', marginBottom: '12px' }}>
                {draftPolicyDiagnostics.metricDefinitions.map((metric) => (
                  <span key={metric.Id} className="health-badge" title={metric.Description || metric.Id}>
                    {metric.Id}
                  </span>
                ))}
              </div>

              {draftPolicyDiagnostics.warnings.length > 0 && (
                <div style={{ marginBottom: '12px' }}>
                  <div style={{ fontWeight: 600, marginBottom: '6px' }}>Warnings</div>
                  {draftPolicyDiagnostics.warnings.map((warning) => (
                    <div key={warning} style={{ color: 'var(--warning-color)', marginBottom: '4px' }}>{warning}</div>
                  ))}
                </div>
              )}

              {draftPolicyDiagnostics.notes.length > 0 && (
                <div>
                  <div style={{ fontWeight: 600, marginBottom: '6px' }}>Notes</div>
                  {draftPolicyDiagnostics.notes.map((note) => (
                    <div key={note} style={{ color: 'var(--text-secondary)', marginBottom: '4px' }}>{note}</div>
                  ))}
                </div>
              )}
            </div>
          )}

          <div className="form-group">
            <label htmlFor="labels" title="String array for categorization and filtering">Labels (JSON)</label>
            <textarea
              id="labels"
              value={formData.LabelsJson}
              onChange={(e) => setFormData({ ...formData, LabelsJson: e.target.value })}
              rows={3}
              className="code-input"
            />
          </div>

          <div className="form-group">
            <label htmlFor="tags" title="Key-value pairs for custom metadata">Tags (JSON)</label>
            <textarea
              id="tags"
              value={formData.TagsJson}
              onChange={(e) => setFormData({ ...formData, TagsJson: e.target.value })}
              rows={3}
              className="code-input"
            />
          </div>

          <div className="form-group">
            <label htmlFor="metadata" title="Optional free-form metadata stored with the policy">Metadata (JSON)</label>
            <textarea
              id="metadata"
              value={formData.MetadataJson}
              onChange={(e) => setFormData({ ...formData, MetadataJson: e.target.value })}
              rows={4}
              className="code-input"
            />
          </div>

          <div className="form-group checkbox-group">
            <label title="Inactive policies remain stored but should not be attached for routing">
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

      {showMetadata && selectedPolicy && (
        <ViewMetadataModal
          data={selectedPolicy}
          title="Load Balancing Policy Details"
          subtitle={selectedPolicy.Id}
          onClose={() => setShowMetadata(false)}
        />
      )}

      <Modal
        isOpen={showDiagnostics}
        onClose={() => { setShowDiagnostics(false); }}
        title={`Policy Diagnostics: ${selectedPolicy?.Name || 'Policy'}`}
        wide
      >
        {selectedPolicy && selectedPolicyDiagnostics && (
          <div style={{ display: 'grid', gap: '18px' }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '12px' }}>
              <div className="health-stat-card">
                <div className="health-stat-label">Attached VMRs</div>
                <div className="health-stat-value">{selectedPolicyDiagnostics.attachedVmrs.length}</div>
              </div>
              <div className="health-stat-card">
                <div className="health-stat-label">Tenant Endpoints</div>
                <div className="health-stat-value">{selectedPolicyDiagnostics.scopedEndpoints.length}</div>
              </div>
              <div className="health-stat-card">
                <div className="health-stat-label">RigMonitor Enabled</div>
                <div className="health-stat-value">{selectedPolicyDiagnostics.rigEnabledEndpoints.length}</div>
              </div>
              <div className="health-stat-card">
                <div className="health-stat-label">Fresh Telemetry</div>
                <div className="health-stat-value">{selectedPolicyDiagnostics.freshRigEndpoints.length}</div>
              </div>
              <div className="health-stat-card">
                <div className="health-stat-label">Fallback Mode</div>
                <div className="health-stat-value">{selectedPolicy.FallbackMode}</div>
              </div>
              <div className="health-stat-card">
                <div className="health-stat-label">Tie Breaker</div>
                <div className="health-stat-value">{selectedPolicy.TieBreaker}</div>
              </div>
            </div>

            {selectedPolicyDiagnostics.warnings.length > 0 && (
              <div>
                <div style={{ fontWeight: 600, marginBottom: '6px' }}>Warnings</div>
                {selectedPolicyDiagnostics.warnings.map((warning) => (
                  <div key={warning} style={{ color: 'var(--warning-color)', marginBottom: '4px' }}>{warning}</div>
                ))}
              </div>
            )}

            {selectedPolicyDiagnostics.notes.length > 0 && (
              <div>
                <div style={{ fontWeight: 600, marginBottom: '6px' }}>Notes</div>
                {selectedPolicyDiagnostics.notes.map((note) => (
                  <div key={note} style={{ color: 'var(--text-secondary)', marginBottom: '4px' }}>{note}</div>
                ))}
              </div>
            )}

            <div>
              <div style={{ fontWeight: 600, marginBottom: '6px' }}>Referenced Metrics</div>
              <div style={{ display: 'grid', gap: '8px' }}>
                {selectedPolicyDiagnostics.metricDefinitions.map((metric) => (
                  <div key={metric.Id} style={{ border: '1px solid var(--border-color)', borderRadius: '8px', padding: '10px 12px' }}>
                    <div style={{ fontWeight: 600 }}>{metric.Id}</div>
                    <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{metric.Name || metric.Id}</div>
                    <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginTop: '4px' }}>{metric.Description || 'No description available.'}</div>
                  </div>
                ))}
              </div>
            </div>

            <div>
              <div style={{ fontWeight: 600, marginBottom: '6px' }}>Filters</div>
              <div style={{ display: 'grid', gap: '6px' }}>
                {(selectedPolicy.Filters || []).length > 0 ? selectedPolicy.Filters.map((filter, index) => (
                  <div key={`${filter.Metric}-${index}`} style={{ color: 'var(--text-secondary)' }}>{summarizeFilter(filter)}</div>
                )) : (
                  <div style={{ color: 'var(--text-secondary)' }}>No explicit filters.</div>
                )}
              </div>
            </div>

            <div>
              <div style={{ fontWeight: 600, marginBottom: '6px' }}>Ranking Rules</div>
              <div style={{ display: 'grid', gap: '6px' }}>
                {(selectedPolicy.Ranking || []).length > 0 ? selectedPolicy.Ranking.map((rule, index) => (
                  <div key={`${rule.Metric}-${index}`} style={{ color: 'var(--text-secondary)' }}>{summarizeRanking(rule)}</div>
                )) : (
                  <div style={{ color: 'var(--text-secondary)' }}>No ranking rules. Tie-breaker will decide among eligible endpoints.</div>
                )}
              </div>
            </div>

            <div>
              <div style={{ fontWeight: 600, marginBottom: '6px' }}>Attached Virtual Model Runners</div>
              <div style={{ display: 'grid', gap: '6px' }}>
                {selectedPolicyDiagnostics.attachedVmrs.length > 0 ? selectedPolicyDiagnostics.attachedVmrs.map((vmr) => (
                  <div key={vmr.Id} style={{ color: 'var(--text-secondary)' }}>
                    {vmr.Name} ({vmr.Id})
                  </div>
                )) : (
                  <div style={{ color: 'var(--text-secondary)' }}>No VMRs currently attach this policy.</div>
                )}
              </div>
            </div>

            <div>
              <div style={{ fontWeight: 600, marginBottom: '6px' }}>RigMonitor Endpoint Coverage</div>
              <div style={{ display: 'grid', gap: '6px' }}>
                {selectedPolicyDiagnostics.rigEnabledEndpoints.length > 0 ? selectedPolicyDiagnostics.rigEnabledEndpoints.map((endpoint) => {
                  const health = endpointHealthMap[endpoint.Id];
                  const rig = health?.RigMonitor;
                  const freshnessLabel = rig?.LastTelemetryUtc && isFreshTelemetry(rig, selectedPolicy.MaxTelemetryAgeMs || 30000) ? 'Fresh' : 'Stale';
                  return (
                    <div key={endpoint.Id} style={{ display: 'flex', justifyContent: 'space-between', gap: '12px', border: '1px solid var(--border-color)', borderRadius: '8px', padding: '8px 10px' }}>
                      <div>
                        <div style={{ fontWeight: 600 }}>{endpoint.Name}</div>
                        <div style={{ fontSize: '13px', color: 'var(--text-secondary)' }}>{endpoint.Id}</div>
                      </div>
                      <div style={{ textAlign: 'right' }}>
                        <div className={`status-badge ${rig?.Ready === false ? 'unhealthy' : 'healthy'}`}>
                          {rig?.Ready === true ? 'Ready' : (rig?.Ready === false ? 'Not Ready' : 'Unknown')}
                        </div>
                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>{freshnessLabel}</div>
                      </div>
                    </div>
                  );
                }) : (
                  <div style={{ color: 'var(--text-secondary)' }}>No RigMonitor-enabled endpoints are available in scope for this policy.</div>
                )}
              </div>
            </div>
          </div>
        )}
      </Modal>

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedPolicy?.Name}
        entityType="load-balancing policy"
        loading={deleteLoading}
      />
    </div>
  );
}

export default LoadBalancingPolicies;
