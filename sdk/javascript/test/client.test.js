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

test('builds request analytics overview query string', async () => {
  let capturedUrl = '';
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url) => {
      capturedUrl = url;
      return createJsonResponse({ TotalRequests: 0 });
    }
  });

  await client.getRequestAnalyticsOverview({
    range: 'lastWeek',
    vmrGuid: 'vmr_1',
    providerName: 'Ollama'
  });

  assert.equal(
    capturedUrl,
    'http://localhost:9000/v1.0/requesthistory/analytics/overview?range=lastWeek&vmrGuid=vmr_1&providerName=Ollama'
  );
});

test('builds request history analytics detail URL', async () => {
  let capturedUrl = '';
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url) => {
      capturedUrl = url;
      return createJsonResponse({ Events: [] });
    }
  });

  await client.getRequestHistoryAnalytics('req_123');

  assert.equal(capturedUrl, 'http://localhost:9000/v1.0/requesthistory/req_123/analytics');
});

test('builds endpoint model load request with tenant query and body', async () => {
  let capturedUrl = '';
  let capturedOptions = null;
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url, options) => {
      capturedUrl = url;
      capturedOptions = options;
      return createJsonResponse({ Success: true, OutcomeCode: 'Loaded' });
    }
  });

  const payload = { Model: 'gemma3:4b', ProbeKind: 'Auto' };
  const result = await client.loadModelRunnerEndpointModel('mre_123', payload, 'ten_123');

  assert.equal(result.OutcomeCode, 'Loaded');
  assert.equal(capturedUrl, 'http://localhost:9000/v1.0/modelrunnerendpoints/mre_123/load-model?tenantId=ten_123');
  assert.equal(capturedOptions.method, 'POST');
  assert.equal(capturedOptions.body, JSON.stringify(payload));
});

test('builds ollama endpoint model management requests', async () => {
  const captured = [];
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url, options) => {
      captured.push({ url, options });
      return createJsonResponse({ Success: true, Models: [] });
    }
  });

  const pullPayload = { Model: 'llama3.2:latest', TimeoutMs: 1800000 };
  const deletePayload = { Model: 'llama3.2:latest' };

  await client.listOllamaEndpointModels('mre_123', 'ten_123');
  await client.pullOllamaEndpointModel('mre_123', pullPayload, 'ten_123');
  await client.deleteOllamaEndpointModel('mre_123', deletePayload, 'ten_123');

  assert.equal(captured[0].url, 'http://localhost:9000/v1.0/modelrunnerendpoints/mre_123/ollama/models?tenantId=ten_123');
  assert.equal(captured[0].options.method, 'GET');
  assert.equal(captured[1].url, 'http://localhost:9000/v1.0/modelrunnerendpoints/mre_123/ollama/models/pull?tenantId=ten_123');
  assert.equal(captured[1].options.method, 'POST');
  assert.equal(captured[1].options.body, JSON.stringify(pullPayload));
  assert.equal(captured[2].url, 'http://localhost:9000/v1.0/modelrunnerendpoints/mre_123/ollama/models/delete?tenantId=ten_123');
  assert.equal(captured[2].options.method, 'POST');
  assert.equal(captured[2].options.body, JSON.stringify(deletePayload));
});

test('builds virtual model runner model load request with target mode', async () => {
  let capturedUrl = '';
  let capturedOptions = null;
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url, options) => {
      capturedUrl = url;
      capturedOptions = options;
      return createJsonResponse({ Success: true, OutcomeCode: 'Verified' });
    }
  });

  const payload = {
    Model: 'gemma3:4b',
    TargetMode: 'AllEligibleEndpoints',
    VerifyLoaded: true
  };
  const result = await client.loadVirtualModelRunnerModel('vmr_123', payload, 'ten_123');

  assert.equal(result.OutcomeCode, 'Verified');
  assert.equal(capturedUrl, 'http://localhost:9000/v1.0/virtualmodelrunners/vmr_123/load-model?tenantId=ten_123');
  assert.equal(capturedOptions.method, 'POST');
  assert.equal(capturedOptions.body, JSON.stringify(payload));
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
