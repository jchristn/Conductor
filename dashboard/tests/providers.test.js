import { describe, it, expect } from 'vitest';
import REGISTRY, {
  API_TYPES,
  CLIENT_FACING_API_TYPES,
  TYPESAFE_HEALTH_PROBE_BODY,
  acceptsEndpointType,
  buildExplorerHeaders,
  getAcceptedEndpointTypes,
  getEndpointDefaults,
  getHealthCheckTooltip,
  getProvider,
  getUnsupportedProbeReason,
  hasCapability,
  isClientFacing,
  isProbeBillable,
  isValidAnthropicVersion,
  normalizeApiType
} from '../src/utils/providers';

// Binding contract: ApiTypeEnum values and names.
const CONTRACT_TYPES = [
  ['Ollama', 0, true],
  ['OpenAI', 1, true],
  ['Gemini', 2, true],
  ['vLLM', 3, true],
  ['Anthropic', 4, true],
  ['TypeSafe', 5, true],
  ['Ollaya', 6, false],
  ['OllamaCloud', 7, false],
  ['OpenAICompatible', 8, false]
];

// Binding contract: VMR-to-endpoint compatibility.
const CONTRACT_COMPATIBILITY = {
  Ollama: ['Ollama', 'OllamaCloud'],
  OpenAI: ['OpenAI', 'OpenAICompatible', 'vLLM'],
  vLLM: ['vLLM'],
  Gemini: ['Gemini'],
  Anthropic: ['Anthropic'],
  TypeSafe: ['TypeSafe', 'Ollaya']
};

const STREAM_FORMATS = ['openai-sse', 'gemini-sse', 'anthropic-sse', 'ndjson', 'none'];

describe('provider registry', () => {
  it('lists all nine contract types in enum order with matching values and client-facing flags', () => {
    expect(API_TYPES).toEqual(CONTRACT_TYPES.map(([name]) => name));
    CONTRACT_TYPES.forEach(([name, value, clientFacing]) => {
      const provider = getProvider(name);
      expect(provider.value).toBe(name);
      expect(provider.enumValue).toBe(value);
      expect(provider.clientFacing).toBe(clientFacing);
      expect(isClientFacing(name)).toBe(clientFacing);
      expect(getProvider(value).value).toBe(name);
    });
    expect(CLIENT_FACING_API_TYPES).toEqual(['Ollama', 'OpenAI', 'Gemini', 'vLLM', 'Anthropic', 'TypeSafe']);
  });

  it('gives every type a complete entry', () => {
    Object.values(REGISTRY).forEach((provider) => {
      expect(provider.label).toBeTruthy();
      expect(provider.description).toBeTruthy();
      expect(STREAM_FORMATS).toContain(provider.streamFormat);
      expect(provider.authStyle).toBeTruthy();
      expect(provider.authLabel).toBeTruthy();
      expect(typeof provider.capabilities.completions).toBe('boolean');
      expect(typeof provider.capabilities.embeddings).toBe('boolean');
      expect(typeof provider.capabilities.decisions).toBe('boolean');
      expect(typeof provider.capabilities.modelManagement).toBe('boolean');
      expect(Array.isArray(provider.billableProbes)).toBe(true);
      expect(provider.explorerOperations).toBeTruthy();
      expect(provider.badgeClass).toMatch(/^[a-z]+$/);
      expect(provider.endpointDefaults.HealthCheckMethod).toBeTruthy();
      expect(typeof provider.endpointDefaults.HealthCheckEnabled).toBe('boolean');
      expect(provider.endpointDefaults.HealthCheckIntervalMs).toBeGreaterThan(0);
    });
  });

  it('matches the VMR-to-endpoint compatibility table exactly', () => {
    CONTRACT_TYPES.forEach(([vmrType]) => {
      expect(getAcceptedEndpointTypes(vmrType).sort()).toEqual([...(CONTRACT_COMPATIBILITY[vmrType] || [])].sort());
      CONTRACT_TYPES.forEach(([endpointType]) => {
        expect(acceptsEndpointType(vmrType, endpointType)).toBe((CONTRACT_COMPATIBILITY[vmrType] || []).includes(endpointType));
      });
    });
  });

  it('applies the contract type defaults for the new types', () => {
    expect(getEndpointDefaults('Anthropic')).toMatchObject({
      Hostname: 'api.anthropic.com', Port: 443, UseSsl: true,
      HealthCheckMethod: 'GET', HealthCheckUrl: '/v1/models?limit=1', HealthCheckUseAuth: true,
      HealthCheckEnabled: true, HealthCheckIntervalMs: 60000
    });
    expect(getEndpointDefaults('TypeSafe')).toMatchObject({
      Hostname: 'api.typesafe.ai', Port: 443, UseSsl: true,
      HealthCheckMethod: 'POST', HealthCheckUrl: '/v1/systemone', HealthCheckUseAuth: true,
      HealthCheckEnabled: false, HealthCheckIntervalMs: 60000,
      HealthCheckRequestBody: TYPESAFE_HEALTH_PROBE_BODY
    });
    expect(JSON.parse(TYPESAFE_HEALTH_PROBE_BODY)).toEqual({
      model: 'jev-latest', state: 'ping', questions: { reachable: { type: 'noul', instructions: 'Is this a test message?' } }
    });
    expect(getEndpointDefaults('Ollaya')).toMatchObject({
      Hostname: '127.0.0.1', Port: '', UseSsl: false,
      HealthCheckMethod: 'GET', HealthCheckUrl: '/v1/models', HealthCheckUseAuth: false,
      HealthCheckEnabled: true, HealthCheckIntervalMs: 5000
    });
    expect(getEndpointDefaults('OllamaCloud')).toMatchObject({
      Hostname: 'ollama.com', Port: 443, UseSsl: true,
      HealthCheckMethod: 'GET', HealthCheckUrl: '/api/tags', HealthCheckUseAuth: true,
      HealthCheckEnabled: true, HealthCheckIntervalMs: 60000
    });
    expect(getEndpointDefaults('OpenAICompatible')).toMatchObject({
      Hostname: '127.0.0.1', Port: '', UseSsl: false,
      HealthCheckMethod: 'GET', HealthCheckUrl: '/v1/models', HealthCheckUseAuth: false,
      HealthCheckEnabled: true, HealthCheckIntervalMs: 5000
    });
  });

  it('keeps the existing dashboard defaults for the original types', () => {
    expect(getEndpointDefaults('OpenAI')).toMatchObject({ Hostname: 'api.openai.com', Port: 443, UseSsl: true, HealthCheckUrl: '/v1/models', HealthCheckUseAuth: true });
    expect(getEndpointDefaults('Gemini')).toMatchObject({ Hostname: 'generativelanguage.googleapis.com', Port: 443, UseSsl: true, HealthCheckUrl: '/v1beta/models', HealthCheckUseAuth: true });
    expect(getEndpointDefaults('Ollama')).toMatchObject({ Hostname: 'localhost', Port: 11434, UseSsl: false, HealthCheckUrl: '/', HealthCheckUseAuth: false });
    expect(getEndpointDefaults('vLLM')).not.toHaveProperty('Hostname');
  });

  it('flags billable probes per the contract', () => {
    expect(isProbeBillable('OpenAI', 'ChatCompletion')).toBe(true);
    expect(isProbeBillable('OpenAI', 'MetadataOnly')).toBe(false);
    expect(isProbeBillable('Gemini', 'Embeddings')).toBe(true);
    expect(isProbeBillable('Anthropic', 'ChatCompletion')).toBe(true);
    expect(isProbeBillable('Anthropic', 'Auto')).toBe(false);
    expect(isProbeBillable('TypeSafe', 'Auto')).toBe(true);
    expect(isProbeBillable('TypeSafe', 'Decision')).toBe(true);
    expect(isProbeBillable('Ollaya', 'Auto')).toBe(false);
    expect(isProbeBillable('OllamaCloud', 'Auto')).toBe(true);
    expect(isProbeBillable('OpenAICompatible', 'ChatCompletion')).toBe(false);
    expect(isProbeBillable('Ollama', 'NativeGenerate')).toBe(false);
  });

  it('marks unsupported probes and capabilities', () => {
    expect(getUnsupportedProbeReason('Anthropic', 'Embeddings')).toMatch(/no embeddings API/);
    expect(getUnsupportedProbeReason('Anthropic', 'ChatCompletion')).toBe('');
    expect(getUnsupportedProbeReason('TypeSafe', 'Decision')).toBe('');
    expect(getUnsupportedProbeReason('OpenAI', 'Decision')).not.toBe('');
    expect(hasCapability('Ollama', 'modelManagement')).toBe(true);
    expect(hasCapability('OllamaCloud', 'modelManagement')).toBe(false);
    expect(hasCapability('Anthropic', 'embeddings')).toBe(false);
    expect(hasCapability('TypeSafe', 'decisions')).toBe(true);
    expect(hasCapability('Ollaya', 'decisions')).toBe(true);
  });

  it('builds explorer headers with Bearer auth and anthropic-version for Anthropic', () => {
    expect(buildExplorerHeaders('OpenAI', 'tok')).toEqual({ 'Content-Type': 'application/json', Authorization: 'Bearer tok' });
    expect(buildExplorerHeaders('Anthropic', 'tok')).toEqual({ 'Content-Type': 'application/json', Authorization: 'Bearer tok', 'anthropic-version': '2023-06-01' });
    expect(buildExplorerHeaders('TypeSafe', null)).toEqual({ 'Content-Type': 'application/json' });
  });

  it('explains why TypeSafe health checks are off by default', () => {
    const tooltip = getHealthCheckTooltip('TypeSafe');
    expect(tooltip).toMatch(/one-question decision/);
    expect(tooltip).toMatch(/billed/);
    expect(tooltip).toMatch(/60 seconds/);
    expect(tooltip).toMatch(/lower the interval/);
    expect(tooltip).toMatch(/treats the endpoint as healthy/);
  });

  it('validates Anthropic versions and normalizes type names without guessing', () => {
    expect(isValidAnthropicVersion('2023-06-01')).toBe(true);
    expect(isValidAnthropicVersion('2023-02-30')).toBe(false);
    expect(isValidAnthropicVersion('2023-6-1')).toBe(false);
    expect(normalizeApiType('anthropic')).toBe('Anthropic');
    expect(normalizeApiType('api.anthropic.com')).toBeNull();
    expect(normalizeApiType(42)).toBeNull();
  });
});
