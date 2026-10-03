/** Real-server checks. No API mocking. Run from client:
 * node tests/carlzipf-review.integration.mjs
 * Optional CARLZIPF_REVIEW_FIXTURE=/absolute/local.json enables mutation tests
 * on two LOCAL demo quotes: { "approveUrl": "/carlzipf/estimate/...?...",
 * "changesUrl": "/carlzipf/estimate/...?..." }. Links must be independently issued.
 * Mutation tests require CARLZIPF_ISOLATED_MUTATIONS=1 after verifying that
 * outbound services are disabled and storage/auth are isolated local fixtures.
 * Localhost alone does not establish isolation. No OTP/login actions are performed.
 */
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { chromium, expect } from '@playwright/test';
const client = process.env.CARLZIPF_CLIENT_URL ?? 'http://127.0.0.1:5189';
const api = process.env.CARLZIPF_API_URL ?? 'http://127.0.0.1:5188';
for (const url of [client, api]) assert.ok(['localhost', '127.0.0.1'].includes(new URL(url).hostname), 'Local test servers only');
const browser = await chromium.launch({headless:true});
try {
 let page = await browser.newPage();
 const missing = '/carlzipf/estimate/11111111-1111-4111-8111-111111111111';
 for (const path of [missing, `${missing}?token=invalid`]) {
  const response = await page.goto(client + path);
  assert.equal(response.status(),404);
  await expect(page.getByRole('button', {name:'Sign & approve quote'})).toHaveCount(0);
 }
 const invalidConsent = await page.request.post(`${client}${missing}?/approve`, {form:{accessToken:'invalid'},headers:{origin:client,accept:'application/json','x-sveltekit-action':'true'}});
 const failed = await invalidConsent.json();
 assert.equal(failed.type,'failure');assert.equal(failed.status,400);assert.ok(failed.data.includes('intent to electronically sign'));
 for (const tenant of ['carlzipf', 'bdr']) {
  for (const [values, message] of [
   [{intentToSign:'yes', signerPrintedName:''}, 'printed name'],
   [{intentToSign:'yes', signerPrintedName:'Demo Customer', revisionNumber:'0'}, 'signing details']
  ]) {
   const response=await page.request.post(`${client}/${tenant}/estimate/11111111-1111-4111-8111-111111111111?/approve`, {form:{accessToken:'invalid',...values},headers:{origin:client,accept:'application/json','x-sveltekit-action':'true'}});
   const result=await response.json();assert.equal(result.type,'failure');assert.equal(result.status,400);assert.ok(result.data.includes(message));
  }
 }
 console.log('PASS missing/invalid review links rejected; Carl Zipf and BDR require signature intent, printed name, and revision metadata');
 if (!process.env.CARLZIPF_REVIEW_FIXTURE) console.log('Issued-quote mutation tests skipped: provide CARLZIPF_REVIEW_FIXTURE for local demo quotes.');
 else {
  assert.equal(process.env.CARLZIPF_ISOLATED_MUTATIONS, '1', 'Issued-quote tests require explicitly verified isolated storage/auth and disabled outbound services.');
  await page.close();
  page = await browser.newPage({javaScriptEnabled:false});
  const fixture=JSON.parse(await readFile(process.env.CARLZIPF_REVIEW_FIXTURE,'utf8'));
  for (const [key, decision] of [['approveUrl','approved'],['changesUrl','changes-requested']]) {
   const url=new URL(fixture[key],client);
   assert.equal(url.origin,new URL(client).origin);assert.ok(url.pathname.startsWith('/carlzipf/estimate/'));
   const response=await page.goto(url.href);
   assert.equal(response.status(),200);
   assert.match(response.headers()['cache-control'],/no-store/);
   assert.equal(response.headers()['referrer-policy'],'no-referrer');
   await expect(page.getByAltText('Carl Zipf Lock Shop')).toBeVisible();
   await expect(page.getByRole('heading',{name:'Doors & hardware'})).toBeVisible();
   await expect(page.getByText(/Concrete|CY$/)).toHaveCount(0);
   await page.setViewportSize({width:390,height:844});
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
   if(decision==='approved') {
    await page.getByLabel('Your printed name').fill('Demo Customer — local test');
    await page.locator('[name=intentToSign]').check();
    await page.getByRole('button',{name:'Sign & approve quote'}).click();
    await expect(page.getByRole('status')).toContainText('Quote approved');
   } else {
    await page.getByText('Something needs to change?').click();
    await page.getByLabel('Tell us what to adjust').fill('Local verification: please revise the hardware finish.');
    await page.getByRole('button',{name:'Request changes'}).click();
    await expect(page.getByRole('status')).toContainText('Changes requested');
   }
   const id=url.pathname.split('/').pop();
   const savedResponse=await page.request.get(`${api}/api/public/quote-estimates/carlzipf/${id}?token=${encodeURIComponent(url.searchParams.get('token'))}`);
   assert.equal(savedResponse.status(),200);
   const saved=(await savedResponse.json()).data;
   assert.equal(saved.delivery.status,decision);
   if (decision==='approved') {
    assert.equal(saved.approvalSignature.signerPrintedName,'Demo Customer — local test');
    assert.equal(saved.approvalSignature.revisionNumber,saved.revisionNumber);
    assert.equal(saved.approvalSignature.documentHash,saved.documentHash);
    assert.equal(saved.approvalSignature.method,'typed-name');
    assert.ok(saved.approvalSignature.signedAtUtc);
   }
   if(decision==='changes-requested') assert.equal(saved.delivery.responseNote,'Local verification: please revise the hardware finish.');
   await page.goto(url.href);
   await expect(page.getByRole('status')).toContainText(decision==='approved'?'Quote approved':'Changes requested');
   await expect(page.getByRole('button',{name:'Sign & approve quote'})).toHaveCount(0);
   if (decision==='approved') await expect(page.getByRole('status')).toContainText('Electronically signed by Demo Customer — local test');
   console.log(`PASS persisted ${decision} for local quote ${id}; mobile layout, no-JavaScript forms, and private response headers`);
  }
 }
} finally {await browser.close();}
