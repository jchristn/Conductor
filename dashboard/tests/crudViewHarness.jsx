// Shared helpers for the CRUD view tests (tenantsView, usersView, ...). Not a test file itself.
// Each test file mocks ../src/context/AppContext and ../src/context/OnboardingContext with vi.mock
// (vi.mock is only hoisted inside test files) and points useApp at the object built here.
import React from 'react';
import { vi, expect } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

export const page = (data) => Promise.resolve({ Data: data });

// Every api method not listed in overrides resolves to an empty page, so views that load
// several lists (VMRs, endpoint groups) only need to describe the list under test.
export function createApp(overrides = {}) {
  const methods = {};
  const api = new Proxy(methods, {
    get(target, name) {
      if (typeof name !== 'string' || name === 'then') return undefined;
      if (!(name in target)) target[name] = vi.fn(() => page([]));
      return target[name];
    }
  });
  Object.entries(overrides).forEach(([name, impl]) => { methods[name] = vi.fn(impl); });
  return { api, setError: vi.fn(), isAdmin: false, serverUrl: 'http://conductor.test' };
}

export function createOnboarding() {
  return { pendingCreate: null, clearPendingCreate: vi.fn(), onEntityCreated: vi.fn() };
}

export function renderView(element) {
  return render(<MemoryRouter>{element}</MemoryRouter>);
}

export async function waitForLoaded() {
  await waitFor(() => expect(screen.queryByText('Loading...')).toBeNull());
}

export function rowFor(text) {
  const row = screen.getByText(text).closest('tr');
  expect(row).not.toBeNull();
  return row;
}

export function openRowAction(text, label) {
  fireEvent.click(rowFor(text).querySelector('.action-menu-trigger'));
  const dropdown = document.querySelector('.action-menu-dropdown');
  expect(dropdown).not.toBeNull();
  fireEvent.click(within(dropdown).getByRole('button', { name: label }));
}

export function deleteDialog() {
  const dialog = document.querySelector('.delete-confirm-modal');
  expect(dialog).not.toBeNull();
  return dialog;
}

export function formDialog() {
  const form = document.querySelector('.modal-body form');
  expect(form).not.toBeNull();
  return form;
}

export function typeInto(input, value) {
  fireEvent.change(input, { target: { value } });
}
