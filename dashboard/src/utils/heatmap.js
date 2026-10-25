// Pure helpers that turn a POST /v1.0/analytics/buckets response (same shape as /v1.0/analytics/query)
// into a laid-out heat map model.
// Everything here is UTC and deterministic: no Date.now, no DOM, no local-time getters.

const DAY_SECONDS = 86400;
const DAY_MS = DAY_SECONDS * 1000;
const WEEK_MS = 7 * DAY_MS;
const WEEKDAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const NO_TIMING_DATA = 'No timing data';

export const DEFAULT_HEATMAP_TIMEFRAME = 'day';
export const DEFAULT_HEATMAP_METRIC = 'requests';

// rowSeconds null means the calendar layout (weekday rows, one column per UTC week). columnSeconds
// means a horizontal layout: one column per columnSeconds of UTC time (oldest left) and one row per
// bucket offset within it, so Last day reads left to right like Last week.
export const HEATMAP_TIMEFRAMES = Object.freeze({
  hour: Object.freeze({ label: 'Last hour', range: 'lastHour', bucketSeconds: 60, rowSeconds: 900 }),
  day: Object.freeze({ label: 'Last day', range: 'lastDay', bucketSeconds: 900, rowSeconds: null, columnSeconds: 3600 }),
  week: Object.freeze({ label: 'Last week', range: 'lastWeek', bucketSeconds: 3600, rowSeconds: 86400 }),
  month: Object.freeze({ label: 'Last month', range: 'lastMonth', bucketSeconds: 86400, rowSeconds: null })
});

export const HEATMAP_METRICS = Object.freeze({
  requests: Object.freeze({ label: 'Requests', field: 'RequestCount', unit: 'requests' }),
  tokens: Object.freeze({ label: 'Total tokens', field: 'TotalTokens', unit: 'tokens' }),
  input: Object.freeze({ label: 'Input tokens', field: 'PromptTokens', unit: 'input tokens' }),
  output: Object.freeze({ label: 'Output tokens', field: 'CompletionTokens', unit: 'output tokens' }),
  errors: Object.freeze({ label: 'Failures', field: 'FailedRequestCount', unit: 'failures' }),
  ttft: Object.freeze({ label: 'Avg TTFT (ms)', field: 'AverageTimeToFirstTokenMs', unit: 'ms' })
});

const own = (table, key) => typeof key === 'string' && Object.prototype.hasOwnProperty.call(table, key);

export function normalizeTimeframe(value) {
  return own(HEATMAP_TIMEFRAMES, value) ? value : DEFAULT_HEATMAP_TIMEFRAME;
}

export function normalizeMetric(value) {
  return own(HEATMAP_METRICS, value) ? value : DEFAULT_HEATMAP_METRIC;
}

// Accepts epoch ms, Date or ISO strings. A string without a zone suffix is UTC, never local time.
// Fractions longer than milliseconds (.NET emits 7 digits) are truncated before parsing.
export function parseUtcMs(value) {
  if (value == null || value === '') return NaN;
  if (typeof value === 'number') return Number.isFinite(value) ? value : NaN;
  if (value instanceof Date) return value.getTime();
  let text = String(value).trim().replace(/(\.\d{3})\d+/, '$1');
  if (/^\d{4}-\d{2}-\d{2}$/.test(text)) text += 'T00:00:00';
  if (!/(Z|[+-]\d{2}:?\d{2})$/i.test(text)) text += 'Z';
  const ms = Date.parse(text);
  return Number.isFinite(ms) ? ms : NaN;
}

const floorTo = (ms, stepMs) => Math.floor(ms / stepMs) * stepMs;
const positiveSeconds = value => {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? n : null;
};

function zeroBucket(startMs) {
  return {
    TimestampUtc: new Date(startMs).toISOString(),
    RequestCount: 0,
    SuccessfulCompletionCount: 0,
    FailedRequestCount: 0,
    PromptTokens: 0,
    CompletionTokens: 0,
    TotalTokens: 0,
    AverageTimeToFirstTokenMs: null
  };
}

// Zero-filled real buckets from floor(StartUtc, BucketSeconds) while start < EndUtc, stepping by the
// response's BucketSeconds. Server buckets match by epoch ms; buckets outside the window are ignored.
export function buildBuckets(response) {
  const bucketSeconds = positiveSeconds(response?.BucketSeconds);
  const startMs = parseUtcMs(response?.StartUtc);
  const endMs = parseUtcMs(response?.EndUtc);
  if (!bucketSeconds || !Number.isFinite(startMs) || !Number.isFinite(endMs)) return [];
  const stepMs = bucketSeconds * 1000;
  const byStart = new Map();
  for (const bucket of Array.isArray(response.TimeSeries) ? response.TimeSeries : []) {
    const ms = parseUtcMs(bucket?.TimestampUtc);
    if (Number.isFinite(ms) && !byStart.has(ms)) byStart.set(ms, bucket);
  }
  const buckets = [];
  for (let t = floorTo(startMs, stepMs); t < endMs; t += stepMs) {
    const server = byStart.get(t);
    buckets.push(server ? { ...server, startMs: t } : { ...zeroBucket(t), startMs: t });
  }
  return buckets;
}

// Count and token fields default to 0. TTFT keeps null (no timing data); a numeric 0 stays 0.
export function metricValue(bucket, metricKey) {
  const key = normalizeMetric(metricKey);
  const raw = bucket ? bucket[HEATMAP_METRICS[key].field] : null;
  if (key === 'ttft') {
    if (raw == null || raw === '') return null;
    const n = Number(raw);
    return Number.isFinite(n) ? n : null;
  }
  const n = Number(raw);
  return Number.isFinite(n) ? n : 0;
}

const isReal = bucket => !!bucket && !bucket.padding;

// Maximum positive value among real buckets, or 0.
export function metricPeak(buckets, metricKey) {
  let peak = 0;
  for (const bucket of buckets || []) {
    if (!isReal(bucket)) continue;
    const value = metricValue(bucket, metricKey);
    if (value != null && value > peak) peak = value;
  }
  return peak;
}

// 0 for null, NaN, 0 or negative; otherwise 1-4 relative to peak. A peak below the value (or 0) is
// replaced by the value so there is never a division by zero and a positive value is never level 0.
export function heatLevel(value, peak) {
  if (value == null) return 0;
  const v = Number(value);
  if (!Number.isFinite(v) || v <= 0) return 0;
  const p = Number(peak);
  const top = Number.isFinite(p) && p > v ? p : v;
  return Math.min(4, Math.max(1, Math.ceil((v / top) * 4)));
}

const pad2 = n => String(n).padStart(2, '0');
const utcDate = ms => {
  const d = new Date(ms);
  return `${d.getUTCFullYear()}-${pad2(d.getUTCMonth() + 1)}-${pad2(d.getUTCDate())}`;
};
const utcTime = ms => {
  const d = new Date(ms);
  return `${pad2(d.getUTCHours())}:${pad2(d.getUTCMinutes())}`;
};

export function formatUtcTimestamp(ms) {
  return `${utcDate(ms)} ${utcTime(ms)} UTC`;
}

function rowLabel(timeframeKey, ms) {
  if (timeframeKey === 'hour') return utcTime(ms);
  return `${WEEKDAY_NAMES[new Date(ms).getUTCDay()]} ${utcDate(ms).slice(5)}`;
}

const realCell = bucket => ({ key: `b${bucket.startMs}`, padding: false, startMs: bucket.startMs, bucket });
const paddingCell = (key, startMs) => ({ key, padding: true, startMs, bucket: null });

function layoutRows(buckets, timeframeKey, rowSeconds, bucketSeconds) {
  const stepMs = bucketSeconds * 1000;
  const rowMs = rowSeconds * 1000;
  const rowSpanMs = Math.max(rowMs, stepMs);
  const cellsPerRow = Math.max(1, Math.ceil(rowSeconds / bucketSeconds));
  const anchor = floorTo(buckets[0].startMs, rowMs);
  const placed = new Map();
  let lastRow = 0;
  for (const bucket of buckets) {
    const offset = bucket.startMs - anchor;
    const row = Math.floor(offset / rowSpanMs);
    const col = Math.min(cellsPerRow - 1, Math.floor((offset - row * rowSpanMs) / stepMs));
    placed.set(`${row}:${col}`, bucket);
    lastRow = Math.max(lastRow, row);
  }
  const rows = [];
  for (let r = 0; r <= lastRow; r++) {
    const startMs = anchor + r * rowSpanMs;
    const cells = [];
    for (let c = 0; c < cellsPerRow; c++) {
      const bucket = placed.get(`${r}:${c}`);
      cells.push(bucket ? realCell(bucket) : paddingCell(`p${r}:${c}`, startMs + c * stepMs));
    }
    rows.push({ key: `r${startMs}`, startMs, label: rowLabel(timeframeKey, startMs), cells });
  }
  return { kind: 'rows', timeframe: timeframeKey, bucketSeconds, cellsPerRow, columns: [], rows };
}

// Horizontal: one column per columnSeconds of UTC time (oldest left, newest right) and one row per bucket
// offset inside the column (':00', ':15', ':30', ':45' for 15-minute buckets in hourly columns). Rows are
// chronological left to right; positions outside the window or not covered by a bucket are padding.
function layoutColumns(buckets, timeframeKey, columnSeconds, bucketSeconds) {
  const stepMs = bucketSeconds * 1000;
  const columnMs = columnSeconds * 1000;
  const columnSpanMs = Math.max(columnMs, stepMs);
  const rowCount = Math.max(1, Math.ceil(columnSeconds / bucketSeconds));
  const anchor = floorTo(buckets[0].startMs, columnMs);
  const placed = new Map();
  let lastColumn = 0;
  for (const bucket of buckets) {
    const offset = bucket.startMs - anchor;
    const col = Math.floor(offset / columnSpanMs);
    const row = Math.min(rowCount - 1, Math.floor((offset - col * columnSpanMs) / stepMs));
    placed.set(`${row}:${col}`, bucket);
    lastColumn = Math.max(lastColumn, col);
  }
  const columns = [];
  for (let c = 0; c <= lastColumn; c++) {
    const startMs = anchor + c * columnSpanMs;
    columns.push({ key: `h${startMs}`, startMs, label: utcTime(startMs), dateLabel: utcDate(startMs).slice(5) });
  }
  const rows = [];
  for (let r = 0; r < rowCount; r++) {
    const offsetSeconds = r * bucketSeconds;
    const label = offsetSeconds % 60 === 0 ? `:${pad2(offsetSeconds / 60)}` : `+${offsetSeconds}s`;
    rows.push({
      key: `o${offsetSeconds}`,
      offsetSeconds,
      label,
      cells: columns.map((column, c) => {
        const bucket = placed.get(`${r}:${c}`);
        return bucket ? realCell(bucket) : paddingCell(`p${r}:${c}`, column.startMs + r * stepMs);
      })
    });
  }
  return { kind: 'columns', timeframe: timeframeKey, bucketSeconds, cellsPerRow: columns.length, columns, rows };
}

// Calendar: 7 UTC weekday rows (Sunday first) by one column per UTC week (each starting on Sunday).
function layoutCalendar(buckets, timeframeKey) {
  const firstDay = floorTo(buckets[0].startMs, DAY_MS);
  const firstSunday = firstDay - new Date(firstDay).getUTCDay() * DAY_MS;
  const byDay = new Map(buckets.map(bucket => [floorTo(bucket.startMs, DAY_MS), bucket]));
  const lastDay = floorTo(buckets[buckets.length - 1].startMs, DAY_MS);
  const columnCount = Math.floor((lastDay - firstSunday) / WEEK_MS) + 1;
  const columns = [];
  for (let c = 0; c < columnCount; c++) {
    const startMs = firstSunday + c * WEEK_MS;
    columns.push({ key: `w${startMs}`, startMs, label: utcDate(startMs).slice(5) });
  }
  const rows = WEEKDAY_NAMES.map((label, weekday) => ({
    key: `d${weekday}`,
    weekday,
    label,
    cells: columns.map(column => {
      const dayMs = column.startMs + weekday * DAY_MS;
      const bucket = byDay.get(dayMs);
      return bucket ? realCell(bucket) : paddingCell(`p${weekday}:${column.startMs}`, dayMs);
    })
  }));
  return { kind: 'calendar', timeframe: timeframeKey, bucketSeconds: DAY_SECONDS, cellsPerRow: columnCount, columns, rows };
}

// Lays out real buckets using the response's bucketSeconds. Positions with no real bucket are padding
// cells (padding: true, bucket: null). Day is horizontal (hour columns by quarter-hour rows). Month uses
// the calendar unless the response bucket is not daily, in which case it falls back to one UTC day per
// row so no bucket is lost.
export function layoutHeatmap(buckets, timeframeKey, bucketSeconds) {
  const key = normalizeTimeframe(timeframeKey);
  const timeframe = HEATMAP_TIMEFRAMES[key];
  const seconds = positiveSeconds(bucketSeconds) || timeframe.bucketSeconds;
  const real = (buckets || []).filter(bucket => isReal(bucket) && Number.isFinite(bucket.startMs));
  const emptyKind = timeframe.columnSeconds ? 'columns' : timeframe.rowSeconds == null ? 'calendar' : 'rows';
  if (real.length === 0) {
    return { kind: emptyKind, timeframe: key, bucketSeconds: seconds, cellsPerRow: 0, columns: [], rows: [] };
  }
  if (timeframe.columnSeconds) {
    return layoutColumns(real, key, timeframe.columnSeconds, seconds);
  }
  if (timeframe.rowSeconds == null) {
    return seconds === DAY_SECONDS ? layoutCalendar(real, key) : layoutRows(real, key, DAY_SECONDS, seconds);
  }
  return layoutRows(real, key, timeframe.rowSeconds, seconds);
}

const formatNumber = (value, options) => Number(value).toLocaleString('en-US', options);

// Singular unit text for a value of exactly 1 ('1 request', not '1 requests'). 'ms' never changes.
const SINGULAR_UNITS = { requests: 'request', tokens: 'token', input: 'input token', output: 'output token', errors: 'failure' };

export function formatMetricValue(value, metricKey) {
  const key = normalizeMetric(metricKey);
  if (key === 'ttft' && value == null) return NO_TIMING_DATA;
  const n = value ?? 0;
  const unit = Number(n) === 1 && SINGULAR_UNITS[key] ? SINGULAR_UNITS[key] : HEATMAP_METRICS[key].unit;
  return `${formatNumber(n)} ${unit}`;
}

// Total (or weighted Average for ttft), Peak with the earliest tied bucket start, and Active 'n of m'
// over real buckets only. Padding cells are ignored; nothing divides by zero.
export function summarize(buckets, metricKey) {
  const key = normalizeMetric(metricKey);
  const real = (buckets || []).filter(isReal);
  let total = 0;
  let weightedSum = 0;
  let weight = 0;
  let active = 0;
  let peakValue = 0;
  let peakStartMs = null;
  let hasData = false;
  for (const bucket of real) {
    const value = metricValue(bucket, key);
    if (value == null) continue;
    hasData = true;
    total += value;
    if (key === 'ttft') {
      const successes = Number(bucket.SuccessfulCompletionCount) || 0;
      weightedSum += value * successes;
      weight += successes;
    }
    if (value > 0) {
      active++;
      // Ties keep the earliest bucket start whatever the input order (calendar cells are weekday-major).
      const parsed = Number.isFinite(bucket.startMs) ? bucket.startMs : parseUtcMs(bucket.TimestampUtc);
      const startMs = Number.isFinite(parsed) ? parsed : null;
      const earlier = startMs != null && (peakStartMs == null || startMs < peakStartMs);
      if (value > peakValue || (value === peakValue && earlier)) {
        peakValue = value;
        peakStartMs = startMs;
      }
    }
  }
  let primary;
  if (key === 'ttft') {
    const average = weight > 0 ? weightedSum / weight : null;
    primary = {
      label: 'Average',
      value: average,
      text: average == null ? NO_TIMING_DATA : `${formatNumber(average, { maximumFractionDigits: 1 })} ms`
    };
  } else {
    primary = { label: 'Total', value: total, text: formatMetricValue(total, key) };
  }
  return {
    metric: key,
    primary,
    peak: {
      value: peakValue,
      startMs: peakStartMs,
      // With no TTFT sample at all, the peak tile says 'No timing data' rather than a misleading '0 ms'.
      text: key === 'ttft' && !hasData ? NO_TIMING_DATA : formatMetricValue(peakValue, key),
      timeText: peakStartMs == null ? null : formatUtcTimestamp(peakStartMs)
    },
    active: { count: active, total: real.length, text: `${active} of ${real.length}` }
  };
}

// '2026-10-24 13:00-14:00 UTC', or '2026-10-24 00:00-2026-10-25 00:00 UTC' when the end is another UTC date.
export function formatBucketRange(startMs, bucketSeconds) {
  const endMs = startMs + (positiveSeconds(bucketSeconds) || 0) * 1000;
  const end = utcDate(endMs) === utcDate(startMs) ? utcTime(endMs) : `${utcDate(endMs)} ${utcTime(endMs)}`;
  return `${utcDate(startMs)} ${utcTime(startMs)}-${end} UTC`;
}

export function cellLabel(bucket, metricKey, bucketSeconds) {
  if (!isReal(bucket)) return '';
  const startMs = Number.isFinite(bucket.startMs) ? bucket.startMs : parseUtcMs(bucket.TimestampUtc);
  if (!Number.isFinite(startMs)) return '';
  return `${formatBucketRange(startMs, bucketSeconds)}: ${formatMetricValue(metricValue(bucket, metricKey), metricKey)}`;
}
