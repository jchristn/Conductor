import React from 'react';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import Sidebar from '../src/components/Sidebar';
import Dashboard from '../src/views/Dashboard';

// Integration checks for voyage vyg_mvn42uia: the version footer (mission 1) and the Heatmap
// chart tab (missions 2-3) rendered together, plus the Dashboard chart tab keys and param reset.

const dashboardDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const pkg = JSON.parse(readFileSync(resolve(dashboardDir, 'package.json'), 'utf8'));

const api = vi.hoisted(() => ({
  queryAnalytics: vi.fn(),
  getObservabilityMetricsSummary: vi.fn(),
  listVirtualModelRunners: vi.fn(),
  getRequestHistorySummary: vi.fn()
}));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ({ api, counts: {}, fetchCounts: () => {}, isAdmin: true, currentUser: { IsAdmin: true } }) }));

const dayResponse = () => ({
  StartUtc: '2026-10-24T00:00:00Z', EndUtc: '2026-10-25T00:00:00Z', BucketSeconds: 900, TotalRequests: 5,
  TimeSeries: [{ TimestampUtc: '2026-10-24T13:00:00Z', RequestCount: 5, SuccessfulCompletionCount: 5 }]
});
const weekResponse = () => ({
  StartUtc: '2026-10-17T13:00:00Z', EndUtc: '2026-10-24T13:00:00Z', BucketSeconds: 3600, TotalRequests: 7,
  TimeSeries: [{ TimestampUtc: '2026-10-20T08:00:00Z', RequestCount: 7, SuccessfulCompletionCount: 7, TotalTokens: 70 }]
});
const heatmapResponse = body => body.Range === 'lastWeek' ? weekResponse() : dayResponse();
const dashboardResponse = () => ({ StartUtc: '2026-10-24T00:00:00Z', EndUtc: '2026-10-25T00:00:00Z', BucketSeconds: 3600, TimeSeries: [] });

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="search">{location.search}</div>;
}
function renderApp(entry) {
  return render(<MemoryRouter initialEntries={[entry]}><Sidebar /><Dashboard /><LocationProbe /></MemoryRouter>);
}
const isHeatmapBody = body => Boolean(body) && 'BucketSeconds' in body;
const heatmapCalls = () => api.queryAnalytics.mock.calls.map(call => call[0]).filter(isHeatmapBody);
const search = () => new URLSearchParams(screen.getByTestId('search').textContent);
const chartTab = name => within(screen.getByRole('tablist', { name: 'Dashboard charts' })).getByRole('tab', { name });

beforeEach(() => {
  // Capture is installed before every render so the first effect's call is observed.
  api.queryAnalytics.mockReset();
  api.queryAnalytics.mockImplementation(body => Promise.resolve(isHeatmapBody(body) ? heatmapResponse(body) : dashboardResponse()));
  api.getObservabilityMetricsSummary.mockReset().mockResolvedValue(null);
  api.listVirtualModelRunners.mockReset().mockResolvedValue({ Data: [] });
  api.getRequestHistorySummary.mockReset().mockResolvedValue({ Buckets: [], TotalSuccess: 0, TotalFailure: 0, TotalRequests: 0 });
});

describe('voyage integration: version footer and heatmap tab together', () => {
  it('renders the sidebar version footer and the heat map grid in one app tree', async () => {
    const { container } = renderApp('/?chart=heatmap');
    const nav = container.querySelector('nav.sidebar');
    expect(nav.lastElementChild.className).toBe('sidebar-version');
    expect(nav.lastElementChild.textContent).toBe('v' + pkg.version);
    expect(nav.lastElementChild.getAttribute('aria-label')).toBe('Conductor version ' + pkg.version);
    expect(await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last day' })).toBeTruthy();
    // The capture saw both known requests: Dashboard's own lastDay call and exactly one heat map call.
    expect(api.queryAnalytics).toHaveBeenCalledWith({ Range: 'lastDay' });
    expect(heatmapCalls()).toEqual([{ Range: 'lastDay', BucketSeconds: 900 }]);
    expect(container.querySelectorAll('.sidebar-version').length).toBe(1);
  });

  it('keeps the existing chart tab keys and order, with heatmap last (D26)', async () => {
    renderApp('/');
    const expected = [['Request history', 'history'], ['Volume / TTFT', 'volume'], ['Latency / TTFT', 'latency'], ['Tokens', 'tokens'], ['Heatmap', 'heatmap']];
    const tabs = within(screen.getByRole('tablist', { name: 'Dashboard charts' })).getAllByRole('tab');
    expect(tabs.map(tab => tab.textContent)).toEqual(expected.map(([label]) => label));
    for (const [label, key] of [...expected.slice(1), expected[0]]) {
      fireEvent.click(chartTab(label));
      await waitFor(() => expect(search().get('chart')).toBe(key));
      expect(chartTab(label).getAttribute('aria-selected')).toBe('true');
    }
    await waitFor(() => expect(api.getRequestHistorySummary).toHaveBeenCalled());
  });

  it('switching chart tabs resets heatmap params, and returning re-queries with the defaults (D10)', async () => {
    renderApp('/?chart=heatmap&heatmapRange=week&heatmapMetric=tokens&keep=1');
    await screen.findByRole('grid', { name: 'Activity heatmap, Total tokens, Last week' });
    expect(heatmapCalls()).toEqual([{ Range: 'lastWeek', BucketSeconds: 3600 }]);

    fireEvent.click(chartTab('Tokens'));
    await waitFor(() => expect(search().get('chart')).toBe('tokens'));
    expect(search().get('heatmapRange')).toBeNull();
    expect(search().get('heatmapMetric')).toBeNull();
    expect(search().get('keep')).toBeNull();
    expect(screen.queryByRole('grid')).toBeNull();

    fireEvent.click(chartTab('Heatmap'));
    expect(await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last day' })).toBeTruthy();
    expect(heatmapCalls()).toEqual([{ Range: 'lastWeek', BucketSeconds: 3600 }, { Range: 'lastDay', BucketSeconds: 900 }]);
    expect(screen.getByLabelText('Timeframe').value).toBe('day');
    expect(screen.getByLabelText('Metric').value).toBe('requests');
  });

  it('a Dashboard lastDay failure does not hide the heat map, and a heat map failure does not hide the other tabs', async () => {
    api.queryAnalytics.mockImplementation(body => isHeatmapBody(body) ? Promise.resolve(dayResponse()) : Promise.reject(new Error('dashboard down')));
    renderApp('/?chart=heatmap');
    expect(await screen.findByRole('grid', { name: 'Activity heatmap, Requests, Last day' })).toBeTruthy();
    expect((await screen.findAllByRole('alert')).map(node => node.textContent)).toEqual(['Analytics unavailable: dashboard down']);

    api.queryAnalytics.mockImplementation(body => isHeatmapBody(body) ? Promise.reject(new Error('heatmap down')) : Promise.resolve(dashboardResponse()));
    fireEvent.change(screen.getByLabelText('Timeframe'), { target: { value: 'hour' } });
    await waitFor(() => expect(screen.getAllByRole('alert').map(node => node.textContent)).toContain('Analytics unavailable: heatmap down'));
    expect(heatmapCalls().at(-1)).toEqual({ Range: 'lastHour', BucketSeconds: 60 });
    expect(screen.queryByRole('grid')).toBeNull();
    expect(within(screen.getByRole('tablist', { name: 'Dashboard charts' })).getAllByRole('tab')).toHaveLength(5);
    // The sidebar footer is unaffected by either analytics failure.
    expect(document.querySelector('nav.sidebar').lastElementChild.textContent).toBe('v' + pkg.version);
  });
});
