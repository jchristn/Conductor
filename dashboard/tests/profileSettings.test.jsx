import React from 'react';
import { it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import QueueProfileSettings from '../src/components/QueueProfileSettings';
const topology = { DefaultClass: 'default', Nodes: [{ Name: 'first' }, { Name: 'last' }], IngressDefaultNode: 'first', TailNode: 'last' };
it('visually configures class-based ingress routing', () => {
 const save = vi.fn(); render(<QueueProfileSettings topology={topology} onSave={save} onClose={() => {}} />);
 fireEvent.change(screen.getByLabelText('Ingress mode'), { target: { value: 'Router' } });
 fireEvent.click(screen.getByText('Add ingress route'));
 fireEvent.change(screen.getByLabelText('Traffic class'), { target: { value: 'gold' } });
 fireEvent.click(screen.getByText('Apply settings'));
 expect(save.mock.calls[0][0].IngressRoutes).toEqual([{ ClassName: 'gold', Node: 'first', Ordinal: 0 }]);
});
it('rejects incomplete ingress routes without applying', () => {
 const save = vi.fn(); render(<QueueProfileSettings topology={{ ...topology, IngressMode: 'Router', IngressRoutes: [{ Node: 'missing' }] }} onSave={save} onClose={() => {}} />);
 fireEvent.click(screen.getByText('Apply settings')); expect(save).not.toHaveBeenCalled(); expect(screen.getByRole('alert').textContent).toContain('Every ingress route');
});
