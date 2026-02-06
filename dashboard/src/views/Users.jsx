import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';

function Users() {
  const { api, setError } = useApp();
  const [users, setUsers] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedUser, setSelectedUser] = useState(null);
  const [showMetadata, setShowMetadata] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [formData, setFormData] = useState({
    TenantId: '',
    FirstName: '',
    LastName: '',
    Email: '',
    Password: '',
    Active: true,
    IsAdmin: false,
    IsTenantAdmin: false,
    LabelsJson: '[]',
    TagsJson: '{}'
  });

  const fetchUsers = useCallback(async () => {
    try {
      setLoading(true);
      const result = await api.listUsers({ maxResults: 1000 });
      setUsers(result.Data || []);
    } catch (err) {
      setError('Failed to fetch users: ' + err.message);
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

  useEffect(() => {
    fetchUsers();
    fetchTenants();
  }, [fetchUsers, fetchTenants]);

  const handleCreate = () => {
    setEditMode(false);
    setFormData({ TenantId: '', FirstName: '', LastName: '', Email: '', Password: '', Active: true, IsAdmin: false, IsTenantAdmin: false, LabelsJson: '[]', TagsJson: '{}' });
    setShowForm(true);
  };

  const handleEdit = (user) => {
    setEditMode(true);
    setSelectedUser(user);
    setFormData({
      TenantId: user.TenantId || '',
      FirstName: user.FirstName || '',
      LastName: user.LastName || '',
      Email: user.Email || '',
      Password: '',
      Active: user.Active !== false,
      IsAdmin: user.IsAdmin || false,
      IsTenantAdmin: user.IsTenantAdmin || false,
      LabelsJson: JSON.stringify(user.Labels || [], null, 2),
      TagsJson: JSON.stringify(user.Tags || {}, null, 2)
    });
    setShowForm(true);
  };

  const handleViewMetadata = (user) => {
    setSelectedUser(user);
    setShowMetadata(true);
  };

  const handleDeleteClick = (user) => {
    setSelectedUser(user);
    setShowDeleteConfirm(true);
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteUser(selectedUser.Id, selectedUser.TenantId);
      setShowDeleteConfirm(false);
      setSelectedUser(null);
      fetchUsers();
    } catch (err) {
      setError('Failed to delete user: ' + err.message);
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
        FirstName: formData.FirstName,
        LastName: formData.LastName,
        Email: formData.Email,
        Password: formData.Password,
        Active: formData.Active,
        IsAdmin: formData.IsAdmin,
        IsTenantAdmin: formData.IsTenantAdmin,
        Labels: labels,
        Tags: tags
      };

      if (editMode && !data.Password) {
        delete data.Password;
      }
      if (editMode) {
        await api.updateUser(selectedUser.Id, data);
      } else {
        await api.createUser(data);
      }
      setShowForm(false);
      fetchUsers();
    } catch (err) {
      setError('Failed to save user: ' + err.message);
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
      tooltip: 'Unique identifier for this user',
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Email',
      label: 'Email',
      tooltip: 'Email address used for authentication'
    },
    {
      key: 'Name',
      label: 'Name',
      tooltip: 'Full name of the user',
      render: (item) => [item.FirstName, item.LastName].filter(Boolean).join(' ') || '-',
      filterValue: (item) => [item.FirstName, item.LastName].filter(Boolean).join(' ')
    },
    {
      key: 'Role',
      label: 'Role',
      tooltip: 'User permission level (Admin, Tenant Admin, or User)',
      width: '140px',
      render: (item) => {
        if (item.IsAdmin) return <span className="role-badge role-admin">Admin</span>;
        if (item.IsTenantAdmin) return <span className="role-badge role-tenant-admin">Tenant Admin</span>;
        return <span className="role-badge role-user">User</span>;
      },
      filterValue: (item) => item.IsAdmin ? 'admin' : (item.IsTenantAdmin ? 'tenant admin' : 'user')
    },
    {
      key: 'Active',
      label: 'Status',
      tooltip: 'Whether this user can authenticate',
      width: '100px',
      render: (item) => <StatusIndicator active={item.Active} />,
      filterValue: (item) => item.Active ? 'active' : 'inactive'
    },
    {
      key: 'CreatedUtc',
      label: 'Created',
      tooltip: 'When this user account was created',
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
        <h1>Users</h1>
        <div className="view-actions">
          <button className="btn-icon" onClick={fetchUsers} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
          <button className="btn-primary" onClick={handleCreate}>
            Create User
          </button>
        </div>
      </div>

      <DataTable data={users} columns={columns} loading={loading} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit User' : 'Create User'}>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="tenantId" title="Organizational unit this user belongs to">Tenant</label>
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
              <label htmlFor="firstName" title="User's first name">First Name</label>
              <input
                type="text"
                id="firstName"
                value={formData.FirstName}
                onChange={(e) => setFormData({ ...formData, FirstName: e.target.value })}
              />
            </div>
            <div className="form-group">
              <label htmlFor="lastName" title="User's last name">Last Name</label>
              <input
                type="text"
                id="lastName"
                value={formData.LastName}
                onChange={(e) => setFormData({ ...formData, LastName: e.target.value })}
              />
            </div>
          </div>
          <div className="form-group">
            <label htmlFor="email" title="Email address used for login">Email</label>
            <input
              type="email"
              id="email"
              value={formData.Email}
              onChange={(e) => setFormData({ ...formData, Email: e.target.value })}
              required
            />
          </div>
          <div className="form-group">
            <label htmlFor="password" title={editMode ? "Leave blank to keep current password" : "Password for authentication"}>Password {editMode && '(leave blank to keep current)'}</label>
            <input
              type="password"
              id="password"
              value={formData.Password}
              onChange={(e) => setFormData({ ...formData, Password: e.target.value })}
              required={!editMode}
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
            <label title="Inactive users cannot authenticate">
              <input
                type="checkbox"
                checked={formData.Active}
                onChange={(e) => setFormData({ ...formData, Active: e.target.checked })}
              />
              Active
            </label>
          </div>
          <div className="form-group checkbox-group">
            <label title="Full cross-tenant access to all resources">
              <input
                type="checkbox"
                checked={formData.IsAdmin}
                onChange={(e) => setFormData({ ...formData, IsAdmin: e.target.checked })}
              />
              Global Admin
            </label>
          </div>
          <div className="form-group checkbox-group">
            <label title="Manage users and credentials within own tenant">
              <input
                type="checkbox"
                checked={formData.IsTenantAdmin}
                onChange={(e) => setFormData({ ...formData, IsTenantAdmin: e.target.checked })}
              />
              Tenant Admin
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

      {showMetadata && selectedUser && (
        <ViewMetadataModal
          data={selectedUser}
          title="User Details"
          subtitle={selectedUser.Id}
          onClose={() => setShowMetadata(false)}
        />
      )}

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedUser?.Email}
        entityType="user"
        loading={deleteLoading}
      />
    </div>
  );
}

export default Users;
