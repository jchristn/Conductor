import React from 'react';

function StatusIndicator({ active, size = 'medium' }) {
  const sizeClass = {
    small: 'status-sm',
    medium: 'status-md',
    large: 'status-lg'
  }[size] || 'status-md';

  return (
    <span className={`status-indicator ${active ? 'active' : 'inactive'} ${sizeClass}`}>
      <span className="status-dot"></span>
      <span className="status-text">{active ? 'Active' : 'Inactive'}</span>
    </span>
  );
}

export default StatusIndicator;
