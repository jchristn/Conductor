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
function topologyToFlow(topology) {
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
    }));

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
  const { flowNodes, flowEdges } = useMemo(() => topologyToFlow(topology), [topology]);

  const [nodes, setNodes, onNodesChange] = useNodesState(flowNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(flowEdges);

  // Re-seed local flow state whenever the incoming topology changes (e.g. template applied, JSON edited).
  React.useEffect(() => {
    setNodes(flowNodes);
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

    const nextNodes = nextNodeIds.map((id) => nodeByName[id] || {
      Name: id, Discipline: 'Fifo', MaxDepth: 0, OverflowPolicy: 'Reject', Classes: [],
    });

    const nextLinks = nextEdges
      .filter((edge) => edge && edge.source && edge.target)
      .map((edge) => ({ FromNode: edge.source, ToNode: edge.target }));

    onChange({
      ...source,
      Nodes: nextNodes,
      Links: nextLinks,
    });
  }, [onChange, topology]);

  const onConnect = useCallback((connection) => {
    setEdges((current) => {
      const updated = addEdge(connection, current);
      emitChange(nodes.map((node) => node.id), updated);
      return updated;
    });
  }, [setEdges, emitChange, nodes]);

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

  const handleAddNode = useCallback(() => {
    // Generate a unique node name so the topology stays valid.
    const existing = new Set(nodes.map((node) => node.id));
    let suffix = existing.size + 1;
    let name = `node-${suffix}`;
    while (existing.has(name)) {
      suffix += 1;
      name = `node-${suffix}`;
    }
    emitChange([...nodes.map((node) => node.id), name], edges);
  }, [nodes, edges, emitChange]);

  return (
    <div style={{ position: 'relative', height: 380, border: '1px solid var(--border-color, #cbd5e1)', borderRadius: 8, overflow: 'hidden' }}>
      <div style={{ position: 'absolute', top: 8, left: 8, zIndex: 5 }}>
        <button type="button" className="btn" onClick={handleAddNode}>Add node</button>
      </div>
      <ReactFlow
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
  );
}
