import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor, within } from '@testing-library/react';
import Credentials from '../src/views/Credentials';
import { createApp, createOnboarding, page, renderView, waitForLoaded, openRowAction, deleteDialog, formDialog, typeInto } from './crudViewHarness';

const ctx = vi.hoisted(() => ({ app: null, onboarding: null }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ctx.app }));
vi.mock('../src/context/OnboardingContext', () => ({ useOnboarding: () => ctx.onboarding }));

const tenants = [{ Id: 'ten_alpha', Name: 'Alpha Corp', Active: true }];
const users = [{ Id: 'usr_ada', TenantId: 'ten_alpha', Email: 'ada@example.com' }];
const credentials = [
  { Id: 'cred_ci', TenantId: 'ten_alpha', Name: 'CI pipeline', BearerToken: 'abcdefghijklmnopqrstuvwxyz', Active: true },
  { Id: 'cred_old', TenantId: 'ten_alpha', Name: 'Legacy key', BearerToken: '0123456789abcdefXYZ', Active: false }
];

describe('Credentials view', () => {
  beforeEach(() => {
    ctx.app = createApp({
      listCredentials: () => page(credentials),
      listTenants: () => page(tenants),
      listUsers: () => page(users)
    });
    ctx.onboarding = createOnboarding();
  });

  it('renders the credential list from the API with truncated tokens', async () => {
    renderView(<Credentials />);
    expect(await screen.findByText('CI pipeline')).toBeTruthy();
    expect(screen.getByText('Legacy key')).toBeTruthy();
    expect(screen.getByText('abcdefghijklmnop...')).toBeTruthy();
    expect(screen.queryByText('abcdefghijklmnopqrstuvwxyz')).toBeNull();
    expect(ctx.app.api.listCredentials).toHaveBeenCalledWith({ maxResults: 1000 });
  });

  it('shows the empty state when there are no credentials', async () => {
    ctx.app.api.listCredentials.mockImplementation(() => page([]));
    renderView(<Credentials />);
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(ctx.app.setError).not.toHaveBeenCalled();
  });

  it('surfaces a list failure through setError without crashing', async () => {
    ctx.app.api.listCredentials.mockImplementation(() => Promise.reject(new Error('timeout')));
    renderView(<Credentials />);
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to fetch credentials: timeout'));
    await waitForLoaded();
    expect(screen.getByRole('heading', { name: 'Credentials' })).toBeTruthy();
    expect(screen.getByText('No data available')).toBeTruthy();
  });

  it('creates a credential with the entered values', async () => {
    ctx.app.api.createCredential.mockImplementation(() => Promise.resolve({ Id: 'cred_new' }));
    renderView(<Credentials />);
    await screen.findByText('CI pipeline');
    fireEvent.click(screen.getByRole('button', { name: 'Create Credential' }));
    const form = formDialog();
    await within(form).findByRole('option', { name: 'ada@example.com (usr_ada)' });
    fireEvent.change(within(form).getByLabelText('Tenant'), { target: { value: 'ten_alpha' } });
    typeInto(within(form).getByLabelText('Name'), 'Deploy bot');
    fireEvent.change(within(form).getByLabelText('User (optional)'), { target: { value: 'usr_ada' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createCredential).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.createCredential).toHaveBeenCalledWith({
      TenantId: 'ten_alpha',
      Name: 'Deploy bot',
      UserId: 'usr_ada',
      Active: true,
      Labels: [],
      Tags: {}
    });
    expect(ctx.onboarding.onEntityCreated).toHaveBeenCalledWith('credential');
    await waitFor(() => expect(document.querySelector('.modal-body form')).toBeNull());
  });

  it('asks for confirmation before deleting and then calls deleteCredential with the tenant', async () => {
    ctx.app.api.deleteCredential.mockImplementation(() => Promise.resolve());
    renderView(<Credentials />);
    await screen.findByText('Legacy key');
    openRowAction('Legacy key', 'Delete');
    const dialog = deleteDialog();
    expect(within(dialog).getByText('Are you sure you want to delete this credential?')).toBeTruthy();
    expect(ctx.app.api.deleteCredential).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(ctx.app.api.deleteCredential).toHaveBeenCalledWith('cred_old', 'ten_alpha'));
    await waitFor(() => expect(document.querySelector('.delete-confirm-modal')).toBeNull());
  });
});
