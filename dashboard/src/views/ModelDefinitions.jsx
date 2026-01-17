import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';

function ModelDefinitions() {
  const { api, setError } = useApp();
  const [definitions, setDefinitions] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedDefinition, setSelectedDefinition] = useState(null);
  const [showMetadata, setShowMetadata] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [formData, setFormData] = useState({
    TenantId: '',
    Name: '',
    SourceUrl: '',
    Family: '',
    ParameterSize: '',
    QuantizationLevel: '',
    SupportsEmbeddings: false,
    SupportsCompletions: true,
    Active: true,
    LabelsJson: '[]',
    TagsJson: '{}'
  });

  const fetchDefinitions = useCallback(async () => {
    try {
      setLoading(true);
      const result = await api.listModelDefinitions({ maxResults: 1000 });
      setDefinitions(result.Data || []);
    } catch (err) {
      setError('Failed to fetch model definitions: ' + err.message);
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
    fetchDefinitions();
    fetchTenants();
  }, [fetchDefinitions, fetchTenants]);

  const handleCreate = () => {
    setEditMode(false);
    setFormData({
      TenantId: '',
      Name: '',
      SourceUrl: '',
      Family: '',
      ParameterSize: '',
      QuantizationLevel: '',
      SupportsEmbeddings: false,
      SupportsCompletions: true,
      Active: true,
      LabelsJson: '[]',
      TagsJson: '{}'
    });
    setShowForm(true);
  };

  const handleEdit = (definition) => {
    setEditMode(true);
    setSelectedDefinition(definition);
    setFormData({
      TenantId: definition.TenantId || '',
      Name: definition.Name || '',
      SourceUrl: definition.SourceUrl || '',
      Family: definition.Family || '',
      ParameterSize: definition.ParameterSize || '',
      QuantizationLevel: definition.QuantizationLevel || '',
      SupportsEmbeddings: definition.SupportsEmbeddings === true,
      SupportsCompletions: definition.SupportsCompletions !== false,
      Active: definition.Active !== false,
      LabelsJson: JSON.stringify(definition.Labels || [], null, 2),
      TagsJson: JSON.stringify(definition.Tags || {}, null, 2)
    });
    setShowForm(true);
  };

  const handleViewMetadata = (definition) => {
    setSelectedDefinition(definition);
    setShowMetadata(true);
  };

  const handleDeleteClick = (definition) => {
    setSelectedDefinition(definition);
    setShowDeleteConfirm(true);
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteModelDefinition(selectedDefinition.Id, selectedDefinition.TenantId);
      setShowDeleteConfirm(false);
      setSelectedDefinition(null);
      fetchDefinitions();
    } catch (err) {
      setError('Failed to delete model definition: ' + err.message);
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
        SourceUrl: formData.SourceUrl,
        Family: formData.Family,
        ParameterSize: formData.ParameterSize,
        QuantizationLevel: formData.QuantizationLevel,
        SupportsEmbeddings: formData.SupportsEmbeddings,
        SupportsCompletions: formData.SupportsCompletions,
        Active: formData.Active,
        Labels: labels,
        Tags: tags
      };

      if (editMode) {
        await api.updateModelDefinition(selectedDefinition.Id, data);
      } else {
        await api.createModelDefinition(data);
      }
      setShowForm(false);
      fetchDefinitions();
    } catch (err) {
      setError('Failed to save model definition: ' + err.message);
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
      key: 'Family',
      label: 'Family',
      width: '120px'
    },
    {
      key: 'ParameterSize',
      label: 'Size',
      width: '100px'
    },
    {
      key: 'QuantizationLevel',
      label: 'Quantization',
      width: '100px'
    },
    {
      key: 'ModelType',
      label: 'Type',
      width: '120px',
      render: (item) => {
        const types = [];
        if (item.SupportsCompletions) types.push('Completions');
        if (item.SupportsEmbeddings) types.push('Embeddings');
        return types.length > 0 ? types.join(', ') : 'None';
      },
      filterValue: (item) => {
        const types = [];
        if (item.SupportsCompletions) types.push('completions');
        if (item.SupportsEmbeddings) types.push('embeddings');
        return types.join(' ');
      }
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
        <h1>Model Definitions</h1>
        <div className="view-actions">
          <button className="btn-icon" onClick={fetchDefinitions} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
          <button className="btn-primary" onClick={handleCreate}>
            Create Definition
          </button>
        </div>
      </div>

      <DataTable data={definitions} columns={columns} loading={loading} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Model Definition' : 'Create Model Definition'} wide>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="tenantId" title="Organizational unit that owns this model definition">Tenant</label>
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
            <label htmlFor="name" title="Model identifier as used in API requests">Name</label>
            <input
              type="text"
              id="name"
              value={formData.Name}
              onChange={(e) => setFormData({ ...formData, Name: e.target.value })}
              placeholder="e.g., llama3.2:3b-instruct-q4_0"
              required
            />
          </div>
          <div className="form-group">
            <label htmlFor="sourceUrl" title="Link to model documentation or download page">Source URL (optional)</label>
            <input
              type="url"
              id="sourceUrl"
              value={formData.SourceUrl}
              onChange={(e) => setFormData({ ...formData, SourceUrl: e.target.value })}
              placeholder="https://ollama.com/library/llama3.2"
            />
          </div>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="family" title="Model family name (e.g., llama, mistral, phi)">Family</label>
              <input
                type="text"
                id="family"
                value={formData.Family}
                onChange={(e) => setFormData({ ...formData, Family: e.target.value })}
                placeholder="e.g., llama"
              />
            </div>
            <div className="form-group">
              <label htmlFor="parameterSize" title="Number of model parameters (e.g., 3B, 7B, 70B)">Parameter Size</label>
              <input
                type="text"
                id="parameterSize"
                value={formData.ParameterSize}
                onChange={(e) => setFormData({ ...formData, ParameterSize: e.target.value })}
                placeholder="e.g., 3B, 7B, 70B"
              />
            </div>
            <div className="form-group">
              <label htmlFor="quantizationLevel" title="Quantization format (e.g., Q4_0, Q8_0, FP16)">Quantization</label>
              <input
                type="text"
                id="quantizationLevel"
                value={formData.QuantizationLevel}
                onChange={(e) => setFormData({ ...formData, QuantizationLevel: e.target.value })}
                placeholder="e.g., Q4_0, Q8_0, FP16"
              />
            </div>
          </div>
          <div className="form-group checkbox-group">
            <label title="Model can generate text completions (chat and text generation)">
              <input
                type="checkbox"
                checked={formData.SupportsCompletions}
                onChange={(e) => setFormData({ ...formData, SupportsCompletions: e.target.checked })}
              />
              Supports Completions
            </label>
          </div>
          <div className="form-group checkbox-group">
            <label title="Model can generate vector embeddings for semantic search">
              <input
                type="checkbox"
                checked={formData.SupportsEmbeddings}
                onChange={(e) => setFormData({ ...formData, SupportsEmbeddings: e.target.checked })}
              />
              Supports Embeddings
            </label>
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
            <label title="Inactive definitions are excluded from VMR model lists">
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

      {showMetadata && selectedDefinition && (
        <ViewMetadataModal
          data={selectedDefinition}
          title="Model Definition Details"
          subtitle={selectedDefinition.Id}
          onClose={() => setShowMetadata(false)}
        />
      )}

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedDefinition?.Name}
        entityType="model definition"
        loading={deleteLoading}
      />
    </div>
  );
}

export default ModelDefinitions;
