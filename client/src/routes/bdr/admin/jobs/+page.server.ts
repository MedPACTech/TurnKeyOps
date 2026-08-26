import { fail } from '@sveltejs/kit';
import { loadBdrBillingSettings } from '$lib/server/bdr-billing-settings';
import { loadBdrInvoices } from '$lib/server/bdr-invoices';
import {
	addBdrScheduledJobNote,
	buildBdrScheduleReadyJobs,
	getBdrInvoiceSchedulingEligibility,
	loadBdrScheduledJobs,
	rescheduleBdrScheduledJob,
	scheduleBdrJobFromInvoice,
	updateBdrScheduledJobPlanning,
	updateBdrScheduledJobStatus,
	type BdrProductionJobChecklistKey,
	type BdrProductionJobConfirmationStatus,
	type BdrProductionJobOrderStatus,
	type BdrProductionJobStatus
} from '$lib/server/bdr-job-scheduling';
import { loadQuoteRequests } from '$lib/server/quote-requests';
import { authTokenCookie } from '$lib/server/auth-session';

const productionStatuses = ['scheduled', 'in-progress', 'on-hold', 'completed', 'cancelled'] as const;
const confirmationStatuses = ['pending', 'confirmed', 'needs-reschedule'] as const;
const orderStatuses = ['not-started', 'requested', 'ordered', 'confirmed', 'delivered'] as const;
const checklistKeys = [
	'customer-confirmed',
	'site-access',
	'utility-locate',
	'base-material-ordered',
	'equipment-reserved',
	'concrete-ordered',
	'forms-reinforcement',
	'weather-check',
	'pour-confirmed',
	'cleanup-walkthrough'
] as const satisfies readonly BdrProductionJobChecklistKey[];

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

const normalizeConfirmationStatus = (value: FormDataEntryValue | null): BdrProductionJobConfirmationStatus => {
	const normalized = String(value ?? '').trim();
	return confirmationStatuses.includes(normalized as BdrProductionJobConfirmationStatus)
		? (normalized as BdrProductionJobConfirmationStatus)
		: 'pending';
};

const normalizeOrderStatus = (value: FormDataEntryValue | null): BdrProductionJobOrderStatus => {
	const normalized = String(value ?? '').trim();
	return orderStatuses.includes(normalized as BdrProductionJobOrderStatus)
		? (normalized as BdrProductionJobOrderStatus)
		: 'not-started';
};

const parseOptionalNumber = (value: FormDataEntryValue | null) => {
	const normalized = String(value ?? '').replaceAll(',', '').trim();
	const parsed = Number.parseFloat(normalized);
	return Number.isFinite(parsed) && parsed > 0 ? parsed : undefined;
};

const normalizeChecklistInput = (formData: FormData) => {
	const submitted = new Set(formData.getAll('checklist').map((value) => String(value)));
	return checklistKeys.filter((key) => submitted.has(key));
};

const getJobId = (formData: FormData) => String(formData.get('jobId') ?? '').trim();

export const load = async ({ fetch, url, cookies }) => {
	const { requests } = await loadQuoteRequests(fetch);
	const billingSettings = await loadBdrBillingSettings(fetch, cookies.get(authTokenCookie));
	const lifecycleInvoices = await loadBdrInvoices(fetch);
	const jobs = await loadBdrScheduledJobs(fetch);
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
	scheduleReadyJob: async ({ request, fetch, cookies }) => {
		const formData = await request.formData();
		const invoiceId = String(formData.get('invoiceId') ?? '').trim();
		const scheduledDate = normalizeDateInput(formData.get('scheduledDate'));
		const windowStart = normalizeTimeInput(formData.get('windowStart'), '08:00');
		const windowEnd = normalizeTimeInput(formData.get('windowEnd'), '12:00');
		const crew = String(formData.get('crew') ?? '').trim();
		const notes = String(formData.get('scheduleNotes') ?? '').trim();
		if (!invoiceId) return fail(400, { jobActionMessage: 'Choose an invoice before creating a job.' });
		if (!scheduledDate) return fail(400, { jobActionMessage: 'Choose a target job date.' });
		if (!crew) return fail(400, { jobActionMessage: 'Assign a crew or scheduler before creating the job.' });

		const billingSettings = await loadBdrBillingSettings(fetch, cookies.get(authTokenCookie));
		const invoice = (await loadBdrInvoices(fetch)).find((record) => record.id === invoiceId);
		if (!invoice) return fail(404, { jobActionMessage: 'Invoice not found.' });
		if (invoice.state === 'draft') return fail(400, { jobActionMessage: 'Send the invoice before creating a job.' });
		const eligibility = getBdrInvoiceSchedulingEligibility(invoice, billingSettings);
		if (!eligibility.isReady) {
			const remainingDeposit = Math.max(0, eligibility.requiredDepositAmount - eligibility.amountPaid);
			return fail(400, {
				jobActionMessage: `Collect $${Math.ceil(remainingDeposit).toLocaleString()} more before creating this job.`
			});
		}
		const { requests } = await loadQuoteRequests(fetch);
		const requestRecord = requests.find((record) => record.id === invoice.sourceRequestId) ?? null;
		const job = await scheduleBdrJobFromInvoice(invoice, requestRecord, billingSettings, {
			scheduledDate,
			windowStart,
			windowEnd,
			crew,
			notes,
			scheduledBy: 'Office admin'
		}, fetch);
		return {
			jobActionMessage: `${job.siteName || job.customerName} is now a production job.`,
			selectedJobId: job.id
		};
	},
	updateStatus: async ({ request, fetch }) => {
		const formData = await request.formData();
		const jobId = getJobId(formData);
		const status = normalizeStatus(formData.get('status'));
		const note = String(formData.get('statusNote') ?? '').trim();
		if (!jobId) return fail(400, { jobActionMessage: 'Choose a job before changing status.' });
		const job = await updateBdrScheduledJobStatus(jobId, {
			status,
			note,
			actor: 'Office admin'
		}, fetch);
		if (!job) return fail(404, { jobActionMessage: 'Job not found.' });
		return {
			jobActionMessage: `${job.siteName || job.customerName} moved to ${status.replace('-', ' ')}.`,
			selectedJobId: job.id
		};
	},
	rescheduleJob: async ({ request, fetch }) => {
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
		}, fetch);
		if (!job) return fail(404, { jobActionMessage: 'Job not found.' });
		return {
			jobActionMessage: `${job.siteName || job.customerName} was rescheduled for ${job.scheduledDate}.`,
			selectedJobId: job.id
		};
	},
	updatePlanning: async ({ request, fetch }) => {
		const formData = await request.formData();
		const jobId = getJobId(formData);
		if (!jobId) return fail(400, { jobActionMessage: 'Choose a job before updating the plan.' });
		const job = await updateBdrScheduledJobPlanning(jobId, {
			customerConfirmationStatus: normalizeConfirmationStatus(formData.get('customerConfirmationStatus')),
			customerConfirmationNote: String(formData.get('customerConfirmationNote') ?? '').trim(),
			accessNotes: String(formData.get('accessNotes') ?? '').trim(),
			targetDate: normalizeDateInput(formData.get('targetDate')),
			prepDate: normalizeDateInput(formData.get('prepDate')),
			pourDate: normalizeDateInput(formData.get('pourDate')),
			cleanupDate: normalizeDateInput(formData.get('cleanupDate')),
			baseMaterialStatus: normalizeOrderStatus(formData.get('baseMaterialStatus')),
			baseMaterialSupplier: String(formData.get('baseMaterialSupplier') ?? '').trim(),
			baseMaterialDeliveryDate: normalizeDateInput(formData.get('baseMaterialDeliveryDate')),
			baseMaterialDeliveryWindow: String(formData.get('baseMaterialDeliveryWindow') ?? '').trim(),
			reinforcementStatus: normalizeOrderStatus(formData.get('reinforcementStatus')),
			reinforcementSupplier: String(formData.get('reinforcementSupplier') ?? '').trim(),
			equipmentStatus: normalizeOrderStatus(formData.get('equipmentStatus')),
			equipmentVendor: String(formData.get('equipmentVendor') ?? '').trim(),
			equipmentDeliveryDate: normalizeDateInput(formData.get('equipmentDeliveryDate')),
			equipmentDeliveryWindow: String(formData.get('equipmentDeliveryWindow') ?? '').trim(),
			concreteStatus: normalizeOrderStatus(formData.get('concreteStatus')),
			concreteSupplier: String(formData.get('concreteSupplier') ?? '').trim(),
			concreteDeliveryDate: normalizeDateInput(formData.get('concreteDeliveryDate')),
			concreteDeliveryWindow: String(formData.get('concreteDeliveryWindow') ?? '').trim(),
			concreteYards: parseOptionalNumber(formData.get('concreteYards')),
			concreteMix: String(formData.get('concreteMix') ?? '').trim(),
			pumpNeeded: formData.get('pumpNeeded') === 'on',
			materialNotes: String(formData.get('materialNotes') ?? '').trim(),
			checklist: normalizeChecklistInput(formData),
			actor: 'Office admin'
		}, fetch);
		if (!job) return fail(404, { jobActionMessage: 'Job not found.' });
		return {
			jobActionMessage: `${job.siteName || job.customerName} plan was updated.`,
			selectedJobId: job.id
		};
	},
	addJobNote: async ({ request, fetch }) => {
		const formData = await request.formData();
		const jobId = getJobId(formData);
		const note = String(formData.get('jobNote') ?? '').trim();
		if (!jobId) return fail(400, { jobActionMessage: 'Choose a job before adding a note.' });
		if (!note) return fail(400, { jobActionMessage: 'Add a note before saving.' });
		const job = await addBdrScheduledJobNote(jobId, {
			note,
			actor: 'Office admin'
		}, fetch);
		if (!job) return fail(404, { jobActionMessage: 'Job not found.' });
		return {
			jobActionMessage: `Note added to ${job.siteName || job.customerName}.`,
			selectedJobId: job.id
		};
	}
};
