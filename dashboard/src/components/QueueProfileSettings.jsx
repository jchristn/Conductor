import React, { useState } from 'react';
import { createPortal } from 'react-dom';
import Modal from './Modal';
export default function QueueProfileSettings({ topology, onSave, onClose }) {
 const [draft, setDraft] = useState({ ...topology });
 const [error, setError] = useState('');
 const set = (key, value) => setDraft(d => ({ ...d, [key]: value }));
 const nodes = draft.Nodes || [];
 const routes = draft.IngressRoutes || [];
 return createPortal(<Modal className="queue-editor" isOpen title="Queuing entry, exit and limits" onClose={onClose} wide>
  {error && <p role="alert">{error}</p>}
  <label className="form-group">Default traffic class<input title="Class assigned when no classification rule matches an incoming request." value={draft.DefaultClass || ''} onChange={e => set('DefaultClass', e.target.value)}/></label>
  <label className="form-group">Ingress mode<select title="Send all requests to the default ingress, or route each traffic class to a queue." value={draft.IngressMode || 'Single'} onChange={e => set('IngressMode', e.target.value)}><option>Single</option><option>Router</option></select></label>
  {['IngressDefaultNode', 'TailNode'].map(key => <label className="form-group" key={key}>{key === 'TailNode' ? 'End of queuing' : 'Default start of queuing'}<select title={key === 'TailNode' ? 'Final queue before dispatch; all admitted work must be able to reach it.' : 'Default queue where requests start waiting after admission.'} value={draft[key] || ''} onChange={e => set(key, e.target.value)}><option value="">Choose a queue</option>{nodes.map(n => <option key={n.Name}>{n.Name}</option>)}</select></label>)}
  {draft.IngressMode === 'Router' && <><h3>Class ingress routes</h3>{routes.map((r, i) => <div key={i} className="form-group">
    <label>Traffic class<input title="Exact classified traffic class matched by this route; routes are evaluated in displayed order." value={r.ClassName || ''} onChange={e => set('IngressRoutes', routes.map((row, index) => index === i ? { ...row, ClassName: e.target.value } : row))}/></label>
    <label>Entry queue<select title="Queue where requests matching this class enter the topology." value={r.Node || ''} onChange={e => set('IngressRoutes', routes.map((row, index) => index === i ? { ...row, Node: e.target.value } : row))}><option value="">Choose a queue</option>{nodes.map(n => <option key={n.Name}>{n.Name}</option>)}</select></label>
    <button type="button" title="Remove this ingress route; unmatched traffic uses the default entry queue." onClick={() => set('IngressRoutes', routes.filter((_, index) => index !== i))}>Remove route</button>
  </div>)}<button type="button" title="Map another classified traffic class to an entry queue." onClick={() => set('IngressRoutes', [...routes, { ClassName: '', Node: nodes[0]?.Name || '' }])}>Add ingress route</button></>}
  {[['MaxTotalDepth', 'Maximum total depth', 0, undefined, 'Profile-wide limit for queued requests; zero permits unbounded depth.'], ['MaxQueueWaitMs', 'Maximum queue wait (ms)', 0, undefined, 'Expire a waiting request after this many milliseconds.'], ['RejectionStatusCode', 'Rejection HTTP status', 400, 599, 'HTTP failure status returned when the profile rejects admission.'], ['RetryAfterSeconds', 'Retry-After (seconds)', 0, undefined, 'Delay advertised to clients when Retry-After is enabled.']].map(([key, label, min, max, help]) => <label className="form-group" key={key}>{label}<input title={help} type="number" min={min} max={max} step="1" value={draft[key] ?? min} onChange={e => set(key, Number(e.target.value))}/></label>)}
  <label><input type="checkbox" title="Include a Retry-After header so rejected clients know when to retry." checked={draft.IncludeRetryAfter ?? true} onChange={e => set('IncludeRetryAfter', e.target.checked)}/>Include Retry-After</label>
  <div className="modal-footer"><button type="button" className="btn-secondary" title="Discard these profile routing and limit edits." onClick={onClose}>Cancel</button><button type="button" className="btn-primary" title="Apply routing and limit settings to the profile draft; save the profile to persist." onClick={() => {
   if (!draft.DefaultClass?.trim() || !nodes.some(n => n.Name === draft.IngressDefaultNode) || !nodes.some(n => n.Name === draft.TailNode)) { setError('Choose a default class and valid start/end queues.'); return; }
   for (const key of ['MaxTotalDepth','MaxQueueWaitMs','RetryAfterSeconds','RejectionStatusCode']) if (draft[key] != null && (!Number.isInteger(draft[key]) || draft[key] < (key === 'RejectionStatusCode' ? 400 : 0) || (key === 'RejectionStatusCode' && draft[key] > 599))) { setError('Limits must be non-negative integers; rejection status must be 400–599.'); return; }
   if (draft.IngressMode === 'Router' && routes.some(r => !r.ClassName?.trim() || !nodes.some(n => n.Name === r.Node))) { setError('Every ingress route needs a traffic class and an existing queue.'); return; }
   onSave({ ...draft, IngressRoutes: routes.map((r, i) => ({ ...r, Ordinal: i })) });
  }}>Apply settings</button></div>
 </Modal>, document.body);
}
