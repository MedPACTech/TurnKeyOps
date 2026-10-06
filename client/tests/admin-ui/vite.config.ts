import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath } from 'node:url';
const local = (path: string) => fileURLToPath(new URL(path, import.meta.url));
export default defineConfig({
 root: local('./fixture/'), plugins: [svelte(), tailwindcss()],
 resolve: { alias: { '$lib': local('../../src/lib'), '$app/state': local('./fixture/app-state.ts'), '$app/navigation': local('./fixture/app-navigation.ts'), '$app/forms': local('./fixture/app-forms.ts'), '$app/environment': local('./fixture/app-environment.ts'), '$env/dynamic/public': local('./fixture/app-environment.ts') } },
 server: { host: '127.0.0.1', port: 5297, strictPort: true, fs: { allow: [local('../../')] } }
});
