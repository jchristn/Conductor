import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { useApp } from './context/AppContext';
import Sidebar from './components/Sidebar';
import PageHeader from './components/PageHeader';
import ErrorBanner from './components/ErrorBanner';
import Dashboard from './views/Dashboard';
import Tenants from './views/Tenants';
import Users from './views/Users';
import Credentials from './views/Credentials';
import ModelRunnerEndpoints from './views/ModelRunnerEndpoints';
import ModelDefinitions from './views/ModelDefinitions';
import ModelConfigurations from './views/ModelConfigurations';
import VirtualModelRunners from './views/VirtualModelRunners';
import ApiExplorer from './views/ApiExplorer';
import Administrators from './views/Administrators';
import BackupRestore from './views/BackupRestore';
import Login from './views/Login';

function App() {
  const { isConnected, isAdmin, currentUser, error, setError } = useApp();

  // Allow backup access for system admins OR users with IsAdmin flag
  const hasAdminAccess = isAdmin || currentUser?.IsAdmin;

  if (!isConnected) {
    return <Login />;
  }

  return (
    <div className="app">
      <Sidebar />
      <main className="main-content">
        <PageHeader />
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/tenants" element={<Tenants />} />
          <Route path="/users" element={<Users />} />
          <Route path="/credentials" element={<Credentials />} />
          <Route path="/endpoints" element={<ModelRunnerEndpoints />} />
          <Route path="/definitions" element={<ModelDefinitions />} />
          <Route path="/configurations" element={<ModelConfigurations />} />
          <Route path="/vmr" element={<VirtualModelRunners />} />
          <Route path="/api-explorer" element={<ApiExplorer />} />
          {isAdmin && <Route path="/administrators" element={<Administrators />} />}
          {hasAdminAccess && <Route path="/backup" element={<BackupRestore />} />}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
