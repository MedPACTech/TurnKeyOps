import { thinkPinkTenant } from '$lib/config/tenants';
import { loadQuoteRequests } from '$lib/server/quote-requests';

export const load = async ({ fetch }) => {
	const { requests, source } = await loadQuoteRequests(fetch, thinkPinkTenant.id);
	return {
		source,
		requests: requests.filter((request) =>
			['qualified', 'inspection-scheduled', 'estimate-drafted', 'estimate-sent', 'won'].includes(request.status)
		)
	};
};
