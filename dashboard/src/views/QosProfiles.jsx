import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';
import QueueHierarchyDiagram from '../components/QueueHierarchyDiagram';
import QosClassifierRulesEditor from '../components/QosClassifierRulesEditor';

// Starter templates for the structural (classification + topology + limits) portion of a profile.
const QOS_TEMPLATES = {
  fifo: {
    DefaultClass: 'default',
    IngressMode: 'Single',
    IngressDefaultNode: 'default',
    TailNode: 'default',
    MaxTotalDepth: 0,
    MaxQueueWaitMs: 30000,
    RejectionStatusCode: 429,
    IncludeRetryAfter: true,
    RetryAfterSeconds: 5,
    Rules: [],
    Nodes: [{ Name: 'default', Discipline: 'Fifo', MaxDepth: 0, OverflowPolicy: 'Reject', Classes: [] }],
    Links: [],
    IngressRoutes: [],
  },
  standard: {
    DefaultClass: 'default',
    IngressMode: 'Single',
    IngressDefaultNode: 'workloads',
    TailNode: 'workloads',
    MaxTotalDepth: 0,
    MaxQueueWaitMs: 30000,
    RejectionStatusCode: 429,
    IncludeRetryAfter: true,
    RetryAfterSeconds: 5,
    Rules: [
      { Ordinal: 0, Source: 'Header', MatchKey: 'X-Conductor-Class', Operator: 'Equals', MatchValue: 'realtime', ClassName: 'realtime' },
      { Ordinal: 1, Source: 'Header', MatchKey: 'X-Conductor-Class', Operator: 'Equals', MatchValue: 'human-interactive', ClassName: 'human-interactive' },
      { Ordinal: 2, Source: 'Header', MatchKey: 'X-Conductor-Class', Operator: 'Equals', MatchValue: 'agent-interactive', ClassName: 'agent-interactive' },
    ],
    Nodes: [{
      Name: 'workloads', Discipline: 'Llq', MaxDepth: 0, OverflowPolicy: 'Reject', DefaultWeight: 1,
      Classes: [
        { Ordinal: 0, Kind: 'PriorityClass', ClassName: 'realtime', RatePerSecond: 200, Burst: 400 },
        { Ordinal: 1, Kind: 'PriorityClass', ClassName: 'human-interactive', RatePerSecond: 100, Burst: 200 },
        { Ordinal: 2, Kind: 'FairClass', ClassName: 'agent-interactive', Weight: 8 },
        { Ordinal: 3, Kind: 'FairClass', ClassName: 'default', Weight: 2 },
        { Ordinal: 4, Kind: 'FairClass', ClassName: 'batch-background', Weight: 1 },
      ],
    }],
    Links: [],
    IngressRoutes: [],
  },
  priority: {
    DefaultClass: 'default',
    IngressMode: 'Single',
    IngressDefaultNode: 'egress',
    TailNode: 'egress',
    MaxTotalDepth: 0,
    MaxQueueWaitMs: 30000,
    RejectionStatusCode: 429,
    IncludeRetryAfter: true,
    RetryAfterSeconds: 5,
    Rules: [{ Ordinal: 0, Source: 'Header', MatchKey: 'X-Conductor-Class', Operator: 'Equals', MatchValue: 'human-interactive', ClassName: 'human-interactive' }],
    Nodes: [{
      Name: 'egress', Discipline: 'Priority', MaxDepth: 0, OverflowPolicy: 'Reject', AgingThresholdMs: 2000,
      Classes: [
        { Ordinal: 0, Kind: 'Band', ClassName: 'human-interactive', Band: 0 },
        { Ordinal: 1, Kind: 'Band', ClassName: 'default', Band: 1 },
        { Ordinal: 2, Kind: 'Band', ClassName: 'batch-background', Band: 2 },
      ],
    }],
    Links: [],
    IngressRoutes: [],
  },
};

function defaultForm() {
  return {
    TenantId: '',
    Name: '',
    Description: '',
    Active: true,
    DefinitionJson: JSON.stringify(QOS_TEMPLATES.fifo, null, 2),
  };
}

export default function QosProfiles() {
  const { api } = useApp();
  const [profiles, setProfiles] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [formData, setFormData] = useState(defaultForm());
  const [error, setError] = useState(null);
  const [validation, setValidation] = useState(null);
  const [selected, setSelected] = useState(null);
  const [showDelete, setShowDelete] = useState(false);
  const [topologyView, setTopologyView] = useState('json');

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const [profileResult, tenantResult] = await Promise.all([
        api.listQosProfiles({ maxResults: 500 }),
        api.listTenants({ maxResults: 500 }),
      ]);
      setProfiles(profileResult?.Data || profileResult?.data || []);
      setTenants(tenantResult?.Data || tenantResult?.data || []);
    } catch (e) {
      setError(e?.message || 'Failed to load QoS profiles');
    } finally {
      setLoading(false);
    }
  }, [api]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const tenantName = useCallback((id) => tenants.find((t) => t.Id === id)?.Name || id, [tenants]);

  const buildPayload = () => {
    const definition = JSON.parse(formData.DefinitionJson);
    return {
      ...definition,
      TenantId: formData.TenantId || undefined,
      Name: formData.Name,
      Description: formData.Description || null,
      Active: formData.Active,
    };
  };

  const handleCreate = () => {
    setEditMode(false);
    setSelected(null);
    setError(null);
    setValidation(null);
    setTopologyView('json');
    setFormData({ ...defaultForm(), TenantId: tenants[0]?.Id || '' });
    setShowForm(true);
  };

  const handleEdit = (profile) => {
    setEditMode(true);
    setSelected(profile);
    setError(null);
    setValidation(null);
    setTopologyView('json');
    const definition = {
      DefaultClass: profile.DefaultClass, IngressMode: profile.IngressMode, IngressDefaultNode: profile.IngressDefaultNode,
      TailNode: profile.TailNode, MaxTotalDepth: profile.MaxTotalDepth, MaxQueueWaitMs: profile.MaxQueueWaitMs,
      RejectionStatusCode: profile.RejectionStatusCode, IncludeRetryAfter: profile.IncludeRetryAfter, RetryAfterSeconds: profile.RetryAfterSeconds,
      Rules: profile.Rules || [], Nodes: profile.Nodes || [], Links: profile.Links || [], IngressRoutes: profile.IngressRoutes || [],
    };
    setFormData({
      TenantId: profile.TenantId, Name: profile.Name, Description: profile.Description || '',
      Active: profile.Active, DefinitionJson: JSON.stringify(definition, null, 2),
    });
    setShowForm(true);
  };

  const applyTemplate = (key) => {
    setFormData((prev) => ({ ...prev, DefinitionJson: JSON.stringify(QOS_TEMPLATES[key], null, 2) }));
    setValidation(null);
  };

  // Parse the current definition JSON so the diagram can render the topology. When the JSON is
  // malformed we return an error instead so the diagram view can show a message rather than crash.
  const parsedTopology = useMemo(() => {
    try {
      const parsed = JSON.parse(formData.DefinitionJson);
      return { value: parsed, error: null };
    } catch (err) {
      return { value: null, error: err.message };
    }
  }, [formData.DefinitionJson]);

  // When the diagram edits nodes/links/ingress/tail, merge them back into the definition JSON so
  // the JSON remains the single source of truth for Validate/Save.
  const handleTopologyChange = useCallback((nextTopology) => {
    setFormData((prev) => {
      let current;
      try {
        current = JSON.parse(prev.DefinitionJson);
      } catch {
        current = {};
      }
      const merged = {
        ...current,
        ...nextTopology,
      };
      return { ...prev, DefinitionJson: JSON.stringify(merged, null, 2) };
    });
    setValidation(null);
  }, []);

  // Class names offered to the classifier ClassName field, gathered from the topology's node classes
  // plus the profile's DefaultClass so operators can assign any class already modeled in the profile.
  const classNameOptions = useMemo(() => {
    const value = parsedTopology.value;
    if (!value || typeof value !== 'object') return [];
    const names = new Set();
    if (value.DefaultClass) names.add(value.DefaultClass);
    (Array.isArray(value.Nodes) ? value.Nodes : []).forEach((node) => {
      (Array.isArray(node?.Classes) ? node.Classes : []).forEach((cls) => {
        if (cls?.ClassName) names.add(cls.ClassName);
      });
    });
    return Array.from(names);
  }, [parsedTopology.value]);

  // The classification rules parsed out of the definition JSON. When the JSON is malformed there is
  // nothing to render structurally, so callers fall back to the raw JSON editor below.
  const parsedRules = useMemo(() => {
    const value = parsedTopology.value;
    return value && Array.isArray(value.Rules) ? value.Rules : [];
  }, [parsedTopology.value]);

  // When the structured editor changes the rules, merge them back into the definition JSON so the
  // JSON remains the single source of truth for Validate/Save.
  const handleRulesChange = useCallback((nextRules) => {
    setFormData((prev) => {
      let current;
      try {
        current = JSON.parse(prev.DefinitionJson);
      } catch {
        return prev;
      }
      const merged = { ...current, Rules: nextRules };
      return { ...prev, DefinitionJson: JSON.stringify(merged, null, 2) };
    });
    setValidation(null);
  }, []);

  const handleValidate = async () => {
    setError(null);
    try {
      const result = await api.validateQosProfile(buildPayload());
      setValidation(result);
    } catch (e) {
      setError(e?.message || 'Validation request failed');
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    let payload;
    try {
      payload = buildPayload();
    } catch (parseErr) {
      setError('Profile definition is not valid JSON: ' + parseErr.message);
      return;
    }
    try {
      if (editMode && selected) {
        await api.updateQosProfile(selected.Id, payload);
      } else {
        await api.createQosProfile(payload);
      }
      setShowForm(false);
      await fetchData();
    } catch (e2) {
      setError(e2?.message || 'Failed to save QoS profile');
    }
  };

  const handleDelete = async () => {
    if (!selected) return;
    try {
      await api.deleteQosProfile(selected.Id, selected.TenantId);
      setShowDelete(false);
      setSelected(null);
      await fetchData();
    } catch (e) {
      setError(e?.message || 'Failed to delete QoS profile');
    }
  };

  const columns = useMemo(() => ([
    { key: 'Name', label: 'Name', render: (row) => (
      <span>{row.Name}{row.IsDefault ? <span className="badge" style={{ marginLeft: 8 }}>default</span> : null}</span>
    ) },
    { key: 'Id', label: 'ID', render: (row) => <CopyableId id={row.Id} /> },
    { key: 'TenantId', label: 'Tenant', render: (row) => tenantName(row.TenantId) },
    { key: 'TailNode', label: 'Tail', render: (row) => row.TailNode },
    { key: 'Active', label: 'Active', render: (row) => <StatusIndicator active={row.Active} /> },
    { key: 'actions', label: '', render: (row) => (
      <ActionMenu actions={[
        { label: 'Edit', onClick: () => handleEdit(row) },
        { label: 'Duplicate', onClick: () => { handleEdit(row); setEditMode(false); setSelected(null); setFormData((p) => ({ ...p, Name: (row.Name || '') + ' (Copy)' })); } },
        { divider: true },
        { label: 'Delete', danger: true, disabled: row.IsDefault, onClick: () => { setSelected(row); setShowDelete(true); } },
      ]} />
    ) },
  ]), [tenantName]);

  return (
    <div className="view-container">
      <div className="view-header">
        <div>
          <h1>QoS Profiles</h1>
          <p className="view-subtitle">Classify and queue traffic per virtual model runner. The default FIFO profile is seeded per tenant and cannot be deleted.</p>
        </div>
        <div className="view-header-actions">
          <button className="btn" onClick={fetchData}>Refresh</button>
          <button className="btn btn-primary" onClick={handleCreate}>Create Profile</button>
        </div>
      </div>

      {error && !showForm && <div className="error-banner">{error}</div>}

      <DataTable data={profiles} columns={columns} loading={loading} onRowClick={handleEdit} />

      {showForm && (
        <Modal wide title={editMode ? 'Edit QoS Profile' : 'Create QoS Profile'} onClose={() => setShowForm(false)}>
          <form onSubmit={handleSubmit}>
            {error && <div className="error-text" style={{ marginBottom: 12 }}>{error}</div>}

            <div className="form-row">
              <div className="form-group">
                <label>Tenant</label>
                <select value={formData.TenantId} onChange={(e) => setFormData({ ...formData, TenantId: e.target.value })} disabled={editMode} required>
                  <option value="">Select a tenant</option>
                  {tenants.map((t) => <option key={t.Id} value={t.Id}>{t.Name}</option>)}
                </select>
              </div>
              <div className="form-group">
                <label>Name</label>
                <input type="text" value={formData.Name} onChange={(e) => setFormData({ ...formData, Name: e.target.value })} required />
              </div>
            </div>

            <div className="form-group">
              <label>Description</label>
              <textarea value={formData.Description} onChange={(e) => setFormData({ ...formData, Description: e.target.value })} rows={2} />
            </div>

            <div className="form-group">
              <label>Templates</label>
              <div className="template-buttons">
                <button type="button" className="btn" onClick={() => applyTemplate('fifo')}>FIFO</button>
                <button type="button" className="btn" onClick={() => applyTemplate('standard')}>Standard Workloads</button>
                <button type="button" className="btn" onClick={() => applyTemplate('priority')}>Two-tier priority</button>
              </div>
            </div>

            <div className="form-group">
              <label>Classification rules</label>
              {parsedTopology.error ? (
                <div className="error-text">The definition JSON is not valid, so the rule editor is disabled: {parsedTopology.error}. Fix it in the profile definition below.</div>
              ) : (
                <>
                  <QosClassifierRulesEditor rules={parsedRules} classes={classNameOptions} onChange={handleRulesChange} />
                  <small>Rules are evaluated top to bottom; the first match assigns the request's class. Unmatched requests use <strong>{parsedTopology.value?.DefaultClass || 'the default class'}</strong>. Edits are written back into the definition JSON below, which stays authoritative for Validate and Save.</small>
                </>
              )}
            </div>

            <div className="form-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <label style={{ marginBottom: 0 }}>Profile definition (classification, topology, limits)</label>
                <div className="template-buttons">
                  <button type="button" className={`btn${topologyView === 'json' ? ' btn-primary' : ''}`} onClick={() => setTopologyView('json')}>JSON</button>
                  <button type="button" className={`btn${topologyView === 'diagram' ? ' btn-primary' : ''}`} onClick={() => setTopologyView('diagram')}>Diagram</button>
                </div>
              </div>
              {topologyView === 'json' ? (
                <>
                  <textarea className="code-input" value={formData.DefinitionJson} onChange={(e) => setFormData({ ...formData, DefinitionJson: e.target.value })} rows={20} spellCheck={false} />
                  <small>Edit the classifier rules, queue nodes, links, ingress routes, and limits. Use <strong>Validate</strong> to compile-check the draft before saving.</small>
                </>
              ) : (
                <>
                  {parsedTopology.error ? (
                    <div className="error-text">The definition JSON is not valid, so the diagram is disabled: {parsedTopology.error}. Switch to JSON to fix it.</div>
                  ) : (
                    <QueueHierarchyDiagram topology={parsedTopology.value} onChange={handleTopologyChange} />
                  )}
                  <small>Drag between nodes to add a link, select a node/edge and press Delete to remove it, or use <strong>Add node</strong>. Edits are written back into the JSON, which remains the source of truth for Validate and Save. Fine-tune disciplines, classes, and limits in the JSON view.</small>
                </>
              )}
            </div>

            {validation && (
              <div className={validation.IsValid ? 'success-text' : 'error-text'} style={{ marginBottom: 12 }}>
                {validation.IsValid ? 'Profile is valid.' : ('Invalid: ' + (validation.Errors || []).map((x) => x.Message).join(' '))}
              </div>
            )}

            <div className="form-group">
              <label><input type="checkbox" checked={formData.Active} onChange={(e) => setFormData({ ...formData, Active: e.target.checked })} /> Active</label>
            </div>

            <div className="form-actions">
              <button type="button" className="btn" onClick={handleValidate}>Validate</button>
              <button type="button" className="btn" onClick={() => setShowForm(false)}>Cancel</button>
              <button type="submit" className="btn btn-primary">{editMode ? 'Save' : 'Create'}</button>
            </div>
          </form>
        </Modal>
      )}

      {showDelete && selected && (
        <DeleteConfirmModal
          title="Delete QoS Profile"
          message={`Delete "${selected.Name}"? Referencing runners will be reassigned to the tenant default profile.`}
          onConfirm={handleDelete}
          onCancel={() => setShowDelete(false)}
        />
      )}
    </div>
  );
}
