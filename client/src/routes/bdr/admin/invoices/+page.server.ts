import { resolveMvpScaffold } from '$lib/mvp';
import {
	type BdrInvoicePaymentMethod,
	loadBdrInvoices,
	syncApprovedEstimateInvoices,
	updateBdrInvoiceState
} from '$lib/server/bdr-invoices';
import { loadBdrBillingSettings } from '$lib/server/bdr-billing-settings';
import {
	buildBdrScheduleReadyJobs,
	getBdrInvoiceSchedulingEligibility,
	loadBdrScheduledJobs,
	scheduleBdrJobFromInvoice
} from '$lib/server/bdr-job-scheduling';
import { loadQuoteRequests } from '$lib/server/quote-requests';
import { fail, redirect } from '@sveltejs/kit';

export const load = async ({ fetch }) => {
	const { snapshot, source } = await resolveMvpScaffold(fetch);
	const billingSettings = await loadBdrBillingSettings();
	const { requests } = await loadQuoteRequests(fetch);
	const lifecycleInvoices = await syncApprovedEstimateInvoices(fetch);
	const scheduledJobs = await loadBdrScheduledJobs(fetch);
	const scheduleReadyJobs = buildBdrScheduleReadyJobs(lifecycleInvoices, requests, billingSettings, scheduledJobs);

	return {
		source,
		invoices: snapshot.invoices,
		customers: snapshot.customers,
		lifecycleInvoices,
		billingSettings,
		scheduledJobs,
		scheduleReadyJobs
	};
};

const getInvoiceId = async (request: Request) => {
	const formData = await request.formData();
	return String(formData.get('invoiceId') ?? '').trim();
};

const normalizePaymentMethod = (value: string): BdrInvoicePaymentMethod => {
	if (value === 'Card' || value === 'Check' || value === 'Cash' || value === 'Other') return value;
	return 'ACH';
};

const parseMoneyInput = (value: FormDataEntryValue | null) => {
	const normalized = String(value ?? '').replaceAll(',', '').replace('$', '').trim();
	const amount = Number.parseFloat(normalized);
	return Number.isFinite(amount) ? amount : 0;
};

const normalizeDateInput = (value: FormDataEntryValue | null) => {
	const normalized = String(value ?? '').trim();
	return /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : '';
};

const normalizeTimeInput = (value: FormDataEntryValue | null, fallback: string) => {
	const normalized = String(value ?? '').trim();
	return /^\d{2}:\d{2}$/.test(normalized) ? normalized : fallback;
};

export const actions = {
	submitInvoice: async ({ request, fetch }) => {
		const invoiceId = await getInvoiceId(request);
		if (!invoiceId) return fail(400, { invoiceActionMessage: 'Choose an invoice first.' });
		const invoice = await updateBdrInvoiceState(invoiceId, { sent: true }, fetch);
		if (!invoice) return fail(404, { invoiceActionMessage: 'Invoice not found.' });
		return { invoiceActionMessage: `${invoice.invoiceNumber} was sent and moved to active invoices.` };
	},
	recordPayment: async ({ request, fetch }) => {
		const formData = await request.formData();
		const invoiceId = String(formData.get('invoiceId') ?? '').trim();
		const paymentAmount = parseMoneyInput(formData.get('paymentAmount'));
		const paymentMethod = normalizePaymentMethod(String(formData.get('paymentMethod') ?? 'ACH'));
		const paymentNote = String(formData.get('paymentNote') ?? '').trim();
		if (!invoiceId) return fail(400, { invoiceActionMessage: 'Choose an invoice first.' });
		if (paymentAmount <= 0) return fail(400, { invoiceActionMessage: 'Enter a payment amount greater than $0.' });
		const invoice = await updateBdrInvoiceState(invoiceId, {
			payment: {
				amount: paymentAmount,
				method: paymentMethod,
				note: paymentNote,
				receivedBy: 'Office admin'
			}
		}, fetch);
		if (!invoice) return fail(404, { invoiceActionMessage: 'Invoice not found.' });
		return { invoiceActionMessage: `${invoice.invoiceNumber} payment was recorded.` };
	},
	sendReminder: async ({ request, fetch }) => {
		const invoiceId = await getInvoiceId(request);
		if (!invoiceId) return fail(400, { invoiceActionMessage: 'Choose an invoice first.' });
		const invoice = await updateBdrInvoiceState(invoiceId, { reminder: true }, fetch);
		if (!invoice) return fail(404, { invoiceActionMessage: 'Invoice not found.' });
		return { invoiceActionMessage: `Reminder noted for ${invoice.invoiceNumber}.` };
	},
	scheduleJob: async ({ request, fetch }) => {
		const formData = await request.formData();
		const invoiceId = String(formData.get('invoiceId') ?? '').trim();
		const scheduledDate = normalizeDateInput(formData.get('scheduledDate'));
		const windowStart = normalizeTimeInput(formData.get('windowStart'), '08:00');
		const windowEnd = normalizeTimeInput(formData.get('windowEnd'), '12:00');
		const crew = String(formData.get('crew') ?? '').trim();
		const notes = String(formData.get('scheduleNotes') ?? '').trim();
		if (!invoiceId) return fail(400, { invoiceActionMessage: 'Choose an invoice first.' });
		if (!scheduledDate) return fail(400, { invoiceActionMessage: 'Choose a production date.' });
		if (!crew) return fail(400, { invoiceActionMessage: 'Assign a crew or scheduler before saving.' });

		const billingSettings = await loadBdrBillingSettings();
		const invoice = (await loadBdrInvoices(fetch)).find((record) => record.id === invoiceId);
		if (!invoice) return fail(404, { invoiceActionMessage: 'Invoice not found.' });
		if (invoice.state === 'draft') return fail(400, { invoiceActionMessage: 'Send the invoice before scheduling the job.' });
		const eligibility = getBdrInvoiceSchedulingEligibility(invoice, billingSettings);
		if (!eligibility.isReady) {
			const remainingDeposit = Math.max(0, eligibility.requiredDepositAmount - eligibility.amountPaid);
			return fail(400, {
				invoiceActionMessage: `Collect $${Math.ceil(remainingDeposit).toLocaleString()} more before scheduling this job.`
			});
		}
		const { requests } = await loadQuoteRequests(fetch);
		const requestRecord = requests.find((record) => record.id === invoice.sourceRequestId) ?? null;
		const scheduledJob = await scheduleBdrJobFromInvoice(invoice, requestRecord, billingSettings, {
			scheduledDate,
			windowStart,
			windowEnd,
			crew,
			notes,
			scheduledBy: 'Office admin'
		}, fetch);
		throw redirect(303, `/bdr/admin/jobs?job=${encodeURIComponent(scheduledJob.id)}`);
	}
};
