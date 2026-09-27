import { useLayoutEffect, useState } from 'react';

/**
 * Position a chart hover tooltip next to the cursor while keeping it fully inside the chart
 * container: the tooltip flips to the other side of the cursor near an edge and is clamped against
 * the container and its own measured size.
 * @param {React.RefObject<HTMLElement>} containerRef - the relatively positioned chart container.
 * @param {React.RefObject<HTMLElement>} tooltipRef - the absolutely positioned tooltip element.
 * @param {{relX:number, relY:number}|null} hovered - cursor position relative to the container, or null.
 * @returns {{left:number, top:number}|null} the tooltip position in pixels, or null before it is measured.
 */
export default function useClampedTooltip(containerRef, tooltipRef, hovered) {
  const [tooltipPos, setTooltipPos] = useState(null);

  useLayoutEffect(() => {
    if (!hovered || !containerRef.current || !tooltipRef.current) {
      return;
    }
    const containerRect = containerRef.current.getBoundingClientRect();
    const tooltipRect = tooltipRef.current.getBoundingClientRect();
    const margin = 8;
    const offset = 14;

    let left = hovered.relX + offset;
    if (left + tooltipRect.width + margin > containerRect.width) {
      left = hovered.relX - tooltipRect.width - offset;
    }
    left = Math.max(margin, Math.min(left, containerRect.width - tooltipRect.width - margin));

    let top = hovered.relY + offset;
    if (top + tooltipRect.height + margin > containerRect.height) {
      top = hovered.relY - tooltipRect.height - offset;
    }
    top = Math.max(margin, Math.min(top, containerRect.height - tooltipRect.height - margin));

    setTooltipPos((current) => {
      if (current && current.left === left && current.top === top) return current;
      return { left, top };
    });
  }, [containerRef, tooltipRef, hovered]);

  return tooltipPos;
}
