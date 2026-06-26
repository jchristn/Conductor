import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';
import RefreshButton from '../components/RefreshButton';
import LabelsTagsEditor, { labelsFromValue, labelsToPayload, tagsFromValue, tagsToPayload } from '../components/LabelsTagsEditor';

const ACTIONS = ['Completions', 'Embeddings', 'ListModels', 'ShowModel', 'LoadModel', 'UnloadModel', 'ModelManagement'];
const SUBJECT_TYPES = ['Credential', 'CredentialLabel', 'User', 'UserLabel', 'Tenant', 'Any'];
const RESOURCE_TYPES = ['ModelDefinition', 'ModelName', 'ModelLabel', 'VirtualModelRunner', 'Any'];

function createRule() {
  return {
    Id: '',
    Name: '',
    Priority: 100,
    Effect: 'Allow',
    SubjectType: 'Any',
    SubjectId: '',
    SubjectSelectorJson: '{}',
    ResourceType: 'Any',
    ResourceId: '',
    ResourceSelectorJson: '{}',
    VirtualModelRunnerId: '',
    Actions: ['Completions'],
    Active: true
  };
}

function defaultFormData() {
  return {
    TenantId: '',
    Name: '',
    Description: '',
    DefaultDecision: 'Deny',
    Active: true,
    Labels: labelsFromValue([]),
    Tags: tagsFromValue({}),
    MetadataJson: 'null',
    Rules: [createRule()]
  };
}

function parseJson(raw, fallback) {
  try {
    return { value: JSON.parse(raw || JSON.stringify(fallback)), error: null };
  } catch (err) {
    return { value: fallback, error: err.message };
  }
}

function apiRuleToForm(rule) {
  return {
    Id: rule.Id || '',
    Name: rule.Name || '',
    Priority: rule.Priority ?? 100,
    Effect: rule.Effect || 'Allow',
    SubjectType: rule.SubjectType || 'Any',
    SubjectId: rule.SubjectId || '',
    SubjectSelectorJson: JSON.stringify(rule.SubjectSelector || {}, null, 2),
    ResourceType: rule.ResourceType || 'Any',
    ResourceId: rule.ResourceId || '',
    ResourceSelectorJson: JSON.stringify(rule.ResourceSelector || {}, null, 2),
    VirtualModelRunnerId: rule.VirtualModelRunnerId || '',
    Actions: rule.Actions && rule.Actions.length > 0 ? rule.Actions : ['Completions'],
    Active: rule.Active !== false
  };
}

function summarizeRule(rule) {
  const subject = rule.SubjectType === 'Any' ? 'Any subject' : `${rule.SubjectType}:${rule.SubjectId || 'selector'}`;
  const resource = rule.ResourceType === 'Any' ? 'Any resource' : `${rule.ResourceType}:${rule.ResourceId || 'selector'}`;
  return `${rule.Priority} ${rule.Effect} ${subject} -> ${resource}`;
}

function findSamePriorityConflicts(rules) {
  const byPriority = new Map();
  (rules || []).forEach((rule) => {
    if (!rule.Active) return;
    const key = String(rule.Priority ?? 0);
    const group = byPriority.get(key) || [];
    group.push(rule);
    byPriority.set(key, group);
  });

  return Array.from(byPriority.entries())
    .filter(([, group]) => group.some((rule) => rule.Effect === 'Allow') && group.some((rule) => rule.Effect === 'Deny'))
    .map(([priority, group]) => ({
      priority,
      rules: group.map((rule) => rule.Name || summarizeRule(rule))
    }));
}

function ModelAccessPolicies() {
  const { api, setError } = useApp();
  const [searchParams] = useSearchParams();
  const [policies, setPolicies] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [vmrs, setVmrs] = useState([]);
  const [credentials, setCredentials] = useState([]);
  const [users, setUsers] = useState([]);
  const [modelDefinitions, setModelDefinitions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedPolicy, setSelectedPolicy] = useState(null);
  const [showMetadata, setShowMetadata] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [forceDetach, setForceDetach] = useState(false);
  const [formData, setFormData] = useState(defaultFormData());
  const [validationResult, setValidationResult] = useState(null);
  const [simulationContext, setSimulationContext] = useState({
    CredentialId: '',
    UserId: '',
    VirtualModelRunnerId: '',
    ModelDefinitionId: '',
    RequestedModel: '',
    EffectiveModel: '',
    Action: 'Completions'
  });
  const [simulationResult, setSimulationResult] = useState(null);
  const [simulationLoading, setSimulationLoading] = useState(false);
  const [effectiveContext, setEffectiveContext] = useState({
    TenantId: searchParams.get('tenantId') || '',
    CredentialId: searchParams.get('credentialId') || '',
    UserId: searchParams.get('userId') || '',
    VirtualModelRunnerId: searchParams.get('vmrId') || '',
    ModelDefinitionId: searchParams.get('modelDefinitionId') || '',
    ModelName: searchParams.get('modelName') || '',
    Action: searchParams.get('action') || 'Completions'
  });
  const [effectiveResult, setEffectiveResult] = useState(null);
  const [effectiveLoading, setEffectiveLoading] = useState(false);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [policyResult, tenantResult, vmrResult, credentialResult, userResult, modelResult] = await Promise.all([
        api.listModelAccessPolicies({ maxResults: 1000 }),
        api.listTenants({ maxResults: 1000 }),
        api.listVirtualModelRunners({ maxResults: 1000 }),
        api.listCredentials({ maxResults: 1000 }),
        api.listUsers({ maxResults: 1000 }),
        api.listModelDefinitions({ maxResults: 1000 })
      ]);
      setPolicies(policyResult.Data || []);
      setTenants(tenantResult.Data || []);
      setVmrs(vmrResult.Data || []);
      setCredentials(credentialResult.Data || []);
      setUsers(userResult.Data || []);
      setModelDefinitions(modelResult.Data || []);
    } catch (err) {
      setError('Failed to fetch model access data: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const tenantNameById = useMemo(() => Object.fromEntries(tenants.map((tenant) => [tenant.Id, tenant.Name])), [tenants]);
  const attachedCounts = useMemo(() => {
    const counts = {};
    vmrs.forEach((vmr) => {
      if (vmr.ModelAccessPolicyId) counts[vmr.ModelAccessPolicyId] = (counts[vmr.ModelAccessPolicyId] || 0) + 1;
    });
    return counts;
  }, [vmrs]);
  const formConflicts = useMemo(() => findSamePriorityConflicts(formData.Rules), [formData.Rules]);

  const handleCreate = () => {
    setEditMode(false);
    setSelectedPolicy(null);
    setFormData(defaultFormData());
    setValidationResult(null);
    setShowForm(true);
  };

  const handleEdit = async (policy) => {
    try {
      const fullPolicy = await api.getModelAccessPolicy(policy.Id, policy.TenantId);
      setEditMode(true);
      setSelectedPolicy(fullPolicy);
      setFormData({
        TenantId: fullPolicy.TenantId || '',
        Name: fullPolicy.Name || '',
        Description: fullPolicy.Description || '',
        DefaultDecision: fullPolicy.DefaultDecision || 'Deny',
        Active: fullPolicy.Active !== false,
        Labels: labelsFromValue(fullPolicy.Labels),
        Tags: tagsFromValue(fullPolicy.Tags),
        MetadataJson: JSON.stringify(fullPolicy.Metadata ?? null, null, 2),
        Rules: (fullPolicy.Rules || []).map(apiRuleToForm)
      });
      setValidationResult(null);
      setShowForm(true);
    } catch (err) {
      setError('Failed to load policy: ' + err.message);
    }
  };

  const handleDeleteClick = (policy) => {
    setSelectedPolicy(policy);
    setForceDetach(false);
    setShowDeleteConfirm(true);
  };

  const buildPayload = () => {
    const metadata = parseJson(formData.MetadataJson, null);
    if (metadata.error) throw new Error('Invalid metadata JSON: ' + metadata.error);

    return {
      TenantId: formData.TenantId || null,
      Name: formData.Name,
      Description: formData.Description || null,
      DefaultDecision: formData.DefaultDecision,
      Active: formData.Active,
      Labels: labelsToPayload(formData.Labels),
      Tags: tagsToPayload(formData.Tags),
      Metadata: metadata.value,
      Rules: formData.Rules.map((rule) => {
        const subjectSelector = parseJson(rule.SubjectSelectorJson, {});
        const resourceSelector = parseJson(rule.ResourceSelectorJson, {});
        if (subjectSelector.error) throw new Error(`Invalid subject selector for ${rule.Name || 'rule'}: ${subjectSelector.error}`);
        if (resourceSelector.error) throw new Error(`Invalid resource selector for ${rule.Name || 'rule'}: ${resourceSelector.error}`);
        return {
          Id: rule.Id || null,
          Name: rule.Name,
          Priority: Number(rule.Priority) || 0,
          Effect: rule.Effect,
          SubjectType: rule.SubjectType,
          SubjectId: rule.SubjectId || null,
          SubjectSelector: subjectSelector.value,
          ResourceType: rule.ResourceType,
          ResourceId: rule.ResourceId || null,
          ResourceSelector: resourceSelector.value,
          VirtualModelRunnerId: rule.VirtualModelRunnerId || null,
          Actions: rule.Actions,
          Active: rule.Active
        };
      })
    };
  };

  const handleValidate = async () => {
    try {
      const payload = buildPayload();
      const result = await api.validateModelAccessPolicy(payload);
      setValidationResult(result);
    } catch (err) {
      setError(err.message);
    }
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    try {
      const payload = buildPayload();
      if (editMode) {
        await api.updateModelAccessPolicy(selectedPolicy.Id, payload);
      } else {
        await api.createModelAccessPolicy(payload);
      }
      setShowForm(false);
      fetchData();
    } catch (err) {
      setError('Failed to save model access policy: ' + err.message);
    }
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteModelAccessPolicy(selectedPolicy.Id, selectedPolicy.TenantId, forceDetach);
      setShowDeleteConfirm(false);
      setSelectedPolicy(null);
      fetchData();
    } catch (err) {
      setError('Failed to delete model access policy: ' + err.message);
    } finally {
      setDeleteLoading(false);
    }
  };

  const updateRule = (index, patch) => {
    setFormData((current) => ({
      ...current,
      Rules: current.Rules.map((rule, ruleIndex) => ruleIndex === index ? { ...rule, ...patch } : rule)
    }));
  };

  const toggleAction = (index, action) => {
    const rule = formData.Rules[index];
    const hasAction = rule.Actions.includes(action);
    const nextActions = hasAction ? rule.Actions.filter((item) => item !== action) : [...rule.Actions, action];
    updateRule(index, { Actions: nextActions });
  };

  const handleSimulate = async () => {
    if (!selectedPolicy) return;
    try {
      setSimulationLoading(true);
      const modelDefinition = modelDefinitions.find((item) => item.Id === simulationContext.ModelDefinitionId);
      const result = await api.evaluateModelAccessPolicy(selectedPolicy.Id, {
        TenantId: selectedPolicy.TenantId,
        CredentialId: simulationContext.CredentialId || null,
        UserId: simulationContext.UserId || null,
        VirtualModelRunnerId: simulationContext.VirtualModelRunnerId || null,
        ModelDefinitionId: simulationContext.ModelDefinitionId || null,
        ModelDefinitionName: modelDefinition?.Name || null,
        RequestedModel: simulationContext.RequestedModel || modelDefinition?.Name || null,
        EffectiveModel: simulationContext.EffectiveModel || simulationContext.RequestedModel || modelDefinition?.Name || null,
        Action: simulationContext.Action
      }, selectedPolicy.TenantId);
      setSimulationResult(result);
    } catch (err) {
      setError('Failed to evaluate model access: ' + err.message);
    } finally {
      setSimulationLoading(false);
    }
  };

  const handleEvaluateEffective = async () => {
    try {
      setEffectiveLoading(true);
      const result = await api.getEffectiveModelAccess({
        tenantId: effectiveContext.TenantId || undefined,
        credentialId: effectiveContext.CredentialId || undefined,
        userId: effectiveContext.UserId || undefined,
        vmrId: effectiveContext.VirtualModelRunnerId || undefined,
        modelDefinitionId: effectiveContext.ModelDefinitionId || undefined,
        modelName: effectiveContext.ModelName || undefined,
        action: effectiveContext.Action || undefined
      });
      setEffectiveResult(result);
    } catch (err) {
      setError('Failed to evaluate effective model access: ' + err.message);
    } finally {
      setEffectiveLoading(false);
    }
  };

  const columns = [
    {
      key: 'Id',
      label: 'ID',
      width: '340px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Name',
      label: 'Name'
    },
    {
      key: 'TenantId',
      label: 'Tenant',
      render: (item) => tenantNameById[item.TenantId] || item.TenantId || '-',
      filterValue: (item) => tenantNameById[item.TenantId] || item.TenantId
    },
    {
      key: 'DefaultDecision',
      label: 'Default',
      width: '120px',
      render: (item) => <span className={`service-state-badge ${item.DefaultDecision === 'Deny' ? 'warning' : 'success'}`}>{item.DefaultDecision}</span>
    },
    {
      key: 'Rules',
      label: 'Rules',
      width: '100px',
      render: (item) => (item.Rules || []).length
    },
    {
      key: 'Attached',
      label: 'VMRs',
      width: '90px',
      render: (item) => attachedCounts[item.Id] || 0,
      filterValue: (item) => String(attachedCounts[item.Id] || 0)
    },
    {
      key: 'Active',
      label: 'Status',
      width: '110px',
      render: (item) => <StatusIndicator active={item.Active} />,
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
            { label: 'Edit', onClick: () => handleEdit(item) },
            { label: 'Simulate', onClick: () => { setSelectedPolicy(item); setSimulationResult(null); } },
            { label: 'View Details', onClick: () => { setSelectedPolicy(item); setShowMetadata(true); } },
            { divider: true },
            { label: 'Delete', onClick: () => handleDeleteClick(item), danger: true }
          ]}
        />
      )
    }
  ];

  return (
    <div className="view-container">
      <div className="view-header">
        <div>
          <h1>Model Access Policies</h1>
          <p className="view-subtitle">Tenant-scoped ACLs for proxy model usage.</p>
        </div>
        <div className="view-actions">
          <RefreshButton onClick={fetchData} title="Refresh model access policies" disabled={loading} />
          <button className="btn-primary" onClick={handleCreate}>Create Policy</button>
        </div>
      </div>

      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-label">Policies</div>
          <div className="stat-value">{policies.length}</div>
        </div>
        <div className="stat-card">
          <div className="stat-label">Active</div>
          <div className="stat-value">{policies.filter((policy) => policy.Active !== false).length}</div>
        </div>
        <div className="stat-card">
          <div className="stat-label">Attached VMRs</div>
          <div className="stat-value">{Object.values(attachedCounts).reduce((total, count) => total + count, 0)}</div>
        </div>
      </div>

      <div className="detail-panel" style={{ marginBottom: '16px' }}>
        <div className="detail-section-header">
          <h3>Effective Access</h3>
          {effectiveResult && (
            <span className={`service-state-badge ${effectiveResult.Allowed ? 'success' : 'warning'}`}>
              {effectiveResult.WouldDeny ? 'Would Deny' : effectiveResult.Allowed ? 'Allowed' : 'Denied'}
            </span>
          )}
        </div>
        <div className="form-grid">
          <div className="form-group">
            <label>Tenant</label>
            <select value={effectiveContext.TenantId} onChange={(e) => setEffectiveContext({ ...effectiveContext, TenantId: e.target.value, CredentialId: '', UserId: '', VirtualModelRunnerId: '', ModelDefinitionId: '' })}>
              <option value="">Current scope</option>
              {tenants.map((tenant) => <option key={tenant.Id} value={tenant.Id}>{tenant.Name || tenant.Id}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label>Credential</label>
            <select value={effectiveContext.CredentialId} onChange={(e) => setEffectiveContext({ ...effectiveContext, CredentialId: e.target.value })}>
              <option value="">None</option>
              {credentials.filter((item) => !effectiveContext.TenantId || item.TenantId === effectiveContext.TenantId).map((credential) => (
                <option key={credential.Id} value={credential.Id}>{credential.Name || credential.Id}</option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label>User</label>
            <select value={effectiveContext.UserId} onChange={(e) => setEffectiveContext({ ...effectiveContext, UserId: e.target.value })}>
              <option value="">None</option>
              {users.filter((item) => !effectiveContext.TenantId || item.TenantId === effectiveContext.TenantId).map((user) => (
                <option key={user.Id} value={user.Id}>{user.Email || user.Id}</option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label>VMR</label>
            <select value={effectiveContext.VirtualModelRunnerId} onChange={(e) => setEffectiveContext({ ...effectiveContext, VirtualModelRunnerId: e.target.value })}>
              <option value="">None</option>
              {vmrs.filter((item) => !effectiveContext.TenantId || item.TenantId === effectiveContext.TenantId).map((vmr) => (
                <option key={vmr.Id} value={vmr.Id}>{vmr.Name || vmr.Id}</option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label>Model Definition</label>
            <select value={effectiveContext.ModelDefinitionId} onChange={(e) => setEffectiveContext({ ...effectiveContext, ModelDefinitionId: e.target.value })}>
              <option value="">None</option>
              {modelDefinitions.filter((item) => !effectiveContext.TenantId || item.TenantId === effectiveContext.TenantId).map((definition) => (
                <option key={definition.Id} value={definition.Id}>{definition.Name || definition.Id}</option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label>Model Name</label>
            <input value={effectiveContext.ModelName} onChange={(e) => setEffectiveContext({ ...effectiveContext, ModelName: e.target.value })} />
          </div>
          <div className="form-group">
            <label>Action</label>
            <select value={effectiveContext.Action} onChange={(e) => setEffectiveContext({ ...effectiveContext, Action: e.target.value })}>
              {ACTIONS.map((action) => <option key={action} value={action}>{action}</option>)}
            </select>
          </div>
        </div>
        <div className="form-actions">
          <button className="btn-primary" onClick={handleEvaluateEffective} disabled={effectiveLoading}>{effectiveLoading ? 'Evaluating...' : 'Evaluate Effective Access'}</button>
        </div>
        {effectiveResult && (
          <div className="detail-grid detail-grid-three" style={{ marginTop: '12px' }}>
            <div className="detail-item"><label>Policy</label><span>{effectiveResult.PolicyName || effectiveResult.PolicyId || '-'}</span></div>
            <div className="detail-item"><label>Rule</label><span>{effectiveResult.RuleName || effectiveResult.RuleId || 'Default'}</span></div>
            <div className="detail-item"><label>Reason</label><span>{effectiveResult.ReasonCode || '-'}</span></div>
          </div>
        )}
      </div>

      <DataTable data={policies} columns={columns} loading={loading} />

      {selectedPolicy && !showForm && (
        <div className="detail-panel" style={{ marginTop: '16px' }}>
          <div className="detail-section-header">
            <h3>Simulation</h3>
            <span>{selectedPolicy.Name}</span>
          </div>
          <div className="form-grid">
            <div className="form-group">
              <label>Credential</label>
              <select value={simulationContext.CredentialId} onChange={(e) => setSimulationContext({ ...simulationContext, CredentialId: e.target.value })}>
                <option value="">None</option>
                {credentials.filter((item) => !selectedPolicy.TenantId || item.TenantId === selectedPolicy.TenantId).map((credential) => (
                  <option key={credential.Id} value={credential.Id}>{credential.Name || credential.Id}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>User</label>
              <select value={simulationContext.UserId} onChange={(e) => setSimulationContext({ ...simulationContext, UserId: e.target.value })}>
                <option value="">None</option>
                {users.filter((item) => !selectedPolicy.TenantId || item.TenantId === selectedPolicy.TenantId).map((user) => (
                  <option key={user.Id} value={user.Id}>{user.Email || user.Id}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>VMR</label>
              <select value={simulationContext.VirtualModelRunnerId} onChange={(e) => setSimulationContext({ ...simulationContext, VirtualModelRunnerId: e.target.value })}>
                <option value="">None</option>
                {vmrs.filter((item) => !selectedPolicy.TenantId || item.TenantId === selectedPolicy.TenantId).map((vmr) => (
                  <option key={vmr.Id} value={vmr.Id}>{vmr.Name || vmr.Id}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>Model Definition</label>
              <select value={simulationContext.ModelDefinitionId} onChange={(e) => setSimulationContext({ ...simulationContext, ModelDefinitionId: e.target.value })}>
                <option value="">None</option>
                {modelDefinitions.filter((item) => !selectedPolicy.TenantId || item.TenantId === selectedPolicy.TenantId).map((definition) => (
                  <option key={definition.Id} value={definition.Id}>{definition.Name || definition.Id}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>Model Name</label>
              <input value={simulationContext.RequestedModel} onChange={(e) => setSimulationContext({ ...simulationContext, RequestedModel: e.target.value })} />
            </div>
            <div className="form-group">
              <label>Action</label>
              <select value={simulationContext.Action} onChange={(e) => setSimulationContext({ ...simulationContext, Action: e.target.value })}>
                {ACTIONS.map((action) => <option key={action} value={action}>{action}</option>)}
              </select>
            </div>
          </div>
          <div className="form-actions">
            <button className="btn-primary" onClick={handleSimulate} disabled={simulationLoading}>{simulationLoading ? 'Evaluating...' : 'Evaluate'}</button>
          </div>
          {simulationResult && (
            <div className="validation-panel">
              <span className={`service-state-badge ${simulationResult.Allowed ? 'success' : 'warning'}`}>{simulationResult.Allowed ? 'Allowed' : 'Denied'}</span>
              {simulationResult.WouldDeny && <span className="service-state-badge warning">Would Deny</span>}
              <span>{simulationResult.ReasonCode}</span>
              <span>{simulationResult.PolicyName || simulationResult.PolicyId || '-'}</span>
              <span>{simulationResult.RuleName || simulationResult.RuleId || 'Default'}</span>
            </div>
          )}
        </div>
      )}

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Model Access Policy' : 'Create Model Access Policy'} extraWide>
        <form onSubmit={handleSubmit}>
          <div className="form-grid">
            <div className="form-group">
              <label>Name</label>
              <input value={formData.Name} onChange={(e) => setFormData({ ...formData, Name: e.target.value })} required />
            </div>
            <div className="form-group">
              <label>Tenant</label>
              <select value={formData.TenantId} onChange={(e) => setFormData({ ...formData, TenantId: e.target.value })}>
                <option value="">Current scope</option>
                {tenants.map((tenant) => <option key={tenant.Id} value={tenant.Id}>{tenant.Name || tenant.Id}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>Default Decision</label>
              <select value={formData.DefaultDecision} onChange={(e) => setFormData({ ...formData, DefaultDecision: e.target.value })}>
                <option value="Deny">Deny</option>
                <option value="Permit">Permit</option>
              </select>
            </div>
            <label className="checkbox-label">
              <input type="checkbox" checked={formData.Active} onChange={(e) => setFormData({ ...formData, Active: e.target.checked })} />
              Active
            </label>
          </div>
          <div className="form-group">
            <label>Description</label>
            <textarea value={formData.Description} onChange={(e) => setFormData({ ...formData, Description: e.target.value })} rows={2} />
          </div>

          <div className="detail-section-header">
            <h3>Rules</h3>
            <button type="button" className="btn-secondary" onClick={() => setFormData({ ...formData, Rules: [...formData.Rules, createRule()] })}>Add Rule</button>
          </div>
          {formConflicts.length > 0 && (
            <div className="warning-list">
              {formConflicts.map((conflict) => (
                <div key={conflict.priority} className="warning-item">Priority {conflict.priority} has allow and deny rules; deny wins.</div>
              ))}
            </div>
          )}
          {formData.Rules.map((rule, index) => (
            <div key={index} className="rule-editor">
              <div className="form-grid">
                <div className="form-group">
                  <label>Rule Name</label>
                  <input value={rule.Name} onChange={(e) => updateRule(index, { Name: e.target.value })} required />
                </div>
                <div className="form-group">
                  <label>Priority</label>
                  <input type="number" value={rule.Priority} onChange={(e) => updateRule(index, { Priority: e.target.value })} />
                </div>
                <div className="form-group">
                  <label>Effect</label>
                  <select value={rule.Effect} onChange={(e) => updateRule(index, { Effect: e.target.value })}>
                    <option value="Allow">Allow</option>
                    <option value="Deny">Deny</option>
                  </select>
                </div>
                <label className="checkbox-label">
                  <input type="checkbox" checked={rule.Active} onChange={(e) => updateRule(index, { Active: e.target.checked })} />
                  Active
                </label>
              </div>
              <div className="form-grid">
                <div className="form-group">
                  <label>Subject Type</label>
                  <select value={rule.SubjectType} onChange={(e) => updateRule(index, { SubjectType: e.target.value })}>
                    {SUBJECT_TYPES.map((type) => <option key={type} value={type}>{type}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>Subject ID</label>
                  <input value={rule.SubjectId} onChange={(e) => updateRule(index, { SubjectId: e.target.value })} />
                </div>
                <div className="form-group">
                  <label>Resource Type</label>
                  <select value={rule.ResourceType} onChange={(e) => updateRule(index, { ResourceType: e.target.value })}>
                    {RESOURCE_TYPES.map((type) => <option key={type} value={type}>{type}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>Resource ID</label>
                  <input value={rule.ResourceId} onChange={(e) => updateRule(index, { ResourceId: e.target.value })} />
                </div>
              </div>
              <div className="form-grid">
                <div className="form-group">
                  <label>VMR Scope</label>
                  <select value={rule.VirtualModelRunnerId} onChange={(e) => updateRule(index, { VirtualModelRunnerId: e.target.value })}>
                    <option value="">Any</option>
                    {vmrs.filter((vmr) => !formData.TenantId || vmr.TenantId === formData.TenantId).map((vmr) => (
                      <option key={vmr.Id} value={vmr.Id}>{vmr.Name || vmr.Id}</option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>Subject Selector JSON</label>
                  <textarea value={rule.SubjectSelectorJson} onChange={(e) => updateRule(index, { SubjectSelectorJson: e.target.value })} rows={3} />
                </div>
                <div className="form-group">
                  <label>Resource Selector JSON</label>
                  <textarea value={rule.ResourceSelectorJson} onChange={(e) => updateRule(index, { ResourceSelectorJson: e.target.value })} rows={3} />
                </div>
              </div>
              <div className="checkbox-row">
                {ACTIONS.map((action) => (
                  <label key={action} className="checkbox-label">
                    <input type="checkbox" checked={rule.Actions.includes(action)} onChange={() => toggleAction(index, action)} />
                    {action}
                  </label>
                ))}
              </div>
              <div className="form-actions">
                <button type="button" className="btn-secondary" onClick={() => setFormData({ ...formData, Rules: formData.Rules.filter((_, ruleIndex) => ruleIndex !== index) })}>Remove Rule</button>
              </div>
            </div>
          ))}

          <LabelsTagsEditor
            labels={formData.Labels}
            tags={formData.Tags}
            onLabelsChange={(Labels) => setFormData({ ...formData, Labels })}
            onTagsChange={(Tags) => setFormData({ ...formData, Tags })}
            idPrefix="model-access-policy"
          />

          <div className="form-group">
            <label>Metadata JSON</label>
            <textarea value={formData.MetadataJson} onChange={(e) => setFormData({ ...formData, MetadataJson: e.target.value })} rows={3} />
          </div>

          {validationResult && (
            <div className="validation-panel">
              <span className={`service-state-badge ${validationResult.IsValid ? 'success' : 'warning'}`}>{validationResult.IsValid ? 'Valid' : 'Invalid'}</span>
              {(validationResult.Errors || []).map((item, index) => <span key={`error-${index}`}>{item.Code || item.Message}</span>)}
              {(validationResult.Warnings || []).map((item, index) => <span key={`warning-${index}`}>{item.Code || item.Message}</span>)}
            </div>
          )}

          <div className="form-actions">
            <button type="button" className="btn-secondary" onClick={handleValidate}>Validate</button>
            <button type="button" className="btn-secondary" onClick={() => setShowForm(false)}>Cancel</button>
            <button type="submit" className="btn-primary">{editMode ? 'Update' : 'Create'}</button>
          </div>
        </form>
      </Modal>

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedPolicy?.Name}
        entityType="model access policy"
        loading={deleteLoading}
        message="Delete this model access policy?"
        warningMessage={
          <label className="checkbox-label">
            <input type="checkbox" checked={forceDetach} onChange={(e) => setForceDetach(e.target.checked)} />
            Detach from referencing VMRs before deleting
          </label>
        }
      />

      {showMetadata && selectedPolicy && (
        <ViewMetadataModal
          isOpen={showMetadata}
          onClose={() => setShowMetadata(false)}
          data={selectedPolicy}
          title="Model Access Policy Details"
          subtitle={selectedPolicy.Id}
        />
      )}
    </div>
  );
}

export default ModelAccessPolicies;
