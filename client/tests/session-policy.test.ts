import test from 'node:test';
import assert from 'node:assert/strict';
import {
	getAdminSessionFromToken,
	getSafeAdminReturnTo,
	getTokenSessionId,
	hasInternalAdminRole,
	isCrossSiteFormMutation,
	resolveBdrAdminRole
} from '../src/lib/server/session-policy.ts';

const token = (claims: Record<string, unknown>) => {
	const encode = (value: object) => Buffer.from(JSON.stringify(value)).toString('base64url');
	return `${encode({ alg: 'none', typ: 'JWT' })}.${encode(claims)}.unsigned`;
};
const future = () => Math.floor(Date.now() / 1000) + 3600;

test('safe admin redirects stay on an exact protected route family', () => {
	assert.equal(getSafeAdminReturnTo('/bdr/admin/jobs?job=1'), '/bdr/admin/jobs?job=1');
	assert.equal(getSafeAdminReturnTo('/thinkpink/admin/settings'), '/thinkpink/admin/settings');
	assert.equal(getSafeAdminReturnTo('/turnkeyops/admin/dashboard'), '/turnkeyops/admin/dashboard');
	for (const unsafe of [
		'https://evil.example/bdr/admin',
		'//evil.example/bdr/admin',
		'/bdr/administrator',
		'/bdr/admin\\evil',
		'/public'
	]) {
		assert.equal(getSafeAdminReturnTo(unsafe), '/bdr/admin/bob');
	}
});

test('external admin session requires unexpired token, tenant, and elevated role', () => {
	assert.equal(getAdminSessionFromToken(token({ role: 'admin', exp: future() }), '/bdr/admin/jobs'), null);
	assert.equal(
		getAdminSessionFromToken(token({ role: 'member', tenant_id: 'tenant-a', exp: future() }), '/bdr/admin/jobs'),
		null
	);
	assert.equal(
		getAdminSessionFromToken(
			token({ role: 'owner', tenant_id: 'tenant-a', exp: Math.floor(Date.now() / 1000) - 1 }),
			'/bdr/admin/jobs'
		),
		null
	);
	assert.equal(
		getAdminSessionFromToken(token({ role: 'owner', tenant_id: 'tenant-a' }), '/bdr/admin/jobs'),
		null
	);

	const session = getAdminSessionFromToken(
		token({ role: 'tenant_admin', tenant_id: 'tenant-a', email: 'admin@example.test', exp: future() }),
		'/thinkpink/admin/settings'
	);
	assert.equal(session?.role, 'office-admin');
	assert.equal(session?.tenantId, 'tenant-a');
});

test('internal admin session requires the explicit internal_admin role', () => {
	assert.equal(
		getAdminSessionFromToken(token({ role: 'owner', tenant_id: 'tenant-a', exp: future() }), '/turnkeyops/admin'),
		null
	);
	assert.equal(
		getAdminSessionFromToken(token({ role: 'admin', tenant_id: 'tenant-a', exp: future() }), '/turnkeyops/admin'),
		null
	);
	assert.equal(
		getAdminSessionFromToken(token({ role: 'internal_admin', exp: future() }), '/turnkeyops/admin')?.surface,
		'internal-admin'
	);
});

test('role normalization never promotes ordinary staff or contacts', () => {
	assert.equal(resolveBdrAdminRole(['staff']), null);
	assert.equal(resolveBdrAdminRole(['contact']), null);
	assert.equal(resolveBdrAdminRole(['billing_admin']), null);
	assert.equal(resolveBdrAdminRole(['tenant owner']), 'owner');
	assert.equal(hasInternalAdminRole(['internal-admin']), true);
	assert.equal(hasInternalAdminRole(['admin']), false);
});

test('logout revocation uses the identity session claim', () => {
	assert.equal(getTokenSessionId(token({ sid: 'session-123', exp: future() })), 'session-123');
	assert.equal(getTokenSessionId(token({ session_id: 'legacy-session', exp: future() })), 'legacy-session');
	assert.equal(getTokenSessionId(token({ exp: future() })), '');
});

test('cross-site browser form mutations are rejected', () => {
	assert.equal(
		isCrossSiteFormMutation('POST', 'application/x-www-form-urlencoded', 'https://evil.test', 'https://app.test'),
		true
	);
	assert.equal(
		isCrossSiteFormMutation('DELETE', 'text/plain; charset=utf-8', null, 'https://app.test'),
		true
	);
	assert.equal(
		isCrossSiteFormMutation('POST', 'multipart/form-data; boundary=x', 'https://app.test', 'https://app.test'),
		false
	);
	assert.equal(isCrossSiteFormMutation('GET', null, null, 'https://app.test'), false);
	assert.equal(isCrossSiteFormMutation('POST', 'application/json', 'https://evil.test', 'https://app.test'), false);
});
