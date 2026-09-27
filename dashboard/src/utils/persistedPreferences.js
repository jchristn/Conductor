// Per-browser UI preferences (time range selections, rows per page) remembered across visits.
// Storage can be unavailable (private mode, blocked site data), so every access is guarded and
// callers always fall back to their defaults.

export const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

export function readPreference(key) {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function writePreference(key, value) {
  try {
    if (value === null || value === undefined || value === '') window.localStorage.removeItem(key);
    else window.localStorage.setItem(key, String(value));
  } catch {
    /* storage unavailable - the preference just isn't remembered */
  }
}

// Returns the stored value when it is one of the allowed values, otherwise the fallback.
export function readChoicePreference(key, allowedValues, fallback) {
  const stored = readPreference(key);
  return allowedValues.includes(stored) ? stored : fallback;
}

// Returns the stored page size when it is one of the allowed sizes, otherwise the fallback.
export function readPageSizePreference(key, fallback = 10, allowedSizes = PAGE_SIZE_OPTIONS) {
  const stored = Number(readPreference(key));
  return allowedSizes.includes(stored) ? stored : fallback;
}
