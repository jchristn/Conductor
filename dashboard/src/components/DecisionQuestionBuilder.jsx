import React, { useEffect, useRef, useState } from 'react';
import {
  DECISION_QUESTION_TYPES,
  buildDecisionBody,
  createDecisionQuestion,
  parseDecisionBody
} from '../utils/typeSafeDecisions';

function nextQuestionId(questions, type) {
  const base = type === 'noul' ? 'binary' : type;
  let index = 1;
  const ids = new Set(questions.map((question) => question.id));
  while (ids.has(`${base}_${index}`)) index += 1;
  return `${base}_${index}`;
}

/**
 * Builder for TypeSafe System One decision requests. Edits the request body JSON in place:
 * every change regenerates the body, and edits to the JSON are read back into the builder.
 */
function DecisionQuestionBuilder({ requestBody, onChange, disabled = false }) {
  const lastWrittenRef = useRef(null);
  const [draft, setDraft] = useState(() => parseDecisionBody(requestBody));

  useEffect(() => {
    if (requestBody !== lastWrittenRef.current) {
      setDraft(parseDecisionBody(requestBody));
    }
  }, [requestBody]);

  if (!draft) {
    return (
      <div className="decision-builder">
        <p className="form-help">The request body is not valid decision JSON. Fix it or reset to the template to use the question builder.</p>
      </div>
    );
  }

  const commit = (next) => {
    setDraft(next);
    const text = JSON.stringify(buildDecisionBody(next), null, 2);
    lastWrittenRef.current = text;
    onChange(text);
  };

  const updateQuestion = (index, changes) => {
    commit({
      ...draft,
      questions: draft.questions.map((question, i) => (i === index ? { ...question, ...changes } : question))
    });
  };

  const addQuestion = (type) => {
    commit({ ...draft, questions: [...draft.questions, createDecisionQuestion(type, nextQuestionId(draft.questions, type))] });
  };

  const removeQuestion = (index) => {
    commit({ ...draft, questions: draft.questions.filter((_, i) => i !== index) });
  };

  const ids = draft.questions.map((question) => question.id.trim()).filter(Boolean);
  const duplicateIds = ids.filter((id, index) => ids.indexOf(id) !== index);

  return (
    <div className="decision-builder" aria-label="Decision question builder">
      <div className="form-group">
        <label htmlFor="decision-state" title="The content the model decides about, such as a message, document or record. Sent as the state field.">State</label>
        <textarea
          id="decision-state"
          className="code-input"
          rows={3}
          value={draft.state}
          disabled={disabled}
          onChange={(e) => commit({ ...draft, state: e.target.value })}
        />
      </div>

      {draft.questions.length === 0 && (
        <p className="form-help">Add at least one question. A decision request with no questions is rejected with 400.</p>
      )}

      {draft.questions.map((question, index) => (
        <div className="decision-question" key={`question-${index}`}>
          <div className="decision-question-row">
            <input
              type="text"
              aria-label={`Question ${index + 1} id`}
              title="Question id. Answers are keyed by this id."
              value={question.id}
              disabled={disabled}
              onChange={(e) => updateQuestion(index, { id: e.target.value })}
              placeholder="question_id"
            />
            <select
              aria-label={`Question ${index + 1} type`}
              value={question.type}
              disabled={disabled}
              onChange={(e) => updateQuestion(index, { ...createDecisionQuestion(e.target.value, question.id), instructions: question.instructions })}
            >
              {DECISION_QUESTION_TYPES.map((type) => (
                <option key={type.value} value={type.value}>{type.label}</option>
              ))}
            </select>
            <button
              type="button"
              className="btn-secondary btn-small"
              onClick={() => removeQuestion(index)}
              disabled={disabled}
              aria-label={`Remove question ${index + 1}`}
              title="Remove this question"
            >
              Remove
            </button>
          </div>
          <input
            type="text"
            aria-label={`Question ${index + 1} instructions`}
            value={question.instructions}
            disabled={disabled}
            onChange={(e) => updateQuestion(index, { instructions: e.target.value })}
            placeholder="Instructions for the model"
          />
          {question.type === 'noul' && (
            <div className="decision-question-row">
              <input
                type="text"
                aria-label={`Question ${index + 1} true criterion`}
                title="Optional. What a true answer means."
                value={question.trueCriterion}
                disabled={disabled}
                onChange={(e) => updateQuestion(index, { trueCriterion: e.target.value })}
                placeholder="True means... (optional)"
              />
              <input
                type="text"
                aria-label={`Question ${index + 1} false criterion`}
                title="Optional. What a false answer means."
                value={question.falseCriterion}
                disabled={disabled}
                onChange={(e) => updateQuestion(index, { falseCriterion: e.target.value })}
                placeholder="False means... (optional)"
              />
            </div>
          )}
          {question.type === 'choice' && (
            <div className="decision-options">
              {question.options.map((option, optionIndex) => (
                <div className="decision-question-row" key={`option-${optionIndex}`}>
                  <input
                    type="text"
                    aria-label={`Question ${index + 1} option ${optionIndex + 1} value`}
                    value={option.value}
                    disabled={disabled}
                    onChange={(e) => updateQuestion(index, {
                      options: question.options.map((item, i) => (i === optionIndex ? { ...item, value: e.target.value } : item))
                    })}
                    placeholder="option"
                  />
                  <input
                    type="text"
                    aria-label={`Question ${index + 1} option ${optionIndex + 1} description`}
                    value={option.description}
                    disabled={disabled}
                    onChange={(e) => updateQuestion(index, {
                      options: question.options.map((item, i) => (i === optionIndex ? { ...item, description: e.target.value } : item))
                    })}
                    placeholder="description (optional)"
                  />
                  <button
                    type="button"
                    className="btn-secondary btn-small"
                    disabled={disabled}
                    aria-label={`Remove option ${optionIndex + 1} of question ${index + 1}`}
                    onClick={() => updateQuestion(index, { options: question.options.filter((_, i) => i !== optionIndex) })}
                  >
                    Remove
                  </button>
                </div>
              ))}
              <button
                type="button"
                className="btn-secondary btn-small"
                disabled={disabled}
                onClick={() => updateQuestion(index, { options: [...question.options, { value: '', description: '' }] })}
              >
                + Option
              </button>
            </div>
          )}
          {question.type === 'score' && (
            <textarea
              className="code-input"
              rows={3}
              aria-label={`Question ${index + 1} levels`}
              title="One level per line, lowest first. The score maps to these levels."
              value={question.levels.join('\n')}
              disabled={disabled}
              onChange={(e) => updateQuestion(index, { levels: e.target.value.split('\n') })}
              placeholder={'low\nmedium\nhigh'}
            />
          )}
        </div>
      ))}

      {duplicateIds.length > 0 && (
        <p className="error-text">Question ids must be unique: {Array.from(new Set(duplicateIds)).join(', ')}.</p>
      )}

      <div className="decision-builder-actions">
        <button type="button" className="btn-secondary btn-small" disabled={disabled} onClick={() => addQuestion('noul')}>+ Binary</button>
        <button type="button" className="btn-secondary btn-small" disabled={disabled} onClick={() => addQuestion('choice')}>+ Choice</button>
        <button type="button" className="btn-secondary btn-small" disabled={disabled} onClick={() => addQuestion('score')}>+ Score</button>
      </div>
    </div>
  );
}

export default DecisionQuestionBuilder;
