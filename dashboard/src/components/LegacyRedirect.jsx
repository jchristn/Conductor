import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';

/**
 * Replace-redirects a pre-hub route to its hub + tab, keeping the original query string so
 * deep links (e.g. `/reservations?vmrId=...`) still land on the filtered view. A `tab`
 * parameter on the old URL (Analytics used it for its own sections) is carried over as `view`.
 */
function LegacyRedirect({ to, tab }) {
  const location = useLocation();
  const params = new URLSearchParams(location.search);
  const legacyTab = params.get('tab');
  if (legacyTab) {
    params.delete('tab');
    if (!params.has('view')) params.set('view', legacyTab);
  }
  const next = new URLSearchParams();
  next.set('tab', tab);
  params.forEach((value, name) => next.append(name, value));
  return <Navigate to={`${to}?${next.toString()}`} replace />;
}

export default LegacyRedirect;
