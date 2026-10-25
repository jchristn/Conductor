import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
const pkg = JSON.parse(readFileSync(fileURLToPath(new URL('./package.json', import.meta.url)), 'utf8'));
export default defineConfig({ plugins: [react()], define: { __APP_VERSION__: JSON.stringify(pkg.version) }, test: { environment: 'jsdom', setupFiles: ['./tests/setup.js'] } });
