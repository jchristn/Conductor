/**
 * Code generation utilities for different programming languages.
 * Generates code snippets based on the current API request configuration.
 */

/**
 * Escape special characters in a string for use in code
 * @param {string} str - The string to escape
 * @returns {string} Escaped string
 */
function escapeString(str) {
  return str
    .replace(/\\/g, '\\\\')
    .replace(/'/g, "\\'")
    .replace(/"/g, '\\"')
    .replace(/\n/g, '\\n')
    .replace(/\r/g, '\\r')
    .replace(/\t/g, '\\t');
}

/**
 * Format JSON for display in code with proper indentation
 * @param {string} json - JSON string
 * @param {number} baseIndent - Base indentation level
 * @param {string} indentChar - Character(s) to use for indentation
 * @returns {string} Formatted JSON
 */
function formatJsonForCode(json, baseIndent = 0, indentChar = '  ') {
  try {
    const obj = JSON.parse(json);
    const formatted = JSON.stringify(obj, null, 2);
    const lines = formatted.split('\n');
    const prefix = indentChar.repeat(baseIndent);
    return lines.map((line, i) => i === 0 ? line : prefix + line).join('\n');
  } catch {
    return json;
  }
}

/**
 * Generate cURL command
 * @param {Object} config - Request configuration
 * @returns {string} cURL command
 */
export function generateCurl(config) {
  const { url, method, headers, body } = config;

  let curl = `curl -X ${method} "${url}"`;

  // Add headers
  Object.entries(headers).forEach(([key, value]) => {
    curl += ` \\\n  -H "${key}: ${escapeString(value)}"`;
  });

  // Add body for non-GET requests
  if (body && method !== 'GET') {
    try {
      const parsed = JSON.parse(body);
      const formatted = JSON.stringify(parsed, null, 2);
      curl += ` \\\n  -d '${formatted.replace(/'/g, "'\\''")}'`;
    } catch {
      curl += ` \\\n  -d '${body.replace(/'/g, "'\\''")}'`;
    }
  }

  return curl;
}

/**
 * Generate JavaScript (Fetch API) code
 * @param {Object} config - Request configuration
 * @returns {string} JavaScript code
 */
export function generateJavaScript(config) {
  const { url, method, headers, body, apiType, operationId } = config;

  let code = '';

  if (method === 'GET') {
    code = `const response = await fetch('${url}', {
  method: '${method}',
  headers: ${formatJsonForCode(JSON.stringify(headers), 1)}
});

const data = await response.json();
console.log(data);`;
  } else {
    // Determine what to log based on operation
    let logStatement = 'console.log(data);';
    if (apiType === 'OpenAI' && (operationId === 'chatCompletions' || operationId === 'completions')) {
      logStatement = operationId === 'chatCompletions'
        ? 'console.log(data.choices[0].message.content);'
        : 'console.log(data.choices[0].text);';
    } else if (apiType === 'Ollama' && (operationId === 'chat' || operationId === 'generate')) {
      logStatement = operationId === 'chat'
        ? 'console.log(data.message.content);'
        : 'console.log(data.response);';
    }

    code = `const response = await fetch('${url}', {
  method: '${method}',
  headers: ${formatJsonForCode(JSON.stringify(headers), 1)},
  body: JSON.stringify(${formatJsonForCode(body, 1)})
});

const data = await response.json();
${logStatement}`;
  }

  return code;
}

/**
 * Generate JavaScript streaming code
 * @param {Object} config - Request configuration
 * @returns {string} JavaScript code for streaming
 */
export function generateJavaScriptStreaming(config) {
  const { url, method, headers, body, apiType } = config;

  const extractContent = apiType === 'OpenAI'
    ? `// OpenAI SSE format
        if (line.startsWith('data: ')) {
          const data = line.slice(6);
          if (data === '[DONE]') continue;
          const parsed = JSON.parse(data);
          const content = parsed.choices?.[0]?.delta?.content || '';
          process.stdout.write(content);
        }`
    : `// Ollama JSON lines format
        const parsed = JSON.parse(line);
        const content = parsed.message?.content || parsed.response || '';
        process.stdout.write(content);`;

  return `const response = await fetch('${url}', {
  method: '${method}',
  headers: ${formatJsonForCode(JSON.stringify(headers), 1)},
  body: JSON.stringify(${formatJsonForCode(body, 1)})
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
      ${extractContent}
    } catch (e) {
      // Skip non-JSON lines
    }
  }
}`;
}

/**
 * Generate Python (requests library) code
 * @param {Object} config - Request configuration
 * @returns {string} Python code
 */
export function generatePython(config) {
  const { url, method, headers, body, apiType, operationId } = config;

  // Format headers for Python
  const headersStr = JSON.stringify(headers, null, 4)
    .replace(/"/g, "'")
    .split('\n')
    .map((line, i) => i === 0 ? line : '    ' + line)
    .join('\n');

  let code = 'import requests\n\n';

  if (method === 'GET') {
    code += `response = requests.get(
    '${url}',
    headers=${headersStr}
)

print(response.json())`;
  } else {
    // Parse and format body
    let bodyStr = '{}';
    try {
      const parsed = JSON.parse(body);
      bodyStr = JSON.stringify(parsed, null, 4)
        .replace(/"/g, "'")
        .replace(/true/g, 'True')
        .replace(/false/g, 'False')
        .replace(/null/g, 'None')
        .split('\n')
        .map((line, i) => i === 0 ? line : '    ' + line)
        .join('\n');
    } catch {
      bodyStr = body;
    }

    // Determine what to print based on operation
    let printStatement = 'print(response.json())';
    if (apiType === 'OpenAI' && (operationId === 'chatCompletions' || operationId === 'completions')) {
      printStatement = operationId === 'chatCompletions'
        ? "print(response.json()['choices'][0]['message']['content'])"
        : "print(response.json()['choices'][0]['text'])";
    } else if (apiType === 'Ollama' && (operationId === 'chat' || operationId === 'generate')) {
      printStatement = operationId === 'chat'
        ? "print(response.json()['message']['content'])"
        : "print(response.json()['response'])";
    }

    const methodName = method.toLowerCase();
    code += `response = requests.${methodName}(
    '${url}',
    headers=${headersStr},
    json=${bodyStr}
)

${printStatement}`;
  }

  return code;
}

/**
 * Generate Python (OpenAI SDK) code - only for OpenAI API type
 * @param {Object} config - Request configuration
 * @returns {string} Python code using OpenAI SDK
 */
export function generatePythonOpenAI(config) {
  const { url, headers, body, operationId } = config;

  // Extract base URL (remove the endpoint path)
  const baseUrl = url.replace(/\/v1\/.*$/, '/v1');
  const apiKey = headers['Authorization']?.replace('Bearer ', '') || 'YOUR_API_KEY';

  let code = `from openai import OpenAI

client = OpenAI(
    base_url='${baseUrl}',
    api_key='${apiKey}'
)

`;

  try {
    const parsed = JSON.parse(body);

    if (operationId === 'chatCompletions') {
      const messages = JSON.stringify(parsed.messages || [], null, 4)
        .replace(/"/g, "'")
        .split('\n')
        .map((line, i) => i === 0 ? line : '    ' + line)
        .join('\n');

      code += `response = client.chat.completions.create(
    model='${parsed.model || 'gpt-3.5-turbo'}',
    messages=${messages},
    stream=${parsed.stream ? 'True' : 'False'}${parsed.temperature !== undefined ? `,\n    temperature=${parsed.temperature}` : ''}${parsed.max_tokens !== undefined ? `,\n    max_tokens=${parsed.max_tokens}` : ''}
)

print(response.choices[0].message.content)`;
    } else if (operationId === 'completions') {
      code += `response = client.completions.create(
    model='${parsed.model || 'gpt-3.5-turbo-instruct'}',
    prompt='${escapeString(parsed.prompt || '')}',
    stream=${parsed.stream ? 'True' : 'False'}${parsed.temperature !== undefined ? `,\n    temperature=${parsed.temperature}` : ''}${parsed.max_tokens !== undefined ? `,\n    max_tokens=${parsed.max_tokens}` : ''}
)

print(response.choices[0].text)`;
    } else if (operationId === 'embeddings') {
      code += `response = client.embeddings.create(
    model='${parsed.model || 'text-embedding-ada-002'}',
    input='${escapeString(typeof parsed.input === 'string' ? parsed.input : JSON.stringify(parsed.input))}'
)

print(response.data[0].embedding)`;
    } else {
      code += '# This operation is not supported by the OpenAI SDK';
    }
  } catch {
    code += '# Unable to parse request body';
  }

  return code;
}

/**
 * Generate C# (HttpClient) code
 * @param {Object} config - Request configuration
 * @returns {string} C# code
 */
export function generateCSharp(config) {
  const { url, method, headers, body, apiType, operationId } = config;

  let code = `using System.Net.Http;
using System.Text;
using System.Text.Json;

using var client = new HttpClient();
`;

  // Add headers
  Object.entries(headers).forEach(([key, value]) => {
    if (key !== 'Content-Type') {
      code += `client.DefaultRequestHeaders.Add("${key}", "${escapeString(value)}");\n`;
    }
  });

  code += '\n';

  if (method === 'GET') {
    code += `var response = await client.GetAsync("${url}");
var result = await response.Content.ReadAsStringAsync();
Console.WriteLine(result);`;
  } else {
    // Format body for C#
    let bodyObj = {};
    try {
      bodyObj = JSON.parse(body);
    } catch {
      bodyObj = {};
    }

    // Convert to C# anonymous object
    const formatCSharpObject = (obj, indent = 0) => {
      const spaces = '    '.repeat(indent);
      if (Array.isArray(obj)) {
        if (obj.length === 0) return 'Array.Empty<object>()';
        const items = obj.map(item => formatCSharpObject(item, indent + 1));
        return `new[] {\n${spaces}    ${items.join(`,\n${spaces}    `)}\n${spaces}}`;
      } else if (typeof obj === 'object' && obj !== null) {
        const entries = Object.entries(obj);
        if (entries.length === 0) return 'new { }';
        const props = entries.map(([k, v]) => `${k} = ${formatCSharpObject(v, indent + 1)}`);
        return `new {\n${spaces}    ${props.join(`,\n${spaces}    `)}\n${spaces}}`;
      } else if (typeof obj === 'string') {
        return `"${escapeString(obj)}"`;
      } else if (typeof obj === 'boolean') {
        return obj.toString();
      } else if (obj === null) {
        return 'null';
      }
      return String(obj);
    };

    const csharpBody = formatCSharpObject(bodyObj, 0);

    const httpMethod = method === 'POST' ? 'PostAsync' :
                       method === 'PUT' ? 'PutAsync' :
                       method === 'DELETE' ? 'DeleteAsync' : 'SendAsync';

    code += `var content = new StringContent(
    JsonSerializer.Serialize(${csharpBody}),
    Encoding.UTF8,
    "application/json"
);

var response = await client.${httpMethod}(
    "${url}",
    content
);
var result = await response.Content.ReadAsStringAsync();
Console.WriteLine(result);`;
  }

  return code;
}

/**
 * Available code generator languages
 */
export const codeGeneratorLanguages = [
  { id: 'curl', label: 'cURL', generate: generateCurl },
  { id: 'javascript', label: 'JavaScript', generate: generateJavaScript },
  { id: 'javascript-stream', label: 'JavaScript (Streaming)', generate: generateJavaScriptStreaming, streamingOnly: true },
  { id: 'python', label: 'Python (requests)', generate: generatePython },
  { id: 'python-openai', label: 'Python (OpenAI SDK)', generate: generatePythonOpenAI, openaiOnly: true },
  { id: 'csharp', label: 'C#', generate: generateCSharp }
];

/**
 * Get available languages based on configuration
 * @param {string} apiType - 'OpenAI' or 'Ollama'
 * @param {boolean} streamEnabled - Whether streaming is enabled
 * @returns {Array} List of available languages
 */
export function getAvailableLanguages(apiType, streamEnabled) {
  return codeGeneratorLanguages.filter(lang => {
    if (lang.openaiOnly && apiType !== 'OpenAI') return false;
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
