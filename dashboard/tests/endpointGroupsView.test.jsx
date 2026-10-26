import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor, within } from '@testing-library/react';
import EndpointGroups from '../src/views/EndpointGroups';
import { createApp, page, renderView, waitForLoaded, rowFor, openRowAction, deleteDialog, formDialog, typeInto } from './crudViewHarness';

const ctx = vi.hoisted(() => ({ app: null }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ctx.app }));

const tenants = [{ Id: 'ten_alpha', Name: 'Alpha Corp', Active: true }];
const endpoints = [
  { Id: 'mre_gpu1', TenantId: 'ten_alpha', Name: 'gpu-box-1', Hostname: '10.0.0.11', Port: 11434, UseSsl: false },
  { Id: 'mre_gpu2', TenantId: 'ten_alpha', Name: 'gpu-box-2', Hostname: '10.0.0.12', Port: 11434, UseSsl: false }
];
const groups = [
  { Id: 'eg_primary', TenantId: 'ten_alpha', Name: 'Primary pool', Priority: 0, TrafficWeight: 100, EndpointIds: ['mre_gpu1', 'mre_gpu2'], Active: true },
  { Id: 'eg_overflow', TenantId: 'ten_alpha', Name: 'Overflow pool', Priority: 1, TrafficWeight: 25, EndpointIds: [], Active: false }
];

describe('Endpoint Groups view', () => {
  beforeEach(() => {
    ctx.app = createApp({
      listEndpointGroups: () => page(groups),
      listTenants: () => page(tenants),
      listModelRunnerEndpoints: () => page(endpoints),
      validateEndpointGroup: () => Promise.resolve({ IsValid: true, Errors: [], Warnings: [] })
    });
  });

  it('renders the endpoint group list from the API with tenant names and endpoint counts', async () => {
    renderView(<EndpointGroups />);
    expect(await screen.findByText('Primary pool')).toBeTruthy();
    const primary = rowFor('Primary pool');
    expect(within(primary).getByText('Alpha Corp')).toBeTruthy();
    expect(within(primary).getByText('2')).toBeTruthy();
    expect(within(rowFor('Overflow pool')).getByText('Inactive')).toBeTruthy();
    expect(ctx.app.api.listEndpointGroups).toHaveBeenCalledWith({ maxResults: 1000 });
  });

  it('shows the empty state when there are no endpoint groups', async () => {
    ctx.app.api.listEndpointGroups.mockImplementation(() => page([]));
    renderView(<EndpointGroups />);
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(ctx.app.setError).not.toHaveBeenCalled();
  });

  it('surfaces a list failure through setError without crashing', async () => {
    ctx.app.api.listEndpointGroups.mockImplementation(() => Promise.reject(new Error('bad gateway')));
    renderView(<EndpointGroups />);
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to fetch endpoint groups: bad gateway'));
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Create Group' })).toBeTruthy();
  });

  it('validates and then creates an endpoint group with the entered values', async () => {
    ctx.app.api.createEndpointGroup.mockImplementation(() => Promise.resolve({ Id: 'eg_new' }));
    renderView(<EndpointGroups />);
    await screen.findByText('Primary pool');
    fireEvent.click(screen.getByRole('button', { name: 'Create Group' }));
    const form = formDialog();
    fireEvent.change(within(form).getByLabelText('Tenant'), { target: { value: 'ten_alpha' } });
    typeInto(within(form).getByLabelText('Name'), 'Canary pool');
    typeInto(within(form).getByLabelText('Description'), 'Five percent canary');
    typeInto(within(form).getByLabelText('Priority'), '2');
    typeInto(within(form).getByLabelText('Traffic Weight'), '5');
    fireEvent.click(within(form).getByText('gpu-box-2'));
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    const expected = {
      TenantId: 'ten_alpha',
      Name: 'Canary pool',
      Description: 'Five percent canary',
      Priority: 2,
      Active: true,
      TrafficWeight: 5,
      EndpointIds: ['mre_gpu2'],
      Labels: [],
      Tags: {}
    };
    await waitFor(() => expect(ctx.app.api.createEndpointGroup).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.validateEndpointGroup).toHaveBeenCalledWith(expected, null);
    expect(ctx.app.api.createEndpointGroup).toHaveBeenCalledWith(expected);
    await waitFor(() => expect(document.querySelector('.modal-body form')).toBeNull());
  });

  it('does not create the group when validation reports blocking errors', async () => {
    ctx.app.api.validateEndpointGroup.mockImplementation(() => Promise.resolve({ IsValid: false, Errors: [{ Message: 'Name already in use.' }] }));
    renderView(<EndpointGroups />);
    await screen.findByText('Primary pool');
    fireEvent.click(screen.getByRole('button', { name: 'Create Group' }));
    const form = formDialog();
    typeInto(within(form).getByLabelText('Name'), 'Primary pool');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    expect(await within(form).findByText('Name already in use.')).toBeTruthy();
    expect(ctx.app.setError).toHaveBeenCalledWith('Resolve the blocking validation issues before saving this endpoint group.');
    expect(ctx.app.api.createEndpointGroup).not.toHaveBeenCalled();
  });

  it('asks for confirmation before deleting and then calls deleteEndpointGroup with the tenant', async () => {
    ctx.app.api.deleteEndpointGroup.mockImplementation(() => Promise.resolve());
    renderView(<EndpointGroups />);
    await screen.findByText('Overflow pool');
    openRowAction('Overflow pool', 'Delete');
    const dialog = deleteDialog();
    expect(within(dialog).getByText('Delete endpoint group "Overflow pool"? It will be detached from virtual model runners.')).toBeTruthy();
    expect(ctx.app.api.deleteEndpointGroup).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(ctx.app.api.deleteEndpointGroup).toHaveBeenCalledWith('eg_overflow', 'ten_alpha'));
    await waitFor(() => expect(document.querySelector('.delete-confirm-modal')).toBeNull());
  });
});
