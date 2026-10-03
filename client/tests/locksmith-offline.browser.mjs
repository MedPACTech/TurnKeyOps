// Run with: node --test tests/locksmith-offline.browser.mjs
// Uses a local API fixture; it does not weaken production authentication.
import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { chromium } from '@playwright/test';

test('field draft photos survive offline saves, conflicts and account isolation; PWA caches no private data', { timeout: 120_000 }, async () => {
	const tenantId = '88888888-8888-4888-8888-888888888883';
	const sourceCalls = []; const estimateInputs = []; let serverEstimate = null; let uploadCalls = 0; let sendCalls = 0;
	const encode = (value) => Buffer.from(JSON.stringify(value)).toString('base64url');
	const adminToken = `${encode({ alg: 'none' })}.${encode({ role: 'owner', tenant_id: tenantId, email: 'admin@example.test', exp: Math.floor(Date.now() / 1000) + 3600 })}.local-fixture`;
	const api = createServer(async (request, response) => {
		response.setHeader('Content-Type', 'application/json');
		if (!['Bearer field-test-only', `Bearer ${adminToken}`].includes(request.headers.authorization)) { response.writeHead(401); response.end('{}'); return; }
		const ok = (data) => response.end(JSON.stringify({ success: true, data }));
		if (request.url === '/api/auth/session') return ok({});
		if (request.url === '/api/quote-estimates') return ok(serverEstimate ? [serverEstimate] : []);
		if (request.url === '/api/locksmith/me') return ok({ tenantId, userId: 'fixture-tech', capabilities: ['residential', 'commercial'] });
		if (request.url === '/api/quote-estimates/locksmith-context') return ok({ jobTypes: ['residential', 'commercial'], catalog: [{ id: 'configured-door', name: 'Configured entry door', jobTypes: ['residential'], unitPrice: 500, sample: false }], policyVersion: 'policy-1', laborRatePerHour: 100, taxPercent: 7 });
		let raw = ''; for await (const chunk of request) raw += chunk;
		if (request.url === '/api/quote-requests/field/carlzipf') { sourceCalls.push(JSON.parse(raw)); return ok({ id: sourceCalls[0].id }); }
		if (request.url?.endsWith('/office-approval')) { assert.equal(JSON.parse(raw).expectedVersion, serverEstimate.version); serverEstimate.status = 'ready-to-send'; serverEstimate.version = 'estimate-v2'; serverEstimate.locksmithPricing.officeApprovedAtUtc = new Date().toISOString(); return ok(serverEstimate); }
		if (request.url?.endsWith('/send')) { assert.equal(JSON.parse(raw).expectedVersion, serverEstimate.version); sendCalls++; serverEstimate.status = 'sent'; serverEstimate.version = 'estimate-v3'; serverEstimate.expiresAtUtc = new Date(Date.now() + 86400_000).toISOString(); serverEstimate.delivery = { status: 'sent', method: 'review-link', reviewUrl: `/carlzipf/estimate/${serverEstimate.quoteRequestId}?token=${'a'.repeat(64)}`, sentAtUtc: new Date().toISOString() }; return ok(serverEstimate); }
		if (request.url?.startsWith('/api/quote-estimates/')) {
			if (request.method === 'GET') { if (serverEstimate) return ok(serverEstimate); response.writeHead(404); return response.end('{}'); }
			estimateInputs.push(JSON.parse(raw));
			if (estimateInputs.length === 1) { response.writeHead(503); return response.end(JSON.stringify({ message: 'Temporary pricing interruption' })); }
			serverEstimate = { id: sourceCalls[0].id, quoteRequestId: sourceCalls[0].id, version: 'estimate-v1', revisionNumber: 1, status: 'draft', customerName: 'Field customer', siteName: '123 Main Street', savedAtUtc: new Date().toISOString(), locksmithPricing: { jobType: 'residential', laborHours: 1, laborRatePerHour: 100, discountPercent: 0, openings: estimateInputs[1].locksmith.openings, lines: [{ catalogItemId: 'configured-door', name: 'Configured entry door', openingName: 'Front entry offline', quantity: 1, unitPrice: 500, total: 500 }], total: 642, subtotal: 600, discountAmount: 0, taxAmount: 42, taxPercent: 7, requiresOfficeApproval: true, approvalReasons: ['Office approval required'], policyVersion: 'policy-1' } };
			return ok(serverEstimate);
		}
		if (request.url?.endsWith('/attachments') && request.method === 'POST') { uploadCalls++; return ok([{ id: 'attachment-1' }]); }
		if (request.url?.startsWith('/api/quote-requests/')) return ok({ propertyType: 'residential' });
		response.writeHead(404); response.end('{}');
	});
	api.listen(0, '127.0.0.1'); await once(api, 'listening');
	const origin = 'http://127.0.0.1:5197';
	const vite = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', '5197', '--strictPort'], { env: { ...process.env, TKO_API_BASE_URL: `http://127.0.0.1:${api.address().port}`, PUBLIC_TKO_API_BASE_URL: `http://127.0.0.1:${api.address().port}` }, stdio: ['ignore', 'pipe', 'pipe'] });
	let logs = ''; vite.stdout.on('data', (data) => { logs += data; }); vite.stderr.on('data', (data) => { logs += data; });
	let browser;
	try {
		let ready = false;
		for (let attempt = 0; attempt < 100; attempt++) { try { if ((await fetch(`${origin}/carlzipf/tech/pwa/offline.html`)).ok) { ready = true; break; } } catch {} await new Promise((resolve) => setTimeout(resolve, 100)); }
		assert.ok(ready, logs);
		browser = await chromium.launch({ headless: true });
		const browserContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
		await browserContext.addCookies([{ name: 'tko_auth_token', value: 'field-test-only', url: origin }]);
		const page = await browserContext.newPage();
		const errors = []; page.on('pageerror', (error) => { errors.push(error.message); console.error('Browser error:', error.message); });
		await page.goto(`${origin}/carlzipf/tech`);
		await page.waitForFunction(() => [...document.querySelectorAll('button')].some((button) => button.textContent.includes('Save draft & photos on device') && !button.disabled));
		await page.getByRole('button', { name: '+ Add opening', exact: true }).click();
		await page.getByLabel('Opening name', { exact: true }).fill('Front entry');
		await page.getByLabel('Door slab width', { exact: true }).fill('36');
		await page.getByLabel('Opening photos', { exact: true }).setInputFiles({ name: 'door.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a2ioAAAAASUVORK5CYII=', 'base64') });
		await page.getByRole('button', { name: 'Save draft & photos on device', exact: true }).click();
		await page.getByText(/Saved 1 photo\(s\) and measurements on this device/).waitFor();
		await page.evaluate(() => navigator.serviceWorker.ready.then(() => undefined));
		assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'mobile layout should not overflow');
		await browserContext.setOffline(true);
		await page.getByLabel('Opening name', { exact: true }).fill('Front entry offline');
		await page.getByRole('button', { name: 'Save draft & photos on device', exact: true }).click();
		await page.getByText(/Saved 1 photo\(s\) and measurements on this device/).waitFor();
		await page.reload();
		await page.getByRole('heading', { name: 'Reconnect to reopen your workspace' }).waitFor();
		await browserContext.setOffline(false);
		await page.getByRole('link', { name: 'Try connecting again' }).click();
		await page.getByRole('button', { name: 'Restore draft', exact: true }).click();
		assert.equal(await page.getByLabel('Opening name', { exact: true }).inputValue(), 'Front entry offline');
		assert.equal(await page.getByRole('img', { name: 'Opening photo: door.png', exact: true }).count(), 1);
		await page.getByLabel('Customer / company', { exact: true }).fill('Field customer');
		await page.getByLabel('Property / site', { exact: true }).fill('123 Main Street');
		await page.getByLabel('Customer email', { exact: true }).fill('field@example.test');
		await page.getByLabel('Customer phone', { exact: true }).fill('555-0100');
		await page.getByLabel('Labor hours', { exact: true }).fill('1');
		await page.getByRole('button', { name: 'Add catalog item', exact: true }).click();
		await page.getByRole('button', { name: 'Prepare server quote draft', exact: true }).click();
		await page.getByText(/The customer request was saved, but detailed quote preparation was not confirmed/).waitFor();
		await page.getByRole('button', { name: 'Prepare server quote draft', exact: true }).click();
		await page.getByText('Server-priced estimate draft saved. It has not been issued, emailed, signed, or scheduled.', { exact: true }).waitFor();
		await page.getByText('1 photo(s) uploaded to the shared request. Device copies remain available.', { exact: true }).waitFor();
		assert.equal(sourceCalls.length, 2); assert.deepEqual(sourceCalls[0], sourceCalls[1]);
		assert.equal(estimateInputs.length, 2); assert.equal(estimateInputs[1].locksmith.openings[0].measurements.slabWidth.value, '36');
		assert.equal(JSON.stringify(estimateInputs[1]).includes('unitPrice'), false); assert.equal(uploadCalls, 2);
		const recovery = await page.evaluate(async () => {
			const module = await import('/src/lib/locksmith-offline.ts');
			const saved = await module.loadDeviceDraft({ tenantId: '88888888-8888-4888-8888-888888888883', userId: 'fixture-tech', capabilities: ['residential', 'commercial'] });
			delete saved.draft.handoff.estimateVersion;
			return (await fetch('/carlzipf/tech/handoff', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(saved.draft) })).json();
		});
		assert.equal(recovery.recovered, true); assert.equal(estimateInputs.length, 2, 'uncertain retry must recover rather than overwrite the server packet');
		const crossSite = await page.request.post(`${origin}/carlzipf/tech/handoff`, { headers: { Origin: 'https://other.example' }, data: {} });
		assert.equal(crossSite.status(), 403);
		const adminContext = await browser.newContext();
		await adminContext.addCookies([{ name: 'tko_auth_token', value: adminToken, url: origin }]);
		const adminPage = await adminContext.newPage();
		await adminPage.goto(`${origin}/carlzipf/admin/estimates?request=${serverEstimate.quoteRequestId}`);
		await adminPage.getByRole('checkbox').check();
		await adminPage.getByRole('button', { name: 'Approve pricing and mark ready', exact: true }).click();
		await adminPage.getByRole('button', { name: 'Issue customer review link', exact: true }).waitFor();
		await page.getByRole('button', { name: 'Refresh quote & customer status', exact: true }).click();
		await page.getByRole('button', { name: 'Issue customer review link', exact: true }).waitFor();
		assert.equal(await page.getByRole('button', { name: 'Issue customer review link', exact: true }).isEnabled(), true);
		await page.getByLabel('Door slab width', { exact: true }).fill('37');
		assert.equal(await page.getByRole('button', { name: 'Issue customer review link', exact: true }).isDisabled(), true);
		await page.getByLabel('Door slab width', { exact: true }).fill('36');
		await adminPage.getByRole('checkbox').check();
		await adminPage.getByRole('button', { name: 'Issue customer review link', exact: true }).click();
		await adminPage.getByText('Customer review link issued. Copy or open it below; no email or text was sent.', { exact: true }).waitFor();
		await page.getByRole('button', { name: 'Refresh quote & customer status', exact: true }).click();
		const customerLink = page.getByRole('link', { name: 'Open for customer on this device', exact: true });
		await customerLink.waitFor();
		assert.equal(await customerLink.getAttribute('href'), serverEstimate.delivery.reviewUrl);
		assert.equal(await customerLink.getAttribute('rel'), 'noopener noreferrer');
		const repeatIssue = await page.evaluate(async ({ requestId }) => (await fetch('/carlzipf/tech/quote', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ requestId, expectedVersion: 'estimate-v2' }) })).json(), { requestId: serverEstimate.quoteRequestId });
		assert.equal(repeatIssue.recovered, true); assert.equal(sendCalls, 1);
		serverEstimate.delivery.status = 'approved';
		await page.getByRole('button', { name: 'Refresh quote & customer status', exact: true }).click();
		await page.getByRole('heading', { name: 'Customer approved this revision', exact: true }).waitFor();
		await adminContext.close();
		const storage = await page.evaluate(async () => {
			const module = await import('/src/lib/locksmith-offline.ts');
			const identity = { tenantId: '88888888-8888-4888-8888-888888888883', userId: 'fixture-tech', capabilities: ['residential', 'commercial'] };
			const saved = await module.loadDeviceDraft(identity);
			const bytes = (await saved.photos[0].blob.arrayBuffer()).byteLength;
			const otherAccount = await module.loadDeviceDraft({ ...identity, userId: 'other-tech' });
			const otherTenant = await module.loadDeviceDraft({ ...identity, tenantId: 'other-tenant' });
			let deniedCapability = false;
			try { await module.loadDeviceDraft({ ...identity, capabilities: ['commercial'] }); } catch { deniedCapability = true; }
			const writes = await Promise.allSettled([module.saveDeviceDraft(identity, saved.draft, saved.photos, saved.revision, saved.catalogVersion), module.saveDeviceDraft(identity, saved.draft, saved.photos, saved.revision, saved.catalogVersion)]);
			let deleteConflict = false;
			try { await module.deleteDeviceDraft(identity, saved.revision); } catch (error) { deleteConflict = error.name === 'DraftConflictError'; }
			const cached = []; for (const name of await caches.keys()) for (const request of await (await caches.open(name)).keys()) cached.push(new URL(request.url).pathname);
			return { bytes, otherAccount, otherTenant, deniedCapability, writes: writes.map((result) => result.status), deleteConflict, cached };
		});
		assert.ok(storage.bytes > 0); assert.equal(storage.otherAccount, null); assert.equal(storage.otherTenant, null); assert.equal(storage.deniedCapability, true);
		assert.deepEqual(storage.writes.sort(), ['fulfilled', 'rejected']); assert.equal(storage.deleteConflict, true);
		assert.deepEqual(storage.cached.sort(), ['/carlzipf/tech/pwa/icon-192.png', '/carlzipf/tech/pwa/icon-512.png', '/carlzipf/tech/pwa/offline.html']);
		await page.getByLabel('Opening name', { exact: true }).fill('Stale tab change');
		await page.getByRole('button', { name: 'Save draft & photos on device', exact: true }).click();
		await page.getByText('Another tab changed this device draft.', { exact: true }).waitFor();
		assert.equal(await page.getByRole('button', { name: 'Save draft & photos on device', exact: true }).isDisabled(), true);
		assert.deepEqual(errors, []);
	} finally { await browser?.close(); vite.kill('SIGTERM'); await new Promise((resolve) => api.close(resolve)); }
});
