import { test } from 'node:test';
import assert from 'node:assert/strict';
import { withApiSession } from '../src/lib/server/api-session-request.ts';

const base = 'https://api.example.test';
test('admin API reads use the current session instead of a shared token', () => {
	const original = new Request(`${base}/api/quote-requests`, { headers: { Authorization: 'Bearer shared', Accept: 'application/json' } });
	const request = withApiSession(original, base, 'user-session');
	assert.equal(request.headers.get('Authorization'), 'Bearer user-session');
	assert.equal(request.headers.get('Accept'), 'application/json');
	assert.equal(request.redirect, 'error');
	assert.equal(original.headers.get('Authorization'), 'Bearer shared');
});
test('no authenticated admin session leaves public requests unchanged', () => {
	const request = new Request(`${base}/api/public/quote-requests/bdr`);
	assert.equal(withApiSession(request, base, null), request);
	assert.equal(request.headers.get('Authorization'), null);
});
test('never forwards session credentials outside the configured API scope', () => {
	for (const url of ['https://other.example.test/api/quote-requests', 'http://api.example.test/api/x', `${base}:8443/api/x`, `${base}/api-lookalike/x`, `${base}/assets/image.png`]) {
		const request = new Request(url);
		assert.equal(withApiSession(request, base, 'secret'), request);
		assert.equal(request.headers.get('Authorization'), null);
	}
});
test('supports configured API base paths and preserves write bodies', async () => {
	const request = withApiSession(new Request(`${base}/service/api/quote-requests`, { method: 'POST', body: '{"test":true}', headers: { 'Content-Type': 'application/json' } }), `${base}/service/`, 'session');
	assert.equal(request.method, 'POST');
	assert.equal(await request.text(), '{"test":true}');
	assert.equal(request.headers.get('Authorization'), 'Bearer session');
	const outside = new Request(`${base}/api/quote-requests`);
	assert.equal(withApiSession(outside, `${base}/service`, 'session'), outside);
});
