import React, { useState } from 'react';
import { createPortal } from 'react-dom';
import Modal from './Modal';
import { saveQueue } from '../utils/qosTopology';

const DISCIPLINES = {
  Fifo: 'FIFO — First in, first out',
  Lifo: 'LIFO — Last in, first out',
  Priority: 'Priority — Strict priority bands',
  Wfq: 'WFQ — Weighted fair queuing',
  Cbwfq: 'CBWFQ — Class-based weighted fair queuing',
  Llq: 'LLQ — Low-latency queuing',
  Wrr: 'WRR — Weighted round robin',
};
const CLASS_KINDS = {
  Priority: ['Band'], Wfq: ['Flow'], Cbwfq: ['Class'],
  Llq: ['PriorityClass', 'FairClass'], Wrr: ['SubQueue'],
};
const KIND_LABELS = {
  Band: 'Priority band', Flow: 'Weighted flow', Class: 'Weighted class',
  PriorityClass: 'Priority class', FairClass: 'Weighted fair class', SubQueue: 'Weighted subqueue',
};
const FLOW_SOURCES = {
  Header: 'HTTP header', BodyJsonPath: 'JSON body path', QueryParam: 'Query parameter',
  Model: 'Model', ApiFamily: 'API family', RequestType: 'Request type', Tenant: 'Tenant',
  Credential: 'Credential', User: 'User', ClientIp: 'Client IP address', Vmr: 'Virtual model runner',
};
const FIELDS = [
  ['MaxDepth', 'Maximum depth', 'Maximum number of waiting requests. Set to 0 for unlimited depth.', 0],
  ['AgingThresholdMs', 'Priority aging (ms)', 'Promote waiting priority requests after this interval. Set to 0 to disable aging.', 0],
  ['DefaultWeight', 'Default weight', 'Relative service share for the default class or a dynamically created flow.', 1],
  ['FlowKey', 'Flow key', 'Header name or attribute key used to identify a weighted fair flow.'],
  ['DefaultKey', 'Default class or flow', 'Name of the existing class or flow used by the default routing policy.'],
];
const CLASS_FIELDS = {
  ClassName: ['Class name', 'Traffic class or flow name matched by classification.'],
  Kind: ['Class type', 'Scheduling role of this class within the selected queue type.'],
  Weight: ['Weight', 'Relative service share. Enter a whole number of at least 1.'],
  Band: ['Priority band', 'Priority band index. Lower values have higher priority.'],
  RatePerSecond: ['Rate per second', 'LLQ priority-class token bucket refill rate per second. Leave empty for an unpoliced class.'],
  Burst: ['Burst capacity', 'Maximum token bucket capacity for an LLQ priority class.'],
};

export default function QueueNodeEditor({ topology, node, onSave, onClose }) {
  const [draft, setDraft] = useState(node || {
    Name: '', Discipline: 'Fifo', MaxDepth: 0, OverflowPolicy: 'Reject', DefaultWeight: 1, Classes: [],
  });
  const [roles, setRoles] = useState({
    ingress: node?.Name === topology?.IngressDefaultNode,
    tail: node?.Name === topology?.TailNode,
  });
  const [error, setError] = useState('');
  const kinds = CLASS_KINDS[draft.Discipline] || [];
  const fair = ['Wfq', 'Cbwfq', 'Llq', 'Wrr'].includes(draft.Discipline);
  const set = (key, value) => setDraft(d => ({ ...d, [key]: value }));
  const updateClass = (index, key, value) => set('Classes', draft.Classes.map((row, i) => (
    i === index ? { ...row, [key]: value } : row
  )));
  const apply = () => {
    try { onSave(saveQueue(topology, node?.Name, draft, roles)); }
    catch (e) { setError(e.message); }
  };

  return createPortal(
    <Modal className="queue-editor" isOpen title={node ? `Configure queue: ${node.Name}` : 'Add queue'} onClose={onClose} wide>
      <p className="queue-editor-intro">Choose how this queue schedules requests. Apply your changes here, then save the profile to keep them.</p>
      {error && <p role="alert" className="error-banner">{error}</p>}
      <div className="queue-form-grid">
        <label className="form-group">Queue name
          <input value={draft.Name} title="Unique queue name referenced by links and ingress routes." onChange={e => set('Name', e.target.value)} />
        </label>
        <label className="form-group">Queue type
          <select title="Choose the algorithm that selects the next waiting request." value={draft.Discipline} onChange={e => {
            const next = e.target.value;
            setDraft(d => ({ ...d, Discipline: next, Classes: (d.Classes || []).map(row => ({
              ...row, Kind: CLASS_KINDS[next]?.[next === 'Llq' ? 1 : 0] || row.Kind,
            })) }));
          }}>
            {Object.entries(DISCIPLINES).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
        </label>
        <label className="form-group">Overflow policy
          <select title="Choose what happens when the queue reaches its maximum depth. Waiting for capacity is not allowed on ingress-reachable queues." value={draft.OverflowPolicy} onChange={e => set('OverflowPolicy', e.target.value)}>
            {Object.entries({ Reject: 'Reject the incoming request', DropNewest: 'Drop the incoming request', DropOldest: 'Drop the oldest queued request', Block: 'Wait for queue capacity' }).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
        </label>
        {FIELDS.filter(([key]) => key !== 'DefaultWeight' || fair)
          .filter(([key]) => key !== 'AgingThresholdMs' || draft.Discipline === 'Priority')
          .filter(([key]) => !['FlowKey', 'DefaultKey'].includes(key) || ['Wfq', 'Wrr'].includes(draft.Discipline))
          .map(([key, label, help, min]) => <label className="form-group" key={key}>{label}
            <input aria-label={label} title={help} type={min == null ? 'text' : 'number'} min={min} step={min == null ? undefined : 1} value={draft[key] ?? (min ?? '')} onChange={e => set(key, min == null ? e.target.value : Number(e.target.value))} />
            {key === 'MaxDepth' && <small>Set to 0 for unlimited depth.</small>}
          </label>)}
        {draft.Discipline === 'Wfq' && <label className="form-group">Flow source
          <select title="Request attribute used to identify weighted fair flows." value={draft.FlowSource || ''} onChange={e => set('FlowSource', e.target.value || null)}>
            <option value="">Not specified</option>
            {Object.entries(FLOW_SOURCES).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
        </label>}
        {['Wfq', 'Wrr'].includes(draft.Discipline) && <label className="form-group">Unknown key policy
          <select title="Choose how to handle a request whose flow or class is not defined." value={draft.UnknownKeyPolicy || 'Throw'} onChange={e => set('UnknownKeyPolicy', e.target.value)}>
            {Object.entries({ Throw: 'Report an error', RouteToDefault: 'Use the default flow or class', CreateDynamic: 'Create a flow dynamically', Reject: 'Reject the request' }).filter(([value]) => value !== 'CreateDynamic' || draft.Discipline === 'Wfq').map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
        </label>}
      </div>

      {kinds.length > 0 && <section className="queue-editor-section" aria-label="Scheduling classes">
        <div className="queue-section-heading"><h3>Scheduling classes</h3><span className="text-muted">{draft.Classes?.length || 0} defined</span></div>
        <p className="text-muted">Define the traffic classes this queue serves. Choose a class type and set the scheduling values that apply.</p>
        {!draft.Classes?.length && <p className="queue-empty-state">No classes yet. Add a class to configure its scheduling behavior.</p>}
        {(draft.Classes || []).map((row, index) => <div key={index} className="queue-config-card">
          <div className="queue-section-heading"><h4>Class {index + 1}{row.ClassName ? `: ${row.ClassName}` : ''}</h4>
            <button type="button" className="btn-secondary btn-small" title="Remove this class from the queue draft. Save the profile to keep this change." onClick={() => set('Classes', draft.Classes.filter((_, i) => i !== index))}>Remove class</button>
          </div>
          <div className="queue-form-grid">
            {Object.entries(CLASS_FIELDS)
              .filter(([key]) => key !== 'Weight' || (fair && row.Kind !== 'PriorityClass'))
              .filter(([key]) => key !== 'Band' || draft.Discipline === 'Priority')
              .filter(([key]) => !['RatePerSecond', 'Burst'].includes(key) || (draft.Discipline === 'Llq' && row.Kind === 'PriorityClass'))
              .map(([key, [label, help]]) => <label className="form-group" key={key}>{label}
                {key === 'Kind' ? <select title={help} value={row.Kind} onChange={e => updateClass(index, key, e.target.value)}>
                  {kinds.map(value => <option key={value} value={value}>{KIND_LABELS[value]}</option>)}
                </select> : <input aria-label={label} title={help} type={key === 'ClassName' ? 'text' : 'number'} min={key === 'Weight' ? 1 : 0} step={['RatePerSecond', 'Burst'].includes(key) ? 'any' : 1} value={row[key] ?? ''} onChange={e => updateClass(index, key, key === 'ClassName' ? e.target.value : e.target.value === '' ? null : Number(e.target.value))} />}
              </label>)}
          </div>
        </div>)}
        <button type="button" className="btn-secondary queue-add-button" title="Add a traffic class and configure its scheduling type, weight or priority." onClick={() => set('Classes', [...(draft.Classes || []), { ClassName: '', Kind: kinds[draft.Discipline === 'Llq' ? 1 : 0], Weight: 1 }])}><span aria-hidden="true">+</span> Add class</button>
      </section>}

      <section className="queue-editor-section" aria-label="Queue placement">
        <h3>Queue placement</h3>
        <p className="text-muted">Choose whether requests enter or leave queuing here. Connect queues in the diagram after applying your changes.</p>
        <div className="queue-choice-list">
          {['ingress', 'tail'].map(key => <label key={key} className="checkbox-label">
            <input type="radio" name={`queue-role-${key}`} checked={roles[key]} title={key === 'ingress' ? 'Make this queue the default entry point for admitted requests.' : 'Make this queue the final queue before endpoint dispatch.'} onChange={e => setRoles(r => ({ ...r, [key]: e.target.checked }))} />
            {key === 'ingress' ? 'Start of queuing (default entry)' : 'End of queuing (tail)'}
          </label>)}
        </div>
      </section>
      <section className="queue-editor-section" aria-label="Queue options">
        <h3>Queue options</h3>
        <div className="queue-choice-list">
          {['WrrClassifierMode', 'EnablePerClassMetrics', 'EnableTracing'].filter(key => key !== 'WrrClassifierMode' || draft.Discipline === 'Wrr').map(key => <label key={key} className="checkbox-label">
            <input type="checkbox" checked={draft[key] ?? key !== 'WrrClassifierMode'} title={{ WrrClassifierMode: 'Route WRR requests by class instead of balancing by weight.', EnablePerClassMetrics: 'Emit per-class metrics. Disable this for an unbounded number of dynamic flows.', EnableTracing: 'Record enqueue and dequeue trace spans.' }[key]} onChange={e => set(key, e.target.checked)} />
            {{ WrrClassifierMode: 'Route WRR requests by class', EnablePerClassMetrics: 'Enable per-class metrics', EnableTracing: 'Enable queue tracing' }[key]}
          </label>)}
        </div>
      </section>
      <div className="modal-footer">
        <button type="button" className="btn-secondary" onClick={onClose} title="Discard these queue edits and return to the diagram.">Cancel</button>
        <button type="button" className="btn-primary" title="Apply these queue settings to the profile draft. Save the profile to keep them." onClick={apply}>Apply queue</button>
      </div>
    </Modal>, document.body,
  );
}
