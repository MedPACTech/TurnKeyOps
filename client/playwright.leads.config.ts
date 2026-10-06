import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
 testDir:'./tests/leads-browser', testMatch:'*.spec.ts',fullyParallel:false,workers:1,
 outputDir:'test-results/leads',reporter:'list',use:{baseURL:'http://127.0.0.1:5299',screenshot:'only-on-failure',trace:'retain-on-failure'},
 projects:[{name:'desktop',use:{...devices['Desktop Chrome']}},{name:'mobile',use:{...devices['Pixel 7']}}],
 webServer:[{command:'node tests/leads-browser/fixture-api.mjs',url:'http://127.0.0.1:5298/api/auth/session',reuseExistingServer:false},
 {command:'npm run dev -- --host 127.0.0.1 --port 5299',url:'http://127.0.0.1:5299/bdr/public',reuseExistingServer:false,env:{TKO_API_BASE_URL:'http://127.0.0.1:5298',PUBLIC_TKO_API_BASE_URL:'http://127.0.0.1:5298'}}]
});
