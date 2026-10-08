import assert from 'node:assert/strict';
import { createServer } from 'vite';
import { createServer as createHttpServer } from 'node:http';
process.chdir(new URL('..', import.meta.url).pathname);
const hmrServer = createHttpServer();
const server = await createServer({
 configFile: false,
 root: new URL('..', import.meta.url).pathname,
 resolve: { alias: { $lib: new URL('../src/lib', import.meta.url).pathname } },
 optimizeDeps: { noDiscovery: true, include: [] },
 plugins: [{ name: 'test-environment', resolveId(id) { if (id.startsWith('$env/') || id === '$app/environment') return id; }, load(id) { if (id.startsWith('$env/')) return 'export const env = {};'; if (id === '$app/environment') return 'export const browser = false;'; } }],
 server: { middlewareMode: true, hmr: { server: hmrServer } }, appType: 'custom'
});
try {
 const { actions, load } = await server.ssrLoadModule('/src/routes/carlzipf/admin/website/+page.server.ts');
 const { normalizeCarlZipfContent } = await server.ssrLoadModule('/src/lib/server/carlzipf-site-content.ts');
 const { defaultCarlZipfContent } = await server.ssrLoadModule('/src/lib/carlzipf-site-content.ts');
 const calls = [];
 let persisted = { existingCarlZipfContent: { retained: true } };
 let version = '';
 const fetch = async (url, options = {}) => {
  calls.push({ url: String(url), options });
  if (options.method === 'PUT') {
   assert.match(String(url), /\/api\/admin\/tenant-settings\/public-content$/);
   assert.equal(new Headers(options.headers).get('Authorization'), 'Bearer carlzipf-test-token');
   const body = JSON.parse(options.body);
   assert.equal(body.expectedVersion, version || null);
   persisted = body.values; version = 'saved-v1';
  } else assert.match(String(url), /\/api\/public\/tenant-settings\/88888888-8888-4888-8888-888888888883\/content$/);
  return new Response(JSON.stringify({ values: persisted, version }), { headers: { 'Content-Type': 'application/json' } });
 };
 const initial = await load({ fetch });
 assert.equal(initial.version, '');
 const edited = structuredClone(initial.content);
 edited.text.more_than_locks = 'More Than Locks.';
 edited.commercialSeo.title = 'Carl Zipf commercial test title';
 const fields = new FormData(); fields.set('content', JSON.stringify(edited)); fields.set('version', '');
 const result = await actions.default({ request: new Request('http://localhost/carlzipf/admin/website', { method: 'POST', body: fields }), fetch, cookies: { get: () => 'carlzipf-test-token' } });
 assert.equal(result.saved, true);
 assert.equal(persisted.existingCarlZipfContent.retained, true);
 assert.equal(persisted.carlZipfSite.commercialSeo.title, edited.commercialSeo.title);
 for (const key of ['navigation','hero','services','quoteForm','footer']) assert.equal(typeof persisted[key], 'object');
 const saved = await load({ fetch });
 assert.equal(saved.content.commercialSeo.title, edited.commercialSeo.title);
 const stale = await actions.default({ request: new Request('http://localhost/carlzipf/admin/website', { method: 'POST', body: fields }), fetch, cookies: { get: () => 'carlzipf-test-token' } });
 assert.equal(stale.status, 409);
 const unsafe = structuredClone(defaultCarlZipfContent); unsafe.links.quote = 'javascript:alert(1)'; unsafe.images.hero = '//untrusted.invalid/picture.jpg';
 const normalized = normalizeCarlZipfContent(unsafe);
 assert.equal(normalized.links.quote, '#quote');
 assert.equal(normalized.images.hero, defaultCarlZipfContent.images.hero);
 assert.equal(calls.filter(call => call.options.method === 'PUT').length, 1);

 const { actions: publicActions } = await server.ssrLoadModule('/src/routes/carlzipf/public/+page.server.ts');
 const inquiry = new FormData();
 const id = crypto.randomUUID();
 for (const [key, value] of Object.entries({ submissionId: id, name: 'Local test', company: 'Test business', email: 'test@example.invalid', phone: '6145550199', address: 'Columbus, Ohio', jobType: 'commercial', service: 'electronic', requestMode: 'callback', timeline: 'Next month', details: 'Electronic opening hardware' })) inquiry.set(key, value);
 let leadPayload;
 const leadFetch = async (url, options) => {
  assert.match(String(url), /\/api\/public\/quote-requests\/carlzipf$/);
  leadPayload = JSON.parse(options.body);
  return new Response(JSON.stringify({ success: true, data: { ...leadPayload, tenantId: '88888888-8888-4888-8888-888888888883', submittedAtUtc: new Date().toISOString(), status: 'new', source: 'website' } }), { headers: { 'Content-Type': 'application/json' } });
 };
 let redirected;
 try { await publicActions.quote({ request: new Request('http://localhost/?/quote', { method: 'POST', body: inquiry }), fetch: leadFetch, url: new URL('http://localhost/') }); } catch (result) { redirected = result; }
 assert.equal(redirected.status, 303);
 assert.match(redirected.location, /^\/\?submitted=1&reference=.*#quote$/);
 assert.equal(leadPayload.propertyType, 'commercial');
 assert.equal(leadPayload.serviceType, 'electronic');
 assert.equal(leadPayload.requestedTimeline, 'Next month');
 assert.equal(leadPayload.id, id);
 console.log('Commercial intake uses the Carl Zipf endpoint, retains the submission ID and timeline, and redirects to its own receipt (local API fixture).');
 console.log('CMS first save, reload, existing content preservation, version conflict and unsafe URL checks passed (local API fixture).');
} finally { await server.close(); }
