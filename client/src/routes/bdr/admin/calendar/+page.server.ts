import { buildQuoteRequestQualification } from '$lib/quote-requests';
import { loadQuoteRequests } from '$lib/server/quote-requests';

export const load = async ({ fetch, url }) => {
	const scheduleRequestId = url.searchParams.get('scheduleRequest')?.trim() ?? '';
	const { requests } = await loadQuoteRequests(fetch);
	const scheduledRequest = scheduleRequestId
		? requests.find((request) => request.id === scheduleRequestId) ?? null
		: null;
	const scheduledRequestQualification = scheduledRequest ? buildQuoteRequestQualification(scheduledRequest) : null;

	return {
		scheduleRequestId,
		scheduledRequest,
		scheduledRequestQualification
	};
};
