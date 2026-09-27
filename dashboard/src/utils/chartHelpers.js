// Shared helpers for the hand-rolled SVG charts (Request History, QoS Monitor).

/**
 * Parse a server timestamp as UTC milliseconds. The API emits UTC DateTimes without a timezone
 * designator (e.g. "2026-09-08T14:30:00" or "2026-09-08 14:30:00"), which the Date constructor
 * would otherwise parse as local time. Any timezone-less value is treated as UTC.
 * @param {string|number|Date|null|undefined} value - the timestamp to parse.
 * @returns {number} UTC milliseconds since the epoch, or NaN when the value is missing or invalid.
 */
export function parseUtcMs(value) {
  if (value == null || value === '') return NaN;
  if (typeof value !== 'string') return new Date(value).getTime();
  const trimmed = value.trim();
  const hasTimezone = /[zZ]$|[+-]\d{2}:?\d{2}$/.test(trimmed);
  const normalized = hasTimezone ? trimmed : trimmed.replace(' ', 'T') + 'Z';
  return new Date(normalized).getTime();
}

/**
 * Evenly spaced integer Y ticks for count axes. Always at least 2 labels; the max is rounded up so
 * every tick is a whole number and the ticks stay evenly spaced by both value and position.
 * @param {number} maxCount - the largest value plotted.
 * @returns {number[]} five ticks from 0 to the rounded-up max.
 */
export function computeYTicks(maxCount) {
  const segments = 4;
  const niceMax = Math.max(segments, Math.ceil(Math.max(maxCount, 1) / segments) * segments);
  const step = niceMax / segments;
  const ticks = [];
  for (let i = 0; i <= segments; i++) {
    ticks.push(Math.round(step * i));
  }
  return ticks;
}

/**
 * "Nice" Y ticks (1, 2, 2.5, 5 x 10^n steps) for continuous axes such as milliseconds.
 * @param {number} maxValue - the largest value plotted.
 * @param {number} [targetSegments=4] - approximate number of segments.
 * @returns {number[]} ticks from 0 to a nice max at or above maxValue.
 */
export function computeNiceTicks(maxValue, targetSegments = 4) {
  const max = Math.max(maxValue || 0, 1);
  const rawStep = max / targetSegments;
  const magnitude = Math.pow(10, Math.floor(Math.log10(rawStep)));
  const residual = rawStep / magnitude;
  let niceStep;
  if (residual <= 1) niceStep = magnitude;
  else if (residual <= 2) niceStep = 2 * magnitude;
  else if (residual <= 2.5) niceStep = 2.5 * magnitude;
  else if (residual <= 5) niceStep = 5 * magnitude;
  else niceStep = 10 * magnitude;
  niceStep = Math.max(1, niceStep);
  const segments = Math.max(1, Math.ceil(max / niceStep));
  const ticks = [];
  for (let i = 0; i <= segments; i++) {
    ticks.push(i * niceStep);
  }
  return ticks;
}

/**
 * At most maxLabels X label indices, evenly spaced across the buckets (including the first and last).
 * @param {number} bucketCount - number of buckets on the X axis.
 * @param {number} [maxLabels=8] - maximum number of labels.
 * @returns {number[]} sorted bucket indices that get a label.
 */
export function computeXLabelIndices(bucketCount, maxLabels = 8) {
  if (bucketCount <= 0) return [];
  const labelCount = Math.min(maxLabels, bucketCount);
  if (labelCount === 1) return [0];
  const indices = new Set();
  for (let i = 0; i < labelCount; i++) {
    indices.add(Math.round((i * (bucketCount - 1)) / (labelCount - 1)));
  }
  return Array.from(indices).sort((a, b) => a - b);
}

/**
 * Resolve a CSS custom property on the document root, for canvas/PNG export where var() does not apply.
 * @param {string} name - the custom property name, e.g. "--success-color".
 * @param {string} fallback - value used when the property is unset or there is no window.
 * @returns {string} the resolved color.
 */
export function cssColor(name, fallback) {
  if (typeof window === 'undefined') return fallback;
  const value = getComputedStyle(document.documentElement).getPropertyValue(name);
  return (value && value.trim()) || fallback;
}
