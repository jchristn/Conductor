/**
 * Streaming and response-content helpers for the API Explorer. The stream format of a VMR type
 * comes from the provider registry, never from the response itself.
 */
import { getProvider, getStreamFormat } from './providers';

/**
 * Create a stateful line parser for a stream format.
 * - openai-sse, gemini-sse: "data: {json}" frames, "[DONE]" ignored.
 * - anthropic-sse: "event: <name>" lines followed by "data: {json}"; the event name is kept.
 * - ndjson: one JSON object per line.
 * - none: nothing is parsed.
 * @param {string} streamFormat
 * @returns {(line: string) => ({event: string|null, data: Object}|null)}
 */
export function createStreamLineParser(streamFormat) {
  let currentEvent = null;

  return (rawLine) => {
    const line = (rawLine || '').replace(/\r$/, '');
    if (!line.trim()) {
      currentEvent = null;
      return null;
    }

    if (streamFormat === 'ndjson') {
      try {
        return { event: null, data: JSON.parse(line) };
      } catch {
        return null;
      }
    }

    if (streamFormat === 'openai-sse' || streamFormat === 'gemini-sse' || streamFormat === 'anthropic-sse') {
      if (line.startsWith('event:')) {
        currentEvent = line.slice(6).trim();
        return null;
      }
      if (!line.startsWith('data:')) return null;
      const payload = line.slice(5).trim();
      if (!payload || payload === '[DONE]') return null;
      try {
        const data = JSON.parse(payload);
        const event = streamFormat === 'anthropic-sse' ? (currentEvent || data?.type || null) : null;
        return { event, data };
      } catch {
        return null;
      }
    }

    return null;
  };
}

function joinGeminiText(data) {
  const parts = data?.candidates?.[0]?.content?.parts;
  if (!Array.isArray(parts)) return '';
  return parts.map((part) => part?.text || '').join('');
}

function joinAnthropicText(data) {
  if (!Array.isArray(data?.content)) return '';
  return data.content
    .filter((block) => block && block.type === 'text')
    .map((block) => block.text || '')
    .join('');
}

/**
 * Text content of one streamed Anthropic event, or an empty string for events that carry none.
 * @param {string|null} event - SSE event name
 * @param {Object} data - parsed data payload
 * @returns {string}
 */
export function extractAnthropicStreamText(event, data) {
  const type = data?.type || event;
  if (type === 'content_block_delta' && data?.delta?.type !== 'input_json_delta') {
    return data?.delta?.text || '';
  }
  if (type === 'error') {
    return `\n[${data?.error?.type || 'error'}] ${data?.error?.message || 'Stream error'}`;
  }
  return '';
}

/**
 * Extract displayable content from a response (or one stream frame) for the API Explorer preview.
 * @param {string} apiType - VMR API type
 * @param {string} operationId - explorer operation id
 * @param {Object} data - parsed JSON
 * @param {boolean} isStreaming - whether data is a stream frame
 * @param {string|null} [event] - SSE event name for Anthropic frames
 * @returns {string}
 */
export function extractExplorerContent(apiType, operationId, data, isStreaming, event = null) {
  const streamFormat = getStreamFormat(apiType);
  const family = getProvider(apiType)?.explorerOperations;

  try {
    if (family === 'openai') {
      if (operationId === 'chatCompletions') {
        return isStreaming ? (data.choices?.[0]?.delta?.content || '') : (data.choices?.[0]?.message?.content || '');
      }
      if (operationId === 'completions') {
        return data.choices?.[0]?.text || '';
      }
    } else if (streamFormat === 'gemini-sse') {
      if (operationId === 'generateContent' || operationId === 'streamGenerateContent') {
        return joinGeminiText(data);
      }
    } else if (family === 'ollama') {
      if (operationId === 'chat') return data.message?.content || '';
      if (operationId === 'generate') return data.response || '';
      if (operationId === 'pull') return data.status || '';
    } else if (family === 'anthropic') {
      if (operationId === 'messages' || operationId === 'messagesStream') {
        if (isStreaming) return extractAnthropicStreamText(event, data);
        const text = joinAnthropicText(data);
        if (text) return text;
      }
    }
    return JSON.stringify(data, null, 2);
  } catch {
    return '';
  }
}
