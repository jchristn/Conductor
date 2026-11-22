import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor, within } from '@testing-library/react';
import VirtualModelRunners, { getDefaultExplainRequest } from '../src/views/VirtualModelRunners';
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

  it('offers only client-facing types and sends AllowDecisions', async () => {
    ctx.app.api.createVirtualModelRunner.mockImplementation(() => Promise.resolve({ Id: 'vmr_new' }));
    renderView(<VirtualModelRunners />);
    await screen.findByText('Chat VMR');
    fireEvent.click(screen.getByRole('button', { name: 'Create VMR' }));
    const form = formDialog();
    const typeSelect = within(form).getByLabelText('API Type');
    expect(Array.from(typeSelect.options).map((option) => option.value)).toEqual(['Ollama', 'OpenAI', 'Gemini', 'vLLM', 'Anthropic', 'TypeSafe']);
    const decisions = within(form).getByLabelText('Allow Decisions');
    expect(decisions.checked).toBe(true);
    fireEvent.click(decisions);
    typeInto(within(form).getByLabelText('Name'), 'Jev');
    typeInto(within(form).getByLabelText('Base Path'), '/v1.0/api/jev');
    fireEvent.change(typeSelect, { target: { value: 'TypeSafe' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createVirtualModelRunner).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.createVirtualModelRunner.mock.calls[0][0]).toMatchObject({ ApiType: 'TypeSafe', AllowDecisions: false });
  });

  it('filters the endpoint picker by the compatibility table', async () => {
    ctx.app.api.listModelRunnerEndpoints.mockImplementation(() => page([
      { Id: 'mre_ollama', TenantId: 'ten_alpha', Name: 'local-ollama', Hostname: '10.0.0.1', Port: 11434, ApiType: 'Ollama', Active: true },
      { Id: 'mre_cloud', TenantId: 'ten_alpha', Name: 'cloud-ollama', Hostname: 'ollama.com', Port: 443, ApiType: 'OllamaCloud', Active: true },
      { Id: 'mre_lmstudio', TenantId: 'ten_alpha', Name: 'lm-studio', Hostname: '10.0.0.2', Port: 1234, ApiType: 'OpenAICompatible', Active: true },
      { Id: 'mre_claude', TenantId: 'ten_alpha', Name: 'claude-upstream', Hostname: 'api.anthropic.com', Port: 443, ApiType: 'Anthropic', Active: true },
      { Id: 'mre_ollaya', TenantId: 'ten_alpha', Name: 'local-ollaya', Hostname: '127.0.0.1', Port: 8080, ApiType: 'Ollaya', Active: true }
    ]));
    renderView(<VirtualModelRunners />);
    await screen.findByText('Chat VMR');
    fireEvent.click(screen.getByRole('button', { name: 'Create VMR' }));
    const form = formDialog();
    await within(form).findByRole('option', { name: 'Alpha Corp (ten_alpha)' });
    fireEvent.change(within(form).getByLabelText('Tenant'), { target: { value: 'ten_alpha' } });
    const typeSelect = within(form).getByLabelText('API Type');

    // OpenAI (default) accepts OpenAI, OpenAICompatible and vLLM
    expect(within(form).getByText('lm-studio')).toBeTruthy();
    expect(within(form).queryByText('local-ollama')).toBeNull();
    expect(within(form).queryByText('claude-upstream')).toBeNull();

    fireEvent.change(typeSelect, { target: { value: 'Ollama' } });
    expect(within(form).getByText('local-ollama')).toBeTruthy();
    expect(within(form).getByText('cloud-ollama')).toBeTruthy();
    expect(within(form).queryByText('lm-studio')).toBeNull();

    fireEvent.change(typeSelect, { target: { value: 'TypeSafe' } });
    expect(within(form).getByText('local-ollaya')).toBeTruthy();
    expect(within(form).queryByText('local-ollama')).toBeNull();
    expect(within(form).getByText(/hidden because TypeSafe VMRs accept only TypeSafe, Ollaya endpoints/)).toBeTruthy();

    fireEvent.change(typeSelect, { target: { value: 'Anthropic' } });
    fireEvent.click(within(form).getByText('claude-upstream'));
    fireEvent.change(typeSelect, { target: { value: 'Gemini' } });
    // A selected endpoint stays visible with a warning rather than silently disappearing
    expect(within(form).getByText('claude-upstream')).toBeTruthy();
    expect(within(form).getByText('Not accepted by Gemini')).toBeTruthy();
  });

  it('shows the Connect a client panel for Anthropic VMRs', async () => {
    ctx.app.api.listVirtualModelRunners.mockImplementation(() => page([
      ...vmrs,
      { Id: 'vmr_claude', TenantId: 'ten_alpha', Name: 'Claude VMR', BasePath: '/v1.0/api/claude/', ApiType: 'Anthropic', LoadBalancingMode: 'RoundRobin', ModelRunnerEndpointIds: [], Active: true }
    ]));
    renderView(<VirtualModelRunners />);
    await screen.findByText('Claude VMR');
    expect(within(rowFor('Claude VMR')).getByText('Anthropic')).toBeTruthy();
    fireEvent.click(rowFor('Chat VMR').querySelector('.action-menu-trigger'));
    expect(within(document.querySelector('.action-menu-dropdown')).queryByRole('button', { name: 'Connect a Client' })).toBeNull();
    fireEvent.click(rowFor('Chat VMR').querySelector('.action-menu-trigger'));
    await waitFor(() => expect(document.querySelector('.action-menu-dropdown')).toBeNull());

    openRowAction('Claude VMR', 'Connect a Client');
    const panel = await screen.findByRole('region', { name: 'Connect a client' });
    expect(within(panel).getByText('ANTHROPIC_BASE_URL')).toBeTruthy();
    expect(within(panel).getByText('http://conductor.test/v1.0/api/claude')).toBeTruthy();
    expect(within(panel).getByText('ANTHROPIC_AUTH_TOKEN')).toBeTruthy();
    expect(within(panel).getByText(/export ANTHROPIC_BASE_URL="http:\/\/conductor.test\/v1.0\/api\/claude"/)).toBeTruthy();
    expect(within(panel).getByRole('button', { name: 'Copy ANTHROPIC_BASE_URL' })).toBeTruthy();
    expect(within(panel).getByRole('button', { name: 'Copy environment variables' })).toBeTruthy();
    expect(panel.textContent).toMatch(/x-api-key/);
    expect(panel.textContent).toMatch(/Conductor credential/);
  });

  it('uses Anthropic and TypeSafe request shapes for explain routing defaults', () => {
    const anthropic = getDefaultExplainRequest('Anthropic');
    expect(anthropic.RelativePath).toBe('/v1/messages');
    expect(JSON.parse(anthropic.Body).max_tokens).toBe(1024);
    const typeSafe = getDefaultExplainRequest('TypeSafe');
    expect(typeSafe.RelativePath).toBe('/v1/systemone');
    expect(JSON.parse(typeSafe.Body).questions.is_greeting.type).toBe('noul');
  });
});
