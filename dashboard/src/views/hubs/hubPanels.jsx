import React from 'react';
import Tenants from '../Tenants';
import Users from '../Users';
import Credentials from '../Credentials';
import Administrators from '../Administrators';
import ModelRunnerEndpoints from '../ModelRunnerEndpoints';
import EndpointGroups from '../EndpointGroups';
import LoadBalancingPolicies from '../LoadBalancingPolicies';
import ModelDefinitions from '../ModelDefinitions';
import ModelConfigurations from '../ModelConfigurations';
import QosMonitor from '../QosMonitor';
import QosProfiles from '../QosProfiles';
import QosTrafficClasses from '../QosTrafficClasses';
import VirtualModelRunners from '../VirtualModelRunners';
import Reservations from '../Reservations';
import ModelAccessPolicies from '../ModelAccessPolicies';
import RequestHistory from '../RequestHistory';
import RequestAnalytics from '../RequestAnalytics';
import ApiExplorer from '../ApiExplorer';
import BackupRestore from '../BackupRestore';

/**
 * Tab panels for each hub, keyed by hub key then tab key (matching config/navConfig.jsx).
 * Each panel is the existing standalone view, rendered unchanged inside the hub.
 */
const HUB_PANELS = {
  runners: {
    runners: () => <VirtualModelRunners />,
    reservations: () => <Reservations />,
    'access-policies': () => <ModelAccessPolicies />
  },
  endpoints: {
    endpoints: () => <ModelRunnerEndpoints />,
    groups: () => <EndpointGroups />,
    'load-balancing': () => <LoadBalancingPolicies />
  },
  models: {
    definitions: () => <ModelDefinitions />,
    configurations: () => <ModelConfigurations />
  },
  qos: {
    monitor: () => <QosMonitor />,
    profiles: () => <QosProfiles />,
    'traffic-classes': () => <QosTrafficClasses />
  },
  observability: {
    requests: () => <RequestHistory />,
    analytics: () => <RequestAnalytics />
  },
  access: {
    tenants: () => <Tenants />,
    users: () => <Users />,
    credentials: () => <Credentials />,
    administrators: () => <Administrators />
  },
  system: {
    'api-explorer': () => <ApiExplorer />,
    backup: () => <BackupRestore />
  }
};

export default HUB_PANELS;
