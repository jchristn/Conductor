import { useEffect } from 'react';
import { controlHelp } from '../utils/controlHelp';
import { useLocation } from 'react-router-dom';

const CONTROL_SELECTOR = 'button, input, select, textarea, label, a, th, [role="button"], [role="tab"], [role="switch"], [role="checkbox"], [tabindex], [contenteditable="true"], [role="menuitem"], [role="slider"], td, summary, .stat-card, .metric-card';
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

function labelText(label) {
  const copy = label.cloneNode(true);
  copy.querySelectorAll('input, select, textarea, button').forEach(node => node.remove());
  return cleanText(copy.textContent);
}

function getAssociatedLabelText(element) {
  if (element.labels && element.labels.length > 0) {
    return cleanText(Array.from(element.labels).map(labelText).join(' '));
  }

  const ownerDocument = element.ownerDocument;
  const id = element.getAttribute('id');
  if (id) {
    const explicitLabel = ownerDocument.querySelector(`label[for="${cssEscape(id)}"]`);
    if (explicitLabel) {
      return labelText(explicitLabel);
    }
  }

  const wrappingLabel = element.closest('label');
  if (wrappingLabel) {
    return labelText(wrappingLabel);
  }

  const fieldContainer = element.closest('.form-group, .filter-group, .detail-item, .field, .input-group');
  const nearbyLabel = fieldContainer?.querySelector('label');
  if (nearbyLabel && nearbyLabel !== element) {
    return labelText(nearbyLabel);
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

export function inferTooltip(element) {
  const tag = element.tagName.toLowerCase();
  const existing = cleanText(element.getAttribute('title'));
  const isField = ['input', 'select', 'textarea', 'label'].includes(tag);
  const authored = element.getAttribute('data-tooltip-source') || (element.getAttribute(AUTO_TOOLTIP_ATTR) !== 'true' ? existing : '');
  const label = cleanText(element.getAttribute('aria-label')) || getTextByIds(element.ownerDocument, element.getAttribute('aria-labelledby')) || (isField ? getAssociatedLabelText(element) : '') || authored || (tag === 'button' ? buttonTextFallback(element) : tag === 'select' ? '' : cleanText(element.textContent)) || humanize(element.name || element.id) || element.getAttribute('placeholder') || 'this setting';
  if (existing && element.getAttribute(AUTO_TOOLTIP_ATTR) !== 'true' && existing.length > 28 && existing.toLowerCase() !== label.toLowerCase()) return existing;
  const context = cleanText(element.closest('.modal, .dashboard-section, .view, main')?.querySelector('h2, h1, h3')?.textContent);
  const scope = context ? ` in ${context}` : '';
  const help = element.closest('.form-group, .field')?.querySelector('.help-text, .form-help, small')?.textContent;
  if (help && isField) return cleanText(help);
  const description = controlHelp(element.name || element.id) || controlHelp(label);
  if (description) return `${description}${tag === 'th' && element.classList.contains('sortable') ? ' Click to sort rows by this value.' : ''}`;
  if (tag === 'td') {
    const header = element.closest('table')?.querySelectorAll('thead tr:first-child th')[element.cellIndex];
    return header?.title || `Recorded ${cleanText(header?.textContent) || 'value'} for this resource; an empty value means it was not recorded.`;
  }
  if (tag === 'th') return `${label}: value recorded for each row${scope}.${element.querySelector('button') || element.onclick ? ' Activate to change the sort order.' : ''}`;
  if (tag === 'a') return `Open ${label}${scope}${element.target === '_blank' ? ' in a new browser tab' : ''}.`;
  if (element.getAttribute('role') === 'tab') return `Show the ${label} panel and its related controls${scope}.`;
  if (tag === 'button' || element.getAttribute('role') === 'button') {
    const action = label.toLowerCase();
    if (/last hour|last day|last week|last month/.test(action)) return `Limit the chart to ${action.replace('last ', 'the past ')}; reload the buckets for that time range.`;
    if (/json/.test(action)) return 'Edit the complete profile definition as JSON; changes are shared with the visual editor.';
    if (/diagram/.test(action)) return 'Open the visual queue editor to configure queues, connect them and choose entry/exit points.';
    if (/fifo|standard workloads|two-tier priority/.test(action)) return `Replace the current draft topology with the ${label} scheduling template; review its classes and limits before saving.`;
    if (/validate/.test(action)) return 'Compile-check the current profile draft on the server and show errors without saving it.';
    if (/help/.test(action)) return 'Open the setup wizard and guided tour to learn the current dashboard workflow.';
    if (/dark mode|light mode/.test(action)) return 'Change the dashboard color scheme for readability; the preference is saved in this browser.';
    if (/logout/.test(action)) return 'Clear the active login from this browser and return to the sign-in page.';
    if (/actions menu/.test(action)) return 'Open the operations available for this row, such as edit, inspect or delete.';
    if (/delete|remove|purge|nuke/.test(action)) return `${label}: remove the selected resource${scope}; review any confirmation before proceeding.`;
    if (/save|apply|create/.test(action)) return `${label}: validate and apply the current configuration${scope}.`;
    if (/cancel|close/.test(action)) return `${label}: leave this dialog without applying unsaved edits.`;
    if (/refresh|reload/.test(action)) return `${label}: fetch the latest server data while keeping the current filters.`;
    if (/copy/.test(action)) return `${label}: place the displayed value on your clipboard for reuse.`;
    if (/show value|hide value/.test(action)) return action.startsWith('show') ? 'Reveal the secret in this field so you can inspect it before saving.' : 'Mask this field to conceal the secret from nearby viewers.';
    if (/next|previous|prev|first|last/.test(action)) return `${label}: move through the filtered results without changing the current filter or sort.`;
    if (/zoom in/.test(action)) return 'Increase diagram magnification to inspect queue settings and connections.';
    if (/zoom out/.test(action)) return 'Reduce diagram magnification to see more of the queue topology.';
    if (/fit view/.test(action)) return 'Fit all queue nodes and entry/exit markers within the diagram viewport.';
    if (/edit|configure/.test(action)) return `${label}: open the current resource settings for review and changes.`;
    return `${label}: open or perform this action${scope}.`;
  }
  const type = element.getAttribute('type');
  if (type === 'checkbox' || element.getAttribute('role') === 'switch') return `Enable or disable ${label}${scope}; changes take effect when the form is applied.`;
  if (tag === 'select') return `Choose ${label}${scope}; the selected option is used when applying this form or filter.`;
  if (type === 'password') return `Enter ${label}${scope}; the value is hidden to protect credentials.`;
  if (type === 'file') return `Choose the local file to use for ${label}${scope}.`;
  if (tag === 'input' || tag === 'textarea') {
    const bounds = type === 'number' ? ` Allowed range: ${element.min || 'no minimum'} to ${element.max || 'no maximum'}.` : '';
    return `Enter ${label}${scope}.${bounds}${element.required ? ' This field is required.' : ''}${element.getAttribute('placeholder') ? ` Example: ${element.getAttribute('placeholder')}.` : ''}`;
  }
  return `Details for ${label}${scope}.`;
}

function applyControlTooltips(root) {
  root.querySelectorAll(CONTROL_SELECTOR).forEach((element) => {
    const existingTitle = cleanText(element.getAttribute('title'));
    const autoTitle = element.getAttribute(AUTO_TOOLTIP_ATTR) === 'true';
    if (existingTitle && !autoTitle && existingTitle.length > 28) {
      return;
    }

    if (existingTitle && !autoTitle) element.setAttribute('data-tooltip-source', existingTitle);
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
    observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['aria-label', 'placeholder', 'disabled'] });

    return () => observer.disconnect();
  }, [location.pathname, location.search]);

  return null;
}

export default ControlTooltipHydrator;
