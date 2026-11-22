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
  { Id: 'mre_vllm', TenantId: 'ten_alpha', Name: 'vllm-cluster', Hostname: 'vllm.internal', Port: 8443, ApiType: 'vLLM', UseSsl: true, Active: true },
  { Id: 'mre_claude', TenantId: 'ten_alpha', Name: 'claude-upstream', Hostname: 'api.anthropic.com', Port: 443, ApiType: 'Anthropic', UseSsl: true, Active: true, ApiKey: '****abcd', AnthropicVersion: '2023-06-01', HealthCheckEnabled: true, HealthCheckMethod: 'GET', HealthCheckUrl: '/v1/models?limit=1' },
  { Id: 'mre_cloud', TenantId: 'ten_alpha', Name: 'ollama-cloud', Hostname: 'ollama.com', Port: 443, ApiType: 'OllamaCloud', UseSsl: true, Active: true, ApiKey: '****wxyz' }
];

function openCreateForm() {
  fireEvent.click(screen.getByRole('button', { name: 'Create Endpoint' }));
  return formDialog();
}

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

  it('applies the registry defaults when a type is selected and lists all nine types', async () => {
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('gpu-box-1');
    const form = openCreateForm();
    const typeSelect = within(form).getByLabelText('API Type');
    expect(Array.from(typeSelect.options).map((option) => option.value)).toEqual([
      'Ollama', 'OpenAI', 'Gemini', 'vLLM', 'Anthropic', 'TypeSafe', 'Ollaya', 'OllamaCloud', 'OpenAICompatible'
    ]);
    fireEvent.change(typeSelect, { target: { value: 'Anthropic' } });
    expect(within(form).getByLabelText('Hostname').value).toBe('api.anthropic.com');
    expect(within(form).getByLabelText('Port').value).toBe('443');
    expect(within(form).getByLabelText('Health Check URL').value).toBe('/v1/models?limit=1');
    expect(within(form).getByLabelText('Interval (ms)').value).toBe('60000');
    expect(within(form).getByLabelText('Health checks enabled').checked).toBe(true);
    expect(within(form).getByLabelText('Use Authentication for Health Checks').checked).toBe(true);

    fireEvent.change(typeSelect, { target: { value: 'OllamaCloud' } });
    expect(within(form).getByLabelText('Hostname').value).toBe('ollama.com');
    expect(within(form).getByLabelText('Health Check URL').value).toBe('/api/tags');
  });

  it('does not infer anything from the hostname', async () => {
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('gpu-box-1');
    const form = openCreateForm();
    typeInto(within(form).getByLabelText('Hostname'), 'api.openai.com');
    expect(within(form).getByLabelText('API Type').value).toBe('Ollama');
    expect(within(form).getByLabelText('Health Check URL').value).toBe('/');
    expect(within(form).getByLabelText('Use Authentication for Health Checks').checked).toBe(false);
    typeInto(within(form).getByLabelText('Hostname'), 'https://generativelanguage.googleapis.com');
    expect(within(form).getByLabelText('Hostname').value).toBe('generativelanguage.googleapis.com');
    expect(within(form).getByLabelText('Port').value).toBe('443');
    expect(within(form).getByLabelText('API Type').value).toBe('Ollama');
    expect(within(form).getByLabelText('Health Check URL').value).toBe('/');
  });

  it('shows the Anthropic version field only for Anthropic and validates its format', async () => {
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('gpu-box-1');
    const form = openCreateForm();
    expect(within(form).queryByLabelText('Anthropic version')).toBeNull();
    fireEvent.change(within(form).getByLabelText('API Type'), { target: { value: 'Anthropic' } });
    const version = within(form).getByLabelText('Anthropic version');
    expect(within(form).getByText(/client's own anthropic-version header always wins/)).toBeTruthy();
    expect(within(form).getByText(/server default \(2023-06-01\)/)).toBeTruthy();
    typeInto(within(form).getByLabelText('Name'), 'claude');
    typeInto(version, '06/01/2023');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(within(form).getByRole('alert').textContent).toMatch(/yyyy-MM-dd/));
    expect(ctx.app.api.validateModelRunnerEndpoint).not.toHaveBeenCalled();

    ctx.app.api.createModelRunnerEndpoint.mockImplementation(() => Promise.resolve({ Id: 'mre_new' }));
    typeInto(version, '2023-06-01');
    typeInto(within(form).getByLabelText('API Key (required)'), 'sk-ant-real');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createModelRunnerEndpoint).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.createModelRunnerEndpoint.mock.calls[0][0]).toMatchObject({
      ApiType: 'Anthropic', AnthropicVersion: '2023-06-01', ApiKey: 'sk-ant-real', HealthCheckEnabled: true, HealthCheckMethod: 'GET', HealthCheckRequestBody: null
    });

    fireEvent.click(screen.getByRole('button', { name: 'Create Endpoint' }));
    const second = formDialog();
    fireEvent.change(within(second).getByLabelText('API Type'), { target: { value: 'Anthropic' } });
    fireEvent.change(within(second).getByLabelText('API Type'), { target: { value: 'OpenAI' } });
    expect(within(second).queryByLabelText('Anthropic version')).toBeNull();
  });

  it('turns TypeSafe health checks off by default and explains the billing in the tooltip', async () => {
    ctx.app.api.createModelRunnerEndpoint.mockImplementation(() => Promise.resolve({ Id: 'mre_ts' }));
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('gpu-box-1');
    const form = openCreateForm();
    fireEvent.change(within(form).getByLabelText('API Type'), { target: { value: 'TypeSafe' } });
    const toggle = within(form).getByLabelText('Health checks enabled');
    expect(toggle.checked).toBe(false);
    const tooltip = toggle.closest('label').getAttribute('title');
    expect(tooltip).toMatch(/real one-question decision to the hosted TypeSafe API, which is billed/);
    expect(tooltip).toMatch(/off by default/);
    expect(tooltip).toMatch(/every 60 seconds by default/);
    expect(tooltip).toMatch(/lower the interval/);
    expect(tooltip).toMatch(/treats the endpoint as healthy/);
    expect(within(form).getByLabelText('Method').value).toBe('POST');
    const body = within(form).getByLabelText('Health Check Request Body');
    expect(JSON.parse(body.value).questions.reachable.type).toBe('noul');
    expect(within(form).getByLabelText('Interval (ms)').value).toBe('60000');

    typeInto(within(form).getByLabelText('Name'), 'typesafe');
    typeInto(within(form).getByLabelText('API Key (required)'), 'ts-key');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createModelRunnerEndpoint).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.createModelRunnerEndpoint.mock.calls[0][0]).toMatchObject({
      ApiType: 'TypeSafe', Hostname: 'api.typesafe.ai', HealthCheckEnabled: false, HealthCheckMethod: 'POST', HealthCheckUrl: '/v1/systemone', AnthropicVersion: null
    });
    expect(JSON.parse(ctx.app.api.createModelRunnerEndpoint.mock.calls[0][0].HealthCheckRequestBody).model).toBe('jev-latest');
  });

  it('requires a valid JSON body for POST health checks', async () => {
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('gpu-box-1');
    const form = openCreateForm();
    typeInto(within(form).getByLabelText('Name'), 'post-check');
    typeInto(within(form).getByLabelText('Hostname'), '10.0.0.20');
    fireEvent.change(within(form).getByLabelText('Method'), { target: { value: 'POST' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(within(form).getByRole('alert').textContent).toMatch(/POST health check needs a request body/));
    typeInto(within(form).getByLabelText('Health Check Request Body'), '{not json');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(within(form).getByRole('alert').textContent).toMatch(/not valid JSON/));
    expect(ctx.app.api.validateModelRunnerEndpoint).not.toHaveBeenCalled();
  });

  it('masks the stored key, reveals it through the API, and keeps it when unchanged', async () => {
    ctx.app.api.revealModelRunnerEndpointApiKey = vi.fn(() => Promise.resolve({ ApiKey: 'sk-ant-full-abcd' }));
    ctx.app.api.updateModelRunnerEndpoint.mockImplementation(() => Promise.resolve({}));
    renderView(<ModelRunnerEndpoints />);
    fireEvent.click(await screen.findByText('claude-upstream'));
    const form = formDialog();
    const keyInput = within(form).getByLabelText('API Key (required)');
    expect(keyInput.type).toBe('password');
    expect(keyInput.value).toBe('****abcd');

    fireEvent.click(within(form).getByRole('button', { name: 'Update' }));
    await waitFor(() => expect(ctx.app.api.updateModelRunnerEndpoint).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.updateModelRunnerEndpoint.mock.calls[0][1].ApiKey).toBe('****abcd');
    expect(ctx.app.api.revealModelRunnerEndpointApiKey).not.toHaveBeenCalled();

    fireEvent.click(await screen.findByText('claude-upstream'));
    const again = formDialog();
    fireEvent.click(within(again).getByRole('button', { name: 'Reveal API key' }));
    await waitFor(() => expect(ctx.app.api.revealModelRunnerEndpointApiKey).toHaveBeenCalledWith('mre_claude', 'ten_alpha'));
    await waitFor(() => expect(within(again).getByLabelText('API Key (required)').value).toBe('sk-ant-full-abcd'));
    expect(within(again).getByLabelText('API Key (required)').type).toBe('text');
  });

  it('omits a blank key and sends an empty string only after Clear', async () => {
    ctx.app.api.updateModelRunnerEndpoint.mockImplementation(() => Promise.resolve({}));
    renderView(<ModelRunnerEndpoints />);
    fireEvent.click(await screen.findByText('claude-upstream'));
    let form = formDialog();
    typeInto(within(form).getByLabelText('API Key (required)'), '');
    fireEvent.click(within(form).getByRole('button', { name: 'Update' }));
    await waitFor(() => expect(ctx.app.api.updateModelRunnerEndpoint).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.updateModelRunnerEndpoint.mock.calls[0][1]).not.toHaveProperty('ApiKey');

    fireEvent.click(await screen.findByText('claude-upstream'));
    form = formDialog();
    fireEvent.click(within(form).getByRole('button', { name: 'Clear' }));
    expect(within(form).getByText(/stored key will be removed when you save/)).toBeTruthy();
    fireEvent.click(within(form).getByRole('button', { name: 'Update' }));
    await waitFor(() => expect(ctx.app.api.updateModelRunnerEndpoint).toHaveBeenCalledTimes(2));
    expect(ctx.app.api.updateModelRunnerEndpoint.mock.calls[1][1].ApiKey).toBe('');
  });

  it('fetches the real key through the reveal route when duplicating', async () => {
    ctx.app.api.revealModelRunnerEndpointApiKey = vi.fn(() => Promise.resolve({ ApiKey: 'sk-ant-full-abcd' }));
    ctx.app.api.createModelRunnerEndpoint.mockImplementation(() => Promise.resolve({ Id: 'mre_copy' }));
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('claude-upstream');
    openRowAction('claude-upstream', 'Duplicate');
    await waitFor(() => expect(ctx.app.api.revealModelRunnerEndpointApiKey).toHaveBeenCalledWith('mre_claude', 'ten_alpha'));
    const form = formDialog();
    await waitFor(() => expect(within(form).getByLabelText('Name').value).toBe('claude-upstream (Copy)'));
    expect(within(form).getByLabelText('API Key (required)').value).toBe('sk-ant-full-abcd');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createModelRunnerEndpoint).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.createModelRunnerEndpoint.mock.calls[0][0].ApiKey).toBe('sk-ant-full-abcd');
  });

  it('offers Manage Models only for Ollama endpoints, not Ollama Cloud', async () => {
    renderView(<ModelRunnerEndpoints />);
    await screen.findByText('ollama-cloud');
    fireEvent.click(rowFor('gpu-box-1').querySelector('.action-menu-trigger'));
    expect(within(document.querySelector('.action-menu-dropdown')).queryByRole('button', { name: 'Manage Models' })).toBeTruthy();
    fireEvent.click(rowFor('gpu-box-1').querySelector('.action-menu-trigger'));
    await waitFor(() => expect(document.querySelector('.action-menu-dropdown')).toBeNull());
    fireEvent.click(rowFor('ollama-cloud').querySelector('.action-menu-trigger'));
    expect(within(document.querySelector('.action-menu-dropdown')).queryByRole('button', { name: 'Manage Models' })).toBeNull();
    expect(within(rowFor('ollama-cloud')).getByText('Ollama Cloud')).toBeTruthy();
  });
});
