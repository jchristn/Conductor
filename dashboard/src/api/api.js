const DEFAULT_API_URL = 'http://localhost:9000';

class ConductorApi {
  constructor() {
    this.baseUrl = localStorage.getItem('conductor_server_url') || DEFAULT_API_URL;
    this.pendingRequests = new Map();

    // Check if logged in as admin
    const isAdmin = localStorage.getItem('conductor_is_admin') === 'true';
    const adminStr = localStorage.getItem('conductor_admin');

    if (isAdmin && adminStr) {
      // Admin login - use admin credentials, clear bearer token
      try {
        const admin = JSON.parse(adminStr);
        this.adminEmail = admin.Email || '';
        this.adminPassword = admin.password || '';
      } catch (e) {
        this.adminEmail = '';
        this.adminPassword = '';
      }
      this.bearerToken = '';
    } else {
      // User login or not logged in - use bearer token, clear admin credentials
      this.bearerToken = localStorage.getItem('conductor_token') || '';
      this.adminEmail = '';
      this.adminPassword = '';
    }
  }

  setBaseUrl(url) {
    this.baseUrl = url;
    localStorage.setItem('conductor_server_url', url);
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
    this.adminEmail = admin.Email;
    this.adminPassword = admin.password; // Store temporarily for API calls
  }

  getUserInfo() {
    const userStr = localStorage.getItem('conductor_user');
    const tenantStr = localStorage.getItem('conductor_tenant');
    return {
      user: userStr ? JSON.parse(userStr) : null,
      tenant: tenantStr ? JSON.parse(tenantStr) : null
    };
  }

  getAdminInfo() {
    const adminStr = localStorage.getItem('conductor_admin');
    const isAdmin = localStorage.getItem('conductor_is_admin') === 'true';
    return {
      admin: adminStr ? JSON.parse(adminStr) : null,
      isAdmin
    };
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
      throw new Error(errorData.Message || errorData.message || `HTTP ${response.status}`);
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
    }

    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : null,
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.error || errorData.Message || errorData.message || `HTTP ${response.status}`);
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

  async getModelRunnerEndpoint(id) {
    return this.request('GET', `/v1.0/modelrunnerendpoints/${id}`);
  }

  async createModelRunnerEndpoint(endpoint) {
    return this.request('POST', '/v1.0/modelrunnerendpoints', endpoint);
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

  async updateModelConfiguration(id, configuration) {
    return this.request('PUT', `/v1.0/modelconfigurations/${id}`, configuration);
  }

  async deleteModelConfiguration(id, tenantId = null) {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.request('DELETE', `/v1.0/modelconfigurations/${id}${query}`);
  }

  // Virtual Model Runner APIs
  async listVirtualModelRunners(params = {}) {
    const query = this.buildQueryString(params);
    return this.dedupedRequest(`listVirtualModelRunners:${query}`, 'GET', `/v1.0/virtualmodelrunners${query}`);
  }

  async getVirtualModelRunner(id) {
    return this.request('GET', `/v1.0/virtualmodelrunners/${id}`);
  }

  async createVirtualModelRunner(vmr) {
    return this.request('POST', '/v1.0/virtualmodelrunners', vmr);
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

  /**
   * Create a backup of all configuration data.
   * @returns {Promise<Object>} The backup package
   */
  async createBackup() {
    return this.adminRequest('GET', '/v1.0/backup');
  }

  /**
   * Restore configuration from a backup file.
   * @param {Object} backup - The parsed backup JSON
   * @param {Object} options - Restore options
   * @returns {Promise<Object>} Restore result
   */
  async restoreBackup(backup, options = {}) {
    return this.adminRequest('POST', '/v1.0/backup/restore', {
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
    return this.adminRequest('POST', '/v1.0/backup/validate', backup);
  }
}

export const api = new ConductorApi();
export default api;
