export class ConductorApiError extends Error {
  constructor(message, status, endpoint, response) {
    super(message);
    this.name = 'ConductorApiError';
    this.status = status;
    this.endpoint = endpoint;
    this.response = response;
  }
}

export class ConductorClient {
  constructor({
    baseUrl,
    bearerToken = null,
    apiKey = null,
    adminEmail = null,
    adminPassword = null,
    fetchImpl = globalThis.fetch
  }) {
    if (!baseUrl) {
      throw new Error('baseUrl is required');
    }
    if (typeof fetchImpl !== 'function') {
      throw new Error('fetchImpl must be a function');
    }

    this.baseUrl = baseUrl.replace(/\/+$/, '');
    this.bearerToken = bearerToken || apiKey || null;
    this.adminEmail = adminEmail;
    this.adminPassword = adminPassword;
    this.fetchImpl = fetchImpl;
  }

  async validateVirtualModelRunner(draft, existingId = null) {
    return this.#request('POST', `/v1.0/virtualmodelrunners/validate${this.#existingIdQuery(existingId)}`, draft);
  }

  async getVirtualModelRunnerEffectiveConfiguration(id, tenantId = null) {
    return this.#request('GET', `/v1.0/virtualmodelrunners/${encodeURIComponent(id)}/effective${this.#tenantQuery(tenantId)}`);
  }

  async loadVirtualModelRunnerModel(id, payload = {}, tenantId = null) {
    return this.#request('POST', `/v1.0/virtualmodelrunners/${encodeURIComponent(id)}/load-model${this.#tenantQuery(tenantId)}`, payload);
  }

  async explainVirtualModelRunnerRouting(id, payload = {}, tenantId = null) {
    return this.#request('POST', `/v1.0/virtualmodelrunners/${encodeURIComponent(id)}/explain-routing${this.#tenantQuery(tenantId)}`, payload);
  }

  async listVirtualModelRunnerReservations(filters = {}) {
    return this.#request('GET', `/v1.0/vmrreservations${this.#queryString(filters)}`);
  }

  async getVirtualModelRunnerReservation(id, tenantId = null) {
    return this.#request('GET', `/v1.0/vmrreservations/${encodeURIComponent(id)}${this.#tenantQuery(tenantId)}`);
  }

  async createVirtualModelRunnerReservation(reservation) {
    return this.#request('POST', '/v1.0/vmrreservations', reservation);
  }

  async updateVirtualModelRunnerReservation(id, reservation) {
    return this.#request('PUT', `/v1.0/vmrreservations/${encodeURIComponent(id)}`, reservation);
  }

  async deleteVirtualModelRunnerReservation(id, tenantId = null) {
    return this.#request('DELETE', `/v1.0/vmrreservations/${encodeURIComponent(id)}${this.#tenantQuery(tenantId)}`);
  }

  async validateVirtualModelRunnerReservation(reservation) {
    return this.#request('POST', '/v1.0/vmrreservations/validate', reservation);
  }

  async listReservationsForVirtualModelRunner(id, filters = {}) {
    return this.#request('GET', `/v1.0/virtualmodelrunners/${encodeURIComponent(id)}/reservations${this.#queryString(filters)}`);
  }

  async getVirtualModelRunnerReservationEffective(id, filters = {}) {
    return this.#request('GET', `/v1.0/virtualmodelrunners/${encodeURIComponent(id)}/reservation-effective${this.#queryString(filters)}`);
  }

  async validateModelRunnerEndpoint(draft, existingId = null) {
    return this.#request('POST', `/v1.0/modelrunnerendpoints/validate${this.#existingIdQuery(existingId)}`, draft);
  }

  async drainModelRunnerEndpoint(id, tenantId = null) {
    return this.#request('POST', `/v1.0/modelrunnerendpoints/${encodeURIComponent(id)}/drain${this.#tenantQuery(tenantId)}`);
  }

  async resumeModelRunnerEndpoint(id, tenantId = null) {
    return this.#request('POST', `/v1.0/modelrunnerendpoints/${encodeURIComponent(id)}/resume${this.#tenantQuery(tenantId)}`);
  }

  async quarantineModelRunnerEndpoint(id, tenantId = null) {
    return this.#request('POST', `/v1.0/modelrunnerendpoints/${encodeURIComponent(id)}/quarantine${this.#tenantQuery(tenantId)}`);
  }

  async loadModelRunnerEndpointModel(id, payload = {}, tenantId = null) {
    return this.#request('POST', `/v1.0/modelrunnerendpoints/${encodeURIComponent(id)}/load-model${this.#tenantQuery(tenantId)}`, payload);
  }

  async listOllamaEndpointModels(id, tenantId = null) {
    return this.#request('GET', `/v1.0/modelrunnerendpoints/${encodeURIComponent(id)}/ollama/models${this.#tenantQuery(tenantId)}`);
  }

  async pullOllamaEndpointModel(id, payload = {}, tenantId = null) {
    return this.#request('POST', `/v1.0/modelrunnerendpoints/${encodeURIComponent(id)}/ollama/models/pull${this.#tenantQuery(tenantId)}`, payload);
  }

  async deleteOllamaEndpointModel(id, payload = {}, tenantId = null) {
    return this.#request('POST', `/v1.0/modelrunnerendpoints/${encodeURIComponent(id)}/ollama/models/delete${this.#tenantQuery(tenantId)}`, payload);
  }

  async validateModelDefinition(draft, existingId = null) {
    return this.#request('POST', `/v1.0/modeldefinitions/validate${this.#existingIdQuery(existingId)}`, draft);
  }

  async validateModelConfiguration(draft, existingId = null) {
    return this.#request('POST', `/v1.0/modelconfigurations/validate${this.#existingIdQuery(existingId)}`, draft);
  }

  async validateLoadBalancingPolicy(draft, existingId = null) {
    return this.#request('POST', `/v1.0/loadbalancingpolicies/validate${this.#existingIdQuery(existingId)}`, draft);
  }

  async listModelAccessPolicies(filters = {}) {
    return this.#request('GET', `/v1.0/modelaccesspolicies${this.#queryString(filters)}`);
  }

  async getModelAccessPolicy(id, tenantId = null) {
    return this.#request('GET', `/v1.0/modelaccesspolicies/${encodeURIComponent(id)}${this.#tenantQuery(tenantId)}`);
  }

  async createModelAccessPolicy(policy) {
    return this.#request('POST', '/v1.0/modelaccesspolicies', policy);
  }

  async updateModelAccessPolicy(id, policy) {
    return this.#request('PUT', `/v1.0/modelaccesspolicies/${encodeURIComponent(id)}`, policy);
  }

  async deleteModelAccessPolicy(id, { tenantId = null, forceDetach = false } = {}) {
    return this.#request('DELETE', `/v1.0/modelaccesspolicies/${encodeURIComponent(id)}${this.#queryString({
      tenantId,
      forceDetach: forceDetach ? true : null
    })}`);
  }

  async validateModelAccessPolicy(policy) {
    return this.#request('POST', '/v1.0/modelaccesspolicies/validate', policy);
  }

  async evaluateModelAccessPolicy(id, context = {}, tenantId = null) {
    return this.#request('POST', `/v1.0/modelaccesspolicies/${encodeURIComponent(id)}/evaluate${this.#tenantQuery(tenantId)}`, context);
  }

  async getEffectiveModelAccess(filters = {}) {
    return this.#request('GET', `/v1.0/modelaccesspolicies/effective${this.#queryString(filters)}`);
  }

  async searchRequestHistory(filters = {}) {
    return this.#request('GET', `/v1.0/requesthistory${this.#queryString(filters)}`);
  }

  async getRequestHistorySummary(filters = {}) {
    return this.#request('GET', `/v1.0/requesthistory/summary${this.#queryString(filters)}`);
  }

  async getRequestAnalyticsOverview(filters = {}) {
    return this.#request('GET', `/v1.0/requesthistory/analytics/overview${this.#queryString(filters)}`);
  }

  async getAnalyticsCatalog() {
    return this.#request('GET', '/v1.0/analytics/catalog');
  }

  async queryAnalytics(query = {}) {
    return this.#request('POST', '/v1.0/analytics/query', query);
  }

  async listAnalyticsSavedReports(filters = {}) {
    return this.#request('GET', `/v1.0/analytics/reports${this.#queryString(filters)}`);
  }

  async createAnalyticsSavedReport(report = {}) {
    return this.#request('POST', '/v1.0/analytics/reports', report);
  }

  async getAnalyticsSavedReport(id, tenantId = null) {
    return this.#request('GET', `/v1.0/analytics/reports/${encodeURIComponent(id)}${this.#tenantQuery(tenantId)}`);
  }

  async updateAnalyticsSavedReport(id, report = {}) {
    return this.#request('PUT', `/v1.0/analytics/reports/${encodeURIComponent(id)}`, report);
  }

  async deleteAnalyticsSavedReport(id, tenantId = null) {
    return this.#request('DELETE', `/v1.0/analytics/reports/${encodeURIComponent(id)}${this.#tenantQuery(tenantId)}`);
  }

  async getAnalyticsSummary(filters = {}) {
    return this.#request('GET', `/v1.0/analytics/summary${this.#queryString(filters)}`);
  }

  async getAnalyticsTimeSeries(filters = {}) {
    return this.#request('GET', `/v1.0/analytics/timeseries${this.#queryString(filters)}`);
  }

  async getAnalyticsTtft(filters = {}) {
    return this.#request('GET', `/v1.0/analytics/ttft${this.#queryString(filters)}`);
  }

  async getAnalyticsTokens(filters = {}) {
    return this.#request('GET', `/v1.0/analytics/tokens${this.#queryString(filters)}`);
  }

  async getAnalyticsCosts(filters = {}) {
    return this.#request('GET', `/v1.0/analytics/costs${this.#queryString(filters)}`);
  }

  async getAnalyticsUsers(filters = {}) {
    return this.#request('GET', `/v1.0/analytics/users${this.#queryString(filters)}`);
  }

  async getAnalyticsAccess(filters = {}) {
    return this.#request('GET', `/v1.0/analytics/access${this.#queryString(filters)}`);
  }

  async getRequestHistoryDetail(id, tenantId = null) {
    return this.#request('GET', `/v1.0/requesthistory/${encodeURIComponent(id)}/detail${this.#tenantQuery(tenantId)}`);
  }

  async getRequestHistoryAnalytics(id, tenantId = null) {
    return this.#request('GET', `/v1.0/requesthistory/${encodeURIComponent(id)}/analytics${this.#tenantQuery(tenantId)}`);
  }

  async bulkDeleteRequestHistory(filters = {}) {
    return this.#request('DELETE', `/v1.0/requesthistory/bulk${this.#queryString(filters)}`);
  }

  async getObservabilityMetricsSummary() {
    return this.#request('GET', '/v1.0/observability/metrics/summary');
  }

  async getObservabilityMetricsText() {
    return this.#request('GET', '/v1.0/observability/metrics', null, { responseType: 'text' });
  }

  async #request(method, endpoint, body = null, options = {}) {
    const headers = {};
    if (options.responseType !== 'text') {
      headers['Content-Type'] = 'application/json';
    }

    if (this.adminEmail && this.adminPassword) {
      headers['x-admin-email'] = this.adminEmail;
      headers['x-admin-password'] = this.adminPassword;
    } else if (this.bearerToken) {
      headers.Authorization = `Bearer ${this.bearerToken}`;
    }

    const response = await this.fetchImpl(`${this.baseUrl}${endpoint}`, {
      method,
      headers,
      body: body != null ? JSON.stringify(body) : null
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new ConductorApiError(
        errorData?.error || errorData?.message || errorData?.Message || `HTTP ${response.status}`,
        response.status,
        endpoint,
        errorData
      );
    }

    if (response.status === 204) {
      return null;
    }

    if (options.responseType === 'text') {
      return response.text();
    }

    return response.json();
  }

  #existingIdQuery(existingId) {
    return existingId ? `?existingId=${encodeURIComponent(existingId)}` : '';
  }

  #tenantQuery(tenantId) {
    return tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : '';
  }

  #queryString(filters) {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(filters || {})) {
      if (value !== null && value !== undefined && value !== '') {
        query.append(key, value);
      }
    }

    const serialized = query.toString();
    return serialized ? `?${serialized}` : '';
  }
}
