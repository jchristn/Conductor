import React, { useState } from 'react';
import { useApp } from '../context/AppContext';

function Login() {
  const { loginWithCredential, loginWithApiKey, loginAsAdmin, loading, error, serverUrl, theme } = useApp();
  const [url, setUrl] = useState(serverUrl);
  const [authType, setAuthType] = useState('credential'); // 'credential', 'apikey', or 'admin'

  // Credential auth fields
  const [tenantId, setTenantId] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  // API key auth field
  const [apiKey, setApiKey] = useState('');

  // Admin auth fields
  const [adminEmail, setAdminEmail] = useState('');
  const [adminPassword, setAdminPassword] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (authType === 'credential') {
      await loginWithCredential(url, tenantId, email, password);
    } else if (authType === 'apikey') {
      await loginWithApiKey(url, apiKey);
    } else if (authType === 'admin') {
      await loginAsAdmin(url, adminEmail, adminPassword);
    }
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <div className="login-header">
          <img src={theme === 'dark' ? '/icon-light.png' : '/icon-dark.png'} alt="Conductor" className="login-logo" />
          <h1>Conductor</h1>
          <p>Virtual Model Runner Management</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">
          <div className="form-group">
            <label htmlFor="serverUrl">Server URL</label>
            <input
              type="url"
              id="serverUrl"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              placeholder="http://localhost:9000"
              required
            />
          </div>

          <div className="form-group">
            <label>Authentication Method</label>
            <div className="auth-type-selector auth-type-three">
              <button
                type="button"
                className={`auth-type-btn ${authType === 'credential' ? 'active' : ''}`}
                onClick={() => setAuthType('credential')}
              >
                User
              </button>
              <button
                type="button"
                className={`auth-type-btn ${authType === 'apikey' ? 'active' : ''}`}
                onClick={() => setAuthType('apikey')}
              >
                API Key
              </button>
              <button
                type="button"
                className={`auth-type-btn ${authType === 'admin' ? 'active' : ''}`}
                onClick={() => setAuthType('admin')}
              >
                Admin
              </button>
            </div>
          </div>

          {authType === 'credential' && (
            <>
              <div className="form-group">
                <label htmlFor="tenantId">Tenant ID</label>
                <input
                  type="text"
                  id="tenantId"
                  value={tenantId}
                  onChange={(e) => setTenantId(e.target.value)}
                  placeholder="ten_xxxxxxxx"
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="email">Email</label>
                <input
                  type="email"
                  id="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="user@example.com"
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="password">Password</label>
                <input
                  type="password"
                  id="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Enter your password"
                  required
                />
              </div>
            </>
          )}

          {authType === 'apikey' && (
            <div className="form-group">
              <label htmlFor="apiKey">API Key</label>
              <input
                type="password"
                id="apiKey"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder="Enter your API key"
                required
              />
            </div>
          )}

          {authType === 'admin' && (
            <>
              <div className="form-group">
                <label htmlFor="adminEmail">Administrator Email</label>
                <input
                  type="email"
                  id="adminEmail"
                  value={adminEmail}
                  onChange={(e) => setAdminEmail(e.target.value)}
                  placeholder="admin@conductor.local"
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="adminPassword">Administrator Password</label>
                <input
                  type="password"
                  id="adminPassword"
                  value={adminPassword}
                  onChange={(e) => setAdminPassword(e.target.value)}
                  placeholder="Enter admin password"
                  required
                />
              </div>
            </>
          )}

          {error && <div className="login-error">{error}</div>}

          <button type="submit" className="btn-primary" disabled={loading}>
            {loading ? 'Connecting...' : 'Login'}
          </button>
        </form>

      </div>
    </div>
  );
}

export default Login;
