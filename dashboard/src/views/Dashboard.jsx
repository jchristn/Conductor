import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import RequestHistoryChart from '../components/RequestHistoryChart';

function Dashboard() {
  const { api, counts, fetchCounts } = useApp();
  const [observability, setObservability] = useState(null);

  useEffect(() => {
    fetchCounts();
  }, [fetchCounts]);

  useEffect(() => {
    let cancelled = false;

    api.getObservabilityMetricsSummary()
      .then((result) => {
        if (!cancelled) {
          setObservability(result);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setObservability(null);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [api]);

  const cards = [
    { label: 'Tenants', count: counts.tenants, path: '/tenants', color: '#3b82f6', tooltip: 'Organizational units for grouping resources' },
    { label: 'Users', count: counts.users, path: '/users', color: '#10b981', tooltip: 'User accounts that authenticate via API credentials' },
    { label: 'Credentials', count: counts.credentials, path: '/credentials', color: '#f59e0b', tooltip: 'API bearer tokens used for authentication' },
    { label: 'Model Runner Endpoints', count: counts.modelRunnerEndpoints, path: '/endpoints', color: '#8b5cf6', tooltip: 'Backend inference servers (Ollama, OpenAI, vLLM, Gemini, etc.)' },
    { label: 'Endpoint Groups', count: counts.endpointGroups, path: '/endpoint-groups', color: '#14b8a6', tooltip: 'Reusable endpoint collections for virtual model runners' },
    { label: 'Model Definitions', count: counts.modelDefinitions, path: '/definitions', color: '#ec4899', tooltip: 'Model metadata describing available models' },
    { label: 'Model Configurations', count: counts.modelConfigurations, path: '/configurations', color: '#06b6d4', tooltip: 'Parameter presets applied to inference requests' },
    { label: 'Virtual Model Runners', count: counts.virtualModelRunners, path: '/vmr', color: '#f97316', tooltip: 'Virtualized API endpoints exposed to clients' }
  ];

  // Subordinate observability services shipped with the Docker Compose stack. The URLs are
  // built from the host the dashboard is served from so links work for remote access too;
  // ports match docker/compose.yaml. Override any entry if you deploy behind different hosts.
  const serviceHost = (typeof window !== 'undefined' && window.location && window.location.hostname) || 'localhost';
  const observabilityServices = [
    {
      name: 'Grafana',
      description: 'Dashboards for metrics, traces, and logs — the primary observability UI.',
      url: `http://${serviceHost}:3000`,
      credentials: 'No login required (anonymous Admin). Default account: admin / admin.',
      color: '#f97316'
    },
    {
      name: 'Prometheus',
      description: 'Metrics storage and PromQL query explorer.',
      url: `http://${serviceHost}:9090`,
      credentials: 'No authentication required.',
      color: '#e11d48'
    },
    {
      name: 'Tempo',
      description: 'Distributed trace storage. Best explored through Grafana.',
      url: `http://${serviceHost}:3200`,
      credentials: 'No authentication required.',
      color: '#8b5cf6'
    },
    {
      name: 'Loki',
      description: 'Log aggregation. Best explored through Grafana.',
      url: `http://${serviceHost}:3100`,
      credentials: 'No authentication required.',
      color: '#22c55e'
    }
  ];

  return (
    <div className="dashboard-view">
      <div className="view-header">
        <div>
          <h1>Dashboard</h1>
          <p className="view-subtitle">Overview of your Conductor environment with quick access to all resource types.</p>
        </div>
      </div>

      <RequestHistoryChart />

      {observability?.Overall && (
        <section className="dashboard-section">
          <div className="request-history-chart-header">
            <h2>Operational Signals</h2>
          </div>
          <div className="observability-grid">
            <div className="metric">
              <span className="metric-label">Total Requests</span>
              <span className="metric-value">{observability.Overall.TotalRequests?.toLocaleString?.() || 0}</span>
            </div>
            <div className="metric">
              <span className="metric-label">Denied Requests</span>
              <span className="metric-value">{observability.Overall.DeniedRequests?.toLocaleString?.() || 0}</span>
            </div>
            <div className="metric">
              <span className="metric-label">Session Hit Rate</span>
              <span className="metric-value">{observability.Overall.SessionAffinityHitRate || 0}%</span>
            </div>
            <div className="metric">
              <span className="metric-label">Route Decision P95</span>
              <span className="metric-value">{observability.Overall.RouteDecisionDurationMs?.P95 || 0} ms</span>
            </div>
            <div className="metric">
              <span className="metric-label">First Token P95</span>
              <span className="metric-value">{observability.Overall.FirstTokenTimeMs?.P95 || 0} ms</span>
            </div>
            <div className="metric">
              <span className="metric-label">Telemetry Freshness Failures</span>
              <span className="metric-value">{observability.Overall.TelemetryFreshnessFailures?.toLocaleString?.() || 0}</span>
            </div>
          </div>
        </section>
      )}

      <div className="dashboard-sections">
        <section className="dashboard-section">
          <h2>Quick Actions</h2>
          <div className="quick-actions">
            <Link to="/vmr" className="action-card" title="Create a new virtualized API endpoint that load-balances across model runner endpoints">
              <span className="action-icon">+</span>
              <span>Create Virtual Model Runner</span>
            </Link>
            <Link to="/endpoints" className="action-card" title="Register a new backend inference server (Ollama, OpenAI, vLLM, Gemini, etc.)">
              <span className="action-icon">+</span>
              <span>Add Model Runner Endpoint</span>
            </Link>
            <Link to="/endpoint-groups" className="action-card" title="Create a reusable collection of model runner endpoints">
              <span className="action-icon">+</span>
              <span>Create Endpoint Group</span>
            </Link>
            <Link to="/configurations" className="action-card" title="Create a parameter preset to apply to inference requests">
              <span className="action-icon">+</span>
              <span>Create Model Configuration</span>
            </Link>
          </div>
        </section>
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

      <section className="dashboard-section">
        <div className="request-history-chart-header">
          <h2>Observability &amp; Tooling</h2>
        </div>
        <p className="view-subtitle service-grid-subtitle">
          Companion services from the Conductor observability stack. Links use the current host and the default Docker Compose ports.
        </p>
        <div className="service-grid">
          {observabilityServices.map((service) => (
            <a
              key={service.name}
              href={service.url}
              target="_blank"
              rel="noopener noreferrer"
              className="service-card"
              title={`Open ${service.name} in a new tab`}
            >
              <div className="service-card-header">
                <span className="service-card-dot" style={{ backgroundColor: service.color }} />
                <h3>{service.name}</h3>
                <span className="service-card-external" aria-hidden="true">&#8599;</span>
              </div>
              <p className="service-card-desc">{service.description}</p>
              <div className="service-card-meta">
                <span className="service-card-meta-label">URL</span>
                <span className="service-card-url">{service.url}</span>
              </div>
              <div className="service-card-meta">
                <span className="service-card-meta-label">Credentials</span>
                <span className="service-card-cred">{service.credentials}</span>
              </div>
            </a>
          ))}
        </div>
      </section>
    </div>
  );
}

export default Dashboard;
