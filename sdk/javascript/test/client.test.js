import test from 'node:test';
import assert from 'node:assert/strict';
import { ConductorClient } from '../src/index.js';

function createJsonResponse(payload, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => payload,
    text: async () => JSON.stringify(payload)
  };
}

test('builds validation request with existingId query', async () => {
  let capturedUrl = '';
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    bearerToken: 'token',
    fetchImpl: async (url) => {
      capturedUrl = url;
      return createJsonResponse({ IsValid: true });
    }
  });

  const result = await client.validateVirtualModelRunner({ Name: 'Test' }, 'vmr_123');

  assert.equal(result.IsValid, true);
  assert.equal(capturedUrl, 'http://localhost:9000/v1.0/virtualmodelrunners/validate?existingId=vmr_123');
});

test('builds request-history search query string', async () => {
  let capturedUrl = '';
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url) => {
      capturedUrl = url;
      return createJsonResponse({ Data: [] });
    }
  });

  await client.searchRequestHistory({
    vmrGuid: 'vmr_1',
    statusClass: '5xx',
    modelName: 'gpt-4o-mini'
  });

  assert.equal(
    capturedUrl,
    'http://localhost:9000/v1.0/requesthistory?vmrGuid=vmr_1&statusClass=5xx&modelName=gpt-4o-mini'
  );
});

test('returns raw text for the observability metrics endpoint', async () => {
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async () => ({
      ok: true,
      status: 200,
      json: async () => ({}),
      text: async () => 'conductor_requests_total 42'
    })
  });

  const result = await client.getObservabilityMetricsText();
  assert.equal(result, 'conductor_requests_total 42');
});
