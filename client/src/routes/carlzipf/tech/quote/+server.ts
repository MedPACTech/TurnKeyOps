import { error, json } from '@sveltejs/kit';
import { fieldApi, fieldResponse, requireFieldOrigin } from '../field-api.server';
import type { FieldEstimate } from '$lib/locksmith-handoff';
import type { RequestHandler } from './$types';

const validId = (id: string) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id);
const headers = { 'Cache-Control': 'private, no-store', 'Referrer-Policy': 'no-referrer' };

export const GET: RequestHandler = async (event) => {
	const id = event.url.searchParams.get('requestId') ?? '';
	if (!validId(id)) throw error(400, 'A valid shared request reference is required.');
	const { call, context } = await fieldApi(event);
	const estimate = await fieldResponse<FieldEstimate>(await call(`/api/quote-estimates/${id}`));
	if (!estimate.locksmithPricing?.jobType || !context.capabilities.includes(estimate.locksmithPricing.jobType)) throw error(403, 'This quote type is not enabled for your account.');
	return json({ estimate }, { headers });
};

export const POST: RequestHandler = async (event) => {
	requireFieldOrigin(event);
	const { requestId, expectedVersion } = await event.request.json();
	if (typeof requestId !== 'string' || !validId(requestId) || typeof expectedVersion !== 'string' || !expectedVersion || expectedVersion.length > 500) throw error(400, 'Load the current server quote before issuing its review link.');
	const { call, context } = await fieldApi(event);
	const existing = await fieldResponse<FieldEstimate>(await call(`/api/quote-estimates/${requestId}`));
	if (!existing.locksmithPricing?.jobType || !context.capabilities.includes(existing.locksmithPricing.jobType)) throw error(403, 'This quote type is not enabled for your account.');
	if (existing.status === 'sent') return json({ estimate: existing, recovered: true }, { headers });
	if (existing.version !== expectedVersion) throw error(409, 'The quote changed. Refresh and review its current version before issuing a link.');
	if (existing.status !== 'ready-to-send') throw error(409, 'Office review is still required before issuing a customer link.');
	const estimate = await fieldResponse<FieldEstimate>(await call(`/api/quote-estimates/${requestId}/send`, { method: 'POST', body: JSON.stringify({ expectedVersion }) }));
	return json({ estimate }, { headers });
};
