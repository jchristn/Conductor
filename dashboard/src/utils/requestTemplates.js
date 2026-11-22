/**
 * Request body templates for different API operations.
 * Each template provides sensible defaults for common parameters.
 * The operation set used for a VMR type comes from the provider registry (providers.js).
 */
import { getProvider, DEFAULT_TYPESAFE_MODEL } from './providers';
import { buildDecisionBody, createDecisionQuestion } from './typeSafeDecisions';

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
 * Gemini API request templates
 */
export const geminiTemplates = {
  generateContent: (_modelName, _streamEnabled) => ({
    contents: [
      {
        role: 'user',
        parts: [
          { text: 'Explain how AI works in a single paragraph.' }
        ]
      }
    ],
    generationConfig: {
      temperature: 0.7,
      maxOutputTokens: 1024
    }
  }),

  embedContent: () => ({
    content: {
      parts: [
        { text: 'The quick brown fox jumps over the lazy dog.' }
      ]
    }
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
 * Anthropic Messages API request templates
 */
export const anthropicTemplates = {
  messages: (modelName) => ({
    model: modelName || 'claude-opus-5-5',
    max_tokens: 1024,
    messages: [
      { role: 'user', content: 'Hello!' }
    ]
  }),

  messagesStream: (modelName) => ({
    model: modelName || 'claude-opus-5-5',
    max_tokens: 1024,
    stream: true,
    messages: [
      { role: 'user', content: 'Hello!' }
    ]
  }),

  countTokens: (modelName) => ({
    model: modelName || 'claude-opus-5-5',
    messages: [
      { role: 'user', content: 'Hello!' }
    ]
  })
};

/**
 * TypeSafe System One request templates
 */
export const typeSafeTemplates = {
  decide: (modelName) => buildDecisionBody({
    model: modelName || DEFAULT_TYPESAFE_MODEL,
    state: 'Hi, my invoice was charged twice this month. Can you refund the duplicate?',
    questions: [
      createDecisionQuestion('noul', 'is_question'),
      createDecisionQuestion('choice', 'category'),
      createDecisionQuestion('score', 'urgency')
    ]
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
 * API operation definitions for Gemini
 */
export const geminiOperations = [
  {
    id: 'generateContent',
    label: 'Generate Content',
    endpoint: '/v1beta/models/{model}:generateContent',
    method: 'POST',
    supportsStreaming: true,
    getTemplate: geminiTemplates.generateContent
  },
  {
    id: 'embedContent',
    label: 'Embeddings',
    endpoint: '/v1beta/models/{model}:embedContent',
    method: 'POST',
    supportsStreaming: false,
    getTemplate: geminiTemplates.embedContent
  },
  {
    id: 'listModels',
    label: 'List Models',
    endpoint: '/v1beta/models',
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
 * API operation definitions for Anthropic
 */
export const anthropicOperations = [
  {
    id: 'messages',
    label: 'Messages',
    endpoint: '/v1/messages',
    method: 'POST',
    supportsStreaming: false,
    getTemplate: anthropicTemplates.messages
  },
  {
    id: 'messagesStream',
    label: 'Messages (stream)',
    endpoint: '/v1/messages',
    method: 'POST',
    supportsStreaming: true,
    forceStream: true,
    getTemplate: anthropicTemplates.messagesStream
  },
  {
    id: 'countTokens',
    label: 'Count Tokens',
    endpoint: '/v1/messages/count_tokens',
    method: 'POST',
    supportsStreaming: false,
    getTemplate: anthropicTemplates.countTokens
  },
  {
    id: 'listModels',
    label: 'List Models',
    endpoint: '/v1/models',
    method: 'GET',
    supportsStreaming: false,
    getTemplate: null
  },
  {
    id: 'getModel',
    label: 'Get Model',
    endpoint: '/v1/models/{model}',
    method: 'GET',
    supportsStreaming: false,
    getTemplate: null
  }
];

/**
 * API operation definitions for TypeSafe (System One)
 */
export const typeSafeOperations = [
  {
    id: 'decide',
    label: 'Decide',
    endpoint: '/v1/systemone',
    method: 'POST',
    supportsStreaming: false,
    decision: true,
    getTemplate: typeSafeTemplates.decide
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

const OPERATION_SETS = {
  openai: openaiOperations,
  gemini: geminiOperations,
  ollama: ollamaOperations,
  anthropic: anthropicOperations,
  typesafe: typeSafeOperations
};

/**
 * Get operations list based on API type. The operation set is named by the provider registry.
 * @param {string} apiType - provider API type (see providers.js)
 * @returns {Array} List of operation definitions; the OpenAI set when the type is unknown
 */
export function getOperationsForApiType(apiType) {
  const key = getProvider(apiType)?.explorerOperations;
  return OPERATION_SETS[key] || openaiOperations;
}

/**
 * Get a specific operation by ID and API type
 * @param {string} apiType - provider API type
 * @param {string} operationId - The operation ID
 * @returns {Object|null} The operation definition or null
 */
export function getOperation(apiType, operationId) {
  const operations = getOperationsForApiType(apiType);
  return operations.find(op => op.id === operationId) || null;
}

/**
 * Generate a request body template for the given operation
 * @param {string} apiType - provider API type
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

/**
 * Whether the operation streams with the current settings. Operations with forceStream always stream.
 * @param {Object|null} operation
 * @param {boolean} streamEnabled
 * @returns {boolean}
 */
export function isOperationStreaming(operation, streamEnabled) {
  if (!operation || !operation.supportsStreaming) return false;
  return operation.forceStream === true || streamEnabled === true;
}

/**
 * Resolve the path of an operation relative to the VMR base path, substituting {model}.
 * @param {string} apiType - VMR API type
 * @param {Object} operation - operation definition
 * @param {string} modelName - model name; the registry default for the type is used when empty
 * @param {boolean} streamEnabled - whether streaming is enabled
 * @returns {string} Path beginning with /
 */
export function resolveOperationPath(apiType, operation, modelName, streamEnabled) {
  if (!operation) return '';
  let endpointTemplate = operation.endpoint.startsWith('/') ? operation.endpoint : '/' + operation.endpoint;
  if (getProvider(apiType)?.streamFormat === 'gemini-sse' && operation.id === 'generateContent' && streamEnabled) {
    endpointTemplate = '/v1beta/models/{model}:streamGenerateContent?alt=sse';
  }
  const resolvedModel = encodeURIComponent(modelName || getProvider(apiType)?.explorerDefaultModel || 'model');
  return endpointTemplate.replace('{model}', resolvedModel);
}
