import { error, fail } from '@sveltejs/kit';
import { decidePublicQuoteEstimate, getPublicQuoteEstimate } from '$lib/server/quote-estimates';

const loadPacket = async (fetcher: typeof globalThis.fetch, requestId: string, token: string) => {
	if (!token) throw error(404, 'Estimate packet not found.');
	try {
		const draft = await getPublicQuoteEstimate(fetcher, 'bdr', requestId, token);
		return { draft, quoteRequest: { email: draft.delivery?.email ?? '', phone: draft.delivery?.phone ?? '' } };
	} catch {
		throw error(404, 'Estimate packet not found.');
	}
};

export const load = async ({ fetch, params, url }) => {
	const requestId = decodeURIComponent(params.requestId);
	const token = url.searchParams.get('token')?.trim() ?? '';
	const result = await loadPacket(fetch, requestId, token);
	const returnTo = url.searchParams.get('returnTo') ?? '';
	return { ...result, accessToken: token, returnTo: returnTo.startsWith('/bdr/admin/') ? returnTo : '' };
};

export const actions = {
	approve: async ({ fetch, request, params }) => {
		const requestId = decodeURIComponent(params.requestId);
		const token = String((await request.formData()).get('accessToken') ?? '').trim();
		await decidePublicQuoteEstimate(fetch, 'bdr', requestId, token, 'approve');
		return { approved: true };
	},
	requestChanges: async ({ fetch, request, params }) => {
		const requestId = decodeURIComponent(params.requestId);
		const formData = await request.formData();
		const token = String(formData.get('accessToken') ?? '').trim();
		const responseNote = String(formData.get('responseNote') ?? '').trim();
		if (!responseNote) return fail(400, { changeMessage: 'Add a short note so the office knows what to adjust.' });
		await decidePublicQuoteEstimate(fetch, 'bdr', requestId, token, 'request-changes', responseNote);
		return { changesRequested: true };
	}
};
