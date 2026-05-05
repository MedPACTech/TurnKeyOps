const fsModuleName = 'node:fs/promises';

type FsPromises = {
	mkdir: (path: string, options: { recursive: boolean }) => Promise<unknown>;
	readFile: (path: string, encoding: 'utf-8') => Promise<string>;
	writeFile: (path: string, data: string) => Promise<unknown>;
};

export type BdrInvoiceState = 'draft' | 'sent' | 'paid';

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
	lineItems: string[];
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
		paidAtUtc: record.paidAtUtc?.trim() || undefined,
		reminderSentAtUtc: record.reminderSentAtUtc?.trim() || undefined,
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
	update: { state?: BdrInvoiceState; sent?: boolean; paid?: boolean; reminder?: boolean }
) => {
	const invoices = await loadBdrInvoices();
	const now = new Date().toISOString();
	const next = invoices.map((invoice) => {
		if (invoice.id !== invoiceId) return invoice;
		return {
			...invoice,
			state: update.state ?? (update.paid ? 'paid' : update.sent ? 'sent' : invoice.state),
			sentAtUtc: update.sent ? now : invoice.sentAtUtc,
			paidAtUtc: update.paid ? now : invoice.paidAtUtc,
			reminderSentAtUtc: update.reminder ? now : invoice.reminderSentAtUtc,
			updatedAtUtc: now
		};
	});
	await saveBdrInvoices(next);
	return next.find((invoice) => invoice.id === invoiceId) ?? null;
};

export const getBdrInvoice = async (invoiceId: string) =>
	(await loadBdrInvoices()).find((invoice) => invoice.id === invoiceId) ?? null;
