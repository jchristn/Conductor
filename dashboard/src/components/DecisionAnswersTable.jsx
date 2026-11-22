import React from 'react';
import { DECISION_QUESTION_TYPES } from '../utils/typeSafeDecisions';

function formatValue(value, kind) {
  if (value === null || value === undefined) return '-';
  if (kind === 'score') return value.toLocaleString(undefined, { maximumFractionDigits: 3 });
  return `${(value * 100).toLocaleString(undefined, { maximumFractionDigits: 1 })}%`;
}

function typeLabel(type) {
  return DECISION_QUESTION_TYPES.find((entry) => entry.value === type)?.label || type;
}

/**
 * Table of TypeSafe System One answers: question, type, answer, probability or score, confidence.
 */
function DecisionAnswersTable({ answers }) {
  return (
    <div className="decision-answers">
      <table className="health-table decision-answers-table" aria-label="Decision answers">
        <thead>
          <tr>
            <th scope="col" title="Question id and instructions from the request">Question</th>
            <th scope="col" title="Question type sent in the request">Type</th>
            <th scope="col" title="Answer from the model. Binary answers read Yes when the probability is 50% or higher.">Answer</th>
            <th scope="col" title="Probability of the answer for binary and choice questions, or the raw score for score questions">Probability / Score</th>
            <th scope="col" title="Model-reported confidence, when the response includes one">Confidence</th>
          </tr>
        </thead>
        <tbody>
          {answers.map((row) => (
            <tr key={row.id}>
              <td>
                <strong>{row.id}</strong>
                {row.instructions && <div className="text-muted">{row.instructions}</div>}
              </td>
              <td>{typeLabel(row.type)}</td>
              <td>{row.answer}</td>
              <td>{formatValue(row.value, row.valueKind)}</td>
              <td>{formatValue(row.confidence, 'probability')}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default DecisionAnswersTable;
