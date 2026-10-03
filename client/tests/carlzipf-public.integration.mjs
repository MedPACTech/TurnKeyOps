/**
 * Real-server Carl Zipf public intake verification. No API mocking.
 * Start the API with local Azurite and the client pointing at that API, then run:
 * node tests/carlzipf-public.integration.mjs
 * Optional CARLZIPF_CLIENT_URL / CARLZIPF_API_URL override local defaults.
 * Set CARLZIPF_WAIT_FOR_WINDOW=1 after recent public traffic; scenarios wait
 * for the shared rate limit. CARLZIPF_RETRY_ONLY=1 runs only photo recovery.
 * CARLZIPF_CALLBACK_ONLY=1 runs the callback path alone.
 * Keep other public-intake tests idle while this runs (about 3–4 minutes).
 * Creates four clearly named verification requests in the local Carl Zipf tenant.
 */
import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
const client = process.env.CARLZIPF_CLIENT_URL ?? 'http://127.0.0.1:5189';
const api = process.env.CARLZIPF_API_URL ?? 'http://127.0.0.1:5188';
for (const url of [client, api]) assert.ok(['127.0.0.1', 'localhost'].includes(new URL(url).hostname), 'Run only against local verification servers.');
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');
const browser = await chromium.launch({ headless: true });
try {
 const page = await browser.newPage();
 // The public API permits six requests/minute across creation and uploads.
 let firstScenario = process.env.CARLZIPF_WAIT_FOR_WINDOW !== '1';
 for (const [jobType, corruptFirst] of [['residential', false], ['commercial', false], ['commercial', true]]) {
  if (process.env.CARLZIPF_CALLBACK_ONLY === '1') continue;
  if (process.env.CARLZIPF_RETRY_ONLY === '1' && !corruptFirst) continue;
  if (!firstScenario) { console.log('Waiting for the public intake rate-limit window…'); await new Promise((resolve) => setTimeout(resolve, 61_000)); }
  firstScenario = false;
  await page.goto(`${client}/carlzipf/public`);
  await page.waitForLoadState('networkidle');
  let id = await page.locator('input[name=submissionId]').inputValue();
  const name = `Verification Carl Zipf ${id.slice(0, 8)}`;
  const details = 'Local integration verification: inspect front entrance.';
  await page.locator(`input[name=jobType][value=${jobType}]`).check();
  for (const [field, value] of Object.entries({ name, email: 'verification@example.invalid', phone: '614-555-0199', address: '161 East Fifth Avenue, Columbus, OH 43201', details, openings: '2', preferredDate: '2026-11-03' })) await page.locator(`[name=${field}]`).fill(value);
  await page.locator('[name=service]').selectOption('repair');
  await page.locator('[name=preferredTime]').selectOption('morning');
  const file = { name: 'entrance.png', mimeType: 'image/png', buffer: corruptFirst ? Buffer.from('not an image') : png };
  await page.locator('[name=photos]').setInputFiles(file);
  id = await page.locator('input[name=submissionId]').inputValue();
  await page.getByRole('button', { name: 'Send service request' }).click();
  if (corruptFirst) {
   await page.getByRole('alert').waitFor();
   assert.match(await page.getByRole('alert').innerText(), /request was saved.*photo upload was not confirmed/i);
   assert.equal(await page.locator('input[name=submissionId]').inputValue(), id);
   assert.equal(await page.locator('input[name=jobType]:checked').inputValue(), jobType);
   assert.equal(await page.locator('[name=details]').inputValue(), details);
   assert.ok(!page.url().includes('submitted=1'), 'Photo failure must not show success.');
   await page.locator('[name=photos]').setInputFiles({ ...file, buffer: png });
   await page.getByRole('button', { name: 'Send service request' }).click();
  }
  await page.waitForURL(/submitted=1&reference=/, { timeout: 25_000 });
  await expect(page.getByRole('status')).toContainText(id.slice(0, 8).toUpperCase());
  const receipt = await page.getByRole('status').innerText();
  assert.ok(receipt.includes(id.slice(0, 8).toUpperCase()));
  assert.match(receipt, /appointment has not been booked/i);
  // Replay the original known request ID and contact/scope to retrieve the persisted
  // repository record through the API's idempotency path, not the UI's URL flag.
  const payload = { id, companyName: name, contactName: name, email: 'verification@example.invalid', phone: '614-555-0199', siteName: '161 East Fifth Avenue, Columbus, OH 43201', serviceAddress: '161 East Fifth Avenue, Columbus, OH 43201', propertyType: jobType, serviceType: 'repair', requestedTimeline: '2026-11-03 / morning', priority: 'standard', attachments: [], need: `Job type: ${jobType}\nService: repair\nOpenings: 2\n${details}\nCustomer requests an assessment visit. Appointment preference only; office confirmation required.` };
  console.log(`Receipt confirmed${corruptFirst ? ' after photo retry' : ''}: ${id}`);
  let response = await page.request.post(`${api}/api/public/quote-requests/carlzipf`, { data: payload });
  if (response.status() === 429) {
   console.log('Waiting to read persisted request after rate limiting…');
   await new Promise((resolve) => setTimeout(resolve, 61_000));
   response = await page.request.post(`${api}/api/public/quote-requests/carlzipf`, { data: payload });
  }
  assert.equal(response.status(), 201, await response.text());
  const envelope = await response.json();
  const saved = envelope.data;
  assert.equal(saved.id, id);
  assert.equal(saved.tenantId, '88888888-8888-4888-8888-888888888883');
  assert.equal(saved.propertyType, jobType);
  assert.equal(saved.status, 'new');
  assert.equal(saved.requestedTimeline, '2026-11-03 / morning');
  assert.ok(!saved.siteVisitSchedule);
  assert.equal(saved.attachments.length, 1);
  assert.equal(saved.attachments[0].contentType, 'image/png');
  assert.ok(saved.attachments[0].blobName);
  console.log(`PASS durable ${jobType} request + photo${corruptFirst ? ' after failed-upload retry' : ''}: ${id}`);
 }
 if (process.env.CARLZIPF_RETRY_ONLY !== '1') {
  if (!firstScenario) { console.log('Waiting for callback verification window…'); await new Promise(resolve => setTimeout(resolve, 61_000)); }
  await page.goto(`${client}/carlzipf/public`);
  await page.waitForLoadState('networkidle');
  await page.getByLabel('Call me first').check();
  await expect(page.locator('[name=preferredDate]')).toHaveCount(0);
  for (const [field, value] of Object.entries({ name: 'Verification callback only', email: 'verification@example.invalid', phone: '614-555-0199', address: '161 East Fifth Avenue', details: 'Please discuss the repair first.' })) await page.locator(`[name=${field}]`).fill(value);
  await page.locator('[name=service]').selectOption('repair');
  await page.locator('[name=photos]').setInputFiles({name: 'callback.png', mimeType: 'image/png', buffer: png});
  const id = await page.locator('input[name=submissionId]').inputValue();
  await page.getByRole('button', {name: 'Send service request'}).click();
  await page.waitForURL(/submitted=1&reference=/, {timeout: 25000});
  await expect(page.getByRole('status')).toContainText(id.slice(0,8).toUpperCase());
  const response = await page.request.post(`${api}/api/public/quote-requests/carlzipf`, {data: {id, companyName: 'Verification callback only', contactName: 'Verification callback only', email: 'verification@example.invalid', phone: '614-555-0199', serviceAddress: '161 East Fifth Avenue', siteName: '161 East Fifth Avenue', propertyType: 'residential', serviceType: 'repair', requestedTimeline: 'Callback requested before arranging an assessment', priority: 'standard', attachments: [], need: 'Job type: residential\nService: repair\nOpening count to be assessed\nPlease discuss the repair first.\nCustomer requests a callback before arranging an assessment. No appointment requested.'}});
  assert.equal(response.status(),201,await response.text());
  const saved=(await response.json()).data;
  assert.equal(saved.id,id);
  assert.equal(saved.requestedTimeline,'Callback requested before arranging an assessment');
  assert.match(saved.need,/No appointment requested/);
  assert.ok(!saved.siteVisitSchedule);
  assert.equal(saved.attachments.length,1);
  console.log(`PASS durable callback request + photo without appointment: ${id}`);
 }
 for (const [label, overrides, message] of [['invalid contact preference', { requestMode: 'book-now' }, 'assessment request or a callback'], ['invalid job type', { jobType: 'industrial' }, 'property type'], ['impossible date', { preferredDate: '2026-02-30' }, 'valid preferred date'], ['oversized details', { details: 'x'.repeat(3501) }, '3,500']]) {
  const response = await page.request.post(`${client}/carlzipf/public?/quote`, { form: { name: 'Validation only', email: 'verification@example.invalid', phone: '614-555-0199', address: '161 East Fifth Avenue', service: 'repair', jobType: 'residential', ...overrides }, headers: { origin: client, accept: 'application/json', 'x-sveltekit-action': 'true' } });
  const result = await response.json();
  assert.equal(result.type, 'failure'); assert.equal(result.status, 400); assert.ok(result.data.includes(message));
  console.log(`PASS ${label} rejected before submission`);
 }
} finally { await browser.close(); }
