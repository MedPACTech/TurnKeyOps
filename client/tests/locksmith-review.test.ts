import test from 'node:test';
import assert from 'node:assert/strict';
import { newOpening, type FieldDraft } from '../src/lib/locksmith-drafts.ts';
import { createFieldEstimate, type FieldEstimate } from '../src/lib/locksmith-handoff.ts';
import { fieldScopeMatchesEstimate, issuedReviewPath } from '../src/lib/locksmith-review.ts';

const requestId = '88888888-8888-4888-8888-888888888883';
const token = 'a'.repeat(64);
function packet(): FieldEstimate {
	return { id: requestId, quoteRequestId: requestId, version: 'v1', revisionNumber: 1, status: 'sent', savedAtUtc: new Date().toISOString(), expiresAtUtc: new Date(Date.now() + 86400_000).toISOString(), delivery: { status: 'sent', method: 'review-link', reviewUrl: `/carlzipf/estimate/${requestId}?token=${token}`, sentAtUtc: new Date().toISOString() }, locksmithPricing: { total: 100, subtotal: 100, discountAmount: 0, taxAmount: 0, taxPercent: 0, requiresOfficeApproval: false, approvalReasons: [], policyVersion: 'p1' } };
}

test('only current issued links for the exact quote route can be handed to a customer', () => {
	const estimate = packet();
	assert.equal(issuedReviewPath(estimate), `/carlzipf/estimate/${requestId}?token=${token}`);
	for (const bad of ['https://evil.example/quote', '//evil.example/quote', `/bdr/estimate/${requestId}?token=${token}`, `/carlzipf/estimate/different?token=${token}`, `/carlzipf/estimate/${requestId}?token=${token}&token=other`, `/carlzipf/estimate/${requestId}?token=${token}#fragment`]) {
		estimate.delivery!.reviewUrl = bad; assert.equal(issuedReviewPath(estimate), null);
	}
});

test('draft or expired quotes do not expose a customer handoff link', () => {
	const estimate = packet(); estimate.status = 'draft'; assert.equal(issuedReviewPath(estimate), null);
	estimate.status = 'sent'; estimate.expiresAtUtc = new Date(0).toISOString(); assert.equal(issuedReviewPath(estimate), null);
});

test('local measurement or hardware changes prevent issuing an earlier server scope', () => {
	const opening = newOpening(); opening.name = 'Front'; opening.items = [{ productId: 'lock', quantity: 1 }];
	const draft: FieldDraft = { version: 1, customer: 'Customer', site: 'Site', jobType: 'residential', openings: [opening] };
	const input = createFieldEstimate(draft);
	const estimate = packet(); estimate.locksmithPricing.jobType = 'residential'; estimate.locksmithPricing.openings = structuredClone(input.locksmith.openings); estimate.locksmithPricing.lines = [{ catalogItemId: 'lock', openingName: 'Front', quantity: 1, unitPrice: 100, total: 100, name: 'Lock' }];
	assert.equal(fieldScopeMatchesEstimate(draft, estimate), true);
	draft.openings[0].measurements.slabWidth.value = '36'; assert.equal(fieldScopeMatchesEstimate(draft, estimate), false);
	draft.openings[0].measurements.slabWidth.value = ''; draft.openings[0].items[0].quantity = 2; assert.equal(fieldScopeMatchesEstimate(draft, estimate), false);
});
