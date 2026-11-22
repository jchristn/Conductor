import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useApiExplorer } from '../src/hooks/useApiExplorer';
import { createStreamLineParser, extractExplorerContent } from '../src/utils/streamParsers';

const anthropicVmr = { Id: 'vmr_claude', Name: 'Claude', BasePath: '/v1.0/api/claude/', ApiType: 'Anthropic' };
const typeSafeVmr = { Id: 'vmr_jev', Name: 'Jev', BasePath: '/v1.0/api/jev/', ApiType: 'TypeSafe' };

const ANTHROPIC_STREAM = [
  'event: message_start',
  'data: {"type":"message_start","message":{"id":"msg_1","usage":{"input_tokens":5}}}',
  '',
  'event: content_block_start',
  'data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}',
  '',
  'event: ping',
  'data: {"type":"ping"}',
  '',
  'event: content_block_delta',
  'data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}',
  '',
  'event: content_block_delta',
  'data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":" there"}}',
  '',
  'event: message_delta',
  'data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":2}}',
  '',
  'event: message_stop',
  'data: {"type":"message_stop"}',
  ''
].join('\n');

function streamResponse(text, chunkSize = 37) {
  const encoder = new TextEncoder();
  const bytes = encoder.encode(text);
  let offset = 0;
  const body = {
    getReader() {
      return {
        read: async () => {
          if (offset >= bytes.length) return { done: true, value: undefined };
          const value = bytes.slice(offset, offset + chunkSize);
          offset += chunkSize;
          return { done: false, value };
        },
        releaseLock: () => {}
      };
    }
  };
  return {
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: new Headers({ 'content-type': 'text/event-stream' }),
    body
  };
}

function jsonResponse(data) {
  const text = JSON.stringify(data);
  return {
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: new Headers({ 'content-type': 'application/json' }),
    text: async () => text
  };
}

describe('Anthropic SSE parsing', () => {
  it('extracts text from content_block_delta events and ignores the rest', () => {
    const parse = createStreamLineParser('anthropic-sse');
    const text = ANTHROPIC_STREAM.split('\n')
      .map((line) => parse(line))
      .filter(Boolean)
      .map((frame) => extractExplorerContent('Anthropic', 'messagesStream', frame.data, true, frame.event))
      .join('');
    expect(text).toBe('Hello there');
  });

  it('reads non-streaming content[].text', () => {
    expect(extractExplorerContent('Anthropic', 'messages', {
      content: [{ type: 'text', text: 'Hi' }, { type: 'tool_use', id: 't' }, { type: 'text', text: '!' }]
    }, false)).toBe('Hi!');
  });
});

describe('useApiExplorer', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('streams an Anthropic message with Bearer auth and anthropic-version', async () => {
    const fetchMock = vi.fn(() => Promise.resolve(streamResponse(ANTHROPIC_STREAM)));
    vi.stubGlobal('fetch', fetchMock);
    const { result } = renderHook(() => useApiExplorer('http://conductor.test'));

    act(() => { result.current.setSelectedVmr(anthropicVmr); });
    await waitFor(() => expect(result.current.apiType).toBe('Anthropic'));
    act(() => { result.current.setOperationId('messagesStream'); });
    await waitFor(() => expect(result.current.requestBody).toContain('"stream": true'));
    expect(result.current.isStreaming).toBe(true);

    await act(async () => { await result.current.sendRequest('cred_token'); });

    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toBe('http://conductor.test/v1.0/api/claude/v1/messages');
    expect(options.headers).toEqual({
      'Content-Type': 'application/json',
      Authorization: 'Bearer cred_token',
      'anthropic-version': '2023-06-01'
    });
    expect(result.current.responsePreview).toBe('Hello there');
    expect(result.current.responseBody).toBe(ANTHROPIC_STREAM);
    expect(result.current.responseStatus.totalStreamingTime).not.toBeNull();
  });

  it('cancels an Anthropic stream', async () => {
    let rejectRead;
    const fetchMock = vi.fn((url, options) => Promise.resolve({
      ok: true,
      status: 200,
      statusText: 'OK',
      headers: new Headers({ 'content-type': 'text/event-stream' }),
      body: {
        getReader() {
          return {
            read: () => new Promise((resolve, reject) => {
              rejectRead = reject;
              options.signal.addEventListener('abort', () => {
                const error = new Error('aborted');
                error.name = 'AbortError';
                reject(error);
              });
            }),
            releaseLock: () => {}
          };
        }
      }
    }));
    vi.stubGlobal('fetch', fetchMock);
    const { result } = renderHook(() => useApiExplorer('http://conductor.test'));
    act(() => { result.current.setSelectedVmr(anthropicVmr); });
    await waitFor(() => expect(result.current.apiType).toBe('Anthropic'));
    act(() => { result.current.setOperationId('messagesStream'); });
    await waitFor(() => expect(result.current.requestBody).toContain('"stream": true'));

    let pending;
    act(() => { pending = result.current.sendRequest('cred_token'); });
    await waitFor(() => expect(rejectRead).toBeTypeOf('function'));
    act(() => { result.current.stopRequest(); });
    await act(async () => { await pending; });

    expect(result.current.responseStatus.isCancelled).toBe(true);
    expect(result.current.isSending).toBe(false);
  });

  it('sends a TypeSafe decision and exposes the answers', async () => {
    const fetchMock = vi.fn(() => Promise.resolve(jsonResponse({
      model: 'jev-latest',
      answers: {
        is_question: { noul: 0.91, confidence: 0.8 },
        category: { choice: 'billing', probabilities: { billing: 0.75, technical: 0.25 } },
        urgency: { score: 0.2 }
      },
      usage: { input_tokens: 30, output_tokens: 3 }
    })));
    vi.stubGlobal('fetch', fetchMock);
    const { result } = renderHook(() => useApiExplorer('http://conductor.test'));

    act(() => { result.current.setSelectedVmr(typeSafeVmr); });
    await waitFor(() => expect(result.current.operationId).toBe('decide'));
    await waitFor(() => expect(result.current.requestBody).toContain('"questions"'));
    expect(result.current.isStreaming).toBe(false);

    await act(async () => { await result.current.sendRequest('cred_token'); });

    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toBe('http://conductor.test/v1.0/api/jev/v1/systemone');
    expect(options.method).toBe('POST');
    expect(options.headers).toEqual({ 'Content-Type': 'application/json', Authorization: 'Bearer cred_token' });
    expect(JSON.parse(options.body).questions.is_question.type).toBe('noul');
    expect(result.current.decisionAnswers).toEqual([
      expect.objectContaining({ id: 'is_question', answer: 'Yes', value: 0.91, confidence: 0.8 }),
      expect.objectContaining({ id: 'category', answer: 'billing', value: 0.75 }),
      expect.objectContaining({ id: 'urgency', answer: 'not urgent', value: 0.2 })
    ]);
  });

  it('falls back to OpenAI instead of guessing when a VMR reports an endpoint-only type', async () => {
    const { result } = renderHook(() => useApiExplorer('http://conductor.test'));
    act(() => { result.current.setSelectedVmr({ ...anthropicVmr, ApiType: 'OllamaCloud' }); });
    await waitFor(() => expect(result.current.apiType).toBe('OpenAI'));
  });
});
