import React, { useEffect, useMemo, useState } from 'react';
import Modal from './Modal';

const PROBE_KINDS = ['Auto', 'MetadataOnly', 'ChatCompletion', 'Completion', 'Embeddings', 'NativeGenerate'];
const TARGET_MODES = ['SelectedEndpoint', 'AllEligibleEndpoints', 'AllConfiguredEndpoints', 'SpecificEndpointIds'];
const EMPTY_DEFINITIONS = [];
const EMPTY_ENDPOINTS = [];

function formatDurationMs(value) {
  if (value === null || value === undefined) return '-';
  if (value < 1000) return `${value}ms`;
  return `${(value / 1000).toFixed(value < 10000 ? 1 : 0)}s`;
}

function getOutcomeTone(outcome, success) {
  if (success === true) return 'success';
  if (outcome === 'Skipped' || outcome === 'DryRun') return 'neutral';
  if (outcome === 'TimedOut' || outcome === 'UnauthorizedUpstream' || outcome === 'Failed') return 'danger';
  return 'warning';
}

function getAttachedActiveDefinitions(target, definitions) {
  const ids = target?.ModelDefinitionIds || [];
  return (definitions || []).filter((definition) => ids.includes(definition.Id) && definition.Active !== false);
}

function getAttachedEndpoints(target, endpoints) {
  const ids = target?.ModelRunnerEndpointIds || [];
  return (endpoints || []).filter((endpoint) => ids.includes(endpoint.Id));
}

function getTargetModelCandidate(target) {
  if (!target) return '';

  const directValue = target.Model || target.ModelName || target.DefaultModel || target.DefaultModelName;
  if (directValue) return directValue;

  const tags = target.Tags || {};
  const tagValue = tags.Model || tags.model || tags.DefaultModel || tags.defaultModel || tags.default_model;
  if (tagValue) return tagValue;

  const metadata = target.Metadata || {};
  if (metadata && typeof metadata === 'object' && !Array.isArray(metadata)) {
    return metadata.Model || metadata.model || metadata.ModelName || metadata.modelName || metadata.DefaultModel || metadata.defaultModel || '';
  }

  return '';
}

function buildInitialForm(target, targetType, definitions) {
  const activeDefinitions = getAttachedActiveDefinitions(target, definitions);
  const singleDefinition = activeDefinitions.length === 1 ? activeDefinitions[0] : null;
  const endpointModel = targetType === 'endpoint' ? getTargetModelCandidate(target) : '';

  return {
    Model: targetType === 'vmr' && singleDefinition ? singleDefinition.Name : endpointModel,
    ModelDefinitionId: targetType === 'vmr' && singleDefinition ? singleDefinition.Id : '',
    ProbeKind: 'Auto',
    TargetMode: 'SelectedEndpoint',
    EndpointIds: [],
    InputText: 'conductor warmup',
    KeepAlive: '30m',
    TimeoutMs: 300000,
    MaxRetries: 0,
    VerifyLoaded: true,
    IncludeInactive: false,
    DryRun: false
  };
}

function getSubmitBlockReason(targetType, formData, activeDefinitions) {
  if (targetType === 'endpoint' && !formData.Model.trim()) {
    return 'Enter the exact model name to load. Placeholder text is only an example and is not submitted.';
  }

  if (targetType === 'vmr'
    && !formData.Model.trim()
    && !formData.ModelDefinitionId
    && activeDefinitions.length !== 1) {
    return 'Enter a model name or select one attached model definition.';
  }

  if (targetType === 'vmr'
    && formData.TargetMode === 'SpecificEndpointIds'
    && (!formData.EndpointIds || formData.EndpointIds.length === 0)) {
    return 'Select at least one endpoint, or choose a target mode that does not require endpoint selection.';
  }

  return '';
}

function LoadModelModal({
  isOpen,
  onClose,
  target,
  targetType,
  api,
  definitions = EMPTY_DEFINITIONS,
  endpoints = EMPTY_ENDPOINTS,
  onComplete
}) {
  const [formData, setFormData] = useState(() => buildInitialForm(target, targetType, definitions));
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');

  const activeDefinitions = useMemo(() => getAttachedActiveDefinitions(target, definitions), [target, definitions]);
  const attachedEndpoints = useMemo(() => getAttachedEndpoints(target, endpoints), [target, endpoints]);
  const apiType = target?.ApiType || '';
  const title = targetType === 'vmr' ? 'Load VMR Model' : 'Load Endpoint Model';
  const hostedPaidProbe = ['OpenAI', 'Gemini'].includes(apiType) && !['Auto', 'MetadataOnly'].includes(formData.ProbeKind);
  const submitBlockReason = getSubmitBlockReason(targetType, formData, activeDefinitions);

  const targetId = target?.Id || '';
  const definitionCount = definitions.length;

  useEffect(() => {
    if (isOpen) {
      setFormData(buildInitialForm(target, targetType, definitions));
      setResult(null);
      setError('');
      setLoading(false);
    }
  }, [isOpen, targetId, targetType, definitionCount]);

  const updateField = (name, value) => {
    setFormData((current) => ({ ...current, [name]: value }));
  };

  const handleDefinitionChange = (event) => {
    const definitionId = event.target.value;
    const definition = activeDefinitions.find((item) => item.Id === definitionId);
    setFormData((current) => ({
      ...current,
      ModelDefinitionId: definitionId,
      Model: definition ? definition.Name : current.Model
    }));
  };

  const handleEndpointToggle = (endpointId) => {
    setFormData((current) => {
      const currentIds = current.EndpointIds || [];
      return {
        ...current,
        EndpointIds: currentIds.includes(endpointId)
          ? currentIds.filter((id) => id !== endpointId)
          : [...currentIds, endpointId]
      };
    });
  };

  const buildPayload = () => ({
    Model: formData.Model.trim(),
    ModelDefinitionId: formData.ModelDefinitionId || null,
    ProbeKind: formData.ProbeKind,
    TargetMode: targetType === 'vmr' ? formData.TargetMode : 'SelectedEndpoint',
    EndpointIds: targetType === 'vmr' ? formData.EndpointIds : [],
    InputText: formData.InputText || 'conductor warmup',
    KeepAlive: formData.KeepAlive || null,
    TimeoutMs: Number(formData.TimeoutMs) || 300000,
    MaxRetries: Number(formData.MaxRetries) || 0,
    VerifyLoaded: formData.VerifyLoaded,
    IncludeInactive: formData.IncludeInactive,
    DryRun: formData.DryRun
  });

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (!target) return;

    if (submitBlockReason) {
      setError(submitBlockReason);
      return;
    }

    setLoading(true);
    setError('');
    setResult(null);
    try {
      const payload = buildPayload();
      const response = targetType === 'vmr'
        ? await api.loadVirtualModelRunnerModel(target.Id, payload, target.TenantId)
        : await api.loadModelRunnerEndpointModel(target.Id, payload, target.TenantId);

      setResult(response);
      if (response?.Success && onComplete) {
        onComplete(response, target);
      }
    } catch (err) {
      setError(err.message || 'Model load failed.');
    } finally {
      setLoading(false);
    }
  };

  if (!target) {
    return null;
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title} extraWide className="modal-load-model">
      <div className="load-model-modal">
        <div className="load-model-target">
          <div>
            <span className="load-model-label">Target</span>
            <strong>{target.Name || target.Id}</strong>
          </div>
          <span className="service-state-badge neutral">{apiType || 'Unknown'}</span>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <div className="form-row">
            <div className="form-group">
              <label htmlFor="loadModelName">Model</label>
              <input
                id="loadModelName"
                value={formData.Model}
                onChange={(event) => updateField('Model', event.target.value)}
                placeholder={targetType === 'vmr' ? 'Resolved from one attached definition when empty' : 'gemma3:4b'}
              />
              <small className="form-help">
                {targetType === 'vmr'
                  ? 'Use a model name directly, or select an attached model definition when available.'
                  : 'Type the exact model name on this runner. Placeholder text is not submitted.'}
              </small>
            </div>
            <div className="form-group">
              <label htmlFor="loadProbeKind">Probe Kind</label>
              <select
                id="loadProbeKind"
                value={formData.ProbeKind}
                onChange={(event) => updateField('ProbeKind', event.target.value)}
              >
                {PROBE_KINDS.map((probe) => <option key={probe} value={probe}>{probe}</option>)}
              </select>
            </div>
          </div>

          {targetType === 'vmr' && (
            <>
              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="loadModelDefinition">Model Definition</label>
                  <select id="loadModelDefinition" value={formData.ModelDefinitionId} onChange={handleDefinitionChange}>
                    <option value="">Use model text</option>
                    {activeDefinitions.map((definition) => (
                      <option key={definition.Id} value={definition.Id}>{definition.Name}</option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label htmlFor="loadTargetMode">Target Mode</label>
                  <select
                    id="loadTargetMode"
                    value={formData.TargetMode}
                    onChange={(event) => {
                      updateField('TargetMode', event.target.value);
                      if (event.target.value !== 'SpecificEndpointIds') updateField('EndpointIds', []);
                    }}
                  >
                    {TARGET_MODES.map((mode) => <option key={mode} value={mode}>{mode}</option>)}
                  </select>
                </div>
              </div>

              {formData.TargetMode === 'SpecificEndpointIds' && (
                <div className="form-group">
                  <label>Endpoints</label>
                  <div className="endpoint-list">
                    {attachedEndpoints.length === 0 ? (
                      <div className="no-items">No endpoints are attached to this VMR.</div>
                    ) : attachedEndpoints.map((endpoint) => (
                      <label key={endpoint.Id} className="endpoint-item">
                        <input
                          type="checkbox"
                          checked={(formData.EndpointIds || []).includes(endpoint.Id)}
                          onChange={() => handleEndpointToggle(endpoint.Id)}
                        />
                        <span className="endpoint-name">{endpoint.Name}</span>
                        <span className="endpoint-url">{endpoint.Hostname}:{endpoint.Port}</span>
                      </label>
                    ))}
                  </div>
                </div>
              )}
            </>
          )}

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="loadTimeout">Timeout ms</label>
              <input
                id="loadTimeout"
                type="number"
                min="1000"
                max="1800000"
                value={formData.TimeoutMs}
                onChange={(event) => updateField('TimeoutMs', event.target.value)}
              />
            </div>
            <div className="form-group">
              <label htmlFor="loadRetries">Retries</label>
              <input
                id="loadRetries"
                type="number"
                min="0"
                max="3"
                value={formData.MaxRetries}
                onChange={(event) => updateField('MaxRetries', event.target.value)}
              />
            </div>
            <div className="form-group">
              <label htmlFor="loadKeepAlive">Keep Alive</label>
              <input
                id="loadKeepAlive"
                value={formData.KeepAlive}
                onChange={(event) => updateField('KeepAlive', event.target.value)}
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="loadInputText">Input Text</label>
            <input
              id="loadInputText"
              value={formData.InputText}
              onChange={(event) => updateField('InputText', event.target.value)}
            />
          </div>

          <div className="form-row checkbox-row">
            <div className="form-group checkbox-group">
              <label>
                <input
                  type="checkbox"
                  checked={formData.VerifyLoaded}
                  onChange={(event) => updateField('VerifyLoaded', event.target.checked)}
                />
                Verify Loaded
              </label>
            </div>
            <div className="form-group checkbox-group">
              <label>
                <input
                  type="checkbox"
                  checked={formData.IncludeInactive}
                  onChange={(event) => updateField('IncludeInactive', event.target.checked)}
                />
                Include Inactive
              </label>
            </div>
            <div className="form-group checkbox-group">
              <label>
                <input
                  type="checkbox"
                  checked={formData.DryRun}
                  onChange={(event) => updateField('DryRun', event.target.checked)}
                />
                Dry Run
              </label>
            </div>
          </div>

          {hostedPaidProbe && (
            <div className="load-model-warning">
              This probe sends the input text to the hosted provider and may be billable. Use MetadataOnly for availability verification without generation or embedding traffic.
            </div>
          )}

          {error && <div className="validation-panel error"><strong>{error}</strong></div>}

          <div className="form-actions">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={loading}>Close</button>
            <button type="submit" className="btn-primary" disabled={loading}>
              {loading ? 'Loading...' : formData.DryRun ? 'Preview' : 'Load Model'}
            </button>
          </div>
        </form>

        {result && (
          <div className="load-model-result">
            <div className="load-model-result-header">
              <div>
                <span className="load-model-label">Outcome</span>
                <strong>{result.Message || result.OutcomeCode}</strong>
              </div>
              <span className={`service-state-badge ${getOutcomeTone(result.OutcomeCode, result.Success)}`}>
                {result.OutcomeCode}
              </span>
            </div>

            <div className="detail-grid load-model-summary-grid">
              <div className="detail-item"><label>Model</label><span>{result.Model || '-'}</span></div>
              <div className="detail-item"><label>Duration</label><span>{formatDurationMs(result.DurationMs)}</span></div>
              <div className="detail-item"><label>Target</label><span>{result.TargetType}</span></div>
              <div className="detail-item"><label>Probe</label><span>{result.ProbeKind}</span></div>
            </div>

            <div className="health-table-container">
              <table className="health-table">
                <thead>
                  <tr>
                    <th>Endpoint</th>
                    <th>API</th>
                    <th>Outcome</th>
                    <th>HTTP</th>
                    <th>Mechanism</th>
                    <th>Duration</th>
                    <th>Verified</th>
                    <th>Error</th>
                  </tr>
                </thead>
                <tbody>
                  {(result.EndpointResults || []).map((endpointResult) => (
                    <tr key={endpointResult.EndpointId || endpointResult.EndpointName}>
                      <td title={endpointResult.EndpointId}>{endpointResult.EndpointName || endpointResult.EndpointId}</td>
                      <td>{endpointResult.ApiType}</td>
                      <td>
                        <span className={`service-state-badge ${getOutcomeTone(endpointResult.OutcomeCode, endpointResult.Success)}`}>
                          {endpointResult.OutcomeCode}
                        </span>
                      </td>
                      <td>{endpointResult.ProviderStatusCode ?? '-'}</td>
                      <td>{endpointResult.Mechanism || '-'}</td>
                      <td>{formatDurationMs(endpointResult.DurationMs)}</td>
                      <td>{endpointResult.VerifiedLoaded ? 'Yes' : 'No'}</td>
                      <td className="load-model-error-cell">{endpointResult.ErrorMessage || '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <details className="load-model-json">
              <summary>JSON response</summary>
              <pre>{JSON.stringify(result, null, 2)}</pre>
            </details>
          </div>
        )}
      </div>
    </Modal>
  );
}

export default LoadModelModal;
