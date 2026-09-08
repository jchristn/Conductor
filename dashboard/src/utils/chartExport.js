// Render a chart's <svg> to a PNG (with a title and X/Y axis labels drawn around it) and copy it to the
// clipboard. Falls back to a file download when the async Clipboard image API is unavailable (for example
// on plain http origins or non-localhost hosts where the browser blocks clipboard image writes).
//
// The chart SVG colors come from CSS variables, which don't resolve once the SVG is serialized and
// rasterized - so we clone the SVG and inline each element's computed fill/stroke first.

function cssVar(name, fallback) {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name);
  return (value && value.trim()) || fallback;
}

function inlineComputedColors(sourceSvg, cloneSvg) {
  const originals = sourceSvg.querySelectorAll('*');
  const clones = cloneSvg.querySelectorAll('*');
  const count = Math.min(originals.length, clones.length);
  for (let i = 0; i < count; i++) {
    const cs = getComputedStyle(originals[i]);
    const clone = clones[i];
    if (cs.fill && cs.fill !== 'none') clone.setAttribute('fill', cs.fill);
    if (cs.stroke && cs.stroke !== 'none') clone.setAttribute('stroke', cs.stroke);
    if (cs.strokeDasharray && cs.strokeDasharray !== 'none') clone.setAttribute('stroke-dasharray', cs.strokeDasharray);
    if (cs.fontSize) clone.setAttribute('font-size', cs.fontSize);
  }
}

async function svgToImage(svgEl, width, height) {
  const clone = svgEl.cloneNode(true);
  inlineComputedColors(svgEl, clone);
  clone.setAttribute('width', String(width));
  clone.setAttribute('height', String(height));
  clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
  const svgString = new XMLSerializer().serializeToString(clone);
  const url = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svgString);
  const img = new Image();
  img.width = width;
  img.height = height;
  await new Promise((resolve, reject) => {
    img.onload = resolve;
    img.onerror = reject;
    img.src = url;
  });
  return img;
}

/**
 * Render `svgEl` to a titled/labeled PNG blob.
 * @param {SVGSVGElement} svgEl - the chart SVG to rasterize; may be null.
 * @param {{title?:string,xLabel?:string,yLabel?:string,scale?:number,legend?:{label:string,color:string}[]}} opts
 * @returns {Promise<Blob|null>} the rendered PNG, or null when `svgEl` is missing.
 */
export async function renderChartPngBlob(svgEl, opts = {}) {
  if (!svgEl) return null;
  const { title = 'Chart', xLabel = '', yLabel = '', scale = 2, legend = [] } = opts;

  const vb = svgEl.viewBox && svgEl.viewBox.baseVal;
  const chartW = vb && vb.width ? vb.width : (svgEl.clientWidth || 800);
  const chartH = vb && vb.height ? vb.height : (svgEl.clientHeight || 240);

  const img = await svgToImage(svgEl, chartW, chartH);

  const bg = cssVar('--card-bg', '#ffffff');
  const fg = cssVar('--text-primary', '#1e293b');
  const sub = cssVar('--text-secondary', '#64748b');
  const border = cssVar('--border-color', '#e2e8f0');

  const padX = 20;
  const titleH = 40;
  const yLabelW = yLabel ? 26 : 8;
  const xLabelH = xLabel ? 30 : 12;
  const legendH = legend.length ? 26 : 0;
  const outW = yLabelW + chartW + padX;
  const outH = titleH + chartH + xLabelH + legendH + 8;

  const canvas = document.createElement('canvas');
  canvas.width = Math.round(outW * scale);
  canvas.height = Math.round(outH * scale);
  const g = canvas.getContext('2d');
  g.scale(scale, scale);

  g.fillStyle = bg;
  g.fillRect(0, 0, outW, outH);

  // Title
  g.fillStyle = fg;
  g.font = '600 16px system-ui, -apple-system, Segoe UI, sans-serif';
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  g.fillText(title, yLabelW + chartW / 2, titleH / 2);

  // Chart image
  g.drawImage(img, yLabelW, titleH, chartW, chartH);

  // Y axis label (rotated)
  if (yLabel) {
    g.save();
    g.translate(13, titleH + chartH / 2);
    g.rotate(-Math.PI / 2);
    g.fillStyle = sub;
    g.font = '12px system-ui, -apple-system, Segoe UI, sans-serif';
    g.textAlign = 'center';
    g.textBaseline = 'middle';
    g.fillText(yLabel, 0, 0);
    g.restore();
  }

  // X axis label
  if (xLabel) {
    g.fillStyle = sub;
    g.font = '12px system-ui, -apple-system, Segoe UI, sans-serif';
    g.textAlign = 'center';
    g.textBaseline = 'middle';
    g.fillText(xLabel, yLabelW + chartW / 2, titleH + chartH + xLabelH / 2);
  }

  // Legend (swatch + label chips, centered)
  if (legend.length) {
    g.font = '11px system-ui, -apple-system, Segoe UI, sans-serif';
    g.textBaseline = 'middle';
    const sw = 10;
    const gap = 6;
    const itemGap = 14;
    const widths = legend.map((l) => sw + gap + g.measureText(l.label).width + itemGap);
    const totalW = widths.reduce((a, w) => a + w, 0) - itemGap;
    let x = yLabelW + Math.max(0, (chartW - totalW) / 2);
    const y = titleH + chartH + xLabelH + legendH / 2;
    for (let i = 0; i < legend.length; i++) {
      g.fillStyle = legend[i].color;
      g.fillRect(x, y - sw / 2, sw, sw);
      g.strokeStyle = border;
      g.strokeRect(x, y - sw / 2, sw, sw);
      g.fillStyle = fg;
      g.textAlign = 'left';
      g.fillText(legend[i].label, x + sw + gap, y);
      x += widths[i];
    }
  }

  return await new Promise((resolve) => canvas.toBlob(resolve, 'image/png'));
}

/**
 * Copy the chart PNG to the clipboard, falling back to a download when the clipboard image API is
 * unavailable or blocked (insecure/http origins, non-localhost hosts, denied permissions).
 * @param {SVGSVGElement} svgEl - the chart SVG to rasterize.
 * @param {object} opts - forwarded to {@link renderChartPngBlob}.
 * @returns {Promise<'copied'|'downloaded'|'failed'>} the outcome of the copy attempt.
 */
export async function copyChartPng(svgEl, opts = {}) {
  try {
    const blob = await renderChartPngBlob(svgEl, opts);
    if (!blob) return 'failed';
    if (navigator.clipboard && typeof window.ClipboardItem !== 'undefined') {
      try {
        await navigator.clipboard.write([new window.ClipboardItem({ 'image/png': blob })]);
        return 'copied';
      } catch {
        /* clipboard blocked (permissions/insecure context) - fall through to download */
      }
    }
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${(opts.title || 'chart').replace(/[^a-z0-9]+/gi, '-').toLowerCase()}.png`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
    return 'downloaded';
  } catch {
    return 'failed';
  }
}
