/**
 * Upstream API type of a model runner endpoint, or the client-facing API type of
 * a virtual model runner. Values are the wire names the server accepts and returns.
 */
export const ApiTypes = Object.freeze({
  Ollama: 'Ollama',
  OpenAI: 'OpenAI',
  Gemini: 'Gemini',
  vLLM: 'vLLM',
  Anthropic: 'Anthropic',
  TypeSafe: 'TypeSafe',
  Ollaya: 'Ollaya',
  OllamaCloud: 'OllamaCloud',
  OpenAICompatible: 'OpenAICompatible'
});

/**
 * Integer value of each API type, matching the server's ApiTypeEnum.
 * Values are appended and never renumbered.
 */
export const ApiTypeValues = Object.freeze({
  Ollama: 0,
  OpenAI: 1,
  Gemini: 2,
  vLLM: 3,
  Anthropic: 4,
  TypeSafe: 5,
  Ollaya: 6,
  OllamaCloud: 7,
  OpenAICompatible: 8
});

/**
 * Endpoint API types accepted by each client-facing virtual model runner type.
 * Ollaya, OllamaCloud and OpenAICompatible are endpoint-only types.
 */
export const VmrEndpointCompatibility = Object.freeze({
  Ollama: Object.freeze(['Ollama', 'OllamaCloud']),
  OpenAI: Object.freeze(['OpenAI', 'OpenAICompatible', 'vLLM']),
  Gemini: Object.freeze(['Gemini']),
  vLLM: Object.freeze(['vLLM']),
  Anthropic: Object.freeze(['Anthropic']),
  TypeSafe: Object.freeze(['TypeSafe', 'Ollaya'])
});

/**
 * API types that may be used as a virtual model runner type, in enum order.
 */
export const ClientFacingApiTypes = Object.freeze(
  Object.keys(VmrEndpointCompatibility).sort((a, b) => ApiTypeValues[a] - ApiTypeValues[b])
);

/**
 * Returns true when a virtual model runner of type vmrApiType accepts an endpoint of type endpointApiType.
 */
export function acceptsEndpoint(vmrApiType, endpointApiType) {
  const accepted = VmrEndpointCompatibility[vmrApiType];
  return Array.isArray(accepted) && accepted.includes(endpointApiType);
}
