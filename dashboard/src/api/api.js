import { DEFAULT_SERVER_URL, persistServerUrl, resolveInitialServerUrl } from '../config/serverUrl';

const DEFAULT_API_URL = DEFAULT_SERVER_URL;

function readStoredJson(key) {
  const value = localStorage.getItem(key);
  if (!value) {
    return null;
  }

  try {
    return JSON.parse(value);
  } catch {
    localStorage.removeItem(key);
    return null;
  }
}

function createApiError(response, errorData, endpoint, method) {
  const error = new Error(
    errorData?.error ||
    errorData?.Message ||
    errorData?.message ||
    `HTTP ${response.status}`
  );

  error.status = response.status;
  error.endpoint = endpoint;
  error.method = method;
  error.response = errorData || {};

  return error;
}

class ConductorApi {
  constructor() {
    this.baseUrl = resolveInitialServerUrl() || DEFAULT_API_URL;
    this.pendingRequests = new Map();
    this.bearerToken = localStorage.getItem('conductor_token') || '';
    this.adminEmail = '';
    this.adminPassword = '';

    // Check if logged in as admin
    const isAdmin = localStorage.getItem('conductor_is_admin') === 'true';
    const admin = readStoredJson('conductor_admin');

    if (isAdmin && admin) {
      if (admin.ApiKey) {
        // Admin API key login - use bearer auth after a browser refresh.
        this.bearerToken = admin.ApiKey || this.bearerToken;
      } else {
        // Admin password login - use admin credentials, clear bearer token.
        this.adminEmail = admin.Email || '';
        this.adminPassword = admin.password || '';
        this.bearerToken = '';
      }
    }
  }

  setBaseUrl(url, source = 'user') {
    this.baseUrl = url;
    persistServerUrl(url, source);
  }

  setBearerToken(token) {
    this.bearerToken = token;
    localStorage.setItem('conductor_token', token);
  }

  clearAuth() {
    this.bearerToken = '';
    this.adminEmail = '';
    this.adminPassword = '';
    localStorage.removeItem('conductor_token');
    localStorage.removeItem('conductor_user');
    localStorage.removeItem('conductor_tenant');
    localStorage.removeItem('conductor_admin');
    localStorage.removeItem('conductor_is_admin');
  }

  setUserInfo(user, tenant) {
    localStorage.setItem('conductor_user', JSON.stringify(user));
    localStorage.setItem('conductor_tenant', JSON.stringify(tenant));
    localStorage.setItem('conductor_is_admin', 'false');
  }

  setAdminInfo(admin) {
    localStorage.setItem('conductor_admin', JSON.stringify(admin));
    localStorage.setItem('conductor_is_admin', 'true');
    if (admin.ApiKey) {
      this.bearerToken = admin.ApiKey;
      this.adminEmail = '';
      this.adminPassword = '';
      localStorage.setItem('conductor_token', admin.ApiKey);
    } else {
      this.bearerToken = '';
      this.adminEmail = admin.Email;
      this.adminPassword = admin.password; // Store temporarily for API calls
      localStorage.removeItem('conductor_token');
    }
  }

  getUserInfo() {
    return {
      user: readStoredJson('conductor_user'),
      tenant: readStoredJson('conductor_tenant')
    };
  }

  getAdminInfo() {
    const isAdmin = localStorage.getItem('conductor_is_admin') === 'true';
    return {
      admin: readStoredJson('conductor_admin'),
      isAdmin
    };
  }

  hasStoredAuth() {
    const { user, tenant } = this.getUserInfo();
    const { admin, isAdmin } = this.getAdminInfo();

    if (isAdmin) {
      return Boolean((admin && admin.ApiKey) || this.bearerToken || (this.adminEmail && this.adminPassword));
    }

    return Boolean(this.bearerToken && user && tenant);
  }

  async request(method, endpoint, body = null) {
    const headers = { 'Content-Type': 'application/json' };

    // Use admin auth OR bearer token, not both
    // Admin auth takes priority if available (for admin dashboard)
    if (this.adminEmail && this.adminPassword) {
      headers['x-admin-email'] = this.adminEmail;
      headers['x-admin-password'] = this.adminPassword;
    } else if (this.bearerToken) {
      headers['Authorization'] = `Bearer ${this.bearerToken}`;
    }

    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : null,
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw createApiError(response, errorData, endpoint, method);
    }

    if (response.status === 204) {
      return null;
    }

    return response.json();
  }

  async adminRequest(method, endpoint, body = null) {
    const headers = { 'Content-Type': 'application/json' };
    if (this.adminEmail && this.adminPassword) {
      headers['x-admin-email'] = this.adminEmail;
      headers['x-admin-password'] = this.adminPassword;
    } else if (this.bearerToken) {
      headers['Authorization'] = `Bearer ${this.bearerToken}`;
    }

    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : null,
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw createApiError(response, errorData, endpoint, method);
    }

    if (response.status === 204) {
      return null;
    }

    return response.json();
  }

  async dedupedRequest(cacheKey, method, endpoint, body = null) {
    if (this.pendingRequests.has(cacheKey)) {
      return this.pendingRequests.get(cacheKey);
    }

    const requestPromise = this.request(method, endpoint, body)
      .finally(() => {
        this.pendingRequests.delete(cacheKey);
      });

    this.pendingRequests.set(cacheKey, requestPromise);
    return requestPromise;
  }

  buildQueryString(params) {
    const query = new URLSearchParams();
    if (params.maxResults) query.append('maxResults', params.maxResults);
    if (params.continuationToken) query.append('continuationToken', params.continuationToken);
    if (params.nameFilter) query.append('nameFilter', params.nameFilter);
    if (params.activeFilter !== undefined) query.append('activeFilter', params.activeFilter);
    return query.toString() ? '?' + query.toString() : '';
  }

  // Auth APIs
  async loginWithCredential(tenantId, email, password) {
    const response = await this.request('POST', '/v1.0/auth/login/credential', {
      TenantId: tenantId,
      Email: email,
      Password: password
    });
    if (response.Success && response.BearerToken) {
      // Clear any existing admin auth (user auth should be used)
      this.adminEmail = '';
      this.adminPassword = '';
      localStorage.removeItem('conductor_admin');

      this.setBearerToken(response.BearerToken);
      this.setUserInfo(response.User, response.Tenant);
    }
    return response;
  }

  async loginWithApiKey(apiKey) {
    const response = await this.request('POST', '/v1.0/auth/login/apikey', {
      ApiKey: apiKey
    });
    if (response.Success) {
      if (response.IsAdmin) {
        // Admin API key login - use the API key for subsequent requests
        this.bearerToken = apiKey;
        localStorage.setItem('conductor_token', apiKey);
        localStorage.setItem('conductor_is_admin', 'true');
        localStorage.setItem('conductor_admin', JSON.stringify({ ApiKey: apiKey }));
        localStorage.removeItem('conductor_user');
        localStorage.removeItem('conductor_tenant');
        // Clear email/password since we're using API key
        this.adminEmail = '';
        this.adminPassword = '';
      } else if (response.BearerToken) {
        // Regular user API key login
        this.adminEmail = '';
        this.adminPassword = '';
        localStorage.removeItem('conductor_admin');

        this.setBearerToken(response.BearerToken);
        this.setUserInfo(response.User, response.Tenant);
      }
    }
    return response;
  }

  // Tenant APIs
  async listTenants(params = {}) {
    const query = this.buildQueryString(params);
    return this.dedupedRequest(`listTenants:${query}`, 'GET', `/v1.0/tenants${query}`);
  }

  async getTenant(id) {
    return this.request('GET', `/v1.0/tenants/${id}`);
  }

  async createTenant(tenant) {
    return this.request('POST', '/v1.0/tenants', tenant);
  }

  async updateTenant(id, tenant) {
    return this.request('PUT', `/v1.0/tenants/${id}`, tenant);
  }

  async deleteTenant(id) {
    return this.request('DELETE', `/v1.0/tenants/${id}`);
  }

  // User APIs
  async listUsers(params = {}) {
    const query = this.buildQueryString(params);
    return this.dedupedRequest(`listUsers:${query}`, 'GET', `/v1.0/users${query}`);
  }

  async getUser(id) {
    return this.request('GET', `/v1.0/users/${id}`);
  }

  async createUser(user) {
    return this.request('POST', '/v1.0/users', user);
  }

  async updateUser(id, user) {
    return this.request('PUT', `/v1.0/users/${id}`, user);
  }

  async deleteUser(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('DELETE', `/v1.0/users/${id}${query}`);
  }

  // Credential APIs
  async listCredentials(params = {}) {
    const query = this.buildQueryString(params);
    return this.dedupedRequest(`listCredentials:${query}`, 'GET', `/v1.0/credentials${query}`);
  }

  async getCredential(id) {
    return this.request('GET', `/v1.0/credentials/${id}`);
  }

  async createCredential(credential) {
    return this.request('POST', '/v1.0/credentials', credential);
  }

  async updateCredential(id, credential) {
    return this.request('PUT', `/v1.0/credentials/${id}`, credential);
  }

  async deleteCredential(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('DELETE', `/v1.0/credentials/${id}${query}`);
  }

  // Model Runner Endpoint APIs
  async listModelRunnerEndpoints(params = {}) {
    const query = this.buildQueryString(params);
    return this.dedupedRequest(`listModelRunnerEndpoints:${query}`, 'GET', `/v1.0/modelrunnerendpoints${query}`);
  }

  async getModelRunnerEndpoint(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('GET', `/v1.0/modelrunnerendpoints/${id}${query}`);
  }

  async createModelRunnerEndpoint(endpoint) {
    return this.request('POST', '/v1.0/modelrunnerendpoints', endpoint);
  }

  async validateModelRunnerEndpoint(endpoint, existingId = null) {
    const query = existingId ? `?existingId=${encodeURIComponent(existingId)}` : '';
    return this.request('POST', `/v1.0/modelrunnerendpoints/validate${query}`, endpoint);
  }

  async updateModelRunnerEndpoint(id, endpoint) {
    return this.request('PUT', `/v1.0/modelrunnerendpoints/${id}`, endpoint);
  }

  async deleteModelRunnerEndpoint(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('DELETE', `/v1.0/modelrunnerendpoints/${id}${query}`);
  }

  async getModelRunnerEndpointsHealth() {
    return this.request('GET', '/v1.0/modelrunnerendpoints/health');
  }

  async getModelRunnerEndpointHealth(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('GET', `/v1.0/modelrunnerendpoints/${id}/health${query}`);
  }

  async getModelRunnerEndpointRigMonitor(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('GET', `/v1.0/modelrunnerendpoints/${id}/rigmonitor${query}`);
  }

  async drainModelRunnerEndpoint(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('POST', `/v1.0/modelrunnerendpoints/${id}/drain${query}`);
  }

  async resumeModelRunnerEndpoint(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('POST', `/v1.0/modelrunnerendpoints/${id}/resume${query}`);
  }

  async quarantineModelRunnerEndpoint(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('POST', `/v1.0/modelrunnerendpoints/${id}/quarantine${query}`);
  }

  // Model Definition APIs
  async listModelDefinitions(params = {}) {
    const query = this.buildQueryString(params);
    return this.dedupedRequest(`listModelDefinitions:${query}`, 'GET', `/v1.0/modeldefinitions${query}`);
  }

  async getModelDefinition(id) {
    return this.request('GET', `/v1.0/modeldefinitions/${id}`);
  }

  async createModelDefinition(definition) {
    return this.request('POST', '/v1.0/modeldefinitions', definition);
  }

  async validateModelDefinition(definition, existingId = null) {
    const query = existingId ? `?existingId=${encodeURIComponent(existingId)}` : '';
    return this.request('POST', `/v1.0/modeldefinitions/validate${query}`, definition);
  }

  async updateModelDefinition(id, definition) {
    return this.request('PUT', `/v1.0/modeldefinitions/${id}`, definition);
  }

  async deleteModelDefinition(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('DELETE', `/v1.0/modeldefinitions/${id}${query}`);
  }

  // Model Configuration APIs
  async listModelConfigurations(params = {}) {
    const query = this.buildQueryString(params);
    return this.dedupedRequest(`listModelConfigurations:${query}`, 'GET', `/v1.0/modelconfigurations${query}`);
  }

  async getModelConfiguration(id) {
    return this.request('GET', `/v1.0/modelconfigurations/${id}`);
  }

  async createModelConfiguration(configuration) {
    return this.request('POST', '/v1.0/modelconfigurations', configuration);
  }

  async validateModelConfiguration(configuration, existingId = null) {
    const query = existingId ? `?existingId=${encodeURIComponent(existingId)}` : '';
    return this.request('POST', `/v1.0/modelconfigurations/validate${query}`, configuration);
  }

  async updateModelConfiguration(id, configuration) {
    return this.request('PUT', `/v1.0/modelconfigurations/${id}`, configuration);
  }

  async deleteModelConfiguration(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('DELETE', `/v1.0/modelconfigurations/${id}${query}`);
  }

  // Load Balancing Policy APIs
  async listLoadBalancingPolicies(params = {}) {
    const query = this.buildQueryString(params);
    return this.dedupedRequest(`listLoadBalancingPolicies:${query}`, 'GET', `/v1.0/loadbalancingpolicies${query}`);
  }

  async getLoadBalancingPolicy(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('GET', `/v1.0/loadbalancingpolicies/${id}${query}`);
  }

  async createLoadBalancingPolicy(policy) {
    return this.request('POST', '/v1.0/loadbalancingpolicies', policy);
  }

  async validateLoadBalancingPolicy(policy, existingId = null) {
    const query = existingId ? `?existingId=${encodeURIComponent(existingId)}` : '';
    return this.request('POST', `/v1.0/loadbalancingpolicies/validate${query}`, policy);
  }

  async updateLoadBalancingPolicy(id, policy) {
    return this.request('PUT', `/v1.0/loadbalancingpolicies/${id}`, policy);
  }

  async deleteLoadBalancingPolicy(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('DELETE', `/v1.0/loadbalancingpolicies/${id}${query}`);
  }

  async getLoadBalancingPolicyMetrics() {
    return this.request('GET', '/v1.0/loadbalancingpolicies/metrics');
  }

  // Virtual Model Runner APIs
  async listVirtualModelRunners(params = {}) {
    const query = this.buildQueryString(params);
    return this.dedupedRequest(`listVirtualModelRunners:${query}`, 'GET', `/v1.0/virtualmodelrunners${query}`);
  }

  async getVirtualModelRunner(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('GET', `/v1.0/virtualmodelrunners/${id}${query}`);
  }

  async createVirtualModelRunner(vmr) {
    return this.request('POST', '/v1.0/virtualmodelrunners', vmr);
  }

  async validateVirtualModelRunner(vmr, existingId = null) {
    const query = existingId ? `?existingId=${encodeURIComponent(existingId)}` : '';
    return this.request('POST', `/v1.0/virtualmodelrunners/validate${query}`, vmr);
  }

  async updateVirtualModelRunner(id, vmr) {
    return this.request('PUT', `/v1.0/virtualmodelrunners/${id}`, vmr);
  }

  async deleteVirtualModelRunner(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('DELETE', `/v1.0/virtualmodelrunners/${id}${query}`);
  }

  async getVirtualModelRunnerHealth(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('GET', `/v1.0/virtualmodelrunners/${id}/health${query}`);
  }

  async getVirtualModelRunnerEffectiveConfiguration(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('GET', `/v1.0/virtualmodelrunners/${id}/effective${query}`);
  }

  async explainVirtualModelRunnerRouting(id, payload = {}, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('POST', `/v1.0/virtualmodelrunners/${id}/explain-routing${query}`, payload);
  }

  async getObservabilityMetricsSummary() {
    return this.request('GET', '/v1.0/observability/metrics/summary');
  }

  async getObservabilityMetricsText() {
    const headers = {};

    if (this.adminEmail && this.adminPassword) {
      headers['x-admin-email'] = this.adminEmail;
      headers['x-admin-password'] = this.adminPassword;
    } else if (this.bearerToken) {
      headers['Authorization'] = `Bearer ${this.bearerToken}`;
    }

    const response = await fetch(`${this.baseUrl}/v1.0/observability/metrics`, {
      method: 'GET',
      headers
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw createApiError(response, errorData, '/v1.0/observability/metrics', 'GET');
    }

    return response.text();
  }

  // Admin Login API
  async loginAsAdmin(email, password) {
    const response = await this.request('POST', '/v1.0/auth/login/admin', {
      Email: email,
      Password: password
    });
    if (response.Success) {
      // Clear any existing bearer token auth (admin auth should take priority)
      this.bearerToken = '';
      localStorage.removeItem('conductor_token');
      localStorage.removeItem('conductor_user');
      localStorage.removeItem('conductor_tenant');

      // Store admin credentials for subsequent API calls
      this.adminEmail = email;
      this.adminPassword = password;
      this.setAdminInfo({ ...response.Administrator, password });
    }
    return response;
  }

  // Administrator APIs
  async listAdministrators(params = {}) {
    const query = this.buildQueryString(params);
    return this.adminRequest('GET', `/v1.0/administrators${query}`);
  }

  async getAdministrator(id) {
    return this.adminRequest('GET', `/v1.0/administrators/${id}`);
  }

  async createAdministrator(admin) {
    return this.adminRequest('POST', '/v1.0/administrators', admin);
  }

  async updateAdministrator(id, admin) {
    return this.adminRequest('PUT', `/v1.0/administrators/${id}`, admin);
  }

  async deleteAdministrator(id) {
    return this.adminRequest('DELETE', `/v1.0/administrators/${id}`);
  }

  // Backup and Restore APIs
  // These use the standard request method which works with both:
  // - Bearer token authentication (users with IsAdmin=true)
  // - Admin header authentication (system administrators)

  /**
   * Create a backup of all configuration data.
   * @returns {Promise<Object>} The backup package
   */
  async createBackup() {
    return this.request('GET', '/v1.0/backup');
  }

  /**
   * Restore configuration from a backup file.
   * @param {Object} backup - The parsed backup JSON
   * @param {Object} options - Restore options
   * @returns {Promise<Object>} Restore result
   */
  async restoreBackup(backup, options = {}) {
    return this.request('POST', '/v1.0/backup/restore', {
      Package: backup,
      Options: options
    });
  }

  /**
   * Validate a backup file without applying changes.
   * @param {Object} backup - The parsed backup JSON
   * @returns {Promise<Object>} Validation result
   */
  async validateBackup(backup) {
    return this.request('POST', '/v1.0/backup/validate', backup);
  }

  // Request History Summary API
  /**
   * Get aggregated request history summary with time-bucketed success/failure counts.
   * @param {Object} params - Summary parameters
   * @param {string} params.vmrGuid - Filter by Virtual Model Runner GUID
   * @param {string} params.startUtc - Start of time range (UTC, ISO 8601)
   * @param {string} params.endUtc - End of time range (UTC, ISO 8601)
   * @param {string} params.interval - Bucket interval: "hour" or "day"
   * @returns {Promise<Object>} Summary result with Data (array of buckets), StartUtc, EndUtc, Interval, TotalSuccess, TotalFailure, TotalRequests
   */
  async getRequestHistorySummary(params = {}) {
    const query = new URLSearchParams();
    if (params.vmrGuid) query.append('vmrGuid', params.vmrGuid);
    if (params.startUtc) query.append('startUtc', params.startUtc);
    if (params.endUtc) query.append('endUtc', params.endUtc);
    if (params.interval) query.append('interval', params.interval);
    if (params.endpointGuid) query.append('endpointGuid', params.endpointGuid);
    if (params.requestorUserGuid) query.append('requestorUserGuid', params.requestorUserGuid);
    if (params.credentialGuid) query.append('credentialGuid', params.credentialGuid);
    if (params.loadBalancingPolicyGuid) query.append('loadBalancingPolicyGuid', params.loadBalancingPolicyGuid);
    if (params.modelName) query.append('modelName', params.modelName);
    if (params.denialReasonCode) query.append('denialReasonCode', params.denialReasonCode);
    if (params.sessionAffinityOutcome) query.append('sessionAffinityOutcome', params.sessionAffinityOutcome);
    if (params.statusClass) query.append('statusClass', params.statusClass);
    if (params.sourceIp) query.append('sourceIp', params.sourceIp);
    if (params.httpStatus) query.append('httpStatus', params.httpStatus);
    const queryString = query.toString() ? '?' + query.toString() : '';
    return this.request('GET', `/v1.0/requesthistory/summary${queryString}`);
  }

  async getRequestAnalyticsOverview(params = {}) {
    const query = new URLSearchParams();
    if (params.range) query.append('range', params.range);
    if (params.startUtc) query.append('startUtc', params.startUtc);
    if (params.endUtc) query.append('endUtc', params.endUtc);
    if (params.bucketSeconds) query.append('bucketSeconds', params.bucketSeconds);
    if (params.limit) query.append('limit', params.limit);
    if (params.vmrGuid) query.append('vmrGuid', params.vmrGuid);
    if (params.endpointGuid) query.append('endpointGuid', params.endpointGuid);
    if (params.providerName) query.append('providerName', params.providerName);
    if (params.modelName) query.append('modelName', params.modelName);
    if (params.stageKind) query.append('stageKind', params.stageKind);
    if (params.statusClass) query.append('statusClass', params.statusClass);
    const queryString = query.toString() ? '?' + query.toString() : '';
    return this.request('GET', `/v1.0/requesthistory/analytics/overview${queryString}`);
  }

  // Request History APIs
  /**
   * Search request history entries with pagination.
   * @param {Object} params - Search parameters
   * @param {string} params.vmrGuid - Filter by Virtual Model Runner GUID
   * @param {string} params.endpointGuid - Filter by Model Endpoint GUID
   * @param {string} params.sourceIp - Filter by source IP
   * @param {number} params.httpStatus - Filter by HTTP status code
   * @param {number} params.page - Page number (1-based)
   * @param {number} params.pageSize - Number of results per page
   * @returns {Promise<Object>} Search result with Data, Page, PageSize, TotalCount, TotalPages
   */
  async searchRequestHistory(params = {}) {
    const query = new URLSearchParams();
    if (params.vmrGuid) query.append('vmrGuid', params.vmrGuid);
    if (params.endpointGuid) query.append('endpointGuid', params.endpointGuid);
    if (params.requestorUserGuid) query.append('requestorUserGuid', params.requestorUserGuid);
    if (params.credentialGuid) query.append('credentialGuid', params.credentialGuid);
    if (params.loadBalancingPolicyGuid) query.append('loadBalancingPolicyGuid', params.loadBalancingPolicyGuid);
    if (params.modelName) query.append('modelName', params.modelName);
    if (params.mutationSummary) query.append('mutationSummary', params.mutationSummary);
    if (params.denialReasonCode) query.append('denialReasonCode', params.denialReasonCode);
    if (params.sessionAffinityOutcome) query.append('sessionAffinityOutcome', params.sessionAffinityOutcome);
    if (params.statusClass) query.append('statusClass', params.statusClass);
    if (params.createdAfterUtc) query.append('createdAfterUtc', params.createdAfterUtc);
    if (params.createdBeforeUtc) query.append('createdBeforeUtc', params.createdBeforeUtc);
    if (params.sourceIp) query.append('sourceIp', params.sourceIp);
    if (params.httpStatus) query.append('httpStatus', params.httpStatus);
    if (params.page) query.append('page', params.page);
    if (params.pageSize) query.append('pageSize', params.pageSize);
    const queryString = query.toString() ? '?' + query.toString() : '';
    return this.request('GET', `/v1.0/requesthistory${queryString}`);
  }

  /**
   * Get a request history entry by ID.
   * @param {string} id - The entry ID
   * @returns {Promise<Object>} Request history entry
   */
  async getRequestHistoryEntry(id) {
    return this.request('GET', `/v1.0/requesthistory/${id}`);
  }

  /**
   * Get full request history detail including headers and bodies.
   * @param {string} id - The entry ID
   * @returns {Promise<Object>} Request history detail
   */
  async getRequestHistoryDetail(id) {
    return this.request('GET', `/v1.0/requesthistory/${id}/detail`);
  }

  async getRequestHistoryAnalytics(id) {
    return this.request('GET', `/v1.0/requesthistory/${id}/analytics`);
  }

  /**
   * Delete a request history entry.
   * @param {string} id - The entry ID
   * @returns {Promise<void>}
   */
  async deleteRequestHistoryEntry(id) {
    return this.request('DELETE', `/v1.0/requesthistory/${id}`);
  }

  /**
   * Delete selected request history entries by ID.
   * @param {string[]} ids - Request history entry IDs to delete
   * @returns {Promise<Object>} Result with DeletedCount
   */
  async deleteRequestHistoryEntries(ids = []) {
    return this.request('POST', '/v1.0/requesthistory/delete', { Ids: ids });
  }

  /**
   * Bulk delete request history entries matching filter.
   * @param {Object} params - Filter parameters
   * @param {string} params.vmrGuid - Filter by Virtual Model Runner GUID
   * @param {string} params.endpointGuid - Filter by Model Endpoint GUID
   * @param {string} params.sourceIp - Filter by source IP
   * @param {number} params.httpStatus - Filter by HTTP status code
   * @returns {Promise<Object>} Result with DeletedCount
   */
  async bulkDeleteRequestHistory(params = {}) {
    const query = new URLSearchParams();
    if (params.vmrGuid) query.append('vmrGuid', params.vmrGuid);
    if (params.endpointGuid) query.append('endpointGuid', params.endpointGuid);
    if (params.requestorUserGuid) query.append('requestorUserGuid', params.requestorUserGuid);
    if (params.credentialGuid) query.append('credentialGuid', params.credentialGuid);
    if (params.loadBalancingPolicyGuid) query.append('loadBalancingPolicyGuid', params.loadBalancingPolicyGuid);
    if (params.modelName) query.append('modelName', params.modelName);
    if (params.mutationSummary) query.append('mutationSummary', params.mutationSummary);
    if (params.denialReasonCode) query.append('denialReasonCode', params.denialReasonCode);
    if (params.sessionAffinityOutcome) query.append('sessionAffinityOutcome', params.sessionAffinityOutcome);
    if (params.statusClass) query.append('statusClass', params.statusClass);
    if (params.createdAfterUtc) query.append('createdAfterUtc', params.createdAfterUtc);
    if (params.createdBeforeUtc) query.append('createdBeforeUtc', params.createdBeforeUtc);
    if (params.sourceIp) query.append('sourceIp', params.sourceIp);
    if (params.httpStatus) query.append('httpStatus', params.httpStatus);
    const queryString = query.toString() ? '?' + query.toString() : '';
    return this.request('DELETE', `/v1.0/requesthistory/bulk${queryString}`);
  }
}

export const api = new ConductorApi();
export default api;
