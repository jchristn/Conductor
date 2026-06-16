import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

const CONTROL_SELECTOR = 'button, input, select, textarea, label, [role="button"]';
const AUTO_TOOLTIP_ATTR = 'data-auto-tooltip';

function cleanText(value) {
  return (value || '')
    .replace(/\s+/g, ' ')
    .replace(/\s*[:*]\s*$/, '')
    .trim();
}

function humanize(value) {
  return cleanText(String(value || '')
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2'));
}

function cssEscape(value) {
  if (typeof CSS !== 'undefined' && CSS.escape) {
    return CSS.escape(value);
  }

  return String(value).replace(/["\\]/g, '\\$&');
}

function getTextByIds(ownerDocument, ids) {
  return cleanText(String(ids || '')
    .split(/\s+/)
    .map((id) => ownerDocument.getElementById(id)?.textContent || '')
    .join(' '));
}

function getAssociatedLabelText(element) {
  if (element.labels && element.labels.length > 0) {
    return cleanText(Array.from(element.labels).map((label) => label.textContent).join(' '));
  }

  const ownerDocument = element.ownerDocument;
  const id = element.getAttribute('id');
  if (id) {
    const explicitLabel = ownerDocument.querySelector(`label[for="${cssEscape(id)}"]`);
    if (explicitLabel) {
      return cleanText(explicitLabel.textContent);
    }
  }

  const wrappingLabel = element.closest('label');
  if (wrappingLabel) {
    return cleanText(wrappingLabel.textContent);
  }

  const fieldContainer = element.closest('.form-group, .filter-group, .detail-item, .field, .input-group');
  const nearbyLabel = fieldContainer?.querySelector('label');
  if (nearbyLabel && nearbyLabel !== element) {
    return cleanText(nearbyLabel.textContent);
  }

  return '';
}

function buttonTextFallback(element) {
  const text = cleanText(element.textContent);
  const symbolLabels = {
    '...': 'Open actions menu',
    '⋮': 'Open actions menu',
    '×': 'Close',
    'x': 'Close',
    'X': 'Close',
    '<': 'Previous',
    '>': 'Next',
    '<<': 'First page',
    '>>': 'Last page',
    '«': 'First page',
    '»': 'Last page'
  };

  if (symbolLabels[text]) {
    return symbolLabels[text];
  }

  if (text && !/^[^\w]+$/.test(text)) {
    return text;
  }

  if (element.classList.contains('action-menu-trigger')) return 'Open actions menu';
  if (element.classList.contains('modal-close')) return 'Close';
  if (element.classList.contains('error-dismiss')) return 'Dismiss message';
  if (element.classList.contains('btn-icon')) return 'Button';

  return 'Button';
}

function inferTooltip(element) {
  const explicitTitle = cleanText(element.getAttribute('title'));
  if (explicitTitle && element.getAttribute(AUTO_TOOLTIP_ATTR) !== 'true') {
    return explicitTitle;
  }

  const ariaLabel = cleanText(element.getAttribute('aria-label'));
  if (ariaLabel) return ariaLabel;

  const labelledBy = getTextByIds(element.ownerDocument, element.getAttribute('aria-labelledby'));
  if (labelledBy) return labelledBy;

  const tagName = element.tagName.toLowerCase();
  if (tagName === 'label') {
    return cleanText(element.textContent) || 'Label';
  }

  if (tagName === 'button' || element.getAttribute('role') === 'button') {
    return buttonTextFallback(element);
  }

  const labelText = getAssociatedLabelText(element);
  if (labelText) return labelText;

  const placeholder = cleanText(element.getAttribute('placeholder'));
  if (placeholder) return placeholder;

  const name = humanize(element.getAttribute('name') || element.getAttribute('id'));
  if (name) return name;

  if (tagName === 'select') return 'Select an option';
  if (tagName === 'textarea') return 'Enter text';
  if (element.getAttribute('type') === 'checkbox') return 'Toggle option';
  if (element.getAttribute('type') === 'file') return 'Select file';

  return 'Input';
}

function applyControlTooltips(root) {
  root.querySelectorAll(CONTROL_SELECTOR).forEach((element) => {
    const existingTitle = cleanText(element.getAttribute('title'));
    const autoTitle = element.getAttribute(AUTO_TOOLTIP_ATTR) === 'true';
    if (existingTitle && !autoTitle) {
      return;
    }

    const tooltip = inferTooltip(element);
    if (tooltip) {
      element.setAttribute('title', tooltip);
      element.setAttribute(AUTO_TOOLTIP_ATTR, 'true');
    }
  });
}

function ControlTooltipHydrator() {
  const location = useLocation();

  useEffect(() => {
    if (typeof document === 'undefined' || !document.body) {
      return undefined;
    }

    let scheduled = false;
    const scheduleHydration = () => {
      if (scheduled) return;
      scheduled = true;
      window.requestAnimationFrame(() => {
        scheduled = false;
        applyControlTooltips(document);
      });
    };

    scheduleHydration();
    const observer = new MutationObserver(scheduleHydration);
    observer.observe(document.body, { childList: true, subtree: true });

    return () => observer.disconnect();
  }, [location.pathname, location.search]);

  return null;
}

export default ControlTooltipHydrator;
