import assert from 'node:assert/strict';
import test from 'node:test';
import { draftStorageKey, fieldDraftErrors, newOpening, readFieldDraft, type FieldDraft } from '../src/lib/locksmith-drafts.ts';

function sampleDraft(): FieldDraft {
	return { version: 1, jobType: 'residential', customer: 'Customer', site: 'Property', openings: [newOpening()] };
}

test('device drafts are keyed by both authenticated tenant and user', () => {
	assert.notEqual(draftStorageKey('a', 'b'), draftStorageKey('a', 'c'));
	assert.notEqual(draftStorageKey('a', 'b'), draftStorageKey('c', 'b'));
	assert.notEqual(draftStorageKey('a:b', 'c'), draftStorageKey('a', 'b:c'));
});

test('restore rejects drafts whose job type is no longer enabled', () => {
	const raw = JSON.stringify(sampleDraft());
	assert.ok(readFieldDraft(raw, ['residential', 'commercial']));
	assert.equal(readFieldDraft(raw, ['commercial']), null);
	assert.equal(readFieldDraft(raw, []), null);
});

test('malformed and unsupported persisted drafts cannot be restored', () => {
	for (const value of ['null', '{}', '{', JSON.stringify({ ...sampleDraft(), version: 2 }), JSON.stringify({ ...sampleDraft(), openings: [{}] })]) {
		assert.equal(readFieldDraft(value, ['residential']), null);
	}
});

test('measurement certainty and dimensions survive restore independently', () => {
	const draft = sampleDraft();
	draft.openings[0].measurements.slabWidth = { value: '35.75', certainty: 'estimated' };
	const restored = readFieldDraft(JSON.stringify(draft), ['residential']);
	assert.deepEqual(restored?.openings[0].measurements.slabWidth, { value: '35.75', certainty: 'estimated' });
	assert.equal(restored?.openings[0].measurements.roughWidth.certainty, 'unknown');
});

test('invalid dimensions and quantities fail validation rather than silently changing totals', () => {
	const draft = sampleDraft();
	draft.openings[0].measurements.slabWidth.value = '-10';
	draft.openings[0].items = [{ productId: 'sample', quantity: 1.5 }];
	assert.equal(fieldDraftErrors(draft).length, 2);
	assert.equal(readFieldDraft(JSON.stringify(draft), ['residential']), null);
	draft.openings[0].measurements.slabWidth.value = '36';
	draft.openings[0].items[0].quantity = 2;
	assert.deepEqual(fieldDraftErrors(draft), []);
});
