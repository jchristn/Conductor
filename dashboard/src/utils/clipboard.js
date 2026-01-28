/**
 * Copy text to clipboard with fallback for non-secure contexts (HTTP).
 *
 * The modern Clipboard API (navigator.clipboard) only works in secure contexts
 * (HTTPS or localhost). This utility provides a fallback using the deprecated
 * but widely supported document.execCommand('copy') method.
 *
 * @param {string} text - The text to copy to clipboard
 * @returns {Promise<boolean>} - Whether the copy operation succeeded
 */
export async function copyToClipboard(text) {
  // Try modern Clipboard API first (requires secure context)
  if (navigator.clipboard && window.isSecureContext) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch (err) {
      console.warn('Clipboard API failed, trying fallback:', err);
    }
  }

  // Fallback for non-secure contexts (HTTP with non-localhost hostname)
  return fallbackCopyToClipboard(text);
}

/**
 * Fallback copy method using a temporary textarea.
 * Works in non-secure contexts where navigator.clipboard is unavailable.
 *
 * @param {string} text - The text to copy
 * @returns {boolean} - Whether the copy operation succeeded
 */
function fallbackCopyToClipboard(text) {
  const textarea = document.createElement('textarea');
  textarea.value = text;

  // Prevent scrolling to bottom of page
  textarea.style.position = 'fixed';
  textarea.style.left = '-9999px';
  textarea.style.top = '-9999px';
  textarea.style.opacity = '0';

  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();

  let success = false;
  try {
    success = document.execCommand('copy');
  } catch (err) {
    console.error('Fallback copy failed:', err);
  }

  document.body.removeChild(textarea);
  return success;
}
