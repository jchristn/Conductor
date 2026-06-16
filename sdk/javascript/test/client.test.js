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
    modelName: 'gpt-4o-mini',
    reservationGuid: 'vmrr_1'
  });

  assert.equal(
    capturedUrl,
    'http://localhost:9000/v1.0/requesthistory?vmrGuid=vmr_1&statusClass=5xx&modelName=gpt-4o-mini&reservationGuid=vmrr_1'
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
    providerName: 'Ollama',
    reservationReasonCode: 'ReservationDenied'
  });

  assert.equal(
    capturedUrl,
    'http://localhost:9000/v1.0/requesthistory/analytics/overview?range=lastWeek&vmrGuid=vmr_1&providerName=Ollama&reservationReasonCode=ReservationDenied'
  );
});

test('builds analytics workspace GET query string', async () => {
  let capturedUrl = '';
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url) => {
      capturedUrl = url;
      return createJsonResponse({ TotalRequests: 0 });
    }
  });

  await client.getAnalyticsSummary({
    range: 'lastDay',
    groupBy: 'RequestorUserId',
    requestorUserGuid: 'usr_1',
    reservationGuid: 'vmrr_1',
    reservationReasonCode: 'ReservationDenied',
    tokenUnitCost: '0.000001',
    costCurrency: 'USD'
  });

  assert.equal(
    capturedUrl,
    'http://localhost:9000/v1.0/analytics/summary?range=lastDay&groupBy=RequestorUserId&requestorUserGuid=usr_1&reservationGuid=vmrr_1&reservationReasonCode=ReservationDenied&tokenUnitCost=0.000001&costCurrency=USD'
  );
});

test('builds analytics workspace POST query request', async () => {
  let capturedUrl = '';
  let capturedOptions = null;
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url, options) => {
      capturedUrl = url;
      capturedOptions = options;
      return createJsonResponse({ TotalRequests: 0 });
    }
  });

  const query = {
    Range: 'lastDay',
    TokenUnitCost: 0.000001,
    CostCurrency: 'USD',
    GroupBy: ['RequestorUserId'],
    Filters: {
      RequestorUserIds: ['usr_1'],
      ReservationReasonCodes: ['ReservationDenied'],
      SuccessfulCompletionsOnly: true
    }
  };

  await client.queryAnalytics(query);

  assert.equal(capturedUrl, 'http://localhost:9000/v1.0/analytics/query');
  assert.equal(capturedOptions.method, 'POST');
  assert.equal(capturedOptions.body, JSON.stringify(query));
});

test('builds analytics saved report CRUD requests', async () => {
  const captured = [];
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url, options) => {
      captured.push({ url, options });
      if (options.method === 'DELETE') {
        return createJsonResponse({}, 204);
      }

      return createJsonResponse({ Id: 'asr_123' });
    }
  });

  const report = {
    TenantId: 'ten_123',
    Name: 'Daily user cost',
    Query: {
      Range: 'lastDay',
      TokenUnitCost: 0.000001,
      GroupBy: ['RequestorUserId']
    }
  };

  await client.listAnalyticsSavedReports({ tenantId: 'ten_123', maxResults: 25, nameFilter: 'daily' });
  await client.createAnalyticsSavedReport(report);
  await client.getAnalyticsSavedReport('asr_123', 'ten_123');
  await client.updateAnalyticsSavedReport('asr_123', report);
  const deleteResult = await client.deleteAnalyticsSavedReport('asr_123', 'ten_123');

  assert.equal(deleteResult, null);
  assert.equal(captured[0].url, 'http://localhost:9000/v1.0/analytics/reports?tenantId=ten_123&maxResults=25&nameFilter=daily');
  assert.equal(captured[0].options.method, 'GET');
  assert.equal(captured[1].url, 'http://localhost:9000/v1.0/analytics/reports');
  assert.equal(captured[1].options.method, 'POST');
  assert.equal(captured[1].options.body, JSON.stringify(report));
  assert.equal(captured[2].url, 'http://localhost:9000/v1.0/analytics/reports/asr_123?tenantId=ten_123');
  assert.equal(captured[2].options.method, 'GET');
  assert.equal(captured[3].url, 'http://localhost:9000/v1.0/analytics/reports/asr_123');
  assert.equal(captured[3].options.method, 'PUT');
  assert.equal(captured[3].options.body, JSON.stringify(report));
  assert.equal(captured[4].url, 'http://localhost:9000/v1.0/analytics/reports/asr_123?tenantId=ten_123');
  assert.equal(captured[4].options.method, 'DELETE');
});

test('builds model access policy management requests', async () => {
  const captured = [];
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url, options) => {
      captured.push({ url, options });
      if (options.method === 'DELETE') {
        return createJsonResponse({}, 204);
      }

      return createJsonResponse({ Success: true });
    }
  });

  const policy = {
    TenantId: 'ten_123',
    Name: 'Production policy',
    DefaultDecision: 'Deny',
    Rules: []
  };
  const context = {
    TenantId: 'ten_123',
    CredentialId: 'cred_123',
    VirtualModelRunnerId: 'vmr_123',
    RequestedModel: 'gpt-4o-mini',
    Action: 'Completions'
  };

  await client.listModelAccessPolicies({
    tenantId: 'ten_123',
    maxResults: 25,
    continuationToken: 'next page',
    nameFilter: 'prod',
    activeFilter: true
  });
  await client.getModelAccessPolicy('map_123', 'ten_123');
  await client.createModelAccessPolicy(policy);
  await client.updateModelAccessPolicy('map_123', policy);
  const deleteResult = await client.deleteModelAccessPolicy('map_123', { tenantId: 'ten_123', forceDetach: true });
  await client.validateModelAccessPolicy(policy);
  await client.evaluateModelAccessPolicy('map_123', context, 'ten_123');
  await client.getEffectiveModelAccess({
    tenantId: 'ten_123',
    credentialId: 'cred_123',
    userId: 'usr_123',
    vmrId: 'vmr_123',
    modelDefinitionId: 'mod_123',
    modelName: 'gpt-4o-mini',
    action: 'Completions'
  });

  assert.equal(deleteResult, null);
  assert.equal(
    captured[0].url,
    'http://localhost:9000/v1.0/modelaccesspolicies?tenantId=ten_123&maxResults=25&continuationToken=next+page&nameFilter=prod&activeFilter=true'
  );
  assert.equal(captured[0].options.method, 'GET');
  assert.equal(captured[1].url, 'http://localhost:9000/v1.0/modelaccesspolicies/map_123?tenantId=ten_123');
  assert.equal(captured[1].options.method, 'GET');
  assert.equal(captured[2].url, 'http://localhost:9000/v1.0/modelaccesspolicies');
  assert.equal(captured[2].options.method, 'POST');
  assert.equal(captured[2].options.body, JSON.stringify(policy));
  assert.equal(captured[3].url, 'http://localhost:9000/v1.0/modelaccesspolicies/map_123');
  assert.equal(captured[3].options.method, 'PUT');
  assert.equal(captured[3].options.body, JSON.stringify(policy));
  assert.equal(captured[4].url, 'http://localhost:9000/v1.0/modelaccesspolicies/map_123?tenantId=ten_123&forceDetach=true');
  assert.equal(captured[4].options.method, 'DELETE');
  assert.equal(captured[5].url, 'http://localhost:9000/v1.0/modelaccesspolicies/validate');
  assert.equal(captured[5].options.method, 'POST');
  assert.equal(captured[5].options.body, JSON.stringify(policy));
  assert.equal(captured[6].url, 'http://localhost:9000/v1.0/modelaccesspolicies/map_123/evaluate?tenantId=ten_123');
  assert.equal(captured[6].options.method, 'POST');
  assert.equal(captured[6].options.body, JSON.stringify(context));
  assert.equal(
    captured[7].url,
    'http://localhost:9000/v1.0/modelaccesspolicies/effective?tenantId=ten_123&credentialId=cred_123&userId=usr_123&vmrId=vmr_123&modelDefinitionId=mod_123&modelName=gpt-4o-mini&action=Completions'
  );
  assert.equal(captured[7].options.method, 'GET');
});

test('builds virtual model runner reservation management requests', async () => {
  const captured = [];
  const client = new ConductorClient({
    baseUrl: 'http://localhost:9000',
    fetchImpl: async (url, options) => {
      captured.push({ url, options });
      if (options.method === 'DELETE') {
        return createJsonResponse({}, 204);
      }

      return createJsonResponse({ Success: true });
    }
  });

  const reservation = {
    TenantId: 'ten_123',
    VirtualModelRunnerId: 'vmr_123',
    Name: 'Benchmark window',
    StartUtc: '2026-06-16T10:00:00Z',
    EndUtc: '2026-06-16T11:00:00Z',
    Subjects: [{ SubjectType: 'User', SubjectId: 'usr_123' }]
  };

  await client.listVirtualModelRunnerReservations({
    tenantId: 'ten_123',
    vmrId: 'vmr_123',
    state: 'upcoming',
    subjectType: 'User',
    subjectId: 'usr_123',
    startsBeforeUtc: '2026-06-16T12:00:00Z',
    endsAfterUtc: '2026-06-16T09:00:00Z',
    maxResults: 25
  });
  await client.getVirtualModelRunnerReservation('vmrr_123', 'ten_123');
  await client.createVirtualModelRunnerReservation(reservation);
  await client.updateVirtualModelRunnerReservation('vmrr_123', reservation);
  const deleteResult = await client.deleteVirtualModelRunnerReservation('vmrr_123', 'ten_123');
  await client.validateVirtualModelRunnerReservation(reservation);
  await client.listReservationsForVirtualModelRunner('vmr_123', { tenantId: 'ten_123', state: 'active' });
  await client.getVirtualModelRunnerReservationEffective('vmr_123', {
    tenantId: 'ten_123',
    userId: 'usr_123',
    credentialId: 'cred_123',
    atUtc: '2026-06-16T10:30:00Z'
  });

  assert.equal(deleteResult, null);
  assert.equal(
    captured[0].url,
    'http://localhost:9000/v1.0/vmrreservations?tenantId=ten_123&vmrId=vmr_123&state=upcoming&subjectType=User&subjectId=usr_123&startsBeforeUtc=2026-06-16T12%3A00%3A00Z&endsAfterUtc=2026-06-16T09%3A00%3A00Z&maxResults=25'
  );
  assert.equal(captured[0].options.method, 'GET');
  assert.equal(captured[1].url, 'http://localhost:9000/v1.0/vmrreservations/vmrr_123?tenantId=ten_123');
  assert.equal(captured[2].url, 'http://localhost:9000/v1.0/vmrreservations');
  assert.equal(captured[2].options.method, 'POST');
  assert.equal(captured[2].options.body, JSON.stringify(reservation));
  assert.equal(captured[3].url, 'http://localhost:9000/v1.0/vmrreservations/vmrr_123');
  assert.equal(captured[3].options.method, 'PUT');
  assert.equal(captured[4].url, 'http://localhost:9000/v1.0/vmrreservations/vmrr_123?tenantId=ten_123');
  assert.equal(captured[4].options.method, 'DELETE');
  assert.equal(captured[5].url, 'http://localhost:9000/v1.0/vmrreservations/validate');
  assert.equal(captured[5].options.method, 'POST');
  assert.equal(captured[6].url, 'http://localhost:9000/v1.0/virtualmodelrunners/vmr_123/reservations?tenantId=ten_123&state=active');
  assert.equal(
    captured[7].url,
    'http://localhost:9000/v1.0/virtualmodelrunners/vmr_123/reservation-effective?tenantId=ten_123&userId=usr_123&credentialId=cred_123&atUtc=2026-06-16T10%3A30%3A00Z'
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
