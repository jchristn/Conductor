const count = value => Math.max(0, Number(value) || 0);
// Cached tokens are a subset of input. Multimodal is an overlapping provider
// dimension and must not be added to the token stack a second time.
export function tokenParts(bucket) {
  const input = count(bucket.PromptTokens);
  const output = count(bucket.CompletionTokens);
  const cached = Math.min(input, count(bucket.CachedTokens));
  return [input - cached, output, cached, Math.max(0, count(bucket.TotalTokens) - input - output)];
}
export function chartSeries(data, mode) {
  if (mode === 'tokens') return ['Input (uncached when known)', 'Output', 'Cached input', 'Other reported'].map((label, i) => ({ label, values: data.map(b => i === 2 && b.CachedTokens == null ? null : tokenParts(b)[i]) }));
  if (mode === 'latency') return [{ label: 'Average TTFT (ms)', values: data.map(b => b.AverageTimeToFirstTokenMs ?? null) }, { label: 'Average response latency (ms)', values: data.map(b => b.AverageResponseTimeMs ?? null) }];
  return [
    { label: 'Successful requests', values: data.map(b => count(b.SuccessfulCompletionCount ?? b.SuccessCount)) },
    { label: 'Failed requests', values: data.map(b => count(b.FailedRequestCount ?? b.FailureCount)) },
    { label: 'Average TTFT (ms, right axis)', axis: 'right', values: data.map(b => b.AverageTimeToFirstTokenMs ?? null) }
  ];
}
