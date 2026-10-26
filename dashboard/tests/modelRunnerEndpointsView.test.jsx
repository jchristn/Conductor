import React from 'react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { screen, fireEvent, waitFor, within, act } from '@testing-library/react';
import ModelRunnerEndpoints from '../src/views/ModelRunnerEndpoints';
import { createApp, createOnboarding, page, renderView, waitForLoaded, rowFor, openRowAction, deleteDialog, formDialog, typeInto } from './crudViewHarness';

const ctx = vi.hoisted(() => ({ app: null, onboarding: null }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ctx.app }));
vi.mock('../src/context/OnboardingContext', () => ({ useOnboarding: () => ctx.onboarding }));

const tenants = [{ Id: 'ten_alpha', Name: 'Alpha Corp', Active: true }];
const endpoints = [
  { Id: 'mre_gpu1', TenantId: 'ten_alpha', Name: 'gpu-box-1', Hostname: '10.0.0.11', Port: 11434, ApiType: 'Ollama', UseSsl: false, Active: true },
  { Id: 'mre_vllm', TenantId: 'ten_alpha', Name: 'vllm-cluster', Hostname: 'vllm.internal', Port: 8443, ApiType: 'vLLM', UseSsl: true, Active: true }
];

describe('Model Runner Endpoints view', () => {
  beforeEach(() => {
    // The view polls health every 15 s; fake only the interval timers so nothing runs on a real clock
    // while promises and waitFor keep working.
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    ctx.app = createApp({
      listModelRunnerEndpoints: () => page(endpoints),
      listTenants: () => page(tenants),
      getModelRunnerEndpointsHealth: () => Promise.resolve([]),
      validateModelRunnerEndpoint: () => Promise.resolve({ IsValid: true, Errors: [], Warnings: [] })
    });
    ctx.onboarding = createOnboarding();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders the endpoint list from the API', async () => {
    renderView(<ModelRunnerEndpoints />);
    expect(await screen.findByText('gpu-box-1')).toBeTruthy();
    expect(within(rowFor('gpu-box-1')).getByText('http://10.0.0.11:11434')).toBeTruthy();
    expect(within(rowFor('vllm-cluster')).getByText('https://vllm.internal:8443')).toBeTruthy();
    expect(within(rowFor('vllm-cluster')).getByText('vLLM')).toBeTruthy();
    expect(ctx.app.api.listModelRunnerEndpoints).toHaveBeenCalledWith({ maxResults: 1000 });
  });

  it('polls endpoint health on the fake interval only', async () => {
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('gpu-box-1');
    expect(ctx.app.api.getModelRunnerEndpointsHealth).toHaveBeenCalledTimes(1);
    await act(async () => { vi.advanceTimersByTime(15000); });
    expect(ctx.app.api.getModelRunnerEndpointsHealth).toHaveBeenCalledTimes(2);
  });

  it('shows the empty state when there are no endpoints', async () => {
    ctx.app.api.listModelRunnerEndpoints.mockImplementation(() => page([]));
    renderView(<ModelRunnerEndpoints />);
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(ctx.app.setError).not.toHaveBeenCalled();
  });

  it('surfaces a list failure through setError without crashing', async () => {
    ctx.app.api.listModelRunnerEndpoints.mockImplementation(() => Promise.reject(new Error('connection refused')));
    ctx.app.api.getModelRunnerEndpointsHealth.mockImplementation(() => Promise.reject(new Error('connection refused')));
    renderView(<ModelRunnerEndpoints />);
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to fetch endpoints: connection refused'));
    await waitForLoaded();
    expect(screen.getByRole('heading', { name: 'Model Runner Endpoints' })).toBeTruthy();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(ctx.app.setError).toHaveBeenCalledTimes(1);
  });

  it('validates and then creates an endpoint with the entered values', async () => {
    ctx.app.api.createModelRunnerEndpoint.mockImplementation(() => Promise.resolve({ Id: 'mre_new' }));
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('gpu-box-1');
    fireEvent.click(screen.getByRole('button', { name: 'Create Endpoint' }));
    const form = formDialog();
    await within(form).findByRole('option', { name: 'Alpha Corp (ten_alpha)' });
    fireEvent.change(within(form).getByLabelText('Tenant'), { target: { value: 'ten_alpha' } });
    typeInto(within(form).getByLabelText('Name'), 'gpu-box-3');
    typeInto(within(form).getByLabelText('Hostname'), '10.0.0.13');
    typeInto(within(form).getByLabelText('Port'), '11500');
    typeInto(within(form).getByLabelText('Max Parallel Requests'), '8');
    typeInto(within(form).getByLabelText('Weight (1-1000)'), '25');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createModelRunnerEndpoint).toHaveBeenCalledTimes(1));
    const payload = ctx.app.api.createModelRunnerEndpoint.mock.calls[0][0];
    expect(payload).toMatchObject({
      TenantId: 'ten_alpha',
      Name: 'gpu-box-3',
      Hostname: '10.0.0.13',
      Port: 11500,
      ApiType: 'Ollama',
      UseSsl: false,
      MaxParallelRequests: 8,
      Weight: 25,
      Active: true,
      Labels: [],
      Tags: {}
    });
    expect(ctx.app.api.validateModelRunnerEndpoint).toHaveBeenCalledWith(payload, null);
    expect(ctx.onboarding.onEntityCreated).toHaveBeenCalledWith('endpoint');
    await waitFor(() => expect(document.querySelector('.modal-body form')).toBeNull());
  });

  it('surfaces a create failure through setError and keeps the form open', async () => {
    ctx.app.api.createModelRunnerEndpoint.mockImplementation(() => Promise.reject(new Error('name taken')));
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('gpu-box-1');
    fireEvent.click(screen.getByRole('button', { name: 'Create Endpoint' }));
    const form = formDialog();
    typeInto(within(form).getByLabelText('Name'), 'gpu-box-1');
    typeInto(within(form).getByLabelText('Hostname'), '10.0.0.11');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to save endpoint: name taken'));
    expect(formDialog()).toBeTruthy();
  });

  it('asks for confirmation before deleting and then calls deleteModelRunnerEndpoint with the tenant', async () => {
    ctx.app.api.deleteModelRunnerEndpoint.mockImplementation(() => Promise.resolve());
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('vllm-cluster');
    openRowAction('vllm-cluster', 'Delete');
    const dialog = deleteDialog();
    expect(within(dialog).getByText('Are you sure you want to delete this endpoint?')).toBeTruthy();
    expect(ctx.app.api.deleteModelRunnerEndpoint).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(ctx.app.api.deleteModelRunnerEndpoint).toHaveBeenCalledWith('mre_vllm', 'ten_alpha'));
    await waitFor(() => expect(document.querySelector('.delete-confirm-modal')).toBeNull());
  });
});
