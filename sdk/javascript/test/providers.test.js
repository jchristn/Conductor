import test from 'node:test';
import assert from 'node:assert/strict';
import {
  ApiTypes,
  ApiTypeValues,
  ClientFacingApiTypes,
  ConductorClient,
  VmrEndpointCompatibility,
  acceptsEndpoint
} from '../src/index.js';

const BASE_URL = 'http://127.0.0.1:9000';

function createJsonResponse(payload, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => payload,
    text: async () => JSON.stringify(payload)
  };
}

function createRecordingClient(responder = () => createJsonResponse({})) {
  const captured = [];
  const client = new ConductorClient({
    baseUrl: BASE_URL,
    bearerToken: 'admin-token',
    fetchImpl: async (url, options = {}) => {
      captured.push({ url, method: options.method, headers: options.headers, body: options.body });
      return responder(url, options);
    }
  });
  return { client, captured };
}

test('ApiTypes lists the nine contract names with their integer values', () => {
  const expected = [
    ['Ollama', 0],
    ['OpenAI', 1],
    ['Gemini', 2],
    ['vLLM', 3],
    ['Anthropic', 4],
    ['TypeSafe', 5],
    ['Ollaya', 6],
    ['OllamaCloud', 7],
    ['OpenAICompatible', 8]
  ];

  assert.deepEqual(Object.keys(ApiTypes), expected.map(([name]) => name));
  for (const [name, value] of expected) {
    assert.equal(ApiTypes[name], name);
    assert.equal(ApiTypeValues[name], value);
  }
  assert.ok(Object.isFrozen(ApiTypes));
  assert.ok(Object.isFrozen(ApiTypeValues));
});

test('client-facing types and compatibility table match the contract', () => {
  assert.deepEqual([...ClientFacingApiTypes], ['Ollama', 'OpenAI', 'Gemini', 'vLLM', 'Anthropic', 'TypeSafe']);
  assert.deepEqual([...VmrEndpointCompatibility.Ollama], ['Ollama', 'OllamaCloud']);
  assert.deepEqual([...VmrEndpointCompatibility.OpenAI], ['OpenAI', 'OpenAICompatible', 'vLLM']);
  assert.deepEqual([...VmrEndpointCompatibility.vLLM], ['vLLM']);
  assert.deepEqual([...VmrEndpointCompatibility.Gemini], ['Gemini']);
  assert.deepEqual([...VmrEndpointCompatibility.Anthropic], ['Anthropic']);
  assert.deepEqual([...VmrEndpointCompatibility.TypeSafe], ['TypeSafe', 'Ollaya']);
  assert.equal(VmrEndpointCompatibility.Ollaya, undefined);
  assert.equal(acceptsEndpoint(ApiTypes.TypeSafe, ApiTypes.Ollaya), true);
  assert.equal(acceptsEndpoint(ApiTypes.Anthropic, ApiTypes.OpenAI), false);
  assert.equal(acceptsEndpoint(ApiTypes.OllamaCloud, ApiTypes.OllamaCloud), false);
});

test('revealModelRunnerEndpointApiKey calls the apikey route and returns the key', async () => {
  const { client, captured } = createRecordingClient(() => createJsonResponse({ ApiKey: 'sk-ant-full-secret-1234' }));

  const key = await client.revealModelRunnerEndpointApiKey('mre_123');
  const scoped = await client.revealModelRunnerEndpointApiKey('mre 456', 'ten_1');

  assert.equal(key, 'sk-ant-full-secret-1234');
  assert.equal(scoped, 'sk-ant-full-secret-1234');
  assert.equal(captured[0].method, 'GET');
  assert.equal(captured[0].url, 'http://127.0.0.1:9000/v1.0/modelrunnerendpoints/mre_123/apikey');
  assert.equal(captured[0].headers.Authorization, 'Bearer admin-token');
  assert.equal(captured[0].body, null);
  assert.equal(captured[1].url, 'http://127.0.0.1:9000/v1.0/modelrunnerendpoints/mre%20456/apikey?tenantId=ten_1');
});

test('revealModelRunnerEndpointApiKey returns null when no key is set and rejects a missing id', async () => {
  const { client } = createRecordingClient(() => createJsonResponse({ ApiKey: null }));

  assert.equal(await client.revealModelRunnerEndpointApiKey('mre_123'), null);
  await assert.rejects(() => client.revealModelRunnerEndpointApiKey(''), /id is required/);
});

test('createModelRunnerEndpoint passes Anthropic fields through and returns a masked key', async () => {
  const { client, captured } = createRecordingClient(() => createJsonResponse({
    Id: 'mre_1',
    ApiType: 'Anthropic',
    ApiKey: '****cdef',
    AnthropicVersion: '2023-06-01',
    HealthCheckEnabled: true
  }));

  const created = await client.createModelRunnerEndpoint({
    Name: 'Anthropic',
    Hostname: 'api.anthropic.com',
    Port: 443,
    UseSsl: true,
    ApiType: ApiTypes.Anthropic,
    ApiKey: 'sk-ant-abcdef',
    AnthropicVersion: '2023-06-01',
    HealthCheckEnabled: true,
    HealthCheckMethod: 'POST',
    HealthCheckRequestBody: '{"ping":true}'
  });

  assert.equal(captured[0].method, 'POST');
  assert.equal(captured[0].url, 'http://127.0.0.1:9000/v1.0/modelrunnerendpoints');
  const body = JSON.parse(captured[0].body);
  assert.equal(body.ApiType, 'Anthropic');
  assert.equal(body.AnthropicVersion, '2023-06-01');
  assert.equal(body.HealthCheckEnabled, true);
  assert.equal(body.HealthCheckMethod, 'POST');
  assert.equal(body.HealthCheckRequestBody, '{"ping":true}');
  assert.equal(created.ApiKey, '****cdef');
  assert.match(created.ApiKey, /^\*{4}.{4}$/);
});

test('model runner endpoint, model definition and VMR CRUD use expected routes', async () => {
  const { client, captured } = createRecordingClient((url, options) =>
    options.method === 'DELETE' ? createJsonResponse({}, 204) : createJsonResponse({ ok: true })
  );

  await client.listModelRunnerEndpoints({ tenantId: 'ten_1', maxResults: 10 });
  await client.getModelRunnerEndpoint('mre_1', 'ten_1');
  await client.updateModelRunnerEndpoint('mre_1', { ApiKey: '****cdef', HealthCheckEnabled: false });
  await client.deleteModelRunnerEndpoint('mre_1', 'ten_1');
  await client.createModelDefinition({ Name: 'jev-latest', SupportsCompletions: false, SupportsDecisions: true });
  await client.listModelDefinitions();
  await client.getModelDefinition('md_1');
  await client.updateModelDefinition('md_1', { SupportsDecisions: false });
  await client.deleteModelDefinition('md_1');
  await client.createVirtualModelRunner({
    Name: 'decisions',
    BasePath: '/v1.0/api/decisions/',
    ApiType: ApiTypes.TypeSafe,
    AllowDecisions: true,
    ModelRunnerEndpointIds: ['mre_1']
  });
  await client.listVirtualModelRunners({ nameFilter: 'dec' });
  await client.getVirtualModelRunner('vmr_1', 'ten_1');
  await client.updateVirtualModelRunner('vmr_1', { AllowDecisions: false });
  await client.deleteVirtualModelRunner('vmr_1', 'ten_1');

  const routes = captured.map((entry) => `${entry.method} ${entry.url.replace(BASE_URL, '')}`);
  assert.deepEqual(routes, [
    'GET /v1.0/modelrunnerendpoints?tenantId=ten_1&maxResults=10',
    'GET /v1.0/modelrunnerendpoints/mre_1?tenantId=ten_1',
    'PUT /v1.0/modelrunnerendpoints/mre_1',
    'DELETE /v1.0/modelrunnerendpoints/mre_1?tenantId=ten_1',
    'POST /v1.0/modeldefinitions',
    'GET /v1.0/modeldefinitions',
    'GET /v1.0/modeldefinitions/md_1',
    'PUT /v1.0/modeldefinitions/md_1',
    'DELETE /v1.0/modeldefinitions/md_1',
    'POST /v1.0/virtualmodelrunners',
    'GET /v1.0/virtualmodelrunners?nameFilter=dec',
    'GET /v1.0/virtualmodelrunners/vmr_1?tenantId=ten_1',
    'PUT /v1.0/virtualmodelrunners/vmr_1',
    'DELETE /v1.0/virtualmodelrunners/vmr_1?tenantId=ten_1'
  ]);
  assert.equal(JSON.parse(captured[2].body).HealthCheckEnabled, false);
  assert.equal(JSON.parse(captured[4].body).SupportsDecisions, true);
  assert.equal(JSON.parse(captured[9].body).ApiType, 'TypeSafe');
  assert.equal(JSON.parse(captured[9].body).AllowDecisions, true);
  assert.equal(JSON.parse(captured[12].body).AllowDecisions, false);
});
