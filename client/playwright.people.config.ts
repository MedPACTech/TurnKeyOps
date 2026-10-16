import { defineConfig, devices } from '@playwright/test';
const baseURL = process.env.PEOPLE_UI_BASE_URL ?? 'http://127.0.0.1:5191';
const port = new URL(baseURL).port;
export default defineConfig({
 testDir:'./tests/people',testMatch:'*.spec.ts',workers:1,outputDir:'test-results/people/artifacts',
 use:{baseURL,...devices['Desktop Chrome'],screenshot:'only-on-failure'},
 webServer:[
  {command:'node tests/people/mock-api.mjs',url:'http://127.0.0.1:5190/health'},
  {command:`npm run dev -- --host 127.0.0.1 --port ${port} --strictPort`,url:`${baseURL}/auth/login`,env:{PUBLIC_TKO_API_BASE_URL:'http://127.0.0.1:5190',TKO_API_BASE_URL:'http://127.0.0.1:5190'}}
 ]
});
