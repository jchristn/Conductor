import React, { useEffect, useRef, useState } from 'react';
import { copyChartPng } from '../utils/chartExport';

/**
 * Icon button that copies a chart SVG to the clipboard as a titled PNG (falling back to a download),
 * showing a check mark briefly after success.
 * @param {{ svgRef: React.RefObject<SVGSVGElement>, getOptions: () => object, label?: string }} props
 *   svgRef - the chart SVG; getOptions - returns the copyChartPng options (title, axis labels, legend);
 *   label - accessible name used when idle.
 */
function ChartCopyButton({ svgRef, getOptions, label = 'Copy chart as an image' }) {
  const [copyState, setCopyState] = useState(null);
  const timerRef = useRef(null);

  useEffect(() => () => clearTimeout(timerRef.current), []);

  const handleCopy = async () => {
    const result = await copyChartPng(svgRef.current, getOptions ? getOptions() : {});
    setCopyState(result);
    clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => setCopyState(null), 1800);
  };

  const title = copyState === 'copied'
    ? 'Copied chart image to clipboard'
    : copyState === 'downloaded'
      ? 'Clipboard unavailable - downloaded chart image instead'
      : label;

  return (
    <button
      type="button"
      className="request-history-refresh-btn"
      onClick={handleCopy}
      title={title}
      aria-label={title}
    >
      {copyState === 'copied' || copyState === 'downloaded' ? (
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <polyline points="20 6 9 17 4 12" />
        </svg>
      ) : (
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
          <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
        </svg>
      )}
    </button>
  );
}

export default ChartCopyButton;
