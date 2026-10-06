import assert from 'node:assert/strict';
import test from 'node:test';
import { withApiSession } from '../src/lib/server/api-session.ts';

const api = 'https://api.example.test';

test('admin quote reads use the signed-in session instead of deployment credentials', () => {
	const request = new Request(`${api}/api/quote-requests`, {
		headers: { Authorization: 'Bearer deployment-tenant', Accept: 'application/json' }
	});
	const scoped = withApiSession(request, api, 'bdr-admin');
	assert.equal(scoped.headers.get('Authorization'), 'Bearer bdr-admin');
	assert.equal(scoped.headers.get('Accept'), 'application/json');
	assert.equal(request.headers.get('Authorization'), 'Bearer deployment-tenant');
});

test('session credentials never reach other origins or non-API paths', () => {
	for (const url of ['https://other.example.test/api/quote-requests', `${api}/assets/file`, `${api}/api-other`]) {
		const request = new Request(url);
		assert.equal(withApiSession(request, api, 'bdr-admin'), request);
		assert.equal(request.headers.get('Authorization'), null);
	}
});

test('anonymous calls remain unchanged', () => {
	const request = new Request(`${api}/api/public/quote-requests/bdr`);
	assert.equal(withApiSession(request, api, undefined), request);
});

test('quote mutations preserve their method and payload', async () => {
	const request = new Request(`${api}/api/quote-requests/123`, {
		method: 'PUT', body: JSON.stringify({ status: 'in-review' }),
		headers: { 'Content-Type': 'application/json' }
	});
	const scoped = withApiSession(request, api, 'bdr-admin');
	assert.equal(scoped.method, 'PUT');
	assert.deepEqual(await scoped.json(), { status: 'in-review' });
});
