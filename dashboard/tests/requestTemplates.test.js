import { describe, it, expect } from 'vitest';
import {
  generateRequestBody,
  getOperation,
  getOperationsForApiType,
  isOperationStreaming,
  resolveOperationPath
} from '../src/utils/requestTemplates';
import { buildDecisionBody, createDecisionQuestion, extractDecisionAnswers, parseDecisionBody } from '../src/utils/typeSafeDecisions';

describe('request templates', () => {
  it('maps every client-facing type to its operation set from the registry', () => {
    expect(getOperationsForApiType('OpenAI').map((op) => op.id)).toContain('chatCompletions');
    expect(getOperationsForApiType('vLLM').map((op) => op.id)).toContain('chatCompletions');
    expect(getOperationsForApiType('Ollama').map((op) => op.id)).toContain('chat');
    expect(getOperationsForApiType('Gemini').map((op) => op.id)).toContain('generateContent');
    expect(getOperationsForApiType('Anthropic').map((op) => op.id)).toEqual(['messages', 'messagesStream', 'countTokens', 'listModels', 'getModel']);
    expect(getOperationsForApiType('TypeSafe').map((op) => op.id)).toEqual(['decide', 'listModels']);
  });

  it('defines the Anthropic operations with the right paths and bodies', () => {
    expect(getOperation('Anthropic', 'messages')).toMatchObject({ endpoint: '/v1/messages', method: 'POST', supportsStreaming: false });
    expect(getOperation('Anthropic', 'messagesStream')).toMatchObject({ endpoint: '/v1/messages', method: 'POST', supportsStreaming: true, forceStream: true });
    expect(getOperation('Anthropic', 'countTokens')).toMatchObject({ endpoint: '/v1/messages/count_tokens', method: 'POST' });
    expect(getOperation('Anthropic', 'listModels')).toMatchObject({ endpoint: '/v1/models', method: 'GET' });
    expect(getOperation('Anthropic', 'getModel')).toMatchObject({ endpoint: '/v1/models/{model}', method: 'GET' });

    const messages = JSON.parse(generateRequestBody('Anthropic', 'messages', 'claude-test', false));
    expect(messages).toEqual({ model: 'claude-test', max_tokens: 1024, messages: [{ role: 'user', content: 'Hello!' }] });
    const stream = JSON.parse(generateRequestBody('Anthropic', 'messagesStream', 'claude-test', false));
    expect(stream.stream).toBe(true);
    expect(stream.max_tokens).toBe(1024);
    const count = JSON.parse(generateRequestBody('Anthropic', 'countTokens', 'claude-test', false));
    expect(count).not.toHaveProperty('max_tokens');
    expect(generateRequestBody('Anthropic', 'listModels', 'x', false)).toBe('');
  });

  it('streams forceStream operations regardless of the toggle', () => {
    expect(isOperationStreaming(getOperation('Anthropic', 'messagesStream'), false)).toBe(true);
    expect(isOperationStreaming(getOperation('Anthropic', 'messages'), true)).toBe(false);
    expect(isOperationStreaming(getOperation('OpenAI', 'chatCompletions'), true)).toBe(true);
  });

  it('resolves {model} paths and the Gemini stream path', () => {
    expect(resolveOperationPath('Anthropic', getOperation('Anthropic', 'getModel'), 'claude-x', false)).toBe('/v1/models/claude-x');
    expect(resolveOperationPath('Gemini', getOperation('Gemini', 'generateContent'), '', true)).toBe('/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse');
  });

  it('defines the TypeSafe Decide operation with a System One body', () => {
    expect(getOperation('TypeSafe', 'decide')).toMatchObject({ endpoint: '/v1/systemone', method: 'POST', supportsStreaming: false, decision: true });
    const body = JSON.parse(generateRequestBody('TypeSafe', 'decide', '', false));
    expect(body.model).toBe('jev-latest');
    expect(typeof body.state).toBe('string');
    expect(body.questions.is_question).toMatchObject({ type: 'noul' });
    expect(body.questions.category.type).toBe('choice');
    expect(Object.keys(body.questions.category.criteria)).toEqual(['billing', 'technical']);
    expect(body.questions.urgency.type).toBe('score');
    expect(Array.isArray(body.questions.urgency.criteria)).toBe(true);
  });
});

describe('TypeSafe decision helpers', () => {
  it('builds binary, choice and score questions and reads them back', () => {
    const binary = { ...createDecisionQuestion('noul', 'q1'), trueCriterion: 'yes it is' };
    const choice = createDecisionQuestion('choice', 'q2');
    const score = createDecisionQuestion('score', 'q3');
    const body = buildDecisionBody({ model: 'jev-latest', state: 'hello', questions: [binary, choice, score] });
    expect(body.questions.q1).toEqual({ type: 'noul', instructions: binary.instructions, criteria: { true: 'yes it is' } });
    expect(body.questions.q2.criteria).toEqual({ billing: null, technical: null });
    expect(body.questions.q3.criteria).toEqual(score.levels);

    const parsed = parseDecisionBody(JSON.stringify(body));
    expect(parsed.state).toBe('hello');
    expect(parsed.questions.map((q) => q.type)).toEqual(['noul', 'choice', 'score']);
    expect(parsed.questions[1].options.map((o) => o.value)).toEqual(['billing', 'technical']);
    expect(parseDecisionBody('not json')).toBeNull();
  });

  it('turns a decision response into answer rows', () => {
    const request = buildDecisionBody({
      model: 'jev-latest',
      state: 'hi',
      questions: [createDecisionQuestion('noul', 'q1'), createDecisionQuestion('choice', 'q2'), createDecisionQuestion('score', 'q3')]
    });
    const rows = extractDecisionAnswers({
      model: 'jev-latest',
      answers: {
        q1: { noul: 0.82, confidence: 0.9 },
        q2: { choice: 'billing', probabilities: { billing: 0.7, technical: 0.3 } },
        q3: { score: 1.6 }
      }
    }, request);
    expect(rows).toEqual([
      expect.objectContaining({ id: 'q1', type: 'noul', answer: 'Yes', value: 0.82, confidence: 0.9 }),
      expect.objectContaining({ id: 'q2', type: 'choice', answer: 'billing', value: 0.7, confidence: null }),
      expect.objectContaining({ id: 'q3', type: 'score', answer: 'very urgent', value: 1.6, valueKind: 'score' })
    ]);
    expect(extractDecisionAnswers({ error: 'x' }, request)).toEqual([]);
  });
});
