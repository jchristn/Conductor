import React, { useEffect, useMemo, useState } from 'react';
import Modal from './Modal';

function TrashIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true" focusable="false">
      <path fillRule="evenodd" d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2h1v10a2 2 0 002 2h6a2 2 0 002-2V6h1a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zm-1 6a1 1 0 012 0v6a1 1 0 11-2 0V8zm4-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z" clipRule="evenodd" />
    </svg>
  );
}

function formatBytes(value) {
  const numericValue = Number(value);
  if (value === null || value === undefined || Number.isNaN(numericValue)) return '-';
  if (numericValue === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const exponent = Math.min(Math.floor(Math.log(numericValue) / Math.log(1024)), units.length - 1);
  const size = numericValue / (1024 ** exponent);
  return `${size.toFixed(size >= 10 || exponent === 0 ? 0 : 1)} ${units[exponent]}`;
}

function formatDateTime(value) {
  if (!value) return '-';
  const timestamp = new Date(value);
  if (Number.isNaN(timestamp.getTime())) return '-';
  return timestamp.toLocaleString();
}

function getModelName(model) {
  return model?.Name || model?.name || model?.Model || model?.model || '';
}

function getModelSize(model) {
  return model?.Size ?? model?.size ?? null;
}

function getModelModifiedAt(model) {
  return model?.ModifiedAt || model?.modified_at || model?.modifiedAt || null;
}

function getModelDigest(model) {
  return model?.Digest || model?.digest || '';
}

function getModelDetails(model) {
  return model?.Details || model?.details || {};
}

function getDetailValue(details, pascalName, snakeName) {
  return details?.[pascalName] || details?.[snakeName] || '-';
}

function getShortDigest(digest) {
  if (!digest) return '-';
  const hash = digest.includes(':') ? digest.split(':').pop() : digest;
  return hash.length > 12 ? hash.slice(0, 12) : hash;
}

function normalizeModels(response) {
  if (Array.isArray(response)) return response;
  if (Array.isArray(response?.Models)) return response.Models;
  if (Array.isArray(response?.models)) return response.models;
  if (Array.isArray(response?.Data)) return response.Data;
  return [];
}

function OllamaModelManagerModal({ isOpen, onClose, endpoint, api, onChanged }) {
  const [models, setModels] = useState([]);
  const [loading, setLoading] = useState(false);
  const [operationLoading, setOperationLoading] = useState('');
  const [pullModel, setPullModel] = useState('');
  const [pullTimeoutMs, setPullTimeoutMs] = useState(1800000);
  const [pullInsecure, setPullInsecure] = useState(false);
  const [deleteCandidate, setDeleteCandidate] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const endpointId = endpoint?.Id || '';
  const isPulling = operationLoading === 'pull';
  const sortedModels = useMemo(() => (
    [...models].sort((left, right) => getModelName(left).localeCompare(getModelName(right)))
  ), [models]);

  const fetchModels = async () => {
    if (!endpoint) return;

    setLoading(true);
    setError('');
    try {
      const response = await api.listOllamaEndpointModels(endpoint.Id, endpoint.TenantId);
      setModels(normalizeModels(response));
      if (response?.Success === false) {
        setError(response.ErrorMessage || response.Message || 'Ollama model list failed.');
      }
    } catch (err) {
      setModels([]);
      setError(err.message || 'Failed to list Ollama models.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen && endpoint) {
      setPullModel('');
      setPullTimeoutMs(1800000);
      setPullInsecure(false);
      setDeleteCandidate('');
      setMessage('');
      setError('');
      fetchModels();
    }
  }, [isOpen, endpointId]);

  const handlePull = async (event) => {
    event.preventDefault();
    const model = pullModel.trim();
    if (!model) {
      setError('Enter a model name to pull.');
      return;
    }

    setOperationLoading('pull');
    setError('');
    setMessage('');
    try {
      const response = await api.pullOllamaEndpointModel(endpoint.Id, {
        Model: model,
        TimeoutMs: Number(pullTimeoutMs) || 1800000,
        Insecure: pullInsecure
      }, endpoint.TenantId);

      if (response?.Success === false) {
        setError(response.ErrorMessage || response.Message || 'Ollama pull failed.');
        return;
      }

      setMessage(response?.Message || `Pulled ${model}.`);
      await fetchModels();
      setPullModel('');
      if (onChanged) onChanged(response, endpoint);
    } catch (err) {
      setError(err.message || 'Ollama pull failed.');
    } finally {
      setOperationLoading('');
    }
  };

  const handleDelete = async (modelName) => {
    if (!modelName) return;

    setOperationLoading(`delete:${modelName}`);
    setError('');
    setMessage('');
    try {
      const response = await api.deleteOllamaEndpointModel(endpoint.Id, {
        Model: modelName
      }, endpoint.TenantId);

      if (response?.Success === false) {
        setError(response.ErrorMessage || response.Message || 'Ollama delete failed.');
        return;
      }

      setMessage(response?.Message || `Deleted ${modelName}.`);
      setDeleteCandidate('');
      await fetchModels();
      if (onChanged) onChanged(response, endpoint);
    } catch (err) {
      setError(err.message || 'Ollama delete failed.');
    } finally {
      setOperationLoading('');
    }
  };

  if (!endpoint) {
    return null;
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Manage Ollama Models" extraWide className="modal-ollama-models">
      <div className="ollama-model-manager">
        <div className="ollama-model-manager-header">
          <div>
            <span className="load-model-label">Endpoint</span>
            <strong>{endpoint.Name || endpoint.Id}</strong>
          </div>
          <button type="button" className="btn-secondary btn-small" onClick={fetchModels} disabled={loading || Boolean(operationLoading)}>
            {loading ? 'Refreshing...' : 'Refresh'}
          </button>
        </div>

        <form className="ollama-model-pull-form" onSubmit={handlePull} noValidate>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="ollamaPullModel">Pull Model</label>
              <input
                id="ollamaPullModel"
                value={pullModel}
                onChange={(event) => setPullModel(event.target.value)}
                placeholder="gemma3:4b"
                disabled={Boolean(operationLoading)}
              />
            </div>
            <div className="form-group">
              <label htmlFor="ollamaPullTimeout">Timeout ms</label>
              <input
                id="ollamaPullTimeout"
                type="number"
                min="1000"
                max="7200000"
                value={pullTimeoutMs}
                onChange={(event) => setPullTimeoutMs(event.target.value)}
                disabled={Boolean(operationLoading)}
              />
            </div>
            <div className="form-group checkbox-group ollama-pull-insecure">
              <label>
                <input
                  type="checkbox"
                  checked={pullInsecure}
                  onChange={(event) => setPullInsecure(event.target.checked)}
                  disabled={Boolean(operationLoading)}
                />
                Insecure Registry
              </label>
            </div>
          </div>
          <div className="form-actions">
            <button type="submit" className="btn-primary" disabled={Boolean(operationLoading) || !pullModel.trim()}>
              {operationLoading === 'pull' ? 'Pulling...' : 'Pull Model'}
            </button>
          </div>
          {isPulling && (
            <div className="ollama-pull-status" role="status" aria-live="polite">
              <div className="ollama-pull-status-row">
                <span className="ollama-pull-status-indicator" aria-hidden="true"></span>
                <span>Pulling {pullModel.trim()} from Ollama</span>
              </div>
              <div className="ollama-pull-progress" aria-label="Pull in progress">
                <div className="ollama-pull-progress-bar"></div>
              </div>
            </div>
          )}
        </form>

        {error && <div className="validation-panel error"><strong>{error}</strong></div>}
        {message && <div className="validation-panel success"><strong>{message}</strong></div>}

        <div className="health-table-container ollama-model-table-container">
          <table className="health-table ollama-model-table">
            <thead>
              <tr>
                <th>Model</th>
                <th>Parameters</th>
                <th>Family</th>
                <th>Quantization</th>
                <th>Size</th>
                <th>Modified</th>
                <th>Digest</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading && sortedModels.length === 0 ? (
                <tr>
                  <td colSpan="8" className="no-items">Loading models...</td>
                </tr>
              ) : sortedModels.length === 0 ? (
                <tr>
                  <td colSpan="8" className="no-items">No local Ollama models were reported by this endpoint.</td>
                </tr>
              ) : sortedModels.map((model) => {
                const modelName = getModelName(model);
                const deleteLabel = modelName ? `Delete ${modelName}` : 'Delete model';
                const details = getModelDetails(model);
                const rowLoading = operationLoading === `delete:${modelName}`;
                const confirmingDelete = deleteCandidate === modelName;

                return (
                  <tr key={modelName || getModelDigest(model)}>
                    <td className="ollama-model-name-cell" title={modelName}>{modelName || '-'}</td>
                    <td>{getDetailValue(details, 'ParameterSize', 'parameter_size')}</td>
                    <td>{getDetailValue(details, 'Family', 'family')}</td>
                    <td>{getDetailValue(details, 'QuantizationLevel', 'quantization_level')}</td>
                    <td>{formatBytes(getModelSize(model))}</td>
                    <td>{formatDateTime(getModelModifiedAt(model))}</td>
                    <td title={getModelDigest(model)}>{getShortDigest(getModelDigest(model))}</td>
                    <td>
                      {confirmingDelete ? (
                        <div className="ollama-model-row-actions">
                          <button
                            type="button"
                            className="btn-danger btn-small"
                            onClick={() => handleDelete(modelName)}
                            disabled={Boolean(operationLoading)}
                          >
                            {rowLoading ? 'Deleting...' : 'Confirm'}
                          </button>
                          <button
                            type="button"
                            className="btn-secondary btn-small"
                            onClick={() => setDeleteCandidate('')}
                            disabled={Boolean(operationLoading)}
                          >
                            Cancel
                          </button>
                        </div>
                      ) : (
                        <button
                          type="button"
                          className="btn-icon ollama-model-delete-trigger"
                          onClick={() => setDeleteCandidate(modelName)}
                          disabled={Boolean(operationLoading) || !modelName}
                          title={deleteLabel}
                          aria-label={deleteLabel}
                        >
                          <TrashIcon />
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </Modal>
  );
}

export default OllamaModelManagerModal;
