import { fail } from '@sveltejs/kit';
import {
	buildQuoteRequestInbox,
	getQuoteRequestMetrics,
	isQuoteRequestMissingInfoReasonCode,
	quoteRequestStatuses,
	type QuoteRequestMissingInfoReasonCode,
	type QuoteRequestStatus
} from '$lib/quote-requests';
import { loadQuoteRequests, updateQuoteRequest } from '$lib/server/quote-requests';

const buildScheduleSiteVisitHref = (requestId: string) =>
	`/bdr/admin/calendar?role=office-admin&scheduleRequest=${encodeURIComponent(requestId)}`;

const buildServiceAddress = (formData: FormData) => {
	const address1 = String(formData.get('address1') ?? '').trim();
	const address2 = String(formData.get('address2') ?? '').trim();
	const city = String(formData.get('city') ?? '').trim();
	const state = String(formData.get('state') ?? '').trim();
	const postalCode = String(formData.get('postalCode') ?? '').trim();
	const cityStateZip = [city, [state, postalCode].filter(Boolean).join(' ')].filter(Boolean).join(', ');
	return [address1, address2, cityStateZip].filter(Boolean).join(', ');
};

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
		const contactName = String(formData.get('contactName') ?? '').trim();
		const email = String(formData.get('email') ?? '').trim();
		const phone = String(formData.get('phone') ?? '').trim();
		const siteName = String(formData.get('siteName') ?? '').trim();
		const requestedTimeline = String(formData.get('requestedTimeline') ?? '').trim();
		const serviceAddress = buildServiceAddress(formData);
		const missingInfoReasonCodes = formData
			.getAll('missingInfoReasonCodes')
			.map((value) => String(value).trim())
			.filter(isQuoteRequestMissingInfoReasonCode) as QuoteRequestMissingInfoReasonCode[];

		if (!id || !quoteRequestStatuses.includes(status)) {
			return fail(400, { message: 'Valid request id and status are required.' });
		}

		if (status === 'needs-info' && missingInfoReasonCodes.length === 0) {
			return fail(400, { message: 'Choose at least one Needs Info reason code before saving.' });
		}

		try {
			await updateQuoteRequest(fetch, {
				id,
				status,
				assignedTo,
				nextAction,
				missingInfoReasonCodes,
				contactName,
				email,
				phone,
				siteName,
				serviceAddress,
				requestedTimeline
			});
		} catch (cause) {
			console.error('Failed to persist quote request update through API.', cause);
			return fail(502, { message: 'Could not save the quote request update to the API.' });
		}

		return { success: true };
	}
};
