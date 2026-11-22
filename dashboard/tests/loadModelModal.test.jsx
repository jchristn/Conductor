import React from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import LoadModelModal from '../src/components/LoadModelModal';

function renderModal(target, targetType = 'endpoint', extra = {}) {
  return render(
    <LoadModelModal isOpen onClose={vi.fn()} target={target} targetType={targetType} api={{}} {...extra} />
  );
}

describe('Load model modal', () => {
  it('offers the Decision probe for TypeSafe and warns that Auto is billable', () => {
    renderModal({ Id: 'mre_ts', Name: 'typesafe', ApiType: 'TypeSafe' });
    const probe = screen.getByLabelText('Probe Kind');
    expect(Array.from(probe.options).map((option) => option.value)).toContain('Decision');
    expect(screen.getByRole('note').textContent).toMatch(/hosted TypeSafe API and is billable/);
    fireEvent.change(probe, { target: { value: 'MetadataOnly' } });
    expect(screen.queryByRole('note')).toBeNull();
    fireEvent.change(probe, { target: { value: 'Decision' } });
    expect(screen.getByRole('note')).toBeTruthy();
  });

  it('offers Decision for Ollaya without a billing warning', () => {
    renderModal({ Id: 'mre_ollaya', Name: 'ollaya', ApiType: 'Ollaya' });
    const probe = screen.getByLabelText('Probe Kind');
    expect(Array.from(probe.options).map((option) => option.value)).toContain('Decision');
    fireEvent.change(probe, { target: { value: 'Decision' } });
    expect(screen.queryByRole('note')).toBeNull();
  });

  it('disables Embeddings for Anthropic with an explanation and flags ChatCompletion as billable', () => {
    renderModal({ Id: 'mre_claude', Name: 'claude', ApiType: 'Anthropic' });
    const probe = screen.getByLabelText('Probe Kind');
    const embeddings = Array.from(probe.options).find((option) => option.value === 'Embeddings');
    expect(embeddings.disabled).toBe(true);
    expect(embeddings.title).toMatch(/no embeddings API/);
    expect(screen.getByText(/Anthropic has no embeddings API/)).toBeTruthy();
    expect(Array.from(probe.options).map((option) => option.value)).not.toContain('Decision');
    expect(screen.queryByRole('note')).toBeNull();
    fireEvent.change(probe, { target: { value: 'ChatCompletion' } });
    expect(screen.getByRole('note').textContent).toMatch(/Anthropic/);
  });

  it('uses attached endpoint types for VMR billing warnings', () => {
    const endpoints = [{ Id: 'mre_cloud', Name: 'cloud', ApiType: 'OllamaCloud', Hostname: 'ollama.com', Port: 443 }];
    renderModal({ Id: 'vmr_ollama', Name: 'ollama vmr', ApiType: 'Ollama', ModelRunnerEndpointIds: ['mre_cloud'] }, 'vmr', { endpoints });
    expect(screen.getByRole('note').textContent).toMatch(/Ollama Cloud/);
  });
});
