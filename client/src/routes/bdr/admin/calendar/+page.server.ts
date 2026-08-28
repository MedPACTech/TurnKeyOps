import { buildQuoteRequestQualification } from '$lib/quote-requests';
import { authTokenCookie } from '$lib/server/auth-session';
import { loadBdrBillingSettings } from '$lib/server/bdr-billing-settings';
import { loadBdrInvoices } from '$lib/server/bdr-invoices';
import { buildBdrScheduleReadyJobs, loadBdrScheduledJobs } from '$lib/server/bdr-job-scheduling';
import { loadQuoteRequests } from '$lib/server/quote-requests';

const failure = (label: string, result: PromiseSettledResult<unknown>) =>
	result.status === 'rejected'
		? `${label}: ${result.reason instanceof Error ? result.reason.message : 'unavailable'}`
		: null;

export const load = async ({ fetch, url, cookies }) => {
	const scheduleRequestId = url.searchParams.get('scheduleRequest')?.trim() ?? '';
	const [requestResult, billingResult, invoiceResult, jobResult] = await Promise.allSettled([
		loadQuoteRequests(fetch),
		loadBdrBillingSettings(fetch, cookies.get(authTokenCookie)),
		loadBdrInvoices(fetch),
		loadBdrScheduledJobs(fetch)
	]);
	const requests = requestResult.status === 'fulfilled' ? requestResult.value.requests : [];
	const billingSettings = billingResult.status === 'fulfilled' ? billingResult.value : null;
	const invoices = invoiceResult.status === 'fulfilled' ? invoiceResult.value : [];
	const scheduledJobs = jobResult.status === 'fulfilled' ? jobResult.value : [];
	const scheduledRequest = scheduleRequestId
		? requests.find((request) => request.id === scheduleRequestId) ?? null
		: null;

	return {
		source: requestResult.status === 'fulfilled' ? requestResult.value.source : 'unavailable',
		scheduledVisitRequests: requests
			.filter((request) => request.siteVisitSchedule)
			.sort((a, b) => `${a.siteVisitSchedule?.visitDate}${a.siteVisitSchedule?.windowStart}`.localeCompare(`${b.siteVisitSchedule?.visitDate}${b.siteVisitSchedule?.windowStart}`)),
		scheduledRequest,
		scheduledRequestQualification: scheduledRequest ? buildQuoteRequestQualification(scheduledRequest) : null,
		scheduledJobs: scheduledJobs.slice().sort((a, b) => `${a.scheduledDate}${a.windowStart}`.localeCompare(`${b.scheduledDate}${b.windowStart}`)),
		scheduleReadyJobs: billingSettings
			? buildBdrScheduleReadyJobs(invoices, requests, billingSettings, scheduledJobs).filter((job) => !job.isScheduled)
			: [],
		integrationState: {
			loadedAtUtc: new Date().toISOString(),
			errors: [failure('Quote requests', requestResult), failure('Billing settings', billingResult), failure('Invoices', invoiceResult), failure('Jobs', jobResult)]
				.filter((value): value is string => Boolean(value))
		}
	};
};
