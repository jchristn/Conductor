import { it, expect, vi } from 'vitest';
it('resolves same-origin API prefixes to a valid absolute login URL', async () => {
 vi.resetModules(); window.CONDUCTOR_SERVER_URL = '/api';
 const { DEFAULT_SERVER_URL } = await import('../src/config/serverUrl');
 expect(DEFAULT_SERVER_URL).toBe(`${window.location.origin}/api`);
 delete window.CONDUCTOR_SERVER_URL;
});
