import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, fireEvent, waitFor, within } from '@testing-library/react';
import ModelDefinitions from '../src/views/ModelDefinitions';
import { createApp, createOnboarding, page, renderView, waitForLoaded, openRowAction, deleteDialog, formDialog, typeInto } from './crudViewHarness';

const ctx = vi.hoisted(() => ({ app: null, onboarding: null }));
vi.mock('../src/context/AppContext', () => ({ useApp: () => ctx.app }));
vi.mock('../src/context/OnboardingContext', () => ({ useOnboarding: () => ctx.onboarding }));

const tenants = [{ Id: 'ten_alpha', Name: 'Alpha Corp', Active: true }];
const definitions = [
  { Id: 'md_llama', TenantId: 'ten_alpha', Name: 'llama3.3:70b', ParameterSize: '70B', QuantizationLevel: 'Q4_K_M', SupportsCompletions: true, Active: true },
  { Id: 'md_embed', TenantId: 'ten_alpha', Name: 'nomic-embed-text', ParameterSize: '137M', QuantizationLevel: 'F16', SupportsEmbeddings: true, SupportsCompletions: false, Active: true }
];

describe('Model Definitions view', () => {
  beforeEach(() => {
    ctx.app = createApp({ listModelDefinitions: () => page(definitions), listTenants: () => page(tenants) });
    ctx.onboarding = createOnboarding();
  });

  it('renders the model definition list from the API', async () => {
    renderView(<ModelDefinitions />);
    expect(await screen.findByText('llama3.3:70b')).toBeTruthy();
    expect(screen.getByText('70B')).toBeTruthy();
    expect(screen.getByText('Q4_K_M')).toBeTruthy();
    expect(screen.getByText('nomic-embed-text')).toBeTruthy();
    expect(ctx.app.api.listModelDefinitions).toHaveBeenCalledWith({ maxResults: 1000 });
  });

  it('shows the empty state when there are no model definitions', async () => {
    ctx.app.api.listModelDefinitions.mockImplementation(() => page([]));
    renderView(<ModelDefinitions />);
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(ctx.app.setError).not.toHaveBeenCalled();
  });

  it('surfaces a list failure through setError without crashing', async () => {
    ctx.app.api.listModelDefinitions.mockImplementation(() => Promise.reject(new Error('db locked')));
    renderView(<ModelDefinitions />);
    await waitFor(() => expect(ctx.app.setError).toHaveBeenCalledWith('Failed to fetch model definitions: db locked'));
    await waitForLoaded();
    expect(screen.getByText('No data available')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Create Definition' })).toBeTruthy();
  });

  it('creates a model definition with the entered values', async () => {
    ctx.app.api.createModelDefinition.mockImplementation(() => Promise.resolve({ Id: 'md_new' }));
    renderView(<ModelDefinitions />);
    await screen.findByText('llama3.3:70b');
    fireEvent.click(screen.getByRole('button', { name: 'Create Definition' }));
    const form = formDialog();
    await within(form).findByRole('option', { name: 'Alpha Corp (ten_alpha)' });
    fireEvent.change(within(form).getByLabelText('Tenant'), { target: { value: 'ten_alpha' } });
    typeInto(within(form).getByLabelText('Name'), 'qwen3:32b');
    typeInto(within(form).getByLabelText('Source URL (optional)'), 'https://ollama.com/library/qwen3');
    typeInto(within(form).getByLabelText('Family'), 'qwen3');
    typeInto(within(form).getByLabelText('Parameter Size'), '32B');
    typeInto(within(form).getByLabelText('Quantization'), 'Q8_0');
    fireEvent.click(within(form).getByLabelText('Supports Embeddings'));
    fireEvent.click(within(form).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(ctx.app.api.createModelDefinition).toHaveBeenCalledTimes(1));
    expect(ctx.app.api.createModelDefinition).toHaveBeenCalledWith({
      TenantId: 'ten_alpha',
      Name: 'qwen3:32b',
      SourceUrl: 'https://ollama.com/library/qwen3',
      Family: 'qwen3',
      ParameterSize: '32B',
      QuantizationLevel: 'Q8_0',
      SupportsEmbeddings: true,
      SupportsCompletions: true,
      Active: true,
      Labels: [],
      Tags: {}
    });
    expect(ctx.onboarding.onEntityCreated).toHaveBeenCalledWith('definition');
    await waitFor(() => expect(document.querySelector('.modal-body form')).toBeNull());
  });

  it('asks for confirmation before deleting and then calls deleteModelDefinition with the tenant', async () => {
    ctx.app.api.deleteModelDefinition.mockImplementation(() => Promise.resolve());
    renderView(<ModelDefinitions />);
    await screen.findByText('nomic-embed-text');
    openRowAction('nomic-embed-text', 'Delete');
    const dialog = deleteDialog();
    expect(within(dialog).getByText('Are you sure you want to delete this model definition?')).toBeTruthy();
    expect(ctx.app.api.deleteModelDefinition).not.toHaveBeenCalled();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(ctx.app.api.deleteModelDefinition).toHaveBeenCalledWith('md_embed', 'ten_alpha'));
    await waitFor(() => expect(document.querySelector('.delete-confirm-modal')).toBeNull());
  });
});
