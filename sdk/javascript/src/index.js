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

  async explainVirtualModelRunnerRouting(id, payload = {}, tenantId = null) {
    return this.#request('POST', `/v1.0/virtualmodelrunners/${encodeURIComponent(id)}/explain-routing${this.#tenantQuery(tenantId)}`, payload);
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

  async validateModelDefinition(draft, existingId = null) {
    return this.#request('POST', `/v1.0/modeldefinitions/validate${this.#existingIdQuery(existingId)}`, draft);
  }

  async validateModelConfiguration(draft, existingId = null) {
    return this.#request('POST', `/v1.0/modelconfigurations/validate${this.#existingIdQuery(existingId)}`, draft);
  }

  async validateLoadBalancingPolicy(draft, existingId = null) {
    return this.#request('POST', `/v1.0/loadbalancingpolicies/validate${this.#existingIdQuery(existingId)}`, draft);
  }

  async searchRequestHistory(filters = {}) {
    return this.#request('GET', `/v1.0/requesthistory${this.#queryString(filters)}`);
  }

  async getRequestHistorySummary(filters = {}) {
    return this.#request('GET', `/v1.0/requesthistory/summary${this.#queryString(filters)}`);
  }

  async getRequestHistoryDetail(id, tenantId = null) {
    return this.#request('GET', `/v1.0/requesthistory/${encodeURIComponent(id)}/detail${this.#tenantQuery(tenantId)}`);
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
