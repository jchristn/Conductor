import React from 'react';

function DeleteConfirmModal({
  isOpen,
  onClose,
  onConfirm,
  entityName,
  entityType = 'item',
  title = 'Confirm Delete',
  message,
  warningMessage = 'This action cannot be undone.',
  loading = false
}) {
  if (!isOpen) return null;

  const handleOverlayClick = (e) => {
    if (e.target === e.currentTarget && !loading) {
      onClose();
    }
  };

  const handleConfirm = () => {
    onConfirm();
  };

  const defaultMessage = `Are you sure you want to delete this ${entityType}?`;

  return (
    <div className="modal-overlay" onClick={handleOverlayClick}>
      <div className="modal delete-confirm-modal" onClick={(e) => e.stopPropagation()}>
        <div className="delete-warning-icon">
          <svg width="48" height="48" viewBox="0 0 48 48" fill="#ef4444">
            <path d="M24 4C12.954 4 4 12.954 4 24s8.954 20 20 20 20-8.954 20-20S35.046 4 24 4zm0 36c-8.837 0-16-7.163-16-16S15.163 8 24 8s16 7.163 16 16-7.163 16-16 16zm-2-26h4v16h-4V14zm0 20h4v4h-4v-4z" />
          </svg>
        </div>
        <h2>{title}</h2>
        <p className="delete-message">{message || defaultMessage}</p>
        {entityName && (
          <p className="entity-name">
            <strong>{entityName}</strong>
          </p>
        )}
        {warningMessage && <p className="delete-warning">{warningMessage}</p>}

        <div className="delete-actions">
          <button className="btn-secondary" onClick={onClose} disabled={loading}>
            Cancel
          </button>
          <button className="btn-danger" onClick={handleConfirm} disabled={loading}>
            {loading ? 'Deleting...' : 'Delete'}
          </button>
        </div>
      </div>
    </div>
  );
}

export default DeleteConfirmModal;
