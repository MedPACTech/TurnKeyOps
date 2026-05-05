import { resolveMvpScaffold } from '$lib/mvp';
import {
	syncApprovedEstimateInvoices,
	updateBdrInvoiceState
} from '$lib/server/bdr-invoices';
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

	return {
		source,
		invoices: snapshot.invoices,
		customers: snapshot.customers,
		approvedEstimateDrafts,
		lifecycleInvoices
	};
};

const getInvoiceId = async (request: Request) => {
	const formData = await request.formData();
	return String(formData.get('invoiceId') ?? '').trim();
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
		const invoiceId = await getInvoiceId(request);
		if (!invoiceId) return fail(400, { invoiceActionMessage: 'Choose an invoice first.' });
		const invoice = await updateBdrInvoiceState(invoiceId, { paid: true });
		if (!invoice) return fail(404, { invoiceActionMessage: 'Invoice not found.' });
		return { invoiceActionMessage: `${invoice.invoiceNumber} was marked paid.` };
	},
	sendReminder: async ({ request }) => {
		const invoiceId = await getInvoiceId(request);
		if (!invoiceId) return fail(400, { invoiceActionMessage: 'Choose an invoice first.' });
		const invoice = await updateBdrInvoiceState(invoiceId, { reminder: true });
		if (!invoice) return fail(404, { invoiceActionMessage: 'Invoice not found.' });
		return { invoiceActionMessage: `Reminder noted for ${invoice.invoiceNumber}.` };
	}
};
