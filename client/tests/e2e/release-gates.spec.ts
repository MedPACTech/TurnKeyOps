import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

const tinyPng = {
	name: 'release-gate.png',
	mimeType: 'image/png',
	buffer: Buffer.from(
		'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
		'base64'
	)
};

const expectNoSeriousAccessibilityViolations = async (page: Page) => {
	const results = await new AxeBuilder({ page }).analyze();
	const blocking = results.violations.filter(
		(violation) => violation.impact === 'serious' || violation.impact === 'critical'
	);
	expect(blocking, JSON.stringify(blocking, null, 2)).toEqual([]);
};

test('BDR public intake persists an attachment for the BDR brand', async ({ page }) => {
	await page.goto('/bdr/public');
	await page.waitForLoadState('networkidle');
	await expect(page.locator('form[action="?/submitQuoteRequest"]')).toBeVisible();
	await page.locator('#contactName').fill('Release Gate BDR');
	await page.locator('#phone').fill('704-555-0198');
	await page.locator('#email').fill('release-bdr@example.invalid');
	await page.locator('#serviceType').selectOption({ label: 'Driveway' });
	await page.locator('#propertyType').selectOption({ label: 'Commercial' });
	await page.locator('#need').fill('Verify the release-gate public intake journey.');
	await page.locator('#attachments').setInputFiles(tinyPng);
	await page.locator('form[action="?/submitQuoteRequest"] button[type="submit"]').click();
	await expect(page).toHaveURL(/\/bdr\/public\?submitted=1&reference=/, { timeout: 20_000 });
	await expect(page.getByRole('status')).toContainText(/Reference [A-F0-9]{8}/);
});

test('Think Pink public intake persists an attachment for the Think Pink brand', async ({ page }) => {
	await page.goto('/thinkpink/public');
	await page.waitForLoadState('networkidle');
	await expect(page.locator('form[action="?/quote"]')).toBeVisible();
	await page.getByLabel('Name').fill('Release Gate Think Pink');
	await page.getByLabel('Phone').fill('614-555-0198');
	await page.getByLabel('Email').fill('release-thinkpink@example.invalid');
	await page.getByLabel('Property address').fill('14 Release Lane, Columbus, OH');
	await page.getByLabel('Approx. acreage').selectOption({ index: 1 });
	await page.getByLabel('Service needed').selectOption({ index: 1 });
	await page.getByLabel('Timeline').selectOption({ index: 1 });
	await page.locator('input[name="photos"]').setInputFiles(tinyPng);
	await expect(page.getByLabel('Name')).toHaveValue('Release Gate Think Pink');
	await expect(page.getByLabel('Phone')).toHaveValue('614-555-0198');
	await expect(page.getByLabel('Property address')).toHaveValue('14 Release Lane, Columbus, OH');
	await page.locator('form[action="?/quote"] button[type="submit"]').click();
	await expect(page).toHaveURL(/\/thinkpink\/public\?submitted=1&reference=/, { timeout: 20_000 });
	await expect(page.getByText('Got it!')).toBeVisible({ timeout: 20_000 });
	await expect(page.getByText(/request was saved as/i)).toBeVisible();
});

test('anonymous and cross-tenant authorization paths fail closed', async ({ page, request }) => {
	await page.goto('/bdr/admin/dashboard');
	await expect(page).toHaveURL(/\/auth\/login\?returnTo=%2Fbdr%2Fadmin%2Fdashboard/);

	await page.goto('/thinkpink/admin/dashboard');
	await expect(page).toHaveURL(/\/auth\/login\?returnTo=%2Fthinkpink%2Fadmin%2Fdashboard/);

	const apiResponse = await request.get('http://127.0.0.1:5188/api/quote-requests');
	expect(apiResponse.status()).toBe(401);

	const crossSiteMutation = await request.post('/bdr/public?/submitQuoteRequest', {
		headers: {
			Origin: 'https://attacker.example',
			'Content-Type': 'application/x-www-form-urlencoded'
		},
		data: 'contactName=Cross-site+probe'
	});
	expect(crossSiteMutation.status()).toBe(403);
});

test('public pages remain accessible on mobile', async ({ page }) => {
	for (const path of ['/bdr/public', '/thinkpink/public']) {
		await page.goto(path);
		await expect(page.locator('main')).toBeVisible();
		await expect(page.locator('body')).not.toHaveCSS('overflow-x', 'scroll');
		await expectNoSeriousAccessibilityViolations(page);
	}
});

test('OTP forms send once while pending and recover after delivery failure', async ({ page }) => {
	await page.goto('/auth/login?returnTo=%2Fbdr%2Fadmin%2Fbob');
	await page.waitForLoadState('networkidle');
	let sends = 0;
	let release!: () => void;
	const gate = new Promise<void>((resolve) => { release = resolve; });
	await page.route('**/auth/login?*/request', async (route) => {
		sends++;
		await gate;
		await route.fulfill({ status: 200, contentType: 'application/json',
			body: JSON.stringify({ type: 'failure', status: 400, data: '[{"step":1,"message":2,"identifier":3},"request","Test provider unavailable","6145550100"]' }) });
	});
	await page.getByLabel('Work email or mobile number').fill('6145550100');
	await page.locator('form[action="?/request"]').evaluate((form: HTMLFormElement) => {
		form.requestSubmit(); form.requestSubmit();
	});
	await expect.poll(() => sends).toBe(1);
	await expect(page.getByRole('button', { name: 'Sending…' })).toBeDisabled();
	release();
	await expect(page.getByText('Test provider unavailable')).toBeVisible();
	await expect(page.getByRole('button', { name: 'Send code' })).toBeEnabled();
	await page.getByRole('button', { name: 'Send code' }).click();
	await expect.poll(() => sends).toBe(2);
});

test('OTP resend is single-flight and refreshing the page does not resend', async ({ page }) => {
	await page.goto('/auth/login?returnTo=%2Fbdr%2Fadmin%2Fbob');
	await page.waitForLoadState('networkidle');
	let sends = 0;
	let release!: () => void;
	const gate = new Promise<void>((resolve) => { release = resolve; });
	await page.route('**/auth/login?*/request', async (route) => {
		sends++;
		if (sends > 1) await gate;
		await route.fulfill({ status: 200, contentType: 'application/json',
			body: JSON.stringify({ type: 'success', status: 200,
				data: '[{"step":1,"identifier":2,"otpState":3},"verify","6145550100",{"channel":4,"challengeId":5},"sms","test-challenge"]' }) });
	});
	await page.getByLabel('Work email or mobile number').fill('6145550100');
	await page.getByRole('button', { name: 'Send code', exact: true }).click();
	await expect(page.getByLabel('Verification code')).toBeVisible();
	await page.locator('form[action="?/request"]').evaluate((form: HTMLFormElement) => {
		form.requestSubmit(); form.requestSubmit();
	});
	await expect.poll(() => sends).toBe(2);
	await expect(page.getByRole('button', { name: 'Resend code' })).toBeDisabled();
	release();
	await expect(page.getByRole('button', { name: 'Resend code' })).toBeEnabled();
	await page.reload();
	await expect(page.getByRole('button', { name: 'Send code', exact: true })).toBeVisible();
	expect(sends).toBe(2);
});
