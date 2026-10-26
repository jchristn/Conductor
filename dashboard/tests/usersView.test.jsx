import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor, within } from '@testing-library/react';
import Users from '../src/views/Users';
import { createApp, createOnboarding, page, renderView, waitForLoaded, openRowAction, deleteDialog, formDialog, typeInto } from './crudViewHarness';

const ctx = vi.hoisted(() => ({ app: null, onboarding: null }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ctx.app }));
vi.mock('../src/context/OnboardingContext', () => ({ useOnboarding: () => ctx.onboarding }));

const tenants = [{ Id: 'ten_alpha', Name: 'Alpha Corp', Active: true }];
const users = [
  { Id: 'usr_ada', TenantId: 'ten_alpha', Email: 'ada@example.com', FirstName: 'Ada', LastName: 'Lovelace', Active: true, IsAdmin: true },
  { Id: 'usr_bob', TenantId: 'ten_alpha', Email: 'bob@example.com', FirstName: 'Bob', LastName: 'Builder', Active: true, IsTenantAdmin: false }
];

describe('Users view', () => {
  beforeEach(() => {
    ctx.app = createApp({ listUsers: () => page(users), listTenants: () => page(tenants) });
    ctx.onboarding = createOnboarding();
  });

  it('renders the user list from the API', async () => {
    renderView(<Users />);
    expect(await screen.findByText('ada@example.com')).toBeTruthy();
    expect(screen.getByText('Ada Lovelace')).toBeTruthy();
    expect(screen.getByText('bob@example.com')).toBeTruthy();
    expect(screen.getByText('Bob Builder')).toBeTruthy();
    expect(ctx.app.api.listUsers).toHaveBeenCalledWith({ maxResults: 1000 });
  });

  it('shows the empty state when there are no users', async () => {
    ctx.app.api.listUsers.mockImplementation(() => page([]));
    renderView(<Users />);
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(ctx.app.setError).not.toHaveBeenCalled();
  });

  it('surfaces a list failure through setError without crashing', async () => {
    ctx.app.api.listUsers.mockImplementation(() => Promise.reject(new Error('server down')));
    renderView(<Users />);
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to fetch users: server down'));
    await waitForLoaded();
    expect(screen.getByRole('heading', { name: 'Users' })).toBeTruthy();
    expect(screen.getByText('No data available')).toBeTruthy();
  });

  it('creates a user with the entered values', async () => {
    ctx.app.api.createUser.mockImplementation(() => Promise.resolve({ Id: 'usr_new' }));
    renderView(<Users />);
    await screen.findByText('ada@example.com');
    fireEvent.click(screen.getByRole('button', { name: 'Create User' }));
    const form = formDialog();
    await within(form).findByRole('option', { name: 'Alpha Corp (ten_alpha)' });
    fireEvent.change(within(form).getByLabelText('Tenant'), { target: { value: 'ten_alpha' } });
    typeInto(within(form).getByLabelText('First Name'), 'Grace');
    typeInto(within(form).getByLabelText('Last Name'), 'Hopper');
    typeInto(within(form).getByLabelText('Email'), 'grace@example.com');
    typeInto(within(form).getByLabelText(/^Password/), 's3cret!');
    fireEvent.click(within(form).getByLabelText('Tenant Admin'));
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createUser).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.createUser).toHaveBeenCalledWith({
      TenantId: 'ten_alpha',
      FirstName: 'Grace',
      LastName: 'Hopper',
      Email: 'grace@example.com',
      Password: 's3cret!',
      Active: true,
      IsAdmin: false,
      IsTenantAdmin: true,
      Labels: [],
      Tags: {}
    });
    expect(ctx.onboarding.onEntityCreated).toHaveBeenCalledWith('user');
    await waitFor(() => expect(document.querySelector('.modal-body form')).toBeNull());
  });

  it('asks for confirmation before deleting and then calls deleteUser with the tenant', async () => {
    ctx.app.api.deleteUser.mockImplementation(() => Promise.resolve());
    renderView(<Users />);
    await screen.findByText('bob@example.com');
    openRowAction('bob@example.com', 'Delete');
    const dialog = deleteDialog();
    expect(within(dialog).getByText('Are you sure you want to delete this user?')).toBeTruthy();
    expect(ctx.app.api.deleteUser).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(ctx.app.api.deleteUser).toHaveBeenCalledWith('usr_bob', 'ten_alpha'));
    await waitFor(() => expect(document.querySelector('.delete-confirm-modal')).toBeNull());
    expect(ctx.app.api.listUsers).toHaveBeenCalledTimes(2);
  });

  it('cancelling the delete confirmation does not call deleteUser', async () => {
    renderView(<Users />);
    await screen.findByText('ada@example.com');
    openRowAction('ada@example.com', 'Delete');
    fireEvent.click(within(deleteDialog()).getByRole('button', { name: 'Cancel' }));
    expect(document.querySelector('.delete-confirm-modal')).toBeNull();
    expect(ctx.app.api.deleteUser).not.toHaveBeenCalled();
  });
});
