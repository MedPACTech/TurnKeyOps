import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
 testDir:'./tests/people',testMatch:'*.spec.ts',workers:1,outputDir:'test-results/people/artifacts',
 use:{baseURL:'http://127.0.0.1:5191',...devices['Desktop Chrome'],screenshot:'only-on-failure'},
 webServer:[
  {command:'node tests/people/mock-api.mjs',url:'http://127.0.0.1:5190/health'},
  {command:'npm run dev -- --host 127.0.0.1 --port 5191',url:'http://127.0.0.1:5191/auth/login',env:{PUBLIC_TKO_API_BASE_URL:'http://127.0.0.1:5190',TKO_API_BASE_URL:'http://127.0.0.1:5190'}}
 ]
});
