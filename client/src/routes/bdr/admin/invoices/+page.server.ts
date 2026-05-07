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
import { fail } from '@sveltejs/kit';

const fsModuleName = 'node:fs/promises';
const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';
const getDraftStorePaths = () => {
	const cwd = getCwd();
	return [
		`${cwd}/.svelte-kit/local-estimate-drafts.json`,
		`${cwd}/client/.svelte-kit/local-estimate-drafts.json`,
		`${cwd}/../client/.svelte-kit/local-estimate-drafts.json`
	];
};

type FsPromises = {
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
};

type EstimateDeliveryRecord = {
	status: 'sent' | 'approved' | 'changes-requested';
	method: 'review-link';
	reviewUrl: string;
	email: string;
	phone: string;
	sentAtUtc: string;
	approvedAtUtc?: string;
	changesRequestedAtUtc?: string;
	responseNote?: string;
};

type EstimateLocationRecord = {
	id: string;
	name: string;
	lengthFeet: number;
	widthFeet: number;
	depthInches: number;
	wastePercent: number;
	numberOfPours: number;
};

type EstimateDraftRecord = {
	requestId: string;
	revisionNumber: number;
	customerName: string;
	siteName: string;
	serviceSummary: string;
	visitFindings: string;
	scopeLineItems: string[];
	notes: string;
	assumptions: string[];
	status: 'draft' | 'ready-to-send' | 'sent';
	commercialSummary: string;
	locations: EstimateLocationRecord[];
	savedAtUtc: string;
	sentAtUtc?: string;
	sentBy?: string;
	delivery?: EstimateDeliveryRecord;
	revisionHistory: unknown[];
};

const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

const normalizeStringArray = (value: unknown) =>
	Array.isArray(value) ? value.map((entry) => String(entry).trim()).filter(Boolean) : [];

const normalizeEstimateDraft = (requestId: string, value: unknown): EstimateDraftRecord | null => {
	if (!value || typeof value !== 'object') return null;
	const record = value as Partial<EstimateDraftRecord>;
	const delivery =
		record.delivery?.status === 'sent' ||
		record.delivery?.status === 'approved' ||
		record.delivery?.status === 'changes-requested'
			? {
					status: record.delivery.status,
					method: 'review-link' as const,
					reviewUrl: String(record.delivery.reviewUrl ?? '').trim(),
					email: String(record.delivery.email ?? '').trim(),
					phone: String(record.delivery.phone ?? '').trim(),
					sentAtUtc: String(record.delivery.sentAtUtc ?? '').trim(),
					approvedAtUtc: record.delivery.approvedAtUtc?.trim() || undefined,
					changesRequestedAtUtc: record.delivery.changesRequestedAtUtc?.trim() || undefined,
					responseNote: record.delivery.responseNote?.trim() || undefined
				}
			: undefined;

	return {
		requestId,
		revisionNumber:
			typeof record.revisionNumber === 'number' && record.revisionNumber > 0 ? record.revisionNumber : 1,
		customerName: String(record.customerName ?? '').trim(),
		siteName: String(record.siteName ?? '').trim(),
		serviceSummary: String(record.serviceSummary ?? '').trim(),
		visitFindings: String(record.visitFindings ?? '').trim(),
		scopeLineItems: normalizeStringArray(record.scopeLineItems),
		notes: String(record.notes ?? '').trim(),
		assumptions: normalizeStringArray(record.assumptions),
		status: record.status === 'ready-to-send' || record.status === 'sent' ? record.status : 'draft',
		commercialSummary: String(record.commercialSummary ?? '').trim(),
		locations: Array.isArray(record.locations) ? record.locations : [],
		savedAtUtc: String(record.savedAtUtc ?? '').trim(),
		sentAtUtc: record.sentAtUtc?.trim() || undefined,
		sentBy: record.sentBy?.trim() || undefined,
		delivery,
		revisionHistory: Array.isArray(record.revisionHistory) ? record.revisionHistory : []
	};
};

const loadEstimateDrafts = async () => {
	const fs = await getFs();
	for (const draftStorePath of getDraftStorePaths()) {
		try {
			const raw = await fs.readFile(draftStorePath, 'utf-8');
			const parsed = JSON.parse(raw) as Record<string, unknown>;
			if (!parsed || typeof parsed !== 'object') return [];
			return Object.entries(parsed)
				.map(([requestId, record]) => normalizeEstimateDraft(requestId, record))
				.filter((record): record is EstimateDraftRecord => Boolean(record));
		} catch {
			// Try the next likely workspace root.
		}
	}
	return [];
};

export const load = async ({ fetch }) => {
	const { snapshot, source } = await resolveMvpScaffold(fetch);
	const billingSettings = await loadBdrBillingSettings();
	const { requests } = await loadQuoteRequests(fetch);
	const estimateDrafts = await loadEstimateDrafts();
	const approvedEstimateDrafts = estimateDrafts.filter((draft) => draft.delivery?.status === 'approved');
	const lifecycleInvoices = await syncApprovedEstimateInvoices(
		approvedEstimateDrafts.map((draft) => ({
			requestId: draft.requestId,
			revisionNumber: draft.revisionNumber,
			customerName: draft.customerName,
			siteName: draft.siteName,
			serviceSummary: draft.serviceSummary,
			scopeLineItems: draft.scopeLineItems,
			approvedAtUtc: draft.delivery?.approvedAtUtc,
			customerEmail: draft.delivery?.email ?? '',
			customerPhone: draft.delivery?.phone ?? '',
			reviewUrl: draft.delivery?.reviewUrl ?? `/bdr/estimate/${encodeURIComponent(draft.requestId)}`
		}))
	);
	const scheduledJobs = await loadBdrScheduledJobs();
	const scheduleReadyJobs = buildBdrScheduleReadyJobs(lifecycleInvoices, requests, billingSettings, scheduledJobs);

	return {
		source,
		invoices: snapshot.invoices,
		customers: snapshot.customers,
		approvedEstimateDrafts,
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
	submitInvoice: async ({ request }) => {
		const invoiceId = await getInvoiceId(request);
		if (!invoiceId) return fail(400, { invoiceActionMessage: 'Choose an invoice first.' });
		const invoice = await updateBdrInvoiceState(invoiceId, { sent: true });
		if (!invoice) return fail(404, { invoiceActionMessage: 'Invoice not found.' });
		return { invoiceActionMessage: `${invoice.invoiceNumber} was sent and moved to active invoices.` };
	},
	recordPayment: async ({ request }) => {
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
		});
		if (!invoice) return fail(404, { invoiceActionMessage: 'Invoice not found.' });
		return { invoiceActionMessage: `${invoice.invoiceNumber} payment was recorded.` };
	},
	sendReminder: async ({ request }) => {
		const invoiceId = await getInvoiceId(request);
		if (!invoiceId) return fail(400, { invoiceActionMessage: 'Choose an invoice first.' });
		const invoice = await updateBdrInvoiceState(invoiceId, { reminder: true });
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
		const invoice = (await loadBdrInvoices()).find((record) => record.id === invoiceId);
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
		});
		return {
			invoiceActionMessage: `${invoice.invoiceNumber} was scheduled for ${scheduledJob.scheduledDate}.`,
			scheduledJob
		};
	}
};
