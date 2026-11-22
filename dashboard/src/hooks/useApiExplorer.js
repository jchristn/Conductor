import { useState, useCallback, useRef, useEffect } from 'react';
import { getOperation, generateRequestBody, getOperationsForApiType, isOperationStreaming, resolveOperationPath } from '../utils/requestTemplates';
import { buildExplorerHeaders, getStreamFormat, isClientFacing, normalizeApiType } from '../utils/providers';
import { createStreamLineParser, extractExplorerContent } from '../utils/streamParsers';
import { extractDecisionAnswers } from '../utils/typeSafeDecisions';

const STORAGE_KEY = 'conductor_api_explorer';
const HISTORY_KEY = 'conductor_api_explorer_history';
const MAX_HISTORY_ITEMS = 10;

/**
 * Load saved configuration from localStorage
 * @returns {Object} Saved configuration or defaults
 */
const DEFAULT_CONFIG = {
  selectedVmrId: null,
  apiType: 'OpenAI',
  operationId: 'chatCompletions',
  modelName: '',
  streamEnabled: true,
  selectedCredentialId: ''
};

/**
 * Resolve a stored or VMR-supplied API type to a client-facing type the explorer can call.
 * Unknown or endpoint-only values fall back to the default rather than guessing.
 */
function resolveExplorerApiType(value) {
  const name = normalizeApiType(value);
  return name && isClientFacing(name) ? name : DEFAULT_CONFIG.apiType;
}

function loadSavedConfig() {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      const parsed = JSON.parse(saved);
      return { ...DEFAULT_CONFIG, ...parsed, apiType: resolveExplorerApiType(parsed?.apiType) };
    }
  } catch {
    // Ignore parse errors
  }
  return { ...DEFAULT_CONFIG };
}

/**
 * Save configuration to localStorage
 * @param {Object} config - Configuration to save
 */
function saveConfig(config) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(config));
  } catch {
    // Ignore storage errors
  }
}

/**
 * Load request history from localStorage
 * @returns {Array} Request history
 */
function loadHistory() {
  try {
    const saved = localStorage.getItem(HISTORY_KEY);
    if (saved) {
      return JSON.parse(saved);
    }
  } catch {
    // Ignore parse errors
  }
  return [];
}

/**
 * Save request history to localStorage
 * @param {Array} history - History to save
 */
function saveHistory(history) {
  try {
    localStorage.setItem(HISTORY_KEY, JSON.stringify(history.slice(0, MAX_HISTORY_ITEMS)));
  } catch {
    // Ignore storage errors
  }
}

/**
 * Custom hook for API Explorer state management and request handling
 * @param {string} serverUrl - The Conductor server URL
 * @returns {Object} API Explorer state and methods
 */
export function useApiExplorer(serverUrl) {
  // Load saved configuration
  const savedConfig = loadSavedConfig();

  // Configuration state
  const [selectedVmr, setSelectedVmr] = useState(null);
  const [apiType, setApiType] = useState(savedConfig.apiType);
  const [operationId, setOperationId] = useState(savedConfig.operationId);
  const [modelName, setModelName] = useState(savedConfig.modelName);
  const [streamEnabled, setStreamEnabled] = useState(savedConfig.streamEnabled);
  const [selectedCredentialId, setSelectedCredentialId] = useState(savedConfig.selectedCredentialId);

  // Request state
  const [requestBody, setRequestBody] = useState('');
  const [isManuallyEdited, setIsManuallyEdited] = useState(false);

  // Response state
  const [responseBody, setResponseBody] = useState('');
  const [responsePreview, setResponsePreview] = useState('');
  const [responseHeaders, setResponseHeaders] = useState({});
  const [responseStatus, setResponseStatus] = useState({
    status: null,
    statusText: '',
    requestTime: null,
    timeToFirstToken: null,
    totalStreamingTime: null,
    isCancelled: false,
    error: null
  });
  const [isSending, setIsSending] = useState(false);
  const [decisionAnswers, setDecisionAnswers] = useState([]);

  // Request history
  const [history, setHistory] = useState(() => loadHistory());

  // Abort controller ref
  const abortControllerRef = useRef(null);
  const requestStartTimeRef = useRef(null);
  const firstTokenTimeRef = useRef(null);

  // Get current operation details
  const operation = getOperation(apiType, operationId);

  // Whether the current operation streams (forceStream operations always do)
  const isStreaming = isOperationStreaming(operation, streamEnabled);

  // Construct full endpoint URL
  const getFullUrl = useCallback(() => {
    if (!selectedVmr || !operation) return '';
    // Remove trailing slash from basePath; the operation path starts with /
    const basePath = (selectedVmr.BasePath || '').replace(/\/+$/, '');
    return `${serverUrl}${basePath}${resolveOperationPath(apiType, operation, modelName, streamEnabled)}`;
  }, [serverUrl, selectedVmr, operation, modelName, apiType, streamEnabled]);

  // Save configuration when it changes
  useEffect(() => {
    saveConfig({
      selectedVmrId: selectedVmr?.Id || null,
      apiType,
      operationId,
      modelName,
      streamEnabled,
      selectedCredentialId
    });
  }, [selectedVmr, apiType, operationId, modelName, streamEnabled, selectedCredentialId]);

  // Generate request body when configuration changes (if not manually edited)
  useEffect(() => {
    if (!isManuallyEdited && operation && operation.getTemplate) {
      const body = generateRequestBody(apiType, operationId, modelName, streamEnabled);
      setRequestBody(body);
    }
  }, [apiType, operationId, modelName, streamEnabled, operation, isManuallyEdited]);

  // Reset operation when API type changes
  useEffect(() => {
    const operations = getOperationsForApiType(apiType);
    const currentExists = operations.some(op => op.id === operationId);
    if (!currentExists && operations.length > 0) {
      setOperationId(operations[0].id);
    }
  }, [apiType, operationId]);

  /**
   * Update request body manually
   */
  const updateRequestBodyManual = useCallback((body) => {
    setRequestBody(body);
    setIsManuallyEdited(true);
  }, []);

  /**
   * Reset request body to template
   */
  const resetRequestBody = useCallback(() => {
    setIsManuallyEdited(false);
    if (operation && operation.getTemplate) {
      const body = generateRequestBody(apiType, operationId, modelName, streamEnabled);
      setRequestBody(body);
    }
  }, [apiType, operationId, modelName, streamEnabled, operation]);

  /**
   * Clear response state
   */
  const clearResponse = useCallback(() => {
    setResponseBody('');
    setResponsePreview('');
    setResponseHeaders({});
    setDecisionAnswers([]);
    setResponseStatus({
      status: null,
      statusText: '',
      requestTime: null,
      timeToFirstToken: null,
      totalStreamingTime: null,
      isCancelled: false,
      error: null
    });
  }, []);

  /**
   * Extract content from response based on API type and operation
   */
  const extractContent = useCallback((data, streaming, event = null) => {
    return extractExplorerContent(apiType, operationId, data, streaming, event);
  }, [apiType, operationId]);

  /**
   * Handle streaming response. The frame format comes from the VMR type in the provider registry.
   */
  const handleStreamingResponse = useCallback(async (response) => {
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    const parseLine = createStreamLineParser(getStreamFormat(apiType));
    let buffer = '';
    let fullBody = '';
    let fullPreview = '';
    let firstToken = false;

    const handleParsed = (parsed) => {
      if (!parsed) return;

      // Record time to first token
      if (!firstToken) {
        firstToken = true;
        firstTokenTimeRef.current = performance.now();
        setResponseStatus(prev => ({
          ...prev,
          timeToFirstToken: firstTokenTimeRef.current - requestStartTimeRef.current
        }));
      }

      const content = extractContent(parsed.data, true, parsed.event);
      if (content) {
        fullPreview += content;
        setResponsePreview(fullPreview);
      }
    };

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        const chunk = decoder.decode(value, { stream: true });
        buffer += chunk;
        fullBody += chunk;

        // Process complete lines
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          handleParsed(parseLine(line));
        }

        setResponseBody(fullBody);
      }

      // Process any remaining buffer
      const flushed = decoder.decode();
      if (flushed) {
        buffer += flushed;
        fullBody += flushed;
      }
      if (buffer.trim()) {
        handleParsed(parseLine(buffer));
      }
      setResponseBody(fullBody);

      // Calculate total streaming time
      const endTime = performance.now();
      setResponseStatus(prev => ({
        ...prev,
        totalStreamingTime: endTime - requestStartTimeRef.current
      }));

    } finally {
      reader.releaseLock();
    }
  }, [apiType, extractContent]);

  /**
   * Handle non-streaming response
   */
  const handleNonStreamingResponse = useCallback(async (response, sentBody) => {
    const text = await response.text();
    setResponseBody(text);

    try {
      const data = JSON.parse(text);
      const content = extractContent(data, false);
      setResponsePreview(content);
      if (operation?.decision && response.ok) {
        setDecisionAnswers(extractDecisionAnswers(data, sentBody));
      }
    } catch {
      setResponsePreview(text);
    }

    const endTime = performance.now();
    setResponseStatus(prev => ({
      ...prev,
      requestTime: endTime - requestStartTimeRef.current
    }));
  }, [extractContent, operation]);

  /**
   * Add request to history
   */
  const addToHistory = useCallback((request) => {
    const historyItem = {
      id: Date.now().toString(),
      timestamp: new Date().toISOString(),
      vmrName: selectedVmr?.Name || 'Unknown',
      vmrId: selectedVmr?.Id || null,
      apiType,
      operationId,
      operation: operation?.label || operationId,
      method: operation?.method || 'POST',
      endpoint: operation?.endpoint || '',
      modelName,
      streamEnabled: isStreaming,
      requestBody: request.body,
      url: request.url
    };

    setHistory(prev => {
      const newHistory = [historyItem, ...prev].slice(0, MAX_HISTORY_ITEMS);
      saveHistory(newHistory);
      return newHistory;
    });
  }, [selectedVmr, apiType, operationId, operation, modelName, isStreaming]);

  /**
   * Send the API request
   * @param {string} apiKey - The API key to use for authentication (from selected credential)
   */
  const sendRequest = useCallback(async (apiKey) => {
    if (!selectedVmr || !operation) return;

    // Validate JSON body for POST/PUT/DELETE with body
    if (operation.method !== 'GET' && requestBody) {
      try {
        JSON.parse(requestBody);
      } catch (e) {
        setResponseStatus(prev => ({
          ...prev,
          error: `Invalid JSON: ${e.message}`
        }));
        return;
      }
    }

    // Abort any existing request
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    // Create new abort controller
    abortControllerRef.current = new AbortController();

    // Clear previous response
    clearResponse();

    // Set sending state
    setIsSending(true);
    requestStartTimeRef.current = performance.now();
    firstTokenTimeRef.current = null;

    const url = getFullUrl();
    // Same headers the code generators use, so snippets match this request exactly
    const headers = buildExplorerHeaders(apiType, apiKey);

    // Add to history
    addToHistory({ url, body: requestBody });

    try {
      const fetchOptions = {
        method: operation.method,
        headers,
        signal: abortControllerRef.current.signal
      };

      if (operation.method !== 'GET' && requestBody) {
        fetchOptions.body = requestBody;
      }

      const response = await fetch(url, fetchOptions);

      // Capture headers
      const headersObj = {};
      response.headers.forEach((value, key) => {
        headersObj[key] = value;
      });
      setResponseHeaders(headersObj);

      // Update status
      setResponseStatus(prev => ({
        ...prev,
        status: response.status,
        statusText: response.statusText
      }));

      // Only use streaming handler if:
      // 1. Response is successful (2xx)
      // 2. Streaming is enabled
      // 3. Operation supports streaming
      // 4. Response content type indicates streaming (SSE or chunked)
      const contentType = response.headers.get('content-type') || '';
      const isStreamingResponse = response.ok &&
        isStreaming &&
        (contentType.includes('text/event-stream') ||
         contentType.includes('application/x-ndjson') ||
         (response.headers.get('transfer-encoding') === 'chunked' && !contentType.includes('application/json')));

      if (isStreamingResponse) {
        await handleStreamingResponse(response);
      } else {
        await handleNonStreamingResponse(response, fetchOptions.body || '');
      }

    } catch (err) {
      if (err.name === 'AbortError') {
        setResponseStatus(prev => ({
          ...prev,
          isCancelled: true,
          requestTime: performance.now() - requestStartTimeRef.current
        }));
      } else {
        setResponseStatus(prev => ({
          ...prev,
          error: err.message
        }));
      }
    } finally {
      setIsSending(false);
      abortControllerRef.current = null;
    }
  }, [selectedVmr, operation, requestBody, apiType, isStreaming, getFullUrl, clearResponse, addToHistory, handleStreamingResponse, handleNonStreamingResponse]);

  /**
   * Stop the current request
   */
  const stopRequest = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
  }, []);

  /**
   * Load a request from history
   */
  const loadFromHistory = useCallback((historyItem) => {
    setApiType(resolveExplorerApiType(historyItem.apiType));
    setOperationId(historyItem.operationId);
    setModelName(historyItem.modelName);
    setStreamEnabled(historyItem.streamEnabled);
    setRequestBody(historyItem.requestBody || '');
    setIsManuallyEdited(true);
  }, []);

  /**
   * Clear history
   */
  const clearHistory = useCallback(() => {
    setHistory([]);
    saveHistory([]);
  }, []);

  /**
   * Handle VMR selection
   */
  const handleSelectVmr = useCallback((vmr) => {
    setSelectedVmr(vmr);
    setIsManuallyEdited(false); // Reset so template regenerates
    if (vmr) {
      // The explorer speaks the VMR's configured type; nothing is inferred.
      setApiType(resolveExplorerApiType(vmr.ApiType));
    }
  }, []);

  /**
   * Handle operation change
   */
  const handleSetOperationId = useCallback((opId) => {
    setOperationId(opId);
    const nextOperation = getOperation(apiType, opId);
    if (opId === 'streamGenerateContent' || nextOperation?.forceStream) {
      setStreamEnabled(true);
    }
    setIsManuallyEdited(false); // Reset so template regenerates
  }, [apiType]);

  /**
   * Handle API type change
   */
  const handleSetApiType = useCallback((type) => {
    setApiType(type);
    setIsManuallyEdited(false); // Reset so template regenerates
  }, []);

  return {
    // Configuration
    selectedVmr,
    setSelectedVmr: handleSelectVmr,
    apiType,
    setApiType: handleSetApiType,
    operationId,
    setOperationId: handleSetOperationId,
    modelName,
    setModelName,
    streamEnabled,
    setStreamEnabled,
    selectedCredentialId,
    setSelectedCredentialId,

    // Request
    requestBody,
    setRequestBody: updateRequestBodyManual,
    resetRequestBody,
    isManuallyEdited,

    // Computed values
    operation,
    isStreaming,
    fullUrl: getFullUrl(),

    // Response
    responseBody,
    responsePreview,
    responseHeaders,
    responseStatus,
    isSending,
    decisionAnswers,

    // Actions
    sendRequest,
    stopRequest,
    clearResponse,

    // History
    history,
    loadFromHistory,
    clearHistory
  };
}

export default useApiExplorer;
