import React from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { tokenParts, chartSeries } from '../src/utils/analyticsSeries';
import { saveQueue, validConnection } from '../src/utils/qosTopology';
import QueueNodeEditor from '../src/components/QueueNodeEditor';
import AnalyticsTrendChart from '../src/components/AnalyticsTrendChart';
import Tooltips, { inferTooltip } from '../src/components/ControlTooltipHydrator';
import { copyChartPng } from '../src/utils/chartExport';
const topology = { Nodes: [{ Name: 'a', Discipline: 'Fifo', MaxDepth: 0, Classes: [] }, { Name: 'b' }], Links: [{ FromNode: 'a', ToNode: 'b' }], IngressDefaultNode: 'a', TailNode: 'b', IngressRoutes: [{ ClassName: 'gold', Node: 'a' }] };
describe('queue topology', () => {
 it('renames every reference and preserves configuration', () => {
   const result = saveQueue(topology, 'a', { ...topology.Nodes[0], Name: 'renamed' });
   expect(result.IngressDefaultNode).toBe('renamed');
   expect(result.IngressRoutes[0].Node).toBe('renamed');
   expect(result.Links[0].FromNode).toBe('renamed');
   expect(topology.Nodes[0].Name).toBe('a');
 });
 it('initializes entry and tail when adding the first queue', () => {
   const result = saveQueue({}, undefined, { Name: 'first' });
   expect(result.IngressDefaultNode).toBe('first'); expect(result.TailNode).toBe('first');
 });
 it.each(['', '__reserved', 'b'])('rejects invalid name %s', name => expect(() => saveQueue(topology, 'a', { Name: name })).toThrow());
 it.each([-1, 1.2, NaN])('rejects invalid depth %s', depth => expect(() => saveQueue(topology, 'a', { Name: 'a', MaxDepth: depth })).toThrow());
 it('rejects cycles, self links, duplicates and missing references', () => {
   for (const [from, to] of [['b','a'],['a','a'],['a','b'],['a','missing']]) expect(validConnection(topology, from, to)).toBe(false);
   expect(validConnection({ ...topology, Links: [] }, 'a','b')).toBe(true);
 });
 it('configures queue type in a dialog and applies without submitting the parent form', () => {
   const onSave = vi.fn(); render(<QueueNodeEditor topology={topology} node={topology.Nodes[0]} onSave={onSave} onClose={() => {}} />);
   fireEvent.change(screen.getByLabelText('Queue type'), { target: { value: 'Llq' } });
   fireEvent.click(screen.getByText('Add class'));
   fireEvent.change(screen.getByLabelText('Class name'), { target: { value: 'interactive' } });
   fireEvent.click(screen.getByText('Apply queue'));
   expect(onSave.mock.calls[0][0].Nodes[0].Discipline).toBe('Llq');
   expect(onSave.mock.calls[0][0].Nodes[0].Classes[0].ClassName).toBe('interactive');
   expect(screen.getByText('Apply queue').type).toBe('button');
 });
 it('keeps invalid drafts open and cancels without mutation', () => {
   const onSave = vi.fn(), close = vi.fn(); render(<QueueNodeEditor topology={topology} node={null} onSave={onSave} onClose={close} />);
   fireEvent.click(screen.getByText('Apply queue')); expect(screen.getByRole('alert').textContent).toContain('queue name'); expect(onSave).not.toHaveBeenCalled();
   fireEvent.click(screen.getByText('Cancel')); expect(close).toHaveBeenCalled();
 });
});
describe('analytics', () => {
 it('separates cached input without double counting multimodal usage', () => {
   expect(tokenParts({ PromptTokens: 100, CompletionTokens: 40, CachedTokens: 20, TotalTokens: 150, MultimodalTokens: 50 })).toEqual([80,40,20,10]);
   expect(tokenParts({ PromptTokens: 10, CachedTokens: 30, TotalTokens: 5 })).toEqual([0,0,10,0]);
   expect(tokenParts({})).toEqual([0,0,0,0]);
 });
 it('uses distinct series and preserves missing timing gaps', () => {
   const data = [{ PromptTokens: 5, AverageTimeToFirstTokenMs: 12, AverageResponseTimeMs: 55 }, {}];
   expect(chartSeries(data,'tokens')).toHaveLength(4);
   expect(chartSeries(data,'latency').map(s => s.values)).toEqual([[12,null],[55,null]]);
   expect(chartSeries(data,'volume')).toHaveLength(3);
 });
 it('renders empty state without an invented zero chart', () => {
   render(<AnalyticsTrendChart mode="latency" />); expect(screen.getByText('No analytics in this range.').textContent).toContain('No analytics'); expect(screen.queryByRole('img')).toBeNull();
 });
 it('renders token type legend and copy control', () => {
   render(<AnalyticsTrendChart mode="tokens" data={[{ TimestampUtc: '2026-10-01T00:00:00Z', PromptTokens: 50, CompletionTokens: 20 }]} />);
   expect(screen.getByText('Cached input')).toBeTruthy(); expect(screen.getByRole('button', { name: 'Copy chart as an image' })).toBeTruthy();
 });
});
describe('tooltips', () => {
 it('explains headers and field constraints instead of repeating labels', () => {
   const { container } = render(<><table><thead><tr><th>Latency</th></tr></thead></table><label>Depth<input type="number" min="0" max="100" required /></label></>);
   expect(inferTooltip(container.querySelector('th'))).toContain('Elapsed request duration');
   expect(inferTooltip(container.querySelector('input'))).toContain('0 to 100');
 });
 it('hydrates late-mounted controls, links and table headers', async () => {
   vi.stubGlobal('requestAnimationFrame', cb => setTimeout(cb, 0));
   const { container, rerender } = render(<MemoryRouter><Tooltips /><button>Save</button></MemoryRouter>);
   await waitFor(() => expect(container.querySelector('button').title).toContain('validate'));
   rerender(<MemoryRouter><Tooltips /><a href="/test">Users</a><select aria-label="Tenant"><option>All</option></select></MemoryRouter>);
   await waitFor(() => expect(container.querySelector('a').title).toContain('Open Users'));
   expect(container.querySelector('select').title).toContain('isolation boundary');
 });
});
describe('image export', () => {
 it('reports missing chart as failure, without success or download', async () => expect(await copyChartPng(null)).toBe('failed'));
});
describe('profile-wide visual configuration', () => {
 it('does not accept a class row without a name', () => {
   expect(() => saveQueue(topology, 'a', { Name: 'a', Classes: [{ ClassName: '', Weight: 1 }] })).toThrow('Each class needs a name');
 });
 it('rejects invalid class scheduling values', () => {
   expect(() => saveQueue(topology, 'a', { Name: 'a', Classes: [{ ClassName: 'gold', Weight: 0 }] })).toThrow();
 });
});
describe('directional QoS diagram', () => {
 it('includes separate entry and dispatch arrows', async () => {
   const { topologyToFlow } = await import('../src/components/QueueHierarchyDiagram');
   const { flowNodes, flowEdges } = topologyToFlow(topology);
   expect(flowNodes.find(n => n.id === '__start').deletable).toBe(false);
   expect(flowNodes.find(n => n.id === '__end').deletable).toBe(false);
   expect(flowEdges.find(e => e.id === '__ingress')).toMatchObject({ source: '__start', target: 'a' });
   expect(flowEdges.find(e => e.id === '__egress')).toMatchObject({ source: 'b', target: '__end' });
   expect(flowEdges.every(e => e.markerEnd)).toBe(true);
 });
 it('shows class-specific ingress routes without serializing synthetic nodes', async () => {
   const { topologyToFlow } = await import('../src/components/QueueHierarchyDiagram');
   expect(topologyToFlow({ ...topology, IngressMode: 'Router' }).flowEdges.some(e => e.label === 'gold' && e.target === 'a')).toBe(true);
   expect(topologyToFlow(null).flowNodes).toHaveLength(0);
 });
 it('keeps unavailable cached counts distinct from reported zeros', () => {
   expect(chartSeries([{}, { CachedTokens: 0 }], 'tokens')[2].values).toEqual([null, 0]);
 });
 it('keeps icon-button authored intent and avoids nearby field labels', () => {
   const { container } = render(<div className="form-group"><label>Nearby label</label><button title="Copy ID"><svg /></button><small>Nearby field help</small></div>);
   expect(inferTooltip(container.querySelector('button'))).toContain('Copy ID');
   expect(inferTooltip(container.querySelector('button'))).not.toContain('Nearby');
 });
});
