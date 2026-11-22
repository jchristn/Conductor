/**
 * Provider registry: the single source of truth for every Conductor ApiType.
 *
 * Every dashboard surface that lists types, labels them, applies defaults, or changes
 * behavior by type reads this registry. Nothing here is inferred from hostnames, URLs or
 * response content: the operator picks a type, and the registry describes that type.
 *
 * Values and names follow the v1.1.0 contract (ApiTypeEnum). Values are appended, never renumbered.
 */

/** Default Anthropic API version sent when neither the client nor the endpoint sets one. */
export const DEFAULT_ANTHROPIC_VERSION = '2023-06-01';

/** Default TypeSafe decision model. */
export const DEFAULT_TYPESAFE_MODEL = 'jev-latest';

/**
 * TypeSafe health probe body: a one-question decision, matching PolyPrompt's
 * TypeSafeClient.ValidateConnectivityAsync. Each probe is a billed request on hosted TypeSafe.
 */
export const TYPESAFE_HEALTH_PROBE_BODY = '{"model":"jev-latest","state":"ping","questions":{"reachable":{"type":"noul","instructions":"Is this a test message?"}}}';

/** Probe kinds understood by the model load route (ModelLoadProbeKindEnum). */
export const PROBE_KINDS = ['Auto', 'MetadataOnly', 'ChatCompletion', 'Completion', 'Embeddings', 'NativeGenerate', 'Decision'];

/** Health check methods (HealthCheckMethodEnum). */
export const HEALTH_CHECK_METHODS = ['GET', 'HEAD', 'POST'];

const GENERATION_PROBES = ['ChatCompletion', 'Completion', 'Embeddings', 'NativeGenerate'];

const TYPESAFE_HEALTH_TOOLTIP = 'Each health check sends a real one-question decision to the hosted TypeSafe API, which is billed. '
  + 'That is why health checks are optional and off by default for TypeSafe. When enabled, they run every 60 seconds by default, '
  + 'and you can lower the interval if you accept the extra cost. When disabled, Conductor sends no probe and treats the endpoint as healthy.';

const DEFAULT_HEALTH_TOOLTIP = 'When enabled, Conductor probes this endpoint on the interval below and stops routing to it after the unhealthy threshold is reached. '
  + 'When disabled, Conductor sends no probe and treats the endpoint as healthy, so failures only surface on real requests.';

function decisionUnsupported(label) {
  return `${label} serves decisions only (System One /v1/systemone). Use Auto, MetadataOnly or Decision.`;
}

const ANTHROPIC_UNSUPPORTED_PROBE = 'Anthropic supports Auto, MetadataOnly and ChatCompletion probes only.';

/**
 * Registry entries keyed by ApiType name.
 *
 * Fields:
 * - value: ApiType name sent to and returned by the server.
 * - enumValue: integer value of ApiTypeEnum.
 * - label: display label.
 * - description: one-line description for selects and tooltips.
 * - clientFacing: true when the type can be a VMR type (the API clients call).
 * - wireApi: upstream wire API summary.
 * - acceptedEndpointTypes: endpoint types a VMR of this type accepts (empty when not client-facing).
 * - authStyle: bearer | bearer-optional | x-goog-api-key | x-api-key.
 * - authLabel: how the key is sent upstream.
 * - apiKeyRequired: true when validation rejects an endpoint without a key.
 * - streamFormat: openai-sse | gemini-sse | anthropic-sse | ndjson | none.
 * - capabilities: completions, embeddings, decisions, modelManagement, keepWarm.
 * - billableProbes: model load probe kinds that send billable traffic to a hosted provider.
 * - unsupportedProbes: probe kinds this type cannot run, mapped to the reason shown to the operator.
 * - explorerOperations: key of the API Explorer operation set (see requestTemplates.js).
 * - explorerDefaultModel: model name the API Explorer suggests when no definition is attached.
 * - explorerHeaders: extra request headers the API Explorer sends for this VMR type.
 * - sdk: official client SDK snippet family (openai | anthropic | null).
 * - badgeClass: CSS modifier for .api-type-badge.
 * - endpointDefaults: fields applied to the endpoint form when the operator selects this type.
 * - healthTooltip: tooltip for the "Health checks enabled" toggle.
 */
const REGISTRY = {
  Ollama: {
    value: 'Ollama',
    enumValue: 0,
    label: 'Ollama',
    description: 'Self-hosted Ollama runner (/api/*). Supports model management.',
    clientFacing: true,
    wireApi: 'Ollama /api/*',
    acceptedEndpointTypes: ['Ollama', 'OllamaCloud'],
    authStyle: 'bearer-optional',
    authLabel: 'Authorization: Bearer, only when a key is set',
    apiKeyRequired: false,
    streamFormat: 'ndjson',
    capabilities: { completions: true, embeddings: true, decisions: false, modelManagement: true, keepWarm: true },
    billableProbes: [],
    unsupportedProbes: { Decision: 'Decision probes apply to TypeSafe and Ollaya only.' },
    explorerOperations: 'ollama',
    explorerDefaultModel: '',
    explorerHeaders: {},
    sdk: null,
    badgeClass: 'ollama',
    endpointDefaults: {
      Hostname: 'localhost',
      Port: 11434,
      UseSsl: false,
      HealthCheckUrl: '/',
      HealthCheckMethod: 'GET',
      HealthCheckUseAuth: false,
      HealthCheckEnabled: true,
      HealthCheckIntervalMs: 5000
    },
    healthTooltip: DEFAULT_HEALTH_TOOLTIP
  },
  OpenAI: {
    value: 'OpenAI',
    enumValue: 1,
    label: 'OpenAI',
    description: 'Hosted OpenAI API (/v1/*).',
    clientFacing: true,
    wireApi: 'OpenAI /v1/*',
    acceptedEndpointTypes: ['OpenAI', 'OpenAICompatible', 'vLLM'],
    authStyle: 'bearer',
    authLabel: 'Authorization: Bearer',
    apiKeyRequired: false,
    streamFormat: 'openai-sse',
    capabilities: { completions: true, embeddings: true, decisions: false, modelManagement: false, keepWarm: false },
    billableProbes: GENERATION_PROBES,
    unsupportedProbes: { Decision: 'Decision probes apply to TypeSafe and Ollaya only.' },
    explorerOperations: 'openai',
    explorerDefaultModel: '',
    explorerHeaders: {},
    sdk: 'openai',
    badgeClass: 'openai',
    endpointDefaults: {
      Hostname: 'api.openai.com',
      Port: 443,
      UseSsl: true,
      HealthCheckUrl: '/v1/models',
      HealthCheckMethod: 'GET',
      HealthCheckUseAuth: true,
      HealthCheckEnabled: true,
      HealthCheckIntervalMs: 5000
    },
    healthTooltip: DEFAULT_HEALTH_TOOLTIP
  },
  Gemini: {
    value: 'Gemini',
    enumValue: 2,
    label: 'Gemini',
    description: 'Google Gemini API (/v1beta/*).',
    clientFacing: true,
    wireApi: 'Gemini /v1beta/*',
    acceptedEndpointTypes: ['Gemini'],
    authStyle: 'x-goog-api-key',
    authLabel: 'x-goog-api-key header',
    apiKeyRequired: false,
    streamFormat: 'gemini-sse',
    capabilities: { completions: true, embeddings: true, decisions: false, modelManagement: false, keepWarm: false },
    billableProbes: GENERATION_PROBES,
    unsupportedProbes: { Decision: 'Decision probes apply to TypeSafe and Ollaya only.' },
    explorerOperations: 'gemini',
    explorerDefaultModel: 'gemini-2.5-flash',
    explorerHeaders: {},
    sdk: null,
    badgeClass: 'gemini',
    endpointDefaults: {
      Hostname: 'generativelanguage.googleapis.com',
      Port: 443,
      UseSsl: true,
      HealthCheckUrl: '/v1beta/models',
      HealthCheckMethod: 'GET',
      HealthCheckUseAuth: true,
      HealthCheckEnabled: true,
      HealthCheckIntervalMs: 5000
    },
    healthTooltip: DEFAULT_HEALTH_TOOLTIP
  },
  vLLM: {
    value: 'vLLM',
    enumValue: 3,
    label: 'vLLM',
    description: 'vLLM server speaking the OpenAI API (/v1/*).',
    clientFacing: true,
    wireApi: 'OpenAI /v1/*',
    acceptedEndpointTypes: ['vLLM'],
    authStyle: 'bearer-optional',
    authLabel: 'Authorization: Bearer, only when a key is set',
    apiKeyRequired: false,
    streamFormat: 'openai-sse',
    capabilities: { completions: true, embeddings: true, decisions: false, modelManagement: false, keepWarm: false },
    billableProbes: [],
    unsupportedProbes: { Decision: 'Decision probes apply to TypeSafe and Ollaya only.' },
    explorerOperations: 'openai',
    explorerDefaultModel: '',
    explorerHeaders: {},
    sdk: 'openai',
    badgeClass: 'vllm',
    // vLLM never had host or port defaults in the dashboard; the operator supplies them.
    endpointDefaults: {
      HealthCheckUrl: '/',
      HealthCheckMethod: 'GET',
      HealthCheckUseAuth: false,
      HealthCheckEnabled: true,
      HealthCheckIntervalMs: 5000
    },
    healthTooltip: DEFAULT_HEALTH_TOOLTIP
  },
  Anthropic: {
    value: 'Anthropic',
    enumValue: 4,
    label: 'Anthropic',
    description: 'Anthropic Messages API (/v1/messages). Works with Claude Code and the Anthropic SDKs.',
    clientFacing: true,
    wireApi: 'Anthropic /v1/*',
    acceptedEndpointTypes: ['Anthropic'],
    authStyle: 'x-api-key',
    authLabel: 'x-api-key and anthropic-version headers',
    apiKeyRequired: true,
    streamFormat: 'anthropic-sse',
    capabilities: { completions: true, embeddings: false, decisions: false, modelManagement: false, keepWarm: false },
    billableProbes: ['ChatCompletion'],
    unsupportedProbes: {
      Completion: ANTHROPIC_UNSUPPORTED_PROBE,
      Embeddings: 'Anthropic has no embeddings API, so an Embeddings probe cannot run. Use MetadataOnly to verify the model without billable traffic.',
      NativeGenerate: ANTHROPIC_UNSUPPORTED_PROBE,
      Decision: 'Decision probes apply to TypeSafe and Ollaya only.'
    },
    explorerOperations: 'anthropic',
    explorerDefaultModel: 'claude-opus-5-5',
    explorerHeaders: { 'anthropic-version': DEFAULT_ANTHROPIC_VERSION },
    sdk: 'anthropic',
    badgeClass: 'anthropic',
    endpointDefaults: {
      Hostname: 'api.anthropic.com',
      Port: 443,
      UseSsl: true,
      HealthCheckUrl: '/v1/models?limit=1',
      HealthCheckMethod: 'GET',
      HealthCheckUseAuth: true,
      HealthCheckEnabled: true,
      HealthCheckIntervalMs: 60000
    },
    healthTooltip: 'Anthropic health checks call GET /v1/models?limit=1 with your key every 60 seconds by default. Listing models is not billed. '
      + 'When disabled, Conductor sends no probe and treats the endpoint as healthy.'
  },
  TypeSafe: {
    value: 'TypeSafe',
    enumValue: 5,
    label: 'TypeSafe',
    description: 'Hosted TypeSafe System One decision API (/v1/systemone).',
    clientFacing: true,
    wireApi: 'System One /v1/*',
    acceptedEndpointTypes: ['TypeSafe', 'Ollaya'],
    authStyle: 'bearer',
    authLabel: 'Authorization: Bearer (required)',
    apiKeyRequired: true,
    streamFormat: 'none',
    capabilities: { completions: false, embeddings: false, decisions: true, modelManagement: false, keepWarm: false },
    billableProbes: ['Auto', 'Decision'],
    unsupportedProbes: {
      ChatCompletion: decisionUnsupported('TypeSafe'),
      Completion: decisionUnsupported('TypeSafe'),
      Embeddings: decisionUnsupported('TypeSafe'),
      NativeGenerate: decisionUnsupported('TypeSafe')
    },
    explorerOperations: 'typesafe',
    explorerDefaultModel: DEFAULT_TYPESAFE_MODEL,
    explorerHeaders: {},
    sdk: null,
    badgeClass: 'typesafe',
    endpointDefaults: {
      Hostname: 'api.typesafe.ai',
      Port: 443,
      UseSsl: true,
      HealthCheckUrl: '/v1/systemone',
      HealthCheckMethod: 'POST',
      HealthCheckRequestBody: TYPESAFE_HEALTH_PROBE_BODY,
      HealthCheckUseAuth: true,
      HealthCheckEnabled: false,
      HealthCheckIntervalMs: 60000
    },
    healthTooltip: TYPESAFE_HEALTH_TOOLTIP
  },
  Ollaya: {
    value: 'Ollaya',
    enumValue: 6,
    label: 'Ollaya',
    description: 'Local Ollaya runtime serving the System One decision API. Endpoint only; attach it to a TypeSafe VMR.',
    clientFacing: false,
    wireApi: 'System One /v1/*',
    acceptedEndpointTypes: [],
    authStyle: 'bearer-optional',
    authLabel: 'Authorization: Bearer, only when a key is set',
    apiKeyRequired: false,
    streamFormat: 'none',
    capabilities: { completions: false, embeddings: false, decisions: true, modelManagement: false, keepWarm: false },
    billableProbes: [],
    unsupportedProbes: {
      ChatCompletion: decisionUnsupported('Ollaya'),
      Completion: decisionUnsupported('Ollaya'),
      Embeddings: decisionUnsupported('Ollaya'),
      NativeGenerate: decisionUnsupported('Ollaya')
    },
    explorerOperations: 'typesafe',
    explorerDefaultModel: DEFAULT_TYPESAFE_MODEL,
    explorerHeaders: {},
    sdk: null,
    badgeClass: 'ollaya',
    endpointDefaults: {
      Hostname: '127.0.0.1',
      Port: '',
      UseSsl: false,
      HealthCheckUrl: '/v1/models',
      HealthCheckMethod: 'GET',
      HealthCheckUseAuth: false,
      HealthCheckEnabled: true,
      HealthCheckIntervalMs: 5000
    },
    healthTooltip: DEFAULT_HEALTH_TOOLTIP
  },
  OllamaCloud: {
    value: 'OllamaCloud',
    enumValue: 7,
    label: 'Ollama Cloud',
    description: 'Hosted Ollama Cloud (ollama.com, /api/*). Endpoint only; attach it to an Ollama VMR. No model management or keep-warm.',
    clientFacing: false,
    wireApi: 'Ollama /api/*',
    acceptedEndpointTypes: [],
    authStyle: 'bearer',
    authLabel: 'Authorization: Bearer (required)',
    apiKeyRequired: true,
    streamFormat: 'ndjson',
    capabilities: { completions: true, embeddings: true, decisions: false, modelManagement: false, keepWarm: false },
    billableProbes: ['Auto', ...GENERATION_PROBES],
    unsupportedProbes: { Decision: 'Decision probes apply to TypeSafe and Ollaya only.' },
    explorerOperations: 'ollama',
    explorerDefaultModel: '',
    explorerHeaders: {},
    sdk: null,
    badgeClass: 'ollamacloud',
    endpointDefaults: {
      Hostname: 'ollama.com',
      Port: 443,
      UseSsl: true,
      HealthCheckUrl: '/api/tags',
      HealthCheckMethod: 'GET',
      HealthCheckUseAuth: true,
      HealthCheckEnabled: true,
      HealthCheckIntervalMs: 60000
    },
    healthTooltip: DEFAULT_HEALTH_TOOLTIP
  },
  OpenAICompatible: {
    value: 'OpenAICompatible',
    enumValue: 8,
    label: 'OpenAI-compatible',
    description: 'Any server speaking the OpenAI API, such as LM Studio or llama.cpp. Endpoint only; attach it to an OpenAI VMR.',
    clientFacing: false,
    wireApi: 'OpenAI /v1/*',
    acceptedEndpointTypes: [],
    authStyle: 'bearer-optional',
    authLabel: 'Authorization: Bearer, only when a key is set',
    apiKeyRequired: false,
    streamFormat: 'openai-sse',
    capabilities: { completions: true, embeddings: true, decisions: false, modelManagement: false, keepWarm: false },
    billableProbes: [],
    unsupportedProbes: { Decision: 'Decision probes apply to TypeSafe and Ollaya only.' },
    explorerOperations: 'openai',
    explorerDefaultModel: '',
    explorerHeaders: {},
    sdk: 'openai',
    badgeClass: 'openaicompatible',
    endpointDefaults: {
      Hostname: '127.0.0.1',
      Port: '',
      UseSsl: false,
      HealthCheckUrl: '/v1/models',
      HealthCheckMethod: 'GET',
      HealthCheckUseAuth: false,
      HealthCheckEnabled: true,
      HealthCheckIntervalMs: 5000
    },
    healthTooltip: DEFAULT_HEALTH_TOOLTIP
  }
};

/** Every ApiType name in enum order. */
export const API_TYPES = Object.values(REGISTRY)
  .sort((a, b) => a.enumValue - b.enumValue)
  .map((entry) => entry.value);

/** ApiType names a VMR may use, in enum order. */
export const CLIENT_FACING_API_TYPES = API_TYPES.filter((type) => REGISTRY[type].clientFacing);

/**
 * Resolve an ApiType from a name or enum integer.
 * @param {string|number|null|undefined} apiType
 * @returns {string|null} The ApiType name, or null when unknown.
 */
export function normalizeApiType(apiType) {
  if (typeof apiType === 'number') {
    const match = Object.values(REGISTRY).find((entry) => entry.enumValue === apiType);
    return match ? match.value : null;
  }
  if (typeof apiType !== 'string' || !apiType) return null;
  if (REGISTRY[apiType]) return apiType;
  const lower = apiType.toLowerCase();
  const match = API_TYPES.find((type) => type.toLowerCase() === lower);
  return match || null;
}

/**
 * Get the registry entry for an ApiType.
 * @param {string|number} apiType
 * @returns {Object|null}
 */
export function getProvider(apiType) {
  const name = normalizeApiType(apiType);
  return name ? REGISTRY[name] : null;
}

/** @returns {Array<Object>} Every registry entry in enum order. */
export function listProviders() {
  return API_TYPES.map((type) => REGISTRY[type]);
}

/** @returns {Array<Object>} Client-facing registry entries in enum order. */
export function listClientFacingProviders() {
  return CLIENT_FACING_API_TYPES.map((type) => REGISTRY[type]);
}

/**
 * Display label for an ApiType. Unknown values are returned as given.
 * @param {string|number} apiType
 * @returns {string}
 */
export function getProviderLabel(apiType) {
  const provider = getProvider(apiType);
  if (provider) return provider.label;
  return apiType === null || apiType === undefined || apiType === '' ? 'Unknown' : String(apiType);
}

/**
 * Comma-separated list of provider labels for copy text, for example "Ollama, OpenAI, and Gemini".
 * @param {Object} [options]
 * @param {boolean} [options.clientFacingOnly=false]
 * @returns {string}
 */
export function formatProviderList({ clientFacingOnly = false } = {}) {
  const labels = (clientFacingOnly ? listClientFacingProviders() : listProviders()).map((entry) => entry.label);
  if (labels.length < 2) return labels.join('');
  return `${labels.slice(0, -1).join(', ')}, and ${labels[labels.length - 1]}`;
}

/** @returns {boolean} True when the type can be a VMR type. */
export function isClientFacing(apiType) {
  return getProvider(apiType)?.clientFacing === true;
}

/**
 * Endpoint types accepted by a VMR type (the compatibility table).
 * @param {string} vmrApiType
 * @returns {string[]}
 */
export function getAcceptedEndpointTypes(vmrApiType) {
  return [...(getProvider(vmrApiType)?.acceptedEndpointTypes || [])];
}

/**
 * Whether a VMR of one type accepts an endpoint of another.
 * @param {string} vmrApiType
 * @param {string} endpointApiType
 * @returns {boolean}
 */
export function acceptsEndpointType(vmrApiType, endpointApiType) {
  const endpointType = normalizeApiType(endpointApiType);
  return endpointType !== null && getAcceptedEndpointTypes(vmrApiType).includes(endpointType);
}

/**
 * Endpoint form defaults for a type. Returns a fresh copy; fields not listed are left to the operator.
 * @param {string} apiType
 * @returns {Object}
 */
export function getEndpointDefaults(apiType) {
  const provider = getProvider(apiType);
  if (!provider) return {};
  return {
    TimeoutMs: 300000,
    HealthCheckExpectedStatusCode: 200,
    HealthCheckRequestBody: '',
    ...provider.endpointDefaults
  };
}

/** @returns {string} Stream format of the type, or 'none'. */
export function getStreamFormat(apiType) {
  return getProvider(apiType)?.streamFormat || 'none';
}

/**
 * Whether a type has a capability.
 * @param {string} apiType
 * @param {'completions'|'embeddings'|'decisions'|'modelManagement'|'keepWarm'} capability
 * @returns {boolean}
 */
export function hasCapability(apiType, capability) {
  return getProvider(apiType)?.capabilities?.[capability] === true;
}

/**
 * Whether a model load probe of this kind sends billable traffic for this type.
 * @param {string} apiType
 * @param {string} probeKind
 * @returns {boolean}
 */
export function isProbeBillable(apiType, probeKind) {
  return (getProvider(apiType)?.billableProbes || []).includes(probeKind);
}

/**
 * Reason a probe kind cannot run against this type, or an empty string when it can.
 * @param {string} apiType
 * @param {string} probeKind
 * @returns {string}
 */
export function getUnsupportedProbeReason(apiType, probeKind) {
  return getProvider(apiType)?.unsupportedProbes?.[probeKind] || '';
}

/** @returns {string} CSS class list for an ApiType badge. */
export function getApiTypeBadgeClass(apiType) {
  const provider = getProvider(apiType);
  return `api-type-badge ${provider ? provider.badgeClass : 'unknown'}`;
}

/** @returns {string|null} SDK snippet family for the type (openai, anthropic) or null. */
export function getProviderSdk(apiType) {
  return getProvider(apiType)?.sdk || null;
}

/**
 * Request headers the API Explorer sends to a VMR of this type. Used by the request and by
 * every generated snippet so the two always match.
 * @param {string} apiType - VMR ApiType
 * @param {string|null} apiKey - Conductor credential bearer token, or null
 * @returns {Object<string, string>}
 */
export function buildExplorerHeaders(apiType, apiKey) {
  const headers = { 'Content-Type': 'application/json' };
  if (apiKey) {
    headers['Authorization'] = `Bearer ${apiKey}`;
  }
  return { ...headers, ...(getProvider(apiType)?.explorerHeaders || {}) };
}

/** @returns {string} Tooltip for the "Health checks enabled" toggle of this type. */
export function getHealthCheckTooltip(apiType) {
  return getProvider(apiType)?.healthTooltip || DEFAULT_HEALTH_TOOLTIP;
}

/**
 * Validate an Anthropic version string (yyyy-MM-dd and a real calendar date).
 * @param {string} value
 * @returns {boolean}
 */
export function isValidAnthropicVersion(value) {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [year, month, day] = value.split('-').map((part) => parseInt(part, 10));
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

export default REGISTRY;
