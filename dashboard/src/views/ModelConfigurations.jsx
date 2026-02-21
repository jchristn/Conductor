import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { useOnboarding } from '../context/OnboardingContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import ViewMetadataModal from '../components/ViewMetadataModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';

function ModelConfigurations() {
  const { api, setError } = useApp();
  const { pendingCreate, clearPendingCreate, onEntityCreated } = useOnboarding();
  const [configurations, setConfigurations] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedConfiguration, setSelectedConfiguration] = useState(null);
  const [showMetadata, setShowMetadata] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [formData, setFormData] = useState({
    TenantId: '',
    Name: '',
    Model: '',
    ContextWindowSize: 4096,
    Temperature: 0.7,
    TopP: 0.9,
    TopK: 40,
    RepeatPenalty: 1.1,
    MaxTokens: 2048,
    PinnedEmbeddingsPropertiesJson: '{}',
    PinnedCompletionsPropertiesJson: '{}',
    Active: true,
    LabelsJson: '[]',
    TagsJson: '{}'
  });

  const fetchConfigurations = useCallback(async () => {
    try {
      setLoading(true);
      const result = await api.listModelConfigurations({ maxResults: 1000 });
      setConfigurations(result.Data || []);
    } catch (err) {
      setError('Failed to fetch model configurations: ' + err.message);
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
    fetchConfigurations();
    fetchTenants();
  }, [fetchConfigurations, fetchTenants]);

  useEffect(() => {
    if (pendingCreate === 'configuration') {
      clearPendingCreate();
      handleCreate();
    }
  }, [pendingCreate]);

  const handleCreate = () => {
    setEditMode(false);
    setFormData({
      TenantId: '',
      Name: '',
      Model: '',
      ContextWindowSize: 4096,
      Temperature: 0.7,
      TopP: 0.9,
      TopK: 40,
      RepeatPenalty: 1.1,
      MaxTokens: 2048,
      PinnedEmbeddingsPropertiesJson: '{}',
      PinnedCompletionsPropertiesJson: '{}',
      Active: true,
      LabelsJson: '[]',
      TagsJson: '{}'
    });
    setShowForm(true);
  };

  const handleEdit = (configuration) => {
    setEditMode(true);
    setSelectedConfiguration(configuration);
    setFormData({
      TenantId: configuration.TenantId || '',
      Name: configuration.Name || '',
      Model: configuration.Model || '',
      ContextWindowSize: configuration.ContextWindowSize || 4096,
      Temperature: configuration.Temperature || 0.7,
      TopP: configuration.TopP || 0.9,
      TopK: configuration.TopK || 40,
      RepeatPenalty: configuration.RepeatPenalty || 1.1,
      MaxTokens: configuration.MaxTokens || 2048,
      PinnedEmbeddingsPropertiesJson: JSON.stringify(configuration.PinnedEmbeddingsProperties || {}, null, 2),
      PinnedCompletionsPropertiesJson: JSON.stringify(configuration.PinnedCompletionsProperties || {}, null, 2),
      Active: configuration.Active !== false,
      LabelsJson: JSON.stringify(configuration.Labels || [], null, 2),
      TagsJson: JSON.stringify(configuration.Tags || {}, null, 2)
    });
    setShowForm(true);
  };

  const handleViewMetadata = (configuration) => {
    setSelectedConfiguration(configuration);
    setShowMetadata(true);
  };

  const handleDeleteClick = (configuration) => {
    setSelectedConfiguration(configuration);
    setShowDeleteConfirm(true);
  };

  const handleDelete = async () => {
    try {
      setDeleteLoading(true);
      await api.deleteModelConfiguration(selectedConfiguration.Id, selectedConfiguration.TenantId);
      setShowDeleteConfirm(false);
      setSelectedConfiguration(null);
      fetchConfigurations();
    } catch (err) {
      setError('Failed to delete model configuration: ' + err.message);
    } finally {
      setDeleteLoading(false);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      let pinnedEmbeddings = {};
      let pinnedCompletions = {};
      let labels = [];
      let tags = {};

      try {
        pinnedEmbeddings = JSON.parse(formData.PinnedEmbeddingsPropertiesJson || '{}');
      } catch (err) {
        setError('Invalid JSON in Pinned Embeddings Properties');
        return;
      }

      try {
        pinnedCompletions = JSON.parse(formData.PinnedCompletionsPropertiesJson || '{}');
      } catch (err) {
        setError('Invalid JSON in Pinned Completions Properties');
        return;
      }

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
        Model: formData.Model || null,
        ContextWindowSize: parseInt(formData.ContextWindowSize),
        Temperature: parseFloat(formData.Temperature),
        TopP: parseFloat(formData.TopP),
        TopK: parseInt(formData.TopK),
        RepeatPenalty: parseFloat(formData.RepeatPenalty),
        MaxTokens: parseInt(formData.MaxTokens),
        PinnedEmbeddingsProperties: pinnedEmbeddings,
        PinnedCompletionsProperties: pinnedCompletions,
        Active: formData.Active,
        Labels: labels,
        Tags: tags
      };

      if (editMode) {
        await api.updateModelConfiguration(selectedConfiguration.Id, data);
      } else {
        await api.createModelConfiguration(data);
        onEntityCreated('configuration');
      }
      setShowForm(false);
      fetchConfigurations();
    } catch (err) {
      setError('Failed to save model configuration: ' + err.message);
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
      tooltip: 'Unique identifier for this configuration',
      width: '280px',
      render: (item) => <CopyableId value={item.Id} />
    },
    {
      key: 'Name',
      label: 'Name',
      tooltip: 'Display name for this configuration'
    },
    {
      key: 'Model',
      label: 'Model',
      tooltip: 'Specific model this configuration applies to (blank = all models)',
      render: (item) => item.Model || '-'
    },
    {
      key: 'Temperature',
      label: 'Temp',
      tooltip: 'Controls randomness in output (0 = deterministic, 2 = highly creative)',
      width: '80px'
    },
    {
      key: 'MaxTokens',
      label: 'Max Tokens',
      tooltip: 'Maximum tokens in generated response',
      width: '100px'
    },
    {
      key: 'ContextWindowSize',
      label: 'Context',
      tooltip: 'Maximum tokens in conversation context window',
      width: '100px'
    },
    {
      key: 'Active',
      label: 'Status',
      tooltip: 'Whether this configuration is applied to requests',
      width: '120px',
      render: (item) => <StatusIndicator active={item.Active} />,
      filterValue: (item) => item.Active ? 'active' : 'inactive'
    },
    {
      key: 'CreatedUtc',
      label: 'Created',
      tooltip: 'When this configuration was created',
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
        <h1>Model Configurations</h1>
        <div className="view-actions">
          <button className="btn-icon" onClick={fetchConfigurations} title="Refresh">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
          <button className="btn-primary" onClick={handleCreate}>
            Create Configuration
          </button>
        </div>
      </div>

      <DataTable data={configurations} columns={columns} loading={loading} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Model Configuration' : 'Create Model Configuration'} wide>
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="tenantId" title="Organizational unit that owns this configuration">Tenant</label>
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
            <label htmlFor="name" title="Display name for this configuration">Name</label>
            <input
              type="text"
              id="name"
              value={formData.Name}
              onChange={(e) => setFormData({ ...formData, Name: e.target.value })}
              required
            />
          </div>
          <div className="form-group">
            <label htmlFor="model" title="When set, this configuration only applies to requests for this specific model">Model</label>
            <input
              type="text"
              id="model"
              value={formData.Model}
              onChange={(e) => setFormData({ ...formData, Model: e.target.value })}
              placeholder="e.g., llama3.2:latest (optional)"
            />
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="contextWindowSize" title="Maximum tokens in conversation context">Context Window Size</label>
              <input
                type="number"
                id="contextWindowSize"
                value={formData.ContextWindowSize}
                onChange={(e) => setFormData({ ...formData, ContextWindowSize: e.target.value })}
                min="1"
              />
            </div>
            <div className="form-group">
              <label htmlFor="maxTokens" title="Maximum tokens in generated response">Max Tokens</label>
              <input
                type="number"
                id="maxTokens"
                value={formData.MaxTokens}
                onChange={(e) => setFormData({ ...formData, MaxTokens: e.target.value })}
                min="1"
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="temperature" title="Controls randomness in output (0 = deterministic, 2 = highly creative)">Temperature</label>
              <input
                type="number"
                id="temperature"
                value={formData.Temperature}
                onChange={(e) => setFormData({ ...formData, Temperature: e.target.value })}
                min="0"
                max="2"
                step="0.1"
              />
            </div>
            <div className="form-group">
              <label htmlFor="topP" title="Nucleus sampling threshold - only consider tokens with cumulative probability up to this value">Top P</label>
              <input
                type="number"
                id="topP"
                value={formData.TopP}
                onChange={(e) => setFormData({ ...formData, TopP: e.target.value })}
                min="0"
                max="1"
                step="0.05"
              />
            </div>
            <div className="form-group">
              <label htmlFor="topK" title="Top-K sampling - only consider the K most likely tokens">Top K</label>
              <input
                type="number"
                id="topK"
                value={formData.TopK}
                onChange={(e) => setFormData({ ...formData, TopK: e.target.value })}
                min="1"
              />
            </div>
            <div className="form-group">
              <label htmlFor="repeatPenalty" title="Penalize tokens that have already appeared in the output">Repeat Penalty</label>
              <input
                type="number"
                id="repeatPenalty"
                value={formData.RepeatPenalty}
                onChange={(e) => setFormData({ ...formData, RepeatPenalty: e.target.value })}
                min="0"
                step="0.1"
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="pinnedEmbeddings" title="Force these properties on all embedding requests - overrides client-provided values">Pinned Embeddings Properties (JSON)</label>
            <textarea
              id="pinnedEmbeddings"
              value={formData.PinnedEmbeddingsPropertiesJson}
              onChange={(e) => setFormData({ ...formData, PinnedEmbeddingsPropertiesJson: e.target.value })}
              rows={4}
              className="code-input"
              placeholder="{}"
            />
          </div>

          <div className="form-group">
            <label htmlFor="pinnedCompletions" title="Force these properties on all completion requests - overrides client-provided values">Pinned Completions Properties (JSON)</label>
            <textarea
              id="pinnedCompletions"
              value={formData.PinnedCompletionsPropertiesJson}
              onChange={(e) => setFormData({ ...formData, PinnedCompletionsPropertiesJson: e.target.value })}
              rows={4}
              className="code-input"
              placeholder="{}"
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
            <label title="Inactive configurations are not applied to requests">
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

      {showMetadata && selectedConfiguration && (
        <ViewMetadataModal
          data={selectedConfiguration}
          title="Model Configuration Details"
          subtitle={selectedConfiguration.Id}
          onClose={() => setShowMetadata(false)}
        />
      )}

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDelete}
        entityName={selectedConfiguration?.Name}
        entityType="model configuration"
        loading={deleteLoading}
      />
    </div>
  );
}

export default ModelConfigurations;
