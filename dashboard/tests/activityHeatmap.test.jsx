import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act, within } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import ActivityHeatmap from '../src/components/ActivityHeatmap';
import Dashboard from '../src/views/Dashboard';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');

const api = vi.hoisted(() => ({
  queryAnalytics: vi.fn(),
  getObservabilityMetricsSummary: vi.fn(),
  listVirtualModelRunners: vi.fn(),
  getRequestHistorySummary: vi.fn()
}));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ({ api, counts: {}, fetchCounts: () => {}, isAdmin: true, currentUser: null }) }));

// Day window that starts at 00:30 UTC: the first row has two leading padding cells and the last row
// two trailing ones, so there are 96 real 15-minute buckets laid out in 25 rows of 4.
function dayResponse(overrides = {}) {
  return {
    StartUtc: '2026-10-24T00:30:00Z',
    EndUtc: '2026-10-25T00:30:00Z',
    BucketSeconds: 900,
    TotalRequests: 1260,
    TimeSeries: [
      // .NET style: no zone suffix and 7 fractional digits; must still be read as UTC.
      { TimestampUtc: '2026-10-24T13:00:00.0000000', RequestCount: 1234, SuccessfulCompletionCount: 1200, FailedRequestCount: 34, PromptTokens: 5000, CompletionTokens: 2500, TotalTokens: 7500, AverageTimeToFirstTokenMs: 120.5 },
      { TimestampUtc: '2026-10-24T14:15:00Z', RequestCount: 26, SuccessfulCompletionCount: 26, FailedRequestCount: 0, PromptTokens: 100, CompletionTokens: 50, TotalTokens: 150, AverageTimeToFirstTokenMs: null }
    ],
    ...overrides
  };
}

const weekResponse = () => ({
  StartUtc: '2026-10-17T13:00:00Z', EndUtc: '2026-10-24T13:00:00Z', BucketSeconds: 3600, TotalRequests: 7,
  TimeSeries: [{ TimestampUtc: '2026-10-20T08:00:00Z', RequestCount: 7, SuccessfulCompletionCount: 7 }]
});
const hourResponse = () => ({
  StartUtc: '2026-10-24T12:00:00Z', EndUtc: '2026-10-24T13:00:00Z', BucketSeconds: 60, TotalRequests: 3,
  TimeSeries: [{ TimestampUtc: '2026-10-24T12:30:00Z', RequestCount: 3 }]
});
const monthResponse = () => ({
  StartUtc: '2026-09-24T00:00:00Z', EndUtc: '2026-10-24T00:00:00Z', BucketSeconds: 86400, TotalRequests: 9,
  TimeSeries: [{ TimestampUtc: '2026-10-01T00:00:00Z', RequestCount: 9 }]
});
const byRange = { lastHour: hourResponse, lastDay: dayResponse, lastWeek: weekResponse, lastMonth: monthResponse };

function deferred() {
  let resolveFn;
  let rejectFn;
  const promise = new Promise((res, rej) => { resolveFn = res; rejectFn = rej; });
  return { promise, resolve: resolveFn, reject: rejectFn };
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="search">{location.search}</div>;
}

function renderHeatmap(entry) {
  return render(<MemoryRouter initialEntries={[entry]}><ActivityHeatmap /><LocationProbe /></MemoryRouter>);
}
function renderDashboard(entry) {
  return render(<MemoryRouter initialEntries={[entry]}><Dashboard /><LocationProbe /></MemoryRouter>);
}

// Only the heat map sends BucketSeconds; Dashboard's own { Range: 'lastDay' } call does not.
const heatmapCalls = () => api.queryAnalytics.mock.calls.map(call => call[0]).filter(body => body && 'BucketSeconds' in body);
const search = () => new URLSearchParams(screen.getByTestId('search').textContent);
const gridCells = () => screen.getAllByRole('gridcell');
const cellNamed = name => screen.getByRole('gridcell', { name });

beforeEach(() => {
  api.queryAnalytics.mockReset();
  api.queryAnalytics.mockImplementation(body => Promise.resolve(byRange[body.Range]()));
  api.getObservabilityMetricsSummary.mockReset().mockResolvedValue(null);
  api.listVirtualModelRunners.mockReset().mockResolvedValue({ Data: [] });
  api.getRequestHistorySummary.mockReset().mockResolvedValue({ Buckets: [], TotalSuccess: 0, TotalFailure: 0, TotalRequests: 0 });
});

describe('heatmap tab is last and lazy', () => {
  it('appends Heatmap after the existing tabs and does not query until it is active', async () => {
    renderDashboard('/');
    const tabs = within(screen.getByRole('tablist', { name: 'Dashboard charts' })).getAllByRole('tab');
    expect(tabs.map(tab => tab.textContent)).toEqual(['Request history', 'Volume / TTFT', 'Latency / TTFT', 'Tokens', 'Heatmap']);
    expect(tabs[0].getAttribute('aria-selected')).toBe('true');
    await waitFor(() => expect(api.queryAnalytics).toHaveBeenCalledWith({ Range: 'lastDay' }));
    await waitFor(() => expect(api.getRequestHistorySummary).toHaveBeenCalled());
    expect(heatmapCalls()).toEqual([]);
    expect(screen.queryByRole('grid')).toBeNull();
  });

  it('renders the heat map panel at ?chart=heatmap', async () => {
    renderDashboard('/?chart=heatmap');
    expect(screen.getByRole('tab', { name: 'Heatmap' }).getAttribute('aria-selected')).toBe('true');
    expect(await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last day' })).toBeTruthy();
    expect(heatmapCalls()).toEqual([{ Range: 'lastDay', BucketSeconds: 900 }]);
    expect(api.queryAnalytics).toHaveBeenCalledWith({ Range: 'lastDay' });
  });

  it('falls back to the history tab for ?chart=Heatmap (wrong case)', async () => {
    renderDashboard('/?chart=Heatmap');
    expect(screen.getByRole('tab', { name: 'Request history' }).getAttribute('aria-selected')).toBe('true');
    await waitFor(() => expect(api.getRequestHistorySummary).toHaveBeenCalled());
    expect(heatmapCalls()).toEqual([]);
  });

  it('keeps Dashboard Tabs without preserveParams', () => {
    const source = readFileSync(resolve(root, 'src/views/Dashboard.jsx'), 'utf8');
    expect(source).not.toContain('preserveParams');
    expect(source).toContain("{ key: 'heatmap', label: 'Heatmap'");
  });
});

describe('timeframe switch re-queries with the right Range and persists param', () => {
  it('loads week from the URL and sends exactly one call per timeframe choice', async () => {
    renderDashboard('/?chart=heatmap&heatmapRange=week');
    expect(await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last week' })).toBeTruthy();
    expect(heatmapCalls()).toEqual([{ Range: 'lastWeek', BucketSeconds: 3600 }]);
    const expected = [['hour', 'lastHour', 60, 'Last hour'], ['day', 'lastDay', 900, 'Last day'], ['month', 'lastMonth', 86400, 'Last month']];
    for (const [key, range, bucketSeconds, label] of expected) {
      const before = heatmapCalls().length;
      fireEvent.change(screen.getByRole('combobox', { name: 'Timeframe' }), { target: { value: key } });
      expect(await screen.findByRole('grid', { name: `Activity heatmap, Requests, ${label}` })).toBeTruthy();
      expect(heatmapCalls().slice(before)).toEqual([{ Range: range, BucketSeconds: bucketSeconds }]);
      expect(search().get('heatmapRange')).toBe(key);
      expect(search().get('chart')).toBe('heatmap');
      expect(screen.getByRole('tab', { name: 'Heatmap' }).getAttribute('aria-selected')).toBe('true');
    }
  });

  it.each(['Week', '', '%20', 'bogus', '%20day'])('?heatmapRange=%s falls back to lastDay/900', async value => {
    renderHeatmap(`/?chart=heatmap&heatmapRange=${value}`);
    expect(await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last day' })).toBeTruthy();
    expect(heatmapCalls()).toEqual([{ Range: 'lastDay', BucketSeconds: 900 }]);
    expect(screen.getByRole('combobox', { name: 'Timeframe' }).value).toBe('day');
  });

  it('never sends a Limit key', async () => {
    renderHeatmap('/?chart=heatmap');
    await screen.findByRole('grid');
    expect(heatmapCalls().every(body => !('Limit' in body))).toBe(true);
  });
});

describe('stale responses are ignored', () => {
  function deferredByRange() {
    const pending = {};
    api.queryAnalytics.mockImplementation(body => {
      pending[body.Range] = deferred();
      return pending[body.Range].promise;
    });
    return pending;
  }

  it('a late day resolve never replaces the week view', async () => {
    const pending = deferredByRange();
    renderHeatmap('/?chart=heatmap');
    expect(screen.getByRole('status').textContent).toBe('Loading analytics…');
    fireEvent.change(screen.getByRole('combobox', { name: 'Timeframe' }), { target: { value: 'week' } });
    expect(screen.getByRole('status').textContent).toBe('Loading analytics…');
    await act(async () => pending.lastWeek.resolve(weekResponse()));
    expect(screen.getByRole('grid', { name: 'Activity heatmap, Requests, Last week' })).toBeTruthy();
    await act(async () => pending.lastDay.resolve(dayResponse()));
    expect(screen.getByRole('grid', { name: 'Activity heatmap, Requests, Last week' })).toBeTruthy();
    expect(screen.queryByRole('gridcell', { name: /1,234 requests/ })).toBeNull();
    expect(screen.getByRole('gridcell', { name: '2026-10-20 08:00-09:00 UTC: 7 requests' })).toBeTruthy();
  });

  it('a late day rejection never shows an error over the week view', async () => {
    const pending = deferredByRange();
    renderHeatmap('/?chart=heatmap');
    fireEvent.change(screen.getByRole('combobox', { name: 'Timeframe' }), { target: { value: 'week' } });
    await act(async () => pending.lastWeek.resolve(weekResponse()));
    await act(async () => pending.lastDay.reject(new Error('late')));
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByText(/Analytics unavailable/)).toBeNull();
    expect(screen.getByRole('grid', { name: 'Activity heatmap, Requests, Last week' })).toBeTruthy();
  });

  it('a new timeframe clears a previous error and shows loading', async () => {
    const pending = deferredByRange();
    renderHeatmap('/?chart=heatmap');
    await act(async () => pending.lastDay.reject(new Error('boom')));
    expect(screen.getByRole('alert').textContent).toBe('Analytics unavailable: boom');
    fireEvent.change(screen.getByRole('combobox', { name: 'Timeframe' }), { target: { value: 'week' } });
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.getByRole('status').textContent).toBe('Loading analytics…');
    await act(async () => pending.lastWeek.resolve(weekResponse()));
    expect(screen.getByRole('grid', { name: 'Activity heatmap, Requests, Last week' })).toBeTruthy();
  });

  it('returning to a timeframe while its new request is pending shows loading, not the older data', async () => {
    const pending = deferredByRange();
    renderHeatmap('/?chart=heatmap');
    await act(async () => pending.lastDay.resolve(dayResponse()));
    fireEvent.change(screen.getByRole('combobox', { name: 'Timeframe' }), { target: { value: 'week' } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Timeframe' }), { target: { value: 'day' } });
    expect(screen.queryByRole('grid')).toBeNull();
    expect(screen.getByRole('status').textContent).toBe('Loading analytics…');
  });
});

describe('metric switch re-renders without re-query', () => {
  const expectations = [
    ['tokens', 'Total tokens', '7,500 tokens', 'Total', '7,650 tokens'],
    ['input', 'Input tokens', '5,000 input tokens', 'Total', '5,100 input tokens'],
    ['output', 'Output tokens', '2,500 output tokens', 'Total', '2,550 output tokens'],
    ['errors', 'Failures', '34 failures', 'Total', '34 failures'],
    ['ttft', 'Avg TTFT (ms)', '120.5 ms', 'Average', '120.5 ms'],
    ['requests', 'Requests', '1,234 requests', 'Total', '1,260 requests']
  ];

  it('switches through all six metrics with one query and keeps every param', async () => {
    renderHeatmap('/?chart=heatmap&heatmapRange=day');
    await screen.findByRole('grid');
    expect(heatmapCalls()).toHaveLength(1);
    for (const [key, label, cellText, primaryLabel, primaryText] of expectations) {
      fireEvent.change(screen.getByRole('combobox', { name: 'Metric' }), { target: { value: key } });
      expect(screen.getByRole('grid', { name: `Activity heatmap, ${label}, Last day` })).toBeTruthy();
      expect(cellNamed(`2026-10-24 13:00-13:15 UTC: ${cellText}`)).toBeTruthy();
      const primary = screen.getByText(primaryLabel, { selector: '.hm-tile-label' }).parentElement;
      expect(primary.querySelector('.hm-tile-value').textContent).toBe(primaryText);
      expect(search().get('heatmapMetric')).toBe(key);
      expect(search().get('chart')).toBe('heatmap');
      expect(search().get('heatmapRange')).toBe('day');
    }
    expect(heatmapCalls()).toHaveLength(1);
  });

  it('labels a null TTFT cell as No timing data, never 0 ms', async () => {
    renderHeatmap('/?chart=heatmap&heatmapMetric=ttft');
    await screen.findByRole('grid', { name: 'Activity heatmap, Avg TTFT (ms), Last day' });
    const cell = cellNamed('2026-10-24 14:15-14:30 UTC: No timing data');
    expect(cell.getAttribute('class')).toContain('hm-level-0');
    expect(screen.queryByRole('gridcell', { name: /14:15-14:30 UTC: 0 ms/ })).toBeNull();
    // A zero-filled bucket has no TTFT either.
    expect(cellNamed('2026-10-24 00:30-00:45 UTC: No timing data')).toBeTruthy();
    expect(screen.queryAllByRole('gridcell', { name: /: 0 ms$/ })).toHaveLength(0);
  });

  it.each(['TOKENS', '', '%20', 'bogus'])('?heatmapMetric=%s falls back to Requests', async value => {
    renderHeatmap(`/?chart=heatmap&heatmapMetric=${value}`);
    expect(await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last day' })).toBeTruthy();
    expect(screen.getByRole('combobox', { name: 'Metric' }).value).toBe('requests');
  });
});

describe('loading, error and empty states', () => {
  it('shows loading while pending, then the alert on rejection', async () => {
    const d = deferred();
    api.queryAnalytics.mockImplementation(() => d.promise);
    renderHeatmap('/?chart=heatmap');
    const status = screen.getByRole('status');
    expect(status.textContent).toBe('Loading analytics…');
    await act(async () => d.reject(new Error('boom')));
    expect(screen.getByRole('alert').textContent).toBe('Analytics unavailable: boom');
    expect(screen.queryByText('No activity for this time range')).toBeNull();
    expect(screen.queryByText('Loading analytics…')).toBeNull();
  });

  it.each([
    ['TimeSeries []', { TimeSeries: [] }],
    ['TimeSeries missing', { TimeSeries: undefined }],
    ['all-zero buckets', { TimeSeries: [{ TimestampUtc: '2026-10-24T13:00:00Z', RequestCount: 0, TotalTokens: 0 }] }]
  ])('%s shows the empty message without an alert', async (_, overrides) => {
    api.queryAnalytics.mockImplementation(() => Promise.resolve(dayResponse(overrides)));
    renderHeatmap('/?chart=heatmap');
    expect(await screen.findByText('No activity for this time range')).toBeTruthy();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByRole('grid')).toBeNull();
    expect(screen.getByText('Active buckets').parentElement.textContent).toContain('0 of 96');
  });

  it('a TTFT metric with only null values shows the empty message', async () => {
    api.queryAnalytics.mockImplementation(() => Promise.resolve(dayResponse({ TimeSeries: [{ TimestampUtc: '2026-10-24T13:00:00Z', RequestCount: 4, AverageTimeToFirstTokenMs: null }] })));
    renderHeatmap('/?chart=heatmap&heatmapMetric=ttft');
    expect(await screen.findByText('No activity for this time range')).toBeTruthy();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('a response with no time window still renders the empty message, not an error', async () => {
    api.queryAnalytics.mockImplementation(() => Promise.resolve({}));
    renderHeatmap('/?chart=heatmap');
    expect(await screen.findByText('No activity for this time range')).toBeTruthy();
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('tooltip, aria, summary and legend content', () => {
  it('labels, tooltips and summarizes in UTC', async () => {
    const { container } = renderHeatmap('/?chart=heatmap');
    await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last day' });
    const text = '2026-10-24 13:00-13:15 UTC: 1,234 requests';
    const cell = cellNamed(text);
    expect(cell.tagName.toLowerCase()).toBe('rect');
    expect(cell.getAttribute('class')).toContain('hm-level-4');

    expect(screen.queryByRole('tooltip')).toBeNull();
    fireEvent.mouseEnter(cell);
    expect(screen.getByRole('tooltip').textContent).toBe(text);
    fireEvent.mouseLeave(cell);
    expect(screen.queryByRole('tooltip')).toBeNull();
    act(() => cell.focus());
    expect(document.activeElement).toBe(cell);
    expect(screen.getByRole('tooltip').textContent).toBe(text);
    act(() => cell.blur());
    expect(screen.queryByRole('tooltip')).toBeNull();

    const tile = label => screen.getByText(label, { selector: '.hm-tile-label' }).parentElement.textContent;
    expect(tile('Total')).toBe('Total1,260 requests');
    expect(tile('Peak')).toBe('Peak1,234 requestsat 2026-10-24 13:00 UTC');
    expect(tile('Active buckets')).toBe('Active buckets2 of 96');

    // 100 rects: 96 real gridcells plus 4 padding cells that are hidden and not focusable.
    expect(gridCells()).toHaveLength(96);
    const padding = container.querySelectorAll('svg[role="grid"] rect.hm-padding');
    expect(padding).toHaveLength(4);
    for (const rect of padding) {
      expect(rect.getAttribute('aria-hidden')).toBe('true');
      expect(rect.hasAttribute('role')).toBe(false);
      expect(rect.hasAttribute('tabindex')).toBe(false);
    }
    expect(screen.getAllByRole('row')).toHaveLength(25);

    const legend = container.querySelector('.hm-legend');
    expect(legend.firstElementChild.textContent).toBe('Less');
    expect(legend.lastElementChild.textContent).toBe('More');
    expect([...legend.querySelectorAll('rect')].map(rect => rect.getAttribute('class'))).toEqual([0, 1, 2, 3, 4].map(n => `hm-cell hm-level-${n}`));
    expect(screen.getByText('All times are UTC.')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Copy chart as an image' })).toBeTruthy();
  });

  it('labels daily calendar cells with the end date', async () => {
    renderHeatmap('/?chart=heatmap&heatmapRange=month');
    await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last month' });
    expect(cellNamed('2026-10-01 00:00-2026-10-02 00:00 UTC: 9 requests')).toBeTruthy();
    expect(gridCells()).toHaveLength(30);
  });

  it('labels stay in UTC when the process time zone is not UTC', async () => {
    // jsdom's Date uses the process zone; label text must not depend on it.
    const original = Date.prototype.getHours;
    Date.prototype.getHours = function () { throw new Error('local time getter used'); };
    try {
      renderHeatmap('/?chart=heatmap');
      await screen.findByRole('grid');
      expect(cellNamed('2026-10-24 13:00-13:15 UTC: 1,234 requests')).toBeTruthy();
    } finally {
      Date.prototype.getHours = original;
    }
  });

  it('passes the UTC x-axis label to the chart copy export', () => {
    const source = readFileSync(resolve(root, 'src/components/ActivityHeatmap.jsx'), 'utf8');
    expect(source).toContain("xLabel: 'Bucket start (UTC)'");
    expect(source).toContain('svgRef={svgRef}');
  });
});

describe('keyboard navigation and accessible names', () => {
  const name = time => new RegExp(`^2026-10-24 ${time}-`);
  const tabStops = () => gridCells().filter(cell => cell.getAttribute('tabindex') === '0');

  it('roves a single tab stop across real cells and skips padding', async () => {
    renderHeatmap('/?chart=heatmap');
    const grid = await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last day' });
    expect(grid.tagName.toLowerCase()).toBe('svg');
    expect(screen.getByRole('combobox', { name: 'Timeframe' }).tagName).toBe('SELECT');
    expect(screen.getByRole('combobox', { name: 'Metric' }).tagName).toBe('SELECT');

    expect(tabStops()).toHaveLength(1);
    const first = tabStops()[0];
    expect(first.getAttribute('aria-label')).toMatch(name('00:30'));
    act(() => first.focus());

    const press = key => fireEvent.keyDown(document.activeElement, { key });
    const focused = () => document.activeElement.getAttribute('aria-label');

    press('ArrowLeft'); // first real cell: the two cells to its left are padding
    expect(document.activeElement).toBe(first);
    press('ArrowUp');
    expect(document.activeElement).toBe(first);
    press('ArrowRight');
    expect(focused()).toMatch(name('00:45'));
    press('ArrowRight'); // end of row: stays
    expect(focused()).toMatch(name('00:45'));
    press('ArrowDown');
    expect(focused()).toMatch(name('01:45'));
    press('Home');
    expect(focused()).toMatch(name('01:00'));
    press('ArrowUp'); // 00:00 above is padding and nothing is above it
    expect(focused()).toMatch(name('01:00'));
    press('End');
    expect(focused()).toMatch(name('01:45'));
    expect(tabStops()).toHaveLength(1);
    expect(tabStops()[0]).toBe(document.activeElement);
    expect(screen.getByRole('tooltip').textContent).toBe(focused());

    act(() => cellNamed(name('23:30')).focus());
    press('ArrowDown'); // the cell below (next day 00:30) is trailing padding: focus stays
    expect(focused()).toBe('2026-10-24 23:30-23:45 UTC: 0 requests');
    act(() => cellNamed(name('23:00')).focus());
    press('ArrowDown');
    expect(focused()).toBe('2026-10-25 00:00-00:15 UTC: 0 requests');
    press('ArrowRight');
    expect(focused()).toBe('2026-10-25 00:15-00:30 UTC: 0 requests');
    press('ArrowRight'); // the rest of the last row is padding
    expect(focused()).toBe('2026-10-25 00:15-00:30 UTC: 0 requests');
    press('End');
    expect(focused()).toBe('2026-10-25 00:15-00:30 UTC: 0 requests');
    expect(tabStops()).toHaveLength(1);
  });

  it('ArrowDown and ArrowUp skip interior padding left by coarsened buckets', async () => {
    // Server clamped the hour view to 120 s buckets: 15-minute rows hold 7.5 buckets, so row 12:15
    // has no bucket in its last column while the rows above and below do.
    api.queryAnalytics.mockImplementation(() => Promise.resolve({ StartUtc: '2026-10-24T12:00:00Z', EndUtc: '2026-10-24T13:00:00Z', BucketSeconds: 120, TimeSeries: [{ TimestampUtc: '2026-10-24T12:30:00Z', RequestCount: 2 }] }));
    const { container } = renderHeatmap('/?chart=heatmap&heatmapRange=hour');
    await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last hour' });
    const middleRow = screen.getAllByRole('row')[1];
    expect(middleRow.lastElementChild.getAttribute('class')).toContain('hm-padding');
    expect(container.querySelectorAll('rect.hm-padding').length).toBeGreaterThan(0);
    act(() => cellNamed(name('12:14')).focus());
    fireEvent.keyDown(document.activeElement, { key: 'ArrowDown' });
    expect(document.activeElement.getAttribute('aria-label')).toMatch(name('12:44'));
    fireEvent.keyDown(document.activeElement, { key: 'ArrowUp' });
    expect(document.activeElement.getAttribute('aria-label')).toMatch(name('12:14'));
    expect(tabStops()).toHaveLength(1);
  });

  it('Tab reaches the grid through its single tab stop', async () => {
    const { default: userEvent } = await import('@testing-library/user-event');
    const user = userEvent.setup();
    renderHeatmap('/?chart=heatmap');
    await screen.findByRole('grid');
    screen.getByRole('combobox', { name: 'Metric' }).focus();
    await user.tab(); // copy button sits in the header before the controls, so the next stop is the grid
    expect(document.activeElement.getAttribute('aria-label')).toMatch(name('00:30'));
    await user.tab();
    expect(document.activeElement.getAttribute('role')).not.toBe('gridcell');
  });
});

describe('truncation note threshold', () => {
  const note = 'Results may be truncated at 10,000 requests.';
  it.each([[10000, true], [12000, true], [9999, false], [undefined, false]])('TotalRequests %s shows note: %s', async (total, shown) => {
    api.queryAnalytics.mockImplementation(() => Promise.resolve(dayResponse({ TotalRequests: total })));
    renderHeatmap('/?chart=heatmap');
    await screen.findByRole('grid');
    expect(screen.queryByText(note) !== null).toBe(shown);
    expect(heatmapCalls()).toEqual([{ Range: 'lastDay', BucketSeconds: 900 }]);
    expect('Limit' in heatmapCalls()[0]).toBe(false);
  });
});

describe('heat map files and styles', () => {
  const css = readFileSync(resolve(root, 'src/components/ActivityHeatmap.css'), 'utf8');
  it('imports its own stylesheet and defines five levels from existing variables', () => {
    expect(readFileSync(resolve(root, 'src/components/ActivityHeatmap.jsx'), 'utf8')).toContain("import './ActivityHeatmap.css';");
    expect(css).toMatch(/:root,\s*\[data-theme="dark"\]\s*\{/);
    for (let n = 0; n <= 4; n++) expect(css).toContain(`.hm-level-${n} { fill: var(--hm-level-${n}); }`);
    expect(css).toContain('--hm-level-0: var(--border-color);');
    expect(css).toContain('--hm-level-4: var(--primary-color);');
    expect(css).not.toMatch(/#[0-9a-f]{3,8}\b/i);
    expect(css).toMatch(/\.hm-cell \{\s*stroke: var\(--hm-cell-stroke\);/);
  });
});
