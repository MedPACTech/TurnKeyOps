import { error, json, isHttpError } from '@sveltejs/kit';
import { readFieldDraft, type FieldRequestSource } from '$lib/locksmith-drafts';
import { createFieldEstimate, handoffErrors, type FieldEstimate, type FieldHandoffResult } from '$lib/locksmith-handoff';
import { fieldApi, fieldResponse, requireFieldOrigin } from '../field-api.server';
import type { RequestHandler } from './$types';

export const POST: RequestHandler = async (event) => {
	requireFieldOrigin(event);
	const { call, context } = await fieldApi(event);
	const raw = await event.request.text();
	if (raw.length > 250_000) throw error(413, 'Split this field draft into smaller requests.');
	const draft = readFieldDraft(raw, context.capabilities);
	if (!draft) throw error(400, 'This field draft is invalid or the job type is not enabled for your account.');
	const issues = handoffErrors(draft, true);
	if (issues.length) throw error(400, issues.join(' '));
	const handoff = draft.handoff;
	if (!handoff || !/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(handoff.requestId) || handoff.source?.id !== handoff.requestId || handoff.source.propertyType !== draft.jobType) throw error(400, 'Save a valid field request reference before submitting.');
	// The original source payload is kept stable across retries; quote updates use optimistic versions.
	const source: FieldRequestSource = { ...handoff.source, attachments: [], priority: 'standard' };
	const result: FieldHandoffResult = { requestId: handoff.requestId, requestSaved: false };
	try {
		await fieldResponse(await call('/api/quote-requests/field/carlzipf', { method: 'POST', body: JSON.stringify(source) }));
		result.requestSaved = true;
		const existing = await call(`/api/quote-estimates/${handoff.requestId}`);
		if (existing.ok) {
			const packet = await fieldResponse<FieldEstimate>(existing);
			if (!handoff.estimateVersion) { result.estimate = packet; result.recovered = true; return json(result, { headers: { 'Cache-Control': 'private, no-store' } }); }
			if (!['draft', 'ready-to-send'].includes(packet.status)) throw error(409, 'This estimate has already been issued. Ask the office to create a revision before changing its field scope.');
		} else if (existing.status !== 404) await fieldResponse(existing);
		result.estimate = await fieldResponse<FieldEstimate>(await call(`/api/quote-estimates/${handoff.requestId}`, { method: 'PUT', body: JSON.stringify(createFieldEstimate(draft)) }));
		return json(result, { headers: { 'Cache-Control': 'private, no-store' } });
	} catch (cause) {
		result.error = isHttpError(cause) ? cause.body.message : 'The shared API did not confirm the handoff. Retry with the saved reference.';
		return json(result, { status: isHttpError(cause) ? cause.status : 502, headers: { 'Cache-Control': 'private, no-store' } });
	}
};
