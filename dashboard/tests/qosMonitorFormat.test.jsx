import { it, expect } from 'vitest';
import { formatRecentUtc } from '../src/views/QosMonitor';

const now = new Date(2026, 9, 22, 18, 30, 0);

it('shows only the time for timestamps from today', () => {
  const value = new Date(2026, 9, 22, 11, 4, 47).toISOString();
  const text = formatRecentUtc(value, now);
  expect(text).not.toMatch(/2026/);
  expect(text).toMatch(/04/);
});

it('shows a short date for older timestamps', () => {
  const value = new Date(2026, 9, 20, 9, 15, 0).toISOString();
  expect(formatRecentUtc(value, now)).toMatch(/20/);
});

it('shows a dash when there is no timestamp', () => {
  expect(formatRecentUtc(null, now)).toBe('-');
});
