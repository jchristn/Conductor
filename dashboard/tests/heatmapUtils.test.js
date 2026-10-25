import { describe, it, expect } from 'vitest';
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
    expect(HEATMAP_TIMEFRAMES.day).toEqual({ label: 'Last day', range: 'lastDay', bucketSeconds: 900, rowSeconds: 3600 });
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

    it('day: 97 real buckets, 4 per row, 25 rows', () => {
      const res = response(900, 86400, [{ TimestampUtc: '2026-10-24T18:00:00Z', RequestCount: 9 }]);
      const buckets = buildBuckets(res);
      expect(buckets).toHaveLength(97);
      expect(buckets[0].startMs).toBe(at('2026-10-24T12:00:00Z'));
      expect(buckets.find(b => b.startMs === at('2026-10-24T18:00:00Z')).RequestCount).toBe(9);
      const layout = layoutHeatmap(buckets, 'day', res.BucketSeconds);
      expect(layout.cellsPerRow).toBe(4);
      expect(layout.rows).toHaveLength(25);
      expect(realCells(layout)).toHaveLength(97);
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

    it('month: 31 real buckets in 7 Sunday-first weekday rows by UTC week columns', () => {
      // 2026-09-24 is a Thursday; 30 days ends 2026-10-24 (Saturday) 12:07.
      const res = response(86400, 30 * 86400, [{ TimestampUtc: '2026-10-01T00:00:00Z', RequestCount: 3 }], '2026-09-24T12:07:33Z');
      const buckets = buildBuckets(res);
      expect(buckets).toHaveLength(31);
      expect(buckets[0].startMs).toBe(at('2026-09-24T00:00:00Z'));
      const layout = layoutHeatmap(buckets, 'month', res.BucketSeconds);
      expect(layout.kind).toBe('calendar');
      expect(layout.rows.map(r => r.label)).toEqual(['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']);
      expect(layout.columns).toHaveLength(5);
      expect(layout.columns[0].startMs).toBe(at('2026-09-20T00:00:00Z'));
      layout.rows.forEach(row => expect(row.cells).toHaveLength(5));
      const cells = realCells(layout);
      expect(cells).toHaveLength(31);
      cells.forEach(c => expect(new Date(c.startMs).getUTCDay()).toBe(layout.rows.find(r => r.cells.includes(c)).weekday));
      // Thursday row, first column: 2026-09-24; Sunday row, first column: padding before the window.
      expect(layout.rows[4].cells[0].bucket.startMs).toBe(at('2026-09-24T00:00:00Z'));
      expect(layout.rows[0].cells[0].padding).toBe(true);
      // Saturday 2026-10-24 is the last real bucket, in column 4.
      expect(layout.rows[6].cells[4].bucket.startMs).toBe(at('2026-10-24T00:00:00Z'));
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
      expect(layout.cellsPerRow).toBe(1);
      expect(realCells(layout)).toHaveLength(buckets.length);
      expect(layout.rows).toHaveLength(buckets.length);
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
      expect(allNull.peak).toMatchObject({ value: 0, startMs: null });
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
      expect(cellLabel({ TimestampUtc: '2026-10-24T13:00:00', RequestCount: 1 }, 'requests', 3600)).toBe('2026-10-24 13:00-14:00 UTC: 1 requests');
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
});
