import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { useApp } from './context/AppContext';
import { OnboardingProvider } from './context/OnboardingContext';
import Sidebar from './components/Sidebar';
import PageHeader from './components/PageHeader';
import ErrorBanner from './components/ErrorBanner';
import ControlTooltipHydrator from './components/ControlTooltipHydrator';
import Tour from './components/Tour';
import SetupWizard from './components/SetupWizard';
import Dashboard from './views/Dashboard';
import HubView from './views/hubs/HubView';
import LegacyRedirect from './components/LegacyRedirect';
import { HUB_ITEMS, LEGACY_REDIRECTS } from './config/navConfig';
import Login from './views/Login';

function App() {
  const { isConnected, error, setError } = useApp();

  if (!isConnected) {
    return <Login />;
  }

  return (
    <OnboardingProvider>
      <ControlTooltipHydrator />
      <div className="app">
        <Sidebar />
        <main className="main-content">
          <PageHeader />
          <ErrorBanner message={error} onDismiss={() => setError(null)} />
          <Routes>
            <Route path="/" element={<Dashboard />} />
            {HUB_ITEMS.map((item) => (
              <Route key={item.key} path={item.path} element={<HubView key={item.key} hubKey={item.key} />} />
            ))}
            {LEGACY_REDIRECTS.map((redirect) => (
              <Route key={redirect.from} path={redirect.from} element={<LegacyRedirect to={redirect.to} tab={redirect.tab} />} />
            ))}
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </main>
        <Tour />
        <SetupWizard />
      </div>
    </OnboardingProvider>
  );
}

export default App;
