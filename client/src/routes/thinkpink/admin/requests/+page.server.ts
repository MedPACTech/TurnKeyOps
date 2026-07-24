import { thinkPinkTenant } from '$lib/config/tenants';
import { loadQuoteRequests } from '$lib/server/quote-requests';

export const load = async ({ fetch }) => loadQuoteRequests(fetch, thinkPinkTenant.id);
