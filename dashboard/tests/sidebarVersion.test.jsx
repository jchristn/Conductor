import React from 'react';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import Sidebar from '../src/components/Sidebar';
import { NAV_SECTIONS, itemVisible } from '../src/config/navConfig';

const app = vi.hoisted(() => ({ value: { isAdmin: false, currentUser: { IsAdmin: false } } }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => app.value }));

const pkg = JSON.parse(readFileSync(resolve(dirname(fileURLToPath(import.meta.url)), '../package.json'), 'utf8'));

function renderSidebar() {
  return render(<MemoryRouter><Sidebar /></MemoryRouter>);
}

describe('Sidebar version footer', () => {
  afterEach(() => { vi.unstubAllGlobals(); });

  it.each([
    ['non-admin', { isAdmin: false, currentUser: { IsAdmin: false } }],
    ['admin', { isAdmin: true, currentUser: { IsAdmin: true } }]
  ])('renders version footer as last nav child (%s)', (_label, auth) => {
    app.value = auth;
    const { container } = renderSidebar();
    const nav = container.querySelector('nav.sidebar');
    const last = nav.lastElementChild;
    expect(last.className).toBe('sidebar-version');
    expect(last.textContent).toBe('v' + pkg.version);
    expect(last.textContent).toBe('v0.6.1');
    expect(last.getAttribute('aria-label')).toBe('Conductor version ' + pkg.version);
    expect(last.getAttribute('aria-label')).toBe('Conductor version 0.6.1');
    const hasAdminAccess = auth.isAdmin || auth.currentUser?.IsAdmin;
    const expected = NAV_SECTIONS.flatMap((s) => s.items)
      .filter((item) => itemVisible(item, { isAdmin: auth.isAdmin, hasAdminAccess }));
    expect(expected.length).toBeGreaterThan(0);
    expect(nav.querySelectorAll('a.nav-link').length).toBe(expected.length);
    expect(container.querySelectorAll('.sidebar-version').length).toBe(1);
  });

  it('omits footer when version is empty', () => {
    vi.stubGlobal('__APP_VERSION__', '');
    app.value = { isAdmin: false, currentUser: { IsAdmin: false } };
    const { container } = renderSidebar();
    expect(container.querySelector('.sidebar-version')).toBeNull();
    const text = container.querySelector('nav.sidebar').textContent;
    expect(text).not.toContain('vundefined');
    expect(container.querySelectorAll('a.nav-link').length).toBeGreaterThan(0);
  });
});
