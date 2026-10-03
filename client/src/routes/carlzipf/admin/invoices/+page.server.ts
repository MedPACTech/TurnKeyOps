import { fail } from '@sveltejs/kit';
import { apiRequest, getApiBaseUrl } from '$lib/api/client';
import { sessionToken } from '../records.server';
import type { Actions, PageServerLoad } from './$types';

type AdminInvoice = {
	id: string;
	invoiceNumber: string;
	status: string;
	customerName?: string | null;
	siteName?: string | null;
	serviceSummary?: string | null;
	quoteRequestId?: string | null;
	estimateRevisionNumber: number;
	total: number;
	amountPaid: number;
	balanceDue: number;
	sentAtUtc?: string | null;
	completionSignature?: { signerPrintedName: string; signedAtUtc: string } | null;
	version: string;
};

type InvoicePage = { data: AdminInvoice[]; continuationToken?: string | null };

async function loadInvoices(event: Parameters<PageServerLoad>[0], token: string) {
	const invoices: AdminInvoice[] = [];
	let continuationToken: string | null = null;
	let pages = 0;
	do {
		const query = new URLSearchParams({ pageSize: '100' });
		if (continuationToken) query.set('continuationToken', continuationToken);
		const response = await event.fetch(`${getApiBaseUrl()}/api/invoices/paged?${query}`, { headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' } });
		const page = await response.json().catch(() => null) as InvoicePage & { success?: boolean } | null;
		if (!response.ok || page?.success !== true || !Array.isArray(page.data)) throw new Error('Invoice list was unavailable.');
		invoices.push(...page.data);
		continuationToken = page.continuationToken || null;
		pages++;
	} while (continuationToken && pages < 50);
	if (continuationToken) throw new Error('Invoice list exceeded the mobile page limit.');
	return invoices;
}

export const load: PageServerLoad = async (event) => {
	const token = sessionToken(event);
	event.setHeaders({ 'Cache-Control': 'private, no-store' });
	try {
		return { invoices: await loadInvoices(event, token), loadError: '' };
	} catch {
		return { invoices: [] as AdminInvoice[], loadError: 'Invoices could not be loaded. Reconnect and retry.' };
	}
};

export const actions: Actions = {
	sync: async (event) => {
		const token = sessionToken(event);
		try {
			const invoices = await apiRequest<AdminInvoice[]>('/api/invoices/sync-approved-estimates', { method: 'POST' }, event.fetch, token);
			return { message: `${invoices.length} approved quote invoice${invoices.length === 1 ? '' : 's'} checked. Existing invoices were reused.` };
		} catch (cause) {
			return fail(400, { error: cause instanceof Error ? cause.message : 'Invoices could not be created.' });
		}
	},
	activate: async (event) => {
		const token = sessionToken(event);
		const body = await event.request.formData();
		const id = String(body.get('invoiceId') ?? '');
		const expectedVersion = String(body.get('version') ?? '');
		if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(id) || !expectedVersion || body.get('reviewed') !== 'yes')
			return fail(400, { error: 'Review a current invoice before activating it.' });
		try {
			const invoice = await apiRequest<AdminInvoice>(`/api/invoices/${encodeURIComponent(id)}/send`, { method: 'POST', body: JSON.stringify({ expectedVersion }) }, event.fetch, token);
			return { message: `${invoice.invoiceNumber} is active for completion acknowledgement. No email or text was sent.` };
		} catch (cause) {
			return fail(400, { error: cause instanceof Error ? cause.message : 'Invoice status was not confirmed.' });
		}
	}
};
