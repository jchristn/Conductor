import { beforeEach, afterEach, it, expect, vi } from 'vitest';
import { copyChartPng } from '../src/utils/chartExport';
let svg;
beforeEach(() => {
 svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
 svg.setAttribute('viewBox', '0 0 800 240'); document.body.append(svg);
 vi.stubGlobal('Image', class { set src(value) { queueMicrotask(() => this.onload()); } });
 vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ scale() {}, fillRect() {}, fillText() {}, drawImage() {}, save() {}, restore() {}, translate() {}, rotate() {}, strokeRect() {}, measureText() { return { width: 20 }; } });
 vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation(function(cb) { cb(new Blob(['png'], { type: 'image/png' })); });
 vi.stubGlobal('ClipboardItem', class { constructor(items) { this.items = items; } });
 Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { write: vi.fn().mockResolvedValue() } });
 URL.createObjectURL = vi.fn(() => 'blob:chart'); URL.revokeObjectURL = vi.fn();
 HTMLDialogElement.prototype.showModal = function() { this.open = true; };
 HTMLDialogElement.prototype.close = function() { this.dispatchEvent(new Event('close')); };
});
afterEach(() => { document.body.replaceChildren(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
it('passes deferred PNG to clipboard immediately to preserve activation', async () => {
 const result = copyChartPng(svg);
 expect(navigator.clipboard.write).toHaveBeenCalledOnce();
 const item = navigator.clipboard.write.mock.calls[0][0][0];
 expect(item.items['image/png']).toBeInstanceOf(Promise);
 expect((await item.items['image/png']).type).toBe('image/png');
 expect(await result).toBe('copied'); expect(document.querySelector('dialog')).toBeNull();
});
it.each(['absent', 'denied'])('offers native image copying when clipboard is %s', async kind => {
 if (kind === 'absent') Object.defineProperty(navigator, 'clipboard', { configurable: true, value: undefined });
 else navigator.clipboard.write.mockRejectedValue(new Error('NotAllowedError'));
 expect(await copyChartPng(svg, { title: '<unsafe>' })).toBe('manual');
 const dialog = document.querySelector('dialog'); expect(dialog.open).toBe(true);
 expect(dialog.querySelector('h2').textContent).toBe('<unsafe>'); expect(dialog.querySelector('unsafe')).toBeNull();
 expect(dialog.querySelector('img').src).toBe('blob:chart');
 expect(dialog.textContent).toContain('Copy Image');
 dialog.querySelector('button').click(); expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:chart'); expect(document.querySelector('dialog')).toBeNull();
});
it('reports rasterization failures honestly', async () => {
 navigator.clipboard.write.mockRejectedValue(new Error('denied'));
 HTMLCanvasElement.prototype.toBlob.mockImplementation(cb => cb(null));
 expect(await copyChartPng(svg)).toBe('failed');
});
