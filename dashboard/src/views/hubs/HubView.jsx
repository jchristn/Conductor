import React, { useMemo } from 'react';
import { useApp } from '../../context/AppContext';
import { HubPanelProvider } from '../../context/HubPanelContext';
import Tabs from '../../components/Tabs';
import { findNavItem, gateVisible, tabStoragePath } from '../../config/navConfig';
import HUB_PANELS from './hubPanels';

/**
 * A tabbed hub page: the hub title plus a URL-synced (?tab=) tab strip. Tabs, labels, and
 * gating come from config/navConfig.jsx; panels come from hubPanels.jsx. Each panel renders
 * in embedded mode, where the hub heading replaces the view's own page title.
 */
function HubView({ hubKey }) {
  const { isAdmin, currentUser } = useApp();
  const hasAdminAccess = isAdmin || currentUser?.IsAdmin;
  const item = findNavItem(hubKey);

  const tabs = useMemo(() => {
    const panels = HUB_PANELS[hubKey] || {};
    return (item?.tabs || []).map((tab) => {
      const context = { embedded: true, storagePath: tabStoragePath(item, tab) };
      return {
        key: tab.key,
        label: tab.label,
        tooltip: tab.tooltip,
        tourId: `tab-${hubKey}-${tab.key}`,
        hidden: !gateVisible(tab.gate || 'none', { isAdmin, hasAdminAccess }) || !panels[tab.key],
        render: () => (
          <HubPanelProvider value={context}>
            <div className="hub-panel">{panels[tab.key]()}</div>
          </HubPanelProvider>
        )
      };
    });
  }, [hubKey, item, isAdmin, hasAdminAccess]);

  if (!item) return null;

  return (
    <div className="hub-view">
      <div className="hub-header">
        <h1>{item.label}</h1>
      </div>
      <Tabs tabs={tabs} ariaLabel={`${item.label} sections`} />
    </div>
  );
}

export default HubView;
