import React, { useState } from 'react';
import { useApp } from '../context/AppContext';

function Login() {
  const { loginWithCredential, loginWithApiKey, loading, error, serverUrl, theme } = useApp();
  const [url, setUrl] = useState(serverUrl);
  const [authType, setAuthType] = useState('credential'); // 'credential' or 'apikey'

  // Credential auth fields
  const [tenantId, setTenantId] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  // API key auth field
  const [apiKey, setApiKey] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (authType === 'credential') {
      await loginWithCredential(url, tenantId, email, password);
    } else {
      await loginWithApiKey(url, apiKey);
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
            <div className="auth-type-selector">
              <button
                type="button"
                className={`auth-type-btn ${authType === 'credential' ? 'active' : ''}`}
                onClick={() => setAuthType('credential')}
              >
                Credential
              </button>
              <button
                type="button"
                className={`auth-type-btn ${authType === 'apikey' ? 'active' : ''}`}
                onClick={() => setAuthType('apikey')}
              >
                API Key
              </button>
            </div>
          </div>

          {authType === 'credential' ? (
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
                  placeholder="admin@conductor"
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
          ) : (
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
