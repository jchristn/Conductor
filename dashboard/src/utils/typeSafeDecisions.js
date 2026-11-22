/**
 * TypeSafe System One helpers: build decision request bodies from a question list, read them
 * back for the question builder, and turn a decision response into table rows.
 *
 * Wire format (POST /v1/systemone):
 *   { "model": "...", "state": "...", "questions": { "<id>": { "type": "noul" | "choice" | "score",
 *     "instructions": "...", "criteria": ... } } }
 * - noul (binary): optional criteria { "true": "...", "false": "..." }; answer { "noul": probability }
 * - choice: criteria { "<option>": "<description or null>" }; answer { "choice": "<option>", "probabilities": {...} }
 * - score: criteria [ "<level 0>", "<level 1>", ... ]; answer { "score": number }
 * Every answer may include "confidence".
 */
import { DEFAULT_TYPESAFE_MODEL } from './providers';

/** Question types offered by the builder, with display labels. */
export const DECISION_QUESTION_TYPES = [
  { value: 'noul', label: 'Binary (noul)' },
  { value: 'choice', label: 'Choice (options)' },
  { value: 'score', label: 'Score (levels)' }
];

/**
 * Create a new builder question.
 * @param {'noul'|'choice'|'score'} type
 * @param {string} id
 * @returns {Object}
 */
export function createDecisionQuestion(type, id) {
  const base = { id, type, instructions: '', trueCriterion: '', falseCriterion: '', options: [], levels: [] };
  if (type === 'choice') {
    return {
      ...base,
      instructions: 'Which category best describes the message?',
      options: [{ value: 'billing', description: '' }, { value: 'technical', description: '' }]
    };
  }
  if (type === 'score') {
    return { ...base, instructions: 'How urgent is the message?', levels: ['not urgent', 'somewhat urgent', 'very urgent'] };
  }
  return { ...base, type: 'noul', instructions: 'Is the message a question?' };
}

/**
 * Convert builder questions to the System One request body.
 * @param {Object} input
 * @param {string} input.model
 * @param {string} input.state
 * @param {Array<Object>} input.questions
 * @returns {Object}
 */
export function buildDecisionBody({ model, state, questions }) {
  const wireQuestions = {};
  (questions || []).forEach((question) => {
    const id = String(question.id || '').trim();
    if (!id) return;
    const wire = { type: question.type, instructions: question.instructions || '' };
    if (question.type === 'noul') {
      const criteria = {};
      if (question.trueCriterion) criteria.true = question.trueCriterion;
      if (question.falseCriterion) criteria.false = question.falseCriterion;
      if (Object.keys(criteria).length > 0) wire.criteria = criteria;
    } else if (question.type === 'choice') {
      const criteria = {};
      (question.options || []).forEach((option) => {
        const value = String(option.value || '').trim();
        if (value) criteria[value] = option.description ? option.description : null;
      });
      wire.criteria = criteria;
    } else if (question.type === 'score') {
      wire.criteria = (question.levels || []).map((level) => String(level)).filter((level) => level.trim() !== '');
    }
    wireQuestions[id] = wire;
  });

  return {
    model: model || DEFAULT_TYPESAFE_MODEL,
    state: state || '',
    questions: wireQuestions
  };
}

/**
 * Read a System One request body back into builder form.
 * @param {string|Object} body - JSON text or parsed object
 * @returns {{model: string, state: string, questions: Array<Object>}|null} Null when the body is not a decision request.
 */
export function parseDecisionBody(body) {
  let parsed = body;
  if (typeof body === 'string') {
    try {
      parsed = JSON.parse(body);
    } catch {
      return null;
    }
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return null;
  const questionsObj = parsed.questions && typeof parsed.questions === 'object' && !Array.isArray(parsed.questions)
    ? parsed.questions
    : {};

  const questions = Object.entries(questionsObj).map(([id, wire]) => {
    const type = ['noul', 'choice', 'score'].includes(wire?.type) ? wire.type : 'noul';
    const question = { id, type, instructions: wire?.instructions || '', trueCriterion: '', falseCriterion: '', options: [], levels: [] };
    const criteria = wire?.criteria;
    if (type === 'noul' && criteria && typeof criteria === 'object') {
      question.trueCriterion = criteria.true || '';
      question.falseCriterion = criteria.false || '';
    } else if (type === 'choice' && criteria && typeof criteria === 'object' && !Array.isArray(criteria)) {
      question.options = Object.entries(criteria).map(([value, description]) => ({ value, description: description || '' }));
    } else if (type === 'score' && Array.isArray(criteria)) {
      question.levels = criteria.map((level) => String(level));
    }
    return question;
  });

  return {
    model: typeof parsed.model === 'string' ? parsed.model : '',
    state: typeof parsed.state === 'string' ? parsed.state : '',
    questions
  };
}

function toNumber(value) {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string' && value.trim() !== '' && Number.isFinite(Number(value))) return Number(value);
  return null;
}

/**
 * Turn a System One response into answer rows for display.
 * @param {Object|string} response - Parsed response or JSON text
 * @param {Object|string|null} request - The request body, used for question types, instructions and score levels
 * @returns {Array<{id: string, type: string, instructions: string, answer: string, value: number|null, valueKind: string, confidence: number|null}>}
 */
export function extractDecisionAnswers(response, request) {
  let data = response;
  if (typeof response === 'string') {
    try {
      data = JSON.parse(response);
    } catch {
      return [];
    }
  }
  const answers = data && typeof data === 'object' ? data.answers : null;
  if (!answers || typeof answers !== 'object' || Array.isArray(answers)) return [];

  const requestQuestions = parseDecisionBody(request || '')?.questions || [];
  const byId = Object.fromEntries(requestQuestions.map((question) => [question.id, question]));

  return Object.entries(answers).map(([id, answer]) => {
    const question = byId[id];
    const source = answer && typeof answer === 'object' ? answer : {};
    let type = question?.type;
    if (!type) {
      if ('noul' in source) type = 'noul';
      else if ('choice' in source) type = 'choice';
      else if ('score' in source) type = 'score';
      else type = 'unknown';
    }

    let answerText = '-';
    let value = null;
    let valueKind = 'probability';
    if (type === 'noul') {
      value = toNumber(source.noul);
      answerText = value === null ? '-' : (value >= 0.5 ? 'Yes' : 'No');
    } else if (type === 'choice') {
      answerText = source.choice !== undefined && source.choice !== null ? String(source.choice) : '-';
      value = toNumber(source.probabilities?.[source.choice]);
    } else if (type === 'score') {
      valueKind = 'score';
      value = toNumber(source.score);
      const levels = question?.levels || [];
      if (value !== null && levels.length > 0) {
        const level = Math.min(Math.max(Math.round(value), 0), levels.length - 1);
        answerText = levels[level];
      } else {
        answerText = value === null ? '-' : String(value);
      }
    }

    return {
      id,
      type,
      instructions: question?.instructions || '',
      answer: answerText,
      value,
      valueKind,
      confidence: toNumber(source.confidence)
    };
  });
}
