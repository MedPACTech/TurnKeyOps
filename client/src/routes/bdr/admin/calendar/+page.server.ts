import { buildQuoteRequestQualification } from '$lib/quote-requests';
import { loadQuoteRequests } from '$lib/server/quote-requests';
import { loadBdrBillingSettings } from '$lib/server/bdr-billing-settings';
import { loadBdrInvoices } from '$lib/server/bdr-invoices';
import {
	buildBdrScheduleReadyJobs,
	loadBdrScheduledJobs
} from '$lib/server/bdr-job-scheduling';

export const load = async ({ fetch, url }) => {
	const scheduleRequestId = url.searchParams.get('scheduleRequest')?.trim() ?? '';
	const { requests } = await loadQuoteRequests(fetch);
	const billingSettings = await loadBdrBillingSettings();
	const lifecycleInvoices = await loadBdrInvoices(fetch);
	const scheduledJobs = await loadBdrScheduledJobs(fetch);
	const scheduleReadyJobs = buildBdrScheduleReadyJobs(lifecycleInvoices, requests, billingSettings, scheduledJobs);
	const scheduledVisitRequests = requests.filter((request) => request.siteVisitSchedule);
	const scheduledRequest = scheduleRequestId
		? requests.find((request) => request.id === scheduleRequestId) ?? null
		: null;
	const scheduledRequestQualification = scheduledRequest ? buildQuoteRequestQualification(scheduledRequest) : null;

	return {
		scheduleRequestId,
		scheduledVisitRequests,
		scheduledRequest,
		scheduledRequestQualification,
		billingSettings,
		scheduledJobs,
		scheduleReadyJobs
	};
};
