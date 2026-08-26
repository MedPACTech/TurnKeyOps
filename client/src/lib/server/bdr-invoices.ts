import { getTurnKeyApiBaseUrl, getTurnKeyApiHeaders, unwrapTurnKeyApiEnvelope } from './turnkey-api';

export type BdrInvoiceState = 'draft' | 'sent' | 'paid';
export type BdrInvoicePaymentMethod = 'ACH' | 'Card' | 'Check' | 'Cash' | 'Other';

export type BdrInvoicePaymentRecord = {
	id: string;
	amount: number;
	method: BdrInvoicePaymentMethod;
	note?: string;
	receivedAtUtc: string;
	receivedBy: string;
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
	amountPaid: number;
	balanceDue: number;
	requiredDepositPercent: number;
	jobRelease: {
		isEligible: boolean;
		requiredDepositAmount: number;
		amountPaid: number;
		remainingDepositAmount: number;
		reason: string;
	};
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
	version: string;
};

type InvoiceApiPayment = {
	id: string;
	kind: string;
	status: string;
	amount: number;
	method: string;
	note?: string | null;
	occurredAtUtc: string;
	actor: string;
};

type InvoiceApiRecord = {
	id: string;
	quoteRequestId?: string | null;
	estimateId?: string | null;
	invoiceNumber: string;
	status: string;
	customerName?: string | null;
	siteName?: string | null;
	serviceSummary?: string | null;
	total: number;
	amountPaid: number;
	balanceDue: number;
	requiredDepositPercent: number;
	jobRelease: BdrInvoiceRecord['jobRelease'];
	customerEmail?: string | null;
	customerPhone?: string | null;
	reviewUrl?: string | null;
	scopeLineItems?: string[];
	lineItems?: Array<{ description: string; lineTotal: number }>;
	payments?: InvoiceApiPayment[];
	reminders?: Array<{ sentAtUtc: string }>;
	issueDate: string;
	paidDate?: string | null;
	sentAtUtc?: string | null;
	dateCreated?: string | null;
	dateUpdated?: string | null;
	version: string;
};

type InvoicePageEnvelope = {
	data: InvoiceApiRecord[];
	success: boolean;
	continuationToken?: string | null;
};

const api = (path: string, init?: RequestInit, fetcher: typeof globalThis.fetch = fetch) =>
	fetcher(`${getTurnKeyApiBaseUrl()}${path}`, {
		...init,
		headers: { ...getTurnKeyApiHeaders(init?.body !== undefined), ...(init?.headers ?? {}) }
	});

const number = (value: unknown) => (typeof value === 'number' && Number.isFinite(value) ? value : 0);

const stateFromStatus = (status: string): BdrInvoiceState => {
	const normalized = status.trim().toLowerCase();
	if (normalized === 'draft') return 'draft';
	if (normalized === 'paid') return 'paid';
	return 'sent';
};

const paymentMethod = (value: string): BdrInvoicePaymentMethod => {
	if (value === 'Card' || value === 'Check' || value === 'Cash' || value === 'Other') return value;
	return 'ACH';
};

const mapInvoice = (invoice: InvoiceApiRecord): BdrInvoiceRecord => {
	const sourceRequestId = invoice.quoteRequestId || invoice.estimateId || invoice.id;
	const successfulPayments = (invoice.payments ?? []).filter(
		(payment) => payment.kind.toLowerCase() === 'payment' && payment.status.toLowerCase() === 'succeeded'
	);
	const latestReminder = [...(invoice.reminders ?? [])]
		.sort((left, right) => right.sentAtUtc.localeCompare(left.sentAtUtc))[0];
	const createdAtUtc = invoice.dateCreated || invoice.issueDate;

	return {
		id: invoice.id,
		sourceRequestId,
		invoiceNumber: invoice.invoiceNumber,
		state: stateFromStatus(invoice.status),
		customerName: invoice.customerName ?? '',
		siteName: invoice.siteName ?? '',
		serviceSummary: invoice.serviceSummary ?? '',
		amount: number(invoice.total),
		amountPaid: number(invoice.amountPaid),
		balanceDue: number(invoice.balanceDue),
		requiredDepositPercent: number(invoice.requiredDepositPercent),
		jobRelease: invoice.jobRelease ?? {
			isEligible: false,
			requiredDepositAmount: 0,
			amountPaid: number(invoice.amountPaid),
			remainingDepositAmount: number(invoice.balanceDue),
			reason: 'Release eligibility was not returned by the API.'
		},
		customerEmail: invoice.customerEmail ?? '',
		customerPhone: invoice.customerPhone ?? '',
		reviewUrl: invoice.reviewUrl ?? '',
		approvedBy: invoice.customerName ?? '',
		approvalMethod: 'customer review link',
		createdAtUtc,
		updatedAtUtc: invoice.dateUpdated || createdAtUtc,
		sentAtUtc: invoice.sentAtUtc || undefined,
		paidAtUtc: invoice.paidDate || undefined,
		reminderSentAtUtc: latestReminder?.sentAtUtc,
		payments: successfulPayments.map((payment) => ({
			id: payment.id,
			amount: number(payment.amount),
			method: paymentMethod(payment.method),
			note: payment.note || undefined,
			receivedAtUtc: payment.occurredAtUtc,
			receivedBy: payment.actor
		})),
		lineItems:
			invoice.scopeLineItems?.length
				? invoice.scopeLineItems
				: (invoice.lineItems ?? []).map((line) => `${line.description} · $${number(line.lineTotal).toFixed(2)}`),
		version: invoice.version
	};
};

export const getBdrInvoiceAmountPaid = (
	invoice: Pick<BdrInvoiceRecord, 'amount' | 'payments' | 'state' | 'paidAtUtc'> & { amountPaid?: number }
) => {
	const paid = Number.isFinite(invoice.amountPaid)
		? number(invoice.amountPaid)
		: invoice.payments.reduce((sum, payment) => sum + number(payment.amount), 0);
	return Math.max(0, Math.min(paid, number(invoice.amount)));
};

export const getBdrInvoiceBalanceDue = (
	invoice: Pick<BdrInvoiceRecord, 'amount' | 'payments' | 'state' | 'paidAtUtc'> & {
		amountPaid?: number;
		balanceDue?: number;
	}
) =>
	Number.isFinite(invoice.balanceDue)
		? Math.max(0, number(invoice.balanceDue))
		: Math.max(0, number(invoice.amount) - getBdrInvoiceAmountPaid(invoice));

export const loadBdrInvoices = async (fetcher: typeof globalThis.fetch = fetch) => {
	const invoices: InvoiceApiRecord[] = [];
	let continuationToken: string | null = null;
	do {
		const query = new URLSearchParams({ pageSize: '100' });
		if (continuationToken) query.set('continuationToken', continuationToken);
		const response = await api(`/api/invoices/paged?${query}`, undefined, fetcher);
		if (!response.ok) throw new Error(`Load invoices failed with ${response.status}.`);
		const page = (await response.json()) as InvoicePageEnvelope;
		if (!page.success || !Array.isArray(page.data)) throw new Error('Load invoices returned an invalid response.');
		invoices.push(...page.data);
		continuationToken = page.continuationToken ?? null;
	} while (continuationToken);

	return invoices.map(mapInvoice);
};

export const syncApprovedEstimateInvoices = async (fetcher: typeof globalThis.fetch = fetch) => {
	const synced = await unwrapTurnKeyApiEnvelope<InvoiceApiRecord[]>(
		await api('/api/invoices/sync-approved-estimates', { method: 'POST' }, fetcher),
		'Sync approved estimates to invoices'
	);
	return synced.map(mapInvoice);
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
	},
	fetcher: typeof globalThis.fetch = fetch
) => {
	let current = await unwrapTurnKeyApiEnvelope<InvoiceApiRecord>(
		await api(`/api/invoices/${encodeURIComponent(invoiceId)}`, undefined, fetcher),
		'Load invoice'
	);

	if (update.sent || update.state === 'sent') {
		current = await unwrapTurnKeyApiEnvelope<InvoiceApiRecord>(
			await api(
				`/api/invoices/${encodeURIComponent(invoiceId)}/send`,
				{ method: 'POST', body: JSON.stringify({ expectedVersion: current.version }) },
				fetcher
			),
			'Send invoice'
		);
	}

	if (update.payment || update.paid || update.state === 'paid') {
		const requested = number(update.payment?.amount);
		const amount = requested > 0 ? requested : number(current.balanceDue);
		if (amount <= 0) throw new Error('The invoice has no remaining balance to record.');
		current = await unwrapTurnKeyApiEnvelope<InvoiceApiRecord>(
			await api(
				`/api/invoices/${encodeURIComponent(invoiceId)}/payments`,
				{
					method: 'POST',
					body: JSON.stringify({
						amount,
						method: update.payment?.method ?? 'ACH',
						note: update.payment?.note || null,
						idempotencyKey: `admin:${crypto.randomUUID()}`,
						expectedVersion: current.version,
						status: 'succeeded'
					})
				},
				fetcher
			),
			'Record invoice payment'
		);
	}

	if (update.reminder) {
		current = await unwrapTurnKeyApiEnvelope<InvoiceApiRecord>(
			await api(
				`/api/invoices/${encodeURIComponent(invoiceId)}/reminders`,
				{
					method: 'POST',
					body: JSON.stringify({
						channel: current.customerEmail ? 'email' : 'sms',
						idempotencyKey: `admin:${crypto.randomUUID()}`,
						expectedVersion: current.version
					})
				},
				fetcher
			),
			'Record invoice reminder'
		);
	}

	return mapInvoice(current);
};

export const getBdrInvoice = async (invoiceId: string, fetcher: typeof globalThis.fetch = fetch) => {
	const response = await api(`/api/invoices/${encodeURIComponent(invoiceId)}`, undefined, fetcher);
	if (response.status === 404) return null;
	return mapInvoice(await unwrapTurnKeyApiEnvelope<InvoiceApiRecord>(response, 'Load invoice'));
};
