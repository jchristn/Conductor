import React, { useState, useEffect, useRef, useMemo } from 'react';
import { getAvailableLanguages, generateCode } from '../utils/codeGenerators';
import CopyButton from './CopyButton';

/**
 * Simple markdown parser for rendering response content.
 * Supports headings, bold, italic, code blocks, inline code, and line breaks.
 */
function parseMarkdown(text) {
  if (!text) return '';

  // Escape HTML to prevent XSS
  let html = text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

  // Code blocks (must be first to prevent other replacements inside)
  html = html.replace(/```(\w*)\n?([\s\S]*?)```/g, (match, lang, code) => {
    return `<pre class="code-block${lang ? ` language-${lang}` : ''}"><code>${code.trim()}</code></pre>`;
  });

  // Inline code
  html = html.replace(/`([^`]+)`/g, '<code class="inline-code">$1</code>');

  // Headings
  html = html.replace(/^### (.+)$/gm, '<h3>$1</h3>');
  html = html.replace(/^## (.+)$/gm, '<h2>$1</h2>');
  html = html.replace(/^# (.+)$/gm, '<h1>$1</h1>');

  // Bold
  html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');

  // Italic
  html = html.replace(/\*([^*]+)\*/g, '<em>$1</em>');

  // Line breaks (convert double newlines to paragraphs, single to <br>)
  html = html.replace(/\n\n/g, '</p><p>');
  html = html.replace(/\n/g, '<br>');

  // Wrap in paragraph if not empty
  if (html.trim()) {
    html = '<p>' + html + '</p>';
  }

  return html;
}

/**
 * Response display component for the API Explorer.
 * Shows response preview, body, headers, status, and code generation.
 */
function ApiExplorerResponse({ explorer, credentials }) {
  const {
    selectedVmr,
    apiType,
    operationId,
    operation,
    streamEnabled,
    fullUrl,
    requestBody,
    selectedCredentialId,
    responseBody,
    responsePreview,
    responseHeaders,
    responseStatus,
    isSending,
    clearResponse
  } = explorer;

  // Get the bearer token from the selected credential for code generation
  const selectedCredential = credentials.find(c => c.Id === selectedCredentialId);
  const apiKey = selectedCredential?.BearerToken || null;

  const [activeTab, setActiveTab] = useState('preview');
  const [codeLanguage, setCodeLanguage] = useState('curl');
  const previewRef = useRef(null);

  // Auto-scroll preview as content arrives
  useEffect(() => {
    if (previewRef.current && isSending) {
      previewRef.current.scrollTop = previewRef.current.scrollHeight;
    }
  }, [responsePreview, isSending]);

  // Get available code languages based on current configuration
  const availableLanguages = useMemo(() => {
    return getAvailableLanguages(apiType, streamEnabled);
  }, [apiType, streamEnabled]);

  // Ensure selected language is available
  useEffect(() => {
    const isAvailable = availableLanguages.some(l => l.id === codeLanguage);
    if (!isAvailable && availableLanguages.length > 0) {
      setCodeLanguage(availableLanguages[0].id);
    }
  }, [availableLanguages, codeLanguage]);

  // Generate code for current configuration
  const generatedCode = useMemo(() => {
    if (!fullUrl || !operation) return '';

    const headers = {
      'Content-Type': 'application/json'
    };
    if (apiKey) {
      headers['Authorization'] = `Bearer ${apiKey}`;
    }

    return generateCode(codeLanguage, {
      url: fullUrl,
      method: operation.method,
      headers,
      body: requestBody,
      apiType,
      operationId
    });
  }, [codeLanguage, fullUrl, operation, requestBody, apiKey, apiType, operationId]);

  // Format JSON for display
  const formattedBody = useMemo(() => {
    if (!responseBody) return '';
    try {
      const parsed = JSON.parse(responseBody);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return responseBody;
    }
  }, [responseBody]);

  // Format headers for display
  const formattedHeaders = useMemo(() => {
    return JSON.stringify(responseHeaders, null, 2);
  }, [responseHeaders]);

  // Render status badge
  const renderStatusBadge = () => {
    const { status, statusText, isCancelled, error } = responseStatus;

    if (error) {
      return <span className="status-badge error">Error</span>;
    }
    if (isCancelled) {
      return <span className="status-badge cancelled">Cancelled</span>;
    }
    if (status === null) {
      return null;
    }
    const isSuccess = status >= 200 && status < 300;
    const isClientError = status >= 400 && status < 500;
    const isServerError = status >= 500;

    let className = 'status-badge';
    if (isSuccess) className += ' success';
    else if (isClientError) className += ' client-error';
    else if (isServerError) className += ' server-error';

    return (
      <span className={className}>
        {status} {statusText}
      </span>
    );
  };

  // Render timing metrics
  const renderMetrics = () => {
    const { requestTime, timeToFirstToken, totalStreamingTime, isCancelled, error } = responseStatus;

    if (error) {
      return (
        <div className="metrics-error">
          <strong>Error:</strong> {error}
        </div>
      );
    }

    return (
      <div className="metrics-grid">
        {totalStreamingTime !== null ? (
          <>
            <div className="metric">
              <span className="metric-label">Time to First Token</span>
              <span className="metric-value">{timeToFirstToken?.toFixed(0) || '-'} ms</span>
            </div>
            <div className="metric">
              <span className="metric-label">Total Streaming Time</span>
              <span className="metric-value">{totalStreamingTime?.toFixed(0) || '-'} ms</span>
            </div>
          </>
        ) : requestTime !== null ? (
          <div className="metric">
            <span className="metric-label">Request Time</span>
            <span className="metric-value">{requestTime?.toFixed(0) || '-'} ms</span>
          </div>
        ) : null}
        {isCancelled && (
          <div className="metric cancelled">
            <span className="metric-label">Status</span>
            <span className="metric-value">Request Cancelled</span>
          </div>
        )}
        <div className="metric">
          <span className="metric-label">Type</span>
          <span className="metric-value">{streamEnabled && operation?.supportsStreaming ? 'Streaming' : 'Non-streaming'}</span>
        </div>
      </div>
    );
  };

  const tabs = [
    { id: 'preview', label: 'Preview' },
    { id: 'body', label: 'Body' },
    { id: 'headers', label: 'Headers' },
    { id: 'status', label: 'Status' },
    { id: 'code', label: 'Code' }
  ];

  return (
    <div className="api-explorer-response">
      <div className="response-header">
        <div className="response-tabs">
          {tabs.map(tab => (
            <button
              key={tab.id}
              className={`tab-button ${activeTab === tab.id ? 'active' : ''}`}
              onClick={() => setActiveTab(tab.id)}
            >
              {tab.label}
            </button>
          ))}
        </div>
        <div className="response-actions">
          {renderStatusBadge()}
          {(responseBody || responsePreview) && (
            <button
              className="btn-secondary btn-small"
              onClick={clearResponse}
            >
              Clear
            </button>
          )}
        </div>
      </div>

      <div className="response-content">
        {activeTab === 'preview' && (
          <div className="tab-content preview-tab" ref={previewRef}>
            {isSending && !responsePreview && (
              <div className="waiting-indicator">
                <span className="spinner"></span>
                Waiting for response...
              </div>
            )}
            {isSending && responsePreview && (
              <div className="streaming-indicator">
                <span className="streaming-dot"></span>
                Streaming...
              </div>
            )}
            {responsePreview ? (
              <div
                className="markdown-content"
                dangerouslySetInnerHTML={{ __html: parseMarkdown(responsePreview) }}
              />
            ) : !isSending ? (
              <div className="placeholder">
                Send a request to see the response preview
              </div>
            ) : null}
            {responsePreview && (
              <div className="preview-actions">
                <CopyButton value={responsePreview} title="Copy response" />
              </div>
            )}
          </div>
        )}

        {activeTab === 'body' && (
          <div className="tab-content body-tab">
            {responseBody ? (
              <>
                <pre className="response-body-code">{formattedBody}</pre>
                <div className="body-actions">
                  <CopyButton value={formattedBody} title="Copy JSON" />
                </div>
              </>
            ) : (
              <div className="placeholder">
                Send a request to see the response body
              </div>
            )}
          </div>
        )}

        {activeTab === 'headers' && (
          <div className="tab-content headers-tab">
            {Object.keys(responseHeaders).length > 0 ? (
              <>
                <pre className="response-headers-code">{formattedHeaders}</pre>
                <div className="headers-actions">
                  <CopyButton value={formattedHeaders} title="Copy headers" />
                </div>
              </>
            ) : (
              <div className="placeholder">
                Send a request to see response headers
              </div>
            )}
          </div>
        )}

        {activeTab === 'status' && (
          <div className="tab-content status-tab">
            {responseStatus.status !== null || responseStatus.error || responseStatus.isCancelled ? (
              <div className="status-content">
                <div className="status-header">
                  {renderStatusBadge()}
                </div>
                {renderMetrics()}
              </div>
            ) : (
              <div className="placeholder">
                Send a request to see status information
              </div>
            )}
          </div>
        )}

        {activeTab === 'code' && (
          <div className="tab-content code-tab">
            <div className="code-language-selector">
              <label htmlFor="code-language">Language:</label>
              <select
                id="code-language"
                value={codeLanguage}
                onChange={(e) => setCodeLanguage(e.target.value)}
              >
                {availableLanguages.map(lang => (
                  <option key={lang.id} value={lang.id}>
                    {lang.label}
                  </option>
                ))}
              </select>
            </div>
            {selectedVmr ? (
              <>
                <pre className="generated-code">{generatedCode}</pre>
                <div className="code-actions">
                  <CopyButton value={generatedCode} title="Copy code" />
                </div>
              </>
            ) : (
              <div className="placeholder">
                Select a VMR to generate code snippets
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default ApiExplorerResponse;
