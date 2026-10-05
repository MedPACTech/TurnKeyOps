import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createServer } from 'vite';

// Reproduce deployment-wide tenant drift without connecting to an external API.
process.env.TKO_API_TENANT_ID = '88888888-8888-8888-8888-888888888882';
const server = await createServer({ server: { middlewareMode: true }, appType: 'custom', optimizeDeps: { noDiscovery: true, entries: [] } });
try {
 const { loadQuoteRequests, getQuoteRequestTenantId, submitQuoteRequest } = await server.ssrLoadModule('/src/lib/server/quote-requests.ts');
 const { bdrTenant, thinkPinkTenant } = await server.ssrLoadModule('/src/lib/config/tenants.ts');
 const { createQuoteRequestFromForm } = await server.ssrLoadModule('/src/lib/quote-requests.ts');
 const input = {
  id: '7909b5b4-0000-4000-8000-000000000001', tenantId: bdrTenant.id,
  companyName: 'Regression fixture', contactName: 'Regression fixture',
  email: 'regression@example.invalid', phone: '555-0100', siteName: 'Test site',
  serviceAddress: 'Test address', serviceType: 'Driveway', propertyType: 'Residential',
  requestedTimeline: 'Next month', priority: 'standard', need: 'Test scope', attachments: []
 };
 const saved = createQuoteRequestFromForm(input);
 const other = createQuoteRequestFromForm({ ...input, id: 'other-tenant', tenantId: thinkPinkTenant.id });
 const list = async () => Response.json({ success: true, data: [saved, other] });

 await test('BDR public receipt remains visible in its default admin queue despite a deployment tenant override', async () => {
  assert.equal(getQuoteRequestTenantId(), bdrTenant.id);
  const submitted = await submitQuoteRequest(async (url, options) => {
   assert.ok(url.endsWith('/api/public/quote-requests/bdr'));
   assert.equal(JSON.parse(options.body).id, input.id);
   return Response.json({ success: true, data: saved });
  }, input);
  const { requests } = await loadQuoteRequests(list);
  assert.deepEqual(requests.map(item => item.id), [submitted.id]);
  const { load } = await server.ssrLoadModule('/src/routes/bdr/admin/requests/+page.server.ts');
  const page = await load({ fetch: list });
  assert.equal(page.requests[0].id, submitted.id);
  assert.equal(page.metrics.total, 1);
 });

 await test('explicit tenant queues still exclude BDR requests', async () => {
  const { requests } = await loadQuoteRequests(list, thinkPinkTenant.id);
  assert.deepEqual(requests.map(item => item.id), ['other-tenant']);
 });
} finally {
 await server.close();
}
