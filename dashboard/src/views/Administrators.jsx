import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';

function Administrators() {
  const { api, setError } = useApp();
  const [administrators, setAdministrators] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedAdmin, setSelectedAdmin] = useState(null);
  const [showDetails, setShowDetails] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [formData, setFormData] = useState({
    FirstName: '',
    LastName: '',
    Email: '',
    Password: '',
    Active: true
  });

  const fetchAdministrators = useCallback(async () => {
    try {
      setLoading(true);
      const result = await api.listAdministrators({ maxResults: 1000 });
      setAdministrators(result.Data || []);
    } catch (err) {
      setError('Failed to fetch administrators: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError]);

  useEffect(() => {
    fetchAdministrators();
  }, [fetchAdministrators]);

  const handleCreate = () => {
    setEditMode(false);
    setFormData({ FirstName: '', LastName: '', Email: '', Password: '', Active: true });
    setShowForm(true);
  };

  const handleEdit = (admin) => {
    setEditMode(true);
    setSelectedAdmin(admin);
    setFormData({
      FirstName: admin.FirstName || '',
      LastName: admin.LastName || '',
      Email: admin.Email || '',
      Password: '',
      Active: admin.Active !== false
    });
    setShowForm(true);
  };

  const handleViewDetails = (admin) => {
    setSelectedAdmin(admin);
    setShowDetails(true);
  };

  const handleDeleteClick = (admin) => {
    setSelectedAdmin(admin);
    setShowDeleteConfirm(true);
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteAdministrator(selectedAdmin.Id);
      setShowDeleteConfirm(false);
      setSelectedAdmin(null);
      fetchAdministrators();
    } catch (err) {
      setError('Failed to delete administrator: ' + err.message);
    } finally {
      setDeleteLoading(false);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      const data = { ...formData };
      if (editMode && !data.Password) {
        delete data.Password;
      }
      if (editMode) {
        await api.updateAdministrator(selectedAdmin.Id, data);
      } else {
        await api.createAdministrator(data);
      }
      setShowForm(false);
      fetchAdministrators();
    } catch (err) {
      setError('Failed to save administrator: ' + err.message);
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
      key: 'Email',
      label: 'Email'
    },
    {
      key: 'Name',
      label: 'Name',
      render: (item) => [item.FirstName, item.LastName].filter(Boolean).join(' ') || '-',
      filterValue: (item) => [item.FirstName, item.LastName].filter(Boolean).join(' ')
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
            { label: 'View Details', onClick: () => handleViewDetails(item) },
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
        <h1>Administrators</h1>
        <div className="view-actions">
          <button className="btn-icon" onClick={fetchAdministrators} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
          <button className="btn-primary" onClick={handleCreate}>
            Create Administrator
          </button>
        </div>
      </div>

      <DataTable data={administrators} columns={columns} loading={loading} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Administrator' : 'Create Administrator'}>
        <form onSubmit={handleSubmit}>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="firstName" title="Administrator's first name">First Name</label>
              <input
                type="text"
                id="firstName"
                value={formData.FirstName}
                onChange={(e) => setFormData({ ...formData, FirstName: e.target.value })}
              />
            </div>
            <div className="form-group">
              <label htmlFor="lastName" title="Administrator's last name">Last Name</label>
              <input
                type="text"
                id="lastName"
                value={formData.LastName}
                onChange={(e) => setFormData({ ...formData, LastName: e.target.value })}
              />
            </div>
          </div>
          <div className="form-group">
            <label htmlFor="email" title="Email address used for dashboard login">Email</label>
            <input
              type="email"
              id="email"
              value={formData.Email}
              onChange={(e) => setFormData({ ...formData, Email: e.target.value })}
              required
            />
          </div>
          <div className="form-group">
            <label htmlFor="password" title={editMode ? "Leave blank to keep current password" : "Password for dashboard authentication"}>Password {editMode && '(leave blank to keep current)'}</label>
            <input
              type="password"
              id="password"
              value={formData.Password}
              onChange={(e) => setFormData({ ...formData, Password: e.target.value })}
              required={!editMode}
            />
          </div>
          <div className="form-group checkbox-group">
            <label title="Inactive administrators cannot log into the dashboard">
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

      {showDetails && selectedAdmin && (
        <ViewMetadataModal
          data={selectedAdmin}
          title="Administrator Details"
          subtitle={selectedAdmin.Id}
          onClose={() => setShowDetails(false)}
        />
      )}

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedAdmin?.Email}
        entityType="administrator"
        loading={deleteLoading}
      />
    </div>
  );
}

export default Administrators;
