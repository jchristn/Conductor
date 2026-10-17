import React from 'react';
import { MemoryRouter } from 'react-router-dom';
import { it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import TrafficClasses from '../src/views/QosTrafficClasses';
const api = vi.hoisted(() => ({
  listQosTrafficClasses: vi.fn().mockResolvedValue({ Data: [{ Id: 'class-1', Name: 'Agents', Tier: 'AgentInteractive' }] }),
  listTenants: vi.fn().mockResolvedValue({ Data: [{ Id: 'default', Name: 'Default' }] }),
}));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ({ api }) }));
it('uses readable traffic tiers in tables and dropdowns while preserving API values', async () => {
  render(<MemoryRouter><TrafficClasses /></MemoryRouter>);
  expect(await screen.findByText('Interactive agent')).toBeTruthy();
  fireEvent.click(screen.getByRole('button', { name: 'Create Traffic Class' }));
  expect(screen.getByRole('option', { name: 'Interactive agent' }).value).toBe('AgentInteractive');
  expect(screen.getByRole('option', { name: 'Time-bound batch' }).value).toBe('BatchTimebound');
  expect(screen.queryByText('AgentInteractive')).toBeNull();
});
