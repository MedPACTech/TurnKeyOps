import { parseQuoteSignatureInput } from '$lib/quote-signatures';
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

export const load = async ({ fetch, params, url, setHeaders }) => {
	setHeaders({ 'cache-control': 'private, no-store', 'referrer-policy': 'no-referrer', 'x-robots-tag': 'noindex, nofollow' });
	const requestId = decodeURIComponent(params.requestId);
	const token = url.searchParams.get('token')?.trim() ?? '';
	const result = await loadPacket(fetch, requestId, token);
	const returnTo = url.searchParams.get('returnTo') ?? '';
	return { ...result, accessToken: token, returnTo: returnTo.startsWith('/bdr/admin/') ? returnTo : '' };
};

export const actions = {
	approve: async ({ fetch, request, params }) => {
		const requestId = decodeURIComponent(params.requestId);
		const data = await request.formData();
		const token = String(data.get('accessToken') ?? '').trim();
		const parsed = parseQuoteSignatureInput(data);
		if (!token || !parsed.signature) return fail(400, { approvalError: parsed.error || 'Open the current estimate link before signing.', signerPrintedName: parsed.signerPrintedName });
		try {
			const saved = await decidePublicQuoteEstimate(fetch, 'bdr', requestId, token, 'approve', undefined, parsed.signature);
			if (saved.delivery?.status !== 'approved' || !saved.approvalSignature) throw new Error('Unconfirmed signature');
			return { approved: true, approvalSignature: saved.approvalSignature };
		} catch { return fail(409, { approvalError: 'Your signature was not confirmed. Reload this estimate to check its latest status before retrying.', signerPrintedName: parsed.signerPrintedName }); }
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
