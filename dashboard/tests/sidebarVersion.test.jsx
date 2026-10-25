import React from 'react';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { execFileSync } from 'node:child_process';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import Sidebar from '../src/components/Sidebar';
import { NAV_SECTIONS, itemVisible } from '../src/config/navConfig';

const app = vi.hoisted(() => ({ value: { isAdmin: false, currentUser: { IsAdmin: false } } }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => app.value }));

const dashboardDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const pkg = JSON.parse(readFileSync(resolve(dashboardDir, 'package.json'), 'utf8'));

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

  it.each([
    ['null', null],
    ['undefined', undefined]
  ])('omits footer when version is %s', (_label, value) => {
    vi.stubGlobal('__APP_VERSION__', value);
    app.value = { isAdmin: false, currentUser: { IsAdmin: false } };
    const { container } = renderSidebar();
    const nav = container.querySelector('nav.sidebar');
    expect(container.querySelector('.sidebar-version')).toBeNull();
    expect(nav.textContent).not.toContain('vundefined');
    expect(nav.textContent).not.toContain('vnull');
    expect(nav.lastElementChild.className).toBe('nav-group');
  });

  it('omits footer without throwing when the global is not declared at all', () => {
    const original = Object.getOwnPropertyDescriptor(globalThis, '__APP_VERSION__');
    expect(original).toBeDefined();
    delete globalThis.__APP_VERSION__;
    try {
      expect('__APP_VERSION__' in globalThis).toBe(false);
      app.value = { isAdmin: false, currentUser: { IsAdmin: false } };
      let result;
      expect(() => { result = renderSidebar(); }).not.toThrow();
      expect(result.container.querySelector('.sidebar-version')).toBeNull();
    } finally {
      Object.defineProperty(globalThis, '__APP_VERSION__', original);
    }
  });

  it('renders the injected value rather than a hardcoded version', () => {
    vi.stubGlobal('__APP_VERSION__', '9.8.7-test');
    app.value = { isAdmin: false, currentUser: { IsAdmin: false } };
    const { container } = renderSidebar();
    const footer = container.querySelector('nav.sidebar').lastElementChild;
    expect(footer.className).toBe('sidebar-version');
    expect(footer.textContent).toBe('v9.8.7-test');
    expect(footer.getAttribute('aria-label')).toBe('Conductor version 9.8.7-test');
    expect(container.textContent).not.toContain('0.6.1');
  });

  it('exposes the package.json version as the global through the vitest define', () => {
    expect(typeof __APP_VERSION__).toBe('string');
    expect(__APP_VERSION__).toBe(pkg.version);
  });

  it.each(['vite.config.js', 'vitest.config.js'])('%s defines __APP_VERSION__ from package.json', (file) => {
    // Evaluate the real config in a child Node process: jsdom's URL rejects the config's file: URL.
    const script = 'const m = await import(process.argv[1]); process.stdout.write(JSON.stringify(m.default.define ?? null));';
    const out = execFileSync(process.execPath, ['--input-type=module', '-e', script, pathToFileURL(resolve(dashboardDir, file)).href],
      { cwd: dashboardDir, encoding: 'utf8', timeout: 30000 });
    const define = JSON.parse(out);
    expect(define).not.toBeNull();
    expect(define.__APP_VERSION__).toBe(JSON.stringify(pkg.version));
    const source = readFileSync(resolve(dashboardDir, file), 'utf8');
    expect(source).toContain('package.json');
    expect(source).not.toContain(pkg.version);
  });

  it('keeps the Sidebar source free of a hardcoded version', () => {
    const source = readFileSync(resolve(dashboardDir, 'src/components/Sidebar.jsx'), 'utf8');
    expect(source).toContain('__APP_VERSION__');
    expect(source).not.toContain(pkg.version);
  });

  it('styles .sidebar-version as an in-flow footer pinned with margin-top:auto', () => {
    const css = readFileSync(resolve(dashboardDir, 'src/index.css'), 'utf8');
    const rules = [...css.matchAll(/(^|\})\s*\.sidebar-version\s*\{([^}]*)\}/g)];
    expect(rules.length).toBe(1);
    const body = rules[0][2].replace(/\s+/g, ' ');
    expect(body).toMatch(/margin-top:\s*auto\s*;/);
    expect(body).toMatch(/flex-shrink:\s*0\s*;/);
    expect(body).toMatch(/color:\s*var\(--sidebar-text\)\s*;/);
    expect(body).not.toMatch(/position\s*:/);
    expect(css).not.toMatch(/\.sidebar-version[^{]*\{[^}]*position\s*:\s*(fixed|absolute|sticky)/);
  });
});
