/**
 * Code generation utilities for different programming languages.
 * Generates code snippets from the exact request the API Explorer composes: the same URL,
 * method, headers (see buildExplorerHeaders in providers.js) and body.
 *
 * Config shape: { url, method, headers, body, apiType, operationId, streaming }
 */
import { getProvider, getProviderSdk, getStreamFormat } from './providers';

/**
 * Escape special characters in a string for use inside a double-quoted code string
 * @param {string} str - The string to escape
 * @returns {string} Escaped string
 */
function escapeString(str) {
  return String(str)
    .replace(/\\/g, '\\\\')
    .replace(/'/g, "\\'")
    .replace(/"/g, '\\"')
    .replace(/\n/g, '\\n')
    .replace(/\r/g, '\\r')
    .replace(/\t/g, '\\t');
}

function parseBody(body) {
  if (!body) return { ok: false, value: null };
  try {
    return { ok: true, value: JSON.parse(body) };
  } catch {
    return { ok: false, value: null };
  }
}

function hasBody(config) {
  return Boolean(config.body) && config.method !== 'GET' && config.method !== 'HEAD';
}

function family(apiType) {
  return getProvider(apiType)?.explorerOperations || 'openai';
}

/**
 * Format a JSON value as a JavaScript literal with indentation continuing at baseIndent.
 */
function jsLiteral(value, baseIndent = 0) {
  const prefix = '  '.repeat(baseIndent);
  return JSON.stringify(value, null, 2)
    .split('\n')
    .map((line, i) => (i === 0 ? line : prefix + line))
    .join('\n');
}

/**
 * Format a JSON value as a Python literal (None/True/False, double-quoted strings).
 * @param {*} value
 * @param {number} indent - current indentation level (4 spaces per level)
 * @returns {string}
 */
export function toPythonLiteral(value, indent = 0) {
  const pad = '    '.repeat(indent);
  const innerPad = '    '.repeat(indent + 1);
  if (value === null || value === undefined) return 'None';
  if (value === true) return 'True';
  if (value === false) return 'False';
  if (typeof value === 'number') return Number.isFinite(value) ? String(value) : 'None';
  if (typeof value === 'string') return JSON.stringify(value);
  if (Array.isArray(value)) {
    if (value.length === 0) return '[]';
    return `[\n${value.map((item) => innerPad + toPythonLiteral(item, indent + 1)).join(',\n')}\n${pad}]`;
  }
  const entries = Object.entries(value);
  if (entries.length === 0) return '{}';
  return `{\n${entries.map(([key, item]) => `${innerPad}${JSON.stringify(key)}: ${toPythonLiteral(item, indent + 1)}`).join(',\n')}\n${pad}}`;
}

/**
 * Format a C# verbatim string literal (@"...") holding the given text.
 */
function csharpVerbatim(text) {
  return `@"${String(text).replace(/"/g, '""')}"`;
}

/**
 * Base URL for SDK clients: the VMR URL without the operation path.
 */
function sdkBaseUrl(url, apiType) {
  const fam = family(apiType);
  if (fam === 'anthropic' || fam === 'openai' || fam === 'typesafe') {
    return url.replace(/\/v1(\/.*)?$/, fam === 'openai' ? '/v1' : '');
  }
  return url;
}

function bearerToken(headers) {
  const auth = headers?.Authorization || headers?.authorization || '';
  return auth.startsWith('Bearer ') ? auth.slice(7) : '';
}

/**
 * Generate cURL command
 * @param {Object} config - Request configuration
 * @returns {string} cURL command
 */
export function generateCurl(config) {
  const { url, method, headers, body, streaming } = config;

  let curl = `curl${streaming ? ' -N' : ''} -X ${method} "${url}"`;

  Object.entries(headers || {}).forEach(([key, value]) => {
    curl += ` \\\n  -H "${key}: ${escapeString(value)}"`;
  });

  if (hasBody(config)) {
    const parsed = parseBody(body);
    const text = parsed.ok ? JSON.stringify(parsed.value, null, 2) : body;
    curl += ` \\\n  -d '${text.replace(/'/g, "'\\''")}'`;
  }

  return curl;
}

function javascriptLogStatement(apiType, operationId) {
  const fam = family(apiType);
  if (fam === 'openai' && operationId === 'chatCompletions') return 'console.log(data.choices[0].message.content);';
  if (fam === 'openai' && operationId === 'completions') return 'console.log(data.choices[0].text);';
  if (fam === 'gemini' && (operationId === 'generateContent' || operationId === 'streamGenerateContent')) {
    return 'console.log((data.candidates?.[0]?.content?.parts || []).map(p => p.text || "").join(""));';
  }
  if (fam === 'ollama' && operationId === 'chat') return 'console.log(data.message.content);';
  if (fam === 'ollama' && operationId === 'generate') return 'console.log(data.response);';
  if (fam === 'anthropic' && operationId === 'messages') {
    return 'console.log(data.content.filter(block => block.type === "text").map(block => block.text).join(""));';
  }
  if (fam === 'anthropic' && operationId === 'countTokens') return 'console.log(data.input_tokens);';
  if (fam === 'typesafe' && operationId === 'decide') return 'console.log(data.answers);';
  return 'console.log(data);';
}

/**
 * Generate JavaScript (Fetch API) code
 * @param {Object} config - Request configuration
 * @returns {string} JavaScript code
 */
export function generateJavaScript(config) {
  const { url, method, headers, body, apiType, operationId } = config;
  const lines = [
    `const response = await fetch(${JSON.stringify(url)}, {`,
    `  method: '${method}',`,
    `  headers: ${jsLiteral(headers || {}, 1)}${hasBody(config) ? ',' : ''}`
  ];
  if (hasBody(config)) {
    const parsed = parseBody(body);
    lines.push(parsed.ok
      ? `  body: JSON.stringify(${jsLiteral(parsed.value, 1)})`
      : `  body: ${JSON.stringify(body)}`);
  }
  lines.push('});', '', 'const data = await response.json();', javascriptLogStatement(apiType, operationId));
  return lines.join('\n');
}

function javascriptStreamHandler(streamFormat) {
  switch (streamFormat) {
    case 'anthropic-sse':
      return `// Anthropic SSE format: "event: <name>" then "data: {json}"
      if (line.startsWith('data: ')) {
        const parsed = JSON.parse(line.slice(6));
        if (parsed.type === 'content_block_delta' && parsed.delta?.type === 'text_delta') {
          process.stdout.write(parsed.delta.text);
        } else if (parsed.type === 'error') {
          console.error(parsed.error);
        }
      }`;
    case 'gemini-sse':
      return `// Gemini SSE format
      if (line.startsWith('data: ')) {
        const data = line.slice(6);
        if (data === '[DONE]') continue;
        const parsed = JSON.parse(data);
        const content = (parsed.candidates?.[0]?.content?.parts || [])
          .map(part => part.text || '')
          .join('');
        process.stdout.write(content);
      }`;
    case 'ndjson':
      return `// Ollama JSON lines format
      const parsed = JSON.parse(line);
      const content = parsed.message?.content || parsed.response || '';
      process.stdout.write(content);`;
    default:
      return `// OpenAI SSE format
      if (line.startsWith('data: ')) {
        const data = line.slice(6);
        if (data === '[DONE]') continue;
        const parsed = JSON.parse(data);
        const content = parsed.choices?.[0]?.delta?.content || '';
        process.stdout.write(content);
      }`;
  }
}

/**
 * Generate JavaScript streaming code
 * @param {Object} config - Request configuration
 * @returns {string} JavaScript code for streaming
 */
export function generateJavaScriptStreaming(config) {
  const { url, method, headers, body, apiType } = config;
  const parsed = parseBody(body);
  const bodyLine = hasBody(config)
    ? (parsed.ok ? `,\n  body: JSON.stringify(${jsLiteral(parsed.value, 1)})` : `,\n  body: ${JSON.stringify(body)}`)
    : '';

  return `const response = await fetch(${JSON.stringify(url)}, {
  method: '${method}',
  headers: ${jsLiteral(headers || {}, 1)}${bodyLine}
});

const reader = response.body.getReader();
const decoder = new TextDecoder();
let buffer = '';

while (true) {
  const { done, value } = await reader.read();
  if (done) break;

  buffer += decoder.decode(value, { stream: true });
  const lines = buffer.split('\\n');
  buffer = lines.pop();

  for (const line of lines) {
    if (!line.trim()) continue;
    try {
      ${javascriptStreamHandler(getStreamFormat(apiType))}
    } catch (e) {
      // Skip non-JSON lines
    }
  }
}`;
}

function pythonPrintStatement(apiType, operationId) {
  const fam = family(apiType);
  if (fam === 'openai' && operationId === 'chatCompletions') return "print(response.json()['choices'][0]['message']['content'])";
  if (fam === 'openai' && operationId === 'completions') return "print(response.json()['choices'][0]['text'])";
  if (fam === 'gemini' && (operationId === 'generateContent' || operationId === 'streamGenerateContent')) {
    return "print(''.join(part.get('text', '') for part in response.json()['candidates'][0]['content'].get('parts', [])))";
  }
  if (fam === 'ollama' && operationId === 'chat') return "print(response.json()['message']['content'])";
  if (fam === 'ollama' && operationId === 'generate') return "print(response.json()['response'])";
  if (fam === 'anthropic' && operationId === 'messages') {
    return "print(''.join(block['text'] for block in response.json()['content'] if block['type'] == 'text'))";
  }
  if (fam === 'anthropic' && operationId === 'countTokens') return "print(response.json()['input_tokens'])";
  if (fam === 'typesafe' && operationId === 'decide') return "print(response.json()['answers'])";
  return 'print(response.json())';
}

/**
 * Generate Python (requests library) code
 * @param {Object} config - Request configuration
 * @returns {string} Python code
 */
export function generatePython(config) {
  const { url, method, headers, body, apiType, operationId, streaming } = config;
  const methodName = method.toLowerCase();
  const args = [`    ${JSON.stringify(url)}`, `    headers=${toPythonLiteral(headers || {}, 1)}`];

  if (hasBody(config)) {
    const parsed = parseBody(body);
    args.push(parsed.ok ? `    json=${toPythonLiteral(parsed.value, 1)}` : `    data=${JSON.stringify(body)}`);
  }

  if (streaming) {
    args.push('    stream=True');
    return `import requests

with requests.${methodName}(
${args.join(',\n')}
) as response:
    for line in response.iter_lines(decode_unicode=True):
        if line:
            print(line)`;
  }

  return `import requests

response = requests.${methodName}(
${args.join(',\n')}
)

${pythonPrintStatement(apiType, operationId)}`;
}

/**
 * Generate Python (OpenAI SDK) code - for VMR types whose registry sdk is openai
 * @param {Object} config - Request configuration
 * @returns {string} Python code using OpenAI SDK
 */
export function generatePythonOpenAI(config) {
  const { url, headers, body, operationId, apiType } = config;

  const baseUrl = sdkBaseUrl(url, apiType);
  const apiKey = bearerToken(headers) || 'YOUR_API_KEY';

  let code = `from openai import OpenAI

client = OpenAI(
    base_url=${JSON.stringify(baseUrl)},
    api_key=${JSON.stringify(apiKey)}
)

`;

  if (operationId === 'listModels') {
    return `${code}for model in client.models.list():
    print(model.id)`;
  }

  const parsed = parseBody(body);
  if (!parsed.ok) return `${code}# Unable to parse request body`;
  const kwargs = Object.entries(parsed.value).map(([key, value]) => `    ${key}=${toPythonLiteral(value, 1)}`).join(',\n');

  if (operationId === 'chatCompletions') {
    return `${code}response = client.chat.completions.create(
${kwargs}
)

${parsed.value.stream ? 'for chunk in response:\n    print(chunk.choices[0].delta.content or "", end="")' : 'print(response.choices[0].message.content)'}`;
  }
  if (operationId === 'completions') {
    return `${code}response = client.completions.create(
${kwargs}
)

${parsed.value.stream ? 'for chunk in response:\n    print(chunk.choices[0].text, end="")' : 'print(response.choices[0].text)'}`;
  }
  if (operationId === 'embeddings') {
    return `${code}response = client.embeddings.create(
${kwargs}
)

print(response.data[0].embedding)`;
  }
  return `${code}# This operation is not supported by the OpenAI SDK`;
}

function modelFromUrl(url) {
  const match = url.match(/\/v1\/models\/([^/?#]+)/);
  return match ? decodeURIComponent(match[1]) : 'MODEL_ID';
}

/**
 * Generate Python (Anthropic SDK) code. The SDK sends auth_token as Authorization: Bearer and
 * sets anthropic-version itself, so the request matches what the explorer sends.
 * @param {Object} config - Request configuration
 * @returns {string} Python code using the Anthropic SDK
 */
export function generatePythonAnthropic(config) {
  const { url, headers, body, operationId, apiType } = config;
  const baseUrl = sdkBaseUrl(url, apiType);
  const token = bearerToken(headers) || 'YOUR_CONDUCTOR_CREDENTIAL';

  const code = `import anthropic

# ANTHROPIC_BASE_URL and ANTHROPIC_AUTH_TOKEN work the same way.
client = anthropic.Anthropic(
    base_url=${JSON.stringify(baseUrl)},
    auth_token=${JSON.stringify(token)}
)

`;

  if (operationId === 'listModels') {
    return `${code}for model in client.models.list():
    print(model.id)`;
  }
  if (operationId === 'getModel') {
    return `${code}model = client.models.retrieve(${JSON.stringify(modelFromUrl(url))})
print(model)`;
  }

  const parsed = parseBody(body);
  if (!parsed.ok) return `${code}# Unable to parse request body`;
  const params = { ...parsed.value };
  const streaming = params.stream === true;
  delete params.stream;
  const kwargs = Object.entries(params).map(([key, value]) => `    ${key}=${toPythonLiteral(value, 1)}`).join(',\n');

  if (operationId === 'countTokens') {
    return `${code}result = client.messages.count_tokens(
${kwargs}
)

print(result.input_tokens)`;
  }
  if (streaming) {
    return `${code}with client.messages.stream(
${kwargs}
) as stream:
    for text in stream.text_stream:
        print(text, end="", flush=True)`;
  }
  return `${code}message = client.messages.create(
${kwargs}
)

print("".join(block.text for block in message.content if block.type == "text"))`;
}

/**
 * Generate TypeScript (Anthropic SDK) code.
 * @param {Object} config - Request configuration
 * @returns {string} TypeScript code using @anthropic-ai/sdk
 */
export function generateTypeScriptAnthropic(config) {
  const { url, headers, body, operationId, apiType } = config;
  const baseUrl = sdkBaseUrl(url, apiType);
  const token = bearerToken(headers) || 'YOUR_CONDUCTOR_CREDENTIAL';

  const code = `import Anthropic from '@anthropic-ai/sdk';

// ANTHROPIC_BASE_URL and ANTHROPIC_AUTH_TOKEN work the same way.
const client = new Anthropic({
  baseURL: ${JSON.stringify(baseUrl)},
  authToken: ${JSON.stringify(token)}
});

`;

  if (operationId === 'listModels') {
    return `${code}for await (const model of client.models.list()) {
  console.log(model.id);
}`;
  }
  if (operationId === 'getModel') {
    return `${code}const model = await client.models.retrieve(${JSON.stringify(modelFromUrl(url))});
console.log(model);`;
  }

  const parsed = parseBody(body);
  if (!parsed.ok) return `${code}// Unable to parse request body`;
  const params = { ...parsed.value };
  const streaming = params.stream === true;
  delete params.stream;

  if (operationId === 'countTokens') {
    return `${code}const result = await client.messages.countTokens(${jsLiteral(params, 0)});
console.log(result.input_tokens);`;
  }
  if (streaming) {
    return `${code}const stream = client.messages.stream(${jsLiteral(params, 0)});
stream.on('text', (text) => process.stdout.write(text));
await stream.finalMessage();`;
  }
  return `${code}const message = await client.messages.create(${jsLiteral(params, 0)});
for (const block of message.content) {
  if (block.type === 'text') console.log(block.text);
}`;
}

/**
 * Generate C# (HttpClient) code. The body is sent as the exact JSON text the explorer sends.
 * @param {Object} config - Request configuration
 * @returns {string} C# code
 */
export function generateCSharp(config) {
  const { url, method, headers, body, streaming } = config;

  let code = `using System;
using System.IO;
using System.Net.Http;
using System.Text;

using HttpClient client = new HttpClient();
using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("${method}"), "${url}");
`;

  Object.entries(headers || {}).forEach(([key, value]) => {
    if (key.toLowerCase() !== 'content-type') {
      code += `request.Headers.TryAddWithoutValidation("${key}", "${escapeString(value)}");\n`;
    }
  });

  if (hasBody(config)) {
    const parsed = parseBody(body);
    const json = parsed.ok ? JSON.stringify(parsed.value, null, 2) : body;
    code += `
string json = ${csharpVerbatim(json)};
request.Content = new StringContent(json, Encoding.UTF8, "application/json");
`;
  }

  if (streaming) {
    code += `
using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
using Stream stream = await response.Content.ReadAsStreamAsync();
using StreamReader reader = new StreamReader(stream);
string? line;
while ((line = await reader.ReadLineAsync()) != null)
{
    if (line.Length > 0) Console.WriteLine(line);
}`;
  } else {
    code += `
using HttpResponseMessage response = await client.SendAsync(request);
string result = await response.Content.ReadAsStringAsync();
Console.WriteLine(result);`;
  }

  return code;
}

/**
 * Available code generator languages.
 * openaiOnly / anthropicOnly entries appear only for VMR types whose registry sdk matches.
 */
export const codeGeneratorLanguages = [
  { id: 'curl', label: 'cURL', generate: generateCurl },
  { id: 'javascript', label: 'JavaScript', generate: generateJavaScript },
  { id: 'javascript-stream', label: 'JavaScript (Streaming)', generate: generateJavaScriptStreaming, streamingOnly: true },
  { id: 'python', label: 'Python (requests)', generate: generatePython },
  { id: 'python-openai', label: 'Python (OpenAI SDK)', generate: generatePythonOpenAI, openaiOnly: true },
  { id: 'python-anthropic', label: 'Python (Anthropic SDK)', generate: generatePythonAnthropic, anthropicOnly: true },
  { id: 'typescript-anthropic', label: 'TypeScript (Anthropic SDK)', generate: generateTypeScriptAnthropic, anthropicOnly: true },
  { id: 'csharp', label: 'C#', generate: generateCSharp }
];

/**
 * Get available languages based on configuration
 * @param {string} apiType - VMR API type
 * @param {boolean} streamEnabled - Whether the current request streams
 * @returns {Array} List of available languages
 */
export function getAvailableLanguages(apiType, streamEnabled) {
  const sdk = getProviderSdk(apiType);
  return codeGeneratorLanguages.filter(lang => {
    if (lang.openaiOnly && sdk !== 'openai') return false;
    if (lang.anthropicOnly && sdk !== 'anthropic') return false;
    if (lang.streamingOnly && !streamEnabled) return false;
    return true;
  });
}

/**
 * Generate code for a specific language
 * @param {string} languageId - The language ID
 * @param {Object} config - Request configuration
 * @returns {string} Generated code
 */
export function generateCode(languageId, config) {
  const language = codeGeneratorLanguages.find(l => l.id === languageId);
  if (!language) return '// Unknown language';
  return language.generate(config);
}
