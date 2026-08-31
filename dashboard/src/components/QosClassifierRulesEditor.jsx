import React from 'react';

// Request attributes a classification rule can inspect. Sources listed in KEY_SOURCES require a
// MatchKey (the header name, query parameter, or JSON path); the remaining sources classify on an
// intrinsic request attribute and ignore the key entirely.
const SOURCE_OPTIONS = [
  'Header',
  'BodyJsonPath',
  'QueryParam',
  'Model',
  'ApiFamily',
  'RequestType',
  'Tenant',
  'Credential',
  'User',
  'ClientIp',
  'Vmr',
];

// Sources whose match is keyed by a caller-supplied name (header/query/JSON-path).
const KEY_SOURCES = new Set(['Header', 'BodyJsonPath', 'QueryParam']);

const OPERATOR_OPTIONS = ['Equals', 'Contains', 'Regex', 'Exists', 'GreaterThan', 'LessThan'];

function keyPlaceholder(source) {
  switch (source) {
    case 'Header': return 'Header name (e.g. X-Conductor-Class)';
    case 'QueryParam': return 'Query parameter name';
    case 'BodyJsonPath': return 'JSON path (e.g. $.metadata.tier)';
    default: return 'Unused for this source';
  }
}

function DeleteIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
      <path fillRule="evenodd" d="M8 4a1 1 0 011-1h2a1 1 0 011 1h4a1 1 0 110 2h-1v10a2 2 0 01-2 2H7a2 2 0 01-2-2V6H4a1 1 0 010-2h4zm-1 2v10h6V6H7zm2 2a1 1 0 011 1v4a1 1 0 11-2 0V9a1 1 0 011-1zm3 0a1 1 0 011 1v4a1 1 0 11-2 0V9a1 1 0 011-1z" clipRule="evenodd" />
    </svg>
  );
}

function UpIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
      <path fillRule="evenodd" d="M10 5a1 1 0 01.707.293l5 5a1 1 0 01-1.414 1.414L10 7.414l-4.293 4.293a1 1 0 01-1.414-1.414l5-5A1 1 0 0110 5z" clipRule="evenodd" />
    </svg>
  );
}

function DownIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
      <path fillRule="evenodd" d="M10 15a1 1 0 01-.707-.293l-5-5a1 1 0 011.414-1.414L10 12.586l4.293-4.293a1 1 0 011.414 1.414l-5 5A1 1 0 0110 15z" clipRule="evenodd" />
    </svg>
  );
}

function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
      <path fillRule="evenodd" d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z" clipRule="evenodd" />
    </svg>
  );
}

function emptyRule() {
  return { Ordinal: 0, Source: 'Header', MatchKey: '', Operator: 'Equals', MatchValue: '', ClassName: '' };
}

// Re-number Ordinal to match array position so ordinals always reflect evaluation order.
function reindex(rules) {
  return rules.map((rule, index) => ({ ...rule, Ordinal: index }));
}

/**
 * Structured editor for a QoS profile's classification Rules array. Fully controlled: every edit
 * calls onChange with a fresh array whose Ordinal values are renumbered by position.
 *
 * @param {object} props
 * @param {Array<object>} [props.rules] Rules to edit; each { Ordinal, Source, MatchKey, Operator, MatchValue, ClassName }.
 * @param {Array<string>} [props.classes] Known class names offered as suggestions for ClassName.
 * @param {(rules: Array<object>) => void} props.onChange Invoked with the updated, renumbered rules.
 */
function QosClassifierRulesEditor({ rules, classes, onChange }) {
  const rows = Array.isArray(rules) ? rules : [];
  const classOptions = Array.isArray(classes) ? classes.filter(Boolean) : [];
  const datalistId = 'qos-class-name-options';

  const updateRow = (index, field, value) => {
    const next = rows.map((rule, currentIndex) => (
      currentIndex === index ? { ...rule, [field]: value } : rule
    ));
    onChange(reindex(next));
  };

  const deleteRow = (index) => {
    onChange(reindex(rows.filter((_, currentIndex) => currentIndex !== index)));
  };

  const moveRow = (index, delta) => {
    const target = index + delta;
    if (target < 0 || target >= rows.length) return;
    const next = rows.slice();
    const [moved] = next.splice(index, 1);
    next.splice(target, 0, moved);
    onChange(reindex(next));
  };

  const addRow = () => {
    onChange(reindex([...rows, emptyRule()]));
  };

  return (
    <div className="qos-rules-editor">
      {classOptions.length > 0 && (
        <datalist id={datalistId}>
          {classOptions.map((name) => <option key={name} value={name} />)}
        </datalist>
      )}

      {rows.length === 0 ? (
        <p className="qos-rules-empty">
          No classification rules. Requests will be assigned the profile's default class. Add a rule to route traffic by header, model, tenant, and more.
        </p>
      ) : (
        <div className="qos-rules-list">
          {rows.map((rule, index) => {
            const source = rule.Source || 'Header';
            const operator = rule.Operator || 'Equals';
            const keyed = KEY_SOURCES.has(source);
            const valueDisabled = operator === 'Exists';
            return (
              <div className="qos-rule-row" key={index}>
                <div className="qos-rule-ordinal" title="Evaluation order">{index + 1}</div>

                <div className="qos-rule-fields">
                  <div className="form-group">
                    <label>Source</label>
                    <select value={source} onChange={(e) => updateRow(index, 'Source', e.target.value)}>
                      {SOURCE_OPTIONS.map((opt) => <option key={opt} value={opt}>{opt}</option>)}
                    </select>
                  </div>

                  <div className="form-group">
                    <label>Key</label>
                    <input
                      type="text"
                      value={rule.MatchKey || ''}
                      onChange={(e) => updateRow(index, 'MatchKey', e.target.value)}
                      placeholder={keyPlaceholder(source)}
                      disabled={!keyed}
                    />
                    <small>{keyed ? 'Header / query / JSON-path name.' : 'Unused for this source.'}</small>
                  </div>

                  <div className="form-group">
                    <label>Operator</label>
                    <select value={operator} onChange={(e) => updateRow(index, 'Operator', e.target.value)}>
                      {OPERATOR_OPTIONS.map((opt) => <option key={opt} value={opt}>{opt}</option>)}
                    </select>
                  </div>

                  <div className="form-group">
                    <label>Value</label>
                    <input
                      type="text"
                      value={rule.MatchValue || ''}
                      onChange={(e) => updateRow(index, 'MatchValue', e.target.value)}
                      placeholder={valueDisabled ? 'Not used for Exists' : 'Match value'}
                      disabled={valueDisabled}
                    />
                  </div>

                  <div className="form-group">
                    <label>Class</label>
                    <input
                      type="text"
                      value={rule.ClassName || ''}
                      onChange={(e) => updateRow(index, 'ClassName', e.target.value)}
                      placeholder="Class assigned on match"
                      list={classOptions.length > 0 ? datalistId : undefined}
                    />
                  </div>
                </div>

                <div className="qos-rule-actions">
                  <button
                    type="button"
                    className="btn-icon-small"
                    onClick={() => moveRow(index, -1)}
                    disabled={index === 0}
                    title="Move up"
                    aria-label={`Move rule ${index + 1} up`}
                  >
                    <UpIcon />
                  </button>
                  <button
                    type="button"
                    className="btn-icon-small"
                    onClick={() => moveRow(index, 1)}
                    disabled={index === rows.length - 1}
                    title="Move down"
                    aria-label={`Move rule ${index + 1} down`}
                  >
                    <DownIcon />
                  </button>
                  <button
                    type="button"
                    className="btn-icon-small qos-rule-delete"
                    onClick={() => deleteRow(index)}
                    title="Delete rule"
                    aria-label={`Delete rule ${index + 1}`}
                  >
                    <DeleteIcon />
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      <div className="qos-rules-footer">
        <button type="button" className="btn-secondary btn-small" onClick={addRow}>
          <PlusIcon /> Add rule
        </button>
      </div>
    </div>
  );
}

export default QosClassifierRulesEditor;
