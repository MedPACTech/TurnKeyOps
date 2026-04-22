import { fail } from '@sveltejs/kit';
import { buildQuoteRequestInbox, getQuoteRequestMetrics, quoteRequestStatuses, type QuoteRequestStatus } from '$lib/quote-requests';
import { loadQuoteRequests, updateQuoteRequest } from '$lib/server/quote-requests';

const buildScheduleSiteVisitHref = (requestId: string) =>
	`/bdr/admin/calendar?role=office-admin&scheduleRequest=${encodeURIComponent(requestId)}`;

export const load = async ({ fetch }) => {
	const { requests, source } = await loadQuoteRequests(fetch);
	const inbox = buildQuoteRequestInbox(requests);

	return {
		requests: inbox,
		metrics: getQuoteRequestMetrics(inbox),
		source,
		scheduleSiteVisitBaseHref: '/bdr/admin/calendar?role=office-admin',
		scheduleSiteVisitByRequestId: Object.fromEntries(
			inbox.map((request) => [request.id, buildScheduleSiteVisitHref(request.id)])
		)
	};
};

export const actions = {
	updateRequest: async ({ fetch, request }) => {
		const formData = await request.formData();
		const id = String(formData.get('id') ?? '').trim();
		const status = String(formData.get('status') ?? '').trim() as QuoteRequestStatus;
		const assignedTo = String(formData.get('assignedTo') ?? '').trim();
		const nextAction = String(formData.get('nextAction') ?? '').trim();

		if (!id || !quoteRequestStatuses.includes(status)) {
			return fail(400, { message: 'Valid request id and status are required.' });
		}

		try {
			await updateQuoteRequest(fetch, {
				id,
				status,
				assignedTo,
				nextAction
			});
		} catch (cause) {
			console.error('Failed to persist quote request update through API.', cause);
			return fail(502, { message: 'Could not save the quote request update to the API.' });
		}

		return { success: true };
	}
};
