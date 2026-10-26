import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor, within } from '@testing-library/react';
import Tenants from '../src/views/Tenants';
import { createApp, createOnboarding, page, renderView, waitForLoaded, openRowAction, deleteDialog, formDialog, typeInto } from './crudViewHarness';

const ctx = vi.hoisted(() => ({ app: null, onboarding: null }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ctx.app }));
vi.mock('../src/context/OnboardingContext', () => ({ useOnboarding: () => ctx.onboarding }));

const tenants = [
  { Id: 'ten_alpha', Name: 'Alpha Corp', Active: true, CreatedUtc: '2026-01-02T03:04:05Z' },
  { Id: 'ten_beta', Name: 'Beta Labs', Active: false, CreatedUtc: '2026-02-03T04:05:06Z' }
];

describe('Tenants view', () => {
  beforeEach(() => {
    ctx.app = createApp({ listTenants: () => page(tenants) });
    ctx.onboarding = createOnboarding();
  });

  it('renders the tenant list from the API', async () => {
    renderView(<Tenants />);
    expect(await screen.findByText('Alpha Corp')).toBeTruthy();
    expect(screen.getByText('Beta Labs')).toBeTruthy();
    expect(screen.getByText('ten_alpha')).toBeTruthy();
    expect(ctx.app.api.listTenants).toHaveBeenCalledWith({ maxResults: 1000 });
  });

  it('shows the empty state when there are no tenants', async () => {
    ctx.app.api.listTenants.mockImplementation(() => page([]));
    renderView(<Tenants />);
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(ctx.app.setError).not.toHaveBeenCalled();
  });

  it('surfaces a list failure through setError without crashing', async () => {
    ctx.app.api.listTenants.mockImplementation(() => Promise.reject(new Error('server down')));
    renderView(<Tenants />);
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to fetch tenants: server down'));
    await waitForLoaded();
    expect(screen.getByRole('heading', { name: 'Tenants' })).toBeTruthy();
    expect(screen.getByText('No data available')).toBeTruthy();
  });

  it('creates a tenant with the entered values', async () => {
    ctx.app.api.createTenant.mockImplementation(() => Promise.resolve({ Id: 'ten_new' }));
    renderView(<Tenants />);
    await screen.findByText('Alpha Corp');
    fireEvent.click(screen.getByRole('button', { name: 'Create Tenant' }));
    const form = formDialog();
    typeInto(within(form).getByLabelText('Name'), 'Gamma Inc');
    fireEvent.click(within(form).getByLabelText('Active'));
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createTenant).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.createTenant).toHaveBeenCalledWith({ Name: 'Gamma Inc', Active: false, Labels: [], Tags: {} });
    expect(ctx.onboarding.onEntityCreated).toHaveBeenCalledWith('tenant');
    await waitFor(() => expect(document.querySelector('.modal-body form')).toBeNull());
    expect(ctx.app.api.listTenants).toHaveBeenCalledTimes(2);
  });

  it('asks for confirmation before deleting and then calls deleteTenant', async () => {
    ctx.app.api.deleteTenant.mockImplementation(() => Promise.resolve());
    renderView(<Tenants />);
    await screen.findByText('Beta Labs');
    openRowAction('Beta Labs', 'Delete');
    const dialog = deleteDialog();
    expect(within(dialog).getByText('Are you sure you want to delete this tenant?')).toBeTruthy();
    expect(within(dialog).getByText('Beta Labs')).toBeTruthy();
    expect(ctx.app.api.deleteTenant).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(ctx.app.api.deleteTenant).toHaveBeenCalledWith('ten_beta'));
    await waitFor(() => expect(document.querySelector('.delete-confirm-modal')).toBeNull());
  });

  it('cancelling the delete confirmation does not call deleteTenant', async () => {
    renderView(<Tenants />);
    await screen.findByText('Alpha Corp');
    openRowAction('Alpha Corp', 'Delete');
    fireEvent.click(within(deleteDialog()).getByRole('button', { name: 'Cancel' }));
    expect(document.querySelector('.delete-confirm-modal')).toBeNull();
    expect(ctx.app.api.deleteTenant).not.toHaveBeenCalled();
  });

  it('surfaces a delete failure through setError', async () => {
    ctx.app.api.deleteTenant.mockImplementation(() => Promise.reject(new Error('in use')));
    renderView(<Tenants />);
    await screen.findByText('Alpha Corp');
    openRowAction('Alpha Corp', 'Delete');
    fireEvent.click(within(deleteDialog()).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to delete tenant: in use'));
    expect(deleteDialog()).toBeTruthy();
    expect(within(document.querySelector('table.data-table')).getByText('Alpha Corp')).toBeTruthy();
  });
});
