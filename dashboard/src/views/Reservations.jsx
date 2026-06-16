import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import DataTable from '../components/DataTable';
import ActionMenu from '../components/ActionMenu';
import Modal from '../components/Modal';
import DeleteConfirmModal from '../components/DeleteConfirmModal';
import StatusIndicator from '../components/StatusIndicator';
import CopyableId from '../components/CopyableId';
import RefreshButton from '../components/RefreshButton';
import ViewMetadataModal from '../components/ViewMetadataModal';

function pad(value) {
  return String(value).padStart(2, '0');
}

function toLocalInputValue(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';

  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function fromLocalInputValue(value) {
  if (!value) return '';
  return new Date(value).toISOString();
}

function defaultStartLocal() {
  const start = new Date();
  start.setMinutes(0, 0, 0);
  start.setHours(start.getHours() + 1);
  return toLocalInputValue(start.toISOString());
}

function addHoursLocal(localValue, hours) {
  const date = new Date(localValue);
  if (Number.isNaN(date.getTime())) return '';
  date.setHours(date.getHours() + hours);
  return toLocalInputValue(date.toISOString());
}

function formatUtc(value) {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toISOString().replace('.000Z', 'Z');
}

function reservationState(reservation) {
  if (!reservation.Active) return { label: 'Inactive', tone: 'inactive' };
  const now = Date.now();
  const start = new Date(reservation.StartUtc).getTime();
  const end = new Date(reservation.EndUtc).getTime();
  if (Number.isNaN(start) || Number.isNaN(end)) return { label: 'Unknown', tone: 'inactive' };
  if (start <= now && now < end) return { label: 'Active', tone: 'active' };
  if (now < start) return { label: 'Upcoming', tone: 'pending' };
  return { label: 'Past', tone: 'inactive' };
}

function subjectKey(subjectType, subjectId) {
  return `${subjectType}:${subjectId}`;
}

function parseSubjectKey(key) {
  const separator = key.indexOf(':');
  return {
    SubjectType: key.slice(0, separator),
    SubjectId: key.slice(separator + 1)
  };
}

function normalizeSubjectType(value) {
  if (value === 1 || value === 'User') return 'User';
  if (value === 2 || value === 'Credential') return 'Credential';
  return String(value || 'Unknown');
}

function subjectLabel(subject, usersById, credentialsById) {
  if (!subject) return 'Unknown subject';
  const subjectType = normalizeSubjectType(subject.SubjectType);
  if (subjectType === 'User') {
    const user = usersById[subject.SubjectId];
    return user ? `User: ${user.Email || user.Id}` : `User: ${subject.SubjectId}`;
  }

  const credential = credentialsById[subject.SubjectId];
  return credential ? `Credential: ${credential.Name || credential.Id}` : `Credential: ${subject.SubjectId}`;
}

function subjectSummary(subjects, usersById, credentialsById) {
  const list = subjects || [];
  if (list.length === 0) return 'No participants';
  const labels = list.slice(0, 2).map((subject) => subjectLabel(subject, usersById, credentialsById));
  const suffix = list.length > 2 ? ` +${list.length - 2}` : '';
  return labels.join(', ') + suffix;
}

function defaultFormData(tenantId = '', vmrId = '') {
  const start = defaultStartLocal();
  return {
    TenantId: tenantId,
    VirtualModelRunnerId: vmrId,
    Name: '',
    Description: '',
    StartLocal: start,
    EndLocal: addHoursLocal(start, 1),
    AdmissionDrainLeadMs: 0,
    Active: true,
    SubjectKeys: []
  };
}

function Reservations() {
  const { api, setError } = useApp();
  const [searchParams] = useSearchParams();
  const queryVmrId = searchParams.get('vmrId') || '';
  const [reservations, setReservations] = useState([]);
  const [tenants, setTenants] = useState([]);
  const [vmrs, setVmrs] = useState([]);
  const [users, setUsers] = useState([]);
  const [credentials, setCredentials] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filters, setFilters] = useState({ State: '', VirtualModelRunnerId: queryVmrId });
  const [showForm, setShowForm] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedReservation, setSelectedReservation] = useState(null);
  const [formData, setFormData] = useState(defaultFormData());
  const [validationResult, setValidationResult] = useState(null);
  const [validationLoading, setValidationLoading] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [jsonReservation, setJsonReservation] = useState(null);
  const [showEffective, setShowEffective] = useState(false);
  const [effectiveContext, setEffectiveContext] = useState({
    TenantId: '',
    VirtualModelRunnerId: '',
    UserId: '',
    CredentialId: '',
    AtLocal: toLocalInputValue(new Date().toISOString())
  });
  const [effectiveResult, setEffectiveResult] = useState(null);
  const [effectiveLoading, setEffectiveLoading] = useState(false);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [tenantResult, vmrResult, userResult, credentialResult] = await Promise.all([
        api.listTenants({ maxResults: 1000 }),
        api.listVirtualModelRunners({ maxResults: 1000 }),
        api.listUsers({ maxResults: 1000 }),
        api.listCredentials({ maxResults: 1000 })
      ]);
      const loadedTenants = tenantResult.Data || [];
      const reservationResults = loadedTenants.length > 0
        ? await Promise.all(loadedTenants.map((tenant) => api.listVirtualModelRunnerReservations({ tenantId: tenant.Id, maxResults: 1000 })))
        : [await api.listVirtualModelRunnerReservations({ maxResults: 1000 })];
      const loadedReservations = reservationResults.flatMap((result) => result.Data || []);

      setReservations(loadedReservations);
      setTenants(loadedTenants);
      setVmrs(vmrResult.Data || []);
      setUsers(userResult.Data || []);
      setCredentials(credentialResult.Data || []);
    } catch (err) {
      setError('Failed to fetch reservation data: ' + err.message);
    } finally {
      setLoading(false);
    }
  }, [api, setError]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    setFilters((prev) => {
      if (prev.VirtualModelRunnerId === queryVmrId) return prev;
      return { ...prev, VirtualModelRunnerId: queryVmrId };
    });
  }, [queryVmrId]);

  const tenantsById = useMemo(() => Object.fromEntries(tenants.map((tenant) => [tenant.Id, tenant])), [tenants]);
  const vmrsById = useMemo(() => Object.fromEntries(vmrs.map((vmr) => [vmr.Id, vmr])), [vmrs]);
  const usersById = useMemo(() => Object.fromEntries(users.map((user) => [user.Id, user])), [users]);
  const credentialsById = useMemo(() => Object.fromEntries(credentials.map((credential) => [credential.Id, credential])), [credentials]);

  const filteredReservations = useMemo(() => {
    return reservations.filter((reservation) => {
      if (filters.State && reservationState(reservation).label.toLowerCase() !== filters.State.toLowerCase()) {
        return false;
      }
      if (filters.VirtualModelRunnerId && reservation.VirtualModelRunnerId !== filters.VirtualModelRunnerId) {
        return false;
      }
      return true;
    });
  }, [reservations, filters]);

  const filteredVmrs = useMemo(() => {
    if (!formData.TenantId) return vmrs;
    return vmrs.filter((vmr) => vmr.TenantId === formData.TenantId);
  }, [formData.TenantId, vmrs]);

  const filteredUsers = useMemo(() => {
    if (!formData.TenantId) return users;
    return users.filter((user) => user.TenantId === formData.TenantId && user.Active !== false);
  }, [formData.TenantId, users]);

  const filteredCredentials = useMemo(() => {
    if (!formData.TenantId) return credentials;
    return credentials.filter((credential) => credential.TenantId === formData.TenantId && credential.Active !== false);
  }, [credentials, formData.TenantId]);

  const handleCreate = () => {
    const selectedVmr = filters.VirtualModelRunnerId
      ? vmrs.find((vmr) => vmr.Id === filters.VirtualModelRunnerId)
      : null;
    const tenantId = selectedVmr?.TenantId || tenants[0]?.Id || '';
    const vmrId = selectedVmr?.Id || vmrs.find((vmr) => vmr.TenantId === tenantId)?.Id || '';
    setEditMode(false);
    setSelectedReservation(null);
    setFormData(defaultFormData(tenantId, vmrId));
    setValidationResult(null);
    setShowForm(true);
  };

  const handleEdit = async (reservation) => {
    try {
      const fullReservation = await api.getVirtualModelRunnerReservation(reservation.Id, reservation.TenantId);
      setEditMode(true);
      setSelectedReservation(fullReservation);
      setFormData({
        TenantId: fullReservation.TenantId || '',
        VirtualModelRunnerId: fullReservation.VirtualModelRunnerId || '',
        Name: fullReservation.Name || '',
        Description: fullReservation.Description || '',
        StartLocal: toLocalInputValue(fullReservation.StartUtc),
        EndLocal: toLocalInputValue(fullReservation.EndUtc),
        AdmissionDrainLeadMs: fullReservation.AdmissionDrainLeadMs || 0,
        Active: fullReservation.Active !== false,
        SubjectKeys: (fullReservation.Subjects || []).map((subject) => subjectKey(normalizeSubjectType(subject.SubjectType), subject.SubjectId))
      });
      setValidationResult(null);
      setShowForm(true);
    } catch (err) {
      setError('Failed to load reservation: ' + err.message);
    }
  };

  const handleDeleteClick = (reservation) => {
    setSelectedReservation(reservation);
    setShowDeleteConfirm(true);
  };

  const handleViewJson = async (reservation) => {
    try {
      const fullReservation = await api.getVirtualModelRunnerReservation(reservation.Id, reservation.TenantId);
      setJsonReservation(fullReservation);
    } catch (err) {
      setError('Failed to load reservation JSON: ' + err.message);
    }
  };

  const handleSubjectToggle = (key) => {
    setFormData((prev) => {
      const current = new Set(prev.SubjectKeys);
      if (current.has(key)) {
        current.delete(key);
      } else {
        current.add(key);
      }

      return { ...prev, SubjectKeys: Array.from(current) };
    });
  };

  const buildPayload = () => {
    if (!formData.TenantId) throw new Error('Tenant is required.');
    if (!formData.VirtualModelRunnerId) throw new Error('Virtual Model Runner is required.');
    if (!formData.Name.trim()) throw new Error('Name is required.');
    if (!formData.StartLocal || !formData.EndLocal) throw new Error('Start and end times are required.');
    if (formData.SubjectKeys.length < 1) throw new Error('Select at least one participant.');

    return {
      Id: selectedReservation?.Id || undefined,
      TenantId: formData.TenantId,
      VirtualModelRunnerId: formData.VirtualModelRunnerId,
      Name: formData.Name.trim(),
      Description: formData.Description.trim() || null,
      StartUtc: fromLocalInputValue(formData.StartLocal),
      EndUtc: fromLocalInputValue(formData.EndLocal),
      AdmissionDrainLeadMs: Number(formData.AdmissionDrainLeadMs || 0),
      Active: formData.Active,
      Subjects: formData.SubjectKeys.map(parseSubjectKey)
    };
  };

  const handleValidate = async () => {
    try {
      setValidationLoading(true);
      const result = await api.validateVirtualModelRunnerReservation(buildPayload());
      setValidationResult(result);
    } catch (err) {
      setValidationResult({
        IsValid: false,
        Errors: [{ Code: 'ClientValidation', Message: err.message }]
      });
    } finally {
      setValidationLoading(false);
    }
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    try {
      const payload = buildPayload();
      if (editMode && selectedReservation) {
        await api.updateVirtualModelRunnerReservation(selectedReservation.Id, payload);
      } else {
        await api.createVirtualModelRunnerReservation(payload);
      }

      setShowForm(false);
      await fetchData();
    } catch (err) {
      setError('Failed to save reservation: ' + err.message);
    }
  };

  const handleDeactivate = async () => {
    if (!selectedReservation) return;
    try {
      setDeleteLoading(true);
      await api.deleteVirtualModelRunnerReservation(selectedReservation.Id, selectedReservation.TenantId);
      setShowDeleteConfirm(false);
      setSelectedReservation(null);
      await fetchData();
    } catch (err) {
      setError('Failed to deactivate reservation: ' + err.message);
    } finally {
      setDeleteLoading(false);
    }
  };

  const openEffective = (reservation = null) => {
    const vmrId = reservation?.VirtualModelRunnerId || filters.VirtualModelRunnerId || vmrs[0]?.Id || '';
    const tenantId = reservation?.TenantId || vmrsById[vmrId]?.TenantId || tenants[0]?.Id || '';
    setEffectiveContext({
      TenantId: tenantId,
      VirtualModelRunnerId: vmrId,
      UserId: '',
      CredentialId: '',
      AtLocal: toLocalInputValue(new Date().toISOString())
    });
    setEffectiveResult(null);
    setShowEffective(true);
  };

  const handleEvaluateEffective = async (event) => {
    event.preventDefault();
    if (!effectiveContext.VirtualModelRunnerId) {
      setError('Select a Virtual Model Runner before evaluating reservation access.');
      return;
    }

    try {
      setEffectiveLoading(true);
      const result = await api.getVirtualModelRunnerReservationEffective(effectiveContext.VirtualModelRunnerId, {
        tenantId: effectiveContext.TenantId,
        userId: effectiveContext.UserId,
        credentialId: effectiveContext.CredentialId,
        atUtc: effectiveContext.AtLocal ? fromLocalInputValue(effectiveContext.AtLocal) : ''
      });
      setEffectiveResult(result);
    } catch (err) {
      setError('Failed to evaluate reservation access: ' + err.message);
    } finally {
      setEffectiveLoading(false);
    }
  };

  const columns = [
    {
      key: 'Name',
      label: 'Reservation',
      render: (item) => (
        <div>
          <strong>{item.Name}</strong>
          <CopyableId value={item.Id} />
        </div>
      )
    },
    {
      key: 'VirtualModelRunnerId',
      label: 'VMR',
      render: (item) => vmrsById[item.VirtualModelRunnerId]?.Name || item.VirtualModelRunnerId
    },
    {
      key: 'TenantId',
      label: 'Tenant',
      render: (item) => tenantsById[item.TenantId]?.Name || item.TenantId
    },
    {
      key: 'Window',
      label: 'Window UTC',
      sortable: false,
      render: (item) => (
        <div>
          <div>{formatUtc(item.StartUtc)}</div>
          <div className="text-muted">{formatUtc(item.EndUtc)}</div>
        </div>
      )
    },
    {
      key: 'Subjects',
      label: 'Participants',
      sortable: false,
      render: (item) => subjectSummary(item.Subjects, usersById, credentialsById)
    },
    {
      key: 'State',
      label: 'State',
      sortValue: (item) => reservationState(item).label,
      render: (item) => {
        const state = reservationState(item);
        return <span className={`status-badge ${state.tone}`}>{state.label}</span>;
      }
    },
    {
      key: 'Active',
      label: 'Enabled',
      render: (item) => <StatusIndicator active={item.Active} />
    },
    {
      key: 'actions',
      label: 'Actions',
      sortable: false,
      render: (item) => (
        <ActionMenu
          actions={[
            { label: 'View JSON', onClick: () => handleViewJson(item) },
            { label: 'Edit', onClick: () => handleEdit(item) },
            { label: 'Evaluate Access', onClick: () => openEffective(item) },
            { divider: true },
            { label: 'Deactivate', onClick: () => handleDeleteClick(item), danger: true, disabled: item.Active === false }
          ]}
        />
      )
    }
  ];

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>Reservations</h1>
          <p>Schedule exclusive virtual model runner reservations for selected users and credentials.</p>
        </div>
        <div className="view-actions">
          <RefreshButton onClick={fetchData} title="Refresh reservations" disabled={loading} />
          <button className="btn-secondary" onClick={() => openEffective()} title="Evaluate reservation access for a candidate identity">
            Evaluate Access
          </button>
          <button className="btn-primary" onClick={handleCreate} title="Create VMR reservation">
            Create Reservation
          </button>
        </div>
      </div>

      <div className="filter-bar">
        <div className="filter-group">
          <label title="Filter reservations by current lifecycle state">State</label>
          <select
            title="Filter reservations by current lifecycle state"
            value={filters.State}
            onChange={(event) => setFilters((prev) => ({ ...prev, State: event.target.value }))}
          >
            <option value="">All states</option>
            <option value="Active">Active</option>
            <option value="Upcoming">Upcoming</option>
            <option value="Past">Past</option>
            <option value="Inactive">Inactive</option>
          </select>
        </div>
        <div className="filter-group">
          <label title="Filter reservations to one Virtual Model Runner">Virtual Model Runner</label>
          <select
            title="Filter reservations to one Virtual Model Runner"
            value={filters.VirtualModelRunnerId}
            onChange={(event) => setFilters((prev) => ({ ...prev, VirtualModelRunnerId: event.target.value }))}
          >
            <option value="">All VMRs</option>
            {vmrs.map((vmr) => (
              <option key={vmr.Id} value={vmr.Id}>{vmr.Name}</option>
            ))}
          </select>
        </div>
      </div>

      <DataTable data={filteredReservations} columns={columns} loading={loading} onRowClick={handleEdit} />

      <Modal isOpen={showForm} onClose={() => setShowForm(false)} title={editMode ? 'Edit Reservation' : 'Create Reservation'} extraWide>
        <form onSubmit={handleSubmit}>
          <div className="form-grid">
            <div className="form-group">
              <label title="Tenant that owns the reservation and eligible participants">Tenant</label>
              <select
                title="Tenant that owns the reservation and eligible participants"
                value={formData.TenantId}
                onChange={(event) => setFormData((prev) => ({
                  ...prev,
                  TenantId: event.target.value,
                  VirtualModelRunnerId: vmrs.find((vmr) => vmr.TenantId === event.target.value)?.Id || '',
                  SubjectKeys: []
                }))}
                disabled={editMode}
                required
              >
                <option value="">Select tenant</option>
                {tenants.map((tenant) => (
                  <option key={tenant.Id} value={tenant.Id}>{tenant.Name}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label title="Virtual Model Runner that becomes exclusive during the reservation window">Virtual Model Runner</label>
              <select
                title="Virtual Model Runner that becomes exclusive during the reservation window"
                value={formData.VirtualModelRunnerId}
                onChange={(event) => setFormData((prev) => ({ ...prev, VirtualModelRunnerId: event.target.value }))}
                required
              >
                <option value="">Select VMR</option>
                {filteredVmrs.map((vmr) => (
                  <option key={vmr.Id} value={vmr.Id}>{vmr.Name}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label title="Human-readable reservation name shown in audit and management views">Name</label>
              <input
                title="Human-readable reservation name shown in audit and management views"
                value={formData.Name}
                onChange={(event) => setFormData((prev) => ({ ...prev, Name: event.target.value }))}
                required
              />
            </div>
            <div className="form-group">
              <label title="Milliseconds before the start time when new non-participant requests should stop being admitted">Admission Drain</label>
              <input
                title="Milliseconds before the start time when new non-participant requests should stop being admitted"
                type="number"
                min="0"
                max="86400000"
                step="1000"
                value={formData.AdmissionDrainLeadMs}
                onChange={(event) => setFormData((prev) => ({ ...prev, AdmissionDrainLeadMs: event.target.value }))}
              />
            </div>
          </div>

          <div className="form-grid reservation-window-grid">
            <div className="form-group">
              <label title="Local start time when the reservation begins enforcing exclusive access">Start</label>
              <input
                title="Local start time when the reservation begins enforcing exclusive access"
                type="datetime-local"
                value={formData.StartLocal}
                onChange={(event) => setFormData((prev) => ({ ...prev, StartLocal: event.target.value }))}
                required
              />
              <span className="form-help">{formatUtc(formData.StartLocal ? fromLocalInputValue(formData.StartLocal) : '')}</span>
            </div>
            <div className="form-group">
              <label title="Local end time when the VMR returns to normal ACL-based access">End</label>
              <input
                title="Local end time when the VMR returns to normal ACL-based access"
                type="datetime-local"
                value={formData.EndLocal}
                onChange={(event) => setFormData((prev) => ({ ...prev, EndLocal: event.target.value }))}
                required
              />
              <span className="form-help">{formatUtc(formData.EndLocal ? fromLocalInputValue(formData.EndLocal) : '')}</span>
            </div>
            <div className="form-group">
              <label title="Optional operational notes for why this reservation exists">Description</label>
              <input
                title="Optional operational notes for why this reservation exists"
                value={formData.Description}
                onChange={(event) => setFormData((prev) => ({ ...prev, Description: event.target.value }))}
              />
              <span className="form-help form-help-placeholder" aria-hidden="true">UTC</span>
            </div>
            <div className="form-group">
              <label title="Whether this reservation participates in access enforcement">Status</label>
              <label className="checkbox-label" title="Enable or disable this reservation without deleting it">
                <input
                  title="Enable or disable this reservation without deleting it"
                  type="checkbox"
                  checked={formData.Active}
                  onChange={(event) => setFormData((prev) => ({ ...prev, Active: event.target.checked }))}
                />
                Active
              </label>
              <span className="form-help form-help-placeholder" aria-hidden="true">UTC</span>
            </div>
          </div>

          <div className="detail-section-header">
            <h3>Participants</h3>
            <span className="form-hint">{formData.SubjectKeys.length} selected</span>
          </div>
          <div className="form-grid reservation-participants-grid">
            <div className="detail-panel">
              <div className="detail-section-header">
                <h3>Users</h3>
              </div>
              <div className="checkbox-row">
                {filteredUsers.map((user) => {
                  const key = subjectKey('User', user.Id);
                  const tooltip = `Allow ${user.Email || user.Id} to use the reserved VMR during this window`;
                  return (
                    <label key={key} className="checkbox-label" title={tooltip}>
                      <input
                        title={tooltip}
                        type="checkbox"
                        checked={formData.SubjectKeys.includes(key)}
                        onChange={() => handleSubjectToggle(key)}
                      />
                      {user.Email || user.Id}
                    </label>
                  );
                })}
                {filteredUsers.length === 0 && <span className="text-muted">No active users in this tenant.</span>}
              </div>
            </div>
            <div className="detail-panel">
              <div className="detail-section-header">
                <h3>Credentials</h3>
              </div>
              <div className="checkbox-row">
                {filteredCredentials.map((credential) => {
                  const key = subjectKey('Credential', credential.Id);
                  const tooltip = `Allow credential ${credential.Name || credential.Id} to use the reserved VMR during this window`;
                  return (
                    <label key={key} className="checkbox-label" title={tooltip}>
                      <input
                        title={tooltip}
                        type="checkbox"
                        checked={formData.SubjectKeys.includes(key)}
                        onChange={() => handleSubjectToggle(key)}
                      />
                      {credential.Name || credential.Id}
                    </label>
                  );
                })}
                {filteredCredentials.length === 0 && <span className="text-muted">No active credentials in this tenant.</span>}
              </div>
            </div>
          </div>

          {validationResult && (
            <div className={`validation-panel ${validationResult.IsValid ? 'success' : 'error'}`}>
              <strong>{validationResult.IsValid ? 'Reservation is valid.' : 'Reservation has validation errors.'}</strong>
              {(validationResult.Errors || []).length > 0 && (
                <ul className="validation-list">
                  {validationResult.Errors.map((issue, index) => (
                    <li key={index}>{issue.Code ? `${issue.Code}: ` : ''}{issue.Message}</li>
                  ))}
                </ul>
              )}
            </div>
          )}

          <div className="form-actions">
            <button
              type="button"
              className="btn-secondary"
              onClick={() => setShowForm(false)}
              title="Close the reservation form without saving changes"
            >
              Cancel
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={handleValidate}
              disabled={validationLoading}
              title="Validate the reservation window and participant selection before saving"
            >
              {validationLoading ? 'Validating...' : 'Validate'}
            </button>
            <button
              type="submit"
              className="btn-primary"
              title={editMode ? 'Save changes to this reservation' : 'Create this VMR reservation'}
            >
              {editMode ? 'Update' : 'Create'}
            </button>
          </div>
        </form>
      </Modal>

      <Modal isOpen={showEffective} onClose={() => setShowEffective(false)} title="Evaluate Reservation Access" wide>
        <form onSubmit={handleEvaluateEffective}>
          <div className="form-grid reservation-effective-grid">
            <div className="form-group">
              <label title="Virtual Model Runner whose reservation access should be evaluated">Virtual Model Runner</label>
              <select
                title="Virtual Model Runner whose reservation access should be evaluated"
                value={effectiveContext.VirtualModelRunnerId}
                onChange={(event) => {
                  const vmr = vmrsById[event.target.value];
                  setEffectiveContext((prev) => ({
                    ...prev,
                    VirtualModelRunnerId: event.target.value,
                    TenantId: vmr?.TenantId || prev.TenantId
                  }));
                }}
                required
              >
                <option value="">Select VMR</option>
                {vmrs.map((vmr) => (
                  <option key={vmr.Id} value={vmr.Id}>{vmr.Name}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label title="Optional user identity to test against the active reservation">User</label>
              <select
                title="Optional user identity to test against the active reservation"
                value={effectiveContext.UserId}
                onChange={(event) => setEffectiveContext((prev) => ({ ...prev, UserId: event.target.value }))}
              >
                <option value="">No user</option>
                {users.filter((user) => !effectiveContext.TenantId || user.TenantId === effectiveContext.TenantId).map((user) => (
                  <option key={user.Id} value={user.Id}>{user.Email || user.Id}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label title="Optional credential identity to test against the active reservation">Credential</label>
              <select
                title="Optional credential identity to test against the active reservation"
                value={effectiveContext.CredentialId}
                onChange={(event) => setEffectiveContext((prev) => ({ ...prev, CredentialId: event.target.value }))}
              >
                <option value="">No credential</option>
                {credentials.filter((credential) => !effectiveContext.TenantId || credential.TenantId === effectiveContext.TenantId).map((credential) => (
                  <option key={credential.Id} value={credential.Id}>{credential.Name || credential.Id}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label title="Local time to use when checking whether a reservation would be active">At Time</label>
              <input
                title="Local time to use when checking whether a reservation would be active"
                type="datetime-local"
                value={effectiveContext.AtLocal}
                onChange={(event) => setEffectiveContext((prev) => ({ ...prev, AtLocal: event.target.value }))}
              />
              <span className="form-help">{formatUtc(effectiveContext.AtLocal ? fromLocalInputValue(effectiveContext.AtLocal) : '')}</span>
            </div>
          </div>

          {effectiveResult && (
            <div className={`validation-panel ${effectiveResult.Allowed ? 'success' : 'error'}`}>
              <strong>{effectiveResult.Allowed ? 'Allowed' : 'Denied'}: {effectiveResult.ReasonCode}</strong>
              <div className="detail-grid-two">
                <div className="detail-item">
                  <label title="Reservation that determined the access decision">Reservation</label>
                  <span>{effectiveResult.ReservationName || effectiveResult.ReservationId || 'None'}</span>
                </div>
                <div className="detail-item">
                  <label title="Reservation access decision returned by the API">Decision</label>
                  <span>{effectiveResult.Decision}</span>
                </div>
              </div>
              {effectiveResult.ReasonText && <p className="form-hint">{effectiveResult.ReasonText}</p>}
            </div>
          )}

          <div className="form-actions">
            <button
              type="button"
              className="btn-secondary"
              onClick={() => setShowEffective(false)}
              title="Close the access evaluation dialog"
            >
              Close
            </button>
            <button
              type="submit"
              className="btn-primary"
              disabled={effectiveLoading}
              title="Evaluate whether the selected identity can use the VMR at the selected time"
            >
              {effectiveLoading ? 'Evaluating...' : 'Evaluate'}
            </button>
          </div>
        </form>
      </Modal>

      <DeleteConfirmModal
        isOpen={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDeactivate}
        entityName={selectedReservation?.Name}
        entityType="reservation"
        title="Deactivate Reservation"
        message="Deactivate this reservation and keep it for audit history?"
        warningMessage="Active or future windows will stop enforcing after deactivation."
        loading={deleteLoading}
      />

      {jsonReservation && (
        <ViewMetadataModal
          data={jsonReservation}
          title="VMR Reservation JSON"
          subtitle={jsonReservation.Id}
          onClose={() => setJsonReservation(null)}
        />
      )}
    </div>
  );
}

export default Reservations;
