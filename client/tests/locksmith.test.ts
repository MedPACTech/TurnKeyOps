import test from 'node:test';
import assert from 'node:assert/strict';
import { catalogForJobType, canHandleJobType, parseTechnicianContext } from '../src/lib/locksmith.ts';
import { getSafeAdminReturnTo, isAdminPath, isTechnicianPath, getAdminSessionFromToken } from '../src/lib/server/session-policy.ts';

test('technician context fails closed for another tenant and malformed capabilities', () => {
	assert.equal(parseTechnicianContext({ tenantId: 'other', userId: 'tech', capabilities: ['residential'] }, 'tenant'), null);
	assert.equal(parseTechnicianContext({ tenantId: 'tenant', userId: '', capabilities: ['residential'] }, 'tenant'), null);
	assert.equal(parseTechnicianContext({ tenantId: 'tenant', userId: 'tech', capabilities: ['admin'] }, 'tenant'), null);
	assert.deepEqual(parseTechnicianContext({ tenantId: 'tenant', userId: 'tech', capabilities: [] }, 'tenant')?.capabilities, []);
});

test('residential and commercial capabilities limit sample hardware availability', () => {
	assert.equal(canHandleJobType(['residential'], 'commercial'), false);
	assert.equal(canHandleJobType(['residential', 'commercial'], 'commercial'), true);
	assert.equal(catalogForJobType('residential').some((item) => item.id === 'sample-exit-device'), false);
	assert.equal(catalogForJobType('commercial').some((item) => item.id === 'sample-exit-device'), true);
});

test('Carl Zipf route boundaries protect admin without granting technician admin roles', () => {
	assert.equal(isAdminPath('/carlzipf/admin/settings'), true);
	assert.equal(isAdminPath('/carlzipf/administrator'), false);
	assert.equal(isTechnicianPath('/carlzipf/tech'), true);
	assert.equal(isTechnicianPath('/carlzipf/technician'), false);
	assert.equal(getSafeAdminReturnTo('/carlzipf/tech?job=1'), '/carlzipf/tech?job=1');
	assert.equal(getSafeAdminReturnTo('/carlzipf/admin/settings'), '/carlzipf/admin/settings');
	assert.equal(getSafeAdminReturnTo('//evil.test/carlzipf/tech'), '/bdr/admin/bob');
	const claims = Buffer.from(JSON.stringify({ role: 'field', tenant_id: 'tenant', exp: Date.now() / 1000 + 3600 })).toString('base64url');
	assert.equal(getAdminSessionFromToken(`header.${claims}.signature`, '/carlzipf/admin/settings'), null);
});
