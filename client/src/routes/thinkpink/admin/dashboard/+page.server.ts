import { thinkPinkTenant } from '$lib/config/tenants';
import { loadQuoteRequests } from '$lib/server/quote-requests';

export const load = async ({ fetch }) => {
	const { requests, source } = await loadQuoteRequests(fetch, thinkPinkTenant.id);
	return {
		source,
		requests,
		metrics: {
			newRequests: requests.filter((request) => request.status === 'new').length,
			assessmentReady: requests.filter((request) => request.status === 'qualified').length,
			visitsScheduled: requests.filter((request) => request.status === 'inspection-scheduled').length,
			activeEstimates: requests.filter((request) => ['estimate-drafted', 'estimate-sent'].includes(request.status)).length
		}
	};
};
