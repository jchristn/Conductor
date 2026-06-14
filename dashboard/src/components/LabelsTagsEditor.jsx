import React from 'react';

const emptyTagRow = () => ({ key: '', value: '' });

export function labelsFromValue(value) {
  if (!Array.isArray(value) || value.length === 0) {
    return [''];
  }

  return value.map((label) => label == null ? '' : String(label));
}

export function tagsFromValue(value) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    return [emptyTagRow()];
  }

  const rows = Object.entries(value).map(([key, tagValue]) => ({
    key,
    value: tagValue == null ? '' : String(tagValue)
  }));

  return rows.length > 0 ? rows : [emptyTagRow()];
}

export function labelsToPayload(labels) {
  if (!Array.isArray(labels)) {
    return [];
  }

  return labels
    .map((label) => label == null ? '' : String(label).trim())
    .filter(Boolean);
}

export function tagsToPayload(tags) {
  if (!Array.isArray(tags)) {
    return {};
  }

  return tags.reduce((result, row) => {
    const key = row?.key == null ? '' : String(row.key).trim();
    if (!key) {
      return result;
    }

    result[key] = row?.value == null ? '' : String(row.value);
    return result;
  }, {});
}

function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
      <path fillRule="evenodd" d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z" clipRule="evenodd" />
    </svg>
  );
}

function DeleteIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
      <path fillRule="evenodd" d="M8 4a1 1 0 011-1h2a1 1 0 011 1h4a1 1 0 110 2h-1v10a2 2 0 01-2 2H7a2 2 0 01-2-2V6H4a1 1 0 010-2h4zm-1 2v10h6V6H7zm2 2a1 1 0 011 1v4a1 1 0 11-2 0V9a1 1 0 011-1zm3 0a1 1 0 011 1v4a1 1 0 11-2 0V9a1 1 0 011-1z" clipRule="evenodd" />
    </svg>
  );
}

function normalizedLabels(labels) {
  return Array.isArray(labels) && labels.length > 0 ? labels : [''];
}

function normalizedTags(tags) {
  return Array.isArray(tags) && tags.length > 0 ? tags : [emptyTagRow()];
}

function LabelsEditor({ labels, onChange, idPrefix }) {
  const rows = normalizedLabels(labels);

  const updateLabel = (index, value) => {
    const next = rows.map((label, currentIndex) => currentIndex === index ? value : label);
    onChange(next);
  };

  const deleteLabel = (index) => {
    const next = rows.filter((_, currentIndex) => currentIndex !== index);
    onChange(next.length > 0 ? next : ['']);
  };

  const addLabel = () => {
    onChange([...rows, '']);
  };

  return (
    <div className="form-group labels-tags-section">
      <label htmlFor={`${idPrefix}-label-0`} title="String labels for categorization and filtering">Labels</label>
      <div className="dynamic-field-list">
        {rows.map((label, index) => (
          <div className="dynamic-field-row label-field-row" key={`${idPrefix}-label-${index}`}>
            <input
              type="text"
              id={`${idPrefix}-label-${index}`}
              value={label}
              onChange={(e) => updateLabel(index, e.target.value)}
              placeholder="Label"
              aria-label={`Label ${index + 1}`}
            />
            <button
              type="button"
              className="dynamic-field-button dynamic-field-delete"
              onClick={() => deleteLabel(index)}
              title="Delete label"
              aria-label={`Delete label ${index + 1}`}
            >
              <DeleteIcon />
            </button>
            {index === rows.length - 1 && (
              <button
                type="button"
                className="dynamic-field-button"
                onClick={addLabel}
                title="Add label"
                aria-label="Add label"
              >
                <PlusIcon />
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

function TagsEditor({ tags, onChange, idPrefix }) {
  const rows = normalizedTags(tags);

  const updateTag = (index, field, value) => {
    const next = rows.map((row, currentIndex) => (
      currentIndex === index ? { ...row, [field]: value } : row
    ));
    onChange(next);
  };

  const deleteTag = (index) => {
    const next = rows.filter((_, currentIndex) => currentIndex !== index);
    onChange(next.length > 0 ? next : [emptyTagRow()]);
  };

  const addTag = () => {
    onChange([...rows, emptyTagRow()]);
  };

  return (
    <div className="form-group labels-tags-section">
      <label htmlFor={`${idPrefix}-tag-key-0`} title="Key-value tags for custom metadata">Tags</label>
      <div className="dynamic-field-list">
        {rows.map((tag, index) => (
          <div className="dynamic-field-row tag-field-row" key={`${idPrefix}-tag-${index}`}>
            <input
              type="text"
              id={`${idPrefix}-tag-key-${index}`}
              value={tag.key}
              onChange={(e) => updateTag(index, 'key', e.target.value)}
              placeholder="Key"
              aria-label={`Tag ${index + 1} key`}
            />
            <input
              type="text"
              id={`${idPrefix}-tag-value-${index}`}
              value={tag.value}
              onChange={(e) => updateTag(index, 'value', e.target.value)}
              placeholder="Value"
              aria-label={`Tag ${index + 1} value`}
            />
            <button
              type="button"
              className="dynamic-field-button dynamic-field-delete"
              onClick={() => deleteTag(index)}
              title="Delete tag"
              aria-label={`Delete tag ${index + 1}`}
            >
              <DeleteIcon />
            </button>
            {index === rows.length - 1 && (
              <button
                type="button"
                className="dynamic-field-button"
                onClick={addTag}
                title="Add tag"
                aria-label="Add tag"
              >
                <PlusIcon />
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

function LabelsTagsEditor({ labels, tags, onLabelsChange, onTagsChange, idPrefix = 'labels-tags' }) {
  return (
    <div className="labels-tags-editor">
      <LabelsEditor labels={labels} onChange={onLabelsChange} idPrefix={idPrefix} />
      <TagsEditor tags={tags} onChange={onTagsChange} idPrefix={idPrefix} />
    </div>
  );
}

export default LabelsTagsEditor;
