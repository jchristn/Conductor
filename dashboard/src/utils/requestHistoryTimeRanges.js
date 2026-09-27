// Time ranges for the request history page. They use the same bucket sizes as the Verbex, Lattice,
// and Pneuma dashboards: last hour -> 1 minute buckets, last day -> 15 minute buckets, last week ->
// 1 hour buckets, last month -> 6 hour buckets. The selected range drives the chart window, the
// summary facets, and the table query.
export const TIME_RANGES = [
  { label: 'Last Hour', value: 'hour', interval: 'minute', stepMs: 60_000, bucketCount: 60 },
  { label: 'Last Day', value: 'day', interval: '15minute', stepMs: 900_000, bucketCount: 96 },
  { label: 'Last Week', value: 'week', interval: 'hour', stepMs: 3_600_000, bucketCount: 24 * 7 },
  { label: 'Last Month', value: 'month', interval: '6hour', stepMs: 21_600_000, bucketCount: 4 * 30 }
];

export const DEFAULT_TIME_RANGE = 'day';

export function getTimeRange(value) {
  return TIME_RANGES.find((entry) => entry.value === value) || TIME_RANGES[1];
}

export function floorToStep(timestamp, stepMs) {
  return Math.floor(timestamp / stepMs) * stepMs;
}

// The window covers bucketCount whole buckets ending with the bucket that contains nowMs.
export function getRangeWindow(range, nowMs) {
  const endExclusiveMs = floorToStep(nowMs, range.stepMs) + range.stepMs;
  const startMs = endExclusiveMs - range.bucketCount * range.stepMs;
  return { startMs, endExclusiveMs };
}

// Combine the selected range with the explicit Created After / Created Before filters: the later
// start and the earlier end win, so an explicit filter can only narrow the selected range.
export function applyTimeRange(filters, rangeValue, nowMs = Date.now()) {
  const { startMs } = getRangeWindow(getTimeRange(rangeValue), nowMs);
  const explicitAfterMs = filters.createdAfterUtc ? new Date(filters.createdAfterUtc).getTime() : NaN;
  const createdAfterMs = Number.isNaN(explicitAfterMs) ? startMs : Math.max(startMs, explicitAfterMs);
  return { ...filters, createdAfterUtc: new Date(createdAfterMs).toISOString() };
}
