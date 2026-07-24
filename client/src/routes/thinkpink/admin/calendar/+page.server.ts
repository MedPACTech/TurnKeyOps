import { thinkPinkTenant } from '$lib/config/tenants';
import { loadQuoteRequests } from '$lib/server/quote-requests';

export const load = async ({ fetch }) => {
	const { requests, source } = await loadQuoteRequests(fetch, thinkPinkTenant.id);
	return {
		source,
		visits: requests
			.filter((request) => request.siteVisitSchedule)
			.map((request) => {
				const schedule = request.siteVisitSchedule!;
				return {
					id: request.id,
					customer: request.siteName || request.contactName || request.customerName,
					address: request.serviceAddress,
					service: request.serviceType || request.projectType,
					status: request.status,
					visitDate: schedule.visitDate,
					windowStart: schedule.windowStart,
					windowEnd: schedule.windowEnd,
					assignedFieldResource: schedule.assignedFieldResource
				};
			})
			.sort((left, right) =>
				`${left.visitDate}${left.windowStart}`.localeCompare(`${right.visitDate}${right.windowStart}`)
			),
		unscheduled: requests.filter(
			(request) =>
				['qualified', 'contacted'].includes(request.status) && !request.siteVisitSchedule
		)
	};
};
