import { fail } from '@sveltejs/kit';
import { loadBdrBillingSettings } from '$lib/server/bdr-billing-settings';
import { loadBdrInvoices } from '$lib/server/bdr-invoices';
import {
	addBdrScheduledJobNote,
	buildBdrScheduleReadyJobs,
	loadBdrScheduledJobs,
	rescheduleBdrScheduledJob,
	updateBdrScheduledJobStatus,
	type BdrProductionJobStatus
} from '$lib/server/bdr-job-scheduling';
import { loadQuoteRequests } from '$lib/server/quote-requests';

const productionStatuses = ['scheduled', 'in-progress', 'on-hold', 'completed', 'cancelled'] as const;

const normalizeStatus = (value: FormDataEntryValue | null): BdrProductionJobStatus => {
	const normalized = String(value ?? '').trim();
	return productionStatuses.includes(normalized as BdrProductionJobStatus)
		? (normalized as BdrProductionJobStatus)
		: 'scheduled';
};

const normalizeDateInput = (value: FormDataEntryValue | null) => {
	const normalized = String(value ?? '').trim();
	return /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : '';
};

const normalizeTimeInput = (value: FormDataEntryValue | null, fallback: string) => {
	const normalized = String(value ?? '').trim();
	return /^\d{2}:\d{2}$/.test(normalized) ? normalized : fallback;
};

const getJobId = (formData: FormData) => String(formData.get('jobId') ?? '').trim();

export const load = async ({ fetch, url }) => {
	const { requests } = await loadQuoteRequests(fetch);
	const billingSettings = await loadBdrBillingSettings();
	const lifecycleInvoices = await loadBdrInvoices();
	const jobs = await loadBdrScheduledJobs();
	const scheduleReadyJobs = buildBdrScheduleReadyJobs(lifecycleInvoices, requests, billingSettings, jobs);
	const selectedJobId = url.searchParams.get('job')?.trim() ?? '';

	return {
		jobs: jobs.sort((a, b) => a.scheduledDate.localeCompare(b.scheduledDate) || a.windowStart.localeCompare(b.windowStart)),
		scheduleReadyJobs: scheduleReadyJobs.filter((job) => !job.isScheduled),
		billingSettings,
		selectedJobId
	};
};

export const actions = {
	updateStatus: async ({ request }) => {
		const formData = await request.formData();
		const jobId = getJobId(formData);
		const status = normalizeStatus(formData.get('status'));
		const note = String(formData.get('statusNote') ?? '').trim();
		if (!jobId) return fail(400, { jobActionMessage: 'Choose a job before changing status.' });
		const job = await updateBdrScheduledJobStatus(jobId, {
			status,
			note,
			actor: 'Office admin'
		});
		if (!job) return fail(404, { jobActionMessage: 'Job not found.' });
		return {
			jobActionMessage: `${job.siteName || job.customerName} moved to ${status.replace('-', ' ')}.`,
			selectedJobId: job.id
		};
	},
	rescheduleJob: async ({ request }) => {
		const formData = await request.formData();
		const jobId = getJobId(formData);
		const scheduledDate = normalizeDateInput(formData.get('scheduledDate'));
		const windowStart = normalizeTimeInput(formData.get('windowStart'), '08:00');
		const windowEnd = normalizeTimeInput(formData.get('windowEnd'), '12:00');
		const crew = String(formData.get('crew') ?? '').trim();
		const note = String(formData.get('scheduleNote') ?? '').trim();
		if (!jobId) return fail(400, { jobActionMessage: 'Choose a job before updating the schedule.' });
		if (!scheduledDate) return fail(400, { jobActionMessage: 'Choose a production date.' });
		if (!crew) return fail(400, { jobActionMessage: 'Assign a crew before saving the schedule.' });
		const job = await rescheduleBdrScheduledJob(jobId, {
			scheduledDate,
			windowStart,
			windowEnd,
			crew,
			note,
			actor: 'Office admin'
		});
		if (!job) return fail(404, { jobActionMessage: 'Job not found.' });
		return {
			jobActionMessage: `${job.siteName || job.customerName} was rescheduled for ${job.scheduledDate}.`,
			selectedJobId: job.id
		};
	},
	addJobNote: async ({ request }) => {
		const formData = await request.formData();
		const jobId = getJobId(formData);
		const note = String(formData.get('jobNote') ?? '').trim();
		if (!jobId) return fail(400, { jobActionMessage: 'Choose a job before adding a note.' });
		if (!note) return fail(400, { jobActionMessage: 'Add a note before saving.' });
		const job = await addBdrScheduledJobNote(jobId, {
			note,
			actor: 'Office admin'
		});
		if (!job) return fail(404, { jobActionMessage: 'Job not found.' });
		return {
			jobActionMessage: `Note added to ${job.siteName || job.customerName}.`,
			selectedJobId: job.id
		};
	}
};
