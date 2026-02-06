import React, { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useApp } from '../context/AppContext';

function Dashboard() {
  const { counts, fetchCounts, serverUrl, disconnect } = useApp();

  useEffect(() => {
    fetchCounts();
  }, [fetchCounts]);

  const cards = [
    { label: 'Tenants', count: counts.tenants, path: '/tenants', color: '#3b82f6', tooltip: 'Organizational units for grouping resources' },
    { label: 'Users', count: counts.users, path: '/users', color: '#10b981', tooltip: 'User accounts that authenticate via API credentials' },
    { label: 'Credentials', count: counts.credentials, path: '/credentials', color: '#f59e0b', tooltip: 'API bearer tokens used for authentication' },
    { label: 'Model Runner Endpoints', count: counts.modelRunnerEndpoints, path: '/endpoints', color: '#8b5cf6', tooltip: 'Backend inference servers (Ollama, OpenAI, etc.)' },
    { label: 'Model Definitions', count: counts.modelDefinitions, path: '/definitions', color: '#ec4899', tooltip: 'Model metadata describing available models' },
    { label: 'Model Configurations', count: counts.modelConfigurations, path: '/configurations', color: '#06b6d4', tooltip: 'Parameter presets applied to inference requests' },
    { label: 'Virtual Model Runners', count: counts.virtualModelRunners, path: '/vmr', color: '#f97316', tooltip: 'Virtualized API endpoints exposed to clients' }
  ];

  return (
    <div className="dashboard-view">
      <div className="view-header">
        <div>
          <h1>Dashboard</h1>
          <p className="server-url">Connected to: {serverUrl}</p>
        </div>
        <button className="btn-secondary" onClick={disconnect}>
          Disconnect
        </button>
      </div>

      <div className="stats-grid">
        {cards.map((card) => (
          <Link to={card.path} key={card.path} className="stat-card" title={card.tooltip}>
            <div className="stat-icon" style={{ backgroundColor: card.color + '20', color: card.color }}>
              <span>{card.count}</span>
            </div>
            <div className="stat-info">
              <h3>{card.label}</h3>
              <p>Total count</p>
            </div>
          </Link>
        ))}
      </div>

      <div className="dashboard-sections">
        <section className="dashboard-section">
          <h2>Quick Actions</h2>
          <div className="quick-actions">
            <Link to="/vmr" className="action-card" title="Create a new virtualized API endpoint that load-balances across model runner endpoints">
              <span className="action-icon">+</span>
              <span>Create Virtual Model Runner</span>
            </Link>
            <Link to="/endpoints" className="action-card" title="Register a new backend inference server (Ollama, OpenAI, etc.)">
              <span className="action-icon">+</span>
              <span>Add Model Runner Endpoint</span>
            </Link>
            <Link to="/configurations" className="action-card" title="Create a parameter preset to apply to inference requests">
              <span className="action-icon">+</span>
              <span>Create Model Configuration</span>
            </Link>
          </div>
        </section>
      </div>
    </div>
  );
}

export default Dashboard;
