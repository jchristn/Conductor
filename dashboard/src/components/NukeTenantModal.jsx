import React, { useState } from 'react';
import Modal from './Modal';
import { useApp } from '../context/AppContext';

// Tenants whose id or name is the reserved 'default' must never be purged.
function isProtectedTenant(tenant) {
  if (!tenant) return true;
  const id = String(tenant.Id || '').toLowerCase();
  const name = String(tenant.Name || '').toLowerCase();
  return id === 'default' || name === 'default';
}

/**
 * Destructive "nuke" (purge) modal for a tenant. The operator must type the tenant's exact Id
 * before the purge button enables. While the purge runs the modal cannot be closed and shows a
 * "purging" state; on success the returned TenantPurgeReport is rendered as a per-category
 * checklist with a Completed banner. The reserved 'default' tenant can never be purged.
 */
export default function NukeTenantModal({ isOpen, tenant, onClose, onPurged }) {
  const { api } = useApp();
  const [typedId, setTypedId] = useState('');
  const [purging, setPurging] = useState(false);
  const [report, setReport] = useState(null);
  const [error, setError] = useState(null);

  if (!isOpen || !tenant) return null;

  const protectedTenant = isProtectedTenant(tenant);
  const matches = typedId === tenant.Id;
  const canPurge = matches && !protectedTenant && !purging && !report;

  const handleClose = () => {
    if (purging) return;
    setTypedId('');
    setReport(null);
    setError(null);
    if (report && typeof onPurged === 'function') onPurged();
    onClose();
  };

  const handlePurge = async () => {
    if (!canPurge) return;
    setError(null);
    setPurging(true);
    try {
      const result = await api.purgeTenant(tenant.Id, typedId);
      setReport(result || { Completed: true, Items: [] });
    } catch (e) {
      setError(e?.message || 'Failed to purge tenant');
    } finally {
      setPurging(false);
    }
  };

  const items = Array.isArray(report?.Items) ? report.Items : [];

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
      title={`Nuke Tenant: ${tenant.Name || tenant.Id}`}
      closeTooltip={purging ? 'Purge in progress' : 'Close'}
      wide
    >
      {!report ? (
        <div>
          <div style={{ marginBottom: 16, color: 'var(--warning-color, #b45309)' }}>
            This permanently deletes the tenant and every subordinate record (endpoints, models,
            configurations, VMRs, policies, credentials, history, and more). This action cannot be
            undone.
          </div>

          {protectedTenant ? (
            <div className="error-text" style={{ marginBottom: 16 }}>
              The reserved <strong>default</strong> tenant cannot be purged.
            </div>
          ) : (
            <div className="form-group">
              <label htmlFor="nuke-confirm">
                Type the tenant id <code>{tenant.Id}</code> to enable the purge:
              </label>
              <input
                id="nuke-confirm"
                type="text"
                value={typedId}
                onChange={(e) => setTypedId(e.target.value)}
                placeholder={tenant.Id}
                disabled={purging}
                autoComplete="off"
                spellCheck={false}
              />
            </div>
          )}

          {error && <div className="error-text" style={{ marginBottom: 16 }}>{error}</div>}

          <div className="form-actions">
            <button type="button" className="btn-secondary" onClick={handleClose} disabled={purging}>
              Cancel
            </button>
            <button type="button" className="btn-danger" onClick={handlePurge} disabled={!canPurge}>
              {purging ? 'Purging…' : 'Purge Tenant'}
            </button>
          </div>
        </div>
      ) : (
        <div>
          {report.Completed && (
            <div className="success-banner" style={{ marginBottom: 16, fontWeight: 600 }}>
              Tenant purge completed{report.CompletedUtc ? ` at ${new Date(report.CompletedUtc).toLocaleString()}` : ''}.
            </div>
          )}

          <div style={{ display: 'grid', gap: 6 }}>
            {items.length > 0 ? items.map((item, index) => (
              <div
                key={`${item.Category || 'category'}-${index}`}
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  gap: 12,
                  border: '1px solid var(--border-color)',
                  borderRadius: 8,
                  padding: '8px 12px',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span aria-hidden="true" style={{ color: item.Error ? 'var(--danger-color, #ef4444)' : 'var(--success-color, #16a34a)' }}>
                    {item.Error ? '✕' : '✓'}
                  </span>
                  <span style={{ fontWeight: 600 }}>{item.Category || 'Unknown'}</span>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div>{item.DeletedCount} deleted</div>
                  {item.Error && <div className="error-text" style={{ fontSize: 12 }}>{item.Error}</div>}
                </div>
              </div>
            )) : (
              <div style={{ color: 'var(--text-secondary)' }}>No categories reported.</div>
            )}
          </div>

          <div className="form-actions" style={{ marginTop: 16 }}>
            <button type="button" className="btn-primary" onClick={handleClose}>Done</button>
          </div>
        </div>
      )}
    </Modal>
  );
}
