import { navIcons } from '../components/navIcons';

/**
 * Single source of truth for the left nav and the tabbed hub pages. `Sidebar` renders from
 * `NAV_SECTIONS`, `HubView` renders a hub's tab strip from the item's `tabs`, and `App`
 * generates a replace-redirect for every `legacyPaths` entry so old bookmarks land on the
 * matching hub + tab (with the original query string preserved).
 *
 * gate values (client-side gating is UX only; the server still enforces authorization):
 *   'none'         everyone
 *   'admin'        system administrators (isAdmin)
 *   'adminAccess'  system administrators or users with the IsAdmin flag
 *
 * An item with tabs is visible when at least one of its tabs is visible.
 * `legacyPaths` are pre-hub routes that redirect to the tab. The first legacy path of a tab is
 * also its `storagePath`, so per-page preferences (rows per page) saved before the hubs existed
 * keep applying to the same table.
 */

export const NAV_SECTIONS = [
  {
    key: 'workspace',
    label: 'Workspace',
    items: [
      {
        key: 'dashboard',
        path: '/',
        label: 'Dashboard',
        icon: navIcons.dashboard,
        tourId: 'nav-dashboard',
        tooltip: 'Overview of system resources and quick actions'
      },
      {
        key: 'runners',
        path: '/runners',
        label: 'Virtual Runners',
        icon: navIcons.runners,
        tourId: 'nav-runners',
        tooltip: 'Virtual model runners, their reservations, and model access policies',
        tabs: [
          { key: 'runners', label: 'Virtual Model Runners', legacyPaths: ['/vmr'] },
          { key: 'reservations', label: 'Reservations', legacyPaths: ['/reservations'] },
          { key: 'access-policies', label: 'Model Access Policies', legacyPaths: ['/model-access-policies'] }
        ]
      },
      {
        key: 'endpoints',
        path: '/endpoints',
        label: 'Endpoints',
        icon: navIcons.endpoints,
        tourId: 'nav-endpoints',
        tooltip: 'Backend model runner endpoints, endpoint groups, and load balancing policies',
        tabs: [
          // '/endpoints' is both the hub path and the old page path, so it needs no redirect.
          { key: 'endpoints', label: 'Model Runner Endpoints', storagePath: '/endpoints' },
          { key: 'groups', label: 'Endpoint Groups', legacyPaths: ['/endpoint-groups'] },
          { key: 'load-balancing', label: 'Load Balancing Policies', legacyPaths: ['/policies'] }
        ]
      },
      {
        key: 'models',
        path: '/models',
        label: 'Models',
        icon: navIcons.models,
        tourId: 'nav-models',
        tooltip: 'Model definitions and model configuration presets',
        tabs: [
          { key: 'definitions', label: 'Model Definitions', legacyPaths: ['/definitions'] },
          { key: 'configurations', label: 'Model Configurations', legacyPaths: ['/configurations'] }
        ]
      },
      {
        key: 'qos',
        path: '/qos',
        label: 'Traffic (QoS)',
        icon: navIcons.qos,
        tourId: 'nav-qos',
        tooltip: 'Live QoS monitoring, QoS profiles, and the traffic class catalog',
        tabs: [
          { key: 'monitor', label: 'Monitor', legacyPaths: ['/qos-monitor'] },
          { key: 'profiles', label: 'Profiles', legacyPaths: ['/qos-profiles'] },
          { key: 'traffic-classes', label: 'Traffic Classes', legacyPaths: ['/qos-traffic-classes'] }
        ]
      },
      {
        key: 'observability',
        path: '/observability',
        label: 'Observability',
        icon: navIcons.observability,
        tourId: 'nav-observability',
        tooltip: 'Request history and request analytics',
        tabs: [
          { key: 'requests', label: 'Request History', legacyPaths: ['/request-history'] },
          { key: 'analytics', label: 'Analytics', legacyPaths: ['/analytics', '/request-analytics'] }
        ]
      }
    ]
  },
  {
    key: 'administration',
    label: 'Administration',
    items: [
      {
        key: 'access',
        path: '/access',
        label: 'Access',
        icon: navIcons.access,
        tourId: 'nav-access',
        tooltip: 'Tenants, users, credentials, and administrator accounts',
        tabs: [
          { key: 'tenants', label: 'Tenants', legacyPaths: ['/tenants'] },
          { key: 'users', label: 'Users', legacyPaths: ['/users'] },
          { key: 'credentials', label: 'Credentials', legacyPaths: ['/credentials'] },
          { key: 'administrators', label: 'Administrators', gate: 'admin', legacyPaths: ['/administrators'] }
        ]
      },
      {
        key: 'system',
        path: '/system',
        label: 'System',
        icon: navIcons.system,
        tourId: 'nav-system',
        tooltip: 'API explorer and backup & restore',
        tabs: [
          { key: 'api-explorer', label: 'API Explorer', legacyPaths: ['/api-explorer'] },
          { key: 'backup', label: 'Backup & Restore', gate: 'adminAccess', legacyPaths: ['/backup'] }
        ]
      }
    ]
  }
];

/** Flat list of every nav item. */
export const NAV_ITEMS = NAV_SECTIONS.flatMap((section) => section.items);

/** Every nav item that is a tabbed hub. */
export const HUB_ITEMS = NAV_ITEMS.filter((item) => Array.isArray(item.tabs) && item.tabs.length > 0);

/** Look up a nav item by key; returns null when no item matches. */
export function findNavItem(key) {
  return NAV_ITEMS.find((item) => item.key === key) || null;
}

/** Evaluate a gate string against the current auth flags. */
export function gateVisible(gate, auth) {
  const { isAdmin, hasAdminAccess } = auth || {};
  switch (gate) {
    case 'admin':
      return !!isAdmin;
    case 'adminAccess':
      return !!hasAdminAccess;
    case 'none':
    default:
      return true;
  }
}

/** True when the nav item should be shown (its own gate passes and, for hubs, any tab is visible). */
export function itemVisible(item, auth) {
  if (!gateVisible(item.gate || 'none', auth)) return false;
  if (!item.tabs) return true;
  return item.tabs.some((tab) => gateVisible(tab.gate || 'none', auth));
}

/** The path used to key per-page preferences for a tab (its pre-hub path when it had one). */
export function tabStoragePath(item, tab) {
  return tab.storagePath || (tab.legacyPaths && tab.legacyPaths[0]) || `${item.path}?tab=${tab.key}`;
}

/**
 * Build a link to a hub tab. `params` is an optional object of extra query parameters;
 * null, undefined, and empty-string values are skipped.
 */
export function hubPath(hubKey, tabKey, params) {
  const item = findNavItem(hubKey);
  const search = new URLSearchParams();
  if (tabKey) search.set('tab', tabKey);
  Object.entries(params || {}).forEach(([name, value]) => {
    if (value !== null && value !== undefined && value !== '') search.set(name, String(value));
  });
  const query = search.toString();
  return `${item ? item.path : '/'}${query ? `?${query}` : ''}`;
}

/** Every legacy route → hub + tab redirect, derived from the config. */
export const LEGACY_REDIRECTS = HUB_ITEMS.flatMap((item) =>
  item.tabs.flatMap((tab) =>
    (tab.legacyPaths || []).map((from) => ({ from, to: item.path, tab: tab.key }))
  )
);
