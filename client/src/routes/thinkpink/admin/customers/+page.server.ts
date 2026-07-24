import { thinkPinkTenant } from '$lib/config/tenants';
import { loadQuoteRequests } from '$lib/server/quote-requests';

export const load = async ({ fetch }) => {
	const { requests, source } = await loadQuoteRequests(fetch, thinkPinkTenant.id);
	const contacts = new Map<string, {
		id: string; name: string; company: string; email: string; phone: string;
		addresses: string[]; requestIds: string[]; latestStatus: string; latestActivity: string;
	}>();
	for (const request of requests) {
		const key = request.email?.toLowerCase() || request.phone || request.contactName || request.customerName;
		const existing = contacts.get(key);
		const address = request.serviceAddress?.trim();
		if (existing) {
			if (address && !existing.addresses.includes(address)) existing.addresses.push(address);
			existing.requestIds.push(request.id);
			existing.latestStatus = request.status;
			existing.latestActivity = request.timeline.at(-1)?.occurredAtUtc ?? request.submittedAtUtc;
		} else {
			contacts.set(key, {
				id: key,
				name: request.contactName || request.customerName,
				company: request.companyName || request.siteName || '',
				email: request.email,
				phone: request.phone,
				addresses: address ? [address] : [],
				requestIds: [request.id],
				latestStatus: request.status,
				latestActivity: request.timeline.at(-1)?.occurredAtUtc ?? request.submittedAtUtc
			});
		}
	}
	return { source, contacts: [...contacts.values()].sort((a, b) => a.name.localeCompare(b.name)) };
};
