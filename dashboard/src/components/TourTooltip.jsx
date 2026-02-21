import React, { useEffect, useState, useRef } from 'react';

function TourTooltip({ targetRect, title, description, stepIndex, totalSteps, onNext, onPrev, onSkip, position }) {
  const tooltipRef = useRef(null);
  const [tooltipStyle, setTooltipStyle] = useState({});
  const [arrowClass, setArrowClass] = useState('tour-arrow-top');

  useEffect(() => {
    if (!targetRect || !tooltipRef.current) return;

    const tooltip = tooltipRef.current;
    const tooltipRect = tooltip.getBoundingClientRect();
    const padding = 12;
    const arrowSize = 8;
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    let top = 0;
    let left = 0;
    let arrowPos = 'tour-arrow-top';

    const preferredPosition = position || 'bottom';

    // Try to place tooltip below target
    if (preferredPosition === 'bottom' || preferredPosition === 'auto') {
      top = targetRect.bottom + padding + arrowSize;
      left = targetRect.left + targetRect.width / 2 - tooltipRect.width / 2;
      arrowPos = 'tour-arrow-top';

      // If overflows bottom, try above
      if (top + tooltipRect.height > viewportHeight - padding) {
        top = targetRect.top - tooltipRect.height - padding - arrowSize;
        arrowPos = 'tour-arrow-bottom';
      }
    } else if (preferredPosition === 'right') {
      top = targetRect.top + targetRect.height / 2 - tooltipRect.height / 2;
      left = targetRect.right + padding + arrowSize;
      arrowPos = 'tour-arrow-left';

      if (left + tooltipRect.width > viewportWidth - padding) {
        left = targetRect.left - tooltipRect.width - padding - arrowSize;
        arrowPos = 'tour-arrow-right';
      }
    } else if (preferredPosition === 'left') {
      top = targetRect.top + targetRect.height / 2 - tooltipRect.height / 2;
      left = targetRect.left - tooltipRect.width - padding - arrowSize;
      arrowPos = 'tour-arrow-right';

      if (left < padding) {
        left = targetRect.right + padding + arrowSize;
        arrowPos = 'tour-arrow-left';
      }
    } else if (preferredPosition === 'top') {
      top = targetRect.top - tooltipRect.height - padding - arrowSize;
      left = targetRect.left + targetRect.width / 2 - tooltipRect.width / 2;
      arrowPos = 'tour-arrow-bottom';

      if (top < padding) {
        top = targetRect.bottom + padding + arrowSize;
        arrowPos = 'tour-arrow-top';
      }
    }

    // Clamp to viewport
    left = Math.max(padding, Math.min(left, viewportWidth - tooltipRect.width - padding));
    top = Math.max(padding, Math.min(top, viewportHeight - tooltipRect.height - padding));

    setTooltipStyle({ top: `${top}px`, left: `${left}px` });
    setArrowClass(arrowPos);
  }, [targetRect, position]);

  return (
    <div ref={tooltipRef} className={`tour-tooltip ${arrowClass}`} style={tooltipStyle}>
      <div className="tour-tooltip-header">
        <span className="tour-step-indicator">{stepIndex + 1} / {totalSteps}</span>
        <button className="tour-skip-btn" onClick={onSkip}>Skip tour</button>
      </div>
      <h3 className="tour-tooltip-title">{title}</h3>
      <p className="tour-tooltip-description">{description}</p>
      <div className="tour-tooltip-actions">
        <button
          className="btn-secondary btn-small"
          onClick={onPrev}
          disabled={stepIndex === 0}
        >
          Back
        </button>
        <button className="btn-primary btn-small" onClick={onNext}>
          {stepIndex === totalSteps - 1 ? 'Finish' : 'Next'}
        </button>
      </div>
    </div>
  );
}

export default TourTooltip;
