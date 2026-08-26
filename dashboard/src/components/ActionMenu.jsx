import React, { useState, useRef, useEffect, useLayoutEffect, useCallback } from 'react';
import { createPortal } from 'react-dom';

const MENU_MIN_WIDTH = 180;
const VIEWPORT_MARGIN = 8;
const TRIGGER_GAP = 6;

function ActionMenu({ actions }) {
  const [isOpen, setIsOpen] = useState(false);
  const [coords, setCoords] = useState({ top: 0, left: 0 });
  const triggerRef = useRef(null);
  const dropdownRef = useRef(null);

  // Position the dropdown in viewport (fixed) coordinates. The menu is rendered in a portal on
  // document.body so it is never clipped by the table's overflow container. It opens ABOVE the
  // trigger by default, falling back to below only when there is more room there, and is always
  // clamped inside the viewport.
  const computePosition = useCallback(() => {
    const trigger = triggerRef.current;
    if (!trigger) return;

    const rect = trigger.getBoundingClientRect();
    const dropdown = dropdownRef.current;
    const menuHeight = dropdown ? dropdown.offsetHeight : 0;
    const menuWidth = dropdown ? Math.max(dropdown.offsetWidth, MENU_MIN_WIDTH) : MENU_MIN_WIDTH;
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    const spaceAbove = rect.top;
    const spaceBelow = viewportHeight - rect.bottom;

    let top;
    if (menuHeight + TRIGGER_GAP <= spaceAbove || spaceAbove >= spaceBelow) {
      top = rect.top - menuHeight - TRIGGER_GAP;
    } else {
      top = rect.bottom + TRIGGER_GAP;
    }
    top = Math.max(VIEWPORT_MARGIN, Math.min(top, viewportHeight - menuHeight - VIEWPORT_MARGIN));

    // Right-align the menu with the trigger, then clamp horizontally.
    let left = rect.right - menuWidth;
    left = Math.max(VIEWPORT_MARGIN, Math.min(left, viewportWidth - menuWidth - VIEWPORT_MARGIN));

    setCoords({ top, left });
  }, []);

  useLayoutEffect(() => {
    if (isOpen) computePosition();
  }, [isOpen, computePosition]);

  useEffect(() => {
    if (!isOpen) return undefined;

    const handleClickOutside = (event) => {
      if (
        triggerRef.current && !triggerRef.current.contains(event.target) &&
        dropdownRef.current && !dropdownRef.current.contains(event.target)
      ) {
        setIsOpen(false);
      }
    };
    const handleReposition = () => computePosition();

    document.addEventListener('mousedown', handleClickOutside);
    window.addEventListener('resize', handleReposition);
    // Capture phase so scrolls inside any container (e.g. the table) reposition the menu too.
    window.addEventListener('scroll', handleReposition, true);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      window.removeEventListener('resize', handleReposition);
      window.removeEventListener('scroll', handleReposition, true);
    };
  }, [isOpen, computePosition]);

  const handleTriggerClick = (e) => {
    e.stopPropagation();
    e.preventDefault();
    setIsOpen((open) => !open);
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
    <div className={`action-menu${isOpen ? ' action-menu-open' : ''}`} onClick={(e) => e.stopPropagation()}>
      <button
        ref={triggerRef}
        className="action-menu-trigger"
        onClick={handleTriggerClick}
        aria-haspopup="true"
        aria-expanded={isOpen}
      >
        <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
          <circle cx="8" cy="3" r="1.5" />
          <circle cx="8" cy="8" r="1.5" />
          <circle cx="8" cy="13" r="1.5" />
        </svg>
      </button>
      {isOpen && createPortal(
        <div
          ref={dropdownRef}
          className="action-menu-dropdown action-menu-dropdown-portal"
          style={{ top: coords.top, left: coords.left }}
          onClick={(e) => e.stopPropagation()}
        >
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
        </div>,
        document.body
      )}
    </div>
  );
}

export default ActionMenu;
