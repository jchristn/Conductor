import { createContext, useContext } from 'react';

/**
 * Context provided to a view rendered as a hub tab. `embedded` is true inside a hub (the hub
 * owns the page title), and `storagePath` is the stable path used to key per-page preferences
 * such as rows per page. Outside a hub both values are falsy.
 */
const HubPanelContext = createContext({ embedded: false, storagePath: null });

export const HubPanelProvider = HubPanelContext.Provider;

export function useHubPanel() {
  return useContext(HubPanelContext);
}

export default HubPanelContext;
