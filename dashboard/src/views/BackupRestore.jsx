import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import Modal from '../components/Modal';

function BackupRestore() {
  const { api, isAdmin, currentUser, setError } = useApp();

  // Allow access for system admins OR users with IsAdmin flag
  const hasAdminAccess = isAdmin || currentUser?.IsAdmin;
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(null);

  // Restore state
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
    setBackupData(null);
    setValidationResult(null);
    setRestoreResult(null);
    setRestoreOptions({
      ConflictResolution: 'Skip',
      RestoreAdministrators: false,
      RestoreCredentials: true,
      TenantFilter: []
    });
    // Reset file input
    const fileInput = document.getElementById('backupFile');
    if (fileInput) fileInput.value = '';
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
        <div className="info-banner success">
          <span>{success}</span>
          <button className="btn-icon" onClick={() => setSuccess(null)} title="Dismiss">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" />
            </svg>
          </button>
        </div>
      )}

      <div className="backup-sections">
        {/* Create Backup Section */}
        <section className="backup-section">
          <h2>Create Backup</h2>
          <p className="section-description">
            Download a complete backup of all configuration data including tenants, users,
            credentials, model definitions, model configurations, model runner endpoints,
            virtual model runners, and administrators.
          </p>
          <div className="info-banner warning">
            <svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
              <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
            </svg>
            <span>The backup file contains sensitive data including API keys and passwords. Store it securely.</span>
          </div>
          <button onClick={handleBackup} disabled={loading} className="btn-primary">
            {loading ? 'Creating Backup...' : 'Download Backup'}
          </button>
        </section>

        {/* Restore Section */}
        <section className="backup-section">
          <h2>Restore from Backup</h2>

          <div className="form-group">
            <label title="Choose a previously exported JSON backup file to restore">Select Backup File</label>
            <div className="file-input-wrapper">
              <input
                type="file"
                id="backupFile"
                accept=".json"
                onChange={handleFileSelect}
                className="file-input-hidden"
              />
              <button
                type="button"
                className="btn-secondary"
                onClick={() => document.getElementById('backupFile').click()}
              >
                Choose File
              </button>
              <span className="file-input-name">
                {backupData ? 'File loaded' : 'No file chosen'}
              </span>
            </div>
          </div>

          {/* Validation Results */}
          {validationResult && (
            <div className={`info-banner ${validationResult.IsValid ? 'success' : 'warning'}`}>
              <div className="validation-content">
                <strong>{validationResult.IsValid ? 'Backup Valid' : 'Validation Warnings'}</strong>
                {validationResult.Summary && (
                  <div className="backup-summary-grid">
                    <span>{validationResult.Summary.TenantCount} tenants</span>
                    <span>{validationResult.Summary.UserCount} users</span>
                    <span>{validationResult.Summary.CredentialCount} credentials</span>
                    <span>{validationResult.Summary.ModelDefinitionCount} model definitions</span>
                    <span>{validationResult.Summary.ModelConfigurationCount} model configurations</span>
                    <span>{validationResult.Summary.ModelRunnerEndpointCount} endpoints</span>
                    <span>{validationResult.Summary.VirtualModelRunnerCount} virtual runners</span>
                    <span>{validationResult.Summary.AdministratorCount} administrators</span>
                  </div>
                )}
                {validationResult.Conflicts && validationResult.Conflicts.length > 0 && (
                  <details className="conflicts-details">
                    <summary>{validationResult.Conflicts.length} existing entities will be affected</summary>
                    <ul>
                      {validationResult.Conflicts.slice(0, 10).map((conflict, i) => (
                        <li key={i}>{conflict}</li>
                      ))}
                      {validationResult.Conflicts.length > 10 && (
                        <li>... and {validationResult.Conflicts.length - 10} more</li>
                      )}
                    </ul>
                  </details>
                )}
              </div>
            </div>
          )}

          {/* Restore Options */}
          {backupData && (
            <>
              <div className="form-group">
                <label htmlFor="conflictResolution" title="How to handle entities that already exist in the current system">Conflict Resolution</label>
                <select
                  id="conflictResolution"
                  value={restoreOptions.ConflictResolution}
                  onChange={(e) => setRestoreOptions({...restoreOptions, ConflictResolution: e.target.value})}
                >
                  <option value="Skip">Skip existing (keep current data)</option>
                  <option value="Overwrite">Overwrite existing (replace with backup)</option>
                  <option value="Fail">Fail on conflict (abort restore)</option>
                </select>
              </div>

              <div className="form-group checkbox-group">
                <label title="Include API keys and bearer tokens from the backup - disable to skip sensitive credential data">
                  <input
                    type="checkbox"
                    checked={restoreOptions.RestoreCredentials}
                    onChange={(e) => setRestoreOptions({...restoreOptions, RestoreCredentials: e.target.checked})}
                  />
                  Restore Credentials (API keys and bearer tokens)
                </label>
              </div>

              <div className="form-group checkbox-group">
                <label title="Include administrator accounts from the backup - use with caution as this may grant dashboard access">
                  <input
                    type="checkbox"
                    checked={restoreOptions.RestoreAdministrators}
                    onChange={(e) => setRestoreOptions({...restoreOptions, RestoreAdministrators: e.target.checked})}
                  />
                  Restore Administrators
                </label>
              </div>

              <div className="button-row">
                <button onClick={handleClearRestore} disabled={loading} className="btn-secondary">
                  Clear
                </button>
                <button onClick={() => setShowRestoreConfirm(true)} disabled={loading} className="btn-primary btn-danger">
                  Begin Restore
                </button>
              </div>
            </>
          )}

          {/* Restore Results */}
          {restoreResult && (
            <div className={`info-banner ${restoreResult.Success ? 'success' : 'error'}`}>
              <div className="restore-result-content">
                <strong>Restore {restoreResult.Success ? 'Completed' : 'Failed'}</strong>
                {restoreResult.Summary && (
                  <table className="restore-table">
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
                      {[
                        ['Tenants', restoreResult.Summary.Tenants],
                        ['Users', restoreResult.Summary.Users],
                        ['Credentials', restoreResult.Summary.Credentials],
                        ['Model Definitions', restoreResult.Summary.ModelDefinitions],
                        ['Model Configurations', restoreResult.Summary.ModelConfigurations],
                        ['Endpoints', restoreResult.Summary.ModelRunnerEndpoints],
                        ['Virtual Runners', restoreResult.Summary.VirtualModelRunners],
                        ['Administrators', restoreResult.Summary.Administrators]
                      ].map(([name, counts]) => (
                        <tr key={name}>
                          <td>{name}</td>
                          <td>{counts?.Created || 0}</td>
                          <td>{counts?.Updated || 0}</td>
                          <td>{counts?.Skipped || 0}</td>
                          <td>{counts?.Failed || 0}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
          )}
        </section>
      </div>

      {/* Restore Confirmation Modal */}
      <Modal
        isOpen={showRestoreConfirm}
        onClose={() => setShowRestoreConfirm(false)}
        title="Confirm Restore"
      >
        <p>Are you sure you want to restore from this backup?</p>
        <p className="form-help">
          Conflict resolution: <strong>{restoreOptions.ConflictResolution}</strong>
        </p>
        {restoreOptions.ConflictResolution === 'Overwrite' && (
          <div className="info-banner warning" style={{ marginTop: '12px' }}>
            <span>Existing data will be overwritten with backup data.</span>
          </div>
        )}
        <div className="form-actions">
          <button onClick={() => setShowRestoreConfirm(false)} className="btn-secondary">
            Cancel
          </button>
          <button onClick={handleRestore} className="btn-primary btn-danger">
            Confirm Restore
          </button>
        </div>
      </Modal>
    </div>
  );
}

export default BackupRestore;
