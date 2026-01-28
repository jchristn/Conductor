import React, { useState } from 'react';
import { useApp } from '../context/AppContext';

function BackupRestore() {
  const { api, isAdmin, currentUser, setError } = useApp();

  // Allow access for system admins OR users with IsAdmin flag
  const hasAdminAccess = isAdmin || currentUser?.IsAdmin;
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(null);

  // Restore state
  const [restoreFile, setRestoreFile] = useState(null);
  const [backupData, setBackupData] = useState(null);
  const [restoreOptions, setRestoreOptions] = useState({
    ConflictResolution: 'Skip',
    RestoreAdministrators: false,
    RestoreCredentials: true,
    TenantFilter: []
  });
  const [validationResult, setValidationResult] = useState(null);
  const [restoreResult, setRestoreResult] = useState(null);
  const [showRestoreConfirm, setShowRestoreConfirm] = useState(false);

  // Backup handler
  const handleBackup = async () => {
    setLoading(true);
    setError(null);
    setSuccess(null);

    try {
      const backup = await api.createBackup();
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      const filename = `conductor-backup-${timestamp}.json`;

      // Create download
      const jsonString = JSON.stringify(backup, null, 2);
      const blob = new Blob([jsonString], { type: 'application/json' });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      setSuccess(`Backup downloaded successfully: ${filename}`);
    } catch (err) {
      setError('Failed to create backup: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  // File selection handler
  const handleFileSelect = async (event) => {
    const file = event.target.files[0];
    if (!file) return;

    setRestoreFile(file);
    setBackupData(null);
    setValidationResult(null);
    setRestoreResult(null);
    setError(null);
    setSuccess(null);

    try {
      const text = await file.text();
      const backup = JSON.parse(text);
      setBackupData(backup);

      // Validate the backup
      setLoading(true);
      const validation = await api.validateBackup(backup);
      setValidationResult(validation);
    } catch (err) {
      setError('Invalid backup file: ' + err.message);
      setRestoreFile(null);
    } finally {
      setLoading(false);
    }
  };

  // Restore handler
  const handleRestore = async () => {
    if (!backupData) return;

    setLoading(true);
    setError(null);
    setSuccess(null);
    setShowRestoreConfirm(false);

    try {
      const result = await api.restoreBackup(backupData, restoreOptions);
      setRestoreResult(result);

      if (result.Success) {
        setSuccess('Restore completed successfully.');
      } else {
        setError(result.ErrorMessage || 'Restore failed.');
      }
    } catch (err) {
      setError('Failed to restore: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  // Reset restore form
  const handleClearRestore = () => {
    setRestoreFile(null);
    setBackupData(null);
    setValidationResult(null);
    setRestoreResult(null);
    setRestoreOptions({
      ConflictResolution: 'Skip',
      RestoreAdministrators: false,
      RestoreCredentials: true,
      TenantFilter: []
    });
  };

  if (!hasAdminAccess) {
    return (
      <div className="view-container">
        <div className="error-banner">
          Access denied. Global administrator privileges required.
        </div>
      </div>
    );
  }

  return (
    <div className="view-container">
      <div className="view-header">
        <h1>Backup & Restore</h1>
      </div>

      {success && (
        <div className="success-banner">
          {success}
          <button className="dismiss-btn" onClick={() => setSuccess(null)}>
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" />
            </svg>
          </button>
        </div>
      )}

      <div className="backup-restore-container">
        {/* Backup Section */}
        <div className="card">
          <div className="card-header">
            <h2>Create Backup</h2>
          </div>
          <div className="card-body">
            <p className="text-muted">
              Download a complete backup of all configuration data including tenants, users,
              credentials, model definitions, model configurations, model runner endpoints,
              virtual model runners, and administrators.
            </p>
            <div className="warning-box">
              <svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
              </svg>
              <span>The backup file contains sensitive data including API keys and hashed passwords. Store it securely.</span>
            </div>
            <button
              onClick={handleBackup}
              disabled={loading}
              className="btn-primary"
            >
              {loading ? 'Creating Backup...' : 'Download Backup'}
            </button>
          </div>
        </div>

        {/* Restore Section */}
        <div className="card">
          <div className="card-header">
            <h2>Restore from Backup</h2>
          </div>
          <div className="card-body">
            {/* File Upload */}
            <div className="form-group">
              <label>Select Backup File</label>
              <input
                type="file"
                accept=".json"
                onChange={handleFileSelect}
                className="file-input"
              />
            </div>

            {/* Validation Results */}
            {validationResult && (
              <div className={`validation-result ${validationResult.IsValid ? 'valid' : 'warning'}`}>
                <h3>
                  {validationResult.IsValid ? 'Backup Valid' : 'Validation Warnings'}
                </h3>
                {validationResult.Summary && (
                  <div className="backup-summary">
                    <p><strong>Backup Contents:</strong></p>
                    <ul>
                      <li>{validationResult.Summary.TenantCount} tenants</li>
                      <li>{validationResult.Summary.UserCount} users</li>
                      <li>{validationResult.Summary.CredentialCount} credentials</li>
                      <li>{validationResult.Summary.ModelDefinitionCount} model definitions</li>
                      <li>{validationResult.Summary.ModelConfigurationCount} model configurations</li>
                      <li>{validationResult.Summary.ModelRunnerEndpointCount} model runner endpoints</li>
                      <li>{validationResult.Summary.VirtualModelRunnerCount} virtual model runners</li>
                      <li>{validationResult.Summary.AdministratorCount} administrators</li>
                    </ul>
                  </div>
                )}
                {validationResult.Conflicts && validationResult.Conflicts.length > 0 && (
                  <div className="conflicts-list">
                    <p><strong>Existing Entities (Conflicts):</strong></p>
                    <ul>
                      {validationResult.Conflicts.slice(0, 10).map((conflict, i) => (
                        <li key={i}>{conflict}</li>
                      ))}
                      {validationResult.Conflicts.length > 10 && (
                        <li>... and {validationResult.Conflicts.length - 10} more</li>
                      )}
                    </ul>
                  </div>
                )}
              </div>
            )}

            {/* Restore Options */}
            {backupData && (
              <div className="restore-options">
                <h3>Restore Options</h3>

                <div className="form-group">
                  <label>Conflict Resolution</label>
                  <select
                    value={restoreOptions.ConflictResolution}
                    onChange={(e) => setRestoreOptions({...restoreOptions, ConflictResolution: e.target.value})}
                  >
                    <option value="Skip">Skip existing (keep current data)</option>
                    <option value="Overwrite">Overwrite existing (replace with backup)</option>
                    <option value="Fail">Fail on conflict (abort restore)</option>
                  </select>
                </div>

                <div className="checkbox-group">
                  <label>
                    <input
                      type="checkbox"
                      checked={restoreOptions.RestoreCredentials}
                      onChange={(e) => setRestoreOptions({...restoreOptions, RestoreCredentials: e.target.checked})}
                    />
                    Restore Credentials (API keys and bearer tokens)
                  </label>
                </div>

                <div className="checkbox-group">
                  <label>
                    <input
                      type="checkbox"
                      checked={restoreOptions.RestoreAdministrators}
                      onChange={(e) => setRestoreOptions({...restoreOptions, RestoreAdministrators: e.target.checked})}
                    />
                    Restore Administrators
                  </label>
                </div>

                <div className="button-group">
                  <button
                    onClick={() => setShowRestoreConfirm(true)}
                    disabled={loading}
                    className="btn-primary btn-warning"
                  >
                    Begin Restore
                  </button>
                  <button
                    onClick={handleClearRestore}
                    disabled={loading}
                    className="btn-secondary"
                  >
                    Clear
                  </button>
                </div>
              </div>
            )}

            {/* Restore Results */}
            {restoreResult && (
              <div className={`restore-result ${restoreResult.Success ? 'success' : 'error'}`}>
                <h3>Restore Summary</h3>
                {restoreResult.Summary && (
                  <table className="restore-summary-table">
                    <thead>
                      <tr>
                        <th>Entity</th>
                        <th>Created</th>
                        <th>Updated</th>
                        <th>Skipped</th>
                        <th>Failed</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr>
                        <td>Tenants</td>
                        <td className="created">{restoreResult.Summary.Tenants?.Created || 0}</td>
                        <td className="updated">{restoreResult.Summary.Tenants?.Updated || 0}</td>
                        <td className="skipped">{restoreResult.Summary.Tenants?.Skipped || 0}</td>
                        <td className="failed">{restoreResult.Summary.Tenants?.Failed || 0}</td>
                      </tr>
                      <tr>
                        <td>Users</td>
                        <td className="created">{restoreResult.Summary.Users?.Created || 0}</td>
                        <td className="updated">{restoreResult.Summary.Users?.Updated || 0}</td>
                        <td className="skipped">{restoreResult.Summary.Users?.Skipped || 0}</td>
                        <td className="failed">{restoreResult.Summary.Users?.Failed || 0}</td>
                      </tr>
                      <tr>
                        <td>Credentials</td>
                        <td className="created">{restoreResult.Summary.Credentials?.Created || 0}</td>
                        <td className="updated">{restoreResult.Summary.Credentials?.Updated || 0}</td>
                        <td className="skipped">{restoreResult.Summary.Credentials?.Skipped || 0}</td>
                        <td className="failed">{restoreResult.Summary.Credentials?.Failed || 0}</td>
                      </tr>
                      <tr>
                        <td>Model Definitions</td>
                        <td className="created">{restoreResult.Summary.ModelDefinitions?.Created || 0}</td>
                        <td className="updated">{restoreResult.Summary.ModelDefinitions?.Updated || 0}</td>
                        <td className="skipped">{restoreResult.Summary.ModelDefinitions?.Skipped || 0}</td>
                        <td className="failed">{restoreResult.Summary.ModelDefinitions?.Failed || 0}</td>
                      </tr>
                      <tr>
                        <td>Model Configurations</td>
                        <td className="created">{restoreResult.Summary.ModelConfigurations?.Created || 0}</td>
                        <td className="updated">{restoreResult.Summary.ModelConfigurations?.Updated || 0}</td>
                        <td className="skipped">{restoreResult.Summary.ModelConfigurations?.Skipped || 0}</td>
                        <td className="failed">{restoreResult.Summary.ModelConfigurations?.Failed || 0}</td>
                      </tr>
                      <tr>
                        <td>Model Runner Endpoints</td>
                        <td className="created">{restoreResult.Summary.ModelRunnerEndpoints?.Created || 0}</td>
                        <td className="updated">{restoreResult.Summary.ModelRunnerEndpoints?.Updated || 0}</td>
                        <td className="skipped">{restoreResult.Summary.ModelRunnerEndpoints?.Skipped || 0}</td>
                        <td className="failed">{restoreResult.Summary.ModelRunnerEndpoints?.Failed || 0}</td>
                      </tr>
                      <tr>
                        <td>Virtual Model Runners</td>
                        <td className="created">{restoreResult.Summary.VirtualModelRunners?.Created || 0}</td>
                        <td className="updated">{restoreResult.Summary.VirtualModelRunners?.Updated || 0}</td>
                        <td className="skipped">{restoreResult.Summary.VirtualModelRunners?.Skipped || 0}</td>
                        <td className="failed">{restoreResult.Summary.VirtualModelRunners?.Failed || 0}</td>
                      </tr>
                      <tr>
                        <td>Administrators</td>
                        <td className="created">{restoreResult.Summary.Administrators?.Created || 0}</td>
                        <td className="updated">{restoreResult.Summary.Administrators?.Updated || 0}</td>
                        <td className="skipped">{restoreResult.Summary.Administrators?.Skipped || 0}</td>
                        <td className="failed">{restoreResult.Summary.Administrators?.Failed || 0}</td>
                      </tr>
                    </tbody>
                  </table>
                )}
                {restoreResult.Warnings && restoreResult.Warnings.length > 0 && (
                  <div className="warnings-list">
                    <p><strong>Warnings:</strong></p>
                    <ul>
                      {restoreResult.Warnings.map((warning, i) => (
                        <li key={i}>{warning}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Restore Confirmation Modal */}
      {showRestoreConfirm && (
        <div className="modal-overlay" onClick={() => setShowRestoreConfirm(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Confirm Restore</h2>
              <button className="modal-close" onClick={() => setShowRestoreConfirm(false)}>
                <svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
                  <path d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" />
                </svg>
              </button>
            </div>
            <div className="modal-body">
              <p>Are you sure you want to restore from this backup?</p>
              <p className="text-muted">
                Conflict resolution: <strong>{restoreOptions.ConflictResolution}</strong>
              </p>
              {restoreOptions.ConflictResolution === 'Overwrite' && (
                <p className="warning-text">
                  Warning: Existing data will be overwritten with backup data.
                </p>
              )}
            </div>
            <div className="form-actions">
              <button
                onClick={() => setShowRestoreConfirm(false)}
                className="btn-secondary"
              >
                Cancel
              </button>
              <button
                onClick={handleRestore}
                className="btn-primary btn-warning"
              >
                Confirm Restore
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default BackupRestore;
