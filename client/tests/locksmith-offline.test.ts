import assert from 'node:assert/strict';
import test from 'node:test';
import { draftStorageKey, newOpening } from '../src/lib/locksmith-drafts.ts';
import { catalogFingerprint, isDraftStale, validateSavedDraft, type SavedFieldDraft } from '../src/lib/locksmith-offline.ts';

const identity = { tenantId: 'tenant-a', userId: 'tech-a', capabilities: ['residential'] as const };
const context = { ...identity, capabilities: [...identity.capabilities] };
function record(): SavedFieldDraft {
	const opening = newOpening();
	return { scope: draftStorageKey(identity.tenantId, identity.userId), version: 1, revision: 'revision-a', savedAt: new Date().toISOString(), catalogVersion: 'sample-v1', state: 'pending-office-sync', draft: { version: 1, jobType: 'residential', customer: '', site: '', openings: [opening] }, photos: [{ id: 'photo-a', openingId: opening.id, name: 'door.png', blob: new Blob(['image'], { type: 'image/png' }) }] };
}

test('saved photo records remain tenant/user scoped and capability checked', () => {
	const saved = record();
	assert.ok(validateSavedDraft(saved, context));
	assert.equal(validateSavedDraft(saved, { ...context, userId: 'tech-b' }), null);
	assert.equal(validateSavedDraft(saved, { ...context, tenantId: 'tenant-b' }), null);
	assert.equal(validateSavedDraft(saved, { ...context, capabilities: ['commercial'] }), null);
});

test('photo payloads must belong to an opening and have an allowed image type', () => {
	const saved = record();
	saved.photos[0].openingId = 'other-opening';
	assert.equal(validateSavedDraft(saved, context), null);
	saved.photos[0].openingId = saved.draft.openings[0].id;
	saved.photos[0].blob = new Blob(['script'], { type: 'text/html' });
	assert.equal(validateSavedDraft(saved, context), null);
});

test('saved state never claims a synced or issued quote', () => {
	const saved = { ...record(), state: 'synced' };
	assert.equal(validateSavedDraft(saved, context), null);
});

test('catalog changes and old or invalid saved timestamps are stale', () => {
	const now = Date.now();
	const saved = { savedAt: new Date(now).toISOString(), catalogVersion: 'v1' };
	assert.equal(isDraftStale(saved, 'v1', now), false);
	assert.equal(isDraftStale(saved, 'v2', now), true);
	assert.equal(isDraftStale(saved, 'v1', now + 25 * 60 * 60 * 1000), true);
	assert.equal(isDraftStale({ ...saved, savedAt: 'invalid' }, 'v1', now), true);
	assert.notEqual(catalogFingerprint([{ price: 5 }]), catalogFingerprint([{ price: 6 }]));
});
