import React, { useState } from 'react';

function ViewMetadataModal({ data, title, subtitle, onClose }) {
  const [copied, setCopied] = useState(false);

  if (!data) return null;

  const jsonString = JSON.stringify(data, null, 2);

  const handleOverlayClick = (e) => {
    if (e.target === e.currentTarget) {
      onClose();
    }
  };

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(jsonString);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  };

  return (
    <div className="modal-overlay" onClick={handleOverlayClick}>
      <div className="modal metadata-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div className="modal-header-titles">
            <h2>{title || 'View Details'}</h2>
            {subtitle && (
              <div className="modal-subtitle">
                <span className="subtitle-text">{subtitle}</span>
              </div>
            )}
          </div>
          <div className="modal-header-actions">
            <button className="btn-icon" onClick={handleCopy} title="Copy JSON">
              {copied ? (
                <svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
                  <path d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" />
                </svg>
              ) : (
                <svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
                  <path d="M8 3a1 1 0 011-1h2a1 1 0 110 2H9a1 1 0 01-1-1z" />
                  <path d="M6 3a2 2 0 00-2 2v11a2 2 0 002 2h8a2 2 0 002-2V5a2 2 0 00-2-2 3 3 0 01-3 3H9a3 3 0 01-3-3z" />
                </svg>
              )}
            </button>
            <button className="modal-close" onClick={onClose}>
              <svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
                <path d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" />
              </svg>
            </button>
          </div>
        </div>
        <div className="metadata-content">
          <pre>{jsonString}</pre>
        </div>
      </div>
    </div>
  );
}

export default ViewMetadataModal;
