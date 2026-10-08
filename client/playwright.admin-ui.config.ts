import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
 testDir: './tests/admin-ui', testMatch: '*.spec.ts', workers: 1, reporter: 'list',
 outputDir: 'test-results/admin-ui', use: { baseURL: 'http://127.0.0.1:5297', screenshot: 'only-on-failure', trace:'retain-on-failure' },
 projects: [{name:'desktop',use:{...devices['Desktop Chrome'],viewport:{width:1440,height:1000}}},{name:'mobile',use:{...devices['Pixel 7']}}],
 webServer: { command:'npx vite --config tests/admin-ui/vite.config.ts', url:'http://127.0.0.1:5297', reuseExistingServer:false }
});
