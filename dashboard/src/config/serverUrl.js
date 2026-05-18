export const LEGACY_DEFAULT_SERVER_URL = 'http://localhost:9001';
export const DEFAULT_SERVER_URL =
  (typeof window !== 'undefined' && window.CONDUCTOR_SERVER_URL) || 'http://localhost:9000';

const SERVER_URL_STORAGE_KEY = 'conductor_server_url';
const SERVER_URL_SOURCE_KEY = 'conductor_server_url_source';
const USER_SOURCE = 'user';
const RUNTIME_SOURCE = 'runtime';

export function persistServerUrl(url, source = USER_SOURCE) {
  localStorage.setItem(SERVER_URL_STORAGE_KEY, url);
  localStorage.setItem(SERVER_URL_SOURCE_KEY, source);
}

export function resolveInitialServerUrl() {
  const storedUrl = localStorage.getItem(SERVER_URL_STORAGE_KEY);
  const storedSource = localStorage.getItem(SERVER_URL_SOURCE_KEY);

  if (!storedUrl) {
    persistServerUrl(DEFAULT_SERVER_URL, RUNTIME_SOURCE);
    return DEFAULT_SERVER_URL;
  }

  if (storedSource === USER_SOURCE) {
    return storedUrl;
  }

  // Migrate runtime-default dashboards away from the temporary Conductor-on-9001 default.
  if (storedUrl === LEGACY_DEFAULT_SERVER_URL && DEFAULT_SERVER_URL !== LEGACY_DEFAULT_SERVER_URL) {
    persistServerUrl(DEFAULT_SERVER_URL, RUNTIME_SOURCE);
    return DEFAULT_SERVER_URL;
  }

  return storedUrl;
}
