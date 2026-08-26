import { resolveMvpScaffold } from '$lib/server/mvp';
import { loadQuoteRequests } from '$lib/server/quote-requests';
import { buildQuoteRequestInbox, getQuoteRequestMetrics } from '$lib/quote-requests';
import { formatCurrency } from '$lib/utils/format';
import { loadBdrBillingSettings } from '$lib/server/bdr-billing-settings';
import { loadBdrInvoices } from '$lib/server/bdr-invoices';
import {
	buildBdrScheduleReadyJobs,
	loadBdrScheduledJobs
} from '$lib/server/bdr-job-scheduling';
import { authTokenCookie } from '$lib/server/auth-session';

export const load = async ({ fetch, cookies }) => {
	const { snapshot, source } = await resolveMvpScaffold(fetch);
	const { requests, source: requestSource } = await loadQuoteRequests(fetch);
	const billingSettings = await loadBdrBillingSettings(fetch, cookies.get(authTokenCookie));
	const lifecycleInvoices = await loadBdrInvoices(fetch);
	const scheduledJobs = await loadBdrScheduledJobs(fetch);
	const scheduleReadyJobs = buildBdrScheduleReadyJobs(lifecycleInvoices, requests, billingSettings, scheduledJobs);
	const requestInbox = buildQuoteRequestInbox(requests);

	return {
		source,
		requestSource,
		snapshot,
		requestInbox,
		billingSettings,
		scheduledJobs,
		scheduleReadyJobs,
		requestMetrics: getQuoteRequestMetrics(requestInbox),
		metrics: [
			{
				label: 'Open opportunities',
				value: String(snapshot.summary.leadCount),
				detail: `Current scaffold leads worth ${formatCurrency(snapshot.summary.pipelineValue)}`
			},
			{
				label: 'Active estimates',
				value: String(snapshot.summary.estimateCount),
				detail: `${formatCurrency(snapshot.summary.estimateValue)} currently sitting in estimate value`
			},
			{
				label: 'Quote requests',
				value: String(requestInbox.length),
				detail: `${getQuoteRequestMetrics(requestInbox).newCount} new request(s) currently waiting for first response`
			}
		]
	};
};
