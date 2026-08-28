import { defineConfig, devices } from '@playwright/test';

const testSecrets = {
	IBeam__Identity__Jwt__SigningKey: '0123456789abcdef0123456789abcdef',
	IBeam__Identity__Otp__HashSalt: '0123456789abcdef0123456789abcdef',
	IBeam__Identity__Otp__VerificationTokenSecret: '0123456789abcdef0123456789abcdef',
	IBeam__Communications__Email__Providers__AzureCommunications__ConnectionString:
		'endpoint=https://local.invalid/;accesskey=0123456789abcdef0123456789abcdef',
	IBeam__Communications__Sms__Providers__AzureCommunications__ConnectionString:
		'endpoint=https://local.invalid/;accesskey=0123456789abcdef0123456789abcdef',
	OpenAISettings__Key: 'test-key'
};

export default defineConfig({
	testDir: './tests/e2e',
	outputDir: 'test-results/artifacts',
	fullyParallel: false,
	workers: 1,
	retries: 0,
	reporter: process.env.CI
		? [
				['line'],
				['junit', { outputFile: 'test-results/e2e-results.xml' }],
				['html', { outputFolder: 'test-results/html', open: 'never' }]
			]
		: [['list'], ['html', { outputFolder: 'test-results/html', open: 'never' }]],
	use: {
		baseURL: 'http://127.0.0.1:5189',
		trace: 'retain-on-failure',
		screenshot: 'only-on-failure',
		video: 'retain-on-failure'
	},
	projects: [
		{
			name: 'chromium',
			use: { ...devices['Desktop Chrome'] }
		},
		{
			name: 'mobile-chromium',
			use: { ...devices['Pixel 7'] },
			grep: /public pages remain accessible on mobile/
		}
	],
	webServer: [
		{
			command:
				'azurite --silent --location /tmp/turnkeyops-playwright-azurite --blobHost 127.0.0.1 --blobPort 10000 --queueHost 127.0.0.1 --queuePort 10001 --tableHost 127.0.0.1 --tablePort 10002',
			url: 'http://127.0.0.1:10000/devstoreaccount1',
			timeout: 30_000,
			reuseExistingServer: false
		},
		{
			command:
				'dotnet run --project ../api/TurnKeyOps.API/TurnKeyOps.API.csproj --no-restore --no-launch-profile',
			url: 'http://127.0.0.1:5188',
			timeout: 120_000,
			reuseExistingServer: false,
			env: {
				...process.env,
				...testSecrets,
				ASPNETCORE_ENVIRONMENT: 'Test',
				ASPNETCORE_URLS: 'http://127.0.0.1:5188'
			}
		},
		{
			command: 'npm run dev -- --host 127.0.0.1 --port 5189',
			url: 'http://127.0.0.1:5189/bdr/public',
			timeout: 120_000,
			reuseExistingServer: false,
			env: {
				...process.env,
				PUBLIC_TKO_API_BASE_URL: 'http://127.0.0.1:5188',
				TKO_API_BASE_URL: 'http://127.0.0.1:5188'
			}
		}
	]
});
