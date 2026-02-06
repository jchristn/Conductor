import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';

function Tenants() {
  const { api, setError } = useApp();
  const [tenants, setTenants] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedTenant, setSelectedTenant] = useState(null);
  const [showMetadata, setShowMetadata] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [formData, setFormData] = useState({
    Name: '',
    Active: true,
    LabelsJson: '[]',
    TagsJson: '{}'
  });

  const fetchTenants = useCallback(async () => {
    try {
      setLoading(true);
      const result = await api.listTenants({ maxResults: 1000 });
      setTenants(result.Data || []);
    } catch (err) {
      setError('Failed to fetch tenants: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError]);

  useEffect(() => {
    fetchTenants();
  }, [fetchTenants]);

  const handleCreate = () => {
    setEditMode(false);
    setFormData({ Name: '', Active: true, LabelsJson: '[]', TagsJson: '{}' });
    setShowForm(true);
  };

  const handleEdit = (tenant) => {
    setEditMode(true);
    setSelectedTenant(tenant);
    setFormData({
      Name: tenant.Name || '',
      Active: tenant.Active !== false,
      LabelsJson: JSON.stringify(tenant.Labels || [], null, 2),
      TagsJson: JSON.stringify(tenant.Tags || {}, null, 2)
    });
    setShowForm(true);
  };

  const handleViewMetadata = (tenant) => {
    setSelectedTenant(tenant);
    setShowMetadata(true);
  };

  const handleDeleteClick = (tenant) => {
    setSelectedTenant(tenant);
    setShowDeleteConfirm(true);
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteTenant(selectedTenant.Id);
      setShowDeleteConfirm(false);
      setSelectedTenant(null);
      fetchTenants();
    } catch (err) {
      setError('Failed to delete tenant: ' + err.message);
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
        Name: formData.Name,
        Active: formData.Active,
        Labels: labels,
        Tags: tags
      };

      if (editMode) {
        await api.updateTenant(selectedTenant.Id, data);
      } else {
        await api.createTenant(data);
      }
      setShowForm(false);
      fetchTenants();
    } catch (err) {
      setError('Failed to save tenant: ' + err.message);
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
      tooltip: 'Unique identifier for this tenant',
      width: '300px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Name',
      label: 'Name',
      tooltip: 'Display name for this tenant'
    },
    {
      key: 'Active',
      label: 'Status',
      tooltip: 'Whether this tenant and its resources are enabled',
      width: '120px',
      render: (item) => <StatusIndicator active={item.Active} />,
      filterValue: (item) => item.Active ? 'active' : 'inactive'
    },
    {
      key: 'CreatedUtc',
      label: 'Created',
      tooltip: 'When this tenant was first created',
      width: '180px',
      render: (item) => formatDate(item.CreatedUtc)
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
        <h1>Tenants</h1>
        <div className="view-actions">
          <button className="btn-icon" onClick={fetchTenants} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
          <button className="btn-primary" onClick={handleCreate}>
            Create Tenant
          </button>
        </div>
      </div>

      <DataTable data={tenants} columns={columns} loading={loading} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Tenant' : 'Create Tenant'}>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="name" title="Display name for this tenant">Name</label>
            <input
              type="text"
              id="name"
              value={formData.Name}
              onChange={(e) => setFormData({ ...formData, Name: e.target.value })}
              required
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
            <label title="Inactive tenants and their resources are disabled">
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

      {showMetadata && selectedTenant && (
        <ViewMetadataModal
          data={selectedTenant}
          title="Tenant Details"
          subtitle={selectedTenant.Id}
          onClose={() => setShowMetadata(false)}
        />
      )}

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedTenant?.Name}
        entityType="tenant"
        loading={deleteLoading}
      />
    </div>
  );
}

export default Tenants;
