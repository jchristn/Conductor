import { useEffect, useRef } from 'react';

/**
 * Registers a chart with a shared copy control. When a parent owns the copy button (the Dashboard
 * chart toolbar), it passes exportRef; the mounted chart publishes its SVG ref and copy options there
 * and clears them on unmount. Without exportRef this hook does nothing and the chart keeps its own button.
 * @param {React.MutableRefObject|null|undefined} exportRef - shared target, or null when the chart owns its copy button.
 * @param {React.RefObject<SVGSVGElement>} svgRef - the chart SVG.
 * @param {() => object} getOptions - returns the copyChartPng options; the latest closure is always used.
 */
export default function useChartExportTarget(exportRef, svgRef, getOptions) {
  const optionsRef = useRef(getOptions);
  optionsRef.current = getOptions;

  useEffect(() => {
    if (!exportRef) return undefined;
    const target = { svgRef, getOptions: () => (optionsRef.current ? optionsRef.current() : {}) };
    exportRef.current = target;
    return () => {
      if (exportRef.current === target) exportRef.current = null;
    };
  }, [exportRef, svgRef]);
}
