import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor, within } from '@testing-library/react';
import ModelConfigurations from '../src/views/ModelConfigurations';
import { createApp, createOnboarding, page, renderView, waitForLoaded, openRowAction, deleteDialog, formDialog, typeInto } from './crudViewHarness';

const ctx = vi.hoisted(() => ({ app: null, onboarding: null }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ctx.app }));
vi.mock('../src/context/OnboardingContext', () => ({ useOnboarding: () => ctx.onboarding }));

const tenants = [{ Id: 'ten_alpha', Name: 'Alpha Corp', Active: true }];
const configurations = [
  { Id: 'mc_creative', TenantId: 'ten_alpha', Name: 'Creative writing', Model: 'llama3.3:70b', Temperature: 1.2, MaxTokens: 4096, ContextWindowSize: 8192, Active: true },
  { Id: 'mc_strict', TenantId: 'ten_alpha', Name: 'Deterministic', Model: null, Temperature: 0, MaxTokens: 512, ContextWindowSize: 2048, Active: true }
];

describe('Model Configurations view', () => {
  beforeEach(() => {
    ctx.app = createApp({ listModelConfigurations: () => page(configurations), listTenants: () => page(tenants) });
    ctx.onboarding = createOnboarding();
  });

  it('renders the model configuration list from the API', async () => {
    renderView(<ModelConfigurations />);
    expect(await screen.findByText('Creative writing')).toBeTruthy();
    expect(screen.getByText('llama3.3:70b')).toBeTruthy();
    expect(screen.getByText('Deterministic')).toBeTruthy();
    expect(ctx.app.api.listModelConfigurations).toHaveBeenCalledWith({ maxResults: 1000 });
  });

  it('shows the empty state when there are no model configurations', async () => {
    ctx.app.api.listModelConfigurations.mockImplementation(() => page([]));
    renderView(<ModelConfigurations />);
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(ctx.app.setError).not.toHaveBeenCalled();
  });

  it('surfaces a list failure through setError without crashing', async () => {
    ctx.app.api.listModelConfigurations.mockImplementation(() => Promise.reject(new Error('unauthorized')));
    renderView(<ModelConfigurations />);
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to fetch model configurations: unauthorized'));
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Create Configuration' })).toBeTruthy();
  });

  it('creates a model configuration with the entered values', async () => {
    ctx.app.api.createModelConfiguration.mockImplementation(() => Promise.resolve({ Id: 'mc_new' }));
    renderView(<ModelConfigurations />);
    await screen.findByText('Creative writing');
    fireEvent.click(screen.getByRole('button', { name: 'Create Configuration' }));
    const form = formDialog();
    await within(form).findByRole('option', { name: 'Alpha Corp (ten_alpha)' });
    fireEvent.change(within(form).getByLabelText('Tenant'), { target: { value: 'ten_alpha' } });
    typeInto(within(form).getByLabelText('Name'), 'Long context');
    typeInto(within(form).getByLabelText('Model'), 'qwen3:32b');
    typeInto(within(form).getByLabelText('Context Window Size'), '32768');
    typeInto(within(form).getByLabelText('Max Tokens'), '1024');
    typeInto(within(form).getByLabelText('Temperature'), '0.2');
    typeInto(within(form).getByLabelText('Pinned Completions Properties (JSON)'), '{"seed":42}');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createModelConfiguration).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.createModelConfiguration).toHaveBeenCalledWith({
      TenantId: 'ten_alpha',
      Name: 'Long context',
      Model: 'qwen3:32b',
      ContextWindowSize: 32768,
      Temperature: 0.2,
      TopP: 0.9,
      TopK: 40,
      RepeatPenalty: 1.1,
      MaxTokens: 1024,
      PinnedEmbeddingsProperties: {},
      PinnedCompletionsProperties: { seed: 42 },
      Active: true,
      Labels: [],
      Tags: {}
    });
    expect(ctx.onboarding.onEntityCreated).toHaveBeenCalledWith('configuration');
    await waitFor(() => expect(document.querySelector('.modal-body form')).toBeNull());
  });

  it('rejects invalid pinned JSON without calling the create API', async () => {
    renderView(<ModelConfigurations />);
    await screen.findByText('Creative writing');
    fireEvent.click(screen.getByRole('button', { name: 'Create Configuration' }));
    const form = formDialog();
    typeInto(within(form).getByLabelText('Name'), 'Broken');
    typeInto(within(form).getByLabelText('Pinned Embeddings Properties (JSON)'), '{not json');
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Invalid JSON in Pinned Embeddings Properties'));
    expect(ctx.app.api.createModelConfiguration).not.toHaveBeenCalled();
    expect(formDialog()).toBeTruthy();
  });

  it('asks for confirmation before deleting and then calls deleteModelConfiguration with the tenant', async () => {
    ctx.app.api.deleteModelConfiguration.mockImplementation(() => Promise.resolve());
    renderView(<ModelConfigurations />);
    await screen.findByText('Deterministic');
    openRowAction('Deterministic', 'Delete');
    const dialog = deleteDialog();
    expect(within(dialog).getByText('Are you sure you want to delete this model configuration?')).toBeTruthy();
    expect(ctx.app.api.deleteModelConfiguration).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(ctx.app.api.deleteModelConfiguration).toHaveBeenCalledWith('mc_strict', 'ten_alpha'));
    await waitFor(() => expect(document.querySelector('.delete-confirm-modal')).toBeNull());
  });
});
