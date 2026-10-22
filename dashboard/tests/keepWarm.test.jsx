import { it, expect } from 'vitest';
import { createDefaultKeepWarm, normalizeKeepWarm, keepWarmToPayload } from '../src/views/VirtualModelRunners';

it('defaults keep-warm to disabled with server-matching values', () => {
  expect(keepWarmToPayload(createDefaultKeepWarm())).toEqual({
    Enabled: false, Models: [], IntervalMs: 300000, KeepAlive: '30m', TimeoutMs: 300000
  });
});

it('round-trips server settings through the form', () => {
  const form = normalizeKeepWarm({ Enabled: true, Models: ['qwen3.5:122b', 'llama3.3:70b'], IntervalMs: 120000, KeepAlive: '24h', TimeoutMs: 900000 });
  expect(form.ModelsText).toBe('qwen3.5:122b, llama3.3:70b');
  expect(keepWarmToPayload(form)).toEqual({
    Enabled: true, Models: ['qwen3.5:122b', 'llama3.3:70b'], IntervalMs: 120000, KeepAlive: '24h', TimeoutMs: 900000
  });
});

it('drops blank model entries and falls back to defaults for empty fields', () => {
  expect(keepWarmToPayload({ Enabled: true, ModelsText: ' a , ,b,', IntervalMs: '', KeepAlive: '  ', TimeoutMs: 'x' })).toEqual({
    Enabled: true, Models: ['a', 'b'], IntervalMs: 300000, KeepAlive: '30m', TimeoutMs: 300000
  });
});

it('treats a missing KeepWarm object as disabled', () => {
  expect(normalizeKeepWarm(undefined).Enabled).toBe(false);
});
