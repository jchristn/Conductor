import React from 'react';
import { it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import History from '../src/components/RequestHistoryChart';
import Health from '../src/components/HealthHistogram';
const api = vi.hoisted(() => ({
 listVirtualModelRunners: vi.fn().mockResolvedValue({ Data: [] }),
 getRequestHistorySummary: vi.fn().mockResolvedValue({ Buckets: [], TotalSuccess: 0, TotalFailure: 0, TotalRequests: 0 })
}));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ({ api }) }));
it('offers PNG copy for the main dashboard request history chart', async () => {
 const { container } = render(<History />);
 await waitFor(() => expect(api.getRequestHistorySummary).toHaveBeenCalled());
 await waitFor(() => expect(container.querySelector('svg[viewBox^="0 0 800"]')).toBeTruthy());
 expect(screen.getByRole('button', { name: 'Copy chart as an image' })).toBeTruthy();
 expect(screen.getByLabelText('Virtual model runner').title).toContain('Limit request history');
});
it('offers PNG copy for endpoint health histograms while preserving empty state', () => {
 const { rerender } = render(<Health history={[]} />);
 expect(screen.getByText('No data')).toBeTruthy(); expect(screen.queryByRole('button')).toBeNull();
 rerender(<Health history={[{ TimestampUtc: new Date().toISOString(), Success: true }]} />);
 expect(screen.getByRole('button', { name: 'Copy chart as an image' })).toBeTruthy();
});
