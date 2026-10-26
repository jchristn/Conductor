import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor, within } from '@testing-library/react';
import Administrators from '../src/views/Administrators';
import { createApp, page, renderView, waitForLoaded, openRowAction, deleteDialog, formDialog, typeInto } from './crudViewHarness';

const ctx = vi.hoisted(() => ({ app: null }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ctx.app }));

const administrators = [
  { Id: 'adm_root', Email: 'root@conductor', FirstName: 'Root', LastName: 'Admin', Active: true },
  { Id: 'adm_ops', Email: 'ops@conductor', FirstName: 'Ops', LastName: 'Team', Active: false }
];

describe('Administrators view', () => {
  beforeEach(() => {
    ctx.app = createApp({ listAdministrators: () => page(administrators) });
  });

  it('renders the administrator list from the API', async () => {
    renderView(<Administrators />);
    expect(await screen.findByText('root@conductor')).toBeTruthy();
    expect(screen.getByText('Root Admin')).toBeTruthy();
    expect(screen.getByText('ops@conductor')).toBeTruthy();
    expect(ctx.app.api.listAdministrators).toHaveBeenCalledWith({ maxResults: 1000 });
  });

  it('shows the empty state when there are no administrators', async () => {
    ctx.app.api.listAdministrators.mockImplementation(() => page([]));
    renderView(<Administrators />);
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(ctx.app.setError).not.toHaveBeenCalled();
  });

  it('surfaces a list failure through setError without crashing', async () => {
    ctx.app.api.listAdministrators.mockImplementation(() => Promise.reject(new Error('forbidden')));
    renderView(<Administrators />);
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to fetch administrators: forbidden'));
    await waitForLoaded();
    expect(screen.getByRole('heading', { name: 'Administrators' })).toBeTruthy();
    expect(screen.getByText('No data available')).toBeTruthy();
  });

  it('creates an administrator with the entered values', async () => {
    ctx.app.api.createAdministrator.mockImplementation(() => Promise.resolve({ Id: 'adm_new' }));
    renderView(<Administrators />);
    await screen.findByText('root@conductor');
    fireEvent.click(screen.getByRole('button', { name: 'Create Administrator' }));
    const form = formDialog();
    typeInto(within(form).getByLabelText('First Name'), 'Linus');
    typeInto(within(form).getByLabelText('Last Name'), 'Torvalds');
    typeInto(within(form).getByLabelText('Email'), 'linus@conductor');
    typeInto(within(form).getByLabelText(/^Password/), 'hunter22');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createAdministrator).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.createAdministrator).toHaveBeenCalledWith({
      FirstName: 'Linus',
      LastName: 'Torvalds',
      Email: 'linus@conductor',
      Password: 'hunter22',
      Active: true
    });
    await waitFor(() => expect(document.querySelector('.modal-body form')).toBeNull());
    expect(ctx.app.api.listAdministrators).toHaveBeenCalledTimes(2);
  });

  it('surfaces a create failure through setError and keeps the form open', async () => {
    ctx.app.api.createAdministrator.mockImplementation(() => Promise.reject(new Error('duplicate email')));
    renderView(<Administrators />);
    await screen.findByText('root@conductor');
    fireEvent.click(screen.getByRole('button', { name: 'Create Administrator' }));
    const form = formDialog();
    typeInto(within(form).getByLabelText('Email'), 'root@conductor');
    typeInto(within(form).getByLabelText(/^Password/), 'pw');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to save administrator: duplicate email'));
    expect(formDialog()).toBeTruthy();
  });

  it('asks for confirmation before deleting and then calls deleteAdministrator', async () => {
    ctx.app.api.deleteAdministrator.mockImplementation(() => Promise.resolve());
    renderView(<Administrators />);
    await screen.findByText('ops@conductor');
    openRowAction('ops@conductor', 'Delete');
    const dialog = deleteDialog();
    expect(within(dialog).getByText('Are you sure you want to delete this administrator?')).toBeTruthy();
    expect(ctx.app.api.deleteAdministrator).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(ctx.app.api.deleteAdministrator).toHaveBeenCalledWith('adm_ops'));
    await waitFor(() => expect(document.querySelector('.delete-confirm-modal')).toBeNull());
  });
});
