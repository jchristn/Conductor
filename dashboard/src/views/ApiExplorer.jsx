import React, { useState, useEffect, useCallback } from 'react';
import { useApp } from '../context/AppContext';
import { useApiExplorer } from '../hooks/useApiExplorer';
import ApiExplorerConfig from '../components/ApiExplorerConfig';
import ApiExplorerRequest from '../components/ApiExplorerRequest';
import ApiExplorerResponse from '../components/ApiExplorerResponse';

/**
 * API Explorer page component.
 * Allows users to test and interact with Virtual Model Runners through OpenAI or Ollama compatible APIs.
 */
function ApiExplorer() {
  const { api, setError, serverUrl } = useApp();
  const [vmrs, setVmrs] = useState([]);
  const [definitions, setDefinitions] = useState([]);
  const [credentials, setCredentials] = useState([]);
  const [loading, setLoading] = useState(true);
  const [configCollapsed, setConfigCollapsed] = useState(false);

  const explorer = useApiExplorer(serverUrl);

  // Load saved VMR ID on mount
  useEffect(() => {
    const savedConfig = localStorage.getItem('conductor_api_explorer');
    if (savedConfig) {
      try {
        const config = JSON.parse(savedConfig);
        if (config.selectedVmrId && vmrs.length > 0) {
          const savedVmr = vmrs.find(v => v.Id === config.selectedVmrId);
          if (savedVmr) {
            explorer.setSelectedVmr(savedVmr);
          }
        }
      } catch {
        // Ignore parse errors
      }
    }
  }, [vmrs, explorer]);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [vmrResult, definitionsResult, credentialsResult] = await Promise.all([
        api.listVirtualModelRunners({ maxResults: 1000 }),
        api.listModelDefinitions({ maxResults: 1000 }),
        api.listCredentials({ maxResults: 1000 })
      ]);
      setVmrs(vmrResult.Data || []);
      setDefinitions(definitionsResult.Data || []);
      setCredentials(credentialsResult.Data || []);
    } catch (err) {
      setError('Failed to fetch data: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Get model definitions for the selected VMR
  const vmrDefinitions = explorer.selectedVmr
    ? definitions.filter(d => (explorer.selectedVmr.ModelDefinitionIds || []).includes(d.Id))
    : [];

  // Get credentials for the selected VMR's tenant
  // If VMR has no tenant, show all credentials; otherwise filter by tenant
  const vmrCredentials = explorer.selectedVmr
    ? (explorer.selectedVmr.TenantId
        ? credentials.filter(c => c.TenantId === explorer.selectedVmr.TenantId)
        : credentials)
    : [];

  return (
    <div className="view-container api-explorer">
      <div className="view-header">
        <h1>API Explorer</h1>
        <div className="view-actions">
          <button
            className="btn-icon"
            onClick={() => setConfigCollapsed(!configCollapsed)}
            title={configCollapsed ? 'Expand configuration' : 'Collapse configuration'}
          >
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              {configCollapsed ? (
                <path fillRule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clipRule="evenodd" />
              ) : (
                <path fillRule="evenodd" d="M14.707 12.707a1 1 0 01-1.414 0L10 9.414l-3.293 3.293a1 1 0 01-1.414-1.414l4-4a1 1 0 011.414 0l4 4a1 1 0 010 1.414z" clipRule="evenodd" />
              )}
            </svg>
          </button>
          <button className="btn-icon" onClick={fetchData} title="Refresh VMRs" disabled={loading}>
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" className={loading ? 'spinning' : ''}>
              <path fillRule="evenodd" d="M4 2a1 1 0 011 1v2.101a7.002 7.002 0 0111.601 2.566 1 1 0 11-1.885.666A5.002 5.002 0 005.999 7H9a1 1 0 010 2H4a1 1 0 01-1-1V3a1 1 0 011-1zm.008 9.057a1 1 0 011.276.61A5.002 5.002 0 0014.001 13H11a1 1 0 110-2h5a1 1 0 011 1v5a1 1 0 11-2 0v-2.101a7.002 7.002 0 01-11.601-2.566 1 1 0 01.61-1.276z" clipRule="evenodd" />
            </svg>
          </button>
        </div>
      </div>

      <ApiExplorerConfig
        explorer={explorer}
        vmrs={vmrs}
        vmrDefinitions={vmrDefinitions}
        vmrCredentials={vmrCredentials}
        loading={loading}
        collapsed={configCollapsed}
      />

      <div className="api-explorer-main">
        <ApiExplorerRequest explorer={explorer} credentials={vmrCredentials} />
        <ApiExplorerResponse explorer={explorer} credentials={vmrCredentials} />
      </div>
    </div>
  );
}

export default ApiExplorer;
