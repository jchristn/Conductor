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

function Credentials() {
  const { api, setError } = useApp();
  const [credentials, setCredentials] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedCredential, setSelectedCredential] = useState(null);
  const [showMetadata, setShowMetadata] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [formData, setFormData] = useState({
    TenantId: '',
    Name: '',
    UserId: '',
    Active: true,
    LabelsJson: '[]',
    TagsJson: '{}'
  });

  const fetchCredentials = useCallback(async () => {
    try {
      setLoading(true);
      const result = await api.listCredentials({ maxResults: 1000 });
      setCredentials(result.Data || []);
    } catch (err) {
      setError('Failed to fetch credentials: ' + err.message);
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

  const fetchUsers = useCallback(async () => {
    try {
      const result = await api.listUsers({ maxResults: 1000 });
      setUsers(result.Data || []);
    } catch (err) {
      setError('Failed to fetch users: ' + err.message);
    }
  }, [api, setError]);

  useEffect(() => {
    fetchCredentials();
    fetchTenants();
    fetchUsers();
  }, [fetchCredentials, fetchTenants, fetchUsers]);

  const handleCreate = () => {
    setEditMode(false);
    setFormData({ TenantId: '', Name: '', UserId: '', Active: true, LabelsJson: '[]', TagsJson: '{}' });
    setShowForm(true);
  };

  const handleEdit = (credential) => {
    setEditMode(true);
    setSelectedCredential(credential);
    setFormData({
      TenantId: credential.TenantId || '',
      Name: credential.Name || '',
      UserId: credential.UserId || '',
      Active: credential.Active !== false,
      LabelsJson: JSON.stringify(credential.Labels || [], null, 2),
      TagsJson: JSON.stringify(credential.Tags || {}, null, 2)
    });
    setShowForm(true);
  };

  const handleViewMetadata = (credential) => {
    setSelectedCredential(credential);
    setShowMetadata(true);
  };

  const handleDeleteClick = (credential) => {
    setSelectedCredential(credential);
    setShowDeleteConfirm(true);
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteCredential(selectedCredential.Id, selectedCredential.TenantId);
      setShowDeleteConfirm(false);
      setSelectedCredential(null);
      fetchCredentials();
    } catch (err) {
      setError('Failed to delete credential: ' + err.message);
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
        TenantId: formData.TenantId || null,
        Name: formData.Name,
        UserId: formData.UserId || null,
        Active: formData.Active,
        Labels: labels,
        Tags: tags
      };

      if (editMode) {
        await api.updateCredential(selectedCredential.Id, data);
      } else {
        await api.createCredential(data);
      }
      setShowForm(false);
      fetchCredentials();
    } catch (err) {
      setError('Failed to save credential: ' + err.message);
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
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Name',
      label: 'Name'
    },
    {
      key: 'BearerToken',
      label: 'Bearer Token',
      width: '200px',
      render: (item) => (
        <div className="token-cell">
          <code>{item.BearerToken?.substring(0, 16)}...</code>
          <CopyButton value={item.BearerToken} title="Copy token" />
        </div>
      )
    },
    {
      key: 'Active',
      label: 'Status',
      width: '120px',
      render: (item) => <StatusIndicator active={item.Active} />,
      filterValue: (item) => item.Active ? 'active' : 'inactive'
    },
    {
      key: 'CreatedUtc',
      label: 'Created',
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
        <h1>Credentials</h1>
        <div className="view-actions">
          <button className="btn-icon" onClick={fetchCredentials} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
          <button className="btn-primary" onClick={handleCreate}>
            Create Credential
          </button>
        </div>
      </div>

      <DataTable data={credentials} columns={columns} loading={loading} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Credential' : 'Create Credential'}>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="tenantId" title="Organizational unit this credential belongs to">Tenant</label>
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
            <label htmlFor="name" title="Display name for this credential">Name</label>
            <input
              type="text"
              id="name"
              value={formData.Name}
              onChange={(e) => setFormData({ ...formData, Name: e.target.value })}
              required
            />
          </div>
          <div className="form-group">
            <label htmlFor="userId" title="Associate this credential with a user for tracking">User (optional)</label>
            <select
              id="userId"
              value={formData.UserId}
              onChange={(e) => setFormData({ ...formData, UserId: e.target.value })}
            >
              <option value="">-- No User --</option>
              {users.map((user) => (
                <option key={user.Id} value={user.Id}>
                  {user.Email} ({user.Id})
                </option>
              ))}
            </select>
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
            <label title="Inactive credentials cannot be used for authentication">
              <input
                type="checkbox"
                checked={formData.Active}
                onChange={(e) => setFormData({ ...formData, Active: e.target.checked })}
              />
              Active
            </label>
          </div>
          {!editMode && (
            <p className="form-hint">A bearer token will be automatically generated.</p>
          )}
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

      {showMetadata && selectedCredential && (
        <ViewMetadataModal
          data={selectedCredential}
          title="Credential Details"
          subtitle={selectedCredential.Id}
          onClose={() => setShowMetadata(false)}
        />
      )}

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedCredential?.Name}
        entityType="credential"
        loading={deleteLoading}
      />
    </div>
  );
}

export default Credentials;
