import { fail } from '@sveltejs/kit';
import { loadQuoteRequests } from '$lib/server/quote-requests';
import { createQuoteEstimateRevision, listQuoteEstimates, saveQuoteEstimate, sendQuoteEstimate, type QuoteEstimate, type QuoteEstimateLocation } from '$lib/server/quote-estimates';

const parseStatus = (value: FormDataEntryValue | null): 'draft' | 'ready-to-send' =>
	String(value ?? '').trim() === 'ready-to-send' ? 'ready-to-send' : 'draft';
const parseLocations = (value: FormDataEntryValue | null): QuoteEstimateLocation[] => {
	try { const parsed = JSON.parse(String(value ?? '[]')); return Array.isArray(parsed) ? parsed : []; }
	catch { return []; }
};
const draftInput = (formData: FormData, version?: string) => ({
	customerName: String(formData.get('customerName') ?? '').trim(),
	siteName: String(formData.get('siteName') ?? '').trim(),
	serviceSummary: String(formData.get('serviceSummary') ?? '').trim(),
	visitFindings: String(formData.get('visitFindings') ?? '').trim(),
	notes: String(formData.get('notes') ?? '').trim(),
	status: parseStatus(formData.get('draftStatus')),
	locations: parseLocations(formData.get('locations')),
	version
});
const mapEstimates = (values: QuoteEstimate[]) => Object.fromEntries(values.map((value) => [value.quoteRequestId, value]));

export const load = async ({ fetch, url }) => {
	const [{ requests }, estimates] = await Promise.all([loadQuoteRequests(fetch), listQuoteEstimates(fetch)]);
	return { quoteRequests: requests, estimateDrafts: mapEstimates(estimates), requestedRequestId: url.searchParams.get('request')?.trim() ?? '' };
};

export const actions = {
	saveDraft: async ({ fetch, request }) => {
		const formData = await request.formData();
		const requestId = String(formData.get('requestId') ?? '').trim();
		if (!requestId) return fail(400, { draftMessage: 'Choose a source request before saving the estimate draft.', savedRequestId: requestId });
		const existing = (await listQuoteEstimates(fetch)).find((item) => item.quoteRequestId === requestId);
		const saved = await saveQuoteEstimate(fetch, requestId, draftInput(formData, existing?.version));
		return { draftSaved: true, savedRequestId: requestId, draftSavedAtUtc: saved.savedAtUtc };
	},
	sendDraft: async ({ fetch, request }) => {
		const formData = await request.formData();
		const requestId = String(formData.get('requestId') ?? '').trim();
		if (!requestId) return fail(400, { draftMessage: 'Choose a source request before sending the estimate.', savedRequestId: requestId });
		if (parseStatus(formData.get('draftStatus')) !== 'ready-to-send') return fail(400, { draftMessage: 'Move the estimate draft to Ready to Send before sending it to the customer.', savedRequestId: requestId });
		const existing = (await listQuoteEstimates(fetch)).find((item) => item.quoteRequestId === requestId);
		const saved = await saveQuoteEstimate(fetch, requestId, draftInput(formData, existing?.version));
		const sent = await sendQuoteEstimate(fetch, requestId, saved.version);
		return { draftSent: true, savedRequestId: requestId, draftSentAtUtc: sent.sentAtUtc, reviewUrl: sent.delivery?.reviewUrl };
	},
	createRevision: async ({ fetch, request }) => {
		const formData = await request.formData();
		const requestId = String(formData.get('requestId') ?? '').trim();
		if (!requestId) return fail(400, { draftMessage: 'Choose a source request before creating a revision.', savedRequestId: requestId });
		const existing = (await listQuoteEstimates(fetch)).find((item) => item.quoteRequestId === requestId);
		if (!existing) return fail(404, { draftMessage: 'The estimate could not be found.', savedRequestId: requestId });
		const revised = await createQuoteEstimateRevision(fetch, requestId, existing.version);
		return { revisionCreated: true, savedRequestId: requestId, revisionNumber: revised.revisionNumber };
	}
};
