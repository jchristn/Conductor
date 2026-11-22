import { describe, it, expect } from 'vitest';
import { generateCode, getAvailableLanguages, toPythonLiteral } from '../src/utils/codeGenerators';
import { buildExplorerHeaders } from '../src/utils/providers';
import { generateRequestBody } from '../src/utils/requestTemplates';

const anthropicUrl = 'http://conductor.test/v1.0/api/claude/v1/messages';
const anthropicBody = generateRequestBody('Anthropic', 'messages', 'claude-test', false);
const anthropicConfig = {
  url: anthropicUrl,
  method: 'POST',
  headers: buildExplorerHeaders('Anthropic', 'cred_token'),
  body: anthropicBody,
  apiType: 'Anthropic',
  operationId: 'messages',
  streaming: false
};

describe('code generators', () => {
  it('includes Authorization: Bearer and anthropic-version in every raw Anthropic snippet', () => {
    ['curl', 'javascript', 'python', 'csharp'].forEach((language) => {
      const code = generateCode(language, anthropicConfig);
      expect(code, language).toContain('Bearer cred_token');
      expect(code, language).toContain('anthropic-version');
      expect(code, language).toContain('2023-06-01');
      expect(code, language).toContain(anthropicUrl);
      expect(code, language).toContain('max_tokens');
    });
    const curl = generateCode('curl', anthropicConfig);
    expect(curl).toContain('-H "Authorization: Bearer cred_token"');
    expect(curl).toContain('-H "anthropic-version: 2023-06-01"');
  });

  it('sends the exact JSON body from C#', () => {
    const csharp = generateCode('csharp', anthropicConfig);
    expect(csharp).toContain('request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");');
    expect(csharp).toContain('""max_tokens"": 1024');
    expect(csharp).toContain('new StringContent(json, Encoding.UTF8, "application/json")');
  });

  it('streams Anthropic events in the JavaScript streaming snippet', () => {
    const code = generateCode('javascript-stream', { ...anthropicConfig, operationId: 'messagesStream', streaming: true });
    expect(code).toContain('content_block_delta');
    expect(code).toContain('parsed.delta.text');
    expect(code).toContain('"anthropic-version": "2023-06-01"');
  });

  it('offers Anthropic SDK snippets only for Anthropic VMRs, gated like the OpenAI SDK', () => {
    const anthropicLanguages = getAvailableLanguages('Anthropic', false).map((lang) => lang.id);
    expect(anthropicLanguages).toContain('python-anthropic');
    expect(anthropicLanguages).toContain('typescript-anthropic');
    expect(anthropicLanguages).not.toContain('python-openai');
    const openaiLanguages = getAvailableLanguages('OpenAI', false).map((lang) => lang.id);
    expect(openaiLanguages).toContain('python-openai');
    expect(openaiLanguages).not.toContain('python-anthropic');
    expect(getAvailableLanguages('TypeSafe', false).map((lang) => lang.id)).toEqual(['curl', 'javascript', 'python', 'csharp']);
  });

  it('builds Anthropic SDK snippets with base_url and auth_token', () => {
    const python = generateCode('python-anthropic', anthropicConfig);
    expect(python).toContain('import anthropic');
    expect(python).toContain('base_url="http://conductor.test/v1.0/api/claude"');
    expect(python).toContain('auth_token="cred_token"');
    expect(python).toContain('client.messages.create(');
    expect(python).toContain('max_tokens=1024');

    const ts = generateCode('typescript-anthropic', anthropicConfig);
    expect(ts).toContain("import Anthropic from '@anthropic-ai/sdk';");
    expect(ts).toContain('baseURL: "http://conductor.test/v1.0/api/claude"');
    expect(ts).toContain('authToken: "cred_token"');

    const streamPy = generateCode('python-anthropic', {
      ...anthropicConfig,
      operationId: 'messagesStream',
      body: generateRequestBody('Anthropic', 'messagesStream', 'claude-test', true),
      streaming: true
    });
    expect(streamPy).toContain('client.messages.stream(');
    expect(streamPy).not.toContain('stream=True');
  });

  it('generates TypeSafe decide snippets with the System One body', () => {
    const body = generateRequestBody('TypeSafe', 'decide', 'jev-latest', false);
    const config = {
      url: 'http://conductor.test/v1.0/api/jev/v1/systemone',
      method: 'POST',
      headers: buildExplorerHeaders('TypeSafe', 'cred_token'),
      body,
      apiType: 'TypeSafe',
      operationId: 'decide',
      streaming: false
    };
    const curl = generateCode('curl', config);
    expect(curl).toContain('/v1/systemone');
    expect(curl).toContain('"questions"');
    expect(curl).not.toContain('anthropic-version');
    const python = generateCode('python', config);
    expect(python).toContain('"type": "noul"');
    expect(python).toContain("print(response.json()['answers'])");
    expect(generateCode('javascript', config)).toContain('console.log(data.answers);');
  });

  it('converts JSON to Python literals without corrupting string content', () => {
    expect(toPythonLiteral({ a: true, b: null, c: 'it is true', 'true': 'x' })).toBe('{\n    "a": True,\n    "b": None,\n    "c": "it is true",\n    "true": "x"\n}');
  });
});
