import React, { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useApp } from '../context/AppContext';

function Dashboard() {
  const { counts, fetchCounts, serverUrl, disconnect } = useApp();

  useEffect(() => {
    fetchCounts();
  }, [fetchCounts]);

  const cards = [
    { label: 'Tenants', count: counts.tenants, path: '/tenants', color: '#3b82f6' },
    { label: 'Users', count: counts.users, path: '/users', color: '#10b981' },
    { label: 'Credentials', count: counts.credentials, path: '/credentials', color: '#f59e0b' },
    { label: 'Model Runner Endpoints', count: counts.modelRunnerEndpoints, path: '/endpoints', color: '#8b5cf6' },
    { label: 'Model Definitions', count: counts.modelDefinitions, path: '/definitions', color: '#ec4899' },
    { label: 'Model Configurations', count: counts.modelConfigurations, path: '/configurations', color: '#06b6d4' },
    { label: 'Virtual Model Runners', count: counts.virtualModelRunners, path: '/vmr', color: '#f97316' }
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
          <Link to={card.path} key={card.path} className="stat-card">
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
            <Link to="/vmr" className="action-card">
              <span className="action-icon">+</span>
              <span>Create Virtual Model Runner</span>
            </Link>
            <Link to="/endpoints" className="action-card">
              <span className="action-icon">+</span>
              <span>Add Model Runner Endpoint</span>
            </Link>
            <Link to="/configurations" className="action-card">
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
