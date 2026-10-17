import React, { useState } from 'react';
import { createPortal } from 'react-dom';
import Modal from './Modal';

const LIMITS = [
  ['MaxTotalDepth', 'Maximum total depth', 0, undefined, 'Maximum number of waiting requests across all queues. Set to 0 for unlimited depth.'],
  ['MaxQueueWaitMs', 'Maximum queue wait (ms)', 0, undefined, 'Time a request may wait in a queue before it expires, in milliseconds.'],
  ['RejectionStatusCode', 'Rejection HTTP status', 400, 599, 'HTTP status returned when the profile rejects a request. Enter a code from 400 to 599.'],
  ['RetryAfterSeconds', 'Retry-After (seconds)', 0, undefined, 'Number of seconds clients should wait before retrying a rejected request.'],
];

export default function QueueProfileSettings({ topology, onSave, onClose }) {
  const [draft, setDraft] = useState({ ...topology });
  const [error, setError] = useState('');
  const set = (key, value) => setDraft(d => ({ ...d, [key]: value }));
  const nodes = draft.Nodes || [];
  const routes = draft.IngressRoutes || [];
  const updateRoute = (index, key, value) => set('IngressRoutes', routes.map((row, i) => (
    i === index ? { ...row, [key]: value } : row
  )));
  const apply = () => {
    if (!draft.DefaultClass?.trim() || !nodes.some(n => n.Name === draft.IngressDefaultNode) || !nodes.some(n => n.Name === draft.TailNode)) {
      setError('Enter a default traffic class and choose an existing queue for both the start and end of queuing.');
      return;
    }
    for (const [key, label, min, max] of LIMITS) {
      if (draft[key] != null && (!Number.isInteger(draft[key]) || draft[key] < min || (max != null && draft[key] > max))) {
        setError(`${label} must be a whole number ${max == null ? `of at least ${min}` : `between ${min} and ${max}`}.`);
        return;
      }
    }
    if (draft.IngressMode === 'Router' && routes.some(r => !r.ClassName?.trim() || !nodes.some(n => n.Name === r.Node))) {
      setError('Every ingress route needs a traffic class and an existing entry queue.');
      return;
    }
    onSave({ ...draft, IngressRoutes: routes.map((r, i) => ({ ...r, Ordinal: i })) });
  };

  return createPortal(
    <Modal className="queue-editor" isOpen title="Queue entry, exit and limits" onClose={onClose} wide>
      <p className="queue-editor-intro">Choose where requests enter and leave queuing, then set the limits that apply to the whole profile.</p>
      {error && <p role="alert" className="error-banner">{error}</p>}
      <div className="queue-form-grid">
        <label className="form-group">Default traffic class
          <input title="Traffic class assigned when no classification rule matches the request." value={draft.DefaultClass || ''} onChange={e => set('DefaultClass', e.target.value)} />
        </label>
        <label className="form-group">Ingress mode
          <select title="Send all requests to one entry queue, or route them to entry queues by traffic class." value={draft.IngressMode || 'Single'} onChange={e => set('IngressMode', e.target.value)}>
            <option value="Single">Single entry queue</option>
            <option value="Router">Route by traffic class</option>
          </select>
        </label>
        {['IngressDefaultNode', 'TailNode'].map(key => <label className="form-group" key={key}>{key === 'TailNode' ? 'End of queuing' : 'Default start of queuing'}
          <select title={key === 'TailNode' ? 'Final queue before dispatch. All admitted requests must be able to reach it.' : 'Entry queue used for all requests in single-entry mode, or when no ingress route matches.'} value={draft[key] || ''} onChange={e => set(key, e.target.value)}>
            <option value="">Choose a queue</option>
            {nodes.map(n => <option key={n.Name} value={n.Name}>{n.Name}</option>)}
          </select>
        </label>)}
      </div>
      {draft.IngressMode === 'Router' && <section className="queue-editor-section" aria-label="Class ingress routes">
        <h3>Class ingress routes</h3>
        <p className="text-muted">Routes are evaluated in the order shown. Requests that do not match a route use the default entry queue.</p>
        {!routes.length && <p className="queue-empty-state">No routes yet. Add a route to send a traffic class to a specific entry queue.</p>}
        {routes.map((r, i) => <div key={i} className="queue-config-card">
          <div className="queue-section-heading"><h4>Route {i + 1}</h4>
            <button type="button" className="btn-secondary btn-small" title="Remove this route from the profile draft. Unmatched traffic uses the default entry queue." onClick={() => set('IngressRoutes', routes.filter((_, index) => index !== i))}>Remove route</button>
          </div>
          <div className="queue-form-grid">
            <label className="form-group">Traffic class
              <input title="Exact traffic class matched by this route." value={r.ClassName || ''} onChange={e => updateRoute(i, 'ClassName', e.target.value)} />
            </label>
            <label className="form-group">Entry queue
              <select title="Queue where requests matching this traffic class enter the topology." value={r.Node || ''} onChange={e => updateRoute(i, 'Node', e.target.value)}>
                <option value="">Choose a queue</option>
                {nodes.map(n => <option key={n.Name} value={n.Name}>{n.Name}</option>)}
              </select>
            </label>
          </div>
        </div>)}
        <button type="button" className="btn-secondary queue-add-button" title="Add a route from a traffic class to an entry queue." onClick={() => set('IngressRoutes', [...routes, { ClassName: '', Node: nodes[0]?.Name || '' }])}><span aria-hidden="true">+</span> Add ingress route</button>
      </section>}
      <section className="queue-editor-section" aria-label="Profile limits">
        <h3>Profile limits</h3>
        <div className="queue-form-grid">
          {LIMITS.map(([key, label, min, max, help]) => <label className="form-group" key={key}>{label}
            <input aria-label={label} title={help} type="number" min={min} max={max} step="1" value={draft[key] ?? min} onChange={e => set(key, Number(e.target.value))} />
            {key === 'MaxTotalDepth' && <small>Set to 0 for unlimited depth.</small>}
          </label>)}
        </div>
        <label className="checkbox-label"><input type="checkbox" title="Include a Retry-After header to tell rejected clients when to try again." checked={draft.IncludeRetryAfter ?? true} onChange={e => set('IncludeRetryAfter', e.target.checked)} />Include Retry-After header</label>
      </section>
      <div className="modal-footer">
        <button type="button" className="btn-secondary" title="Discard these routing and limit edits." onClick={onClose}>Cancel</button>
        <button type="button" className="btn-primary" title="Apply these settings to the profile draft. Save the profile to keep them." onClick={apply}>Apply settings</button>
      </div>
    </Modal>, document.body,
  );
}
