export function saveQueue(topology, originalName, draft, roles = {}) {
  const source = topology || {};
  const nodes = source.Nodes || [];
  const name = draft.Name?.trim();
  if (!name || name.startsWith('__')) throw new Error('Enter a queue name that does not begin with __.');
  if (nodes.some(n => n.Name === name && n.Name !== originalName)) throw new Error('Queue names must be unique.');
  for (const key of ['MaxDepth', 'AgingThresholdMs', 'DefaultWeight']) {
    const min = key === 'DefaultWeight' ? 1 : 0;
    if (!Number.isInteger(draft[key] ?? min) || (draft[key] ?? min) < min) throw new Error(`${key} must be an integer of at least ${min}.`);
  }
  const classes = (draft.Classes || []).map((row, Ordinal) => {
    if (!row.ClassName?.trim()) throw new Error('Each class needs a name.');
    for (const key of ['Weight', 'Band', 'RatePerSecond', 'Burst']) {
      if (row[key] != null && (!Number.isFinite(row[key]) || row[key] < (key === 'Weight' ? 1 : 0))) throw new Error(`${key} is outside its allowed range.`);
    }
    return { ...row, Ordinal };
  });
  draft = { ...draft, Classes: classes };
  const rename = value => value === originalName ? name : value;
  return { ...source, Nodes: originalName ? nodes.map(n => n.Name === originalName ? { ...draft, Name: name } : n) : [...nodes, { ...draft, Name: name }],
    Links: (source.Links || []).map(l => ({ ...l, FromNode: rename(l.FromNode), ToNode: rename(l.ToNode) })),
    IngressRoutes: (source.IngressRoutes || []).map(r => ({ ...r, Node: rename(r.Node) })),
    IngressDefaultNode: roles.ingress || !source.IngressDefaultNode ? name : rename(source.IngressDefaultNode),
    TailNode: roles.tail || !source.TailNode ? name : rename(source.TailNode) };
}
export function validConnection(topology, source, target) {
  if (source === target || !topology?.Nodes?.some(n => n.Name === source) || !topology.Nodes.some(n => n.Name === target)) return false;
  const links = topology.Links || [];
  if (links.some(l => l.FromNode === source && l.ToNode === target)) return false;
  const seen = new Set();
  function reaches(node) {
    if (node === source) return true;
    if (seen.has(node)) return false;
    seen.add(node);
    return links.filter(l => l.FromNode === node).some(l => reaches(l.ToNode));
  }
  return !reaches(target);
}
