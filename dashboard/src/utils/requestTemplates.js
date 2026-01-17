/**
 * Request body templates for different API operations.
 * Each template provides sensible defaults for common parameters.
 */

/**
 * OpenAI API request templates
 */
export const openaiTemplates = {
  chatCompletions: (modelName, streamEnabled) => ({
    model: modelName || 'gpt-3.5-turbo',
    messages: [
      { role: 'system', content: 'You are a helpful assistant.' },
      { role: 'user', content: 'Hello!' }
    ],
    stream: streamEnabled,
    temperature: 0.7,
    max_tokens: 1024
  }),

  completions: (modelName, streamEnabled) => ({
    model: modelName || 'gpt-3.5-turbo-instruct',
    prompt: 'Say hello in a creative way:',
    stream: streamEnabled,
    temperature: 0.7,
    max_tokens: 256
  }),

  embeddings: (modelName) => ({
    model: modelName || 'text-embedding-ada-002',
    input: 'The quick brown fox jumps over the lazy dog.'
  })
};

/**
 * Ollama API request templates
 */
export const ollamaTemplates = {
  chat: (modelName, streamEnabled) => ({
    model: modelName || 'llama2',
    messages: [
      { role: 'system', content: 'You are a helpful assistant.' },
      { role: 'user', content: 'Hello!' }
    ],
    stream: streamEnabled,
    options: {
      temperature: 0.7
    }
  }),

  generate: (modelName, streamEnabled) => ({
    model: modelName || 'llama2',
    prompt: 'Say hello in a creative way:',
    stream: streamEnabled,
    options: {
      temperature: 0.7
    }
  }),

  embed: (modelName) => ({
    model: modelName || 'nomic-embed-text',
    input: 'The quick brown fox jumps over the lazy dog.'
  }),

  pull: (modelName) => ({
    name: modelName || 'llama2',
    stream: true
  }),

  delete: (modelName) => ({
    name: modelName || 'llama2'
  })
};

/**
 * API operation definitions for OpenAI
 */
export const openaiOperations = [
  {
    id: 'chatCompletions',
    label: 'Chat Completions',
    endpoint: '/v1/chat/completions',
    method: 'POST',
    supportsStreaming: true,
    getTemplate: openaiTemplates.chatCompletions
  },
  {
    id: 'completions',
    label: 'Completions',
    endpoint: '/v1/completions',
    method: 'POST',
    supportsStreaming: true,
    getTemplate: openaiTemplates.completions
  },
  {
    id: 'embeddings',
    label: 'Embeddings',
    endpoint: '/v1/embeddings',
    method: 'POST',
    supportsStreaming: false,
    getTemplate: openaiTemplates.embeddings
  },
  {
    id: 'listModels',
    label: 'List Models',
    endpoint: '/v1/models',
    method: 'GET',
    supportsStreaming: false,
    getTemplate: null
  }
];

/**
 * API operation definitions for Ollama
 */
export const ollamaOperations = [
  {
    id: 'chat',
    label: 'Chat',
    endpoint: '/api/chat',
    method: 'POST',
    supportsStreaming: true,
    getTemplate: ollamaTemplates.chat
  },
  {
    id: 'generate',
    label: 'Generate',
    endpoint: '/api/generate',
    method: 'POST',
    supportsStreaming: true,
    getTemplate: ollamaTemplates.generate
  },
  {
    id: 'embed',
    label: 'Embeddings',
    endpoint: '/api/embed',
    method: 'POST',
    supportsStreaming: false,
    getTemplate: ollamaTemplates.embed
  },
  {
    id: 'tags',
    label: 'List Models',
    endpoint: '/api/tags',
    method: 'GET',
    supportsStreaming: false,
    getTemplate: null
  },
  {
    id: 'pull',
    label: 'Pull Model',
    endpoint: '/api/pull',
    method: 'POST',
    supportsStreaming: true,
    getTemplate: ollamaTemplates.pull
  },
  {
    id: 'delete',
    label: 'Delete Model',
    endpoint: '/api/delete',
    method: 'DELETE',
    supportsStreaming: false,
    getTemplate: ollamaTemplates.delete
  },
  {
    id: 'ps',
    label: 'List Running Models',
    endpoint: '/api/ps',
    method: 'GET',
    supportsStreaming: false,
    getTemplate: null
  }
];

/**
 * Get operations list based on API type
 * @param {string} apiType - 'OpenAI' or 'Ollama'
 * @returns {Array} List of operation definitions
 */
export function getOperationsForApiType(apiType) {
  return apiType === 'Ollama' ? ollamaOperations : openaiOperations;
}

/**
 * Get a specific operation by ID and API type
 * @param {string} apiType - 'OpenAI' or 'Ollama'
 * @param {string} operationId - The operation ID
 * @returns {Object|null} The operation definition or null
 */
export function getOperation(apiType, operationId) {
  const operations = getOperationsForApiType(apiType);
  return operations.find(op => op.id === operationId) || null;
}

/**
 * Generate a request body template for the given operation
 * @param {string} apiType - 'OpenAI' or 'Ollama'
 * @param {string} operationId - The operation ID
 * @param {string} modelName - The model name to use
 * @param {boolean} streamEnabled - Whether streaming is enabled
 * @returns {string} JSON string of the request body, or empty string for GET requests
 */
export function generateRequestBody(apiType, operationId, modelName, streamEnabled) {
  const operation = getOperation(apiType, operationId);
  if (!operation || !operation.getTemplate) {
    return '';
  }

  const template = operation.getTemplate(modelName, streamEnabled);
  return JSON.stringify(template, null, 2);
}
