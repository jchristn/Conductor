import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import CopyableId from '../components/CopyableId';
import LabelsTagsEditor, { labelsFromValue, labelsToPayload, tagsFromValue, tagsToPayload } from '../components/LabelsTagsEditor';

function defaultFormData() {
  return {
    TenantId: '',
    Name: '',
    Description: '',
    Priority: 0,
    Active: true,
    TrafficWeight: 100,
    EndpointIds: [],
    Labels: labelsFromValue([]),
    Tags: tagsFromValue({})
  };
}

function EndpointGroups() {
  const { api, setError } = useApp();
  const [groups, setGroups] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [endpoints, setEndpoints] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedGroup, setSelectedGroup] = useState(null);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [showMetadata, setShowMetadata] = useState(false);
  const [validationResult, setValidationResult] = useState(null);
  const [validationLoading, setValidationLoading] = useState(false);
  const [formData, setFormData] = useState(defaultFormData());

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [groupResult, tenantResult, endpointResult] = await Promise.all([
        api.listEndpointGroups({ maxResults: 1000 }),
        api.listTenants({ maxResults: 1000 }),
        api.listModelRunnerEndpoints({ maxResults: 1000 })
      ]);
      setGroups(groupResult.Data || []);
      setTenants(tenantResult.Data || []);
      setEndpoints(endpointResult.Data || []);
    } catch (err) {
      setError('Failed to fetch endpoint groups: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleCreate = () => {
    setEditMode(false);
    setSelectedGroup(null);
    setValidationResult(null);
    setFormData(defaultFormData());
    setShowForm(true);
  };

  const handleEdit = (group) => {
    setEditMode(true);
    setSelectedGroup(group);
    setValidationResult(null);
    setFormData({
      TenantId: group.TenantId || '',
      Name: group.Name || '',
      Description: group.Description || '',
      Priority: group.Priority ?? 0,
      Active: group.Active !== false,
      TrafficWeight: group.TrafficWeight ?? 100,
      EndpointIds: group.EndpointIds || [],
      Labels: labelsFromValue(group.Labels),
      Tags: tagsFromValue(group.Tags)
    });
    setShowForm(true);
  };

  const buildPayload = () => ({
    TenantId: formData.TenantId,
    Name: formData.Name,
    Description: formData.Description || null,
    Priority: parseInt(formData.Priority, 10),
    Active: formData.Active,
    TrafficWeight: parseInt(formData.TrafficWeight, 10),
    EndpointIds: formData.EndpointIds || [],
    Labels: labelsToPayload(formData.Labels),
    Tags: tagsToPayload(formData.Tags)
  });

  const validateDraft = async () => {
    setValidationLoading(true);
    try {
      const payload = buildPayload();
      const result = await api.validateEndpointGroup(payload, editMode ? selectedGroup?.Id : null);
      setValidationResult(result);
      return { payload, result };
    } catch (err) {
      setError('Failed to validate endpoint group: ' + err.message);
      throw err;
    } finally {
      setValidationLoading(false);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      const { payload, result } = await validateDraft();
      if (!result?.IsValid) {
        setError('Resolve the blocking validation issues before saving this endpoint group.');
        return;
      }

      if (editMode) {
        await api.updateEndpointGroup(selectedGroup.Id, payload);
      } else {
        await api.createEndpointGroup(payload);
      }

      setShowForm(false);
      setValidationResult(null);
      fetchData();
    } catch (err) {
      setError('Failed to save endpoint group: ' + err.message);
    }
  };

  const handleDeleteClick = (group) => {
    setSelectedGroup(group);
    setShowDeleteConfirm(true);
  };

  const handleDeleteConfirm = async () => {
    if (!selectedGroup) return;
    setDeleteLoading(true);
    try {
      await api.deleteEndpointGroup(selectedGroup.Id, selectedGroup.TenantId);
      setShowDeleteConfirm(false);
      setSelectedGroup(null);
      fetchData();
    } catch (err) {
      setError('Failed to delete endpoint group: ' + err.message);
    } finally {
      setDeleteLoading(false);
    }
  };

  const handleEndpointToggle = (endpointId) => {
    const current = formData.EndpointIds || [];
    setFormData({
      ...formData,
      EndpointIds: current.includes(endpointId)
        ? current.filter((id) => id !== endpointId)
        : [...current, endpointId]
    });
  };

  const endpointName = (endpointId) => endpoints.find((endpoint) => endpoint.Id === endpointId)?.Name || endpointId;

  const columns = [
    {
      key: 'Name',
      label: 'Name',
      render: (group) => (
        <div>
          <strong>{group.Name}</strong>
          <div className="text-muted"><CopyableId value={group.Id} /></div>
        </div>
      )
    },
    {
      key: 'TenantId',
      label: 'Tenant',
      render: (group) => tenants.find((tenant) => tenant.Id === group.TenantId)?.Name || group.TenantId
    },
    {
      key: 'Priority',
      label: 'Priority',
      render: (group) => group.Priority ?? 0
    },
    {
      key: 'TrafficWeight',
      label: 'Weight',
      render: (group) => group.TrafficWeight ?? 0
    },
    {
      key: 'EndpointIds',
      label: 'Endpoints',
      render: (group) => (group.EndpointIds || []).length
    },
    {
      key: 'Active',
      label: 'Status',
      render: (group) => <span className={`service-state-badge ${group.Active === false ? 'danger' : 'success'}`}>{group.Active === false ? 'Inactive' : 'Active'}</span>
    },
    {
      key: 'actions',
      label: 'Actions',
      sortable: false,
      render: (group) => (
        <ActionMenu
          actions={[
            { label: 'Edit', onClick: () => handleEdit(group) },
            { label: 'View Metadata', onClick: () => { setSelectedGroup(group); setShowMetadata(true); } },
            { label: 'Delete', onClick: () => handleDeleteClick(group), danger: true }
          ]}
        />
      )
    }
  ];

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>Endpoint Groups</h1>
          <p>Reusable tenant-scoped collections of model runner endpoints for route selection.</p>
        </div>
        <button className="btn-primary" onClick={handleCreate}>Create Group</button>
      </div>

      <DataTable data={groups} columns={columns} loading={loading} onRowClick={handleEdit} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Endpoint Group' : 'Create Endpoint Group'} wide>
        <form onSubmit={handleSubmit}>
          {validationResult && (
            <div className={`validation-panel ${validationResult.IsValid ? 'success' : 'error'}`}>
              <strong>{validationResult.IsValid ? 'Draft looks internally consistent.' : 'Resolve the blocking validation issues before saving.'}</strong>
              {validationResult.Errors?.length > 0 && (
                <ul className="validation-list">
                  {validationResult.Errors.map((issue, index) => <li key={`eg-error-${index}`}>{issue.Message}</li>)}
                </ul>
              )}
              {validationResult.Warnings?.length > 0 && (
                <ul className="validation-list warning">
                  {validationResult.Warnings.map((issue, index) => <li key={`eg-warning-${index}`}>{issue.Message}</li>)}
                </ul>
              )}
            </div>
          )}

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="endpointGroupTenant">Tenant</label>
              <select
                id="endpointGroupTenant"
                value={formData.TenantId}
                onChange={(e) => setFormData({ ...formData, TenantId: e.target.value, EndpointIds: [] })}
              >
                <option value="">-- No Tenant --</option>
                {tenants.map((tenant) => (
                  <option key={tenant.Id} value={tenant.Id}>{tenant.Name} ({tenant.Id})</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label htmlFor="endpointGroupName">Name</label>
              <input id="endpointGroupName" value={formData.Name} onChange={(e) => setFormData({ ...formData, Name: e.target.value })} required />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="endpointGroupDescription">Description</label>
            <textarea id="endpointGroupDescription" value={formData.Description} onChange={(e) => setFormData({ ...formData, Description: e.target.value })} rows="2" />
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="endpointGroupPriority">Priority</label>
              <input id="endpointGroupPriority" type="number" min="0" value={formData.Priority} onChange={(e) => setFormData({ ...formData, Priority: e.target.value })} />
            </div>
            <div className="form-group">
              <label htmlFor="endpointGroupWeight">Traffic Weight</label>
              <input id="endpointGroupWeight" type="number" min="0" value={formData.TrafficWeight} onChange={(e) => setFormData({ ...formData, TrafficWeight: e.target.value })} />
            </div>
            <div className="form-group">
              <label htmlFor="endpointGroupActive">Status</label>
              <select id="endpointGroupActive" value={formData.Active ? 'true' : 'false'} onChange={(e) => setFormData({ ...formData, Active: e.target.value === 'true' })}>
                <option value="true">Active</option>
                <option value="false">Inactive</option>
              </select>
            </div>
          </div>

          <div className="form-group">
            <label>Model Runner Endpoints</label>
            <div className="endpoint-list">
              {!formData.TenantId ? (
                <p className="no-items">Select a tenant first.</p>
              ) : endpoints.filter((endpoint) => endpoint.TenantId === formData.TenantId).length < 1 ? (
                <p className="no-items">No endpoints available for this tenant.</p>
              ) : (
                endpoints.filter((endpoint) => endpoint.TenantId === formData.TenantId).map((endpoint) => (
                  <label key={endpoint.Id} className="endpoint-item">
                    <input type="checkbox" checked={(formData.EndpointIds || []).includes(endpoint.Id)} onChange={() => handleEndpointToggle(endpoint.Id)} />
                    <span className="endpoint-name">{endpoint.Name}</span>
                    <span className="endpoint-url">{endpoint.UseSsl ? 'https' : 'http'}://{endpoint.Hostname}:{endpoint.Port}</span>
                  </label>
                ))
              )}
            </div>
          </div>

          {(formData.EndpointIds || []).length > 0 && (
            <p className="form-help">Selected: {(formData.EndpointIds || []).map(endpointName).join(', ')}</p>
          )}

          <LabelsTagsEditor
            labels={formData.Labels}
            tags={formData.Tags}
            onLabelsChange={(labels) => setFormData({ ...formData, Labels: labels })}
            onTagsChange={(tags) => setFormData({ ...formData, Tags: tags })}
            idPrefix="endpoint-group"
          />

          <div className="form-actions">
            <button type="button" className="btn-secondary" onClick={() => validateDraft()} disabled={validationLoading}>
              {validationLoading ? 'Validating...' : 'Validate'}
            </button>
            <button type="button" className="btn-secondary" onClick={() => setShowForm(false)}>Cancel</button>
            <button type="submit" className="btn-primary">{editMode ? 'Update' : 'Create'}</button>
          </div>
        </form>
      </Modal>

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDeleteConfirm}
        title="Delete Endpoint Group"
        message={`Delete endpoint group "${selectedGroup?.Name || ''}"? It will be detached from virtual model runners.`}
        loading={deleteLoading}
      />

      {showMetadata && selectedGroup && (
        <ViewMetadataModal
          data={selectedGroup}
          title="Endpoint Group Details"
          subtitle={selectedGroup.Id}
          onClose={() => setShowMetadata(false)}
        />
      )}
    </div>
  );
}

export default EndpointGroups;
