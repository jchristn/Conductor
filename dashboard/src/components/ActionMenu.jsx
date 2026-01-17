import React, { useState, useRef, useEffect } from 'react';

function ActionMenu({ actions }) {
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef(null);

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleTriggerClick = (e) => {
    e.stopPropagation();
    e.preventDefault();
    setIsOpen(!isOpen);
  };

  const handleAction = (e, action) => {
    e.stopPropagation();
    e.preventDefault();
    setIsOpen(false);
    if (action.onClick) {
      action.onClick();
    }
  };

  return (
    <div className={`action-menu${isOpen ? ' action-menu-open' : ''}`} ref={menuRef} onClick={(e) => e.stopPropagation()}>
      <button className="action-menu-trigger" onClick={handleTriggerClick}>
        <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
          <circle cx="8" cy="3" r="1.5" />
          <circle cx="8" cy="8" r="1.5" />
          <circle cx="8" cy="13" r="1.5" />
        </svg>
      </button>
      {isOpen && (
        <div className="action-menu-dropdown">
          {actions.map((action, index) =>
            action.divider ? (
              <div key={index} className="action-menu-divider"></div>
            ) : (
              <button
                key={index}
                className={`action-menu-item ${action.danger ? 'danger' : ''}`}
                onClick={(e) => handleAction(e, action)}
                disabled={action.disabled}
              >
                {action.icon && <span className="action-icon">{action.icon}</span>}
                {action.label}
              </button>
            )
          )}
        </div>
      )}
    </div>
  );
}

export default ActionMenu;
