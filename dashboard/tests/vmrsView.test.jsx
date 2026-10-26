import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor, within } from '@testing-library/react';
import VirtualModelRunners from '../src/views/VirtualModelRunners';
import { createApp, createOnboarding, page, renderView, waitForLoaded, rowFor, openRowAction, deleteDialog, formDialog, typeInto } from './crudViewHarness';

const ctx = vi.hoisted(() => ({ app: null, onboarding: null }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ctx.app }));
vi.mock('../src/context/OnboardingContext', () => ({ useOnboarding: () => ctx.onboarding }));

const tenants = [{ Id: 'ten_alpha', Name: 'Alpha Corp', Active: true }];
const endpoints = [{ Id: 'mre_gpu1', TenantId: 'ten_alpha', Name: 'gpu-box-1', Hostname: '10.0.0.11', Port: 11434, Active: true }];
const vmrs = [
  { Id: 'vmr_chat', TenantId: 'ten_alpha', Name: 'Chat VMR', BasePath: '/v1.0/api/chat/', ApiType: 'OpenAI', LoadBalancingMode: 'RoundRobin', ModelRunnerEndpointIds: ['mre_gpu1'], Active: true },
  { Id: 'vmr_embed', TenantId: 'ten_alpha', Name: 'Embeddings VMR', BasePath: '/v1.0/api/embed/', ApiType: 'Ollama', LoadBalancingMode: 'Random', ModelRunnerEndpointIds: [], Active: true }
];

describe('Virtual Model Runners view', () => {
  beforeEach(() => {
    ctx.app = createApp({
      listVirtualModelRunners: () => page(vmrs),
      listTenants: () => page(tenants),
      listModelRunnerEndpoints: () => page(endpoints),
      validateVirtualModelRunner: () => Promise.resolve({ IsValid: true, Errors: [], Warnings: [] })
    });
    ctx.onboarding = createOnboarding();
  });

  it('renders the VMR list from the API', async () => {
    renderView(<VirtualModelRunners />);
    expect(await screen.findByText('Chat VMR')).toBeTruthy();
    expect(within(rowFor('Chat VMR')).getByText('/v1.0/api/chat/')).toBeTruthy();
    expect(within(rowFor('Embeddings VMR')).getByText('/v1.0/api/embed/')).toBeTruthy();
    expect(ctx.app.api.listVirtualModelRunners).toHaveBeenCalledWith({ maxResults: 1000 });
  });

  it('shows the empty state when there are no VMRs', async () => {
    ctx.app.api.listVirtualModelRunners.mockImplementation(() => page([]));
    renderView(<VirtualModelRunners />);
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(ctx.app.setError).not.toHaveBeenCalled();
  });

  it('surfaces a list failure through setError without crashing', async () => {
    ctx.app.api.listVirtualModelRunners.mockImplementation(() => Promise.reject(new Error('service unavailable')));
    renderView(<VirtualModelRunners />);
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to fetch data: service unavailable'));
    await waitForLoaded();
    expect(screen.getByRole('heading', { name: 'Virtual Model Runners' })).toBeTruthy();
    expect(screen.getByText('No data available')).toBeTruthy();
  });

  it('validates and then creates a VMR with the entered values', async () => {
    ctx.app.api.createVirtualModelRunner.mockImplementation(() => Promise.resolve({ Id: 'vmr_new' }));
    renderView(<VirtualModelRunners />);
    await screen.findByText('Chat VMR');
    fireEvent.click(screen.getByRole('button', { name: 'Create VMR' }));
    const form = formDialog();
    await within(form).findByRole('option', { name: 'Alpha Corp (ten_alpha)' });
    fireEvent.change(within(form).getByLabelText('Tenant'), { target: { value: 'ten_alpha' } });
    typeInto(within(form).getByLabelText('Name'), 'Code VMR');
    typeInto(within(form).getByLabelText('Base Path'), '/v1.0/api/code');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createVirtualModelRunner).toHaveBeenCalledTimes(1));
    const payload = ctx.app.api.createVirtualModelRunner.mock.calls[0][0];
    expect(payload).toMatchObject({
      TenantId: 'ten_alpha',
      Name: 'Code VMR',
      Hostname: null,
      BasePath: '/v1.0/api/code/',
      ApiType: 'OpenAI',
      LoadBalancingMode: 'RoundRobin',
      ModelRunnerEndpointIds: [],
      TimeoutMs: 300000,
      Active: true,
      Labels: [],
      Tags: {}
    });
    expect(ctx.app.api.validateVirtualModelRunner).toHaveBeenCalledWith(payload, null);
    expect(ctx.onboarding.onEntityCreated).toHaveBeenCalledWith('vmr');
    await waitFor(() => expect(document.querySelector('.modal-body form')).toBeNull());
  });

  it('rejects an unroutable base path without calling validate or create', async () => {
    renderView(<VirtualModelRunners />);
    await screen.findByText('Chat VMR');
    fireEvent.click(screen.getByRole('button', { name: 'Create VMR' }));
    const form = formDialog();
    typeInto(within(form).getByLabelText('Name'), 'Bad VMR');
    typeInto(within(form).getByLabelText('Base Path'), '/custom/path/');
    expect(within(form).getByText('Base path must start with /v1.0/api/.')).toBeTruthy();
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Base path must start with /v1.0/api/.'));
    expect(ctx.app.api.validateVirtualModelRunner).not.toHaveBeenCalled();
    expect(ctx.app.api.createVirtualModelRunner).not.toHaveBeenCalled();
  });

  it('asks for confirmation before deleting and then calls deleteVirtualModelRunner with the tenant', async () => {
    ctx.app.api.deleteVirtualModelRunner.mockImplementation(() => Promise.resolve());
    renderView(<VirtualModelRunners />);
    await screen.findByText('Embeddings VMR');
    openRowAction('Embeddings VMR', 'Delete');
    const dialog = deleteDialog();
    expect(within(dialog).getByText('Are you sure you want to delete this virtual model runner?')).toBeTruthy();
    expect(ctx.app.api.deleteVirtualModelRunner).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(ctx.app.api.deleteVirtualModelRunner).toHaveBeenCalledWith('vmr_embed', 'ten_alpha'));
    await waitFor(() => expect(document.querySelector('.delete-confirm-modal')).toBeNull());
  });
});
