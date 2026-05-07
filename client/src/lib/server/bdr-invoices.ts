const fsModuleName = 'node:fs/promises';

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
};

export type BdrInvoiceState = 'draft' | 'sent' | 'paid';
export type BdrInvoicePaymentMethod = 'ACH' | 'Card' | 'Check' | 'Cash' | 'Other';

export type ApprovedEstimateInvoiceInput = {
	requestId: string;
	revisionNumber: number;
	customerName: string;
	siteName: string;
	serviceSummary: string;
	scopeLineItems: string[];
	approvedAtUtc?: string;
	customerEmail: string;
	customerPhone: string;
	reviewUrl: string;
};

export type BdrInvoiceRecord = {
	id: string;
	sourceRequestId: string;
	invoiceNumber: string;
	state: BdrInvoiceState;
	customerName: string;
	siteName: string;
	serviceSummary: string;
	amount: number;
	customerEmail: string;
	customerPhone: string;
	reviewUrl: string;
	approvedBy: string;
	approvalMethod: 'customer review link';
	approvedAtUtc?: string;
	createdAtUtc: string;
	updatedAtUtc: string;
	sentAtUtc?: string;
	paidAtUtc?: string;
	reminderSentAtUtc?: string;
	payments: BdrInvoicePaymentRecord[];
	lineItems: string[];
};

export type BdrInvoicePaymentRecord = {
	id: string;
	amount: number;
	method: BdrInvoicePaymentMethod;
	note?: string;
	receivedAtUtc: string;
	receivedBy: string;
};

const getCwd = () =>
	(globalThis as typeof globalThis & { process?: { cwd: () => string } }).process?.cwd() ?? '.';

const getStoreDir = () => `${getCwd()}/.svelte-kit`;
const getStorePath = () => `${getStoreDir()}/local-bdr-invoices.json`;
const getFs = async () => (await import(/* @vite-ignore */ fsModuleName)) as FsPromises;

const normalizeState = (value: unknown): BdrInvoiceState => {
	if (value === 'sent' || value === 'paid') return value;
	return 'draft';
};

const parseEstimateTotal = (lineItems: string[]) => {
	const totalLine = [...lineItems]
		.reverse()
		.find((line) => line.toLowerCase().startsWith('estimated total'));
	const match = totalLine?.match(/\$([0-9,]+(?:\.\d{1,2})?)/);
	return match ? Number.parseFloat(match[1].replaceAll(',', '')) : 0;
};

const normalizePaymentMethod = (value: unknown): BdrInvoicePaymentMethod => {
	if (value === 'Card' || value === 'Check' || value === 'Cash' || value === 'Other') return value;
	return 'ACH';
};

const normalizePayment = (value: unknown): BdrInvoicePaymentRecord | null => {
	if (!value || typeof value !== 'object') return null;
	const record = value as Partial<BdrInvoicePaymentRecord>;
	const amount = typeof record.amount === 'number' && Number.isFinite(record.amount) ? record.amount : 0;
	if (amount <= 0) return null;

	return {
		id: String(record.id ?? `payment-${Date.now()}`).trim(),
		amount,
		method: normalizePaymentMethod(record.method),
		note: record.note?.trim() || undefined,
		receivedAtUtc: String(record.receivedAtUtc ?? '').trim() || new Date().toISOString(),
		receivedBy: String(record.receivedBy ?? 'Office admin').trim()
	};
};

export const getBdrInvoiceAmountPaid = (invoice: Pick<BdrInvoiceRecord, 'amount' | 'payments' | 'state' | 'paidAtUtc'>) => {
	const paymentTotal = invoice.payments.reduce((sum, payment) => sum + payment.amount, 0);
	if (paymentTotal > 0) return Math.min(paymentTotal, invoice.amount);
	if (invoice.state === 'paid' || invoice.paidAtUtc) return invoice.amount;
	return 0;
};

export const getBdrInvoiceBalanceDue = (invoice: Pick<BdrInvoiceRecord, 'amount' | 'payments' | 'state' | 'paidAtUtc'>) =>
	Math.max(invoice.amount - getBdrInvoiceAmountPaid(invoice), 0);

const normalizeInvoice = (value: unknown): BdrInvoiceRecord | null => {
	if (!value || typeof value !== 'object') return null;
	const record = value as Partial<BdrInvoiceRecord>;
	const id = String(record.id ?? '').trim();
	const sourceRequestId = String(record.sourceRequestId ?? '').trim();
	if (!id || !sourceRequestId) return null;

	const lineItems = Array.isArray(record.lineItems)
		? record.lineItems.map((line) => String(line).trim()).filter(Boolean)
		: [];
	const amount =
		typeof record.amount === 'number' && Number.isFinite(record.amount)
			? record.amount
			: parseEstimateTotal(lineItems);
	const now = new Date().toISOString();
	const payments = Array.isArray(record.payments)
		? record.payments.map(normalizePayment).filter((payment): payment is BdrInvoicePaymentRecord => Boolean(payment))
		: [];
	const paidAtUtc = record.paidAtUtc?.trim() || undefined;
	const normalizedPayments =
		payments.length || normalizeState(record.state) !== 'paid'
			? payments
			: [
					{
						id: `payment-${id}-paid`,
						amount,
						method: 'ACH' as const,
						note: 'Legacy paid invoice balance captured as collected.',
						receivedAtUtc: paidAtUtc ?? now,
						receivedBy: 'Office admin'
					}
				];

	return {
		id,
		sourceRequestId,
		invoiceNumber: String(record.invoiceNumber ?? `INV-${sourceRequestId.slice(0, 8).toUpperCase()}`).trim(),
		state: normalizeState(record.state),
		customerName: String(record.customerName ?? '').trim(),
		siteName: String(record.siteName ?? '').trim(),
		serviceSummary: String(record.serviceSummary ?? '').trim(),
		amount,
		customerEmail: String(record.customerEmail ?? '').trim(),
		customerPhone: String(record.customerPhone ?? '').trim(),
		reviewUrl: String(record.reviewUrl ?? '').trim(),
		approvedBy: String(record.approvedBy ?? record.customerName ?? '').trim(),
		approvalMethod: 'customer review link',
		approvedAtUtc: record.approvedAtUtc?.trim() || undefined,
		createdAtUtc: String(record.createdAtUtc ?? '').trim() || now,
		updatedAtUtc: String(record.updatedAtUtc ?? '').trim() || now,
		sentAtUtc: record.sentAtUtc?.trim() || undefined,
		paidAtUtc,
		reminderSentAtUtc: record.reminderSentAtUtc?.trim() || undefined,
		payments: normalizedPayments,
		lineItems
	};
};

export const loadBdrInvoices = async () => {
	try {
		const fs = await getFs();
		const raw = await fs.readFile(getStorePath(), 'utf-8');
		const parsed = JSON.parse(raw) as unknown[];
		if (!Array.isArray(parsed)) return [];
		return parsed.map(normalizeInvoice).filter((record): record is BdrInvoiceRecord => Boolean(record));
	} catch {
		return [];
	}
};

const saveBdrInvoices = async (invoices: BdrInvoiceRecord[]) => {
	const fs = await getFs();
	await fs.mkdir(getStoreDir(), { recursive: true });
	await fs.writeFile(getStorePath(), JSON.stringify(invoices, null, 2));
};

export const syncApprovedEstimateInvoices = async (approvedEstimates: ApprovedEstimateInvoiceInput[]) => {
	const existing = await loadBdrInvoices();
	const byRequestId = new Map(existing.map((invoice) => [invoice.sourceRequestId, invoice]));
	let changed = false;

	for (const estimate of approvedEstimates) {
		if (byRequestId.has(estimate.requestId)) continue;
		const now = new Date().toISOString();
		const invoice: BdrInvoiceRecord = {
			id: `invoice-${estimate.requestId}`,
			sourceRequestId: estimate.requestId,
			invoiceNumber: `INV-${estimate.requestId.slice(0, 8).toUpperCase()}`,
			state: 'draft',
			customerName: estimate.customerName,
			siteName: estimate.siteName,
			serviceSummary: estimate.serviceSummary,
			amount: parseEstimateTotal(estimate.scopeLineItems),
			customerEmail: estimate.customerEmail,
			customerPhone: estimate.customerPhone,
			reviewUrl: estimate.reviewUrl,
			approvedBy: estimate.customerName,
			approvalMethod: 'customer review link',
			approvedAtUtc: estimate.approvedAtUtc,
			createdAtUtc: now,
			updatedAtUtc: now,
			payments: [],
			lineItems: estimate.scopeLineItems
		};
		existing.unshift(invoice);
		byRequestId.set(estimate.requestId, invoice);
		changed = true;
	}

	if (changed) await saveBdrInvoices(existing);
	return existing;
};

export const updateBdrInvoiceState = async (
	invoiceId: string,
	update: {
		state?: BdrInvoiceState;
		sent?: boolean;
		paid?: boolean;
		reminder?: boolean;
		payment?: {
			amount?: number;
			method?: BdrInvoicePaymentMethod;
			note?: string;
			receivedBy?: string;
		};
	}
) => {
	const invoices = await loadBdrInvoices();
	const now = new Date().toISOString();
	const next = invoices.map((invoice) => {
		if (invoice.id !== invoiceId) return invoice;
		const balanceDue = getBdrInvoiceBalanceDue(invoice);
		const paymentAmount =
			typeof update.payment?.amount === 'number' && Number.isFinite(update.payment.amount)
				? Math.max(0, Math.min(update.payment.amount, balanceDue))
				: update.paid
					? balanceDue
					: 0;
		const payments =
			paymentAmount > 0
				? [
						...invoice.payments,
						{
							id: `payment-${now}`,
							amount: paymentAmount,
							method: update.payment?.method ?? 'ACH',
							note: update.payment?.note?.trim() || undefined,
							receivedAtUtc: now,
							receivedBy: update.payment?.receivedBy?.trim() || 'Office admin'
						}
					]
				: invoice.payments;
		const nextBalanceDue = Math.max(invoice.amount - payments.reduce((sum, payment) => sum + payment.amount, 0), 0);
		const isFullyPaid = nextBalanceDue <= 0.01 || update.paid;
		return {
			...invoice,
			state: update.state ?? (isFullyPaid ? 'paid' : update.sent ? 'sent' : invoice.state),
			sentAtUtc: update.sent ? now : invoice.sentAtUtc,
			paidAtUtc: isFullyPaid ? invoice.paidAtUtc ?? now : invoice.paidAtUtc,
			reminderSentAtUtc: update.reminder ? now : invoice.reminderSentAtUtc,
			payments,
			updatedAtUtc: now
		};
	});
	await saveBdrInvoices(next);
	return next.find((invoice) => invoice.id === invoiceId) ?? null;
};

export const getBdrInvoice = async (invoiceId: string) =>
	(await loadBdrInvoices()).find((invoice) => invoice.id === invoiceId) ?? null;
