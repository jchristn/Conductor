import React, { useCallback, useMemo } from 'react';
import {
  ReactFlow,
  Background,
  Controls,
  addEdge,
  useNodesState,
  useEdgesState,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import QueueProfileSettings from './QueueProfileSettings';
import QueueNodeEditor from './QueueNodeEditor';
import { validConnection } from '../utils/qosTopology';
import { MarkerType, Position } from '@xyflow/react';

// Compute a simple topological level for each node based on Links (FromNode -> ToNode).
// Nodes with no incoming link are level 0; every other node is one deeper than its deepest parent.
// Cycles and missing references are handled defensively so the diagram never throws.
function computeLevels(nodeNames, links) {
  const levels = {};
  nodeNames.forEach((name) => { levels[name] = 0; });

  // Iterate a bounded number of passes to settle levels without risking an infinite loop on cycles.
  const maxPasses = nodeNames.length + 1;
  for (let pass = 0; pass < maxPasses; pass += 1) {
    let changed = false;
    links.forEach((link) => {
      if (!link || !link.FromNode || !link.ToNode) return;
      if (!(link.FromNode in levels) || !(link.ToNode in levels)) return;
      const candidate = levels[link.FromNode] + 1;
      if (candidate > levels[link.ToNode]) {
        levels[link.ToNode] = candidate;
        changed = true;
      }
    });
    if (!changed) break;
  }
  return levels;
}

// Convert a profile topology object into React Flow nodes and edges with a basic layered layout.
export function topologyToFlow(topology) {
  const safeTopology = topology && typeof topology === 'object' ? topology : {};
  const nodeList = Array.isArray(safeTopology.Nodes) ? safeTopology.Nodes : [];
  const linkList = Array.isArray(safeTopology.Links) ? safeTopology.Links : [];
  const ingressDefault = safeTopology.IngressDefaultNode || null;
  const tailNode = safeTopology.TailNode || null;

  const nodeNames = nodeList
    .map((node) => (node && node.Name ? String(node.Name) : null))
    .filter((name) => name !== null);

  const levels = computeLevels(nodeNames, linkList);

  // Track how many nodes we have already placed at each level so rows stack without overlapping.
  const rowCounters = {};
  const columnWidth = 240;
  const rowHeight = 120;

  const flowNodes = nodeList
    .filter((node) => node && node.Name)
    .map((node) => {
      const name = String(node.Name);
      const level = levels[name] || 0;
      const row = rowCounters[level] || 0;
      rowCounters[level] = row + 1;

      const isIngress = ingressDefault && name === ingressDefault;
      const isTail = tailNode && name === tailNode;
      const roleParts = [];
      if (isIngress) roleParts.push('ingress');
      if (isTail) roleParts.push('tail');

      const discipline = node.Discipline || 'Fifo';
      const depth = (node.MaxDepth === 0 || node.MaxDepth) ? node.MaxDepth : '';
      const classCount = Array.isArray(node.Classes) ? node.Classes.length : 0;

      return {
        id: name,
        sourcePosition: Position.Right, targetPosition: Position.Left,
        position: { x: level * columnWidth + 40, y: row * rowHeight + 40 },
        data: {
          label: (
            <div style={{ textAlign: 'left', fontSize: 12, lineHeight: 1.35 }}>
              <div style={{ fontWeight: 600 }}>{name}</div>
              <div style={{ opacity: 0.8 }}>{discipline}{depth !== '' ? ` · depth ${depth}` : ''}</div>
              <div style={{ opacity: 0.7 }}>{classCount} class{classCount === 1 ? '' : 'es'}</div>
              {roleParts.length > 0 && (
                <div style={{ marginTop: 2, fontWeight: 600, color: '#2563eb' }}>{roleParts.join(' · ')}</div>
              )}
            </div>
          ),
        },
        style: {
          border: isIngress ? '2px solid #2563eb' : (isTail ? '2px solid #16a34a' : '1px solid #94a3b8'),
          borderRadius: 8,
          padding: 8,
          background: '#ffffff',
          color: '#0f172a',
          width: 190,
        },
      };
    });

  const flowEdges = linkList
    .filter((link) => link && link.FromNode && link.ToNode)
    .map((link, index) => ({
      id: `${link.FromNode}->${link.ToNode}-${index}`,
      source: String(link.FromNode),
      target: String(link.ToNode),
      animated: false,
      markerEnd: { type: MarkerType.ArrowClosed },
    }));

  if (ingressDefault && nodeNames.includes(ingressDefault)) {
    flowNodes.push({ id: '__start', type: 'input', sourcePosition: Position.Right, position: { x: -220, y: 40 }, data: { label: 'Start of queuing →' }, deletable: false, connectable: false });
    if (safeTopology.IngressMode === 'Router') (safeTopology.IngressRoutes || []).forEach((route, i) => {
      if (nodeNames.includes(route.Node)) flowEdges.push({ id: `__route-${i}`, source: '__start', target: route.Node, label: route.ClassName, deletable: false, markerEnd: { type: MarkerType.ArrowClosed } });
    });
    flowEdges.push({ id: '__ingress', source: '__start', target: ingressDefault, deletable: false, markerEnd: { type: MarkerType.ArrowClosed } });
  }
  if (tailNode && nodeNames.includes(tailNode)) {
    const tail = flowNodes.find(n => n.id === tailNode);
    flowNodes.push({ id: '__end', type: 'output', targetPosition: Position.Left, position: { x: tail.position.x + 260, y: tail.position.y }, data: { label: 'End of queuing → Dispatch' }, deletable: false, connectable: false });
    flowEdges.push({ id: '__egress', source: tailNode, target: '__end', deletable: false, markerEnd: { type: MarkerType.ArrowClosed } });
  }
  return { flowNodes, flowEdges };
}

/**
 * Interactive queue-hierarchy diagram for a QoS profile topology.
 *
 * The `topology` prop is the single source of truth ({ Nodes, Links, IngressMode,
 * IngressDefaultNode, TailNode }). All user gestures (add node, delete node, connect/remove
 * edge) are translated back into a topology object and reported through `onChange` so the
 * parent can keep its JSON representation in sync. The component is defensive: an empty,
 * null, or malformed topology renders an empty canvas instead of throwing.
 */
export default function QueueHierarchyDiagram({ topology, onChange }) {
  const [settingsOpen, setSettingsOpen] = React.useState(false);
  const [editing, setEditing] = React.useState(null);
  const { flowNodes, flowEdges } = useMemo(() => topologyToFlow(topology), [topology]);

  const [nodes, setNodes, onNodesChange] = useNodesState(flowNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(flowEdges);

  // Re-seed local flow state whenever the incoming topology changes (e.g. template applied, JSON edited).
  React.useEffect(() => {
    setNodes(current => flowNodes.map(node => ({ ...node, position: (!node.id.startsWith('__') && current.find(n => n.id === node.id)?.position) || node.position })));
    setEdges(flowEdges);
  }, [flowNodes, flowEdges, setNodes, setEdges]);

  // Build an updated topology object from the current node/edge ids, preserving the original
  // per-node fields (Discipline, Classes, etc.) and scalar ingress/tail settings.
  const emitChange = useCallback((nextNodeIds, nextEdges) => {
    if (typeof onChange !== 'function') return;
    const source = topology && typeof topology === 'object' ? topology : {};
    const existingNodes = Array.isArray(source.Nodes) ? source.Nodes : [];
    const nodeByName = {};
    existingNodes.forEach((node) => { if (node && node.Name) nodeByName[String(node.Name)] = node; });

    const nextNodes = nextNodeIds.filter(id => !id.startsWith('__')).map((id) => nodeByName[id] || {
      Name: id, Discipline: 'Fifo', MaxDepth: 0, OverflowPolicy: 'Reject', Classes: [],
    });

    const nextLinks = nextEdges
      .filter((edge) => edge && edge.source && edge.target && !edge.source.startsWith('__') && !edge.target.startsWith('__'))
      .map((edge) => ({ FromNode: edge.source, ToNode: edge.target }));

    onChange({
      ...source,
      Nodes: nextNodes,
      Links: nextLinks,
      IngressDefaultNode: nextNodes.some(n => n.Name === source.IngressDefaultNode) ? source.IngressDefaultNode : nextNodes[0]?.Name || null,
      TailNode: nextNodes.some(n => n.Name === source.TailNode) ? source.TailNode : nextNodes.at(-1)?.Name || null,
      IngressRoutes: (source.IngressRoutes || []).filter(r => nextNodes.some(n => n.Name === r.Node)),
    });
  }, [onChange, topology]);

  const onConnect = useCallback((connection) => {
    if (!validConnection(topology, connection.source, connection.target)) return;
    const updated = addEdge(connection, edges);
    setEdges(updated);
    emitChange(nodes.map(node => node.id), updated);
  }, [setEdges, emitChange, nodes, edges, topology]);

  const onNodesDelete = useCallback((deleted) => {
    const deletedIds = new Set(deleted.map((node) => node.id));
    const remainingNodeIds = nodes.map((node) => node.id).filter((id) => !deletedIds.has(id));
    const remainingEdges = edges.filter((edge) => !deletedIds.has(edge.source) && !deletedIds.has(edge.target));
    emitChange(remainingNodeIds, remainingEdges);
  }, [nodes, edges, emitChange]);

  const onEdgesDelete = useCallback((deleted) => {
    const deletedIds = new Set(deleted.map((edge) => edge.id));
    const remainingEdges = edges.filter((edge) => !deletedIds.has(edge.id));
    emitChange(nodes.map((node) => node.id), remainingEdges);
  }, [nodes, edges, emitChange]);

  const handleAddNode = () => setEditing({ node: null });

  return (
    <>
    <button type="button" className="btn-secondary btn-small" title="Configure default class, ingress routing, start/end queues and profile-wide admission limits." onClick={() => setSettingsOpen(true)}>Entry, exit and limits</button>
    <p title="Arrow direction follows request flow. Node edits remain in the draft until the profile is saved.">Click a queue to configure it. Drag between handles to connect queues. Select a link and press Delete to remove it.</p>
    <div style={{ position: 'relative', height: 480, border: '1px solid var(--border-color, #cbd5e1)', borderRadius: 8, overflow: 'hidden' }}>
      <div style={{ position: 'absolute', top: 8, left: 8, zIndex: 5 }}>
        <button type="button" className="btn-secondary btn-small" title="Create a queue and choose its scheduling algorithm and limits." onClick={handleAddNode}>Add node</button>
      </div>
      <ReactFlow
        onNodeClick={(_, node) => { if (!node.id.startsWith('__')) setEditing({ node: (topology?.Nodes || []).find(n => n.Name === node.id) }); }}
        isValidConnection={c => validConnection(topology, c.source, c.target)}
        defaultEdgeOptions={{ markerEnd: { type: MarkerType.ArrowClosed } }}
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onConnect={onConnect}
        onNodesDelete={onNodesDelete}
        onEdgesDelete={onEdgesDelete}
        fitView
        proOptions={{ hideAttribution: true }}
      >
        <Background />
        <Controls />
      </ReactFlow>
    </div>
    {settingsOpen && <QueueProfileSettings topology={topology} onClose={() => setSettingsOpen(false)} onSave={value => { onChange(value); setSettingsOpen(false); }} />}
    {editing && <QueueNodeEditor topology={topology} node={editing.node} onClose={() => setEditing(null)} onSave={value => { onChange(value); setEditing(null); }} />}
    </>
  );
}
