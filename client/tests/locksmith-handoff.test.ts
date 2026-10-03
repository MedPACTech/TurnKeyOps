import test from 'node:test';
import assert from 'node:assert/strict';
import { newOpening, readFieldDraft, type FieldDraft } from '../src/lib/locksmith-drafts.ts';
import { createFieldEstimate, createFieldSource, handoffErrors, standardLaborHours } from '../src/lib/locksmith-handoff.ts';

function visit(): FieldDraft {
	const opening = newOpening(); opening.name = 'Front entry'; opening.measurements.slabWidth = { value: '36', certainty: 'measured' }; opening.items = [{ productId: 'office-door', quantity: 2 }];
	return { version: 1, jobType: 'residential', customer: 'Customer', site: '123 Main', contactEmail: 'customer@example.test', contactPhone: '555-0100', laborHours: 2, discountPercent: 5, openings: [opening] };
}

test('shared quote input preserves opening observations and sends IDs, never client unit prices', () => {
	const draft = visit();
	const input = createFieldEstimate(draft);
	assert.equal(input.status, 'draft'); assert.equal(input.expectedVersion, null);
	assert.deepEqual(input.locksmith.openings[0].measurements.slabWidth, { value: '36', certainty: 'measured' });
	assert.deepEqual(input.locksmith.items, [{ catalogItemId: 'office-door', quantity: 2, openingName: 'Front entry' }]);
	assert.equal(JSON.stringify(input).includes('unitPrice'), false);
});

test('source request remains an immutable retry snapshot while quote updates carry expected versions', () => {
	const draft = visit(); const requestId = crypto.randomUUID();
	draft.handoff = { requestId, source: createFieldSource(draft, requestId), estimateVersion: 'etag-v1' };
	draft.openings[0].name = 'Updated front entry';
	assert.equal(createFieldEstimate(draft).expectedVersion, 'etag-v1');
	assert.equal(draft.handoff.source.id, requestId);
	assert.ok(readFieldDraft(JSON.stringify(draft), ['residential']));
	draft.handoff.source.id = crypto.randomUUID();
	assert.equal(readFieldDraft(JSON.stringify(draft), ['residential']), null);
});

test('contact, named openings and bounded labor/discount are required for handoff', () => {
	const draft = visit(); assert.deepEqual(handoffErrors(draft, true), []);
	draft.contactEmail = ''; draft.openings[0].name = ''; draft.discountPercent = 101;
	assert.equal(handoffErrors(draft, true).length, 3);
});

test('standard labor accumulates per opening and extra hours carry a reason', () => {
	const draft = visit(); draft.laborHours = 0;
	const context = { jobTypes: ['residential'] as const, catalog: [], policyVersion: 'v1', laborRatePerHour: 100, taxPercent: 0,
		laborHoursByJobType: { residential: { 'Door and frame replacement': 4 }, commercial: { 'Door and frame replacement': 6 } } };
	assert.equal(standardLaborHours(draft, context as Parameters<typeof standardLaborHours>[1]), 4);
	draft.laborHours = 6; draft.laborOverrideReason = 'Damaged jamb';
	assert.equal(createFieldEstimate(draft).locksmith.laborOverrideReason, 'Damaged jamb');
});
