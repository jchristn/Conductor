import React, { useState, useCallback, useRef, useEffect } from 'react';
import CopyButton from './CopyButton';

/**
 * Request builder component for the API Explorer.
 * Displays endpoint URL and allows editing of request body.
 */
function ApiExplorerRequest({ explorer, credentials }) {
  const {
    selectedVmr,
    selectedCredentialId,
    operation,
    fullUrl,
    requestBody,
    setRequestBody,
    resetRequestBody,
    isManuallyEdited,
    isSending,
    sendRequest,
    stopRequest,
    history,
    loadFromHistory,
    clearHistory
  } = explorer;

  // Get the bearer token from the selected credential
  const getApiKey = useCallback(() => {
    if (!selectedCredentialId) return null;
    const credential = credentials.find(c => c.Id === selectedCredentialId);
    return credential?.BearerToken || null;
  }, [selectedCredentialId, credentials]);

  // Wrapper for sendRequest that passes the API key
  const handleSendRequest = useCallback(() => {
    const apiKey = getApiKey();
    sendRequest(apiKey);
  }, [sendRequest, getApiKey]);

  const [jsonError, setJsonError] = useState(null);
  const [showHistory, setShowHistory] = useState(false);
  const textareaRef = useRef(null);

  // Validate JSON on blur
  const handleBlur = useCallback(() => {
    if (!requestBody || operation?.method === 'GET') {
      setJsonError(null);
      return;
    }
    try {
      JSON.parse(requestBody);
      setJsonError(null);
    } catch (err) {
      setJsonError(err.message);
    }
  }, [requestBody, operation]);

  // Handle keyboard shortcuts
  const handleKeyDown = useCallback((e) => {
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
      e.preventDefault();
      if (!isSending && selectedVmr && operation) {
        handleSendRequest();
      }
    }
    if (e.key === 'Escape' && isSending) {
      e.preventDefault();
      stopRequest();
    }
  }, [isSending, selectedVmr, operation, handleSendRequest, stopRequest]);

  // Add global keyboard listener for Escape
  useEffect(() => {
    const handleGlobalKeyDown = (e) => {
      if (e.key === 'Escape' && isSending) {
        e.preventDefault();
        stopRequest();
      }
    };
    window.addEventListener('keydown', handleGlobalKeyDown);
    return () => window.removeEventListener('keydown', handleGlobalKeyDown);
  }, [isSending, stopRequest]);

  const formatDate = (dateString) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  const canSend = selectedVmr && operation && !isSending;

  return (
    <div className="api-explorer-request">
      <div className="request-header">
        <h3>Request</h3>
        <div className="request-actions">
          {history.length > 0 && (
            <button
              className="btn-secondary btn-small"
              onClick={() => setShowHistory(!showHistory)}
              title="View request history"
            >
              <svg width="14" height="14" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clipRule="evenodd" />
              </svg>
              History ({history.length})
            </button>
          )}
          {isManuallyEdited && (
            <button
              className="btn-secondary btn-small"
              onClick={resetRequestBody}
              title="Reset to template"
            >
              Reset
            </button>
          )}
        </div>
      </div>

      {showHistory && history.length > 0 && (
        <div className="request-history">
          <div className="history-header">
            <span>Recent Requests</span>
            <button
              className="btn-icon btn-small"
              onClick={clearHistory}
              title="Clear history"
            >
              <svg width="14" height="14" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2v10a2 2 0 002 2h8a2 2 0 002-2V6a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zM7 8a1 1 0 012 0v6a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z" clipRule="evenodd" />
              </svg>
            </button>
          </div>
          <div className="history-list">
            {history.map(item => (
              <div
                key={item.id}
                className="history-item"
                onClick={() => {
                  loadFromHistory(item);
                  setShowHistory(false);
                }}
              >
                <div className="history-item-header">
                  <span className="history-vmr">{item.vmrName}</span>
                  <span className="history-time">{formatDate(item.timestamp)}</span>
                </div>
                <div className="history-item-detail">
                  <span className={`method-badge ${item.method.toLowerCase()}`}>{item.method}</span>
                  <span className="history-operation">{item.operation}</span>
                  {item.modelName && <span className="history-model">{item.modelName}</span>}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="endpoint-display">
        <span className={`method-badge ${operation?.method?.toLowerCase() || 'get'}`}>
          {operation?.method || 'GET'}
        </span>
        <code className="endpoint-url">{fullUrl || 'Select a VMR to see the endpoint URL'}</code>
        {fullUrl && <CopyButton value={fullUrl} title="Copy URL" />}
      </div>

      {operation?.method !== 'GET' && (
        <div className="request-body">
          <label htmlFor="request-body">
            Request Body
            {isManuallyEdited && <span className="edited-indicator">(edited)</span>}
          </label>
          <textarea
            ref={textareaRef}
            id="request-body"
            className={`code-input ${jsonError ? 'error' : ''}`}
            value={requestBody}
            onChange={(e) => setRequestBody(e.target.value)}
            onBlur={handleBlur}
            onKeyDown={handleKeyDown}
            rows={15}
            spellCheck={false}
            placeholder="Enter JSON request body..."
          />
          {jsonError && (
            <div className="json-error">
              <svg width="14" height="14" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
              </svg>
              {jsonError}
            </div>
          )}
        </div>
      )}

      <div className="request-footer">
        <div className="send-actions">
          <button
            className="btn-primary"
            onClick={handleSendRequest}
            disabled={!canSend}
          >
            {isSending ? (
              <>
                <span className="spinner"></span>
                Sending...
              </>
            ) : (
              'Send Request'
            )}
          </button>
          {isSending && (
            <button
              className="btn-danger"
              onClick={stopRequest}
            >
              Stop
            </button>
          )}
        </div>
        <span className="keyboard-hint">
          {isSending ? 'Press Esc to stop' : 'Press Ctrl+Enter to send'}
        </span>
      </div>
    </div>
  );
}

export default ApiExplorerRequest;
