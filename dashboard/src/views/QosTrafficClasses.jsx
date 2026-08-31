import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';

// Scheduling tiers mirror the server-side QosClassTierEnum.
const TIER_OPTIONS = [
  'Realtime',
  'Interactive',
  'AgentInteractive',
  'BatchTimebound',
  'BatchBackground',
  'Default',
];

function defaultForm() {
  return {
    TenantId: '',
    Name: '',
    Description: '',
    Tier: 'Default',
  };
}

export default function QosTrafficClasses() {
  const { api } = useApp();
  const [classes, setClasses] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [formData, setFormData] = useState(defaultForm());
  const [error, setError] = useState(null);
  const [selected, setSelected] = useState(null);
  const [showDelete, setShowDelete] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const [classResult, tenantResult] = await Promise.all([
        api.listQosTrafficClasses({ maxResults: 500 }),
        api.listTenants({ maxResults: 500 }),
      ]);
      setClasses(classResult?.Data || classResult?.data || []);
      setTenants(tenantResult?.Data || tenantResult?.data || []);
    } catch (e) {
      setError(e?.message || 'Failed to load traffic classes');
    } finally {
      setLoading(false);
    }
  }, [api]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const tenantName = useCallback((id) => tenants.find((t) => t.Id === id)?.Name || id, [tenants]);

  const handleCreate = () => {
    setEditMode(false);
    setSelected(null);
    setError(null);
    setFormData({ ...defaultForm(), TenantId: tenants[0]?.Id || '' });
    setShowForm(true);
  };

  const handleEdit = (trafficClass) => {
    setEditMode(true);
    setSelected(trafficClass);
    setError(null);
    setFormData({
      TenantId: trafficClass.TenantId || '',
      Name: trafficClass.Name || '',
      Description: trafficClass.Description || '',
      Tier: trafficClass.Tier || 'Default',
    });
    setShowForm(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    const payload = {
      TenantId: formData.TenantId || undefined,
      Name: formData.Name,
      Description: formData.Description || null,
      Tier: formData.Tier,
    };
    try {
      if (editMode && selected) {
        await api.updateQosTrafficClass(selected.Id, payload);
      } else {
        await api.createQosTrafficClass(payload);
      }
      setShowForm(false);
      await fetchData();
    } catch (e2) {
      setError(e2?.message || 'Failed to save traffic class');
    }
  };

  const handleDelete = async () => {
    if (!selected) return;
    try {
      setDeleteLoading(true);
      await api.deleteQosTrafficClass(selected.Id, selected.TenantId);
      setShowDelete(false);
      setSelected(null);
      await fetchData();
    } catch (e) {
      setError(e?.message || 'Failed to delete traffic class');
    } finally {
      setDeleteLoading(false);
    }
  };

  const columns = useMemo(() => ([
    { key: 'Name', label: 'Name', tooltip: 'Class name, unique per tenant' },
    { key: 'Id', label: 'ID', width: '300px', render: (row) => <CopyableId value={row.Id} /> },
    { key: 'TenantId', label: 'Tenant', width: '180px', render: (row) => tenantName(row.TenantId) },
    { key: 'Tier', label: 'Tier', width: '160px', render: (row) => row.Tier || 'Default' },
    {
      key: 'IsSystem',
      label: 'System',
      width: '120px',
      render: (row) => (row.IsSystem
        ? <span className="status-badge">system</span>
        : <span style={{ color: 'var(--text-secondary)' }}>custom</span>),
      filterValue: (row) => (row.IsSystem ? 'system' : 'custom'),
    },
    { key: 'Description', label: 'Description', render: (row) => row.Description || '-' },
    {
      key: 'actions',
      label: 'Actions',
      width: '80px',
      sortable: false,
      filterable: false,
      isAction: true,
      render: (row) => (
        <ActionMenu actions={[
          { label: 'Edit', onClick: () => handleEdit(row) },
          { divider: true },
          { label: 'Delete', danger: true, disabled: row.IsSystem, onClick: () => { setSelected(row); setShowDelete(true); } },
        ]} />
      ),
    },
  ]), [tenantName]);

  return (
    <div className="view-container">
      <div className="view-header">
        <div>
          <h1>Traffic Classes</h1>
          <p className="view-subtitle">Manage the per-tenant QoS class catalog. Classifier rules resolve requests to a class name, and profile topologies schedule classes by name. Seeded system classes cannot be deleted.</p>
        </div>
        <div className="view-actions">
          <button className="btn-icon" onClick={fetchData} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
          <button className="btn-primary" onClick={handleCreate}>Create Traffic Class</button>
        </div>
      </div>

      {error && !showForm && <div className="error-banner">{error}</div>}

      <DataTable data={classes} columns={columns} loading={loading} onRowClick={handleEdit} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Traffic Class' : 'Create Traffic Class'}>
        <form onSubmit={handleSubmit}>
          {error && <div className="error-text" style={{ marginBottom: 12 }}>{error}</div>}

          <div className="form-group">
            <label htmlFor="tc-tenant" title="Tenant that owns this class">Tenant</label>
            <select
              id="tc-tenant"
              value={formData.TenantId}
              onChange={(e) => setFormData({ ...formData, TenantId: e.target.value })}
              disabled={editMode}
              required
            >
              <option value="">Select a tenant</option>
              {tenants.map((t) => <option key={t.Id} value={t.Id}>{t.Name}</option>)}
            </select>
          </div>

          <div className="form-group">
            <label htmlFor="tc-name" title="Class name, unique per tenant">Name</label>
            <input
              id="tc-name"
              type="text"
              value={formData.Name}
              onChange={(e) => setFormData({ ...formData, Name: e.target.value })}
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="tc-description" title="Optional description">Description</label>
            <textarea
              id="tc-description"
              value={formData.Description}
              onChange={(e) => setFormData({ ...formData, Description: e.target.value })}
              rows={2}
            />
          </div>

          <div className="form-group">
            <label htmlFor="tc-tier" title="Suggested scheduling tier a profile can adopt">Tier</label>
            <select
              id="tc-tier"
              value={formData.Tier}
              onChange={(e) => setFormData({ ...formData, Tier: e.target.value })}
            >
              {TIER_OPTIONS.map((tier) => <option key={tier} value={tier}>{tier}</option>)}
            </select>
          </div>

          <div className="form-actions">
            <button type="button" className="btn-secondary" onClick={() => setShowForm(false)}>Cancel</button>
            <button type="submit" className="btn-primary">{editMode ? 'Update' : 'Create'}</button>
          </div>
        </form>
      </Modal>

      <DeleteConfirmModal
        isOpen={showDelete}
        onClose={() => setShowDelete(false)}
        onConfirm={handleDelete}
        entityName={selected?.Name}
        entityType="traffic class"
        loading={deleteLoading}
      />
    </div>
  );
}
