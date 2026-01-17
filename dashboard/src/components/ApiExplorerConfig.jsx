import React from 'react';
import { getOperationsForApiType } from '../utils/requestTemplates';

/**
 * Configuration panel component for the API Explorer.
 * Allows users to select VMR, API type, operation, model, and streaming options.
 */
function ApiExplorerConfig({ explorer, vmrs, vmrDefinitions, vmrCredentials, loading, collapsed }) {
  const {
    selectedVmr,
    setSelectedVmr,
    apiType,
    setApiType,
    operationId,
    setOperationId,
    modelName,
    setModelName,
    streamEnabled,
    setStreamEnabled,
    selectedCredentialId,
    setSelectedCredentialId,
    operation
  } = explorer;

  const operations = getOperationsForApiType(apiType);
  const activeVmrs = vmrs.filter(v => v.Active);
  const inactiveVmrs = vmrs.filter(v => !v.Active);

  const handleVmrChange = (e) => {
    const vmrId = e.target.value;
    const vmr = vmrs.find(v => v.Id === vmrId) || null;
    setSelectedVmr(vmr);
  };

  const handleOperationChange = (e) => {
    setOperationId(e.target.value);
  };

  const handleApiTypeChange = (e) => {
    setApiType(e.target.value);
  };

  // Find selected credential name for display
  const selectedCredential = vmrCredentials.find(c => c.Id === selectedCredentialId);

  if (collapsed) {
    return (
      <div className="api-explorer-config collapsed">
        <div className="config-summary">
          {selectedVmr ? (
            <>
              <span className="config-item">
                <strong>VMR:</strong> {selectedVmr.Name}
              </span>
              <span className="config-item">
                <span className={`api-type-badge ${apiType.toLowerCase()}`}>{apiType}</span>
              </span>
              <span className="config-item">
                <strong>Operation:</strong> {operation?.label || operationId}
              </span>
              {modelName && (
                <span className="config-item">
                  <strong>Model:</strong> {modelName}
                </span>
              )}
              {operation?.supportsStreaming && (
                <span className="config-item">
                  {streamEnabled ? 'Streaming' : 'Non-streaming'}
                </span>
              )}
              {selectedCredential && (
                <span className="config-item">
                  <strong>Credential:</strong> {selectedCredential.Name}
                </span>
              )}
            </>
          ) : (
            <span className="config-item muted">No VMR selected</span>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className="api-explorer-config">
      <div className="config-row">
        <div className="config-group vmr-select">
          <label htmlFor="vmr-selector" title="Select a Virtual Model Runner to test">
            Virtual Model Runner
          </label>
          <select
            id="vmr-selector"
            value={selectedVmr?.Id || ''}
            onChange={handleVmrChange}
            disabled={loading}
          >
            <option value="">-- Select a VMR --</option>
            {activeVmrs.length > 0 && (
              <optgroup label="Active">
                {activeVmrs.map(vmr => (
                  <option key={vmr.Id} value={vmr.Id}>
                    {vmr.Name} ({vmr.ApiType})
                  </option>
                ))}
              </optgroup>
            )}
            {inactiveVmrs.length > 0 && (
              <optgroup label="Inactive">
                {inactiveVmrs.map(vmr => (
                  <option key={vmr.Id} value={vmr.Id}>
                    {vmr.Name} ({vmr.ApiType})
                  </option>
                ))}
              </optgroup>
            )}
          </select>
        </div>

        <div className="config-group api-type">
          <label htmlFor="api-type" title="API format (auto-set from VMR)">
            API Type
          </label>
          <select
            id="api-type"
            value={apiType}
            onChange={handleApiTypeChange}
          >
            <option value="OpenAI">OpenAI</option>
            <option value="Ollama">Ollama</option>
          </select>
        </div>

        <div className="config-group operation">
          <label htmlFor="operation-type" title="API operation to perform">
            Operation
          </label>
          <select
            id="operation-type"
            value={operationId}
            onChange={handleOperationChange}
          >
            {operations.map(op => (
              <option key={op.id} value={op.id}>
                {op.label} ({op.method})
              </option>
            ))}
          </select>
        </div>

        <div className="config-group model">
          <label htmlFor="model-name" title="Model name for the request">
            Model
          </label>
          <input
            type="text"
            id="model-name"
            value={modelName}
            onChange={(e) => setModelName(e.target.value)}
            placeholder="Enter model name"
            list="model-suggestions"
          />
          {vmrDefinitions.length > 0 && (
            <datalist id="model-suggestions">
              {vmrDefinitions.map(def => (
                <option key={def.Id} value={def.Name}>
                  {def.Name} {def.Family && `(${def.Family})`}
                </option>
              ))}
            </datalist>
          )}
        </div>

        <div className="config-group streaming">
          <label title="Enable streaming responses">
            Streaming
          </label>
          <label className="toggle-switch">
            <input
              type="checkbox"
              checked={streamEnabled}
              onChange={(e) => setStreamEnabled(e.target.checked)}
              disabled={!operation?.supportsStreaming}
            />
            <span className="toggle-slider"></span>
          </label>
          {!operation?.supportsStreaming && (
            <span className="config-hint">Not supported</span>
          )}
        </div>
      </div>

      <div className="config-row secondary">
        <div className="config-group credential-select">
          <label htmlFor="credential-selector" title="Select a credential for authentication">
            Credential
          </label>
          <select
            id="credential-selector"
            value={selectedCredentialId}
            onChange={(e) => setSelectedCredentialId(e.target.value)}
            disabled={!selectedVmr}
          >
            <option value="">-- No Credential --</option>
            {vmrCredentials.map(cred => (
              <option key={cred.Id} value={cred.Id}>
                {cred.Name}
              </option>
            ))}
          </select>
          {selectedVmr && vmrCredentials.length === 0 && (
            <span className="config-hint">No credentials available</span>
          )}
        </div>
      </div>
    </div>
  );
}

export default ApiExplorerConfig;
