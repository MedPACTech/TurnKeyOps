import { buildQuoteRequestInbox, getQuoteRequestMetrics } from '$lib/quote-requests';
import { authTokenCookie } from '$lib/server/auth-session';
import { loadBdrBillingSettings } from '$lib/server/bdr-billing-settings';
import { getBdrInvoiceBalanceDue, loadBdrInvoices } from '$lib/server/bdr-invoices';
import { buildBdrScheduleReadyJobs, loadBdrScheduledJobs } from '$lib/server/bdr-job-scheduling';
import { loadQuoteRequests } from '$lib/server/quote-requests';

const reason = (label: string, result: PromiseSettledResult<unknown>) =>
	result.status === 'rejected'
		? `${label}: ${result.reason instanceof Error ? result.reason.message : 'unavailable'}`
		: null;

export const load = async ({ fetch, cookies }) => {
	const [requestResult, billingResult, invoiceResult, jobResult] = await Promise.allSettled([
		loadQuoteRequests(fetch),
		loadBdrBillingSettings(fetch, cookies.get(authTokenCookie)),
		loadBdrInvoices(fetch),
		loadBdrScheduledJobs(fetch)
	]);
	const requests = requestResult.status === 'fulfilled' ? requestResult.value.requests : [];
	const requestSource = requestResult.status === 'fulfilled' ? requestResult.value.source : 'unavailable';
	const billingSettings = billingResult.status === 'fulfilled' ? billingResult.value : null;
	const invoices = invoiceResult.status === 'fulfilled' ? invoiceResult.value : [];
	const jobs = jobResult.status === 'fulfilled' ? jobResult.value : [];
	const requestInbox = buildQuoteRequestInbox(requests);
	const scheduleReadyJobs = billingSettings
		? buildBdrScheduleReadyJobs(invoices, requests, billingSettings, jobs).filter((job) => !job.isScheduled)
		: [];
	const errors = [
		reason('Quote requests', requestResult),
		reason('Billing settings', billingResult),
		reason('Invoices', invoiceResult),
		reason('Jobs', jobResult)
	].filter((value): value is string => Boolean(value));
	const currentMonth = new Date().toISOString().slice(0, 7);

	return {
		requestSource,
		requestInbox,
		invoices,
		jobs: jobs.slice().sort((a, b) => a.scheduledDate.localeCompare(b.scheduledDate)),
		scheduleReadyJobs,
		requestMetrics: getQuoteRequestMetrics(requestInbox),
		metrics: {
			activeJobs: jobs.filter((job) => ['scheduled', 'in-progress', 'on-hold'].includes(job.status)).length,
			pendingEstimates: requests.filter((request) => ['estimate-drafted', 'estimate-sent'].includes(request.status)).length,
			openInvoices: invoices.filter((invoice) => getBdrInvoiceBalanceDue(invoice) > 0.01).length,
			openBalance: invoices.reduce((sum, invoice) => sum + getBdrInvoiceBalanceDue(invoice), 0),
			collectedThisMonth: invoices.flatMap((invoice) => invoice.payments)
				.filter((payment) => payment.receivedAtUtc.startsWith(currentMonth))
				.reduce((sum, payment) => sum + payment.amount, 0)
		},
		integrationState: { loadedAtUtc: new Date().toISOString(), errors }
	};
};
