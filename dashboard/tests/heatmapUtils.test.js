import { describe, it, expect, vi, afterEach } from 'vitest';
import { execFileSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  HEATMAP_TIMEFRAMES,
  HEATMAP_METRICS,
  normalizeTimeframe,
  normalizeMetric,
  parseUtcMs,
  buildBuckets,
  metricValue,
  metricPeak,
  heatLevel,
  layoutHeatmap,
  summarize,
  formatBucketRange,
  cellLabel
} from '../src/utils/heatmap';

const dashboardRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const iso = ms => new Date(ms).toISOString();
const at = text => Date.parse(text);

// Unaligned start so the window floors to n+1 buckets.
function response(bucketSeconds, rangeSeconds, timeSeries, startText = '2026-10-24T12:07:33.1234567Z') {
  const startMs = at(startText);
  return { StartUtc: startText, EndUtc: iso(startMs + rangeSeconds * 1000), BucketSeconds: bucketSeconds, TimeSeries: timeSeries };
}

function realCells(layout) {
  return layout.rows.flatMap(row => row.cells).filter(cell => !cell.padding);
}

describe('heatmap utils', () => {
  it('timeframe table maps to backend ranges and bucket sizes', () => {
    expect(Object.keys(HEATMAP_TIMEFRAMES)).toEqual(['hour', 'day', 'week', 'month']);
    expect(HEATMAP_TIMEFRAMES.hour).toEqual({ label: 'Last hour', range: 'lastHour', bucketSeconds: 60, rowSeconds: 900 });
    expect(HEATMAP_TIMEFRAMES.day).toEqual({ label: 'Last day', range: 'lastDay', bucketSeconds: 900, rowSeconds: null, columnSeconds: 3600 });
    expect(HEATMAP_TIMEFRAMES.week).toEqual({ label: 'Last week', range: 'lastWeek', bucketSeconds: 3600, rowSeconds: 86400 });
    expect(HEATMAP_TIMEFRAMES.month).toEqual({ label: 'Last month', range: 'lastMonth', bucketSeconds: 86400, rowSeconds: null });
    expect(Object.keys(HEATMAP_METRICS)).toEqual(['requests', 'tokens', 'input', 'output', 'errors', 'ttft']);
    expect(Object.entries(HEATMAP_METRICS).map(([k, m]) => [k, m.field, m.label])).toEqual([
      ['requests', 'RequestCount', 'Requests'],
      ['tokens', 'TotalTokens', 'Total tokens'],
      ['input', 'PromptTokens', 'Input tokens'],
      ['output', 'CompletionTokens', 'Output tokens'],
      ['errors', 'FailedRequestCount', 'Failures'],
      ['ttft', 'AverageTimeToFirstTokenMs', 'Avg TTFT (ms)']
    ]);
  });

  describe('zero-fills and lays out each timeframe', () => {
    it('hour: 61 real buckets, 15 per row, 5 rows with leading and trailing padding', () => {
      const first = at('2026-10-24T12:07:00Z');
      const res = response(60, 3600, [
        { TimestampUtc: iso(first), RequestCount: 5, AverageTimeToFirstTokenMs: 120, SuccessfulCompletionCount: 5 },
        { TimestampUtc: iso(first + 30 * 60000), RequestCount: 2 }
      ]);
      const buckets = buildBuckets(res);
      expect(buckets).toHaveLength(61);
      expect(buckets[0].startMs).toBe(first);
      expect(buckets[0].RequestCount).toBe(5);
      expect(buckets[1]).toMatchObject({ RequestCount: 0, TotalTokens: 0, PromptTokens: 0, CompletionTokens: 0, FailedRequestCount: 0, SuccessfulCompletionCount: 0, AverageTimeToFirstTokenMs: null });
      expect(buckets[30].RequestCount).toBe(2);
      const layout = layoutHeatmap(buckets, 'hour', res.BucketSeconds);
      expect(layout.kind).toBe('rows');
      expect(layout.cellsPerRow).toBe(15);
      expect(layout.rows).toHaveLength(5);
      layout.rows.forEach(row => expect(row.cells).toHaveLength(15));
      expect(layout.rows[0].startMs).toBe(at('2026-10-24T12:00:00Z'));
      expect(layout.rows[0].cells.slice(0, 7).every(c => c.padding === true && c.bucket === null)).toBe(true);
      expect(layout.rows[0].cells[7].bucket.startMs).toBe(first);
      expect(layout.rows[4].cells[7].bucket.startMs).toBe(at('2026-10-24T13:07:00Z'));
      expect(layout.rows[4].cells.slice(8).every(c => c.padding)).toBe(true);
      expect(realCells(layout)).toHaveLength(61);
      expect(realCells(layout).map(c => c.bucket.startMs)).toEqual(buckets.map(b => b.startMs));
    });

    it('day: horizontal, 97 real buckets in 25 UTC hour columns (oldest left) by 4 quarter-hour rows', () => {
      const res = response(900, 86400, [{ TimestampUtc: '2026-10-24T18:00:00Z', RequestCount: 9 }]);
      const buckets = buildBuckets(res);
      expect(buckets).toHaveLength(97);
      expect(buckets[0].startMs).toBe(at('2026-10-24T12:00:00Z'));
      expect(buckets.find(b => b.startMs === at('2026-10-24T18:00:00Z')).RequestCount).toBe(9);
      const layout = layoutHeatmap(buckets, 'day', res.BucketSeconds);
      expect(layout.kind).toBe('columns');
      expect(layout.rows.map(r => r.label)).toEqual([':00', ':15', ':30', ':45']);
      expect(layout.cellsPerRow).toBe(25);
      expect(layout.columns).toHaveLength(25);
      expect(layout.columns[0]).toMatchObject({ startMs: at('2026-10-24T12:00:00Z'), label: '12:00', dateLabel: '10-24' });
      expect(layout.columns[12]).toMatchObject({ label: '00:00', dateLabel: '10-25' });
      expect(layout.columns.at(-1)).toMatchObject({ startMs: at('2026-10-25T12:00:00Z'), label: '12:00', dateLabel: '10-25' });
      expect(layout.rows.every(r => r.cells.length === 25)).toBe(true);
      // Reads left to right: the next cell in a row is one hour later, the next row is 15 minutes later.
      expect(layout.rows[0].cells[0].bucket.startMs).toBe(at('2026-10-24T12:00:00Z'));
      expect(layout.rows[0].cells[1].bucket.startMs).toBe(at('2026-10-24T13:00:00Z'));
      expect(layout.rows[1].cells[0].bucket.startMs).toBe(at('2026-10-24T12:15:00Z'));
      expect(layout.rows[3].cells[0].bucket.startMs).toBe(at('2026-10-24T12:45:00Z'));
      expect(layout.rows[0].cells[6].bucket.RequestCount).toBe(9);
      expect(layout.rows[0].cells[24].padding).toBe(false);
      expect(layout.rows.slice(1).every(r => r.cells[24].padding)).toBe(true);
      expect(realCells(layout)).toHaveLength(97);
      expect(new Set(realCells(layout).map(c => c.bucket.startMs))).toEqual(new Set(buckets.map(b => b.startMs)));
    });

    it('day with an unaligned window pads the partial first and last hours and keeps every bucket', () => {
      const res = { StartUtc: '2026-10-23T13:37:00Z', EndUtc: '2026-10-24T13:37:00Z', BucketSeconds: 900, TimeSeries: [] };
      const buckets = buildBuckets(res);
      const layout = layoutHeatmap(buckets, 'day', 900);
      expect(layout.rows.map(r => r.cells.map(c => (c.padding ? '.' : '#')).join(''))).toEqual([
        '.########################',
        '.########################',
        '#########################',
        '########################.'
      ]);
      expect(realCells(layout)).toHaveLength(buckets.length);
    });

    it('week: 169 real buckets, 24 per row, 8 rows', () => {
      const res = response(3600, 7 * 86400, []);
      const buckets = buildBuckets(res);
      expect(buckets).toHaveLength(169);
      const layout = layoutHeatmap(buckets, 'week', res.BucketSeconds);
      expect(layout.cellsPerRow).toBe(24);
      expect(layout.rows).toHaveLength(8);
      expect(layout.rows[0].startMs).toBe(at('2026-10-24T00:00:00Z'));
      expect(layout.rows[0].cells.slice(0, 12).every(c => c.padding)).toBe(true);
      expect(layout.rows[0].cells[12].bucket.startMs).toBe(at('2026-10-24T12:00:00Z'));
      expect(realCells(layout)).toHaveLength(169);
    });

    it('month: 31 real buckets in Sunday-first UTC week rows by 7 weekday columns (days run horizontally)', () => {
      // 2026-09-24 is a Thursday; 30 days ends 2026-10-24 (Saturday) 12:07.
      const res = response(86400, 30 * 86400, [{ TimestampUtc: '2026-10-01T00:00:00Z', RequestCount: 3 }], '2026-09-24T12:07:33Z');
      const buckets = buildBuckets(res);
      expect(buckets).toHaveLength(31);
      expect(buckets[0].startMs).toBe(at('2026-09-24T00:00:00Z'));
      const layout = layoutHeatmap(buckets, 'month', res.BucketSeconds);
      expect(layout.kind).toBe('calendar');
      expect(layout.columns.map(c => c.label)).toEqual(['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']);
      expect(layout.cellsPerRow).toBe(7);
      expect(layout.rows).toHaveLength(5);
      expect(layout.rows.map(r => r.label)).toEqual(['09-20', '09-27', '10-04', '10-11', '10-18']);
      expect(layout.rows[0].startMs).toBe(at('2026-09-20T00:00:00Z'));
      layout.rows.forEach(row => expect(row.cells).toHaveLength(7));
      const cells = realCells(layout);
      expect(cells).toHaveLength(31);
      // Each cell sits in its weekday column, and consecutive days run left to right then wrap to the next row.
      layout.rows.forEach(row => row.cells.forEach((c, i) => expect(new Date(c.startMs).getUTCDay()).toBe(i)));
      expect(cells.map(c => c.startMs)).toEqual(buckets.map(b => b.startMs));
      // First row: Sun-Wed padding before the window, Thursday 2026-09-24 in column 4.
      expect(layout.rows[0].cells.slice(0, 4).every(c => c.padding)).toBe(true);
      expect(layout.rows[0].cells[4].bucket.startMs).toBe(at('2026-09-24T00:00:00Z'));
      // Saturday 2026-10-24 is the last real bucket, at the end of the last row.
      expect(layout.rows[4].cells[6].bucket.startMs).toBe(at('2026-10-24T00:00:00Z'));
      expect(cells.find(c => c.startMs === at('2026-10-01T00:00:00Z')).bucket.RequestCount).toBe(3);
    });

    it('ignores server buckets outside [floor(StartUtc), EndUtc)', () => {
      const res = response(60, 3600, [
        { TimestampUtc: '2026-10-24T12:06:00Z', RequestCount: 100 },
        { TimestampUtc: '2026-10-24T13:08:00Z', RequestCount: 100 },
        { TimestampUtc: '2026-10-24T13:07:00Z', RequestCount: 1 }
      ]);
      const buckets = buildBuckets(res);
      expect(buckets).toHaveLength(61);
      expect(buckets.reduce((sum, b) => sum + metricValue(b, 'requests'), 0)).toBe(1);
      expect(buckets[60].RequestCount).toBe(1);
    });

    it('TimeSeries [] or undefined gives all-zero buckets, not a throw', () => {
      for (const ts of [[], undefined, null]) {
        const buckets = buildBuckets(response(60, 3600, ts));
        expect(buckets).toHaveLength(61);
        expect(buckets.every(b => b.RequestCount === 0 && b.AverageTimeToFirstTokenMs === null)).toBe(true);
      }
      expect(buildBuckets(undefined)).toEqual([]);
      expect(buildBuckets({})).toEqual([]);
      expect(buildBuckets({ StartUtc: '2026-10-24T12:00:00Z', EndUtc: '2026-10-24T13:00:00Z', BucketSeconds: 0 })).toEqual([]);
      expect(layoutHeatmap([], 'day', 900).rows).toEqual([]);
    });

    it('TimestampUtc with and without Z both match the 13:00 UTC bucket', () => {
      const base = { StartUtc: '2026-10-24T12:00:00', EndUtc: '2026-10-24T14:00:00', BucketSeconds: 3600 };
      for (const ts of ['2026-10-24T13:00:00', '2026-10-24T13:00:00Z', '2026-10-24T13:00:00.0000000Z', '2026-10-24T13:00:00+00:00']) {
        const buckets = buildBuckets({ ...base, TimeSeries: [{ TimestampUtc: ts, RequestCount: 7 }] });
        expect(buckets.map(b => b.startMs)).toEqual([at('2026-10-24T12:00:00Z'), at('2026-10-24T13:00:00Z')]);
        expect(buckets[1].RequestCount).toBe(7);
        expect(buckets[0].RequestCount).toBe(0);
      }
      expect(parseUtcMs('2026-10-24T13:00:00')).toBe(at('2026-10-24T13:00:00Z'));
    });

    it('server-coarsened BucketSeconds 120 for hour gives 8 cells per row with no lost buckets', () => {
      const res = response(120, 3600, [{ TimestampUtc: '2026-10-24T12:30:00Z', RequestCount: 4 }]);
      const buckets = buildBuckets(res);
      expect(buckets).toHaveLength(31);
      const layout = layoutHeatmap(buckets, 'hour', res.BucketSeconds);
      expect(layout.cellsPerRow).toBe(8);
      layout.rows.forEach(row => expect(row.cells).toHaveLength(8));
      const cells = realCells(layout);
      expect(cells).toHaveLength(31);
      expect(new Set(cells.map(c => c.bucket.startMs)).size).toBe(31);
      expect(cells.find(c => c.startMs === at('2026-10-24T12:30:00Z')).bucket.RequestCount).toBe(4);
    });

    it('a bucket coarser than the row keeps every bucket', () => {
      const res = response(7200, 86400, []);
      const buckets = buildBuckets(res);
      const layout = layoutHeatmap(buckets, 'day', res.BucketSeconds);
      expect(layout.rows).toHaveLength(1);
      expect(realCells(layout)).toHaveLength(buckets.length);
      expect(layout.cellsPerRow).toBe(buckets.length);
    });

    it('month with a non-daily response bucket falls back to day rows without losing buckets', () => {
      const res = response(43200, 30 * 86400, [], '2026-09-24T12:07:33Z');
      const buckets = buildBuckets(res);
      const layout = layoutHeatmap(buckets, 'month', res.BucketSeconds);
      expect(layout.kind).toBe('rows');
      expect(layout.cellsPerRow).toBe(2);
      expect(realCells(layout)).toHaveLength(buckets.length);
    });

    it('padding cells never count toward totals, peak or active', () => {
      const res = response(60, 3600, [{ TimestampUtc: '2026-10-24T12:07:00Z', RequestCount: 2 }]);
      const layout = layoutHeatmap(buildBuckets(res), 'hour', 60);
      const allCells = layout.rows.flatMap(r => r.cells).map(c => c.padding ? { padding: true, RequestCount: 999, startMs: c.startMs } : c.bucket);
      const summary = summarize(allCells, 'requests');
      expect(summary.primary.value).toBe(2);
      expect(summary.peak.value).toBe(2);
      expect(summary.active.text).toBe('1 of 61');
      expect(metricPeak(allCells, 'requests')).toBe(2);
    });
  });

  it('heat level quantization', () => {
    expect([0, 0, 0].map(v => heatLevel(v, 0))).toEqual([0, 0, 0]);
    const spike = [0, 0, 50, 0];
    const spikePeak = metricPeak(spike.map(v => ({ RequestCount: v })), 'requests');
    expect(spikePeak).toBe(50);
    expect(spike.map(v => heatLevel(v, spikePeak))).toEqual([0, 0, 4, 0]);
    expect(heatLevel(1000, 1000)).toBe(4);
    expect(heatLevel(1, 1000)).toBe(1);
    expect(heatLevel(500, 1000)).toBe(2);
    expect(heatLevel(501, 1000)).toBe(3);
    expect(heatLevel(750, 1000)).toBe(3);
    expect(heatLevel(751, 1000)).toBe(4);
    for (const v of [null, undefined, NaN, -5, 0, '']) expect(heatLevel(v, 100)).toBe(0);
    for (const v of [0, 5, 1e-9]) {
      const level = heatLevel(v, 0);
      expect(Number.isFinite(level)).toBe(true);
    }
    expect(heatLevel(0, 0)).toBe(0);
    expect(heatLevel(5, NaN)).toBe(4);
    expect(heatLevel(5, undefined)).toBe(4);
    expect(heatLevel(1e-9, 1e9)).toBe(1);
    expect(heatLevel(1e-9, 0)).toBe(4);
    for (let v = 0; v <= 1100; v += 37) {
      const level = heatLevel(v, 1000);
      expect(Number.isInteger(level) && level >= 0 && level <= 4).toBe(true);
    }
  });

  it('metric field mapping and null TTFT', () => {
    const bucket = { RequestCount: 10, TotalTokens: 300, PromptTokens: 200, CompletionTokens: 100, FailedRequestCount: 2, AverageTimeToFirstTokenMs: 42.5 };
    expect(['requests', 'tokens', 'input', 'output', 'errors', 'ttft'].map(k => metricValue(bucket, k))).toEqual([10, 300, 200, 100, 2, 42.5]);
    const empty = { RequestCount: null, AverageTimeToFirstTokenMs: null };
    expect(['requests', 'tokens', 'input', 'output', 'errors'].map(k => metricValue(empty, k))).toEqual([0, 0, 0, 0, 0]);
    expect(metricValue(empty, 'ttft')).toBeNull();
    expect(metricValue({}, 'ttft')).toBeNull();
    expect(metricValue({ AverageTimeToFirstTokenMs: 0 }, 'ttft')).toBe(0);
    expect(metricValue({ RequestCount: 7, TotalTokens: 99 }, normalizeMetric('bogus'))).toBe(7);
    expect(metricValue({ RequestCount: 7, TotalTokens: 99 }, 'bogus')).toBe(7);
    expect(heatLevel(metricValue(empty, 'ttft'), 100)).toBe(0);
  });

  it('query param normalization', () => {
    for (const key of ['hour', 'day', 'week', 'month']) expect(normalizeTimeframe(key)).toBe(key);
    for (const key of ['requests', 'tokens', 'input', 'output', 'errors', 'ttft']) expect(normalizeMetric(key)).toBe(key);
    for (const bad of [null, undefined, '', '   ', 'Week', ' day', 'day ', 'lastWeek', 'TOKENS', 'toString', '__proto__', 7]) {
      expect(normalizeTimeframe(bad)).toBe('day');
      expect(normalizeMetric(bad)).toBe('requests');
    }
  });

  describe('summary stats', () => {
    const series = [
      { startMs: at('2026-10-24T10:00:00Z'), RequestCount: 0 },
      { startMs: at('2026-10-24T11:00:00Z'), RequestCount: 5 },
      { startMs: at('2026-10-24T12:00:00Z'), RequestCount: 8 },
      { startMs: at('2026-10-24T13:00:00Z'), RequestCount: 8 }
    ];

    it('requests: total, earliest tied peak and active over real buckets', () => {
      const summary = summarize([...series, { padding: true, RequestCount: 50, startMs: 0 }], 'requests');
      expect(summary.primary).toEqual({ label: 'Total', value: 21, text: '21 requests' });
      expect(summary.peak.value).toBe(8);
      expect(summary.peak.startMs).toBe(at('2026-10-24T12:00:00Z'));
      expect(summary.peak.timeText).toBe('2026-10-24 12:00 UTC');
      expect(summary.active).toEqual({ count: 3, total: 4, text: '3 of 4' });
      expect(summary.peak.text).toBe('8 requests');
      expect(summarize([{ startMs: 0, RequestCount: 1 }], 'requests').primary.text).toBe('1 request');
    });

    it('peak ties keep the earliest bucket start for unsorted and calendar-ordered input', () => {
      const unsorted = [
        { startMs: at('2026-10-26T00:00:00Z'), RequestCount: 8 },
        { startMs: at('2026-10-20T00:00:00Z'), RequestCount: 8 },
        { startMs: at('2026-10-23T00:00:00Z'), RequestCount: 3 }
      ];
      expect(summarize(unsorted, 'requests').peak).toMatchObject({ value: 8, startMs: at('2026-10-20T00:00:00Z'), timeText: '2026-10-20 00:00 UTC' });
      expect(summarize([...unsorted].reverse(), 'requests').peak.startMs).toBe(at('2026-10-20T00:00:00Z'));
      // Raw server buckets without startMs use their TimestampUtc (zone-less is UTC) for the tie-break.
      const raw = [{ TimestampUtc: '2026-10-26T00:00:00', TotalTokens: 5 }, { TimestampUtc: '2026-10-21T00:00:00', TotalTokens: 5 }];
      expect(summarize(raw, 'tokens').peak.startMs).toBe(at('2026-10-21T00:00:00Z'));
      const ttft = [
        { startMs: at('2026-10-24T15:00:00Z'), AverageTimeToFirstTokenMs: 40, SuccessfulCompletionCount: 1 },
        { startMs: at('2026-10-24T11:00:00Z'), AverageTimeToFirstTokenMs: 40, SuccessfulCompletionCount: 1 }
      ];
      expect(summarize(ttft, 'ttft').peak.startMs).toBe(at('2026-10-24T11:00:00Z'));

      // Month calendar cells are week-major, so the Monday 09-28 cell precedes the Sunday 10-04 cell.
      const res = { StartUtc: '2026-09-24T12:07:33Z', EndUtc: '2026-10-24T12:07:33Z', BucketSeconds: 86400,
        TimeSeries: [{ TimestampUtc: '2026-09-28T00:00:00Z', RequestCount: 9 }, { TimestampUtc: '2026-10-04T00:00:00Z', RequestCount: 9 }] };
      const buckets = buildBuckets(res);
      const layout = layoutHeatmap(buckets, 'month', 86400);
      const cells = layout.rows.flatMap(row => row.cells);
      const tied = cells.filter(cell => !cell.padding && cell.bucket.RequestCount === 9).map(cell => cell.bucket.startMs);
      expect(tied).toEqual([at('2026-09-28T00:00:00Z'), at('2026-10-04T00:00:00Z')]);
      const summary = summarize(cells.map(cell => (cell.padding ? cell : cell.bucket)), 'requests');
      expect(summary.peak).toMatchObject({ value: 9, startMs: at('2026-09-28T00:00:00Z'), timeText: '2026-09-28 00:00 UTC' });
      expect(summary.peak).toEqual(summarize(buckets, 'requests').peak);
      expect(summary.active.text).toBe(`2 of ${buckets.length}`);
    });

    it('all-zero data gives Total 0, Peak 0 with no time, Active 0 of m', () => {
      const zeros = buildBuckets(response(60, 3600, []));
      const summary = summarize(zeros, 'requests');
      expect(summary.primary.value).toBe(0);
      expect(summary.peak).toMatchObject({ value: 0, startMs: null, timeText: null });
      expect(summary.active.text).toBe('0 of 61');
      expect(summarize([], 'tokens').active.text).toBe('0 of 0');
    });

    it('ttft: weighted average, null buckets excluded from active and peak', () => {
      const buckets = [
        { startMs: at('2026-10-24T10:00:00Z'), AverageTimeToFirstTokenMs: 100, SuccessfulCompletionCount: 1 },
        { startMs: at('2026-10-24T11:00:00Z'), AverageTimeToFirstTokenMs: null, SuccessfulCompletionCount: 0 },
        { startMs: at('2026-10-24T12:00:00Z'), AverageTimeToFirstTokenMs: 300, SuccessfulCompletionCount: 3 },
        { startMs: at('2026-10-24T13:00:00Z'), AverageTimeToFirstTokenMs: 0, SuccessfulCompletionCount: 0 }
      ];
      const summary = summarize(buckets, 'ttft');
      expect(summary.primary).toEqual({ label: 'Average', value: 250, text: '250 ms' });
      expect(summary.peak.value).toBe(300);
      expect(summary.peak.startMs).toBe(at('2026-10-24T12:00:00Z'));
      expect(summary.active).toEqual({ count: 2, total: 4, text: '2 of 4' });
    });

    it('ttft all null or zero weight gives No timing data, not NaN', () => {
      const allNull = summarize(buildBuckets(response(900, 86400, [])), 'ttft');
      expect(allNull.primary).toEqual({ label: 'Average', value: null, text: 'No timing data' });
      expect(allNull.peak).toMatchObject({ value: 0, startMs: null, text: 'No timing data', timeText: null });
      const zeroOnly = summarize([{ startMs: 0, AverageTimeToFirstTokenMs: 0, SuccessfulCompletionCount: 2 }], 'ttft');
      expect(zeroOnly.peak).toMatchObject({ value: 0, text: '0 ms', timeText: null });
      expect(zeroOnly.primary.text).toBe('0 ms');
      expect(allNull.active.text).toBe('0 of 97');
      const zeroWeight = summarize([{ startMs: 0, AverageTimeToFirstTokenMs: 120, SuccessfulCompletionCount: 0 }], 'ttft');
      expect(zeroWeight.primary.text).toBe('No timing data');
      expect(zeroWeight.primary.value).toBeNull();
    });
  });

  describe('cell labels are UTC with exact values', () => {
    it('formats hourly, daily and null ttft labels in ASCII', () => {
      const hourly = { startMs: at('2026-10-24T13:00:00Z'), RequestCount: 1234 };
      const daily = { startMs: at('2026-10-24T00:00:00Z'), TotalTokens: 1234567 };
      const nullTtft = { startMs: at('2026-10-24T13:00:00Z'), AverageTimeToFirstTokenMs: null };
      const labels = [
        cellLabel(hourly, 'requests', 3600),
        cellLabel(daily, 'tokens', 86400),
        cellLabel(nullTtft, 'ttft', 3600),
        cellLabel({ startMs: at('2026-10-24T13:00:00Z'), AverageTimeToFirstTokenMs: 0 }, 'ttft', 60)
      ];
      expect(labels).toEqual([
        '2026-10-24 13:00-14:00 UTC: 1,234 requests',
        '2026-10-24 00:00-2026-10-25 00:00 UTC: 1,234,567 tokens',
        '2026-10-24 13:00-14:00 UTC: No timing data',
        '2026-10-24 13:00-13:01 UTC: 0 ms'
      ]);
      labels.forEach(label => expect(/^[\x20-\x7e]*$/.test(label)).toBe(true));
      expect(labels[2]).not.toContain('0 ms');
      expect(formatBucketRange(at('2026-10-24T23:45:00Z'), 900)).toBe('2026-10-24 23:45-2026-10-25 00:00 UTC');
      expect(cellLabel({ padding: true, startMs: 0 }, 'requests', 60)).toBe('');
      expect(cellLabel({ TimestampUtc: '2026-10-24T13:00:00', RequestCount: 1 }, 'requests', 3600)).toBe('2026-10-24 13:00-14:00 UTC: 1 request');
      expect(cellLabel({ startMs: at('2026-10-24T13:00:00Z'), FailedRequestCount: 1 }, 'errors', 3600)).toBe('2026-10-24 13:00-14:00 UTC: 1 failure');
      expect(cellLabel({ startMs: at('2026-10-24T13:00:00Z'), PromptTokens: 1 }, 'input', 3600)).toBe('2026-10-24 13:00-14:00 UTC: 1 input token');
      expect(cellLabel({ startMs: at('2026-10-24T13:00:00Z'), AverageTimeToFirstTokenMs: 1 }, 'ttft', 3600)).toBe('2026-10-24 13:00-14:00 UTC: 1 ms');
      expect(cellLabel({ startMs: at('2026-10-24T13:00:00Z'), CompletionTokens: 0 }, 'output', 3600)).toBe('2026-10-24 13:00-14:00 UTC: 0 output tokens');
    });

    it('produces identical output under TZ=America/Los_Angeles in a child process', () => {
      const script = `
        const m = await import(${JSON.stringify(resolve(dashboardRoot, 'src/utils/heatmap.js'))});
        const b = m.buildBuckets({ StartUtc: '2026-10-24T12:00:00', EndUtc: '2026-10-24T14:00:00', BucketSeconds: 3600,
          TimeSeries: [{ TimestampUtc: '2026-10-24T13:00:00', RequestCount: 1234 }] });
        const week = m.layoutHeatmap(m.buildBuckets({ StartUtc: '2026-10-24T12:07:00Z', EndUtc: '2026-10-31T12:07:00Z', BucketSeconds: 3600 }), 'week', 3600);
        console.log(JSON.stringify({ offset: new Date(Date.UTC(2026, 9, 24, 13)).getTimezoneOffset(), label: m.cellLabel(b[1], 'requests', 3600),
          day: m.cellLabel({ startMs: Date.UTC(2026, 9, 24) }, 'requests', 86400), rows: week.rows.length, firstRow: week.rows[0].label }));
      `;
      const out = JSON.parse(execFileSync(process.execPath, ['--input-type=module', '-e', script], { env: { ...process.env, TZ: 'America/Los_Angeles' }, encoding: 'utf8' }));
      expect(out.offset).toBe(420);
      expect(out.label).toBe('2026-10-24 13:00-14:00 UTC: 1,234 requests');
      expect(out.day).toBe('2026-10-24 00:00-2026-10-25 00:00 UTC: 0 requests');
      expect(out.rows).toBe(8);
      expect(out.firstRow).toBe('Sat 10-24');
    });
  });

  describe('test engineer edge coverage', () => {
    afterEach(() => vi.restoreAllMocks());

    it('is pure: no Date.now, no input mutation, deterministic output', () => {
      vi.spyOn(Date, 'now').mockImplementation(() => { throw new Error('Date.now must not be called'); });
      const deepFreeze = o => { Object.values(o).forEach(v => v && typeof v === 'object' && deepFreeze(v)); return Object.freeze(o); };
      const res = deepFreeze(response(60, 3600, [{ TimestampUtc: '2026-10-24T12:30:00Z', RequestCount: 3, AverageTimeToFirstTokenMs: 10, SuccessfulCompletionCount: 1 }]));
      const run = () => {
        const buckets = buildBuckets(res);
        const layout = layoutHeatmap(buckets, 'hour', res.BucketSeconds);
        return { buckets, layout, summary: summarize(buckets, 'ttft'), label: cellLabel(buckets[23], 'requests', 60) };
      };
      const first = run();
      expect(run()).toEqual(first);
      expect(first.label).toBe('2026-10-24 12:30-12:31 UTC: 3 requests');
      expect(res.TimeSeries[0]).not.toHaveProperty('startMs');
      expect(Date.now).not.toHaveBeenCalled();
    });

    it('matches offsets, 7-digit fractions and epoch timestamps; ignores duplicates and garbage', () => {
      const base = { StartUtc: '2026-10-24T12:00:00.0000000', EndUtc: '2026-10-24T14:00:00.0000000', BucketSeconds: 3600 };
      for (const ts of ['2026-10-24T06:00:00-07:00', '2026-10-24T13:00:00+0000', '2026-10-24T13:00:00.0000000', at('2026-10-24T13:00:00Z')]) {
        const buckets = buildBuckets({ ...base, TimeSeries: [{ TimestampUtc: ts, RequestCount: 7 }] });
        expect(buckets.map(b => b.RequestCount)).toEqual([0, 7]);
      }
      const buckets = buildBuckets({ ...base, TimeSeries: [
        { TimestampUtc: 'not a date', RequestCount: 50 },
        null,
        { RequestCount: 60 },
        { TimestampUtc: '2026-10-24T13:00:00Z', RequestCount: 1 },
        { TimestampUtc: '2026-10-24T13:00:00', RequestCount: 99 }
      ] });
      expect(buckets.map(b => b.RequestCount)).toEqual([0, 1]);
      expect(parseUtcMs('   ')).toBeNaN();
      expect(parseUtcMs(Infinity)).toBeNaN();
      expect(parseUtcMs('2026-10-24')).toBe(at('2026-10-24T00:00:00Z'));
    });

    it('aligned window gives exactly n buckets with EndUtc exclusive', () => {
      const buckets = buildBuckets({ StartUtc: '2026-10-24T12:00:00Z', EndUtc: '2026-10-24T13:00:00Z', BucketSeconds: 60,
        TimeSeries: [{ TimestampUtc: '2026-10-24T13:00:00Z', RequestCount: 5 }] });
      expect(buckets).toHaveLength(60);
      expect(buckets[59].startMs).toBe(at('2026-10-24T12:59:00Z'));
      expect(summarize(buckets, 'requests').primary.value).toBe(0);
      const layout = layoutHeatmap(buckets, 'hour', 60);
      expect(layout.rows).toHaveLength(4);
      expect(layout.rows.flatMap(r => r.cells).some(c => c.padding)).toBe(false);
    });

    it('invalid window inputs give an empty list instead of throwing', () => {
      const ok = { StartUtc: '2026-10-24T12:00:00Z', EndUtc: '2026-10-24T13:00:00Z', BucketSeconds: 60 };
      for (const patch of [{ BucketSeconds: -60 }, { BucketSeconds: NaN }, { BucketSeconds: 'abc' }, { BucketSeconds: null },
        { StartUtc: 'garbage' }, { EndUtc: '' }, { EndUtc: '2026-10-24T11:00:00Z' }]) {
        expect(buildBuckets({ ...ok, ...patch })).toEqual([]);
      }
      expect(buildBuckets({ ...ok, BucketSeconds: '60' })).toHaveLength(60);
    });

    it('every response bucket size lays out each bucket exactly once, in order, with full rows', () => {
      for (const [timeframe, rangeSeconds] of [['hour', 3600], ['day', 86400], ['week', 7 * 86400], ['month', 30 * 86400]]) {
        for (const bucketSeconds of [60, 90, 120, 420, 600, 840, 900, 1000, 3600, 5400, 7200, 43200, 86400, 172800]) {
          if (rangeSeconds / bucketSeconds > 800) continue;
          const buckets = buildBuckets(response(bucketSeconds, rangeSeconds, []));
          const layout = layoutHeatmap(buckets, timeframe, bucketSeconds);
          const cells = layout.rows.flatMap(r => r.cells);
          const real = cells.filter(c => !c.padding);
          const context = `${timeframe}/${bucketSeconds}`;
          const placed = real.map(c => c.bucket.startMs);
          expect(layout.kind === 'rows' ? placed : [...placed].sort((a, b) => a - b), context).toEqual(buckets.map(b => b.startMs));
          expect(new Set(real.map(c => c.bucket.startMs)).size, context).toBe(buckets.length);
          layout.rows.forEach(row => expect(row.cells, context).toHaveLength(layout.cellsPerRow));
          cells.filter(c => c.padding).forEach(c => expect(c.bucket, context).toBeNull());
          expect(new Set(cells.map(c => c.key)).size, context).toBe(cells.length);
        }
      }
    });

    it('month calendar starting on a Sunday has no leading padding and a Sunday-first week row', () => {
      // 2026-10-04 is a Sunday.
      const res = { StartUtc: '2026-10-04T00:00:00Z', EndUtc: '2026-10-11T00:00:00Z', BucketSeconds: 86400 };
      const layout = layoutHeatmap(buildBuckets(res), 'month', 86400);
      expect(layout.kind).toBe('calendar');
      expect(layout.rows.map(r => r.label)).toEqual(['10-04']);
      expect(layout.rows[0].cells.map(c => c.padding)).toEqual([false, false, false, false, false, false, false]);
      expect(layout.rows[0].cells[0].bucket.startMs).toBe(at('2026-10-04T00:00:00Z'));
    });

    it('layout defaults: unknown timeframe falls back to day, missing bucketSeconds uses the timeframe size', () => {
      const buckets = buildBuckets(response(900, 86400, []));
      expect(layoutHeatmap(buckets, 'Week', 900)).toEqual(layoutHeatmap(buckets, 'day', 900));
      expect(layoutHeatmap(buckets, 'day', undefined).rows).toHaveLength(4);
      expect(layoutHeatmap(buckets, 'day', 0).rows).toHaveLength(4);
      expect(layoutHeatmap([], 'day', 900)).toMatchObject({ kind: 'columns', rows: [], columns: [] });
      expect(layoutHeatmap(undefined, 'month', 86400)).toMatchObject({ kind: 'calendar', rows: [] });
    });

    it('ttft text: Average rounds to 1 decimal, labels use en-US separators, string fields are numeric', () => {
      const summary = summarize([
        { startMs: 0, AverageTimeToFirstTokenMs: 100, SuccessfulCompletionCount: 1 },
        { startMs: 1, AverageTimeToFirstTokenMs: '101', SuccessfulCompletionCount: '2' }
      ], 'ttft');
      expect(summary.primary.text).toBe('100.7 ms');
      expect(summary.peak).toMatchObject({ value: 101, startMs: 1 });
      expect(cellLabel({ startMs: at('2026-10-24T13:00:00Z'), AverageTimeToFirstTokenMs: 1234.5 }, 'ttft', 60)).toBe('2026-10-24 13:00-13:01 UTC: 1,234.5 ms');
      expect(cellLabel({ startMs: at('2026-10-24T13:00:00Z'), AverageTimeToFirstTokenMs: 'n/a' }, 'ttft', 60)).toBe('2026-10-24 13:00-13:01 UTC: No timing data');
      expect(cellLabel({ startMs: at('2026-10-24T13:00:00Z'), RequestCount: 1 }, 'bogus', 60)).toBe('2026-10-24 13:00-13:01 UTC: 1 request');
      expect(cellLabel(null, 'requests', 60)).toBe('');
      expect(cellLabel({ TimestampUtc: 'garbage' }, 'requests', 60)).toBe('');
    });
  });

  describe('rework 1 regression guards', () => {
    it('ttft average ignores successes on null-TTFT buckets but weights real 0 ms buckets (D14, D17)', () => {
      const buckets = [
        { startMs: at('2026-10-24T10:00:00Z'), AverageTimeToFirstTokenMs: 100, SuccessfulCompletionCount: 1 },
        { startMs: at('2026-10-24T11:00:00Z'), AverageTimeToFirstTokenMs: null, SuccessfulCompletionCount: 5 },
        { startMs: at('2026-10-24T12:00:00Z'), AverageTimeToFirstTokenMs: 0, SuccessfulCompletionCount: 1 },
        { padding: true, startMs: at('2026-10-24T09:00:00Z'), AverageTimeToFirstTokenMs: 900, SuccessfulCompletionCount: 9 }
      ];
      const summary = summarize(buckets, 'ttft');
      expect(summary.primary).toEqual({ label: 'Average', value: 50, text: '50 ms' });
      expect(summary.peak).toMatchObject({ value: 100, startMs: at('2026-10-24T10:00:00Z'), text: '100 ms', timeText: '2026-10-24 10:00 UTC' });
      expect(summary.active).toEqual({ count: 1, total: 3, text: '1 of 3' });
      expect(metricPeak(buckets, 'ttft')).toBe(100);
      expect(buckets.map(b => heatLevel(metricValue(b, 'ttft'), 100)).slice(0, 3)).toEqual([4, 0, 0]);
    });

    it('heat level quarter boundaries and peak ignore padding, null and negative values (D15)', () => {
      expect([1, 250, 251, 500, 501, 750, 751, 1000].map(v => heatLevel(v, 1000))).toEqual([1, 1, 2, 2, 3, 3, 4, 4]);
      expect(heatLevel(5, '10')).toBe(2);
      expect(metricPeak([{ RequestCount: -8 }, { RequestCount: null }, {}], 'requests')).toBe(0);
      expect(metricPeak([{ padding: true, TotalTokens: 99 }, { TotalTokens: 3 }, null], 'tokens')).toBe(3);
      expect(metricPeak(undefined, 'requests')).toBe(0);
      const negative = summarize([{ startMs: 0, FailedRequestCount: -3 }], 'errors');
      expect(negative.peak).toMatchObject({ value: 0, startMs: null, timeText: null });
      expect(negative.active.text).toBe('0 of 1');
    });

    it('layout cells summarize exactly like the bucket list for every timeframe (D6, D16)', () => {
      for (const [timeframe, bucketSeconds, rangeSeconds] of [['hour', 60, 3600], ['day', 900, 86400], ['week', 3600, 7 * 86400], ['month', 86400, 30 * 86400]]) {
        const base = buildBuckets(response(bucketSeconds, rangeSeconds, []));
        const pick = [base[0], base[Math.floor(base.length / 2)], base[base.length - 1]];
        const res = response(bucketSeconds, rangeSeconds, pick.map((b, i) => ({ TimestampUtc: b.TimestampUtc, RequestCount: 7, TotalTokens: i + 1, AverageTimeToFirstTokenMs: 10 * (i + 1), SuccessfulCompletionCount: 1 })));
        const buckets = buildBuckets(res);
        const layout = layoutHeatmap(buckets, timeframe, res.BucketSeconds);
        const cells = layout.rows.flatMap(row => row.cells).map(cell => (cell.padding ? { ...cell, RequestCount: 1e6, TotalTokens: 1e6 } : cell.bucket));
        expect(cells.some(cell => cell.padding), timeframe).toBe(true);
        for (const metric of ['requests', 'tokens', 'ttft']) {
          expect(summarize(cells, metric), `${timeframe}/${metric}`).toEqual(summarize(buckets, metric));
          expect(metricPeak(cells, metric), `${timeframe}/${metric}`).toBe(metricPeak(buckets, metric));
        }
        const summary = summarize(buckets, 'requests');
        expect(summary.primary.value, timeframe).toBe(21);
        expect(summary.peak.startMs, timeframe).toBe(buckets[0].startMs);
        expect(summary.active.text, timeframe).toBe(`3 of ${buckets.length}`);
        expect(summarize(buckets, 'ttft').primary.value, timeframe).toBe(20);
      }
    });

    it('labels cross month and year boundaries and follow a coarsened response bucket (D6, D18)', () => {
      expect(formatBucketRange(at('2026-12-31T23:00:00Z'), 3600)).toBe('2026-12-31 23:00-2027-01-01 00:00 UTC');
      expect(formatBucketRange(at('2026-10-31T00:00:00Z'), 86400)).toBe('2026-10-31 00:00-2026-11-01 00:00 UTC');
      expect(formatBucketRange(at('2028-02-28T00:00:00Z'), 86400)).toBe('2028-02-28 00:00-2028-02-29 00:00 UTC');
      expect(formatBucketRange(at('2026-10-24T00:00:00Z'), 31 * 86400)).toBe('2026-10-24 00:00-2026-11-24 00:00 UTC');
      const res = response(120, 3600, [{ TimestampUtc: '2026-10-24T12:06:00', RequestCount: 12345678 }]);
      const layout = layoutHeatmap(buildBuckets(res), 'hour', res.BucketSeconds);
      const first = layout.rows.flatMap(row => row.cells).find(cell => !cell.padding);
      expect(first.bucket.startMs).toBe(at('2026-10-24T12:06:00Z'));
      expect(cellLabel(first.bucket, 'requests', layout.bucketSeconds)).toBe('2026-10-24 12:06-12:08 UTC: 12,345,678 requests');
      const zero = buildBuckets(res)[1];
      expect(zero).toMatchObject({ RequestCount: 0, SuccessfulCompletionCount: 0, AverageTimeToFirstTokenMs: null });
      expect(cellLabel(zero, 'ttft', layout.bucketSeconds)).toBe('2026-10-24 12:08-12:10 UTC: No timing data');
      expect(cellLabel({ startMs: at('2026-10-24T13:00:00Z') }, 'tokens', 3600)).toBe('2026-10-24 13:00-14:00 UTC: 0 tokens');
    });
  });
});
