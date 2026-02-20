import React, { createContext, useContext, useState, useCallback, useEffect, useRef } from 'react';
import api from '../api/api';

const AppContext = createContext(null);

export function AppProvider({ children }) {
  const [serverUrl, setServerUrl] = useState(() =>
    localStorage.getItem('conductor_server_url') || window.CONDUCTOR_SERVER_URL || 'http://localhost:9000'
  );
  const [isConnected, setIsConnected] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  // Theme state
  const [theme, setTheme] = useState(() =>
    localStorage.getItem('conductor_theme') || 'light'
  );

  // Apply theme to document
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('conductor_theme', theme);
  }, [theme]);

  const toggleTheme = useCallback(() => {
    setTheme(prev => prev === 'light' ? 'dark' : 'light');
  }, []);

  // User and tenant info from login
  const [currentUser, setCurrentUser] = useState(() => {
    const { user } = api.getUserInfo();
    return user;
  });
  const [currentTenant, setCurrentTenant] = useState(() => {
    const { tenant } = api.getUserInfo();
    return tenant;
  });

  // Admin info
  const [currentAdmin, setCurrentAdmin] = useState(() => {
    const { admin } = api.getAdminInfo();
    return admin;
  });
  const [isAdmin, setIsAdmin] = useState(() => {
    const { isAdmin } = api.getAdminInfo();
    return isAdmin;
  });

  // Entity counts for dashboard
  const [counts, setCounts] = useState({
    tenants: 0,
    users: 0,
    credentials: 0,
    modelRunnerEndpoints: 0,
    modelDefinitions: 0,
    modelConfigurations: 0,
    virtualModelRunners: 0,
    administrators: 0
  });

  // Auto-dismiss errors after 5 seconds
  useEffect(() => {
    if (error) {
      const timer = setTimeout(() => setError(null), 5000);
      return () => clearTimeout(timer);
    }
  }, [error]);

  const loginWithCredential = useCallback(async (url, tenantId, email, password) => {
    try {
      setLoading(true);
      api.setBaseUrl(url);
      const response = await api.loginWithCredential(tenantId, email, password);
      if (response.Success) {
        setServerUrl(url);
        setCurrentUser(response.User);
        setCurrentTenant(response.Tenant);
        setIsConnected(true);
        setError(null);
        return true;
      } else {
        setError('Login failed');
        return false;
      }
    } catch (err) {
      setError('Login failed: ' + err.message);
      setIsConnected(false);
      return false;
    } finally {
      setLoading(false);
    }
  }, []);

  const loginWithApiKey = useCallback(async (url, apiKey) => {
    try {
      setLoading(true);
      api.setBaseUrl(url);
      const response = await api.loginWithApiKey(apiKey);
      if (response.Success) {
        setServerUrl(url);
        if (response.IsAdmin) {
          // Admin API key login
          setCurrentUser(null);
          setCurrentTenant(null);
          setIsAdmin(true);
          setCurrentAdmin({ ApiKey: apiKey });
        } else {
          // Regular user API key login
          setCurrentUser(response.User);
          setCurrentTenant(response.Tenant);
          setIsAdmin(false);
          setCurrentAdmin(null);
        }
        setIsConnected(true);
        setError(null);
        return true;
      } else {
        setError('Login failed: Invalid API key');
        return false;
      }
    } catch (err) {
      setError('Login failed: ' + err.message);
      setIsConnected(false);
      return false;
    } finally {
      setLoading(false);
    }
  }, []);

  const loginAsAdmin = useCallback(async (url, email, password) => {
    try {
      setLoading(true);
      api.setBaseUrl(url);
      const response = await api.loginAsAdmin(email, password);
      if (response.Success) {
        setServerUrl(url);
        setCurrentAdmin(response.Administrator);
        setIsAdmin(true);
        setCurrentUser(null);
        setCurrentTenant(null);
        setIsConnected(true);
        setError(null);
        return true;
      } else {
        setError('Login failed');
        return false;
      }
    } catch (err) {
      setError('Login failed: ' + err.message);
      setIsConnected(false);
      return false;
    } finally {
      setLoading(false);
    }
  }, []);

  const disconnect = useCallback(() => {
    api.clearAuth();
    setIsConnected(false);
    setCurrentUser(null);
    setCurrentTenant(null);
    setCurrentAdmin(null);
    setIsAdmin(false);
  }, []);

  const fetchCounts = useCallback(async () => {
    try {
      // For admin users, fetch administrators count too
      if (isAdmin) {
        const [
          tenants,
          admins
        ] = await Promise.all([
          api.listTenants({ maxResults: 1 }),
          api.listAdministrators({ maxResults: 1 })
        ]);

        setCounts({
          tenants: tenants.TotalCount || tenants.Data?.length || 0,
          users: 0,
          credentials: 0,
          modelRunnerEndpoints: 0,
          modelDefinitions: 0,
          modelConfigurations: 0,
          virtualModelRunners: 0,
          administrators: admins.TotalCount || admins.Data?.length || 0
        });
      } else {
        const [
          tenants,
          users,
          credentials,
          endpoints,
          definitions,
          configurations,
          vmrs
        ] = await Promise.all([
          api.listTenants({ maxResults: 1 }),
          api.listUsers({ maxResults: 1 }),
          api.listCredentials({ maxResults: 1 }),
          api.listModelRunnerEndpoints({ maxResults: 1 }),
          api.listModelDefinitions({ maxResults: 1 }),
          api.listModelConfigurations({ maxResults: 1 }),
          api.listVirtualModelRunners({ maxResults: 1 })
        ]);

        setCounts({
          tenants: tenants.TotalCount || tenants.Data?.length || 0,
          users: users.TotalCount || users.Data?.length || 0,
          credentials: credentials.TotalCount || credentials.Data?.length || 0,
          modelRunnerEndpoints: endpoints.TotalCount || endpoints.Data?.length || 0,
          modelDefinitions: definitions.TotalCount || definitions.Data?.length || 0,
          modelConfigurations: configurations.TotalCount || configurations.Data?.length || 0,
          virtualModelRunners: vmrs.TotalCount || vmrs.Data?.length || 0,
          administrators: 0
        });
      }
    } catch (err) {
      console.error('Failed to fetch counts:', err);
    }
  }, [isAdmin]);

  const value = {
    serverUrl,
    isConnected,
    loading,
    error,
    setError,
    counts,
    currentUser,
    currentTenant,
    currentAdmin,
    isAdmin,
    theme,
    toggleTheme,
    loginWithCredential,
    loginWithApiKey,
    loginAsAdmin,
    disconnect,
    fetchCounts,
    api
  };

  return <AppContext.Provider value={value}>{children}</AppContext.Provider>;
}

export function useApp() {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error('useApp must be used within AppProvider');
  }
  return context;
}

export default AppContext;
