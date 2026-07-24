import { thinkPinkTenant } from '$lib/config/tenants';
import { loadQuoteRequests } from '$lib/server/quote-requests';

export const load = async ({ fetch }) => {
	const { requests, source } = await loadQuoteRequests(fetch, thinkPinkTenant.id);
	return {
		source,
		invoiceCandidates: requests
			.filter((request) => ['estimate-sent', 'won'].includes(request.status))
			.map((request) => ({
				id: request.id,
				customer: request.contactName || request.customerName,
				site: request.siteName || request.serviceAddress,
				service: request.serviceType || request.projectType,
				status: request.status === 'won' ? 'Ready to invoice' : 'Awaiting approval',
				nextAction: request.nextAction,
				updatedAtUtc: request.timeline.at(-1)?.occurredAtUtc ?? request.submittedAtUtc
			}))
	};
};
