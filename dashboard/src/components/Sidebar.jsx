import React from 'react';
import { NavLink } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import { NAV_SECTIONS, itemVisible } from '../config/navConfig';

/**
 * Grouped left nav rendered from config/navConfig.jsx. Each hub item stays highlighted for
 * every tab within it because tabs live in the query string, not the path.
 */
function Sidebar() {
  const { isAdmin, currentUser } = useApp();
  const auth = { isAdmin, hasAdminAccess: isAdmin || currentUser?.IsAdmin };

  const sections = NAV_SECTIONS.map((section) => ({
    ...section,
    items: section.items.filter((item) => itemVisible(item, auth))
  })).filter((section) => section.items.length > 0);

  return (
    <nav className="sidebar" aria-label="Primary">
      <div className="sidebar-header">
        <img src="/icon-light.png" alt="Conductor" className="sidebar-logo" />
        <h1>Conductor</h1>
      </div>
      {sections.map((section) => (
        <div className="nav-group" key={section.key}>
          <div className="nav-group-label">{section.label}</div>
          <ul className="sidebar-nav">
            {section.items.map((item) => (
              <li key={item.key}>
                <NavLink
                  to={item.path}
                  end={item.path === '/'}
                  className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
                  title={item.tooltip || ''}
                  data-tour-id={item.tourId || undefined}
                >
                  <span className="nav-icon">{item.icon}</span>
                  <span className="nav-label">{item.label}</span>
                </NavLink>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </nav>
  );
}

export default Sidebar;
